using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using FantasyRoyale.Playtest.OverheadStudyA;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>Explicit batch entry points for generating and independently reopening the reviewed S0/E outputs.</summary>
    public static class StudyANativeLocationBatchVerification
    {
        private const string ConfigRoot = "Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationLayouts-v2-20261006/";
        private static readonly string[] Configs = {
            ConfigRoot + "native-layout-exterior-quality-config-v1.json",
            ConfigRoot + "native-layout-exterior-quality-s0-seed3-config-v1.json"
        };

        [Serializable] public sealed class SavedFile { public string Path, SHA256; }
        [Serializable] public sealed class SpriteReadback
        {
            public string Path, TexturePath, GUID;
            public long LocalFileID;
            public Rect Rect;
            public Vector2 Pivot;
            public float PixelsPerUnit;
        }
        [Serializable] public sealed class ReloadReadback
        {
            public string RecipeId, MapScenePath, EntryScenePath, CapturePath;
            public int SourceOwnerCount, MapFootUnitCount, SpriteRendererCount, SourceSpriteRendererCount, ActiveSpriteRendererCount, ExteriorSegmentCount, ExteriorHostCount;
            public Rect NavigationFrame, CameraFrame;
            public string[] Checks;
            public SpriteReadback[] Sprites;
        }
        [Serializable] public sealed class Receipt
        {
            public bool Success;
            public string Phase, UTC, UnityVersion, GraphicsDevice, Error;
            public int ProcessId;
            public StudyANativeLocationSceneReport[] Reports;
            public SavedFile[] SavedFiles;
            public ReloadReadback[] Reloaded;
            public SpriteReadback[] ImportedOriginalGrounds;
        }

        [Serializable] public sealed class SourceRenderer
        {
            public string Name, Path, SpritePath;
            public bool Enabled, ActiveSelf, ActiveInHierarchy;
            public Vector3 Scale;
            public float PixelsPerUnit;
        }
        [Serializable] public sealed class SourcePrefab
        {
            public string Id, Path;
            public int Owners, FootGroups, Cameras, Navigations;
            public SourceRenderer[] Renderers;
        }
        [Serializable] public sealed class SourceInspection { public string UTC; public SourcePrefab[] Prefabs; }

        public static void InspectSourcesBatch()
        {
            Require(Application.isBatchMode && !Application.isPlaying, "Use an owned batch Editor in Edit Mode.");
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-studyNativeEvidence");
            Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "An explicit evidence directory is required.");
            var output = Path.Combine(args[index + 1], "unity-source-inspection.json");
            Require(!File.Exists(output), "Never overwrite source inspection evidence.");
            var config = JsonUtility.FromJson<StudyANativeLocationSceneConfig>(AssetDatabase.LoadAssetAtPath<TextAsset>(Configs[0]).text);
            var catalog = JsonUtility.FromJson<StudyANativeLocationCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(config.CatalogPath).text);
            var result = new SourceInspection { UTC = DateTime.UtcNow.ToString("o"), Prefabs = catalog.Locations.Select(d => {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.PrefabPath);
                Require(prefab != null, "Source prefab missing: " + d.Id);
                return new SourcePrefab { Id = d.Id, Path = d.PrefabPath, Owners = prefab.GetComponentsInChildren<StudyAIndependentObjectState>(true).Length,
                    FootGroups = prefab.GetComponentsInChildren<SortingGroup>(true).Length, Cameras = prefab.GetComponentsInChildren<Camera>(true).Length,
                    Navigations = prefab.GetComponentsInChildren<StudyAIndependentNavigation>(true).Length,
                    Renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true).Select(r => new SourceRenderer { Name = r.name,
                        Path = AnimationUtility.CalculateTransformPath(r.transform, prefab.transform), Enabled = r.enabled, ActiveSelf = r.gameObject.activeSelf,
                        ActiveInHierarchy = r.gameObject.activeInHierarchy, SpritePath = r.sprite == null ? null : AssetDatabase.GetAssetPath(r.sprite),
                        Scale = r.transform.lossyScale, PixelsPerUnit = r.sprite == null ? 0 : r.sprite.pixelsPerUnit }).ToArray() };
            }).ToArray() };
            File.WriteAllText(output, JsonUtility.ToJson(result, true) + "\n");
            Debug.Log("Native source inspection saved: " + output);
        }

        public static void GenerateBatch() => Run(false);
        public static void VerifyReloadBatch() => Run(true);

        private static void Run(bool reload)
        {
            Require(Application.isBatchMode && !Application.isPlaying, "Use an owned batch Editor in Edit Mode.");
            Require(Application.dataPath.Replace('\\', '/').EndsWith("/FantasyRoyale/Assets", StringComparison.Ordinal), "Unexpected Unity project.");
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-studyNativeEvidence");
            Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "An explicit absolute -studyNativeEvidence directory is required.");
            var evidence = Path.GetFullPath(args[index + 1]);
            Require(!evidence.StartsWith(Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase), "Keep batch evidence outside Assets.");
            Directory.CreateDirectory(evidence);
            var receiptPath = Path.Combine(evidence, reload ? "unity-reload-receipt.json" : "unity-generation-receipt.json");
            Require(!File.Exists(receiptPath), "Never overwrite an existing batch receipt.");
            var receipt = new Receipt { Phase = reload ? "independent-process-reload" : "generation-save",
                UTC = DateTime.UtcNow.ToString("o"), ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                UnityVersion = Application.unityVersion, GraphicsDevice = SystemInfo.graphicsDeviceType.ToString() };
            try
            {
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    Require(!SceneManager.GetSceneAt(i).isDirty, "The batch session contains a dirty scene; do not discard it.");
                if (!reload)
                {
                    var first = JsonUtility.FromJson<StudyANativeLocationSceneConfig>(AssetDatabase.LoadAssetAtPath<TextAsset>(Configs[0]).text);
                    var catalog = JsonUtility.FromJson<StudyANativeLocationCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(first.CatalogPath).text);
                    receipt.ImportedOriginalGrounds = catalog.Locations.Select(d => ReadSprite(d.GroundPath)).ToArray();
                    EditorSceneManager.OpenScene(first.MapTemplatePath, OpenSceneMode.Single);
                    var reports = new List<StudyANativeLocationSceneReport>();
                    for (var i = 0; i < Configs.Length; i++)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Configs[i]);
                        Require(asset != null, "Missing saved configuration: " + Configs[i]);
                        var report = StudyANativeLocationSceneBuilder.BuildConfigured(asset);
                        Require(report.Generated, "Generation failed: " + report.Error);
                        Require(report.RecipeId == (i == 0 ? "E" : "S0") && report.Seed == (i == 0 ? 0 : 3), "Auto recipe selected an unexpected layout.");
                        Require(report.Assembly.Blocks.Count == 4 && report.SourceOwnerCount == 43 && report.MapFootUnitCount == 35, "Generated source identities changed.");
                        reports.Add(report);
                        receipt.Reports = reports.ToArray();
                    }
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    receipt.Reports = reports.ToArray();
                    receipt.SavedFiles = Snapshot(receipt.Reports);
                }
                else
                {
                    var generated = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(evidence, "unity-generation-receipt.json")));
                    Require(generated != null && generated.Success && generated.Reports.Length == 2 && generated.ProcessId != receipt.ProcessId,
                        "Reload requires two saved outputs from a previous, different Unity process.");
                    VerifyFiles(generated.SavedFiles);
                    receipt.Reports = generated.Reports;
                    receipt.SavedFiles = generated.SavedFiles;
                    receipt.ImportedOriginalGrounds = generated.ImportedOriginalGrounds;
                    receipt.Reloaded = generated.Reports.Select(report => VerifyOutput(report, evidence)).ToArray();
                    VerifyFiles(generated.SavedFiles);
                }
                receipt.Success = true;
                File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true) + "\n");
                Debug.Log("Native location batch " + receipt.Phase + " PASS: " + receiptPath);
            }
            catch (Exception exception)
            {
                receipt.Error = exception.ToString();
                File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true) + "\n");
                throw;
            }
        }

        private static ReloadReadback VerifyOutput(StudyANativeLocationSceneReport report, string evidence)
        {
            var map = EditorSceneManager.OpenScene(report.MapScenePath, OpenSceneMode.Single);
            Scene entry = default;
            var checks = new List<string>();
            try
            {
                Require(!map.isDirty, "Saved Map is dirty immediately after reload.");
                var root = map.GetRootGameObjects().Single(g => g.name == "MapRoot");
                var owners = root.GetComponentsInChildren<StudyAIndependentObjectState>(true);
                var groups = root.GetComponentsInChildren<SortingGroup>(true);
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                var expectedExterior = report.RecipeId == "E" ? 78 : 82;
                var catalog = JsonUtility.FromJson<StudyANativeLocationCatalog>(File.ReadAllText(ProjectPath(report.OutputRoot + "/source-catalog.json")));
                var sourceRendererCount = catalog.Locations.Sum(d => AssetDatabase.LoadAssetAtPath<GameObject>(d.PrefabPath).GetComponentsInChildren<SpriteRenderer>(true).Length);
                Require(owners.Length == 43 && owners.All(o => o.ValidateContract()) && owners.Select(o => o.ObjectId).Distinct().Count() == 43,
                    "Source owners failed to persist.");
                Require(groups.Length == 35 && renderers.Length == sourceRendererCount + expectedExterior + (report.RecipeId == "E" ? 1 : 0) &&
                    renderers.All(r => r.sprite != null), "Map foot groups or sprite references failed to persist.");
                Require(sourceRendererCount == 115 && renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy) == 114 + expectedExterior + (report.RecipeId == "E" ? 1 : 0),
                    "Retain the original 114 active sprite parts and the inactive WaterHighlights renderer.");
                checks.Add("43 owner identities, 35 map foot groups and all " + sourceRendererCount + " authored SpriteRenderer components persisted");
                var nav = root.GetComponentsInChildren<StudyAIndependentNavigation>(true).Single();
                Require(nav.PixelsPerUnit == 64 && nav.FrameWorldBounds == report.FrameWorldBounds &&
                    nav.GeometryRecipeSha256 == report.GeometryRecipeSHA256 && nav.StaticBlockedRectanglesWorld.Length == report.StaticNavigationCount &&
                    nav.Owners.Length == 43 && new HashSet<StudyAIndependentObjectState>(nav.Owners).SetEquals(owners), "Navigation serialization changed.");
                Require(!nav.IsBlockedPoint(report.NativeSpawnWorld), "Saved spawn is blocked.");
                var config = JsonUtility.FromJson<StudyANativeLocationSceneConfig>(File.ReadAllText(ProjectPath(report.OutputRoot + "/source-config.json")));
                Require(new HashSet<string>(owners.Select(o => o.ObjectId), StringComparer.Ordinal).SetEquals(catalog.Locations.SelectMany(d => d.SourceOwnerIds)),
                    "Saved owner IDs differ from the authored catalog.");
                foreach (var block in report.Assembly.Blocks)
                {
                    var definition = catalog.Locations.Single(d => d.Id == block.LocationId);
                    var instance = root.transform.Cast<Transform>().Single(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == definition.PrefabPath);
                    Require(Vector3.Distance(instance.localPosition, new Vector3(block.OriginPx.x / 64f, block.OriginPx.y / 64f, 0)) < .00001f &&
                        Quaternion.Angle(instance.localRotation, Quaternion.identity) < .0001f && instance.localScale == Vector3.one, "Saved location transform changed.");
                    var ground = instance.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.sortingLayerName == "MapGround" &&
                        r.sortingOrder == 0 && r.GetComponentInParent<StudyAIndependentObjectState>() == null);
                    var expectedGround = definition.GroundPath;
                    if (report.RecipeId == "E" && block.LocationId == "B") expectedGround = ConfigRoot + "Sources/B_E_ground.png";
                    if (report.RecipeId == "E" && block.LocationId == "D") expectedGround = ConfigRoot + "Sources/D_W_ground.png";
                    Require(AssetDatabase.GetAssetPath(ground.sprite) == expectedGround, "Accepted ground override failed to persist: " + block.LocationId);
                }
                checks.Add("four native location transforms and E B/D ground overrides persisted");
                var geometry = StudyANativeLocationGeometry.MethodPlan(report.Assembly, 64);
                var recipe = JsonUtility.FromJson<StudyANativeLocationGeometryRecipe>(File.ReadAllText(ProjectPath(report.OutputRoot + "/geometry-recipe.json")));
                Require(nav.StaticBlockedRectanglesWorld.SequenceEqual(recipe.StaticBlockedRectanglesWorld), "Saved static navigation rectangle values changed.");
                var walls = root.transform.Cast<Transform>().Where(t => t.name == "NativePerimeter").ToArray();
                Require(walls.Length == geometry.PhysicalPerimeterPx.Length, "Saved physical perimeter count changed.");
                for (var i = 0; i < walls.Length; i++)
                {
                    var p = geometry.PhysicalPerimeterPx[i];
                    var rect = new Rect(p.x / 64f, p.y / 64f, p.width / 64f, p.height / 64f);
                    var collider = walls[i].GetComponent<BoxCollider2D>();
                    Require(collider != null && !collider.isTrigger && collider.size == rect.size && collider.offset == Vector2.zero &&
                        walls[i].gameObject.layer == LayerMask.NameToLayer("MapCollision") &&
                        Vector2.Distance(walls[i].position, rect.center) < .00001f, "Saved physical perimeter geometry changed.");
                }
                checks.Add("original physical perimeter and every static navigation rectangle persisted");
                var profile = JsonUtility.FromJson<StudyANativeExteriorPresentationProfile>(File.ReadAllText(ProjectPath(report.OutputRoot + "/source-exterior-presentation-profile.json")));
                var exterior = StudyANativeExteriorPresentation.BuildPlan(profile, report.Assembly, report.RecipeId, geometry);
                Require(exterior.Segments.Length == (report.RecipeId == "E" ? 11 : 12) && exterior.Placements.Length == expectedExterior, "Exterior segment plan changed.");
                for (var i = 0; i < exterior.Placements.Length; i++)
                {
                    var p = exterior.Placements[i];
                    var t = root.transform.Find("NativeExteriorPresentation-" + p.LocationId + "-" + p.Side + "-" + i);
                    Require(t != null, "Missing saved exterior host.");
                    var r = t.GetComponent<SpriteRenderer>();
                    Require(r != null && r.sortingLayerName == "MapGround" && r.sortingOrder == p.SortingOrder && r.drawMode == SpriteDrawMode.Simple &&
                        !r.flipX && !r.flipY && t.localScale == Vector3.one &&
                        Vector3.Distance(t.localPosition, new Vector3(p.WorldCenterPx.x / 64f, p.WorldCenterPx.y / 64f, 0)) < .00001f &&
                        Quaternion.Angle(t.localRotation, Quaternion.Euler(0, 0, p.RotationDegrees)) < .0001f &&
                        r.sprite.rect == new Rect(p.SourceRectPx.x, p.SourceRectPx.y, p.SourceRectPx.width, p.SourceRectPx.height) &&
                        AssetDatabase.GetAssetPath(r.sprite).StartsWith(report.OutputRoot + "/", StringComparison.Ordinal) &&
                        AssetDatabase.GetAssetPath(r.sprite.texture) == profile.SourceTexturePath &&
                        t.GetComponent<Collider2D>() == null && t.GetComponent<SortingGroup>() == null && t.GetComponent<StudyAIndependentObjectState>() == null,
                        "Saved exterior sprite placement, native slice or layer changed.");
                }
                var cap = renderers.Where(r => AssetDatabase.GetAssetPath(r.sprite) == report.OutputRoot + "/NativeGroundPresentation-0.asset").ToArray();
                Require(cap.Length == (report.RecipeId == "E" ? 1 : 0) && cap.All(r => r.sortingLayerName == "MapGround" && r.sortingOrder == 1), "C unused-exit presentation did not persist.");
                if (cap.Length == 1)
                {
                    var c = report.Assembly.Blocks.Single(b => b.LocationId == "C");
                    var r = cap[0];
                    var center = ((Vector2)c.OriginPx + new Vector2(32, 192)) / 64;
                    Require(r.sprite.rect == new Rect(0, 256, 64, 128) &&
                        AssetDatabase.GetAssetPath(r.sprite.texture) == catalog.Locations.Single(d => d.Id == "D").GroundPath &&
                        Vector2.Distance(r.transform.position, center) < .00001f && r.transform.localScale == Vector3.one &&
                        Quaternion.Angle(r.transform.rotation, Quaternion.identity) < .0001f && !r.flipX && !r.flipY &&
                        r.GetComponent<Collider2D>() == null && r.GetComponent<SortingGroup>() == null &&
                        r.GetComponent<StudyAIndependentObjectState>() == null, "Saved C terminal native slice/transform changed.");
                }
                checks.Add("23 combined exterior segments, native sprite UVs, orders2/3/4 and E C terminal order1 persisted");
                var sprites = Directory.GetFiles(ProjectPath(report.OutputRoot), "*.asset").OrderBy(p => p, StringComparer.Ordinal).Select(path => {
                    var assetPath = path.Replace('\\', '/').Substring(ProjectPath("Assets").Replace('\\', '/').Length - 6);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    Require(sprite != null && sprite.texture != null && sprite.pixelsPerUnit == 64 && sprite.pivot == sprite.rect.size / 2, "Reloaded Sprite.asset contract changed.");
                    return ReadSprite(assetPath);
                }).ToArray();
                Require(sprites.Length == (report.RecipeId == "E" ? 4 : 3), "Unexpected persistent Sprite asset count.");
                entry = EditorSceneManager.OpenScene(report.EntryScenePath, OpenSceneMode.Additive);
                var diagnostic = All<StudyABlockMovementDiagnostic>(entry).Single();
                var actor = All<StudyAIndependentActorRenderSetup>(entry).Single();
                var depth = All<StudyAIndependentStableDepthSort>(entry).Single();
                var expectedUnits = catalog.Locations.SelectMany(d => d.FootUnits).Select(u => u.OwnerId + "\n" + u.PartPath + "\n" + u.TieOrder);
                Require(depth.Units.Select(u => u.OwnerId + "\n" + u.PartPath + "\n" + u.TieOrder).SequenceEqual(expectedUnits), "Saved map foot unit identities/order changed.");
                Require(!entry.isDirty && diagnostic.MapScenePath == report.MapScenePath && actor.ExpectedMapScenePath == report.MapScenePath &&
                    depth.ExpectedMapScenePath == report.MapScenePath && actor.Diagnostic == diagnostic && depth.Diagnostic == diagnostic &&
                    depth.EnableForExperiment && depth.Units.Length == 35 && diagnostic.UseHumanInput && !diagnostic.AutoTraverse && diagnostic.FollowActorCamera &&
                    diagnostic.CameraMapBounds == report.PresentationFrameWorldBounds && diagnostic.HeroSprite != null && diagnostic.HeroMaterial != null &&
                    diagnostic.InputActions != null && diagnostic.Route.Length == 2 && diagnostic.Route.All(v => v == report.NativeSpawnWorld), "Saved Entry links/input/spawn/depth contract changed.");
                checks.Add("Entry Map references, saved input/spawn/camera and 35 serialized map depth units persisted");
                EditorSceneManager.CloseScene(entry, true); entry = default;
                var cameras = All<Camera>(map);
                var camera = cameras.Single(c => c.name == "Main Camera");
                Require(camera.isActiveAndEnabled && cameras.Count(c => c.name == "WestBaselineCamera_DISABLED" && !c.gameObject.activeSelf) == 1 && cameras.Length == 2,
                    "Saved map must retain one active Main Camera and the unchanged inactive A preview camera.");
                Require(camera.orthographic && camera.transparencySortMode == TransparencySortMode.CustomAxis && camera.transparencySortAxis == Vector3.up,
                    "Saved map camera contract changed.");
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Reload verification requires a graphics device for actual Unity rendering.");
                var bounds = report.PresentationFrameWorldBounds;
                Require(Vector3.Distance(camera.transform.position, new Vector3(bounds.center.x, bounds.center.y, -10)) < .00001f,
                    "Saved camera presentation-frame center changed.");
                var width = Mathf.RoundToInt(bounds.width * 64);
                var height = Mathf.RoundToInt(bounds.height * 64);
                var capture = Path.Combine(evidence, report.RecipeId + "-Seed" + report.Seed + "-unity-reloaded-native.png");
                Require(!File.Exists(capture), "Never overwrite a native Unity capture.");
                // The framing change exists only in this owned verification session; no scene is saved here.
                camera.orthographicSize = bounds.height / 2;
                camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.center.y, -10), Quaternion.identity);
                ForestRestartAssetBuilder.CaptureCamera(camera, capture, width, height);
                Require(File.Exists(capture) && new FileInfo(capture).Length > 1024, "Unity capture was not written.");
                checks.Add("actual Unity GPU camera render captured at native64PPU with the presentation frame");
                return new ReloadReadback { RecipeId = report.RecipeId, MapScenePath = report.MapScenePath, EntryScenePath = report.EntryScenePath,
                    CapturePath = capture, SourceOwnerCount = owners.Length, MapFootUnitCount = groups.Length, SpriteRendererCount = renderers.Length,
                    SourceSpriteRendererCount = sourceRendererCount, ActiveSpriteRendererCount = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy),
                    ExteriorSegmentCount = exterior.Segments.Length, ExteriorHostCount = expectedExterior, NavigationFrame = nav.FrameWorldBounds,
                    CameraFrame = bounds, Checks = checks.ToArray(), Sprites = sprites };
            }
            finally
            {
                if (entry.IsValid() && entry.isLoaded) EditorSceneManager.CloseScene(entry, true);
                if (map.IsValid() && map.isLoaded) EditorSceneManager.CloseScene(map, true);
            }
        }

        private static SavedFile[] Snapshot(IEnumerable<StudyANativeLocationSceneReport> reports) => reports
            .SelectMany(r => Directory.GetFiles(ProjectPath(r.OutputRoot), "*", SearchOption.AllDirectories))
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => new SavedFile { Path = p, SHA256 = Hash(p) }).ToArray();
        private static SpriteReadback ReadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null, "Imported Sprite is missing: " + path);
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId), "Imported Sprite identity is missing: " + path);
            return new SpriteReadback { Path = path, TexturePath = AssetDatabase.GetAssetPath(sprite.texture), GUID = guid, LocalFileID = localId,
                Rect = sprite.rect, Pivot = sprite.pivot, PixelsPerUnit = sprite.pixelsPerUnit };
        }
        private static void VerifyFiles(IEnumerable<SavedFile> files)
        {
            foreach (var file in files) Require(File.Exists(file.Path) && Hash(file.Path) == file.SHA256, "Saved output bytes changed during independent reload: " + file.Path);
        }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(b => b.ToString("x2"))); }
        private static string ProjectPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
