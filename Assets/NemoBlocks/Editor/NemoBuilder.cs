using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class NemoBuilder
{
    public const string ScenePath="Assets/NemoBlocks/Scenes/NemoRandomMap_WalkRegions.unity";
    public static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../NemoValidationV8Visual"));
    public static string Evidence=>Path.Combine(Root,"evidence");
    static Sprite Resolve(NativeVisual v)
    {
        var result=AssetDatabase.LoadAllAssetsAtPath(v.path).OfType<Sprite>().FirstOrDefault(s=>Mathf.Abs(s.rect.x-v.rect[0])<.01f&&Mathf.Abs(s.rect.y-v.rect[1])<.01f&&Mathf.Abs(s.rect.width-v.rect[2])<.01f&&Mathf.Abs(s.rect.height-v.rect[3])<.01f);
        if(result==null)throw new Exception("Exact native sprite unresolved: "+v.path+" "+string.Join(",",v.rect));
        if(result.pixelsPerUnit!=64||Mathf.Abs(result.pivot.x-v.pivotX)>.02f||Mathf.Abs(result.pivot.y-v.pivotY)>.02f)throw new Exception("Native sprite contract mismatch: "+v.path);return result;
    }
    static Texture2D Patch(Texture2D source,RectInt r,string name)
    {
        var t=new Texture2D(r.width,r.height,TextureFormat.RGBA32,false);t.name=name;t.SetPixels(source.GetPixels(r.x,r.y,r.width,r.height));t.Apply();t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Mirror;AssetDatabase.CreateAsset(t,"Assets/NemoBlocks/NativeLibrary/"+name+".asset");return t;
    }
    public static NemoNativeLibrary Library()
    {
        var existing=AssetDatabase.LoadAssetAtPath<NemoNativeLibrary>("Assets/NemoBlocks/NativeLibrary/Library.asset");if(existing!=null)return existing;
        Directory.CreateDirectory("Assets/NemoBlocks/NativeLibrary");var d=JsonUtility.FromJson<NativeInput>(File.ReadAllText("Assets/NemoBlocks/Resources/native-library.json"));var library=ScriptableObject.CreateInstance<NemoNativeLibrary>();library.Prototypes=d.prototypes;library.River=d.river;
        foreach(var p in library.Prototypes)foreach(var v in p.visuals)v.sprite=Resolve(v);foreach(var owner in library.River)foreach(var v in owner.visuals)v.sprite=Resolve(v);
        library.Hero=AssetDatabase.LoadAllAssetsAtPath(d.hero).OfType<Sprite>().First();if(library.Hero.pixelsPerUnit!=64)throw new Exception("Hero PPU");
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(d.grassSource));library.Grass=new[]{Patch(source,new RectInt(0,0,128,96),"grass-0"),Patch(source,new RectInt(128,0,128,96),"grass-1"),Patch(source,new RectInt(256,0,128,96),"grass-2"),Patch(source,new RectInt(384,0,128,96),"grass-3")};library.Dirt=Patch(source,new RectInt(344,304,64,64),"dirt-native");UnityEngine.Object.DestroyImmediate(source);AssetDatabase.CreateAsset(library,"Assets/NemoBlocks/NativeLibrary/Library.asset");AssetDatabase.SaveAssets();return library;
    }
    public static NemoWalk Create(int seed,bool designedRivers=false,bool visualFinish=true)
    {
        Directory.CreateDirectory(Evidence);var library=Library();NemoPixelWorkshop.Ensure(library);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var map=new GameObject("Nemo generator").AddComponent<NemoMap>();map.Library=library;map.UseDesignedRivers=designedRivers;map.EnableVisualFinish=visualFinish;
        map.FinishGrassSources=new[]{"grass_01","grass_02"}.Select(name=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Map/OverheadStudyA/Input/GroundR2/Assets/"+name+".png")).ToArray();
        map.FinishDirtSource=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Map/OverheadStudyA/Input/GroundR2/Assets/dirt_01.png");
        map.FinishGroundDetails=new[]{"grass_tuft_01","grass_tuft_02","grass_patch_01","grass_patch_02","flower_white","flower_yellow","flower_pink"}.Select(name=>AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Map/OverheadStudyA/Input/ForestDetailsR1/Assets/"+name+".png").OfType<Sprite>().First()).ToArray();map.Generate(seed);
        var actor=new GameObject("Native hero | capsule 0.48 x 0.55");var rb=actor.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;var cap=actor.AddComponent<CapsuleCollider2D>();cap.size=new Vector2(.48f,.55f);cap.offset=new Vector2(0,.22f);cap.direction=CapsuleDirection2D.Vertical;var sr=actor.AddComponent<SpriteRenderer>();sr.sprite=library.Hero;map.BindSpriteMaterial(sr);sr.sortingOrder=10000-Mathf.RoundToInt(map.Plan.parcels[0].hub.y*64)+1;
        var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.orthographic=true;cam.orthographicSize=NemoWalk.NormalCameraSize;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.06f,.1f,.08f);cam.transform.position=new Vector3(map.Plan.parcels[0].hub.x,map.Plan.parcels[0].hub.y,-10);
        var walk=new GameObject("Map controls").AddComponent<NemoWalk>();walk.Map=map;walk.Actor=rb;walk.View=cam;walk.Teleport(map.Plan.parcels[0].hub);return walk;
    }
    public static void Persist(NemoWalk walk,string scene,string assets)
    {
        Directory.CreateDirectory(assets);int n=0;AssetDatabase.StartAssetEditing();try{foreach(var obj in walk.Map.Transient.Where(o=>o!=null&&!EditorUtility.IsPersistent(o))){obj.name="native-derived-"+n;AssetDatabase.CreateAsset(obj,assets+"/derived-"+(n++)+".asset");}}finally{AssetDatabase.StopAssetEditing();}walk.Map.Transient.Clear();Directory.CreateDirectory(Path.GetDirectoryName(scene));EditorSceneManager.SaveScene(walk.gameObject.scene,scene);AssetDatabase.SaveAssets();
    }
    [MenuItem("Nemo/Build walking-area random map")]
    public static void Build(){if(SceneManager.GetActiveScene().isDirty)throw new Exception("Save the current Scene before building a new map.");Directory.CreateDirectory(Evidence);var walk=Create(0,true);Persist(walk,ScenePath,"Assets/NemoBlocks/Generated/seed-0");Capture(walk.View,new Vector2(72,54),56,Path.Combine(Evidence,"seed-0-overview-initial.png"),1728,1296);Capture(walk.View,walk.BodyCenter,NemoWalk.NormalCameraSize,Path.Combine(Evidence,"seed-0-start-initial.png"));File.WriteAllText(Path.Combine(Evidence,"initial-plan.json"),JsonUtility.ToJson(walk.Map.Plan,true));File.WriteAllText(Path.Combine(Evidence,"initial-skipped.json"),JsonUtility.ToJson(new Skips{skipped=walk.Map.Skipped},true));Debug.Log("NEMO_BUILD_COMPLETE");}
    [Serializable]class Skips{public NemoSkipped[] skipped;}
    public static void Capture(Camera camera,Vector2 center,float size,string file,int width=1280,int height=960)
    {
        var beforePosition=camera.transform.position;var beforeSize=camera.orthographicSize;var beforeAspect=camera.aspect;var previous=RenderTexture.active;
        var rt=new RenderTexture(width,height,24);rt.Create();camera.targetTexture=rt;camera.aspect=(float)width/height;camera.transform.position=new Vector3(center.x,center.y,-10);camera.orthographicSize=size;camera.Render();RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file,image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);camera.transform.position=beforePosition;camera.orthographicSize=beforeSize;camera.aspect=beforeAspect;
    }
}
