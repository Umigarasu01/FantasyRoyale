using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    [Serializable]
    public sealed class StudyANativeLocationSceneConfig
    {
        public string CatalogPath;
        public string LayoutSetPath, RecipeId, GroundAcceptanceProfilePath, GroundAcceptanceProfileSHA256;
        public string ArtAcceptanceReceiptPath, ArtAcceptanceReceiptSHA256;
        public string NativeGroundPresentationProfilePath, NativeGroundPresentationProfileSHA256;
        public string NativeExteriorPresentationProfilePath, NativeExteriorPresentationProfileSHA256;
        public int Seed;
        public int MinBlocks = 2, MaxBlocks = 4, RequestedBlockCount;
        public string OutputRoot;
        public string MapTemplatePath, EntryTemplatePath, WaterTemplatePrefabPath;
        public string MapTemplateSHA256, EntryTemplateSHA256, WaterTemplatePrefabSHA256;
        public int WaterTemplatePrefixCount = 283;
    }

    [Serializable]
    public sealed class StudyANativeLocationSceneReport
    {
        public bool Generated, StructuralOnly = true, UnityRuntimeVerified;
        public string Error, ConfigPath, CatalogPath, CatalogSHA256, OutputRoot, MapScenePath, EntryScenePath;
        public string ContentHash, GeometryRecipeSHA256, FullCatalogQualification;
        public string LayoutSetPath, LayoutSetSHA256, RecipeId, LayoutContentHash, GroundPreflightSHA256;
        public string NativeGroundPresentationSHA256;
        public int NativeGroundPresentationSpriteCount;
        [NonSerialized] public string NativeGroundPresentationJSON;
        public string NativeExteriorPresentationSHA256;
        public int NativeExteriorPresentationSpriteCount, NativeExteriorPresentationAssetCount;
        [NonSerialized] public string NativeExteriorPresentationJSON;
        public string GroundAcceptanceQualification;
        [NonSerialized] public string GroundPreflightJSON, LayoutResolutionJSON;
        public int Seed, PixelsPerUnit, SourceOwnerCount, MapFootUnitCount, WithHeroFootUnitCount;
        public int WaterRectangleCount, PhysicalPerimeterCount, VoidNavigationCount, StaticNavigationCount;
        public Vector2 NativeSpawnWorld;
        public Rect NativeSpawnBodyWorld;
        public bool SpawnPointAndBodyValidated;
        public float SpawnBodyMargin = .01f;
        public Rect FrameWorldBounds, PresentationFrameWorldBounds;
        public StudyANativeLocationAssembly Assembly;
        public string Qualification = "New copied scenes and source registration only. Compile, import, movement, camera and runtime depth require separate permitted verification.";
    }

    [Serializable]
    public sealed class StudyANativeLocationGeometryRecipe
    {
        public int PixelsPerUnit;
        public string CatalogSHA256, ContentHash, WaterTemplatePrefabSHA256;
        public RectInt BoundsPx;
        public RectInt[] FloorsPx, PhysicalPerimeterPx, VoidInsideFramePx;
        public Rect[] WaterRectanglesWorld, StaticBlockedRectanglesWorld;
        public bool SharedGrassFloorOpen = true, SoilAlphaCreatesCollision, VoidCreatesPhysicalSlabs;
    }

    /// <summary>Explicit opt-in native location builder. Never replaces a source asset or an existing output.</summary>
    public static partial class StudyANativeLocationSceneBuilder
    {
        public const string DefaultConfigPath = "Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationAssembly-v1-20261006/native-assembly-config.json";
        private const string ScenePrefix = "Assets/Scenes/MapAuthoring/OverheadStudyA/";
        private const int PPU = 64;

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Generate Configured Native Location Assembly")]
        public static void GenerateConfigured()
        {
            var config = Selection.activeObject as TextAsset;
            if (config == null) config = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultConfigPath);
            var report = BuildConfigured(config);
            if (report.Generated) Debug.Log(JsonUtility.ToJson(report, true));
            else Debug.LogWarning(JsonUtility.ToJson(report, true));
        }

        public static StudyANativeLocationSceneReport BuildConfigured(TextAsset configAsset)
        {
            var report = new StudyANativeLocationSceneReport { PixelsPerUnit = PPU };
            var previous = SceneManager.GetActiveScene();
            Scene map = default, entry = default;
            string ownedOutput = null;
            bool outputCreated = false;
            try
            {
                GuardEditor();
                Require(configAsset != null, "Select a native config TextAsset or create the documented default config.");
                Require(!EditorUtility.IsDirty(configAsset), "The selected config has unsaved changes.");
                var config = JsonUtility.FromJson<StudyANativeLocationSceneConfig>(configAsset.text);
                Require(config != null, "Native config JSON is invalid.");
                var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath(config.CatalogPath));
                Require(catalogAsset != null && !EditorUtility.IsDirty(catalogAsset), "Catalog TextAsset is missing or dirty.");
                var catalog = JsonUtility.FromJson<StudyANativeLocationCatalog>(catalogAsset.text);
                var sourceCatalog = catalog;
                Dictionary<string, Sprite> groundOverrides = null;
                StudyANativeLocationAssembly assembly;
                if (string.IsNullOrEmpty(config.LayoutSetPath))
                    assembly = StudyABlockGenerator.GenerateNativeAssembly(catalog, config.Seed, config.MinBlocks, config.MaxBlocks, config.RequestedBlockCount);
                else
                    assembly = ResolveConfiguredNativeLayout(config, catalog, report, out catalog, out groundOverrides);
                var errors = StudyABlockGenerator.ValidateNativeAssembly(catalog, assembly);
                Require(errors.Count == 0, string.Join("; ", errors));
                var plan = StudyANativeLocationGeometry.MethodPlan(assembly, PPU);
                var definitions = catalog.Locations.ToDictionary(d => d.Id, StringComparer.Ordinal);
                var selected = new HashSet<string>(assembly.Blocks.Select(b => b.LocationId), StringComparer.Ordinal);
                // Authored catalog order preserves the original full-catalog owner/unit sequence.
                var ordered = catalog.Locations.Where(d => selected.Contains(d.Id)).ToArray();
                var prefabs = ordered.ToDictionary(d => d.Id, d => GuardNativePrefab(d), StringComparer.Ordinal);
                var originalGrounds = sourceCatalog.Locations.Where(d => selected.Contains(d.Id)).ToDictionary(d => d.Id,
                    d => AssetDatabase.LoadAssetAtPath<Sprite>(d.GroundPath), StringComparer.Ordinal);
                Require(originalGrounds.Values.All(s => s != null), "Every authored native ground must import as an existing Sprite.");
                GuardFile(config.MapTemplatePath, config.MapTemplateSHA256);
                GuardFile(config.EntryTemplatePath, config.EntryTemplateSHA256);
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(config.MapTemplatePath) != null &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(config.EntryTemplatePath) != null, "Both copied templates must be existing scene assets.");
                var presentation = ResolveNativePresentation(config, assembly, report.RecipeId, out var presentationJSON);
                var exterior = ResolveNativeExteriorPresentation(config, assembly, report.RecipeId, plan, out var exteriorJSON);
                var water = ReadWater(config, assembly);
                var frame = World(plan.BoundsPx);
                var blocked = water.Concat(plan.PhysicalPerimeterPx.Select(World)).Concat(plan.VoidInsideFramePx.Select(World)).ToArray();
                var first = assembly.Blocks[0];
                var spawn = ((Vector2)first.OriginPx + definitions[first.LocationId].SpawnPx) / PPU;
                Require(Finite(spawn.x) && Finite(spawn.y) && plan.FloorsPx.Any(r => World(r).Contains(spawn)), "Native spawn is outside actual selected floors.");
                Require(!blocked.Any(r => r.Contains(spawn)), "Native spawn intersects static navigation geometry.");
                var spawnBody = HeroBody(spawn);
                Require(!blocked.Any(r => PositiveOverlap(r, spawnBody)), "Native Hero body with .01 margin intersects static navigation geometry.");
                if (selected.Contains("A"))
                    Require(Vector2.Distance(definitions["A"].SpawnPx / PPU, new Vector2(.65f, 4.640625f)) < .00001f,
                        "Preserve authored continuous A spawn(.65,4.640625); do not round it to a pixel or cell.");
                Require(LayerMask.NameToLayer("MapCollision") >= 0, "MapCollision layer is required.");
                var outputBase = AssetPath(config.OutputRoot);
                Require(outputBase.StartsWith(ScenePrefix, StringComparison.Ordinal), "OutputRoot must be inside the isolated OverheadStudyA scene directory.");
                Require(!string.IsNullOrEmpty(assembly.ContentHash) && assembly.ContentHash.Length >= 12, "Native content hash is missing.");
                ownedOutput = outputBase + "/Seed-" + config.Seed.ToString(CultureInfo.InvariantCulture) + "-" + assembly.ContentHash.Substring(0, 12);
                Require(!Directory.Exists(ProjectPath(ownedOutput)) && !File.Exists(ProjectPath(ownedOutput)) &&
                    !File.Exists(ProjectPath(ownedOutput) + ".meta") && !AssetDatabase.IsValidFolder(ownedOutput), "Configured output already exists; choose another config/seed or a new OutputRoot.");
                report.ConfigPath = AssetDatabase.GetAssetPath(configAsset);
                report.CatalogPath = config.CatalogPath; report.CatalogSHA256 = FileHash(config.CatalogPath);
                report.Seed = config.Seed; report.ContentHash = assembly.ContentHash; report.Assembly = assembly;
                if (presentationJSON != null)
                {
                    report.NativeGroundPresentationSHA256 = config.NativeGroundPresentationProfileSHA256;
                    report.NativeGroundPresentationJSON = presentationJSON;
                }
                if (exteriorJSON != null)
                {
                    report.NativeExteriorPresentationSHA256 = config.NativeExteriorPresentationProfileSHA256;
                    report.NativeExteriorPresentationJSON = exteriorJSON;
                }
                report.OutputRoot = ownedOutput; report.MapScenePath = ownedOutput + "/NativeMap.unity";
                report.EntryScenePath = ownedOutput + "/NativeEntry.unity";
                report.FrameWorldBounds = frame; report.NativeSpawnWorld = spawn;
                report.PresentationFrameWorldBounds = exterior == null ? frame : World(exterior.Plan.PresentationFramePx);
                report.NativeSpawnBodyWorld = spawnBody;
                report.WaterRectangleCount = water.Length; report.PhysicalPerimeterCount = plan.PhysicalPerimeterPx.Length;
                report.VoidNavigationCount = plan.VoidInsideFramePx.Length; report.StaticNavigationCount = blocked.Length;
                report.FullCatalogQualification = assembly.FixedFullCatalogLayoutQualification;
                var recipe = new StudyANativeLocationGeometryRecipe { PixelsPerUnit = PPU, BoundsPx = plan.BoundsPx,
                    FloorsPx = plan.FloorsPx, PhysicalPerimeterPx = plan.PhysicalPerimeterPx, VoidInsideFramePx = plan.VoidInsideFramePx,
                    WaterRectanglesWorld = water, StaticBlockedRectanglesWorld = blocked, ContentHash = assembly.ContentHash,
                    CatalogSHA256 = report.CatalogSHA256, WaterTemplatePrefabSHA256 = water.Length == 0 ? null : config.WaterTemplatePrefabSHA256 };
                var recipeJSON = JsonUtility.ToJson(recipe, true) + "\n";
                report.GeometryRecipeSHA256 = TextHash(recipeJSON);
                // All environment, source, plan and output guards above precede the first mutation.
                EnsureFolder(ownedOutput); outputCreated = true;
                Require(AssetDatabase.CopyAsset(config.MapTemplatePath, report.MapScenePath), "Could not copy the map template into the fresh output.");
                Require(AssetDatabase.CopyAsset(config.EntryTemplatePath, report.EntryScenePath), "Could not copy the entry template into the fresh output.");
                map = EditorSceneManager.OpenScene(report.MapScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(map);
                var units = BuildMap(map, assembly, definitions, prefabs, ordered, plan, blocked, report, originalGrounds, groundOverrides, presentation, exterior, ownedOutput);
                Require(EditorSceneManager.SaveScene(map, report.MapScenePath), "Saving the new map scene failed.");
                entry = EditorSceneManager.OpenScene(report.EntryScenePath, OpenSceneMode.Additive);
                BuildEntry(entry, report, units);
                Require(EditorSceneManager.SaveScene(entry, report.EntryScenePath), "Saving the new entry scene failed.");
                WriteNew(ownedOutput + "/geometry-recipe.json", recipeJSON);
                WriteNew(ownedOutput + "/source-config.json", configAsset.text);
                WriteNew(ownedOutput + "/source-catalog.json", catalogAsset.text);
                WriteConfiguredNativeLayoutEvidence(config, ownedOutput, report);
                if (report.NativeGroundPresentationJSON != null)
                    WriteNew(ownedOutput + "/source-ground-presentation-profile.json", report.NativeGroundPresentationJSON);
                if (report.NativeExteriorPresentationJSON != null)
                    WriteNew(ownedOutput + "/source-exterior-presentation-profile.json", report.NativeExteriorPresentationJSON);
                report.Generated = true;
                WriteNew(ownedOutput + "/assembly-report.json", JsonUtility.ToJson(report, true) + "\n");
                AssetDatabase.ImportAsset(ownedOutput + "/geometry-recipe.json");
                AssetDatabase.ImportAsset(ownedOutput + "/source-config.json");
                AssetDatabase.ImportAsset(ownedOutput + "/source-catalog.json");
                AssetDatabase.ImportAsset(ownedOutput + "/assembly-report.json");
            }
            catch (Exception exception)
            {
                report.Generated = false; report.Error = exception.ToString();
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (entry.IsValid() && entry.isLoaded) EditorSceneManager.CloseScene(entry, true);
                if (map.IsValid() && map.isLoaded) EditorSceneManager.CloseScene(map, true);
                // This path was absent at preflight and created exclusively by this invocation.
                if (!report.Generated && outputCreated && ownedOutput != null) AssetDatabase.DeleteAsset(ownedOutput);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            return report;
        }

        private static StudyAIndependentStableDepthSort.Unit[] BuildMap(Scene scene, StudyANativeLocationAssembly assembly,
            Dictionary<string, StudyANativeLocationDefinition> definitions, Dictionary<string, GameObject> prefabs,
            StudyANativeLocationDefinition[] ordered, StudyANativeLocationGeometry.Plan plan, Rect[] blocked,
            StudyANativeLocationSceneReport report, Dictionary<string, Sprite> originalGrounds, Dictionary<string, Sprite> groundOverrides = null,
            NativePresentationAssets presentation = null, NativeExteriorPresentationAssets exterior = null, string ownedOutput = null)
        {
            var oldRoots = scene.GetRootGameObjects().Where(g => g.name == "MapRoot").ToArray();
            Require(oldRoots.Length == 1, "Copied map template needs exactly one MapRoot.");
            var old = oldRoots[0];
            if (PrefabUtility.IsPartOfPrefabInstance(old))
                PrefabUtility.UnpackPrefabInstance(old, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var oldCameras = old.GetComponentsInChildren<Camera>(true);
            Require(oldCameras.Count(c => c.name == "Main Camera" && c.isActiveAndEnabled) == 1 &&
                oldCameras.Where(c => c.name != "Main Camera").All(IsDormantNativePreviewCamera),
                "The copied template needs one active Main Camera and only the known inactive source preview camera.");
            foreach (var camera in oldCameras.Where(c => c.name == "Main Camera")) camera.transform.SetParent(null, true);
            Object.DestroyImmediate(old);
            var mapRoot = new GameObject("MapRoot"); SceneManager.MoveGameObjectToScene(mapRoot, scene);
            mapRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); mapRoot.transform.localScale = Vector3.one;
            foreach (var block in assembly.Blocks)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[block.LocationId], scene);
                Require(instance != null, "Selected native prefab instantiation failed.");
                instance.transform.SetParent(mapRoot.transform, false);
                instance.transform.localPosition = new Vector3(block.OriginPx.x / (float)PPU, block.OriginPx.y / (float)PPU, 0);
                instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
                BindNativeSourceGround(instance, originalGrounds[block.LocationId]);
                if (groundOverrides != null && groundOverrides.ContainsKey(block.LocationId))
                    ApplyAcceptedNativeGround(instance, block.LocationId, groundOverrides[block.LocationId]);
            }
            var states = mapRoot.GetComponentsInChildren<StudyAIndependentObjectState>(true);
            Require(states.All(o => o != null && !string.IsNullOrEmpty(o.ObjectId) && o.ValidateContract()), "Selected owner contracts are invalid.");
            Require(states.Select(o => o.ObjectId).Distinct(StringComparer.Ordinal).Count() == states.Length, "Selected source owner IDs duplicate.");
            var expectedIds = ordered.SelectMany(d => d.SourceOwnerIds).ToArray();
            Require(expectedIds.Length == states.Length && new HashSet<string>(expectedIds, StringComparer.Ordinal).SetEquals(states.Select(o => o.ObjectId)),
                "Selected prefab owner IDs do not exactly match catalog SourceOwnerIds.");
            var byId = states.ToDictionary(o => o.ObjectId, StringComparer.Ordinal);
            var units = ordered.SelectMany(d => d.FootUnits).Select(u => new StudyAIndependentStableDepthSort.Unit {
                OwnerId = u.OwnerId, PartPath = u.PartPath, TieOrder = u.TieOrder }).ToArray();
            var groups = new HashSet<SortingGroup>();
            foreach (var unit in units)
            {
                Require(byId.ContainsKey(unit.OwnerId), "Foot unit owner is missing.");
                var part = byId[unit.OwnerId].transform.Find(unit.PartPath);
                Require(part != null && part.GetComponent<SortingGroup>() != null && groups.Add(part.GetComponent<SortingGroup>()), "Foot path is missing or duplicated.");
            }
            Require(groups.Count == mapRoot.GetComponentsInChildren<SortingGroup>(true).Length, "Unregistered logical SortingGroup exists in selected source prefabs.");
            var nav = mapRoot.AddComponent<StudyAIndependentNavigation>(); nav.PixelsPerUnit = PPU;
            nav.FrameWorldBounds = World(plan.BoundsPx); nav.GeometryRecipeSha256 = report.GeometryRecipeSHA256;
            nav.StaticBlockedRectanglesWorld = blocked; nav.Owners = expectedIds.Select(id => byId[id]).ToArray();
            foreach (var rectangle in plan.PhysicalPerimeterPx)
            {
                var world = World(rectangle); var wall = new GameObject("NativePerimeter");
                wall.transform.SetParent(mapRoot.transform, false); wall.transform.localPosition = world.center;
                wall.layer = LayerMask.NameToLayer("MapCollision");
                var collider = wall.AddComponent<BoxCollider2D>(); collider.size = world.size; collider.offset = Vector2.zero;
            }
            ApplyNativePresentation(mapRoot, ownedOutput, report, presentation);
            ApplyNativeExteriorPresentation(mapRoot, ownedOutput, report, exterior);
            Require(mapRoot.GetComponentsInChildren<StudyAIndependentObjectState>(true).Length == states.Length &&
                mapRoot.GetComponentsInChildren<SortingGroup>(true).Length == groups.Count,
                "Native presentations must preserve all source owners and registered foot groups.");
            Require(All<StudyAIndependentNavigation>(scene).Length == 1, "New map must have exactly one navigation root.");
            var cameras = All<Camera>(scene);
            var mainCameras = cameras.Where(c => c.name == "Main Camera").ToArray();
            Require(mainCameras.Length == 1 && mainCameras[0].isActiveAndEnabled &&
                cameras.Where(c => c.name != "Main Camera").All(IsDormantNativePreviewCamera),
                "Preserved active Main Camera must be unique; only the unchanged inactive source preview camera is allowed.");
            var cameraObject = mainCameras[0]; var bounds = report.PresentationFrameWorldBounds;
            cameraObject.orthographic = true;
            cameraObject.orthographicSize = Mathf.Max(bounds.height / 2, bounds.width / (2 * Mathf.Max(.1f, cameraObject.aspect)));
            cameraObject.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.center.y, -10), Quaternion.identity);
            cameraObject.transparencySortMode = TransparencySortMode.CustomAxis; cameraObject.transparencySortAxis = Vector3.up;
            Require(!nav.IsBlockedPoint(report.NativeSpawnWorld), "Authored native spawn is blocked by actual selected owner navigation.");
            Require(!blocked.Concat(states.SelectMany(o => o.CurrentNavigationRectangles())).Any(r => PositiveOverlap(r, report.NativeSpawnBodyWorld)),
                "Authored Hero body with .01 margin intersects actual selected owner or static navigation.");
            report.SpawnPointAndBodyValidated = true;
            report.SourceOwnerCount = states.Length; report.MapFootUnitCount = units.Length; report.WithHeroFootUnitCount = units.Length + 1;
            return units;
        }

        private static void BuildEntry(Scene scene, StudyANativeLocationSceneReport report, StudyAIndependentStableDepthSort.Unit[] units)
        {
            var diagnostics = All<StudyABlockMovementDiagnostic>(scene);
            var actors = All<StudyAIndependentActorRenderSetup>(scene);
            var depths = All<StudyAIndependentStableDepthSort>(scene);
            Require(diagnostics.Length == 1 && actors.Length == 1 && depths.Length == 1, "Copied entry needs the existing diagnostic, actor setup and depth manager exactly once.");
            var diagnostic = diagnostics[0]; Require(actors[0].Diagnostic == diagnostic && depths[0].Diagnostic == diagnostic, "Entry component diagnostic references must stay aligned.");
            Require(diagnostic.HeroSprite != null && diagnostic.HeroMaterial != null && diagnostic.InputActions != null && depths[0].EnableForExperiment,
                "Preserve source Hero, human input asset and enabled foot-depth manager.");
            diagnostic.MapScenePath = report.MapScenePath; actors[0].ExpectedMapScenePath = report.MapScenePath;
            depths[0].ExpectedMapScenePath = report.MapScenePath; depths[0].Units = units;
            diagnostic.Route = new[] { report.NativeSpawnWorld, report.NativeSpawnWorld };
            diagnostic.UseHumanInput = true; diagnostic.AutoTraverse = false; diagnostic.FollowActorCamera = true;
            diagnostic.CameraMapBounds = report.PresentationFrameWorldBounds;
        }

        private static GameObject GuardNativePrefab(StudyANativeLocationDefinition definition)
        {
            GuardFile(definition.PrefabPath, definition.PrefabSHA256); GuardFile(definition.GroundPath, definition.GroundSHA256);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);
            Require(prefab != null && PrefabUtility.IsPartOfPrefabAsset(prefab) && !EditorUtility.IsDirty(prefab), "Native source prefab is missing, dirty or not an asset.");
            Require(Quaternion.Angle(prefab.transform.rotation, Quaternion.identity) < .001f && Vector3.Distance(prefab.transform.lossyScale, Vector3.one) < .0001f,
                "Native root must remain unrotated at original scale1.");
            Require(prefab.GetComponentsInChildren<StudyAIndependentNavigation>(true).Length == 0,
                "Catalog must reference independent location prefabs without combined map navigation.");
            var previewCameras = prefab.GetComponentsInChildren<Camera>(true);
            Require(previewCameras.Length == 0 || (definition.Id == "A" && previewCameras.Length == 1 &&
                previewCameras[0].transform.parent == prefab.transform && IsDormantNativePreviewCamera(previewCameras[0])),
                "Native prefabs may retain only A's pinned inactive preview camera; active or unknown cameras are rejected.");
            var owners = prefab.GetComponentsInChildren<StudyAIndependentObjectState>(true);
            Require(definition.SourceOwnerIds != null && definition.FootUnits != null &&
                definition.SourceOwnerIds.All(id => !string.IsNullOrEmpty(id)) &&
                definition.SourceOwnerIds.Distinct(StringComparer.Ordinal).Count() == definition.SourceOwnerIds.Length &&
                owners.All(o => o != null && !string.IsNullOrEmpty(o.ObjectId) && o.ValidateContract()) &&
                owners.Select(o => o.ObjectId).Distinct(StringComparer.Ordinal).Count() == owners.Length &&
                owners.Length == definition.SourceOwnerIds.Length && new HashSet<string>(definition.SourceOwnerIds, StringComparer.Ordinal).SetEquals(owners.Select(o => o.ObjectId)),
                "Source prefab owner identities differ from authored catalog.");
            var byId = owners.ToDictionary(o => o.ObjectId, StringComparer.Ordinal);
            var groups = new HashSet<SortingGroup>();
            foreach (var unit in definition.FootUnits)
            {
                Require(unit != null && !string.IsNullOrEmpty(unit.OwnerId) && byId.ContainsKey(unit.OwnerId) &&
                    !string.IsNullOrEmpty(unit.PartPath), "Source foot unit owner or part path is missing.");
                var part = byId[unit.OwnerId].transform.Find(unit.PartPath);
                var group = part == null ? null : part.GetComponent<SortingGroup>();
                Require(group != null && groups.Add(group), "Source foot path has no unique SortingGroup.");
            }
            Require(groups.Count == prefab.GetComponentsInChildren<SortingGroup>(true).Length,
                "Every source SortingGroup must be registered once before creating output.");
            var groundRenderer = NativeGroundRenderer(prefab);
            foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var legacyGroundReference = renderer == groundRenderer && (definition.Id == "C" || definition.Id == "D") &&
                    renderer.name == "NativeGround_" + definition.Id && renderer.transform.parent == prefab.transform &&
                    Vector3.Distance(renderer.transform.localPosition, new Vector3(definition.ExtentPx.x / 128f, definition.ExtentPx.y / 128f, 0)) < .00001f;
                Require(renderer.sprite != null || !renderer.enabled || legacyGroundReference,
                    "Enabled native source sprite is missing: " + definition.Id + "/" + renderer.name);
                if (renderer.sprite == null) continue;
                Require(Mathf.Abs(renderer.sprite.pixelsPerUnit - PPU) < .0001f && Vector3.Distance(renderer.transform.lossyScale, Vector3.one) < .0001f,
                    "Every native visible sprite keeps64PPU and original scale1.");
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(definition.GroundPath);
            var importer = AssetImporter.GetAtPath(definition.GroundPath) as TextureImporter;
            Require(sprite != null && importer != null && Mathf.Abs(sprite.pixelsPerUnit - PPU) < .0001f &&
                sprite.rect.size == (Vector2)definition.ExtentPx && sprite.pivot == sprite.rect.size / 2 && importer.filterMode == FilterMode.Point &&
                !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed && importer.spriteImportMode == SpriteImportMode.Single,
                "Ground must retain exact native dimensions, center pivot, PPU64, Point and uncompressed single-sprite import.");
            return prefab;
        }

        private static Rect[] ReadWater(StudyANativeLocationSceneConfig config, StudyANativeLocationAssembly assembly)
        {
            var a = assembly.Blocks.FirstOrDefault(b => b.LocationId == "A");
            if (a == null) return Array.Empty<Rect>();
            Require(config.WaterTemplatePrefixCount == 283, "Authored A water prefix must be283 rectangles.");
            GuardFile(config.WaterTemplatePrefabPath, config.WaterTemplatePrefabSHA256);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(config.WaterTemplatePrefabPath);
            Require(source != null && !EditorUtility.IsDirty(source), "Water template prefab is missing or dirty.");
            var navs = source.GetComponentsInChildren<StudyAIndependentNavigation>(true);
            Require(navs.Length == 1 && navs[0].PixelsPerUnit == PPU && navs[0].StaticBlockedRectanglesWorld != null &&
                navs[0].StaticBlockedRectanglesWorld.Length >= 283, "Water template needs the unique preserved navigation prefix.");
            Require(navs[0].StaticBlockedRectanglesWorld.Take(283).All(r => Finite(r.x) && Finite(r.y) && Finite(r.width) && Finite(r.height) && r.width > 0 && r.height > 0),
                "Preserved A water rectangles must remain finite positive geometry.");
            var offset = (Vector2)a.OriginPx / PPU;
            return navs[0].StaticBlockedRectanglesWorld.Take(283).Select(r => new Rect(r.position + offset, r.size)).ToArray();
        }

        private static SpriteRenderer NativeGroundRenderer(GameObject root)
        {
            var grounds = root.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sortingLayerName == "MapGround" &&
                r.sortingOrder == 0 && r.GetComponentInParent<StudyAIndependentObjectState>() == null &&
                r.GetComponentInParent<SortingGroup>() == null).ToArray();
            Require(grounds.Length == 1, "Native location needs exactly one unowned MapGround/order0 renderer.");
            return grounds[0];
        }

        private static void BindNativeSourceGround(GameObject instance, Sprite sourceGround)
        {
            Require(sourceGround != null && Mathf.Abs(sourceGround.pixelsPerUnit - PPU) < .0001f,
                "Bind the existing native ground Sprite returned by Unity's importer.");
            var renderer = NativeGroundRenderer(instance);
            if (renderer.sprite == sourceGround) return;
            renderer.sprite = sourceGround;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(instance.scene);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            var modifications = PrefabUtility.GetPropertyModifications(instance);
            Require(source != null && modifications != null && modifications.Any(m => m.target == source &&
                m.propertyPath == "m_Sprite" && m.objectReference == sourceGround), "The native source-ground reference was not recorded for scene serialization.");
        }

        private static bool IsDormantNativePreviewCamera(Camera camera)
        {
            if (camera == null || camera.name != "WestBaselineCamera_DISABLED" || camera.gameObject.activeSelf ||
                camera.transform.childCount != 0 || camera.GetComponentInParent<StudyAIndependentObjectState>() != null) return false;
            var components = camera.GetComponents<Component>();
            return components.Length == 3 && components.All(c => c is Transform || c is Camera || c is StudyACameraYSort) &&
                camera.GetComponent<StudyACameraYSort>() != null;
        }

        private static void GuardEditor()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating, "Do not generate while playing, compiling or importing.");
            Require(PrefabStageUtility.GetCurrentPrefabStage() == null, "Close Prefab Mode before generating; no source stage is edited.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                Require(!scene.isDirty && !string.IsNullOrEmpty(scene.path), "All loaded user scenes must be saved and clean. The builder never saves them.");
            }
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        private static Rect World(RectInt r) => new Rect(r.x / (float)PPU, r.y / (float)PPU, r.width / (float)PPU, r.height / (float)PPU);
        // CharacterActor2D's native capsule AABB is .48 x .55, offset(0,.22).
        // The .01 margin protects spawn clearance; it does not alter the runtime collider.
        private static Rect HeroBody(Vector2 foot) => new Rect(foot + new Vector2(-.25f, -.065f), new Vector2(.50f, .57f));
        private static bool PositiveOverlap(Rect a, Rect b) => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string AssetPath(string value)
        {
            Require(!string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) && !value.Contains("\\") && !value.Contains(":"), "Use an explicit project-relative Assets path.");
            Require(value.Split('/').All(p => p.Length > 0 && p != "." && p != ".." && !p.EndsWith(".", StringComparison.Ordinal) && !p.EndsWith(" ", StringComparison.Ordinal)), "Asset path has unsafe segments.");
            return value;
        }
        private static string ProjectPath(string value) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetPath(value)));
        private static void GuardFile(string path, string expected)
        {
            AssetPath(path); Require(expected != null && expected.Length == 64 && expected.All(Uri.IsHexDigit), "Explicit source SHA256 is required for " + path);
            Require(File.Exists(ProjectPath(path)) && string.Equals(FileHash(path), expected, StringComparison.OrdinalIgnoreCase), "Source bytes changed or are missing: " + path);
        }
        private static string FileHash(string path) { using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(File.ReadAllBytes(ProjectPath(path)))); }
        private static string TextHash(string value) { using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        private static void WriteNew(string path, string text)
        {
            Require(!File.Exists(ProjectPath(path)), "Never overwrite a generated evidence file.");
            using (var stream = new FileStream(ProjectPath(path), FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var split = path.LastIndexOf('/'); Require(split > 0, "Cannot create an unbounded output folder.");
            EnsureFolder(path.Substring(0, split));
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1))), "Could not create the new output folder.");
        }
    }
}
