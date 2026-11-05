using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
public static class NemoRiverPlaySmoke
{
    [Serializable]public class Result{public bool success,nativeActor,normalCamera,overviewCamera,followCamera,savedReload,bothBridgesRestored,inputMove,inputRequired,debugParcels,debugWalk,debugRestored,inputHeldBefore;public float inputDistance;public int frames,playerInputUpdates;public string scope,inputScope;public string[] errors;}
    const string Key="NemoRiverPlaySmoke.";static int stage,frame;static double began;static NemoWalk walk;static Result result;static Vector2 moveStart;static float moveBegan;
#if ENABLE_INPUT_SYSTEM
    static Keyboard synthetic,previousKeyboard;
    static void DriveKeyboard(){if(stage==1&&synthetic!=null&&(InputState.currentUpdateType==InputUpdateType.Dynamic||InputState.currentUpdateType==InputUpdateType.Fixed)){InputState.Change(synthetic,new KeyboardState(UnityEngine.InputSystem.Key.D),InputState.currentUpdateType);synthetic.MakeCurrent();result.playerInputUpdates++;}}
#endif
    static readonly List<string> errors=new List<string>();
    [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool(Key+"pending",false))return;stage=SessionState.GetInt(Key+"stage",-1);began=double.Parse(SessionState.GetString(Key+"began","0"),CultureInfo.InvariantCulture);result=JsonUtility.FromJson<Result>(SessionState.GetString(Key+"result","{}"));errors.Clear();if(result.errors!=null)errors.AddRange(result.errors);Wire();}
    static void Wire(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
    static void Save(int next){stage=next;result.errors=errors.ToArray();SessionState.SetInt(Key+"stage",stage);SessionState.SetString(Key+"result",JsonUtility.ToJson(result));}
    static void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors.Add(text+"\n"+stack);Save(stage);}}
    public static void Begin(){errors.Clear();result=new Result{scope="Actual isolated PlayMode: delivered river Scene startup, native actor, normal/follow/overview LateUpdate, two bridge state SaveNow/LoadNow and restoration. No physical keyboard injection, GUI screenshot or Player build."};EditorSceneManager.OpenScene(NemoRiverVerifier.ScenePath);began=EditorApplication.timeSinceStartup;SessionState.SetBool(Key+"pending",true);SessionState.SetString(Key+"began",began.ToString(CultureInfo.InvariantCulture));Save(-1);Wire();}
    static void Tick()
    {
        try{if(stage==4){if(!EditorApplication.isPlaying){SessionState.SetBool(Key+"pending",false);EditorApplication.update-=Tick;}return;}if(EditorApplication.timeSinceStartup-began>300){errors.Add("Play smoke timed out");Finish();return;}if(stage==-1){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup-began<20)return;Save(0);EditorApplication.isPlaying=true;return;}if(!EditorApplication.isPlaying)return;
            if(stage==0){if(Time.frameCount<4)return;walk=UnityEngine.Object.FindAnyObjectByType<NemoWalk>();if(walk==null||!walk.Map.UseDesignedRivers||walk.Map.AllBridges.Length!=2)throw new Exception("River Scene startup contract failed");result.nativeActor=walk.Actor.GetComponent<SpriteRenderer>().sprite.pixelsPerUnit==64&&walk.Actor.transform.lossyScale==Vector3.one;result.normalCamera=Mathf.Abs(walk.View.orthographicSize-NemoWalk.NormalCameraSize)<.001f;walk.ToggleParcels();result.debugParcels=walk.Map.Overlay.activeSelf&&!walk.Map.Boundary.WalkRegion.Overlay.activeSelf;walk.ToggleParcels();walk.ToggleWalkArea();result.debugWalk=walk.Map.Boundary.WalkRegion.Overlay.activeSelf&&!walk.Map.Overlay.activeSelf;walk.ToggleWalkArea();result.debugRestored=!walk.Map.Overlay.activeSelf&&!walk.Map.Boundary.WalkRegion.Overlay.activeSelf;var cap=walk.Actor.GetComponent<CapsuleCollider2D>();bool Clear(Vector2 p)=>!Physics2D.OverlapCapsuleAll(p,cap.size,CapsuleDirection2D.Vertical,0).Any(c=>c!=cap&&c.enabled&&!c.isTrigger);var start=walk.Map.Plan.links.SelectMany(e=>e.points).First(p=>Clear(p)&&Clear(p+Vector2.right*.7f)&&Clear(p+Vector2.left*.3f)&&walk.Map.Boundary.WalkRegion.Contains(p+Vector2.right*.7f));walk.Teleport(start);moveStart=walk.BodyCenter;moveBegan=Time.time;
#if ENABLE_INPUT_SYSTEM
                result.inputRequired=true;result.inputScope="Actual new Input System virtual Keyboard D -> NemoInput -> FixedUpdate -> Rigidbody2D displacement. No OS keyboard injection or settings change.";previousKeyboard=Keyboard.current;InputSystem.RegisterLayout("{\"name\":\"NemoValidationKeyboard\",\"extend\":\"Keyboard\",\"canRunInBackground\":true}");synthetic=(Keyboard)InputSystem.AddDevice("NemoValidationKeyboard");InputSystem.onAfterUpdate+=DriveKeyboard;InputSystem.QueueStateEvent(synthetic,new KeyboardState(UnityEngine.InputSystem.Key.D));
#else
                result.inputScope="Legacy keyboard injection not available; core Physics2D movement and all shared GUI/key callback methods covered separately.";
#endif
                frame=Time.frameCount;Save(1);return;}
            if(stage==1){
#if ENABLE_INPUT_SYSTEM
                result.inputHeldBefore|=NemoInput.Held(KeyCode.D);InputSystem.QueueStateEvent(synthetic,new KeyboardState(UnityEngine.InputSystem.Key.D));
#endif
                if(Time.frameCount-frame<8||Time.time-moveBegan<.40f)return;
#if ENABLE_INPUT_SYSTEM
                result.inputDistance=walk.BodyCenter.x-moveStart.x;result.inputMove=result.inputDistance>.05f;InputSystem.QueueStateEvent(synthetic,new KeyboardState());InputSystem.onAfterUpdate-=DriveKeyboard;InputSystem.RemoveDevice(synthetic);synthetic=null;InputSystem.RemoveLayout("NemoValidationKeyboard");if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
#endif
                result.followCamera=Vector2.Distance(walk.View.transform.position,walk.BodyCenter)<.001f;NemoBuilder.Capture(walk.View,walk.BodyCenter,NemoWalk.NormalCameraSize,Path.Combine(NemoBuilder.Evidence,"river-play-normal.png"));walk.Overview=true;frame=Time.frameCount;Save(2);return;}
            if(stage==2){if(Time.frameCount-frame<4)return;result.overviewCamera=Mathf.Abs(walk.View.orthographicSize-56)<.001f&&Vector2.Distance(walk.View.transform.position,new Vector2(72,54))<.001f;walk.Overview=false;string ground=walk.Map.Boundary.WalkRegion.SavedGroundMask,plan=JsonUtility.ToJson(walk.Map.Plan),river=JsonUtility.ToJson(walk.Map.RiverNetwork.Design);var p=walk.BodyCenter;foreach(var bridge in walk.Map.AllBridges)bridge.SetPresent(false);walk.SaveNow();walk.Regenerate(42);walk.LoadNow();result.savedReload=walk.Map.Boundary.WalkRegion.SavedGroundMask==ground&&walk.Map.Seed==0&&walk.Map.AllBridges.All(b=>!b.Present)&&JsonUtility.ToJson(walk.Map.Plan)==plan&&JsonUtility.ToJson(walk.Map.RiverNetwork.Design)==river&&Vector2.Distance(walk.BodyCenter,p)<.001f;foreach(var bridge in walk.Map.AllBridges)bridge.SetPresent(true);result.bothBridgesRestored=walk.Map.AllBridges.All(b=>b.Present);frame=Time.frameCount;Save(3);return;}
            if(stage==3){if(Time.frameCount-frame<4)return;result.normalCamera&=Mathf.Abs(walk.View.orthographicSize-NemoWalk.NormalCameraSize)<.001f;result.frames=Time.frameCount;Finish();}
        }catch(Exception e){errors.Add(e.ToString());Finish();}
    }
    static void Finish(){if(stage==4)return;
#if ENABLE_INPUT_SYSTEM
        if(synthetic!=null){InputSystem.onAfterUpdate-=DriveKeyboard;InputSystem.RemoveDevice(synthetic);synthetic=null;InputSystem.RemoveLayout("NemoValidationKeyboard");if(previousKeyboard!=null)previousKeyboard.MakeCurrent();}
#endif
        result.errors=errors.ToArray();result.success=errors.Count==0&&(!result.inputRequired||result.inputMove)&&result.debugParcels&&result.debugWalk&&result.debugRestored&&result.nativeActor&&result.normalCamera&&result.overviewCamera&&result.followCamera&&result.savedReload&&result.bothBridgesRestored;Directory.CreateDirectory(NemoBuilder.Evidence);File.WriteAllText(Path.Combine(NemoBuilder.Evidence,"river-playmode-smoke.json"),JsonUtility.ToJson(result,true));Application.logMessageReceived-=Log;Save(4);EditorApplication.isPlaying=false;}
}
