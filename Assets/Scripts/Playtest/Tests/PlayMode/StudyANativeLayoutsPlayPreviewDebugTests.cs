using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FantasyRoyale.Playtest.OverheadStudyA;
using FantasyRoyale.Gameplay.Characters.Unity;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>Saved native layouts through the actual Play input/Actor/camera path; never saves scene changes.</summary>
    public sealed partial class StudyANativeLayoutsPlayPreviewDebugTests : InputTestFixture
    {
        const string Root = "Assets/Scenes/MapAuthoring/OverheadStudyA/NativeLocationLayouts-Exterior-Quality-v1-20261007/";
        [Serializable] public sealed class FrameRecord
        {
            public string Label, CapturePath;
            public bool IsPlaying, Focused, InputEnabled, WaitingForNeutral;
            public int Frame, FixedFrame, InputReadFrames, DepthUnits, DepthRefreshCount;
            public Vector2 Actor, LastMove;
            public Vector2 AdapterMove;
            public int AdapterReadFrames;
            public Vector3 Camera;
            public float CameraAspect, OrthographicSize;
        }
        [Serializable] public sealed class Report
        {
            public string Recipe, UnityVersion, GraphicsDevice, InputMethod, Qualification, Failure;
            public bool Completed;
            public List<FrameRecord> Frames = new List<FrameRecord>();
            public List<string> Checks = new List<string>();
            public List<string> Facts = new List<string>();
        }
        Keyboard keyboard;
        float previousVolume;
        bool previousAudioPause;
        Report report;
        string evidencePath;
        StudyANativeInputPreviewDebugDriver driver;

        [SetUp] public void MuteOwnedPlaySession()
        {
            previousVolume = AudioListener.volume; previousAudioPause = AudioListener.pause;
            AudioListener.volume = 0f; AudioListener.pause = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            evidencePath = Argument("-studyNativePlayEvidence");
            Assert.That(evidencePath, Is.Not.Null.And.Not.Empty, "Explicit evidence folder is required.");
            Assert.That(Path.IsPathRooted(evidencePath), Is.True);
            Directory.CreateDirectory(evidencePath);
        }

        [TearDown] public void RestoreOwnedPlaySession()
        {
            if (driver != null) { driver.Release(); UnityEngine.Object.Destroy(driver); driver = null; }
            AudioListener.volume = previousVolume; AudioListener.pause = previousAudioPause;
            if (report != null)
            {
                if (!report.Completed) report.Failure = TestContext.CurrentContext.Result.Message;
                File.WriteAllText(Path.Combine(evidencePath, report.Recipe + "-actual-play-receipt.json"), JsonUtility.ToJson(report, true));
            }
            report = null;
        }

        [UnityTest] public IEnumerator E_SavedLayoutRunsActualPlayInputAndCamera()
            => Verify("E", "Seed-0-fbdad6ba333c");
        [UnityTest] public IEnumerator S0_SavedLayoutRunsActualPlayInputAndCamera()
            => Verify("S0", "Seed-3-9b43d4858afc");

        IEnumerator Verify(string recipe, string folder)
        {
            report = new Report { Recipe = recipe, UnityVersion = Application.unityVersion,
                GraphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                InputMethod = "Actual Play input source is recorded after the platform focus check.",
                Qualification = "Actual Unity PlayMode using saved scenes, production Actor Physics and ordinary gameplay camera. Desktop/manual focus path remains unverified when hidden batch focus is false." };
            Assert.That(Application.isPlaying, Is.True);
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode(Root + folder + "/NativeEntry.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var diagnostic = UnityEngine.Object.FindAnyObjectByType<StudyABlockMovementDiagnostic>();
            Assert.That(diagnostic, Is.Not.Null);
            var deadline = Time.realtimeSinceStartup + 15f;
            while (!diagnostic.Ready && diagnostic.Failure.Length == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(diagnostic.Failure, Is.Empty);
            Assert.That(diagnostic.Ready, Is.True);
            Assert.That(diagnostic.MapScenePath, Is.EqualTo(Root + folder + "/NativeMap.unity"));
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty, "Unexpected audio source; stop this silent session.");
            Assert.That(AudioListener.volume, Is.EqualTo(0f)); Assert.That(AudioListener.pause, Is.True);
            var depth = diagnostic.GetComponent<StudyAIndependentStableDepthSort>();
            for (int i = 0; i < 5; i++) yield return null;
            Capture(diagnostic, depth, "start");
            Assert.That(depth.Failure, Is.Empty);
            Assert.That(depth.Applied, Is.True);
            Assert.That(depth.BoundUnitCount, Is.EqualTo(36));
            Assert.That(diagnostic.GameplayCamera.orthographicSize, Is.EqualTo(8f));
            Assert.That(diagnostic.Running, Is.True);
            if (!Application.isFocused)
            {
                Assert.That(diagnostic.InputEnabled, Is.False);
                report.Facts.Add("Original Diagnostic focus guard stopped its human input: focused=false/inputEnabled=false/inputReadFrames=" + diagnostic.InputReadFrames);
                var asset = UnityEngine.Object.Instantiate(diagnostic.InputActions);
                asset.name = "StudyNative_TestOnly_Input_Clone";
                var move = asset.FindAction("Player/Move", true);
                move.actionMap.devices = new InputDevice[] { keyboard };
                driver = diagnostic.gameObject.AddComponent<StudyANativeInputPreviewDebugDriver>();
                driver.Configure(diagnostic.Actor, asset);
                report.InputMethod = "Test-only Update driver after suspended Diagnostic -> original HumanCharacterMoveInputAdapter -> same production Actor.FixedUpdate -> actual Unity2D Physics; virtual Keyboard. Original focus-gated positive manual-input path unverified.";
            }
            else
            {
                Assert.That(diagnostic.InputEnabled, Is.True);
                report.InputMethod = "Virtual Keyboard -> normal Diagnostic Human adapter Update -> same Actor.FixedUpdate -> actual Unity2D Physics.";
            }
            var action = driver != null ? driver.Adapter.MoveAction : diagnostic.RuntimeInputActions.FindAction("Player/Move", true);
            action.actionMap.Disable(); action.actionMap.devices = new InputDevice[] { keyboard }; action.actionMap.Enable();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(action.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
            if (driver == null) Assert.That(diagnostic.WaitingForNeutral, Is.False);
            var before = diagnostic.Position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            for (int i = 0; i < 12; i++) { yield return new WaitForFixedUpdate(); yield return null; }
            Assert.That(driver != null ? driver.LastMove.x : diagnostic.LastHumanMove.x, Is.GreaterThan(.5f));
            Assert.That(diagnostic.Position.x, Is.GreaterThan(before.x + .4f));
            report.Facts.Add("Initial synthetic D input action=" + action.ReadValue<Vector2>() + "; actual command=" + diagnostic.Actor.MoveCommand.Horizontal + "," + diagnostic.Actor.MoveCommand.Vertical + "; body delta=" + (diagnostic.Position-before));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (int i = 0; i < 3; i++) { yield return new WaitForFixedUpdate(); yield return null; }
            Capture(diagnostic, depth, "after-right-input");
            Assert.That(diagnostic.LastHumanMove, Is.EqualTo(Vector2.zero));
            report.Checks.Add("Saved Entry loaded actual Map, production Actor, 36 foot-depth units and ordinary ortho8 camera in Play.");
            report.Checks.Add("Virtual D key passed through the recorded existing Human adapter Update path and moved actual Rigidbody2D; release stopped command.");
            yield return RunFullChecks(diagnostic, depth, recipe);
            report.Completed = true;
#else
            Assert.Ignore("Native diagnostic is Editor Play only.");
            yield break;
#endif
        }

        void QueueMove(Vector2 direction)
        {
            var keys = new List<Key>();
            if (direction.x > .1f) keys.Add(Key.D); if (direction.x < -.1f) keys.Add(Key.A);
            if (direction.y > .1f) keys.Add(Key.W); if (direction.y < -.1f) keys.Add(Key.S);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
            InputSystem.Update(); // Same explicit synthetic-device processing used by InputTestFixture.Press.
        }

        IEnumerator Drive(StudyABlockMovementDiagnostic diagnostic, Vector2 direction, float seconds)
        {
            QueueMove(direction);
            for(int i=0;i<2;i++) yield return null;
            var move = driver != null ? driver.LastMove : diagnostic.LastHumanMove;
            if(direction.sqrMagnitude > .1f)
                Assert.That(Vector2.Dot(move,direction.normalized), Is.GreaterThan(.9f), "Adapter Update must see the synthetic command before counting actual physics ticks.");
            else Assert.That(move, Is.EqualTo(Vector2.zero));
            var finish = diagnostic.FixedFrame + Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            while (diagnostic.FixedFrame < finish) { yield return new WaitForFixedUpdate(); yield return null; }
            QueueMove(Vector2.zero);
            yield return null;
            for (int i = 0; i < 3; i++) { yield return new WaitForFixedUpdate(); yield return null; }
            Assert.That(driver != null ? driver.LastMove : diagnostic.LastHumanMove, Is.EqualTo(Vector2.zero));
        }

        IEnumerator Place(StudyABlockMovementDiagnostic diagnostic, Vector2 position)
        {
            yield return Drive(diagnostic, Vector2.zero, .04f);
            diagnostic.Actor.Body.position = position;
            diagnostic.Actor.Body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            for(int i=0;i<3;i++) { yield return new WaitForFixedUpdate(); yield return null; }
            Assert.That(Vector2.Distance(diagnostic.Position, position), Is.LessThan(.05f), "Fixture start settled away from requested free point.");
        }

        void Capture(StudyABlockMovementDiagnostic diagnostic, StudyAIndependentStableDepthSort depth, string label)
        {
            var camera = diagnostic.GameplayCamera;
            Assert.That(camera, Is.Not.Null);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var height = 1024;
            var width = Mathf.RoundToInt(height * camera.aspect);
            var path = Path.Combine(evidencePath, report.Recipe + "-" + label + "-ordinary-camera.png");
            Assert.That(File.Exists(path), Is.False, "Capture overwrite is prohibited.");
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(texture);
            }
            report.Frames.Add(new FrameRecord { Label=label, CapturePath=path, IsPlaying=Application.isPlaying,
                Focused=Application.isFocused, InputEnabled=diagnostic.InputEnabled, WaitingForNeutral=diagnostic.WaitingForNeutral,
                Frame=diagnostic.Frame, FixedFrame=diagnostic.FixedFrame, InputReadFrames=diagnostic.InputReadFrames,
                Actor=diagnostic.Position, LastMove=diagnostic.LastHumanMove, Camera=camera.transform.position,
                AdapterMove=driver != null ? driver.LastMove : diagnostic.LastHumanMove,
                AdapterReadFrames=driver != null ? driver.ReadFrames : diagnostic.InputReadFrames,
                CameraAspect=camera.aspect, OrthographicSize=camera.orthographicSize,
                DepthUnits=depth.BoundUnitCount, DepthRefreshCount=depth.RefreshCount });
            File.WriteAllText(Path.Combine(evidencePath, report.Recipe + "-actual-play-receipt.json"), JsonUtility.ToJson(report,true));
        }

        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++) if(args[i]==key) return args[i+1];
            return null;
        }
    }

    [DefaultExecutionOrder(-50)]
    public sealed class StudyANativeInputPreviewDebugDriver : MonoBehaviour
    {
        public HumanCharacterMoveInputAdapter Adapter { get; private set; }
        public Vector2 LastMove { get; private set; }
        public int ReadFrames { get; private set; }
        CharacterActor2D actor;
        InputActionAsset asset;
        public void Configure(CharacterActor2D actualActor, InputActionAsset inputAsset)
        {
            actor=actualActor; asset=inputAsset;
            Adapter=new HumanCharacterMoveInputAdapter(asset.FindAction("Player/Move",true)); Adapter.Enable();
        }
        void Update()
        {
            if (Adapter == null || actor == null) return;
            var command=Adapter.ReadMoveCommand(); ReadFrames++;
            LastMove=new Vector2(command.Horizontal,command.Vertical); actor.SetMoveCommand(command);
        }
        public void Release()
        {
            if(actor != null) actor.SetMoveCommand(FantasyRoyale.Gameplay.Characters.CharacterMoveCommand.None);
            Adapter?.Dispose(); Adapter=null;
            if(asset != null) UnityEngine.Object.Destroy(asset); asset=null;
        }
        void OnDestroy() { Release(); }
    }
}
