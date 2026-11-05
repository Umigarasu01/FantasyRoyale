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
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>Owned batch generation and different-process saved-asset readback. GPU and Play evidence are separate.</summary>
    public static class StudyANativeFlexibleBatchVerification
    {
        private const string ConfigRoot = "Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeFlexibleConnectionPilot-v1-20261008/";
        private static readonly string[] Configs = { ConfigRoot + "flexible-j6-config.json", ConfigRoot + "flexible-j4-config.json", ConfigRoot + "flexible-j8-config.json" };
        private static readonly int[] Lengths = { 384, 256, 512 };
        [Serializable] public sealed class SavedFile { public string Path, SHA256; public long Bytes; }
        [Serializable] public sealed class Capture
        {
            public string Path, SHA256, Qualification = "Actual Unity GPU full-map render at native64PPU; this is not the normal Hero-follow Play camera.";
            public int Width, Height, ConnectorLengthPx;
            public Rect PresentationFrame;
        }
        [Serializable] public sealed class ReloadReadback
        {
            public string OutputRoot, MapScenePath, EntryScenePath, FloorMaskSHA256, SoilMaskSHA256;
            public int ConnectorLengthPx, SourceOwnerCount, CoreOwners, JOwners, MapFootUnitCount, CoreFeet, JFeet, SpriteRendererCount, PerimeterColliders, VoidNavigationRectangles;
            public string[] Checks;
        }
        [Serializable] public sealed class Receipt
        {
            public bool Success;
            public string Phase, UTC, FinishedUTC, UnityVersion, GraphicsDevice, Error;
            public int ProcessId, GenerationProcessId;
            public StudyANativeFlexibleSceneReport[] Reports = Array.Empty<StudyANativeFlexibleSceneReport>();
            public SavedFile[] SavedFiles = Array.Empty<SavedFile>();
            public Capture[] Captures = Array.Empty<Capture>();
            public ReloadReadback[] Reloaded = Array.Empty<ReloadReadback>();
            public string Qualification = "Fixed explicit4/6/8-unit shape comparison; saved assets and GPU only. Actual traversal and contacts require the separate Play fixture.";
        }

        public static void GenerateBatch() => Run(false);
        public static void VerifyReloadBatch() => Run(true);

        private static void Run(bool reload)
        {
            Require(Application.isBatchMode && !Application.isPlaying, "Use an owned batch Editor in Edit Mode.");
            Require(Application.dataPath.Replace('\\', '/').EndsWith("/FantasyRoyale/Assets", StringComparison.Ordinal), "Unexpected Unity project.");
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-studyFlexibleEvidence");
            Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "Explicit absolute -studyFlexibleEvidence directory is required.");
            var evidence = Path.GetFullPath(args[index + 1]);
            Require(!Inside(evidence, Application.dataPath), "Keep batch evidence outside Assets.");
            Directory.CreateDirectory(evidence);
            var receiptPath = Path.Combine(evidence, reload ? "flexible-reload.json" : "flexible-generation.json");
            Require(!File.Exists(receiptPath), "Never overwrite an existing batch receipt.");
            var receipt = new Receipt { Phase = reload ? "independent-process-reload" : "generation-save-and-gpu",
                UTC = DateTime.UtcNow.ToString("o"), ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                UnityVersion = Application.unityVersion, GraphicsDevice = SystemInfo.graphicsDeviceType.ToString() };
            var listenerVolume = AudioListener.volume; var listenerPause = AudioListener.pause;
            try
            {
                AudioListener.volume = 0; AudioListener.pause = true;
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Actual GPU rendering requires a graphics device; do not use -nographics.");
                for (var i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Do not discard a dirty scene.");
                var first = ReadConfig(Configs[0]);
                var accepted = AssetDatabase.LoadAssetAtPath<TextAsset>(first.AcceptedConfigPath);
                Require(accepted != null, "Accepted config is missing.");
                var template = JsonUtility.FromJson<StudyANativeLocationSceneConfig>(accepted.text).MapTemplatePath;
                if (!reload)
                {
                    EditorSceneManager.OpenScene(template, OpenSceneMode.Single);
                    var reports = new List<StudyANativeFlexibleSceneReport>(); var captures = new List<Capture>();
                    for (var i = 0; i < Configs.Length; i++)
                    {
                        var cfg = ReadConfig(Configs[i]); Require(cfg.ConnectorLengthPx == Lengths[i], "Saved configuration length differs from the explicit6/4/8 comparison order.");
                        var result = StudyANativeLocationSceneBuilder.BuildFlexibleConfigured(AssetDatabase.LoadAssetAtPath<TextAsset>(Configs[i]));
                        Require(result.Generated && result.Scene != null && result.Scene.Generated, "Flexible generation failed: " + result.Error);
                        Require(result.ConnectorLengthPx == Lengths[i] && result.Seed == cfg.Seed && result.Scene.Assembly.Blocks.Select(b => b.LocationId).SequenceEqual(new[] { "B", "J", "D", "C" }), "Generated explicit pilot shape/seed differs.");
                        Require(result.Scene.SourceOwnerCount == 16 + result.JOwners && result.Scene.MapFootUnitCount == 12 + result.JFootUnits && result.Scene.WithHeroFootUnitCount == 13 + result.JFootUnits && result.Scene.WaterRectangleCount == 0, "Selected B/C/D core identities or water contract changed.");
                        reports.Add(result); receipt.Reports = reports.ToArray();
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        receipt.SavedFiles = Snapshot(receipt.Reports);
                        // Write partial progress before capture, then publish the first actual6-unit image before starting4 units.
                        SaveReceipt(receiptPath, receipt);
                        captures.Add(CaptureMap(result, evidence, "generated")); receipt.Captures = captures.ToArray();
                        VerifyFiles(receipt.SavedFiles); SaveReceipt(receiptPath, receipt);
                        EditorSceneManager.OpenScene(template, OpenSceneMode.Single);
                    }
                    receipt.SavedFiles = Snapshot(receipt.Reports);
                }
                else
                {
                    var generationPath = Path.Combine(evidence, "flexible-generation.json");
                    Require(File.Exists(generationPath), "Generation receipt is missing.");
                    var generated = JsonUtility.FromJson<Receipt>(File.ReadAllText(generationPath));
                    Require(generated != null && generated.Success && generated.Reports.Length == 3 && generated.ProcessId != receipt.ProcessId, "Reload needs three completed outputs from a different Unity process.");
                    Require(generated.Reports.Select(r => r.ConnectorLengthPx).SequenceEqual(Lengths), "Generation order differs.");
                    VerifyFiles(generated.SavedFiles);
                    receipt.GenerationProcessId = generated.ProcessId; receipt.Reports = generated.Reports; receipt.SavedFiles = generated.SavedFiles;
                    var readbacks = new List<ReloadReadback>(); var captures = new List<Capture>();
                    foreach (var result in generated.Reports)
                    {
                        readbacks.Add(VerifyOutput(result)); receipt.Reloaded = readbacks.ToArray();
                        captures.Add(CaptureMap(result, evidence, "reloaded")); receipt.Captures = captures.ToArray();
                        VerifyFiles(generated.SavedFiles); SaveReceipt(receiptPath, receipt);
                    }
                    VerifyFiles(generated.SavedFiles);
                }
                receipt.Success = true; receipt.FinishedUTC = DateTime.UtcNow.ToString("o"); SaveReceipt(receiptPath, receipt);
                Debug.Log("Flexible native batch " + receipt.Phase + " PASS: " + receiptPath);
            }
            catch (Exception e) { receipt.Error = e.ToString(); receipt.FinishedUTC = DateTime.UtcNow.ToString("o"); SaveReceipt(receiptPath, receipt); throw; }
            finally { AudioListener.volume = listenerVolume; AudioListener.pause = listenerPause; }
        }

        private static ReloadReadback VerifyOutput(StudyANativeFlexibleSceneReport result)
        {
            var report = result.Scene; var checks = new List<string>();
            var catalog = ReadJSON<StudyANativeLocationCatalog>(report.OutputRoot + "/resolved-catalog.json");
            var plan = StudyANativeFlexibleConnectionPlan.Build(catalog, result.ConnectorLengthPx, result.Seed);
            Require(plan.ContentHash == report.ContentHash && plan.FramePx == report.Assembly.NormalizedBoundsPx &&
                JsonUtility.ToJson(plan.Assembly) == JsonUtility.ToJson(report.Assembly), "Saved explicit assembly/content hash changed.");
            Require(StudyABlockGenerator.ValidateNativeAssembly(catalog, report.Assembly).Count == 0, "Resolved saved catalog is invalid.");
            var floor = File.ReadAllBytes(ProjectPath(report.OutputRoot + "/J-floor-mask.bin")); var soil = File.ReadAllBytes(ProjectPath(report.OutputRoot + "/J-soil-mask.bin"));
            Require(floor.SequenceEqual(plan.FloorMaskBL) && soil.SequenceEqual(plan.SoilMaskBL) && floor.All(v => v <= 1) && soil.All(v => v <= 1) &&
                Hash(floor) == result.FloorMaskSHA256 && Hash(soil) == result.SoilMaskSHA256, "Saved masks differ from the independently rebuilt binary actual-floor plan.");
            Require(floor.Any(v => v == 0) && plan.MinimumInteriorFloorWidthPx < 384, "A bent soil road on a rectangular floor is not the flexible candidate.");
            var recipePath = report.OutputRoot + "/geometry-recipe.json"; var recipe = ReadJSON<StudyANativeLocationGeometryRecipe>(recipePath);
            Require(HashFile(ProjectPath(recipePath)) == report.GeometryRecipeSHA256 && recipe.BoundsPx == plan.FramePx && recipe.PixelsPerUnit == 64 &&
                recipe.ContentHash == plan.ContentHash && recipe.FloorsPx.SequenceEqual(plan.FloorsPx) && recipe.PhysicalPerimeterPx.SequenceEqual(plan.PhysicalPerimeterPx) &&
                recipe.VoidInsideFramePx.SequenceEqual(plan.VoidInsideFramePx) && recipe.WaterRectanglesWorld.Length == 0 && !recipe.SoilAlphaCreatesCollision && !recipe.VoidCreatesPhysicalSlabs, "Saved floor/perimeter/void recipe differs from the actual mask plan.");
            var expectedBlocked = plan.PhysicalPerimeterPx.Concat(plan.VoidInsideFramePx).Select(World).ToArray();
            Require(recipe.StaticBlockedRectanglesWorld.SequenceEqual(expectedBlocked), "Saved static navigation does not derive from the common floor union.");
            checks.Add("different-sized B/J/D/C assembly, nonrectangular binary floor, soil reservation, full exact mask runs and perimeter/void rederived");
            var jDef = catalog.Locations.Single(d => d.Id == "J");
            var jp = ReadPixels(jDef.GroundPath, out var jw, out var jh); var be = catalog.Locations.Single(d => d.Id == "B"); var dw = catalog.Locations.Single(d => d.Id == "D");
            var bp = ReadPixels(be.GroundPath, out var bw, out var bh); var dp = ReadPixels(dw.GroundPath, out var dwidth, out var dh);
            Require(jw == result.ConnectorLengthPx && jh == 384 && bw == 384 && bh == 640 && dwidth == 640 && dh == 512 && HashFile(ProjectPath(jDef.GroundPath)) == result.GroundSHA256, "Native ground dimensions/hash changed.");
            for (var i = 0; i < floor.Length; i++) Require(jp[i].a == (floor[i] == 0 ? 0 : 255), "J ground alpha differs from the actual-floor mask at " + i);
            for (var y = 0; y < 384; y++) for (var depth = 0; depth < 32; depth++)
                Require(jp[y * jw + depth].Equals(bp[y * bw + bw - 1 - depth]) && jp[y * jw + jw - 1 - depth].Equals(dp[y * dwidth + depth]), "Fixed full384px endpoint collar RGBA changed.");
            foreach (var definition in catalog.Locations) VerifyNativeGround(definition.GroundPath, definition.ExtentPx);
            VerifyNativeGround(report.OutputRoot + "/union-exterior.png", new Vector2Int(plan.FramePx.width + 256, plan.FramePx.height + 256));
            checks.Add("native64PPU Point/full-rect imports, exact ground alpha and every received-source endpoint/collar RGBA texel verified");
            var map = EditorSceneManager.OpenScene(report.MapScenePath, OpenSceneMode.Single); Scene entry = default;
            try
            {
                Require(!map.isDirty, "Saved Map is dirty immediately after reopening."); Require(All<AudioSource>(map).Length == 0, "Silent batch requires zero AudioSources, including inactive objects.");
                var root = map.GetRootGameObjects().Single(g => g.name == "MapRoot");
                var owners = root.GetComponentsInChildren<StudyAIndependentObjectState>(true); var expectedIDs = catalog.Locations.SelectMany(d => d.SourceOwnerIds).ToArray();
                Require(owners.Length == 16 + result.JOwners && owners.Length == expectedIDs.Length && owners.All(o => o.ValidateContract()) &&
                    owners.Select(o => o.ObjectId).Distinct(StringComparer.Ordinal).Count() == owners.Length && new HashSet<string>(owners.Select(o => o.ObjectId), StringComparer.Ordinal).SetEquals(expectedIDs), "Saved owner IDs/contracts changed.");
                var groups = root.GetComponentsInChildren<SortingGroup>(true); var expectedFeet = catalog.Locations.SelectMany(d => d.FootUnits).ToArray();
                Require(groups.Length == 12 + result.JFootUnits && groups.Length == expectedFeet.Length, "Saved map foot groups differ.");
                foreach (var block in report.Assembly.Blocks)
                {
                    var def = catalog.Locations.Single(d => d.Id == block.LocationId); var source = AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath);
                    Require(source != null && HashFile(ProjectPath(def.PrefabPath)) == def.PrefabSHA256 && HashFile(ProjectPath(def.GroundPath)) == def.GroundSHA256, "Saved source/prefab hash differs: " + def.Id);
                    var instance = root.transform.Cast<Transform>().Single(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == def.PrefabPath);
                    Require(instance.localPosition == new Vector3(block.OriginPx.x / 64f, block.OriginPx.y / 64f, 0) && instance.localRotation == Quaternion.identity && instance.localScale == Vector3.one, "Saved native location transform changed.");
                    var ground = instance.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.sortingLayerName == "MapGround" && r.sortingOrder == 0 && r.GetComponentInParent<StudyAIndependentObjectState>() == null);
                    Require(AssetDatabase.GetAssetPath(ground.sprite) == def.GroundPath && ground.transform.lossyScale == Vector3.one, "Accepted/native ground binding changed: " + def.Id);
                    VerifyOwnedSource(source, instance.gameObject);
                }
                foreach (var foot in expectedFeet)
                {
                    var owner = owners.Single(o => o.ObjectId == foot.OwnerId); var part = owner.transform.Find(foot.PartPath);
                    Require(part != null && part.GetComponent<SortingGroup>() != null, "Saved registered foot path is missing.");
                }
                checks.Add("all16 original core owners and12 original foot units preserve source hierarchy transforms, sprites, navigation and collision; separate J IDs/groups persist");
                var nav = root.GetComponentsInChildren<StudyAIndependentNavigation>(true).Single();
                Require(nav.PixelsPerUnit == 64 && nav.FrameWorldBounds == report.FrameWorldBounds && nav.GeometryRecipeSha256 == report.GeometryRecipeSHA256 &&
                    nav.StaticBlockedRectanglesWorld.SequenceEqual(expectedBlocked) && nav.Owners.Select(o => o.ObjectId).SequenceEqual(expectedIDs) && !nav.IsBlockedPoint(report.NativeSpawnWorld), "Saved navigation/spawn serialization differs.");
                var walls = root.transform.Cast<Transform>().Where(t => t.name == "NativePerimeter").ToArray();
                Require(walls.Length == plan.PhysicalPerimeterPx.Length && walls.Length == report.PhysicalPerimeterCount, "Saved perimeter collider count differs.");
                for (var i = 0; i < walls.Length; i++)
                {
                    var rect = World(plan.PhysicalPerimeterPx[i]); var collider = walls[i].GetComponent<BoxCollider2D>();
                    Require(collider != null && !collider.isTrigger && collider.size == rect.size && collider.offset == Vector2.zero &&
                        walls[i].gameObject.layer == LayerMask.NameToLayer("MapCollision") && (Vector2)walls[i].position == rect.center && walls[i].localScale == Vector3.one, "Saved exact perimeter box differs.");
                }
                Require(report.VoidNavigationCount == plan.VoidInsideFramePx.Length && report.StaticNavigationCount == expectedBlocked.Length &&
                    root.transform.Cast<Transform>().All(t => t.name != "NativeVoid"), "Void must remain navigation-only, with no physical slabs.");
                checks.Add("every native-pixel perimeter BoxCollider2D and navigation-only void persisted from the same actual-floor union");
                var exterior = root.transform.Find("FlexibleUnionExterior"); var ex = exterior == null ? null : exterior.GetComponent<SpriteRenderer>();
                Require(ex != null && ex.sortingLayerName == "MapGround" && ex.sortingOrder == 3 && exterior.localScale == Vector3.one &&
                    AssetDatabase.GetAssetPath(ex.sprite) == report.OutputRoot + "/union-exterior.png" && exterior.GetComponents<Collider2D>().Length == 0 && exterior.GetComponent<StudyAIndependentObjectState>() == null, "Saved union exterior presentation differs.");
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                Require(renderers.All(r => r.sprite != null && r.sprite.pixelsPerUnit == 64 && r.transform.lossyScale == Vector3.one), "Saved sprite missing or stretched.");
                var cameras = All<Camera>(map); var camera = cameras.Single(c => c.name == "Main Camera");
                Require(cameras.Length == 1 && camera.isActiveAndEnabled && camera.orthographic && camera.transparencySortMode == TransparencySortMode.CustomAxis && camera.transparencySortAxis == Vector3.up &&
                    camera.transform.position == new Vector3(report.PresentationFrameWorldBounds.center.x, report.PresentationFrameWorldBounds.center.y, -10), "Saved pilot camera differs.");
                entry = EditorSceneManager.OpenScene(report.EntryScenePath, OpenSceneMode.Additive);
                Require(!entry.isDirty && All<AudioSource>(entry).Length == 0, "Saved Entry is dirty or has AudioSources.");
                var diagnostic = All<StudyABlockMovementDiagnostic>(entry).Single(); var actor = All<StudyAIndependentActorRenderSetup>(entry).Single(); var depth = All<StudyAIndependentStableDepthSort>(entry).Single();
                Require(diagnostic.MapScenePath == report.MapScenePath && actor.ExpectedMapScenePath == report.MapScenePath && depth.ExpectedMapScenePath == report.MapScenePath &&
                    actor.Diagnostic == diagnostic && depth.Diagnostic == diagnostic && depth.EnableForExperiment &&
                    depth.Units.Select(u => u.OwnerId + "\n" + u.PartPath + "\n" + u.TieOrder).SequenceEqual(expectedFeet.Select(u => u.OwnerId + "\n" + u.PartPath + "\n" + u.TieOrder)) &&
                    diagnostic.UseHumanInput && !diagnostic.AutoTraverse && diagnostic.FollowActorCamera && diagnostic.CameraMapBounds == report.PresentationFrameWorldBounds &&
                    diagnostic.HeroSprite != null && diagnostic.HeroMaterial != null && diagnostic.InputActions != null && diagnostic.Route.Length == 2 && diagnostic.Route.All(p => p == report.NativeSpawnWorld), "Saved Entry production input/camera/depth/spawn contract differs.");
                checks.Add("saved Entry links production human input, normal follow camera bounds, original Hero, explicit B spawn and map depth registry; zero AudioSources");
                return new ReloadReadback { OutputRoot = report.OutputRoot, MapScenePath = report.MapScenePath, EntryScenePath = report.EntryScenePath,
                    ConnectorLengthPx = result.ConnectorLengthPx, SourceOwnerCount = owners.Length, CoreOwners = 16, JOwners = result.JOwners,
                    MapFootUnitCount = groups.Length, CoreFeet = 12, JFeet = result.JFootUnits, SpriteRendererCount = renderers.Length,
                    PerimeterColliders = walls.Length, VoidNavigationRectangles = plan.VoidInsideFramePx.Length, FloorMaskSHA256 = Hash(floor), SoilMaskSHA256 = Hash(soil), Checks = checks.ToArray() };
            }
            finally { if (entry.IsValid() && entry.isLoaded) EditorSceneManager.CloseScene(entry, true); }
        }

        private static void VerifyOwnedSource(GameObject source, GameObject instance)
        {
            var originals = source.GetComponentsInChildren<StudyAIndependentObjectState>(true); var saved = instance.GetComponentsInChildren<StudyAIndependentObjectState>(true);
            foreach (var original in originals)
            {
                var owner = saved.Single(o => o.ObjectId == original.ObjectId);
                Require(owner.Present == original.Present && owner.NavigationRectanglesLocal.SequenceEqual(original.NavigationRectanglesLocal) && owner.AbsentNavigationRectanglesLocal.SequenceEqual(original.AbsentNavigationRectanglesLocal) &&
                    OwnedPaths(owner.PresentObjects, owner.transform).SequenceEqual(OwnedPaths(original.PresentObjects, original.transform)) && OwnedPaths(owner.AbsentObjects, owner.transform).SequenceEqual(OwnedPaths(original.AbsentObjects, original.transform)), "Saved owner state/navigation/references differ: " + original.ObjectId);
                foreach (var t in original.GetComponentsInChildren<Transform>(true))
                {
                    var path = AnimationUtility.CalculateTransformPath(t, original.transform); var other = path.Length == 0 ? owner.transform : owner.transform.Find(path);
                    Require(other != null && other.localPosition == t.localPosition && other.localRotation == t.localRotation && other.localScale == t.localScale && other.gameObject.activeSelf == t.gameObject.activeSelf, "Saved owned transform/state differs: " + original.ObjectId + "/" + path);
                    var a = t.GetComponents<SpriteRenderer>(); var b = other.GetComponents<SpriteRenderer>(); Require(a.Length == b.Length, "Saved owned renderer count differs.");
                    for (var n = 0; n < a.Length; n++) Require(a[n].sprite == b[n].sprite && a[n].sharedMaterial == b[n].sharedMaterial && a[n].enabled == b[n].enabled && a[n].color == b[n].color && a[n].flipX == b[n].flipX && a[n].flipY == b[n].flipY && a[n].sortingLayerID == b[n].sortingLayerID && a[n].sortingOrder == b[n].sortingOrder, "Saved owned sprite contract differs.");
                    var ac = t.GetComponents<Collider2D>(); var bc = other.GetComponents<Collider2D>(); Require(ac.Length == bc.Length, "Saved owned collider count differs.");
                    for (var n = 0; n < ac.Length; n++)
                    {
                        var originalSignature = ColliderSignature(ac[n]); var savedSignature = ColliderSignature(bc[n]);
                        Require(originalSignature == savedSignature, "Saved owned physics body differs. Owner=" + original.ObjectId + "; path=" + path + "; colliderIndex=" + n +
                            "; sourcePrefab=" + AssetDatabase.GetAssetPath(source) + "; savedScene=" + instance.scene.path +
                            "\nPROPERTY DIFFERENCES:\n" + SignatureDifferences(originalSignature, savedSignature) +
                            "\nSOURCE FULL SIGNATURE:\n" + originalSignature + "\nSAVED FULL SIGNATURE:\n" + savedSignature);
                    }
                }
            }
        }

        private static string ColliderSignature(Collider2D c)
        {
            // Editor serialization supplies the complete authored shape without instance-dependent object references.
            var s = new SerializedObject(c); var iterator = s.GetIterator(); var values = new List<string>(); var enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                // Object references have internal m_FileID/m_PathID children. Scene and asset instance IDs differ;
                // compare the stable asset path below and suppress those children, including skipped identity roots.
                enterChildren = iterator.propertyType != SerializedPropertyType.ObjectReference;
                if (iterator.propertyPath == "m_GameObject" || iterator.propertyPath == "m_CorrespondingSourceObject" || iterator.propertyPath == "m_PrefabInstance" || iterator.propertyPath == "m_PrefabAsset") continue;
                if (iterator.propertyType == SerializedPropertyType.ObjectReference) values.Add(iterator.propertyPath + "=" + (iterator.objectReferenceValue == null ? "null" : AssetDatabase.GetAssetPath(iterator.objectReferenceValue)));
                else if (iterator.propertyType == SerializedPropertyType.Generic) continue;
                else values.Add(iterator.propertyPath + "=" + PropertyValue(iterator));
            }
            return c.GetType().FullName + "\n" + string.Join("\n", values);
        }
        private static string SignatureDifferences(string original, string saved)
        {
            var sourceLines = original.Split('\n'); var savedLines = saved.Split('\n'); var differences = new List<string>();
            for (var n = 0; n < Math.Max(sourceLines.Length, savedLines.Length); n++)
            {
                var a = n < sourceLines.Length ? sourceLines[n] : "<missing>"; var b = n < savedLines.Length ? savedLines[n] : "<missing>";
                if (a != b) differences.Add("line" + n + ": SOURCE[" + a + "] SAVED[" + b + "]");
            }
            return string.Join("\n", differences);
        }
        private static string PropertyValue(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.LayerMask: case SerializedPropertyType.ArraySize: case SerializedPropertyType.Character: return p.longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.doubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Vector2: return JsonUtility.ToJson(p.vector2Value);
                case SerializedPropertyType.Vector3: return JsonUtility.ToJson(p.vector3Value);
                case SerializedPropertyType.Vector4: return JsonUtility.ToJson(p.vector4Value);
                case SerializedPropertyType.Quaternion: return JsonUtility.ToJson(p.quaternionValue);
                case SerializedPropertyType.Rect: return JsonUtility.ToJson(p.rectValue);
                case SerializedPropertyType.Bounds: return JsonUtility.ToJson(p.boundsValue);
                case SerializedPropertyType.Color: return JsonUtility.ToJson(p.colorValue);
                default: throw new InvalidOperationException("Unhandled authored collider property: " + p.propertyPath + "/" + p.propertyType);
            }
        }
        private static string[] OwnedPaths(GameObject[] items, Transform owner) => items.Select(g => AnimationUtility.CalculateTransformPath(g.transform, owner)).ToArray();

        private static Capture CaptureMap(StudyANativeFlexibleSceneReport result, string evidence, string phase)
        {
            var report = result.Scene; var map = EditorSceneManager.OpenScene(report.MapScenePath, OpenSceneMode.Single);
            Require(!map.isDirty && All<AudioSource>(map).Length == 0, "Capture saved silent Map only.");
            var camera = All<Camera>(map).Single(c => c.name == "Main Camera"); var bounds = report.PresentationFrameWorldBounds;
            var previousPosition = camera.transform.position; var previousRotation = camera.transform.rotation; var previousSize = camera.orthographicSize; var previousAspect = camera.aspect;
            var width = Mathf.RoundToInt(bounds.width * 64); var height = Mathf.RoundToInt(bounds.height * 64);
            var path = Path.Combine(evidence, "J-" + result.ConnectorLengthPx / 64 + "u-Seed" + result.Seed + "-" + phase + "-native.png");
            Require(!File.Exists(path), "Never overwrite a GPU image.");
            try
            {
                camera.orthographicSize = bounds.height / 2; camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.center.y, -10), Quaternion.identity);
                ForestRestartAssetBuilder.CaptureCamera(camera, path, width, height);
                Require(File.Exists(path) && new FileInfo(path).Length > 1024, "Actual Unity GPU image was not written.");
                return new Capture { Path = path, SHA256 = HashFile(path), Width = width, Height = height, ConnectorLengthPx = result.ConnectorLengthPx, PresentationFrame = bounds };
            }
            finally { camera.transform.SetPositionAndRotation(previousPosition, previousRotation); camera.orthographicSize = previousSize; camera.aspect = previousAspect; }
        }

        private static void VerifyNativeGround(string path, Vector2Int extent)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings(); if (importer != null) importer.ReadTextureSettings(settings);
            Require(sprite != null && importer != null && sprite.pixelsPerUnit == 64 && sprite.rect.size == (Vector2)extent && sprite.pivot == sprite.rect.size / 2 &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.spriteImportMode == SpriteImportMode.Single && settings.spriteMeshType == SpriteMeshType.FullRect, "Native ground import differs: " + path);
        }
        private static Color32[] ReadPixels(string path, out int width, out int height)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try { Require(ImageConversion.LoadImage(t, File.ReadAllBytes(ProjectPath(path)), false), "PNG decode failed: " + path); width = t.width; height = t.height; return t.GetPixels32(); }
            finally { Object.DestroyImmediate(t); }
        }
        private static SavedFile[] Snapshot(IEnumerable<StudyANativeFlexibleSceneReport> reports) => reports.SelectMany(r => Directory.GetFiles(ProjectPath(r.Scene.OutputRoot), "*", SearchOption.AllDirectories).Concat(new[] { ProjectPath(r.Scene.OutputRoot) + ".meta" }))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal).Select(p => new SavedFile { Path = p, SHA256 = HashFile(p), Bytes = new FileInfo(p).Length }).ToArray();
        private static void VerifyFiles(IEnumerable<SavedFile> files) { foreach (var f in files) Require(File.Exists(f.Path) && new FileInfo(f.Path).Length == f.Bytes && HashFile(f.Path) == f.SHA256, "Saved output bytes changed: " + f.Path); }
        private static StudyANativeFlexibleSceneConfig ReadConfig(string path) { var a = AssetDatabase.LoadAssetAtPath<TextAsset>(path); Require(a != null && !EditorUtility.IsDirty(a), "Saved flexible config missing/dirty: " + path); return JsonUtility.FromJson<StudyANativeFlexibleSceneConfig>(a.text); }
        private static T ReadJSON<T>(string assetPath) => JsonUtility.FromJson<T>(File.ReadAllText(ProjectPath(assetPath)));
        private static void SaveReceipt(string path, Receipt receipt) => File.WriteAllText(path, JsonUtility.ToJson(receipt, true) + "\n");
        private static string HashFile(string path) => Hash(File.ReadAllBytes(path));
        private static string Hash(byte[] bytes) { using (var h = SHA256.Create()) return string.Concat(h.ComputeHash(bytes).Select(b => b.ToString("x2"))); }
        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        private static bool Inside(string path, string root) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        private static Rect World(RectInt r) => new Rect(r.x / 64f, r.y / 64f, r.width / 64f, r.height / 64f);
        private static T[] All<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
