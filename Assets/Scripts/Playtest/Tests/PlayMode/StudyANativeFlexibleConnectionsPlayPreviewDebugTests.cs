using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using FantasyRoyale.Playtest.OverheadStudyA;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>Fresh flexible candidates, actual production Actor physics and ordinary GPU camera; never saves scenes.</summary>
    public sealed class StudyANativeFlexibleConnectionsPlayPreviewDebugTests : InputTestFixture
    {
        const int PPU = 64;
        const string OutputPrefix = "Assets/Scenes/MapAuthoring/OverheadStudyA/NativeFlexibleConnectionPilot-v1-20261008/";
        [Serializable] public sealed class Generation { public bool Success; public Candidate[] Reports; }
        [Serializable] public sealed class Candidate
        {
            public bool Generated;
            public int ConnectorLengthPx, Seed, JOwners, JFootUnits;
            public string Error, FloorMaskSHA256, SoilMaskSHA256;
            public SavedScene Scene;
        }
        [Serializable] public sealed class SavedScene
        {
            public bool Generated;
            public string OutputRoot, MapScenePath, EntryScenePath, ContentHash, GeometryRecipeSHA256;
            public int SourceOwnerCount, MapFootUnitCount, WithHeroFootUnitCount;
            public Vector2 NativeSpawnWorld;
            public Rect FrameWorldBounds, PresentationFrameWorldBounds;
            public StudyANativeLocationAssembly Assembly;
        }
        [Serializable] public sealed class SavedGeometry
        {
            public int PixelsPerUnit;
            public RectInt BoundsPx;
            public RectInt[] FloorsPx, PhysicalPerimeterPx, VoidInsideFramePx;
            public Rect[] StaticBlockedRectanglesWorld, WaterRectanglesWorld;
        }
        [Serializable] public sealed class FrameRecord
        {
            public string Label, CapturePath;
            public int Frame, FixedFrame, AdapterReadFrames, OriginalInputReadFrames, DepthUnits;
            public bool IsPlaying, Focused, OriginalInputEnabled;
            public Vector2 Actor, AdapterMove;
            public Vector3 Camera;
            public float Aspect, Ortho;
        }
        [Serializable] public sealed class MovementRecord
        {
            public string Label;
            public Vector2 From, ExpectedTo, ActualTo;
            public int Waypoints, StartFixedFrame, EndFixedFrame;
            public float MaximumWaypointError;
        }
        [Serializable] public sealed class ContactRecord
        {
            public string Label, Collider;
            public Vector2 From, Direction, ActualTo;
            public int StartFixedFrame, EndFixedFrame;
            public float CastDistance, Progress, FreeDistance, ActualCapsuleDistance;
        }
        [Serializable] public sealed class Report
        {
            public bool Completed;
            public string Case, Failure, UnityVersion, GraphicsDevice, InputMethod, Qualification;
            public int Seed, ConnectorLengthPx, JOwners, JFootUnits;
            public string GenerationReceiptSHA256, FloorMaskSHA256, SoilMaskSHA256, GeometrySHA256, ContentHash;
            public List<string> Facts = new List<string>(), Checks = new List<string>();
            public List<FrameRecord> Frames = new List<FrameRecord>();
            public List<MovementRecord> Movement = new List<MovementRecord>();
            public List<ContactRecord> Contacts = new List<ContactRecord>();
        }
        sealed class Fixture
        {
            public Candidate Saved;
            public SavedGeometry Geometry;
            public StudyANativeLocationCatalog Catalog;
            public StudyANativePlacedLocation J;
            public byte[] Floor, Soil, UnionFloor;
            public float[] Centre;
        }
        Keyboard keyboard;
        StudyANativeInputPreviewDebugDriver driver;
        StudyABlockMovementDiagnostic diagnostic;
        StudyAIndependentStableDepthSort depth;
        StudyAIndependentNavigation nav;
        Fixture fixture;
        Report report;
        string evidence;
        float previousVolume;
        bool previousPause;
        bool ownsReport;
        Vector2 queued;

        [SetUp] public void PrepareSilentOwnedSession()
        {
            previousVolume = AudioListener.volume; previousPause = AudioListener.pause;
            AudioListener.volume = 0; AudioListener.pause = true;
            evidence = Argument("-studyFlexiblePlayEvidence");
            Assert.That(evidence, Is.Not.Null.And.Not.Empty);
            Assert.That(Path.IsPathRooted(evidence), Is.True);
            Directory.CreateDirectory(evidence);
            keyboard = InputSystem.AddDevice<Keyboard>(); queued = new Vector2(float.NaN, float.NaN);
            ownsReport = false;
        }
        [TearDown] public void RestoreSilentOwnedSession()
        {
            if (driver != null) { driver.Release(); UnityEngine.Object.Destroy(driver); driver = null; }
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            AudioListener.volume = previousVolume; AudioListener.pause = previousPause;
            if (report != null && ownsReport)
            {
                if (!report.Completed) report.Failure = TestContext.CurrentContext.Result.Message;
                WriteReport();
            }
            report = null; diagnostic = null; fixture = null; nav = null; depth = null;
        }
        [UnityTest] public IEnumerator J6u_ActualPlayUsesSavedFloorAndActor() => Verify(384);
        [UnityTest] public IEnumerator J4u_ActualPlayUsesSavedFloorAndActor() => Verify(256);
        [UnityTest] public IEnumerator J8u_ActualPlayUsesSavedFloorAndActor() => Verify(512);

        IEnumerator Verify(int length)
        {
            Assert.That(Application.isPlaying, Is.True);
            var generationPath = Path.Combine(Directory.GetParent(evidence).FullName, "flexible-generation.json");
            Assert.That(File.Exists(generationPath), Is.True, "Play needs the actual successful generation receipt next to its evidence directory.");
            var generation = JsonUtility.FromJson<Generation>(File.ReadAllText(generationPath));
            Assert.That(generation.Success, Is.True); Assert.That(generation.Reports.Length, Is.EqualTo(3));
            var saved = generation.Reports.Single(r => r.ConnectorLengthPx == length);
            Assert.That(saved.Generated && saved.Scene.Generated, Is.True, saved.Error);
            Assert.That(saved.Scene.OutputRoot.StartsWith(OutputPrefix, StringComparison.Ordinal), Is.True);
            Assert.That(saved.Scene.MapScenePath, Is.EqualTo(saved.Scene.OutputRoot + "/NativeMap.unity"));
            Assert.That(saved.Scene.EntryScenePath, Is.EqualTo(saved.Scene.OutputRoot + "/NativeEntry.unity"));
            report = new Report { Case = "J-" + length / PPU + "u-Seed-" + saved.Seed,
                Seed = saved.Seed, ConnectorLengthPx = length, JOwners = saved.JOwners, JFootUnits = saved.JFootUnits,
                UnityVersion = Application.unityVersion, GraphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                GenerationReceiptSHA256 = Hash(File.ReadAllBytes(generationPath)), ContentHash = saved.Scene.ContentHash,
                Qualification = "Actual Play production Actor/Physics and ordinary camera. Fixture relocation is setup only. Hidden focus=false uses an owned virtual Keyboard and existing adapter driver; original focus-positive desktop input and initial target-image match remain unverified." };
            Assert.That(File.Exists(ReportPath()), Is.False, "Use a fresh evidence directory; do not overwrite previous Play evidence.");
            using (var stream = new FileStream(ReportPath(), FileMode.CreateNew, FileAccess.Write)) { }
            ownsReport = true;
            fixture = ReadFixture(saved);
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(saved.Scene.EntryScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            diagnostic = UnityEngine.Object.FindAnyObjectByType<StudyABlockMovementDiagnostic>();
            Assert.That(diagnostic, Is.Not.Null);
            var deadline = Time.realtimeSinceStartup + 20;
            while (!diagnostic.Ready && diagnostic.Failure.Length == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(diagnostic.Ready, Is.True); Assert.That(diagnostic.Failure, Is.Empty);
            Assert.That(diagnostic.MapScenePath, Is.EqualTo(saved.Scene.MapScenePath));
            Assert.That(diagnostic.UseHumanInput && !diagnostic.AutoTraverse && diagnostic.Running, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include), Is.Empty);
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include), Is.Empty);
            Assert.That(AudioListener.volume, Is.EqualTo(0)); Assert.That(AudioListener.pause, Is.True);
            depth = diagnostic.GetComponent<StudyAIndependentStableDepthSort>();
            var mapRoot = diagnostic.Actor.gameObject.scene.GetRootGameObjects().Single(g => g.name == "MapRoot");
            nav = mapRoot.GetComponent<StudyAIndependentNavigation>();
            Assert.That(nav, Is.Not.Null); Assert.That(nav.Failure, Is.Empty);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.That(depth.Applied, Is.True); Assert.That(depth.Failure, Is.Empty);
            Assert.That(depth.BoundUnitCount, Is.EqualTo(13 + saved.JFootUnits));
            Assert.That(saved.Scene.WithHeroFootUnitCount, Is.EqualTo(13 + saved.JFootUnits));
            Assert.That(nav.Owners.Length, Is.EqualTo(16 + saved.JOwners));
            Assert.That(saved.Scene.SourceOwnerCount, Is.EqualTo(nav.Owners.Length));
            Assert.That(saved.Scene.MapFootUnitCount, Is.EqualTo(12 + saved.JFootUnits));
            Assert.That(nav.Owners.Select(o => o.ObjectId), Is.EquivalentTo(fixture.Catalog.Locations.SelectMany(l => l.SourceOwnerIds)));
            Assert.That(mapRoot.GetComponentsInChildren<StudyAIndependentWaterAnimation>(true), Is.Empty, "A and its water are not selected by this pilot.");
            Assert.That(nav.StaticBlockedRectanglesWorld, Is.EqualTo(fixture.Geometry.StaticBlockedRectanglesWorld));
            Assert.That(nav.FrameWorldBounds, Is.EqualTo(saved.Scene.FrameWorldBounds));
            Assert.That(diagnostic.CameraMapBounds, Is.EqualTo(saved.Scene.PresentationFrameWorldBounds));
            Assert.That(diagnostic.GameplayCamera.orthographicSize, Is.EqualTo(8));
            Assert.That(diagnostic.GameplayCamera.transform.position.z, Is.EqualTo(-10));
            Assert.That(Quaternion.Angle(diagnostic.Actor.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(diagnostic.Actor.transform.lossyScale, Vector3.one), Is.LessThan(.001f));
            var originalCore = CoreSourceAndTransforms(mapRoot);
            var ownerStates = nav.Owners.ToDictionary(o => o, o => o.Present);
            ConfigureInput();
            yield return Acknowledge(Vector2.zero);
            Capture("start"); CameraCheck();
            report.Facts.Add("Camera actual floorBounds=" + saved.Scene.FrameWorldBounds + ", presentation/followBounds=" + diagnostic.CameraMapBounds +
                ", aspect=" + diagnostic.GameplayCamera.aspect + ", ortho=" + diagnostic.GameplayCamera.orthographicSize + "; clamp and1/32 snap derived from actual presentation bounds, never an old fixed delta threshold.");
            report.Checks.Add("Actual saved Map/Entry, selected core16 owners/12 foot plus reported J additions, original transforms and presentation-camera bounds loaded.");
            try
            {
                yield return FullCentrelineBothWays();
                yield return SharedSeamLanes();
                yield return CurvedBoundaryContacts();
                yield return CornerContact();
                yield return JOwnerStateChecks();
                yield return TreeDepthPixelControls();
            }
            finally
            {
                foreach (var state in ownerStates) if (state.Key != null) state.Key.SetPresent(state.Value);
                Physics2D.SyncTransforms();
            }
            Assert.That(CoreSourceAndTransforms(mapRoot), Is.EqualTo(originalCore), "Original core owner/foot transforms changed during the new fixture.");
            Assert.That(depth.BoundUnitCount, Is.EqualTo(13 + saved.JFootUnits));
            Assert.That(report.Contacts.Count, Is.GreaterThanOrEqualTo(13));
            Assert.That(diagnostic.Failure, Is.Empty); Assert.That(nav.Failure, Is.Empty); Assert.That(depth.Failure, Is.Empty);
            report.Checks.Add("All three graph edges crossed both directions, J actual-mask boundary13 contacts, J present-state linkage, production camera clamp/snap and same-frame Hero depth GPU controls verified.");
            report.Completed = true; WriteReport();
#else
            Assert.Ignore("Saved native diagnostic supports Editor Play only."); yield break;
#endif
        }

        Fixture ReadFixture(Candidate saved)
        {
            var directory = Project(saved.Scene.OutputRoot);
            var floor = File.ReadAllBytes(Path.Combine(directory, "J-floor-mask.bin"));
            var soil = File.ReadAllBytes(Path.Combine(directory, "J-soil-mask.bin"));
            Assert.That(floor.Length, Is.EqualTo(saved.ConnectorLengthPx * 384)); Assert.That(soil.Length, Is.EqualTo(floor.Length));
            Assert.That(floor.All(v => v <= 1) && soil.All(v => v <= 1), Is.True);
            Assert.That(Hash(floor), Is.EqualTo(saved.FloorMaskSHA256)); Assert.That(Hash(soil), Is.EqualTo(saved.SoilMaskSHA256));
            var geometryBytes = File.ReadAllBytes(Path.Combine(directory, "geometry-recipe.json"));
            Assert.That(Hash(geometryBytes), Is.EqualTo(saved.Scene.GeometryRecipeSHA256));
            var geometry = JsonUtility.FromJson<SavedGeometry>(System.Text.Encoding.UTF8.GetString(geometryBytes));
            var catalog = JsonUtility.FromJson<StudyANativeLocationCatalog>(File.ReadAllText(Path.Combine(directory, "resolved-catalog.json")));
            Assert.That(geometry.PixelsPerUnit, Is.EqualTo(PPU)); Assert.That(geometry.WaterRectanglesWorld, Is.Empty);
            Assert.That(catalog.Locations.Select(l => l.Id), Is.EquivalentTo(new[] { "B", "J", "D", "C" }));
            var j = saved.Scene.Assembly.Blocks.Single(b => b.LocationId == "J");
            Assert.That(j.ExtentPx, Is.EqualTo(new Vector2Int(saved.ConnectorLengthPx, 384)));
            var union = new byte[geometry.BoundsPx.width * geometry.BoundsPx.height];
            foreach (var rectangle in geometry.FloorsPx)
            {
                Assert.That(rectangle.xMin >= geometry.BoundsPx.xMin && rectangle.yMin >= geometry.BoundsPx.yMin &&
                    rectangle.xMax <= geometry.BoundsPx.xMax && rectangle.yMax <= geometry.BoundsPx.yMax, Is.True);
                for (var y = rectangle.yMin; y < rectangle.yMax; y++) for (var x = rectangle.xMin; x < rectangle.xMax; x++)
                    union[x - geometry.BoundsPx.x + (y - geometry.BoundsPx.y) * geometry.BoundsPx.width] = 1;
            }
            var mismatches = 0;
            for (var y = 0; y < j.ExtentPx.y; y++) for (var x = 0; x < j.ExtentPx.x; x++)
                if (union[j.OriginPx.x + x - geometry.BoundsPx.x + (j.OriginPx.y + y - geometry.BoundsPx.y) * geometry.BoundsPx.width] != floor[x + y * j.ExtentPx.x]) mismatches++;
            Assert.That(mismatches, Is.EqualTo(0), "Saved geometry floor differs from the actual J mask.");
            var center = new float[saved.ConnectorLengthPx];
            for (var x = 0; x < center.Length; x++)
            {
                var rows = Enumerable.Range(0, 384).Where(y => soil[x + y * center.Length] != 0).ToArray();
                Assert.That(rows.Length, Is.GreaterThanOrEqualTo(64)); center[x] = rows.Average(y => y + .5f);
                Assert.That(rows.All(y => floor[x + y * center.Length] == 1), Is.True);
            }
            report.FloorMaskSHA256 = Hash(floor); report.SoilMaskSHA256 = Hash(soil); report.GeometrySHA256 = Hash(geometryBytes);
            return new Fixture { Saved = saved, Geometry = geometry, Catalog = catalog, J = j, Floor = floor, Soil = soil, UnionFloor = union, Centre = center };
        }
        void ConfigureInput()
        {
            if (!Application.isFocused)
            {
                Assert.That(diagnostic.InputEnabled, Is.False);
                var asset = UnityEngine.Object.Instantiate(diagnostic.InputActions);
                asset.name = "FlexibleNative_TestOwned_Input_Clone";
                asset.FindAction("Player/Move", true).actionMap.devices = new InputDevice[] { keyboard };
                driver = diagnostic.gameObject.AddComponent<StudyANativeInputPreviewDebugDriver>(); driver.Configure(diagnostic.Actor, asset);
                report.InputMethod = "focus=false: existing public test-only Update driver -> original HumanCharacterMoveInputAdapter -> production Actor.FixedUpdate/Rigidbody2D/Physics2D; owned Keyboard. Original focus guard retained; positive desktop path unverified.";
            }
            else report.InputMethod = "focus=true: owned virtual Keyboard -> original Diagnostic Human adapter -> production Actor.FixedUpdate/Physics2D; real desktop key operation unverified.";
            var action = driver != null ? driver.Adapter.MoveAction : diagnostic.RuntimeInputActions.FindAction("Player/Move", true);
            action.actionMap.Disable(); action.actionMap.devices = new InputDevice[] { keyboard }; action.actionMap.Enable();
            report.Facts.Add("Input initial Focus=" + Application.isFocused + ", originalReadFrames=" + diagnostic.InputReadFrames + "; fixture placements are setup, all traversal/contact outcomes use subsequent actual normal fixed ticks.");
        }
        Vector2 ObservedMove => driver != null ? driver.LastMove : diagnostic.LastHumanMove;
        int Reads => driver != null ? driver.ReadFrames : diagnostic.InputReadFrames;
        static Vector2 Digital(Vector2 delta)
        {
            if (delta.sqrMagnitude < .00001f) return Vector2.zero;
            var angle = Mathf.Round(Mathf.Atan2(delta.y, delta.x) / (Mathf.PI / 4)) * (Mathf.PI / 4);
            return new Vector2(Mathf.Round(Mathf.Cos(angle)), Mathf.Round(Mathf.Sin(angle))).normalized;
        }
        void Queue(Vector2 direction)
        {
            var keys = new List<Key>(); if (direction.x > .1f) keys.Add(Key.D); if (direction.x < -.1f) keys.Add(Key.A);
            if (direction.y > .1f) keys.Add(Key.W); if (direction.y < -.1f) keys.Add(Key.S);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray())); InputSystem.Update(); queued = direction;
        }
        IEnumerator Acknowledge(Vector2 direction)
        {
            Queue(direction); var reads = Reads; var deadline = Time.realtimeSinceStartup + 10;
            do { yield return null; } while ((Reads <= reads || Vector2.Distance(ObservedMove, direction) > .02f) && Time.realtimeSinceStartup < deadline);
            Assert.That(Reads, Is.GreaterThan(reads), "Existing adapter Update did not acknowledge input.");
            Assert.That(Vector2.Distance(ObservedMove, direction), Is.LessThan(.02f));
        }
        IEnumerator Ticks(int count)
        {
            var target = diagnostic.FixedFrame + count; var deadline = Time.realtimeSinceStartup + 20;
            while (diagnostic.FixedFrame < target && Time.realtimeSinceStartup < deadline) { yield return new WaitForFixedUpdate(); yield return null; }
            Assert.That(diagnostic.FixedFrame, Is.GreaterThanOrEqualTo(target), "Actual runtime FixedFrame failed to advance.");
        }
        IEnumerator Stop() { yield return Acknowledge(Vector2.zero); yield return Ticks(2); }
        IEnumerator Place(Vector2 point)
        {
            yield return Stop(); Assert.That(Clear(point), Is.True, "Fixture start must fit actual floor and real capsule clearance: " + point);
            diagnostic.Actor.Body.position = point; diagnostic.Actor.Body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            yield return Ticks(3); Assert.That(Vector2.Distance(diagnostic.Position, point), Is.LessThan(.06f));
        }
        IEnumerator Follow(Vector2[] path, string label, bool captureMiddle = false)
        {
            Assert.That(path.Length, Is.GreaterThan(1));
            foreach (var p in path) Assert.That(FloorBody(p), Is.True, label + " saved route leaves actual floor");
            var from = diagnostic.Position; var fixedStart = diagnostic.FixedFrame; var deadline = Time.realtimeSinceStartup + 90;
            var index = 1; var maximum = 0f; var captured = false;
            while (index < path.Length && Time.realtimeSinceStartup < deadline)
            {
                var error = Vector2.Distance(diagnostic.Position, path[index]);
                if (error <= .16f) { maximum = Mathf.Max(maximum, error); index++; continue; }
                var command = Digital(path[index] - diagnostic.Position);
                if (Vector2.Distance(command, queued) > .01f) yield return Acknowledge(command);
                yield return Ticks(1);
                Assert.That(FloorBody(diagnostic.Position), Is.True, label + " real Actor left floor");
                Assert.That(Overlaps(diagnostic.Position).All(c => Physics2D.Distance(diagnostic.Actor.FootCollider, c).distance >= -.03f), Is.True, label + " penetrated body");
                if (captureMiddle && !captured && index >= path.Length / 2) { yield return Stop(); Capture(label + "-mid-J"); captured = true; }
            }
            Assert.That(index, Is.EqualTo(path.Length), label + " actual Actor could not follow saved dense route");
            yield return Stop(); Assert.That(Vector2.Distance(diagnostic.Position, path[path.Length - 1]), Is.LessThan(.23f));
            CameraCheck(); // Neutral + actual fixed ticks lets the unchanged LateUpdate camera settle.
            Assert.That(diagnostic.FixedFrame, Is.GreaterThan(fixedStart));
            report.Movement.Add(new MovementRecord { Label = label, From = from, ExpectedTo = path[path.Length - 1], ActualTo = diagnostic.Position,
                Waypoints = path.Length, StartFixedFrame = fixedStart, EndFixedFrame = diagnostic.FixedFrame, MaximumWaypointError = maximum });
        }
        Vector2 Port(string id)
        {
            foreach (var block in fixture.Saved.Scene.Assembly.Blocks)
            {
                var definition = fixture.Catalog.Locations.Single(l => l.Id == block.LocationId);
                var port = definition.Ports.SingleOrDefault(p => p.Id == id);
                if (port != null) return ((Vector2)block.OriginPx + port.AnchorPx) / PPU;
            }
            Assert.Fail("Missing saved port " + id); return Vector2.zero;
        }
        Vector2[] Dense(params Vector2[] controls)
        {
            var result = new List<Vector2> { controls[0] };
            for (var i = 1; i < controls.Length; i++)
            {
                var n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(controls[i - 1], controls[i]) * PPU / 8));
                for (var step = 1; step <= n; step++) result.Add(Vector2.Lerp(controls[i - 1], controls[i], step / (float)n));
            }
            return result.ToArray();
        }
        IEnumerator FullCentrelineBothWays()
        {
            var points = new List<Vector2> { fixture.Saved.Scene.NativeSpawnWorld, Port("B.E.road") };
            for (var x = 0; x < fixture.J.ExtentPx.x; x += 8) points.Add(((Vector2)fixture.J.OriginPx + new Vector2(x + .5f, fixture.Centre[x])) / PPU);
            points.Add(Port("J.E.road")); points.Add(Port("D.W.road") + Vector2.right * .75f);
            var path = Dense(points.ToArray()); yield return Place(path[0]);
            yield return Follow(path, "B-J-D-forward", true); Capture("connection-J-D-forward");
            yield return Follow(path.Reverse().ToArray(), "D-J-B-reverse", true); Capture("connection-B-J-reverse");
            report.Checks.Add("Saved soil centreline sampled at <=8 native pixels; real Actor followed the entire B-J-D curve both directions without teleporting between waypoints.");
        }
        IEnumerator SharedSeamLanes()
        {
            var names = new[] { "B-J", "J-D", "D-C" };
            var ports = new[] { Port("B.E.road"), Port("J.E.road"), Port("D.S.road") };
            for (var i = 0; i < names.Length; i++)
            {
                var direction = i == 2 ? Vector2.down : Vector2.right; var tangent = new Vector2(-direction.y, direction.x);
                foreach (var lane in new[] { -.125f, 0, .125f })
                {
                    var start = ports[i] - direction * .55f + tangent * lane; var end = ports[i] + direction * .55f + tangent * lane;
                    Assert.That(Clear(start) && Clear(end), Is.True, names[i] + " lane actual capsule clearance");
                    var path = Dense(start, end); yield return Place(start);
                    var tag = names[i] + "-lane-" + Mathf.RoundToInt(lane * 64);
                    yield return Follow(path, tag + "-forward");
                    if (lane == 0) Capture("connection-" + names[i]);
                    yield return Follow(path.Reverse().ToArray(), tag + "-reverse");
                }
            }
            report.Checks.Add("B-J, J-D and D-C each crossed forward/reverse in three reserved-mouth lanes with real Actor physics.");
        }
        IEnumerator CurvedBoundaryContacts()
        {
            var successful = new HashSet<string>();
            foreach (var candidate in BoundaryCandidates())
            {
                if (successful.Contains(candidate.Label) || !Clear(candidate.Start)) continue;
                var hit = FirstHit(candidate.Start, candidate.Direction, 2);
                if (hit.collider == null || hit.collider.name != "NativePerimeter" || hit.distance < .1f) continue;
                yield return Place(candidate.Start); yield return Block(candidate.Direction, hit, candidate.Label);
                successful.Add(candidate.Label);
                if (candidate.Label == "J-upper-2") Capture("J-curve-upper-edge-block");
            }
            Assert.That(successful.Count, Is.EqualTo(12), "Every upper/lower curved boundary region needs an actual contact; none can be silently skipped.");
        }
        sealed class BoundaryCandidate { public string Label; public Vector2 Start, Direction; }
        IEnumerable<BoundaryCandidate> BoundaryCandidates()
        {
            var width = fixture.J.ExtentPx.x; var fractions = new[] { .05f, .2f, .4f, .6f, .8f, .95f };
            var shifts = new[] { 0, 4, -4, 8, -8, 12, -12, 16, -16, 24, -24, 32, -32 };
            for (var region = 0; region < 6; region++) for (var side = 0; side < 2; side++) foreach (var shift in shifts)
            {
                var x = 32 + Mathf.RoundToInt((width - 65) * fractions[region]) + shift; if (x < 32 || x >= width - 32) continue;
                var rows = Enumerable.Range(0, 384).Where(y => fixture.Floor[x + y * width] == 1).ToArray();
                Assert.That(rows.Length, Is.GreaterThan(0)); Assert.That(rows.Length, Is.EqualTo(rows.Last() - rows.First() + 1));
                var outward = side == 0 ? Vector2.up : Vector2.down;
                var boundary = ((Vector2)fixture.J.OriginPx + new Vector2(x + .5f, side == 0 ? rows.Last() + 1 : rows.First())) / PPU;
                var center = boundary - outward * (diagnostic.Actor.FootCollider.size.y * .5f + .6f);
                yield return new BoundaryCandidate { Label = "J-" + (side == 0 ? "upper-" : "lower-") + region,
                    Start = center - diagnostic.Actor.FootCollider.offset, Direction = outward };
            }
        }
        IEnumerator CornerContact()
        {
            var c = fixture.Saved.Scene.Assembly.Blocks.Single(b => b.LocationId == "C");
            var corner = ((Vector2)c.OriginPx + new Vector2(0, c.ExtentPx.y)) / PPU;
            var direction = new Vector2(-1, -1).normalized;
            var found = false;
            foreach (var distance in new[] { .7f, .9f, 1.1f })
            {
                var start = corner - direction * distance - diagnostic.Actor.FootCollider.offset;
                if (!Clear(start)) continue;
                var hit = FirstHit(start, direction, 2);
                if (hit.collider == null || hit.collider.name != "NativePerimeter" || hit.distance < .1f) continue;
                yield return Place(start); yield return Block(direction, hit, "J-C-concave-corner"); Capture("J-C-corner-block"); found = true; break;
            }
            Assert.That(found, Is.True, "J/C corner needs an actual-capsule approach and real stop.");
        }
        IEnumerator Block(Vector2 direction, RaycastHit2D hit, string label)
        {
            var from = diagnostic.Position; var frame = diagnostic.FixedFrame;
            var seconds = Mathf.Max(.25f, (hit.distance + .3f) / diagnostic.Actor.MovementSpeed);
            yield return Acknowledge(direction); yield return Ticks(Mathf.CeilToInt(seconds / Time.fixedDeltaTime)); yield return Stop();
            var progress = Vector2.Dot(diagnostic.Position - from, direction);
            Assert.That(progress, Is.GreaterThan(.02f)); Assert.That(progress, Is.LessThan(diagnostic.Actor.MovementSpeed * seconds - .1f));
            var walls = nav.GetComponentsInChildren<Collider2D>().Where(IsBody).Where(c => c.name == "NativePerimeter")
                .Select(c => new { Collider = c, Distance = Physics2D.Distance(diagnostic.Actor.FootCollider, c) }).Where(v => v.Distance.isValid).OrderBy(v => Mathf.Abs(v.Distance.distance)).ToArray();
            Assert.That(walls.Length, Is.GreaterThan(0)); var nearest = walls[0];
            Assert.That(nearest.Distance.distance, Is.InRange(-.03f, .07f), label + " actual capsule contact distance");
            Assert.That(Overlaps(diagnostic.Position).All(c => Physics2D.Distance(diagnostic.Actor.FootCollider, c).distance >= -.03f), Is.True);
            report.Contacts.Add(new ContactRecord { Label = label, Collider = nearest.Collider.name, From = from, Direction = direction,
                ActualTo = diagnostic.Position, StartFixedFrame = frame, EndFixedFrame = diagnostic.FixedFrame, CastDistance = hit.distance,
                Progress = progress, FreeDistance = diagnostic.Actor.MovementSpeed * seconds, ActualCapsuleDistance = nearest.Distance.distance });
        }
        IEnumerator JOwnerStateChecks()
        {
            var owners = nav.Owners.Where(o => fixture.Catalog.Locations.Single(l => l.Id == "J").SourceOwnerIds.Contains(o.ObjectId)).ToArray();
            Assert.That(owners.Length, Is.EqualTo(fixture.Saved.JOwners));
            foreach (var owner in owners)
            {
                Assert.That(owner.Present, Is.True); var others = OtherOwners(owner); var bodies = ActiveBodies(owner).Length;
                var rectangles = owner.CurrentNavigationRectangles(); var total = nav.ActiveOwnerRectangleCount;
                try
                {
                    owner.SetPresent(false); Physics2D.SyncTransforms(); yield return null;
                    Assert.That(owner.Failure, Is.Empty); Assert.That(owner.PresentObjects.All(g => !g.activeInHierarchy), Is.True);
                    Assert.That(ActiveBodies(owner), Is.Empty); Assert.That(owner.CurrentNavigationRectangles(), Is.Empty);
                    Assert.That(nav.ActiveOwnerRectangleCount, Is.EqualTo(total - rectangles.Length)); Assert.That(OtherOwners(owner), Is.EqualTo(others));
                }
                finally { owner.SetPresent(true); Physics2D.SyncTransforms(); }
                yield return null; Assert.That(ActiveBodies(owner).Length, Is.EqualTo(bodies));
                Assert.That(owner.CurrentNavigationRectangles(), Is.EqualTo(rectangles)); Assert.That(OtherOwners(owner), Is.EqualTo(others));
                report.Facts.Add(owner.ObjectId + " present/absent/restored: bodies=" + bodies + ", nav=" + rectangles.Length + ", other owner state/config exact.");
            }
            report.Checks.Add("Each reported J owner independently toggles its own graphics, body and live navigation; selected core and other J owners are retained.");
        }
        IEnumerator TreeDepthPixelControls()
        {
            var tree = nav.Owners.Single(o => o.ObjectId == "c_tree_northwest_anchor01");
            var unit = fixture.Catalog.Locations.Single(l => l.Id == "C").FootUnits.Single(u => u.OwnerId == tree.ObjectId);
            var foot = (Vector2)tree.transform.Find(unit.PartPath).position;
            var found = false; Vector2 start = Vector2.zero;
            foreach (var lateral in new[] { -.6f, .6f, -.8f, .8f, -1f, 1f, -1.2f, 1.2f })
            {
                var point = foot + new Vector2(lateral, -.6f);
                if (!Clear(point) || !Clear(point + Vector2.up * 1.2f) || FirstHit(point, Vector2.up, 1.2f).collider != null) continue;
                start = point; found = true; break;
            }
            Assert.That(found, Is.True, "Original C tree needs a floor/capsule-clear front-to-back approach.");
            yield return Place(start); yield return null;
            var frontHero = depth.Observations.Single(o => o.Key == "actor:hero"); var frontTree = depth.Observations.Single(o => o.OwnerId == tree.ObjectId);
            Assert.That(frontHero.FootWorldY, Is.LessThan(frontTree.FootWorldY)); Assert.That(frontHero.AssignedOrder, Is.GreaterThan(frontTree.AssignedOrder));
            var front = HeroControl("hero-front-C-tree");
            yield return Follow(Dense(start, start + Vector2.up * 1.2f), "C-tree-front-back"); yield return null;
            var backHero = depth.Observations.Single(o => o.Key == "actor:hero"); var backTree = depth.Observations.Single(o => o.OwnerId == tree.ObjectId);
            Assert.That(backHero.FootWorldY, Is.GreaterThan(backTree.FootWorldY)); Assert.That(backHero.AssignedOrder, Is.LessThan(backTree.AssignedOrder));
            var back = HeroControl("hero-back-C-tree");
            Assert.That(front, Is.GreaterThan(0)); Assert.That(back, Is.LessThan(front), "Actual tree failed to occlude more Hero pixels behind its foot.");
            report.Facts.Add("Same-frame Hero on/off changed pixels: front=" + front + ", back=" + back + "; actual Actor crossed C tree foot Y.");
        }

        string[] CoreSourceAndTransforms(GameObject mapRoot)
        {
            var records = new List<string>();
#if UNITY_EDITOR
            foreach (var definition in fixture.Catalog.Locations.Where(l => l.Id != "J"))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath); Assert.That(source, Is.Not.Null);
                Assert.That(Hash(File.ReadAllBytes(Project(definition.PrefabPath))), Is.EqualTo(definition.PrefabSHA256));
                Assert.That(Hash(File.ReadAllBytes(Project(definition.GroundPath))), Is.EqualTo(definition.GroundSHA256));
                var original = source.GetComponentsInChildren<StudyAIndependentObjectState>(true).ToDictionary(o => o.ObjectId);
                Assert.That(original.Keys, Is.EquivalentTo(definition.SourceOwnerIds));
                foreach (var id in definition.SourceOwnerIds)
                {
                    var live = nav.Owners.Single(o => o.ObjectId == id); var pinned = original[id];
                    var instance = live.transform; while (instance.parent != mapRoot.transform && instance.parent != null) instance = instance.parent;
                    var block = fixture.Saved.Scene.Assembly.Blocks.Single(b => b.LocationId == definition.Id);
                    Assert.That(Vector3.Distance(instance.localPosition, new Vector3(block.OriginPx.x / (float)PPU, block.OriginPx.y / (float)PPU, 0)), Is.LessThan(.00001f));
                    Assert.That(Quaternion.Angle(instance.localRotation, Quaternion.identity), Is.LessThan(.001f));
                    Assert.That(Vector3.Distance(instance.localScale, Vector3.one), Is.LessThan(.00001f));
                    var ground = instance.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.sortingLayerName == "MapGround" && r.sortingOrder == 0 && r.GetComponentInParent<StudyAIndependentObjectState>() == null);
                    Assert.That(ground.sprite, Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(definition.GroundPath)), "Selected source/accepted ground binding changed.");
                    Assert.That(live.NavigationRectanglesLocal, Is.EqualTo(pinned.NavigationRectanglesLocal));
                    foreach (var t in pinned.GetComponentsInChildren<Transform>(true))
                    {
                        var path = Relative(pinned.transform, t); var actual = path.Length == 0 ? live.transform : live.transform.Find(path);
                        Assert.That(actual, Is.Not.Null); Assert.That(Local(actual), Is.EqualTo(Local(t)), id + "/" + path);
                        records.Add(id + "/" + path + "=" + Local(actual));
                    }
                    foreach (var sprite in pinned.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        var path = Relative(pinned.transform, sprite.transform);
                        var actual = (path.Length == 0 ? live.transform : live.transform.Find(path)).GetComponent<SpriteRenderer>();
                        Assert.That(actual.sprite, Is.SameAs(sprite.sprite)); Assert.That(actual.sharedMaterial, Is.SameAs(sprite.sharedMaterial));
                        Assert.That(actual.color, Is.EqualTo(sprite.color)); Assert.That(actual.flipX, Is.EqualTo(sprite.flipX)); Assert.That(actual.flipY, Is.EqualTo(sprite.flipY));
                    }
                }
                foreach (var unit in definition.FootUnits)
                {
                    var part = nav.Owners.Single(o => o.ObjectId == unit.OwnerId).transform.Find(unit.PartPath);
                    Assert.That(part.GetComponent<SortingGroup>(), Is.Not.Null); records.Add("foot:" + unit.OwnerId + "/" + unit.PartPath + "=" + Local(part));
                }
            }
#endif
            return records.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }
        static string Relative(Transform root, Transform t)
        {
            if (t == root) return ""; var names = new List<string>(); while (t != root && t != null) { names.Add(t.name); t = t.parent; }
            Assert.That(t, Is.SameAs(root)); names.Reverse(); return string.Join("/", names);
        }
        static string Local(Transform t) => t.localPosition.ToString("F8") + "|" + t.localRotation.ToString("F8") + "|" + t.localScale.ToString("F8");
        string[] OtherOwners(StudyAIndependentObjectState excluded) => nav.Owners.Where(o => o != excluded).OrderBy(o => o.ObjectId, StringComparer.Ordinal)
            .Select(o => o.ObjectId + "|" + o.Present + "|" + string.Join(";", o.CurrentNavigationRectangles().Select(r => r.ToString("F8"))) + "|" +
                string.Join(";", o.GetComponentsInChildren<Collider2D>(true).Select(c => c.name + ":" + c.enabled + ":" + c.gameObject.activeSelf)) + "|" +
                string.Join(";", o.GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.name + ":" + r.enabled + ":" + r.gameObject.activeInHierarchy))).ToArray();
        static bool IsBody(Collider2D c) => c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && c.gameObject.layer == LayerMask.NameToLayer("MapCollision");
        static Collider2D[] ActiveBodies(StudyAIndependentObjectState owner) => owner.GetComponentsInChildren<Collider2D>(false).Where(IsBody).ToArray();
        Collider2D[] Overlaps(Vector2 body) => Physics2D.OverlapCapsuleAll(body + diagnostic.Actor.FootCollider.offset, diagnostic.Actor.FootCollider.size,
            diagnostic.Actor.FootCollider.direction, 0, 1 << LayerMask.NameToLayer("MapCollision")).Where(IsBody).ToArray();
        RaycastHit2D FirstHit(Vector2 body, Vector2 direction, float distance) => Physics2D.CapsuleCastAll(body + diagnostic.Actor.FootCollider.offset,
            diagnostic.Actor.FootCollider.size, diagnostic.Actor.FootCollider.direction, 0, direction, distance, 1 << LayerMask.NameToLayer("MapCollision"))
            .Where(h => IsBody(h.collider)).OrderBy(h => h.distance).FirstOrDefault();
        bool Clear(Vector2 body) => FloorBody(body) && Overlaps(body).Length == 0;
        bool FloorBody(Vector2 body)
        {
            var center = body + diagnostic.Actor.FootCollider.offset; var half = diagnostic.Actor.FootCollider.size * .5f + Vector2.one * .01f;
            for (var y = Mathf.FloorToInt((center.y - half.y) * PPU); y < Mathf.CeilToInt((center.y + half.y) * PPU); y++)
                for (var x = Mathf.FloorToInt((center.x - half.x) * PPU); x < Mathf.CeilToInt((center.x + half.x) * PPU); x++)
                {
                    var b = fixture.Geometry.BoundsPx;
                    if (x < b.xMin || x >= b.xMax || y < b.yMin || y >= b.yMax || fixture.UnionFloor[x - b.x + (y - b.y) * b.width] == 0) return false;
                }
            return true;
        }
        void CameraCheck()
        {
            var c = diagnostic.GameplayCamera; var bounds = diagnostic.CameraMapBounds;
            var x = Clamp(diagnostic.Position.x, bounds.xMin, bounds.xMax, c.orthographicSize * c.aspect);
            var y = Clamp(diagnostic.Position.y, bounds.yMin, bounds.yMax, c.orthographicSize);
            var expected = new Vector3(Mathf.Round(x * 32) / 32, Mathf.Round(y * 32) / 32, -10);
            Assert.That(Vector3.Distance(c.transform.position, expected), Is.LessThan(.001f), "Settled ordinary production camera clamp/snap; presentation margin included.");
            Assert.That(c.orthographicSize, Is.EqualTo(8));
        }
        static float Clamp(float value, float min, float max, float extent) => min + extent > max - extent ? (min + max) * .5f : Mathf.Clamp(value, min + extent, max - extent);
        void Capture(string label)
        {
            CameraCheck(); var c = diagnostic.GameplayCamera;
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var h = 1024; var width = Mathf.RoundToInt(h * c.aspect); var path = CapturePath(label);
            Assert.That(File.Exists(path), Is.False); var oldTarget = c.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, h, 24, RenderTextureFormat.ARGB32); var texture = new Texture2D(width, h, TextureFormat.RGBA32, false);
            try { c.targetTexture = target; c.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, h), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { c.targetTexture = oldTarget; RenderTexture.active = oldActive; UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(texture); }
            report.Frames.Add(new FrameRecord { Label = label, CapturePath = path, Frame = diagnostic.Frame, FixedFrame = diagnostic.FixedFrame,
                AdapterReadFrames = Reads, OriginalInputReadFrames = diagnostic.InputReadFrames, DepthUnits = depth.BoundUnitCount,
                IsPlaying = Application.isPlaying, Focused = Application.isFocused, OriginalInputEnabled = diagnostic.InputEnabled,
                Actor = diagnostic.Position, AdapterMove = ObservedMove, Camera = c.transform.position, Aspect = c.aspect, Ortho = c.orthographicSize }); WriteReport();
        }
        int HeroControl(string label)
        {
            var renderers = diagnostic.Actor.GetComponentsInChildren<SpriteRenderer>(true); var enabled = renderers.Select(r => r.enabled).ToArray();
            var frame = diagnostic.Frame; var fixedFrame = diagnostic.FixedFrame; var actor = diagnostic.Position; var camera = diagnostic.GameplayCamera.transform.position;
            Capture(label);
            try { foreach (var renderer in renderers) renderer.enabled = false; Capture(label + "-hero-hidden-control"); }
            finally { for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = enabled[i]; }
            Assert.That(diagnostic.Frame, Is.EqualTo(frame)); Assert.That(diagnostic.FixedFrame, Is.EqualTo(fixedFrame));
            Assert.That(diagnostic.Position, Is.EqualTo(actor)); Assert.That(diagnostic.GameplayCamera.transform.position, Is.EqualTo(camera));
            var a = new Texture2D(2, 2); var b = new Texture2D(2, 2);
            try { Assert.That(a.LoadImage(File.ReadAllBytes(CapturePath(label))), Is.True); Assert.That(b.LoadImage(File.ReadAllBytes(CapturePath(label + "-hero-hidden-control"))), Is.True);
                var pa = a.GetPixels32(); var pb = b.GetPixels32(); Assert.That(pa.Length, Is.EqualTo(pb.Length)); return pa.Where((p, i) => !p.Equals(pb[i])).Count(); }
            finally { UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b); }
        }
        string CapturePath(string label) => Path.Combine(evidence, report.Case + "-" + label + "-ordinary-camera.png");
        string ReportPath() => Path.Combine(evidence, report.Case + "-actual-play-receipt.json");
        void WriteReport() { if (ownsReport) File.WriteAllText(ReportPath(), JsonUtility.ToJson(report, true)); }
        static string Project(string path)
        {
            Assert.That(path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains(":"), Is.True);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2"))); }
        static string Argument(string key) { var args = Environment.GetCommandLineArgs(); for (var i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1]; return null; }
    }
}
