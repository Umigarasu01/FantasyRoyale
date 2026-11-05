using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using FantasyRoyale.Playtest.OverheadStudyA;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable] public sealed class StudyANativeFlexibleSceneConfig
    {
        public string AcceptedConfigPath = "Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationLayouts-v2-20261006/native-layout-exterior-quality-config-v1.json";
        public string OutputRoot = "Assets/Scenes/MapAuthoring/OverheadStudyA/NativeFlexibleConnectionPilot-v1-20261008";
        public int Seed, ConnectorLengthPx = 384;
        public string SpawnLocationId = "B";
    }
    [Serializable] public sealed class StudyANativeFlexibleSceneReport
    {
        public bool Generated;
        public string Error, Qualification = "Saved candidate geometry and art; actual GPU/Play review required. Existing S0/E is preserved.";
        public int ConnectorLengthPx, Seed, JOwners, JFootUnits, JPhysicsBodies, JNavigationRectangles, ExteriorPixels;
        public string FloorMaskSHA256, SoilMaskSHA256, GroundSHA256, GroundMetricsJSON;
        public string SourceAdmissionRecipeId, SourceAdmissionLayoutHash, SourceAcceptedConfigPath, SourceAcceptedConfigSHA256;
        public string JPrefabSHA256, ExteriorSHA256, PresentationReceiptSHA256, GenerationKey;
        public StudyANativeLocationSceneReport Scene;
    }
    public static partial class StudyANativeLocationSceneBuilder
    {
        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Generate Flexible Native Connection Pilot")]
        public static void GenerateFlexibleConfigured()
        {
            var input = Selection.activeObject as TextAsset;
            var result = BuildFlexibleConfigured(input);
            if (!result.Generated) throw new InvalidOperationException(result.Error);
            Debug.Log(JsonUtility.ToJson(result, true));
        }

        public static StudyANativeFlexibleSceneReport BuildFlexibleConfigured(TextAsset input)
        {
            var result = new StudyANativeFlexibleSceneReport();
            var previous = SceneManager.GetActiveScene();
            Scene map=default,entry=default;
            string owned=null; bool created=false;
            GameObject transient=null;
            try
            {
                GuardEditor(); Require(input!=null && !EditorUtility.IsDirty(input), "Select the saved flexible pilot config.");
                var cfg=JsonUtility.FromJson<StudyANativeFlexibleSceneConfig>(input.text);
                Require(cfg!=null && cfg.SpawnLocationId=="B", "This explicit pilot fixes the preserved B spawn.");
                var accepted=AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath(cfg.AcceptedConfigPath));
                Require(accepted!=null && !EditorUtility.IsDirty(accepted), "Accepted source config missing/dirty.");
                var sourceConfig=JsonUtility.FromJson<StudyANativeLocationSceneConfig>(accepted.text);
                sourceConfig.RecipeId="E";
                var originalAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(sourceConfig.CatalogPath);
                var original=JsonUtility.FromJson<StudyANativeLocationCatalog>(originalAsset.text);
                var sceneReport=new StudyANativeLocationSceneReport { PixelsPerUnit=PPU, Seed=cfg.Seed, RecipeId="Flexible-BJDC-v1" };
                StudyANativeLocationCatalog admitted; Dictionary<string,Sprite> admittedOverrides;
                ResolveConfiguredNativeLayout(sourceConfig,original,sceneReport,out admitted,out admittedOverrides);
                result.SourceAdmissionRecipeId=sceneReport.RecipeId;result.SourceAdmissionLayoutHash=sceneReport.LayoutContentHash;
                result.SourceAcceptedConfigPath=cfg.AcceptedConfigPath;result.SourceAcceptedConfigSHA256=FileHash(cfg.AcceptedConfigPath);
                sceneReport.RecipeId="Flexible-BJDC-v1";sceneReport.LayoutContentHash=null;sceneReport.ConfigPath=AssetDatabase.GetAssetPath(input);
                Require(admittedOverrides!=null && admittedOverrides.ContainsKey("B") && admittedOverrides.ContainsKey("D"), "Explicit E source admission must provide B/D overrides.");
                var plan=StudyANativeFlexibleConnectionPlan.Build(admitted,cfg.ConnectorLengthPx,cfg.Seed);
                var assembly=plan.Assembly;
                var geometry=plan.Geometry;
                var definitions=admitted.Locations.Where(d=>d.Id=="B"||d.Id=="C"||d.Id=="D").ToDictionary(d=>d.Id,StringComparer.Ordinal);
                var prefabs=definitions.ToDictionary(p=>p.Key,p=>GuardNativePrefab(p.Value),StringComparer.Ordinal);
                var originalGrounds=original.Locations.Where(d=>definitions.ContainsKey(d.Id)).ToDictionary(d=>d.Id,d=>AssetDatabase.LoadAssetAtPath<Sprite>(d.GroundPath));
                var be=ReadFlexPixels(definitions["B"].GroundPath,out var bw,out var bh);
                var dw=ReadFlexPixels(definitions["D"].GroundPath,out var dwWidth,out var dh);
                var ground=StudyANativeFlexibleGroundWriter.BuildPixels(cfg.ConnectorLengthPx,384,cfg.Seed,plan.FloorMaskBL,plan.SoilCentreYByXBL,be,bw,bh,dw,dwWidth,dh);
                GuardFile(sourceConfig.MapTemplatePath,sourceConfig.MapTemplateSHA256);
                GuardFile(sourceConfig.EntryTemplatePath,sourceConfig.EntryTemplateSHA256);
                GuardFile("Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/IsolatedStyleFamily-v2-20261004-215942621/Sources/bank_west_style.png","b975b0da6456582c76ce2831dd4b820b7c6c87077484abb1f4f1ed0a5118161a");
                GuardFile("Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/IsolatedStyleFamily-v2-20261004-215942621/Sources/bank_west_style.png.meta","149834344ab296fedc0aa14d1e0859e1d76e9948f04767252884e2f1cd2652c8");
                var baseOutput=AssetPath(cfg.OutputRoot);
                Require(baseOutput=="Assets/Scenes/MapAuthoring/OverheadStudyA/NativeFlexibleConnectionPilot-v1-20261008" || baseOutput.StartsWith("Assets/Scenes/MapAuthoring/OverheadStudyA/NativeFlexibleConnectionPilot-v1-20261008-",StringComparison.Ordinal),"Use a new isolated flexible pilot output root.");
                result.GenerationKey=TextHash(assembly.ContentHash+"\n"+ground.PixelSHA256BL+"\nsource-b-native-rock2-grass6-v1");
                owned=baseOutput+"/J-"+(cfg.ConnectorLengthPx/64)+"u-Seed-"+cfg.Seed+"-"+result.GenerationKey.Substring(0,12);
                Require(!Directory.Exists(ProjectPath(owned)) && !File.Exists(ProjectPath(owned)+".meta"),"Output already exists; never overwrite a saved candidate.");
                EnsureFolder(owned);created=true;
                var material=NativeGroundRenderer(prefabs["B"]).sharedMaterial;
                var groundPath=owned+"/J-ground.png";
                var sprite=WriteFlexSprite(groundPath,cfg.ConnectorLengthPx,384,ground.PixelsBL);
                transient=new GameObject("JFlexibleConnector");
                var sr=new GameObject("NativeGround_J").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transient.transform,false);sr.transform.localPosition=new Vector3(cfg.ConnectorLengthPx/128f,3,0);
                sr.sprite=sprite;sr.sharedMaterial=material;sr.sortingLayerName="MapGround";sr.sortingOrder=0;
                var jFeet=AddFlexScenery(transient,prefabs["B"],definitions["B"],plan);
                var jp=owned+"/JComposition.prefab";
                Require(PrefabUtility.SaveAsPrefabAsset(transient,jp)!=null,"J prefab save failed.");
                Object.DestroyImmediate(transient);transient=null;
                var jDef=new StudyANativeLocationDefinition { Id="J", PrefabPath=jp, GroundPath=groundPath,
                    ExtentPx=new Vector2Int(cfg.ConnectorLengthPx,384), SpawnPx=new Vector2(32,297),
                    PrefabSHA256=FileHash(jp), GroundSHA256=FileHash(groundPath),
                    SourceOwnerIds=AssetDatabase.LoadAssetAtPath<GameObject>(jp).GetComponentsInChildren<StudyAIndependentObjectState>(true).Select(o=>o.ObjectId).ToArray(),FootUnits=jFeet,
                    Ports=new[]{new StudyANativeLocationPort { Id="J.W.road",Direction=StudyAPortDirection.West,AnchorPx=new Vector2Int(0,297),MouthWidthPx=64,AlongMinOffsetPx=-32,Terrain=StudyATerrain.Soil,CompatibilityKey=definitions["B"].Ports.Single(p=>p.Direction==StudyAPortDirection.East).CompatibilityKey },
                        new StudyANativeLocationPort { Id="J.E.road",Direction=StudyAPortDirection.East,AnchorPx=new Vector2Int(cfg.ConnectorLengthPx,169),MouthWidthPx=64,AlongMinOffsetPx=-32,Terrain=StudyATerrain.Soil,CompatibilityKey=definitions["D"].Ports.Single(p=>p.Direction==StudyAPortDirection.West).CompatibilityKey }} };
                definitions.Add("J",jDef);prefabs.Add("J",GuardNativePrefab(jDef));originalGrounds.Add("J",sprite);
                result.JPhysicsBodies=prefabs["J"].GetComponentsInChildren<Collider2D>(true).Count(c=>!c.isTrigger);
                result.JNavigationRectangles=prefabs["J"].GetComponentsInChildren<StudyAIndependentObjectState>(true).Sum(o=>o.NavigationRectanglesLocal.Length);
                var ordered=new[]{definitions["B"],definitions["C"],definitions["D"],jDef};
                var catalog=new StudyANativeLocationCatalog { Id="flexible-bjdc-v1",Version=1,PixelsPerUnit=64,Locations=ordered,
                    SharedEdgeMatches=new[]{new StudyANativeSharedEdgeMatch{FromLocationId="B",ToLocationId="J",FromDirection=StudyAPortDirection.East,LengthPx=384},new StudyANativeSharedEdgeMatch{FromLocationId="J",ToLocationId="D",FromDirection=StudyAPortDirection.East,LengthPx=384},new StudyANativeSharedEdgeMatch{FromLocationId="C",ToLocationId="D",FromDirection=StudyAPortDirection.North,LengthPx=512}}};
                var validation=StudyABlockGenerator.ValidateNativeAssembly(catalog,assembly);Require(validation.Count==0,string.Join(";",validation));
                var catalogJson=JsonUtility.ToJson(catalog,true);sceneReport.CatalogPath=owned+"/resolved-catalog.json";sceneReport.CatalogSHA256=TextHash(catalogJson);
                var bBlock=assembly.Blocks.Single(b=>b.LocationId=="B");var spawn=((Vector2)bBlock.OriginPx+definitions["B"].SpawnPx)/PPU;
                var blocked=geometry.PhysicalPerimeterPx.Concat(geometry.VoidInsideFramePx).Select(World).ToArray();
                sceneReport.ContentHash=assembly.ContentHash;sceneReport.Assembly=assembly;sceneReport.NativeSpawnWorld=spawn;
                sceneReport.NativeSpawnBodyWorld=HeroBody(spawn);sceneReport.FrameWorldBounds=World(geometry.BoundsPx);
                sceneReport.PresentationFrameWorldBounds=new Rect(sceneReport.FrameWorldBounds.position-Vector2.one*2,sceneReport.FrameWorldBounds.size+Vector2.one*4);
                sceneReport.OutputRoot=owned;sceneReport.MapScenePath=owned+"/NativeMap.unity";sceneReport.EntryScenePath=owned+"/NativeEntry.unity";
                var recipe=new StudyANativeLocationGeometryRecipe{PixelsPerUnit=64,BoundsPx=geometry.BoundsPx,FloorsPx=geometry.FloorsPx,PhysicalPerimeterPx=geometry.PhysicalPerimeterPx,VoidInsideFramePx=geometry.VoidInsideFramePx,WaterRectanglesWorld=Array.Empty<Rect>(),StaticBlockedRectanglesWorld=blocked,ContentHash=assembly.ContentHash};
                var geometryJson=JsonUtility.ToJson(recipe,true)+"\n";sceneReport.GeometryRecipeSHA256=TextHash(geometryJson);
                Require(AssetDatabase.CopyAsset(sourceConfig.MapTemplatePath,sceneReport.MapScenePath),"Map copy failed.");
                Require(AssetDatabase.CopyAsset(sourceConfig.EntryTemplatePath,sceneReport.EntryScenePath),"Entry copy failed.");
                map=EditorSceneManager.OpenScene(sceneReport.MapScenePath,OpenSceneMode.Additive);SceneManager.SetActiveScene(map);
                var units=BuildMap(map,assembly,definitions,prefabs,ordered,geometry,blocked,sceneReport,originalGrounds,admittedOverrides);
                var root=map.GetRootGameObjects().Single(g=>g.name=="MapRoot");
                result.ExteriorPixels=ApplyFlexibleExterior(root,geometry,owned,material);
                Require(EditorSceneManager.SaveScene(map,sceneReport.MapScenePath),"Map save failed.");
                entry=EditorSceneManager.OpenScene(sceneReport.EntryScenePath,OpenSceneMode.Additive);BuildEntry(entry,sceneReport,units);
                Require(EditorSceneManager.SaveScene(entry,sceneReport.EntryScenePath),"Entry save failed.");
                sceneReport.PhysicalPerimeterCount=geometry.PhysicalPerimeterPx.Length;sceneReport.VoidNavigationCount=geometry.VoidInsideFramePx.Length;sceneReport.StaticNavigationCount=blocked.Length;
                sceneReport.Generated=true;sceneReport.FullCatalogQualification="Explicit B/J/D/C pilot; curved actual-floor mask, original B/C/D core, new independent J owners. Not S0/E or a full four-way catalog.";
                result.Scene=sceneReport;result.ConnectorLengthPx=cfg.ConnectorLengthPx;result.Seed=cfg.Seed;result.JOwners=jDef.SourceOwnerIds.Length;result.JFootUnits=jFeet.Length;
                result.FloorMaskSHA256=BytesHash(plan.FloorMaskBL);result.SoilMaskSHA256=BytesHash(plan.SoilMaskBL);result.GroundSHA256=FileHash(groundPath);result.GroundMetricsJSON=JsonUtility.ToJson(ground,true);
                result.JPrefabSHA256=jDef.PrefabSHA256;result.ExteriorSHA256=FileHash(owned+"/union-exterior.png");
                result.PresentationReceiptSHA256=TextHash(assembly.ContentHash+"\n"+result.GroundSHA256+"\n"+result.JPrefabSHA256+"\n"+result.ExteriorSHA256);
                WriteNew(owned+"/geometry-recipe.json",geometryJson);WriteNew(owned+"/source-config.json",input.text);
                WriteNew(owned+"/resolved-catalog.json",catalogJson);
                WriteNew(owned+"/source-preflight.json",sceneReport.GroundPreflightJSON??"{}");
                File.WriteAllBytes(ProjectPath(owned+"/J-floor-mask.bin"),plan.FloorMaskBL);File.WriteAllBytes(ProjectPath(owned+"/J-soil-mask.bin"),plan.SoilMaskBL);
                result.Generated=true;WriteNew(owned+"/flexible-report.json",JsonUtility.ToJson(result,true));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch(Exception e){result.Generated=false;result.Error=e.ToString();}
            finally
            {
                if(transient!=null)Object.DestroyImmediate(transient);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                if(entry.IsValid()&&entry.isLoaded)EditorSceneManager.CloseScene(entry,true);
                if(map.IsValid()&&map.isLoaded)EditorSceneManager.CloseScene(map,true);
                if(!result.Generated&&created&&owned!=null)AssetDatabase.DeleteAsset(owned);
            }
            return result;
        }

        private static Color32[] ReadFlexPixels(string path,out int width,out int height)
        {
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try {Require(ImageConversion.LoadImage(t,File.ReadAllBytes(ProjectPath(path)),false),"Native texture decode failed.");width=t.width;height=t.height;return t.GetPixels32();}
            finally{Object.DestroyImmediate(t);}
        }
        private static string BytesHash(byte[] values)
        {using(var h=System.Security.Cryptography.SHA256.Create())return string.Concat(h.ComputeHash(values).Select(b=>b.ToString("x2")));}
        private static Sprite WriteFlexSprite(string path,int width,int height,Color32[] pixels)
        {
            Require(!File.Exists(ProjectPath(path)),"New output texture only.");var t=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            try{t.SetPixels32(pixels);t.Apply(false);File.WriteAllBytes(ProjectPath(path),ImageConversion.EncodeToPNG(t));}finally{Object.DestroyImmediate(t);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=64;importer.spritePivot=new Vector2(.5f,.5f);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.maxTextureSize=4096;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static StudyANativeFootUnit[] AddFlexScenery(GameObject jRoot,GameObject source,StudyANativeLocationDefinition definition,StudyANativeFlexibleConnectionPlan.Plan plan)
        {
            var feet=new List<StudyANativeFootUnit>();var owners=source.GetComponentsInChildren<StudyAIndependentObjectState>(true);
            var rocks=definition.FootUnits.Select(f=>new{Foot=f,Owner=owners.Single(o=>o.ObjectId==f.OwnerId)})
                .Where(v=>v.Owner.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite!=null&&r.sprite.name=="shore_rock"))
                .OrderBy(v=>v.Owner.NavigationRectanglesLocal.Sum(r=>r.width*r.height)).ToArray();
            Require(rocks.Length>0,"The preserved B source must supply an explicit native rock/foot owner.");
            for(var n=0;n<2;n++)
            {
                var sourceRock=rocks[n%rocks.Length];var clone=Object.Instantiate(sourceRock.Owner.gameObject);clone.name="J_rock_"+n;clone.transform.SetParent(jRoot.transform,false);
                var owner=clone.GetComponent<StudyAIndependentObjectState>();owner.ObjectId="j_rock_"+n;
                var foot=owner.transform.Find(sourceRock.Foot.PartPath);var baseFoot=jRoot.transform.InverseTransformPoint(foot.position);var originalPosition=clone.transform.localPosition;bool accepted=false;
                for(var step=0;step<32&&!accepted;step++)
                {
                    var tx=.28f+n*.38f+((step/8)%2==0?1:-1)*(step/8)*.035f;var x=Mathf.Clamp(Mathf.RoundToInt(tx*plan.ConnectorExtentPx.x),40,plan.ConnectorExtentPx.x-41);
                    var center=plan.SoilCentreYByXBL[x];var side=n==0?1:-1;var y=center+side*(58+step%8*4);
                    clone.transform.localPosition=originalPosition+new Vector3(x/64f,y/64f,0)-baseFoot;
                    var nav=owner.CurrentNavigationRectangles();
                    if(nav.All(r=>FlexContains(plan.FloorMaskBL,plan.ConnectorExtentPx,r))&&!nav.Any(r=>FlexRouteIntersects(plan,r)))accepted=true;
                }
                if(!accepted){Object.DestroyImmediate(clone);continue;}
                feet.Add(new StudyANativeFootUnit{OwnerId=owner.ObjectId,PartPath=sourceRock.Foot.PartPath,TieOrder=0});
            }
            var grasses=owners.Where(o=>!definition.FootUnits.Any(f=>f.OwnerId==o.ObjectId)&&o.NavigationRectanglesLocal.Length==0).ToArray();
            for(var n=0;n<6&&grasses.Length>0;n++)
            {
                var clone=Object.Instantiate(grasses[n%grasses.Length].gameObject);clone.name="J_grass_"+n;clone.transform.SetParent(jRoot.transform,false);
                var owner=clone.GetComponent<StudyAIndependentObjectState>();owner.ObjectId="j_grass_"+n;
                var visual=clone.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite!=null);var anchor=jRoot.transform.InverseTransformPoint(visual.transform.position);
                var x=Mathf.RoundToInt(plan.ConnectorExtentPx.x*(.15f+.14f*n));var y=plan.SoilCentreYByXBL[x]+(n%2==0?63:-63);
                clone.transform.localPosition+=new Vector3(x/64f,y/64f,0)-anchor;
            }
            return feet.ToArray();
        }
        private static bool FlexContains(byte[] mask,Vector2Int extent,Rect r)
        {
            var x0=Mathf.FloorToInt(r.xMin*64);var x1=Mathf.CeilToInt(r.xMax*64);var y0=Mathf.FloorToInt(r.yMin*64);var y1=Mathf.CeilToInt(r.yMax*64);
            if(x0<0||y0<0||x1>extent.x||y1>extent.y)return false;
            for(var y=y0;y<y1;y++)for(var x=x0;x<x1;x++)if(mask[x+y*extent.x]==0)return false;return true;
        }
        private static bool FlexRouteIntersects(StudyANativeFlexibleConnectionPlan.Plan p,Rect r)
        {for(var x=0;x<p.ConnectorExtentPx.x;x+=4)if(PositiveOverlap(r,HeroBody(new Vector2(x/64f,p.SoilCentreYByXBL[x]/64f))))return true;return false;}

        private static int ApplyFlexibleExterior(GameObject root,StudyANativeLocationGeometry.Plan geo,string output,Material material)
        {
            var source=ReadFlexPixels("Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/IsolatedStyleFamily-v2-20261004-215942621/Sources/bank_west_style.png",out var sw,out var sh);
            var frame=new RectInt(geo.BoundsPx.position-Vector2Int.one*128,geo.BoundsPx.size+Vector2Int.one*256);
            var floor=new byte[frame.width*frame.height];foreach(var r in geo.FloorsPx)for(var y=r.yMin;y<r.yMax;y++)for(var x=r.xMin;x<r.xMax;x++)floor[(x-frame.x)+(y-frame.y)*frame.width]=255;
            var pixels=new Color32[floor.Length];var count=0;
            for(var y=10;y<frame.height-10;y++)for(var x=10;x<frame.width-10;x++)
            {
                var i=x+y*frame.width;if(floor[i]!=0)continue;int distance=0,phase=0;
                for(var d=1;d<=9;d++)
                {
                    if(floor[i+d*frame.width]!=0||floor[i-d*frame.width]!=0){distance=d;phase=(x+frame.x)%64;break;}
                    if(floor[i+d]!=0||floor[i-d]!=0){distance=d;phase=(y+frame.y)%64;break;}
                }
                if(distance==0)continue;if(phase<0)phase+=64;var c=source[(389+distance-1)+(384+phase)*sw];if(c.a==0)continue;pixels[i]=c;count++;
            }
            var sprite=WriteFlexSprite(output+"/union-exterior.png",frame.width,frame.height,pixels);
            var sr=new GameObject("FlexibleUnionExterior").AddComponent<SpriteRenderer>();sr.transform.SetParent(root.transform,false);sr.transform.localPosition=new Vector3(frame.center.x/64f,frame.center.y/64f,0);sr.sprite=sprite;sr.sharedMaterial=material;sr.sortingLayerName="MapGround";sr.sortingOrder=3;
            return count;
        }
    }
}
