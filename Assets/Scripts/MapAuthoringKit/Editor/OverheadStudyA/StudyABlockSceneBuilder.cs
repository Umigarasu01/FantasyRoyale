using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyABlockSceneReport
    {
        public bool Generated;
        public bool StructuralOnly = true;
        public bool FullTargetReady;
        public bool ActualActorWalkVerified;
        public bool TerrainAssetsReady, StaticHeroPrototypeReady;
        public string CaseName, OutputRoot, ScenePath, CapturePath, MetadataPath, SettingsPath, LayoutHash, StarterFingerprint, VariationSelectionHash;
        public string TerrainFingerprint, TerrainSelectionHash;
        public string GameplayCapturePath, OverviewCapturePath;
        public int WorldWidth, WorldHeight, OverviewCaptureWidth, OverviewCaptureHeight;
        public bool ThreeBlockLandscapeEnabled;
        public int CustomTerrainCellsPreserved;
        public bool FineSoilContourEnabled;
        public StudyABridgeRiverAppearanceReport BridgeRiverAppearance;
        public string FineSoilMaskSha256, FineSoilTexturePath, FineSoilVariantSelectionHash, FineSoilRecipe;
        public int FineSoilPixelsPerUnit, FineSoilOpaquePixelCount;
        public int FineDepthTieProxyCount;
        public string FineDepthTieSelectionHash;
        public string[] FineDepthTieSelections;
        public StudyASoilContourPolygon[] FineSoilPolygons;
        public int CompositionVersion;
        public int ArtFinishVersion;
        public string[] ArtFinishLayoutChanges;
        public int BridgeStructureVersion;
        public StudyABridge01StructureReport BridgeStructure;
        public StudyAArtFinishReport ArtFinish;
        public StudyAForestArtFinishReport ForestArtFinish;
        public int FoliageExperimentVersion;
        public StudyAFoliageExperimentReport FoliageExperiment;
        public StudyAGroundIntegrationExperimentReport GroundIntegrationExperiment;
        public string TerrainRevision, BackdropSelectionHash;
        public bool ForestBackdropEnabled, ForestDetailsEnabled;
        public string ForestDetailFingerprint, ForestDetailSelectionHash;
        public string[] ForestDetailSelections, ForestDetailRoles, ForestDetailPaths;
        public Vector2[] ForestDetailAnchors;
        public Rect[] ForestDetailFootprints, ForestDetailColliderBounds;
        public int Seed;
        public Vector2 Spawn;
        public Vector2[] WalkCheckpoints;
        public Vector2Int[] WaterCells, BridgeCells, BridgeFloorCells, SolidCells, RoadCells, SoilCells, WalkCells, BlockOrigins;
        public string[] BlockIds, PrefabPaths, VariationSelections, TerrainSelections, MissingFullTargetRoles;
        public string[] BackdropSelections;
        public Vector2[] BackdropAnchors;
        public Rect[] BackdropFootprints, BackdropColliderBounds;
        public Rect[] BridgeFloorBounds;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    /// <summary>一画面・最大2区画だけの構造検証。地形診断とStarter6小物を全体品質達成と混同しない。</summary>
    public static class StudyABlockSceneBuilder
    {
        public const string ArtRoot = "Assets/Art/Map/OverheadStudyA/BlockStudies";
        public const string SceneRoot = "Assets/Scenes/MapAuthoring/OverheadStudyA/Blocks";
        public const int Width = 24, Height = 16, CaptureWidth = 1536, CaptureHeight = 1024;
        private static bool IsThreeBlockCase(StudyABlockSceneSettings settings) => settings != null && settings.Case == StudyABlockSceneCase.PlazaPathAndBridgeBank;
        private static int FrameWidth(StudyABlockSceneSettings settings) => IsThreeBlockCase(settings) ? 32 : Width;
        private static readonly string[] MaskNames = { "Walk", "Soil", "Road", "Water", "Bridge", "BridgeFloor", "Solid", "Occlusion" };
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.down, Vector2Int.left };

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Generate Selected Block Study")]
        public static void BuildSelected()
        {
            var report = BuildRepresentative(Selection.activeObject as StudyABlockSceneSettings);
            if (report.Generated) Debug.Log(JsonUtility.ToJson(report, true));
            else Debug.LogWarning(JsonUtility.ToJson(report, true));
        }

        /// <summary>Prefabの実Tilemap/Port/Routeを読む。未指定区画は固定preset。ファイルとSceneを変更しない。</summary>
        public static StudyABlockAssembly CreateAssembly(StudyABlockSceneSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            StudyABlockDefinition Read(StudyABlockKind kind)
            {
                var prefab = Template(settings, kind);
                return prefab == null ? Preset(kind, settings.Seed, WantsFineContour(settings)) : ReadPrefabDefinition(prefab);
            }
            if (IsThreeBlockCase(settings) && (settings.PlazaPrefab == null || settings.ForestPathPrefab == null || settings.BridgeBankPrefab == null))
                throw new ArgumentException("The three-block study requires all three existing reviewed Native Prefabs; no replacement templates are generated.");
            StudyABlockAssembly assembly;
            switch (settings.Case)
            {
                case StudyABlockSceneCase.Plaza: assembly = StudyABlockGenerator.Single(Read(StudyABlockKind.Plaza), Vector2Int.zero, settings.Seed); break;
                case StudyABlockSceneCase.ForestPath: assembly = StudyABlockGenerator.Single(Read(StudyABlockKind.ForestPath), Vector2Int.zero, settings.Seed); break;
                case StudyABlockSceneCase.BridgeBank: assembly = StudyABlockGenerator.Single(Read(StudyABlockKind.BridgeBank), Vector2Int.zero, settings.Seed); break;
                case StudyABlockSceneCase.PlazaAndPath: assembly = StudyABlockGenerator.Pair(Read(StudyABlockKind.Plaza), 1, Read(StudyABlockKind.ForestPath), 0, settings.Seed); break;
                case StudyABlockSceneCase.PathAndBridgeBank: assembly = StudyABlockGenerator.Pair(Read(StudyABlockKind.ForestPath), 1, Read(StudyABlockKind.BridgeBank), 0, settings.Seed); break;
                case StudyABlockSceneCase.PlazaPathAndBridgeBank:
                    assembly = StudyABlockGenerator.AddConnected(StudyABlockGenerator.Pair(Read(StudyABlockKind.Plaza), 1, Read(StudyABlockKind.ForestPath), 0, settings.Seed), 1, 1, Read(StudyABlockKind.BridgeBank), 0);
                    if (assembly.Blocks[0].Definition.Kind != StudyABlockKind.Plaza || assembly.Blocks[1].Definition.Kind != StudyABlockKind.ForestPath || assembly.Blocks[2].Definition.Kind != StudyABlockKind.BridgeBank
                        || assembly.Blocks[0].Definition.Extent != new Vector2Int(12,12) || assembly.Blocks[1].Definition.Extent != new Vector2Int(8,12) || assembly.Blocks[2].Definition.Extent != new Vector2Int(12,12))
                        throw new ArgumentException("Three-block input must preserve the reviewed12x12 Plaza,8x12 ForestPath and12x12 BridgeBank.");
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(settings.Case));
            }
            if (assembly.Blocks.Count > (IsThreeBlockCase(settings) ? 3 : 2) || assembly.Bounds.width > FrameWidth(settings) || assembly.Bounds.height > Height)
                throw new ArgumentException("最初の親レビューまでは最大2区画・24x16一画面内です。");
            var offset = new Vector2Int((FrameWidth(settings) - assembly.Bounds.width) / 2 - assembly.Bounds.xMin,
                (Height - assembly.Bounds.height) / 2 - assembly.Bounds.yMin);
            return assembly.Translate(offset);
        }

        public static StudyABridgeRiverFinePlan CreateBridgeRiverFinePlan(StudyABlockSceneSettings settings)
        {
            if (settings == null || !settings.EnableFineBridgeRiverContours || !settings.EnableBridgeRiverArt)
                throw new ArgumentException("Explicit FineBridgeRiver and BridgeRiverArt settings are required.");
            var assembly = CreateAssembly(settings); RequireValid(assembly);
            var polygons = StudyABridgeRiverFineGeometry.ReadPolygons(assembly, assembly.Blocks.Select(b => Template(settings, b.Definition.Kind)));
            return StudyABridgeRiverFineGeometry.MakePlan(assembly, polygons, FrameWidth(settings), Height, settings.BridgeStructureVersion);
        }

        public static StudyABlockSceneReport BuildRepresentative(StudyABlockSceneSettings settings = null)
        {
            // Missing/unreviewed入力ではPrefab/Settings/Scene/QA/folder作成にも進まない。
            var readiness = StudyAStarterAssets.CheckReadiness();
            StudyATerrainSettings terrainSettings;
            try { terrainSettings = ResolveTerrainSettings(settings); }
            catch (Exception error) { return new StudyABlockSceneReport { Errors = new List<string> { error.ToString() } }; }
            var terrainReadiness = StudyATerrainAssets.CheckReadiness(terrainSettings);
            var freshNativeInput = settings == null || (settings.PlazaPrefab == null && settings.ForestPathPrefab == null && settings.BridgeBankPrefab == null);
            // Old saved inputs with Details=false do not load new art or change their composition.
            var detailReadiness = freshNativeInput || (settings != null && settings.EnableForestDetails) ? StudyAForestDetailAssets.CheckReadiness() : null;
            var forestDetails = detailReadiness != null && detailReadiness.DetailsReady;
            var fineSoil = WantsFineContour(settings);
            var bridgeRiverArt = settings != null && settings.EnableBridgeRiverArt;
            var fineBridge = settings != null && settings.EnableFineBridgeRiverContours;
            var bridgeLandscapeVersion = !fineBridge ? 0 : settings.BridgeRiverLandscapeVersion > 0 ? settings.BridgeRiverLandscapeVersion : settings.BridgeBankPrefab == null ? 2 : 1;
            var bridgeReadiness = bridgeRiverArt ? StudyABridgeRiverAssets.CheckReadiness() : null;
            var report = new StudyABlockSceneReport
            {
                StarterFingerprint = readiness.Fingerprint, TerrainFingerprint = terrainReadiness.Fingerprint,
                TerrainAssetsReady = terrainReadiness.ApprovedTerrainReady, StaticHeroPrototypeReady = terrainReadiness.HeroReady,
                MissingFullTargetRoles = terrainReadiness.MissingFullTargetRoles
            };
            if (!readiness.StarterPropsReady)
            {
                report.Errors.Add("Starter6小物の原本/派生レビューが未成立です。出力0。\n" + readiness);
                return report;
            }
            if (!terrainReadiness.ApprovedTerrainReady || !terrainReadiness.HeroReady)
            {
                report.Errors.Add("承認済みTerrain/静止Heroの原本・派生レビューが未成立です。出力0。\n" + terrainReadiness);
                return report;
            }
            if (settings != null && settings.EnableForestDetails && !forestDetails)
            {
                report.Errors.Add("Forest Detailsの原本/派生レビューが未成立です。出力0。\n" + detailReadiness); return report;
            }
            if (bridgeRiverArt && (bridgeReadiness == null || !bridgeReadiness.BridgeRiverReady))
            {
                report.Errors.Add("BridgeRiver5素材の原本/派生レビューが未成立です。出力0。\n" + bridgeReadiness); return report;
            }
            if (fineBridge && (!bridgeRiverArt || fineSoil))
            { report.Errors.Add("FineBridgeRiver requires BridgeRiverArt and a separate bridge-road contour; global Fine soil stays disabled."); return report; }
            if (settings != null && (settings.ArtFinishVersion < 0 || settings.ArtFinishVersion > 1 || (settings.ArtFinishVersion == 1 && !fineBridge)))
            { report.Errors.Add("Art finish1 requires the existing FineBridgeRiver study; saved version0 remains unchanged."); return report; }
            if (settings != null && (settings.BridgeStructureVersion < 0 || settings.BridgeStructureVersion > 1 ||
                (settings.BridgeStructureVersion == 1 && (!fineBridge || settings.ArtFinishVersion != 1 || settings.Case != StudyABlockSceneCase.PlazaPathAndBridgeBank))))
            { report.Errors.Add("BridgeStructure1 is a separate opt-in using the existing three-block FineBridgeRiver/ArtFinish1 snapshot."); return report; }
            if (settings != null && (settings.FoliageExperimentVersion < 0 || settings.FoliageExperimentVersion > 3 ||
                (settings.FoliageExperimentVersion != 0 && (settings.BridgeStructureVersion != 1 || settings.ArtFinishVersion != 1 || settings.Case != StudyABlockSceneCase.PlazaPathAndBridgeBank))))
            { report.Errors.Add("Foliage experiments require the separate BridgeStructure1/ArtFinish1 three-block snapshot, preserving the same tree placements, finish and camera."); return report; }
            if (settings != null && (settings.FoliageExperimentVersion == 1 || settings.FoliageExperimentVersion == 2))
                StudyAFoliageExperiment.RequireApprovedInputs(settings.FoliageExperimentFingerprint);
            var ownSettings = settings == null;
            if (ownSettings) settings = ScriptableObject.CreateInstance<StudyABlockSceneSettings>();
            var freshComposition = settings.CompositionVersion <= 0 && settings.PlazaPrefab == null && settings.ForestPathPrefab == null && settings.BridgeBankPrefab == null;
            var compositionVersion = freshComposition ? 2 : Math.Max(1, settings.CompositionVersion);
            var forestBackdrop = compositionVersion >= 2 && (freshComposition || settings.EnableForestBackdrop);
            Scene generatedScene = default;
            var previousActive = SceneManager.GetActiveScene();
            StudyABlockSceneSettings savedSettings = null;
            try
            {
                EnsureCleanEditor();
                var threeBlocks = IsThreeBlockCase(settings); var frameWidth = FrameWidth(settings);
                if (threeBlocks && (!fineBridge || !bridgeRiverArt || fineSoil || bridgeLandscapeVersion != 2))
                    throw new ArgumentException("The three-block case requires FineBridgeRiver=true,BridgeRiverArt=true,LandscapeVersion2 and globalFineSoil=false.");
                report.WorldWidth = frameWidth; report.WorldHeight = Height; report.ThreeBlockLandscapeEnabled = threeBlocks;
                if (float.IsNaN(settings.CameraOrthographicSize) || float.IsInfinity(settings.CameraOrthographicSize) || settings.CameraOrthographicSize <= 0f
                    || float.IsNaN(settings.VariationProbability) || float.IsInfinity(settings.VariationProbability)
                    || settings.VariationProbability < 0f || settings.VariationProbability > 1f || settings.MaxVariablePropsPerBlock < 0)
                    throw new ArgumentException("Camera/variation設定が不正です。");
                if (!Mathf.Approximately(settings.CameraOrthographicSize, 8f)) throw new ArgumentException("24x16のTerrain品質比較はCamera ortho8を使用します。製品Cameraの制約ではありません。");
                if (settings.ArtFinishVersion == 1) StudyAArtFinishRenderer.RequireApprovedInputs();
                if (settings.BridgeStructureVersion == 1) StudyABridge01Structure.RequireApprovedInputs(settings.BridgeStructureFingerprint);
                var initial = CreateAssembly(settings); RequireValid(initial);
                RequireTerrainLayout(initial.SoilCells);
                var props = StudyAStarterAssets.Load();
                var terrainSprites = StudyATerrainAssets.LoadSpriteSet(terrainSettings);
                var detailSprites = forestDetails ? StudyAForestDetailAssets.LoadSpriteSet() : null;
                StudyABridgeRiverSpriteSet bridgeSprites = null; StudyABridgeRiverAppearancePlan bridgePlan = null;
                StudyABridgeRiverFinePlan fineBridgePlan = null; StudyASoilMask bridgeRoadMask = null; StudyASoilContourPolygon[] bridgeRoadPolygons = null;
                if (bridgeRiverArt)
                {
                    if (fineBridge && (bridgeLandscapeVersion<1 || bridgeLandscapeVersion>2)) throw new ArgumentException("Unsupported fine bridge landscape snapshot version.");
                    if (terrainSettings.InputRoot != StudyATerrainAssets.R2InputRoot) throw new ArgumentException("BridgeRiver exterior soil uses approved GroundR2; choose explicit R2 in the new opt-in settings.");
                    if (settings.Case != StudyABlockSceneCase.BridgeBank && settings.Case != StudyABlockSceneCase.PathAndBridgeBank && !threeBlocks)
                        throw new ArgumentException("BridgeRiver opt-in is limited to BridgeBank or PathAndBridgeBank.");
                    bridgeSprites = StudyABridgeRiverAssets.LoadSpriteSet();
                    if (!string.IsNullOrEmpty(settings.BridgeRiverFingerprint) && settings.BridgeRiverFingerprint != bridgeSprites.Fingerprint)
                        throw new InvalidDataException("Saved BridgeRiver fingerprint changed; original snapshot is not silently migrated.");
                    if (fineBridge)
                    {
                        fineBridgePlan = CreateBridgeRiverFinePlan(settings);
                        bridgeRoadPolygons = ReadBridgeRoadPolygons(initial, settings);
                        bridgeRoadMask = StudyASoilContourRenderer.MakeMask(bridgeRoadPolygons, Vector2Int.zero, frameWidth, Height);
                        StudyASoilContourRenderer.RequireRouteAndPorts(bridgeRoadMask, initial, bridgeRoadPolygons);
                    }
                    StudyABridgeRiverRenderer.RequireCompatibleInput(initial, initial.Blocks.Select(b => Template(settings, b.Definition.Kind)), bridgeSprites, fineBridge, threeBlocks);
                    bridgePlan = fineBridge ? StudyABridgeRiverRenderer.MakeFinePlan(fineBridgePlan, settings.Seed, bridgeSprites, bridgeLandscapeVersion)
                        : StudyABridgeRiverRenderer.MakePlan(initial, settings.Seed, bridgeSprites);
                    StudyABridgeRiverRenderer.RequireCompatibleBanks(bridgePlan, initial.Blocks.Select(b => Template(settings, b.Definition.Kind)));
                    if (fineBridge)
                    {
                        StudyABridgeRiverRenderer.RequireFineNativeScenery(fineBridgePlan, initial.Blocks.Select(b => Template(settings, b.Definition.Kind)), props);
                        bridgeRoadMask=StudyABridgeRiverRenderer.ClipRoadMask(bridgeRoadMask,fineBridgePlan);
                        StudyASoilContourRenderer.RequireRouteAndPorts(bridgeRoadMask,initial,bridgeRoadPolygons);
                        if (threeBlocks) RequireFinePoiSupport(initial,settings,props,bridgeRoadMask);
                    }
                }
                StudyASoilContourPolygon[] finePolygons = null; StudyASoilMask fineMask = null;
                if (fineSoil)
                {
                    if (terrainSettings.InputRoot != StudyATerrainAssets.R2InputRoot) throw new ArgumentException("Fine soil uses approved GroundR2 only. Historical R1 remains unchanged when Fine=false.");
                    finePolygons = ReadFinePolygons(initial, settings);
                    fineMask = StudyASoilContourRenderer.MakeMask(finePolygons, Vector2Int.zero, Width, Height);
                    StudyASoilContourRenderer.RequireRouteAndPorts(fineMask, initial, finePolygons);
                    RequireFinePoiSupport(initial, settings, props, fineMask);
                }
                var hero = terrainSprites.Hero;
                if (hero == null) throw new InvalidOperationException("承認された静止Hero Spriteがありません。旧Heroへ代替しません。");
                var runId = "terrain-" + settings.Case.ToString().ToLowerInvariant() + "-seed-" + settings.Seed + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
                var runRoot = ArtRoot + "/" + runId;
                report.OutputRoot = runRoot;
                if (AssetDatabase.IsValidFolder(runRoot)) throw new IOException("同名出力が既にあります。上書きしません。");
                EnsureFolder(runRoot + "/Prefabs"); EnsureFolder(runRoot + "/Tiles"); EnsureFolder(runRoot + "/QA"); if (fineSoil) EnsureFolder(runRoot + "/FineSoil"); if (bridgeRiverArt) EnsureFolder(runRoot + "/BridgeRiver"); EnsureFolder(SceneRoot);
                generatedScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(generatedScene);
                var material = MaterialForRun(runRoot);
                var tiles = TilesForRun(runRoot, terrainSprites, bridgeSprites);
                savedSettings = Object.Instantiate(settings); savedSettings.name = "StudyABlockSceneSettings";
                savedSettings.TerrainRevision = terrainSettings.InputRoot == StudyATerrainAssets.R1InputRoot ? StudyABlockTerrainRevision.R1 : StudyABlockTerrainRevision.GroundR2;
                savedSettings.CompositionVersion = compositionVersion; savedSettings.EnableForestBackdrop = forestBackdrop;
                savedSettings.EnableForestDetails = forestDetails; report.ForestDetailsEnabled = forestDetails;
                savedSettings.EnableFineSoilContour = fineSoil; report.FineSoilContourEnabled = fineSoil;
                savedSettings.EnableBridgeRiverArt = bridgeRiverArt;
                savedSettings.EnableFineBridgeRiverContours = fineBridge;
                if(fineBridge) savedSettings.BridgeRiverLandscapeVersion = bridgeLandscapeVersion;
                if (bridgeRiverArt) savedSettings.BridgeRiverFingerprint = bridgeSprites.Fingerprint;
                if (savedSettings.BridgeStructureVersion == 1) savedSettings.BridgeStructureFingerprint = StudyABridge01Structure.Fingerprint();
                if (savedSettings.FoliageExperimentVersion == 1 || savedSettings.FoliageExperimentVersion == 2) savedSettings.FoliageExperimentFingerprint = StudyAFoliageExperiment.Fingerprint();
                if (forestDetails)
                {
                    report.ForestDetailFingerprint = detailSprites.Fingerprint;
                    report.MissingFullTargetRoles = report.MissingFullTargetRoles.Where(r => r != "flower").Concat(new[] { "hero-animation" }).Distinct().ToArray();
                }
                if (bridgeRiverArt) report.MissingFullTargetRoles = report.MissingFullTargetRoles.Where(r => r != "water" && r != "bridge").Concat(new[] { "hero-animation" }).Distinct().ToArray();
                report.TerrainRevision = savedSettings.TerrainRevision.ToString(); report.CompositionVersion = compositionVersion; report.ForestBackdropEnabled = forestBackdrop;
                foreach (StudyABlockKind kind in Enum.GetValues(typeof(StudyABlockKind)))
                {
                    if (Template(savedSettings, kind) != null) continue;
                    var definition = Preset(kind, settings.Seed, fineSoil);
                    var prefab = BuildPrefab(definition, props, tiles, terrainSprites, settings.Seed, material, runRoot + "/Prefabs/" + kind + ".prefab", fineSoil, bridgeSprites, fineBridge, bridgeLandscapeVersion);
                    AssignTemplate(savedSettings, kind, prefab);
                    var reloaded = ReadPrefabDefinition(prefab);
                    var before = StudyABlockGenerator.Single(definition, Vector2Int.zero, settings.Seed).StableHash();
                    var after = StudyABlockGenerator.Single(reloaded, Vector2Int.zero, settings.Seed).StableHash();
                    if (before != after) throw new InvalidDataException("Prefab保存/readbackで契約が変わりました: " + kind);
                }
                report.SettingsPath = runRoot + "/StudyABlockSceneSettings.asset";
                AssetDatabase.CreateAsset(savedSettings, report.SettingsPath);
                var assembly = CreateAssembly(savedSettings); RequireValid(assembly);
                if (bridgeRiverArt && assembly.StableHash() != initial.StableHash()) throw new InvalidDataException("BridgeRiver Native readback changed the logical assembly.");
                RequireTerrainLayout(assembly.SoilCells);
                report.Seed = savedSettings.Seed; report.CaseName = savedSettings.Case.ToString(); report.LayoutHash = assembly.StableHash(); report.Spawn = assembly.Spawn;
                report.ArtFinishVersion = savedSettings.ArtFinishVersion;
                report.BridgeStructureVersion = savedSettings.BridgeStructureVersion;
                report.FoliageExperimentVersion = savedSettings.FoliageExperimentVersion;
                var map = Child(null, "MapRoot");
                var grid = Child(map.transform, "Grid"); grid.AddComponent<Grid>();
                var ground = Tilemap(grid.transform, "GroundTilemap", "MapGround", 0, material);
                var water = Tilemap(grid.transform, "WaterTilemap", "MapGround", 5, material);
                var terrain = Tilemap(grid.transform, "TerrainTilemap", "MapGround", 10, material);
                var collision = Tilemap(grid.transform, "CollisionTilemap", "Default", 0, material, true);
                Tilemap(grid.transform, "ForegroundTilemap", "MapForeground", 0, material);
                var decorations = Child(map.transform, "Decorations");
                var obstacles = Child(map.transform, "ObstacleVisuals");
                var worldProps = Child(map.transform, "Props");
                var sockets = Child(map.transform, "Sockets");
                foreach (var cell in new RectInt(0, 0, frameWidth, Height).allPositionsWithin) ground.SetTile(Cell(cell), tiles["grass"]);
                Paint(water, assembly.WaterCells, tiles["water"]);
                Paint(terrain, assembly.SoilCells.Except(assembly.WaterCells), tiles["soil"]);
                Paint(terrain, assembly.BridgeFloorCells, tiles["deck"]);
                Paint(collision, fineBridge ? fineBridgePlan.RetainedSolidCells : assembly.WaterCells.Except(assembly.BridgeCells).Concat(assembly.SolidCells), tiles["collision"]);
                var selections = new List<string>();
                var copiedMaterials = new Dictionary<UnityEngine.Tilemaps.Tilemap, Material>();
                foreach (var block in assembly.Blocks)
                {
                    var prefab = Template(savedSettings, block.Definition.Kind);
                    MergeVisibleTiles(prefab, block, ground, water, terrain, copiedMaterials);
                    var fixedRoot = prefab.transform.Find("FixedScenery");
                    if (fixedRoot != null)
                    {
                        var clone = Object.Instantiate(fixedRoot.gameObject, obstacles.transform);
                        clone.name = block.Definition.Id + "_FixedScenery"; clone.transform.localPosition = (Vector2)block.Origin;
                    }
                    AddVariations(prefab, block, assembly, savedSettings, props, obstacles.transform, material, selections);
                    if (block.Definition.Kind == StudyABlockKind.BridgeBank && savedSettings.BridgeStructureVersion == 0) BridgePosts(block, bridgeRiverArt ? bridgeSprites.Post : tiles["post"].sprite, obstacles.transform, material);
                }
                if (savedSettings.ArtFinishVersion == 1) report.ArtFinishLayoutChanges = StudyAArtFinishLayout.Apply(assembly, obstacles.transform, props, material, fineBridgePlan, bridgeRoadMask);
                if (forestBackdrop) BuildForestBackdrop(assembly, props, obstacles.transform, material, report, fineBridge, bridgeLandscapeVersion);
                if (fineBridge) StudyABridgeRiverRenderer.RequireFineScenery(fineBridgePlan, obstacles.transform);
                var terrainSelections = new List<string>();
                report.CustomTerrainCellsPreserved = ApplyGlobalTerrain(assembly, savedSettings.Seed, terrainSprites, tiles, ground, terrain, runRoot, terrainSelections, frameWidth);
                report.TerrainSelections = terrainSelections.ToArray(); report.TerrainSelectionHash = Hash(string.Join("\n", terrainSelections));
                if (fineSoil)
                {
                    finePolygons = ReadFinePolygons(assembly, savedSettings);
                    fineMask = StudyASoilContourRenderer.MakeMask(finePolygons, Vector2Int.zero, Width, Height);
                    StudyASoilContourRenderer.RequireRouteAndPorts(fineMask, assembly, finePolygons);
                    ApplyFineSoil(fineMask, finePolygons, terrainSprites, savedSettings.Seed, terrain, decorations.transform, runRoot, report);
                }
                if (bridgeRiverArt)
                {
                    report.BridgeRiverAppearance = fineBridge
                        ? StudyABridgeRiverRenderer.ApplyFine(bridgePlan, fineBridgePlan, bridgeRoadMask, bridgeRoadPolygons, bridgeSprites, terrainSprites, water, terrain, decorations.transform, material, runRoot + "/BridgeRiver", bridgeLandscapeVersion)
                        : StudyABridgeRiverRenderer.Apply(bridgePlan, bridgeSprites, terrainSprites, water, terrain, decorations.transform, material, runRoot + "/BridgeRiver/shoreline.png");
                    if (fineBridge) StudyABridgeRiverFineGeometry.Bind(fineBridgePlan, map.transform);
                    if (savedSettings.BridgeStructureVersion == 1)
                        report.BridgeStructure = StudyABridge01Structure.Apply(fineBridgePlan, decorations.transform, obstacles.transform, material, runRoot + "/BridgeRiver/Bridge01");
                }
                if (forestDetails) BuildForestDetails(assembly, savedSettings.Seed, detailSprites, props, decorations.transform, obstacles.transform, material, report, fineBridge ? bridgeRoadMask : fineMask, fineBridgePlan, bridgeLandscapeVersion, savedSettings.ArtFinishVersion);
                if (fineBridge) StudyABridgeRiverRenderer.RequireFineScenery(fineBridgePlan, obstacles.transform);
                if (savedSettings.ArtFinishVersion == 1)
                {
                    EnsureFolder(runRoot + "/ArtFinish");
                    report.ArtFinish = StudyAArtFinishRenderer.Apply(fineBridgePlan, decorations.transform, obstacles.transform, material, runRoot + "/ArtFinish");
                    report.ForestArtFinish = StudyAForestArtFinishRenderer.Apply(obstacles.transform, decorations.transform, material, runRoot + "/ArtFinish/forest-finish", assembly, fineBridgePlan, props, bridgeRoadMask);
                    report.Warnings.Add(savedSettings.BridgeStructureVersion == 1 ? "Separate BridgeStructure1 candidate changes only bridge appearance/body/water opening and local approach taper. Source Native prefabs, original R4 settings/scene and actor remain preserved; actual movement and visual review remain required." : "Visual finish1 changes reviewed scenery poses outside guaranteed walking land, native sprite tint and decorative overlays. Fine water/deck/collision geometry and authored prefab inputs are retained; target appearance still requires pixel review.");
                }
                if (savedSettings.FoliageExperimentVersion == 3)
                    report.GroundIntegrationExperiment = StudyAGroundIntegrationExperiment.Apply(obstacles.transform, decorations.transform, material,
                        runRoot + "/GroundIntegrationExperiment", assembly, fineBridgePlan, props, report.ForestArtFinish, ground, bridgeRoadMask);
                SpriteObject(worldProps.transform, "ConceptScaleHero_StaticDiagnostic", hero, report.Spawn, material);
                Socket(sockets.transform, "study-a-player-start", MapSocketKind.PlayerStart, report.Spawn);
                if (fineSoil || bridgeRiverArt) ResolveFineDepthTies(map, report);
                if (savedSettings.FoliageExperimentVersion == 1 || savedSettings.FoliageExperimentVersion == 2)
                    report.FoliageExperiment = StudyAFoliageExperiment.Apply(obstacles.transform, props, savedSettings.FoliageExperimentVersion,
                        savedSettings.FoliageExperimentFingerprint, runRoot + "/FoliageExperiment");
                var cameraGo = Child(map.transform, "Main Camera"); cameraGo.tag = "MainCamera";
                var camera = cameraGo.AddComponent<Camera>();
                if(fineBridge) cameraGo.AddComponent<StudyACameraYSort>();
                camera.orthographic = true; camera.orthographicSize = savedSettings.CameraOrthographicSize;
                camera.aspect = (float)CaptureWidth / CaptureHeight; camera.transform.localPosition = new Vector3(frameWidth * .5f, 8f, -10f);
                camera.backgroundColor = new Color32(45, 76, 47, 255); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.transparencySortMode = TransparencySortMode.CustomAxis; camera.transparencySortAxis = Vector3.up;
                Camera overview = null;
                if (threeBlocks)
                {
                    var overviewGo = Child(map.transform,"Overview Camera");
                    overview = overviewGo.AddComponent<Camera>(); overviewGo.AddComponent<StudyACameraYSort>();
                    overview.orthographic = true; overview.orthographicSize = 8f; overview.aspect = 2f;
                    overview.transform.localPosition = new Vector3(16f,8f,-10f); overview.backgroundColor = camera.backgroundColor; overview.clearFlags = CameraClearFlags.SolidColor;
                    overview.enabled = false; // Manual QA render only. MainCamera retains the usual8/1.5 view.
                    report.OverviewCaptureWidth = 2048; report.OverviewCaptureHeight = CaptureHeight;
                }
                if (savedSettings.IncludeDiagnosticLabel) DiagnosticLabel(map.transform, report.CaseName);
                Physics2D.SyncTransforms();
                report.WalkCheckpoints = OrderedRoute(assembly);
                ValidateActualColliders(map, report.WalkCheckpoints, assembly.RoadCells);
                foreach (var tilemap in map.GetComponentsInChildren<Tilemap>()) { tilemap.CompressBounds(); tilemap.RefreshAllTiles(); }
                report.VariationSelections = selections.ToArray(); report.VariationSelectionHash = Hash(string.Join("\n", selections));
                report.WaterCells = Sorted(assembly.WaterCells); report.BridgeCells = Sorted(assembly.BridgeCells); report.BridgeFloorCells = Sorted(assembly.BridgeFloorCells);
                report.SolidCells = Sorted(assembly.SolidCells); report.RoadCells = Sorted(assembly.RoadCells); report.SoilCells = Sorted(assembly.SoilCells); report.WalkCells = Sorted(assembly.WalkCells);
                report.BlockOrigins = assembly.Blocks.Select(b => b.Origin).ToArray(); report.BlockIds = assembly.Blocks.Select(b => b.Definition.Id).ToArray();
                report.PrefabPaths = assembly.Blocks.Select(b => AssetDatabase.GetAssetPath(Template(savedSettings, b.Definition.Kind))).ToArray();
                report.BridgeFloorBounds = savedSettings.BridgeStructureVersion == 1 ? fineBridgePlan.DeckBounds.ToArray() :
                    assembly.Blocks.Where(b => b.Definition.BridgeFloorCells.Count > 0).Select(b => Bounds(b.Definition.BridgeFloorCells.Select(c => c + b.Origin))).ToArray();
                report.ScenePath = SceneRoot + "/" + runId + ".unity";
                report.CapturePath = runRoot + "/QA/" + runId + (threeBlocks ? "-overview-2048x1024.png" : "-1536x1024.png");
                if (threeBlocks)
                {
                    report.OverviewCapturePath = report.CapturePath;
                    report.GameplayCapturePath = runRoot + "/QA/" + runId + "-gameplay-1536x1024.png";
                    report.Warnings.Add("Three existing Native blocks form a32x16 study. The2048x1024 overview is a structural QA view; normal MainCamera remains ortho8/aspect1.5. Manual movement entry handles actor follow separately.");
                }
                report.MetadataPath = runRoot + "/QA/" + runId + "-report.json";
                report.Warnings.Add(bridgeRiverArt ? "Terrain/静止Hero/BridgeRiver品質比較Prototype。Hero animation等は未完成で、A全体の完成ではありません。" : forestDetails ? "Terrain/静止Hero/局所Detailsの品質比較Prototype。water/bridgeとHero animationは不足し、A全体の完成ではありません。"
                    : "Terrain/静止Heroの品質比較Prototype。water/bridge/flowerとHero animationは不足し、A全体の完成ではありません。");
                if (bridgeRiverArt)
                {
                    report.Warnings.Add(fineBridge ? "Fine waterの閉輪郭を表示と進入禁止Colliderで共有。coarse Water/Walkは接続骨格metadataで、実通行はFineWaterCollision＋RetainedSolid＋prop足元Colliderが正本。専用泡追加なし。"
                        : "BridgeRiver5素材を表示。川は既存2セルmaskと64px段差のまま、外岸soil8/16pxで外観補助。専用foam素材が無いため泡追加なし。連続した滑らかな川の物理地形ではありません。");
                    if (!fineBridge && savedSettings.Case == StudyABlockSceneCase.PathAndBridgeBank) report.Warnings.Add("Global Fine soilはBridgeguardで無効。既存FinePath Native指定も生成Sceneはmacro Soilへ結合し、StableFine pathの細輪郭表示と同品質とは扱いません。");
                }
                else if (assembly.WaterCells.Count > 0) report.Warnings.Add("water/bridgeは診断描画のままです。承認Terrainはgrass/soil/境界だけです。");
                if (report.CustomTerrainCellsPreserved > 0) report.Warnings.Add("指定NativePrefabのcustom tileを保持しました: " + report.CustomTerrainCellsPreserved + " cells。全地表が承認Terrainとは限りません。");
                report.Warnings.Add("静止Hero。実Actor歩行・遮蔽・Prefab接続の実画像は別途レビューしてください。");
                report.Warnings.Add("Enemy専用の許可領域・抽選契約は未実装のため、今回Enemy候補は出力しません。");
                if (fineBridge) UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
                AssetDatabase.SaveAssetIfDirty(savedSettings);
                if (!EditorSceneManager.SaveScene(generatedScene, report.ScenePath)) throw new IOException("独立Scene保存に失敗しました。");
                var aspect = camera.aspect;
                try
                {
                    if (threeBlocks)
                    {
                        ForestRestartAssetBuilder.CaptureCamera(overview, ProjectPath(report.CapturePath), report.OverviewCaptureWidth, report.OverviewCaptureHeight);
                        ForestRestartAssetBuilder.CaptureCamera(camera, ProjectPath(report.GameplayCapturePath), CaptureWidth, CaptureHeight);
                    }
                    else ForestRestartAssetBuilder.CaptureCamera(camera, ProjectPath(report.CapturePath), CaptureWidth, CaptureHeight);
                }
                finally { camera.aspect = aspect; }
                report.Generated = true;
                File.WriteAllText(ProjectPath(report.MetadataPath), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("Study A TERRAIN PROTOTYPE generated: " + report.ScenePath + "\n" + report.CapturePath);
            }
            catch (Exception exception) { report.Errors.Add(exception.ToString()); }
            finally
            {
                if (generatedScene.IsValid() && generatedScene.isLoaded) EditorSceneManager.CloseScene(generatedScene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                if (ownSettings) Object.DestroyImmediate(settings);
                if (savedSettings != null && !AssetDatabase.Contains(savedSettings)) Object.DestroyImmediate(savedSettings);
            }
            return report;
        }

        public static StudyABlockDefinition ReadPrefabDefinition(GameObject prefab)
        {
            if (prefab == null || !AssetDatabase.Contains(prefab)) throw new ArgumentException("保存済みのBlock Prefabが必要です。");
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                if (transform.localScale != Vector3.one || Quaternion.Angle(transform.localRotation, Quaternion.identity) > 0.001f)
                    throw new ArgumentException("ブロックはScale1/回転0、90度回転/flipなしです: " + transform.name);
            if (prefab.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true).Length != 0)
                throw new ArgumentException("区画SortingGroupは使用しません。各Spriteをglobal足元Yで描画してください。");
            var data = prefab.GetComponent<StudyABlockPrefabData>();
            if (data == null || data.Definition == null) throw new ArgumentException("Block authoring契約がありません。");
            foreach (var name in new[] { "Grid", "Ports", "MainRoute", "FixedScenery", "VariationSlots" })
            {
                var child = prefab.transform.Find(name);
                if (child == null || child.localPosition != Vector3.zero) throw new ArgumentException("Authoring RootはlocalPosition0にしてください: " + name);
            }
            var definition = JsonUtility.FromJson<StudyABlockDefinition>(JsonUtility.ToJson(data.Definition));
            var grid = prefab.transform.Find("Grid").GetComponent<Grid>();
            if (grid == null || grid.cellLayout != GridLayout.CellLayout.Rectangle || grid.cellSwizzle != GridLayout.CellSwizzle.XYZ
                || grid.cellSize.x != 1f || grid.cellSize.y != 1f || grid.cellGap != Vector3.zero)
                throw new ArgumentException("Prefab Gridはunit XY/Rectangle/XYZ/CellGap0にしてください。");
            var ground = prefab.transform.Find("Grid/GroundTilemap")?.GetComponent<Tilemap>();
            if (ground == null || ground.cellBounds.min != Vector3Int.zero) throw new ArgumentException("Groundはlocal(0,0)から始めてください。");
            if (ground.transform.localPosition != Vector3.zero) throw new ArgumentException("GroundTilemapのlocalPositionは0にしてください。");
            foreach (var name in new[] { "Ground", "Soil", "Water", "BridgeFloor" })
            {
                var visible = prefab.transform.Find("Grid/" + name + "Tilemap")?.GetComponent<Tilemap>();
                if (visible == null || visible.tileAnchor != new Vector3(0.5f, 0.5f, 0f))
                    throw new ArgumentException("Visible TilemapのtileAnchorは(0.5,0.5,0)にしてください: " + name);
            }
            definition.Extent = new Vector2Int(ground.cellBounds.size.x, ground.cellBounds.size.y);
            foreach (var cell in ground.cellBounds.allPositionsWithin)
                if (cell.z != 0 || !ground.HasTile(cell)) throw new ArgumentException("GroundはZ0の区画矩形全体を埋めてください。");
            var targets = new[] { definition.WalkCells, definition.SoilCells, definition.RoadCells, definition.WaterCells, definition.BridgeCells, definition.BridgeFloorCells, definition.SolidCells, definition.OcclusionCells };
            for (var i = 0; i < MaskNames.Length; i++)
            {
                targets[i].Clear(); var tilemap = prefab.transform.Find("Grid/" + MaskNames[i] + "Tilemap")?.GetComponent<Tilemap>();
                if (tilemap == null) throw new ArgumentException("Prefab mask不足: " + MaskNames[i]);
                if (tilemap.transform.localPosition != Vector3.zero) throw new ArgumentException("Prefab maskのlocalPositionは0にしてください。");
                foreach (var cell in tilemap.cellBounds.allPositionsWithin) if (tilemap.HasTile(cell))
                {
                    if (cell.z != 0) throw new ArgumentException("Prefab maskはZ0だけに配置してください。");
                    targets[i].Add(new Vector2Int(cell.x, cell.y));
                }
            }
            definition.Ports.Clear();
            var portRoot = prefab.transform.Find("Ports");
            if (portRoot == null || portRoot.localPosition != Vector3.zero) throw new ArgumentException("Prefab Ports rootが不正です。");
            foreach (Transform child in portRoot) definition.Ports.Add(child.GetComponent<StudyABlockPortMarker>()?.ReadPort() ?? throw new ArgumentException("PortMarker不足"));
            definition.MainRoute.Clear();
            var route = prefab.transform.Find("MainRoute");
            if (route == null || route.localPosition != Vector3.zero) throw new ArgumentException("MainRoute root不足/offset不正");
            foreach (Transform point in route) definition.MainRoute.Add(point.localPosition);
            var spawn = prefab.transform.Find("Spawn"); if (spawn == null) throw new ArgumentException("Prefab Spawn不足");
            definition.Spawn = spawn.localPosition;
            var errors = definition.Validate(); if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors));
            return definition;
        }

        private static GameObject BuildPrefab(StudyABlockDefinition definition, Dictionary<string, StudyAStarterProp> props, Dictionary<string, Tile> tiles, StudyATerrainSpriteSet terrainSprites, int seed, Material material, string path, bool fineSoil, StudyABridgeRiverSpriteSet bridgeArt = null, bool fineBridge = false, int bridgeLandscapeVersion = 0)
        {
            var root = Child(null, definition.Kind.ToString());
            try
            {
                root.AddComponent<StudyABlockPrefabData>().Definition = definition;
                if (fineSoil && definition.Kind != StudyABlockKind.BridgeBank)
                {
                    var polygon = StudyASoilContourRenderer.DefaultPolygon(definition, Vector2Int.zero);
                    var contour = root.AddComponent<StudyASoilContour>(); contour.Points = polygon.Points.ToList(); contour.PixelStep = polygon.PixelStep;
                    var mask = StudyASoilContourRenderer.MakeMask(new[] { polygon }, Vector2Int.zero, definition.Extent.x, definition.Extent.y);
                    var texturePath = path.Substring(0, path.LastIndexOf("/Prefabs/", StringComparison.Ordinal)) + "/FineSoil/native-" + definition.Kind + ".png";
                    var sprite = StudyASoilContourRenderer.WriteSprite(mask, terrainSprites, seed, texturePath, out _);
                    var render = SpriteObject(root.transform, "FineSoilAuthoringPreview", sprite, Vector2.zero, material).GetComponent<SpriteRenderer>();
                    render.sortingLayerName = "MapGround"; render.sortingOrder = 10;
                }
                var grid = Child(root.transform, "Grid"); grid.AddComponent<Grid>();
                var ground = Tilemap(grid.transform, "GroundTilemap", "MapGround", 0, material);
                foreach (var cell in definition.Bounds.allPositionsWithin) ground.SetTile(Cell(cell), GrassTile(seed, cell, terrainSprites, tiles));
                var localSoil = new HashSet<Vector2Int>(definition.SoilCells); RequireTerrainLayout(localSoil);
                var sources = new[] { definition.WalkCells, definition.SoilCells, definition.RoadCells, definition.WaterCells, definition.BridgeCells, definition.BridgeFloorCells, definition.SolidCells, definition.OcclusionCells };
                for (var i = 0; i < MaskNames.Length; i++)
                {
                    var visible = MaskNames[i] == "Soil" || MaskNames[i] == "Water" || MaskNames[i] == "BridgeFloor";
                    var tilemap = Tilemap(grid.transform, MaskNames[i] + "Tilemap", "MapGround", i + 1, material);
                    tilemap.GetComponent<TilemapRenderer>().enabled = visible && !(fineSoil && definition.Kind != StudyABlockKind.BridgeBank && MaskNames[i] == "Soil");
                    if (MaskNames[i] == "Soil")
                        foreach (var cell in sources[i]) tilemap.SetTile(Cell(cell), SoilTile(seed, cell, localSoil, terrainSprites, tiles));
                    else Paint(tilemap, sources[i], tiles[MaskNames[i] == "Water" ? "water" : MaskNames[i] == "BridgeFloor" ? "deck" : "soil"]);
                }
                if (bridgeArt != null) StudyABridgeRiverRenderer.AddNativePreview(root, definition, bridgeArt, material);
                if (fineBridge && definition.Kind == StudyABlockKind.BridgeBank)
                {
                    StudyABridgeRiverFineGeometry.CreateDefaultContour(root, definition);
                    var road = root.AddComponent<StudyASoilContour>(); road.PixelStep = 8;
                    road.Points = StudyABridgeRiverRenderer.DefaultRoadPolygon(definition, Vector2Int.zero).Points.ToList();
                    StudyABridgeRiverRenderer.AddFineNativePreview(root, definition, bridgeArt, terrainSprites, settingsSeed: seed, material: material, outputRoot: path.Substring(0, path.LastIndexOf("/Prefabs/", StringComparison.Ordinal)));
                }
                var ports = Child(root.transform, "Ports");
                foreach (var port in definition.Ports)
                {
                    var go = Child(ports.transform, "port_" + port.Id); go.transform.localPosition = (Vector2)port.Position + Vector2.one * 0.5f;
                    var marker = go.AddComponent<StudyABlockPortMarker>(); marker.Id = port.Id; marker.Direction = port.Direction; marker.Width = port.Width; marker.Terrain = port.Terrain; marker.Height = port.Height;
                }
                var route = Child(root.transform, "MainRoute");
                for (var i = 0; i < definition.MainRoute.Count; i++) Child(route.transform, "point_" + i.ToString("000")).transform.localPosition = definition.MainRoute[i];
                Child(root.transform, "Spawn").transform.localPosition = definition.Spawn;
                var fixedScenery = Child(root.transform, "FixedScenery");
                var fixedSlots = (fineBridge && definition.Kind == StudyABlockKind.BridgeBank ? FineBridgeFixedSlots(bridgeLandscapeVersion) : FixedSlots(definition.Kind)).ToArray();
                ValidateFixedSlots(definition, fixedSlots, props);
                var names = new Dictionary<string, int>();
                foreach (var slot in fixedSlots)
                {
                    names.TryGetValue(slot.Item1, out var index); names[slot.Item1] = index + 1;
                    CreateProp(fixedScenery.transform, slot.Item1 + "_" + index.ToString("00"), props[slot.Item1], slot.Item2, material);
                }
                var variables = Child(root.transform, "VariationSlots");
                foreach (var slot in VariableSlots(definition.Kind)) Child(variables.transform, slot.Item1).transform.localPosition = slot.Item2;
                foreach (var tilemap in root.GetComponentsInChildren<Tilemap>()) tilemap.CompressBounds();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static IEnumerable<Tuple<string, Vector2>> FixedSlots(StudyABlockKind kind)
        {
            if (kind == StudyABlockKind.Plaza)
            {
                // 南側は2つの大小混成群。実alpha上端は主道のy=4より下に留める。
                yield return Tuple.Create("treeLarge", new Vector2(0.875f, 0.25f)); yield return Tuple.Create("treeMedium", new Vector2(2.5f, 0.0625f));
                yield return Tuple.Create("treeLarge", new Vector2(3.625f, 0.125f)); yield return Tuple.Create("treeMedium", new Vector2(7.875f, 0.25f));
                yield return Tuple.Create("treeLarge", new Vector2(9.25f, 0.125f)); yield return Tuple.Create("treeMedium", new Vector2(11f, 0.5f));
                // 北西の奥行きある塊と北の背面林。等間隔の単木列にしない。
                yield return Tuple.Create("treeLarge", new Vector2(0.75f, 7.625f)); yield return Tuple.Create("treeLarge", new Vector2(2.375f, 8.5f));
                yield return Tuple.Create("treeMedium", new Vector2(1.25f, 10.125f)); yield return Tuple.Create("treeMedium", new Vector2(3f, 11.25f));
                yield return Tuple.Create("treeLarge", new Vector2(5.25f, 11f)); yield return Tuple.Create("treeMedium", new Vector2(7.375f, 10.875f));
                yield return Tuple.Create("treeLarge", new Vector2(9.125f, 11.125f)); yield return Tuple.Create("treeMedium", new Vector2(11.25f, 10.875f));
                // 屋台より足元Yが高い背面林は屋根を覆わず、店前と中央広場は開ける。
                yield return Tuple.Create("fountain", new Vector2(5.75f, 7.75f)); yield return Tuple.Create("shop", new Vector2(9f, 8.125f));
                yield return Tuple.Create("shrub", new Vector2(0.625f, 0.125f)); yield return Tuple.Create("shrub", new Vector2(2.25f, 0.125f));
                yield return Tuple.Create("shrub", new Vector2(5.75f, 1.75f)); yield return Tuple.Create("shrub", new Vector2(7.125f, 0.25f));
                yield return Tuple.Create("shrub", new Vector2(9.875f, 0.125f)); yield return Tuple.Create("shrub", new Vector2(0.875f, 7.25f));
                yield return Tuple.Create("shrub", new Vector2(3.375f, 7.375f)); yield return Tuple.Create("shrub", new Vector2(11.25f, 10.25f));
                yield return Tuple.Create("rock", new Vector2(5.625f, 0.875f)); yield return Tuple.Create("rock", new Vector2(11.25f, 2.625f));
                yield return Tuple.Create("rock", new Vector2(3.125f, 7.625f)); yield return Tuple.Create("rock", new Vector2(11.25f, 9.875f));
            }
            else if (kind == StudyABlockKind.ForestPath)
            {
                // 曲がりの上側を含め3u路面は開け、樹冠だけを隣の広場背面林へつなぐ。
                yield return Tuple.Create("treeLarge", new Vector2(0.875f, 0.25f)); yield return Tuple.Create("treeMedium", new Vector2(2.5f, 0.0625f));
                yield return Tuple.Create("treeLarge", new Vector2(3.625f, 0.125f)); yield return Tuple.Create("treeMedium", new Vector2(6.25f, 0.25f));
                yield return Tuple.Create("treeLarge", new Vector2(7.25f, 0.125f));
                yield return Tuple.Create("treeLarge", new Vector2(0.875f, 7.625f)); yield return Tuple.Create("treeMedium", new Vector2(2.375f, 8.5f));
                yield return Tuple.Create("treeLarge", new Vector2(4.125f, 8f)); yield return Tuple.Create("treeMedium", new Vector2(6.125f, 7.75f));
                yield return Tuple.Create("treeLarge", new Vector2(7.375f, 9.125f));
                yield return Tuple.Create("treeLarge", new Vector2(2.125f, 10.875f)); yield return Tuple.Create("treeMedium", new Vector2(5.5f, 10.875f));
                yield return Tuple.Create("shrub", new Vector2(0.875f, 0.125f)); yield return Tuple.Create("shrub", new Vector2(2.375f, 0.25f));
                yield return Tuple.Create("shrub", new Vector2(5.375f, 0.125f)); yield return Tuple.Create("shrub", new Vector2(7.125f, 0.25f));
                yield return Tuple.Create("shrub", new Vector2(0.875f, 7.25f)); yield return Tuple.Create("shrub", new Vector2(5.75f, 7.125f));
                yield return Tuple.Create("shrub", new Vector2(7.25f, 7.375f)); yield return Tuple.Create("rock", new Vector2(4.625f, 0.125f));
            }
            else
            {
                yield return Tuple.Create("treeLarge", new Vector2(1.75f, 0.125f)); yield return Tuple.Create("treeMedium", new Vector2(10.25f, 0.125f));
                yield return Tuple.Create("treeLarge", new Vector2(1.75f, 8.125f)); yield return Tuple.Create("treeMedium", new Vector2(10.25f, 8.875f));
            }
        }

        private static void ValidateFixedSlots(StudyABlockDefinition definition, Tuple<string, Vector2>[] slots, Dictionary<string, StudyAStarterProp> props)
        {
            var bodies = new List<Rect>();
            foreach (var slot in slots)
            {
                var prop = props[slot.Item1];
                var footprint = prop.Metrics.LocalFootprint; footprint.position += slot.Item2;
                if (!Contains(definition.Bounds, footprint) || definition.WaterCells.Concat(definition.RoadCells).Any(c => footprint.Overlaps(new Rect((Vector2)c, Vector2.one))))
                    throw new InvalidDataException("固定Sceneryの足元が区画外/川/主道に侵入しています: " + definition.Id + "/" + slot.Item1 + "@" + slot.Item2);
                var body = prop.Metrics.LocalCollider; body.position += slot.Item2;
                if (body.width <= 0f || body.height <= 0f) continue;
                if (bodies.Any(b => b.Overlaps(body))) throw new InvalidDataException("固定Sceneryの実Collisionが重なっています: " + definition.Id + "/" + slot.Item1 + "@" + slot.Item2);
                bodies.Add(body);
            }
        }

        private static IEnumerable<Tuple<string, Vector2>> VariableSlots(StudyABlockKind kind)
        {
            if (kind == StudyABlockKind.ForestPath)
            {
                yield return Tuple.Create("shrub_00", new Vector2(2f, 0.5f)); yield return Tuple.Create("rock_01", new Vector2(1.5f, 9.375f)); yield return Tuple.Create("rock_02", new Vector2(6.5f, 9.25f));
            }
            else
            {
                yield return Tuple.Create("shrub_00", new Vector2(3.5f, 1.25f)); yield return Tuple.Create("rock_01", new Vector2(8f, 1.25f)); yield return Tuple.Create("shrub_02", new Vector2(3.5f, 9.5f));
            }
        }

        private static void AddVariations(GameObject prefab, StudyAPlacedBlock block, StudyABlockAssembly assembly, StudyABlockSceneSettings settings, Dictionary<string, StudyAStarterProp> props, Transform parent, Material material, List<string> selected)
        {
            if (!settings.EnableLimitedVariation) return;
            var root = prefab.transform.Find("VariationSlots"); if (root == null) return;
            var count = 0;
            foreach (Transform slot in root)
            {
                if (count >= settings.MaxVariablePropsPerBlock) break;
                var identity = block.Definition.Id + "/" + slot.name;
                var random = new System.Random(unchecked(settings.Seed ^ StableInt(identity)));
                var key = slot.name.Split('_')[0]; if (!props.TryGetValue(key, out var prop)) throw new ArgumentException("VariationSlot role不明: " + slot.name);
                var localFootprint = prop.Metrics.LocalFootprint; localFootprint.position += (Vector2)slot.localPosition;
                var region = block.Definition.VariationRegions.FirstOrDefault(r => r.Roles.Contains(prop.Metrics.Role) && Contains(r.Bounds, localFootprint));
                if (region == null || random.NextDouble() >= settings.VariationProbability * Mathf.Clamp01(region.Density)) continue;
                var position = (Vector2)block.Origin + (Vector2)slot.localPosition;
                var collider = prop.Metrics.LocalCollider; collider.position += position;
                var footprint = prop.Metrics.LocalFootprint; footprint.position += position;
                if (!Contains(block.Bounds, footprint) || assembly.WaterCells.Concat(assembly.RoadCells).Any(c => footprint.Overlaps(new Rect((Vector2)c, Vector2.one)))) continue;
                if (collider.width > 0f && collider.height > 0f)
                {
                    if (!Contains(region.Bounds, new Rect(collider.position - (Vector2)block.Origin, collider.size))) continue;
                    Physics2D.SyncTransforms();
                    if (parent.GetComponentsInChildren<BoxCollider2D>().Any(c => c.bounds.Intersects(new Bounds(collider.center, collider.size)))) continue;
                }
                var visible = prop.Sprite.bounds; var visualRect = new Rect((Vector2)visible.min + position, visible.size);
                var playerView = new Rect(assembly.Spawn + new Vector2(-0.5f, -0.25f), new Vector2(1f, 1.5f));
                if (visualRect.Overlaps(playerView)) continue;
                if (parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite == props["shop"].Sprite || r.sprite == props["fountain"].Sprite)
                    .Any(r => visualRect.Overlaps(new Rect(r.bounds.min, r.bounds.size)))) continue;
                CreateProp(parent, identity.Replace('/', '_'), prop, position, material);
                selected.Add(identity + "[" + region.Id + "]=" + key + "@" + position.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + position.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); count++;
            }
        }

        private static void MergeVisibleTiles(GameObject prefab, StudyAPlacedBlock block, UnityEngine.Tilemaps.Tilemap ground, UnityEngine.Tilemaps.Tilemap water, UnityEngine.Tilemaps.Tilemap terrain, Dictionary<UnityEngine.Tilemaps.Tilemap, Material> copiedMaterials)
        {
            void Copy(string mask, IEnumerable<Vector2Int> cells, UnityEngine.Tilemaps.Tilemap target)
            {
                var source = prefab.transform.Find("Grid/" + mask + "Tilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
                var copyCells = cells.ToArray();
                if (copyCells.Length == 0) return;
                var sourceMaterial = source.GetComponent<TilemapRenderer>().sharedMaterial;
                if (copiedMaterials.TryGetValue(target, out var copied) && copied != sourceMaterial)
                    throw new InvalidDataException("同一Tilemapへ異なるPrefab Materialを結合できません。元Prefabの意図を保つため停止: " + block.Definition.Id + "/" + mask);
                copiedMaterials[target] = sourceMaterial; target.GetComponent<TilemapRenderer>().sharedMaterial = sourceMaterial;
                foreach (var cell in copyCells)
                {
                    var local = Cell(cell); var world = Cell(cell + block.Origin);
                    if (source.GetTransformMatrix(local) != Matrix4x4.identity) throw new ArgumentException("Tileの回転/scale/flipは使用しません。");
                    target.SetTile(world, source.GetTile(local));
                    target.SetTileFlags(world, TileFlags.None); target.SetColor(world, source.GetColor(local) * source.color);
                }
            }
            Copy("Ground", Enumerate(block.Definition.Bounds), ground);
            Copy("Water", block.Definition.WaterCells, water);
            Copy("Soil", block.Definition.SoilCells.Except(block.Definition.WaterCells), terrain);
            Copy("BridgeFloor", block.Definition.BridgeFloorCells, terrain);
        }

        private static void RequireTerrainLayout(ISet<Vector2Int> soil)
        {
            var errors = StudyATerrainTileSelector.ValidateLayout(soil);
            if (errors.Count > 0) throw new InvalidDataException("承認された地表境界で表せない形です。無言の平面fallbackは行いません: " + string.Join("; ", errors));
        }

        private static Tile GrassTile(int seed, Vector2Int cell, StudyATerrainSpriteSet sprites, Dictionary<string, Tile> tiles)
        {
            var index = StudyATerrainTileSelector.SelectVariant(unchecked(seed ^ StableInt("grass")), cell, sprites.GrassVariants.Length);
            return tiles[sprites.GrassVariants[index].name];
        }

        private static Tile SoilTile(int seed, Vector2Int cell, ISet<Vector2Int> soil, StudyATerrainSpriteSet sprites, Dictionary<string, Tile> tiles)
        {
            if (!StudyATerrainTileSelector.TrySelect(soil, cell, out var shape, out var error)) throw new InvalidDataException(cell + ": " + error);
            var sprite = shape == StudyASoilTileShape.Interior
                ? sprites.SoilVariants[StudyATerrainTileSelector.SelectVariant(unchecked(seed ^ StableInt(shape.ToString())), cell, sprites.SoilVariants.Length)]
                : sprites.Boundaries[shape];
            return tiles[sprite.name];
        }

        private static StudyATerrainSettings ResolveTerrainSettings(StudyABlockSceneSettings settings)
        {
            var revision = settings == null ? StudyABlockTerrainRevision.Current : settings.TerrainRevision;
            if (!Enum.IsDefined(typeof(StudyABlockTerrainRevision), revision)) throw new ArgumentException("Unknown TerrainRevision.");
            if (revision == StudyABlockTerrainRevision.Current && settings != null)
            {
                var versions = new HashSet<StudyABlockTerrainRevision>();
                foreach (var prefab in new[] { settings.PlazaPrefab, settings.ForestPathPrefab, settings.BridgeBankPrefab }.Where(p => p != null))
                {
                    foreach (var name in new[] { "GroundTilemap", "SoilTilemap" })
                    {
                        var map = prefab.transform.Find("Grid/" + name)?.GetComponent<UnityEngine.Tilemaps.Tilemap>(); if (map == null) continue;
                        foreach (var cell in map.cellBounds.allPositionsWithin)
                        {
                            var sprite = map.GetSprite(cell); if (sprite == null || !StudyATerrainAssets.IsApprovedGroundSprite(sprite)) continue;
                            var path = AssetDatabase.GetAssetPath(sprite);
                            if (path.StartsWith(StudyATerrainAssets.R1DerivedRoot + "/", StringComparison.Ordinal)) versions.Add(StudyABlockTerrainRevision.R1);
                            if (path.StartsWith(StudyATerrainAssets.R2DerivedRoot + "/", StringComparison.Ordinal)) versions.Add(StudyABlockTerrainRevision.GroundR2);
                        }
                    }
                }
                if (versions.Count > 1) throw new InvalidDataException("NativePrefabに地表版が混在しています。TerrainRevisionを明示してください。");
                if (versions.Count == 1) revision = versions.Single();
            }
            return revision == StudyABlockTerrainRevision.R1
                ? new StudyATerrainSettings { InputRoot = StudyATerrainAssets.R1InputRoot, DerivedRoot = StudyATerrainAssets.R1DerivedRoot }
                : new StudyATerrainSettings { InputRoot = StudyATerrainAssets.R2InputRoot, DerivedRoot = StudyATerrainAssets.R2DerivedRoot };
        }

        private static int ApplyGlobalTerrain(StudyABlockAssembly assembly, int seed, StudyATerrainSpriteSet sprites, Dictionary<string, Tile> tiles,
            UnityEngine.Tilemaps.Tilemap ground, UnityEngine.Tilemaps.Tilemap terrain, string runRoot, List<string> selections, int frameWidth = Width)
        {
            // Known approved sprites remain managed after prefab save/reload. Custom overrides retain their authored tile/style.
            RequireTerrainLayout(assembly.SoilCells);
            var approvedGrass = new HashSet<Sprite>(sprites.GrassVariants);
            var approvedSoil = new HashSet<Sprite>(sprites.SoilVariants.Concat(sprites.Boundaries.Values));
            var styled = new Dictionary<Tuple<Tile, Tile>, Tile>();
            var knownSprites = new Dictionary<Sprite, bool>();
            var preserved = 0;
            void Replace(UnityEngine.Tilemaps.Tilemap map, Vector2Int cell, Tile chosen, HashSet<Sprite> approved, string role)
            {
                var original = map.GetTile(Cell(cell)) as Tile;
                bool KnownRole(Sprite sprite)
                {
                    if (sprite == null) return false;
                    if (approved.Contains(sprite)) return true;
                    if (!knownSprites.TryGetValue(sprite, out var known)) { known = StudyATerrainAssets.IsApprovedGroundSprite(sprite); knownSprites.Add(sprite, known); }
                    return known && sprite.name.StartsWith(role == "grass" ? "grass_" : "dirt_", StringComparison.Ordinal);
                }
                if (original == null || original.GetType() != typeof(Tile) || !KnownRole(original.sprite))
                {
                    preserved++; selections.Add(role + "@" + cell.x + "," + cell.y + "=authored:" + (map.GetTile(Cell(cell))?.name ?? "none")); return;
                }
                var replacement = chosen;
                if (original.color != chosen.color || original.flags != chosen.flags || original.colliderType != chosen.colliderType || original.transform != chosen.transform || original.gameObject != chosen.gameObject)
                {
                    var identity = Tuple.Create(original, chosen);
                    if (!styled.TryGetValue(identity, out replacement))
                    {
                        replacement = Object.Instantiate(original); replacement.sprite = chosen.sprite;
                        replacement.name = "approved-preserved-style-" + styled.Count.ToString("000");
                        AssetDatabase.CreateAsset(replacement, runRoot + "/Tiles/" + replacement.name + ".asset"); styled.Add(identity, replacement);
                    }
                }
                var tint = map.GetColor(Cell(cell));
                map.SetTile(Cell(cell), replacement); map.SetTileFlags(Cell(cell), TileFlags.None); map.SetColor(Cell(cell), tint);
                selections.Add(role + "@" + cell.x + "," + cell.y + "=" + chosen.sprite.name);
            }
            foreach (var cell in new RectInt(0, 0, frameWidth, Height).allPositionsWithin)
                Replace(ground, cell, GrassTile(seed, cell, sprites, tiles), approvedGrass, "grass");
            foreach (var cell in assembly.SoilCells.OrderBy(c => c.y).ThenBy(c => c.x))
                Replace(terrain, cell, SoilTile(seed, cell, assembly.SoilCells, sprites, tiles), approvedSoil, "soil");
            return preserved;
        }

        private static GameObject CreateProp(Transform parent, string name, StudyAStarterProp prop, Vector2 position, Material material)
        {
            var go = SpriteObject(parent, name, prop.Sprite, position, material);
            var collider = prop.Metrics.LocalCollider;
            if (collider.width > 0f && collider.height > 0f)
            {
                var footprint = prop.Metrics.LocalFootprint; var min = Vector2Int.FloorToInt(footprint.min + position); var max = Vector2Int.CeilToInt(footprint.max + position);
                ConfigureCollision(go, max - min, min - Vector2Int.FloorToInt(position), collider.size, collider.center);
            }
            return go;
        }


        private static void ResolveFineDepthTies(GameObject map, StudyABlockSceneReport report)
        {
            // Equal foot-Y transparent sprites otherwise fall back to transient renderer/material IDs.
            // One sprite per proxy keeps world-Y sorting against the actual actor. Child graphics and
            // its collision body retain their exact world pose; only the invisible sort anchor differs.
            var sprites = map.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null
                && r.sortingLayerName == "WorldObjects" && r.spriteSortPoint == SpriteSortPoint.Pivot
                && !(report.BridgeStructureVersion == 1 && r.name.StartsWith("Bridge01_", StringComparison.Ordinal))).ToArray();
            var keys = sprites.ToDictionary(r => r, r => StableHierarchyKey(r.transform) + "/" + AssetDatabase.GetAssetPath(r.sprite));
            var records = new List<string>();
            foreach (var domain in sprites.GroupBy(r => Tuple.Create(r.sortingLayerID, r.sortingOrder)))
            {
                var ys = domain.Select(r => r.transform.position.y).Distinct().ToArray();
                foreach (var cohort in domain.GroupBy(r => r.transform.position.y).Where(g => g.Count() > 1).OrderBy(g => g.Key))
                {
                    var members = cohort.Where(r => cohort.Any(other => other != r && r.bounds.Intersects(other.bounds)))
                        .OrderBy(r => r.name.StartsWith("ArtFinish_BridgeBeam_", StringComparison.Ordinal) ? 1 : 0)
                        .ThenBy(r => keys[r], StringComparer.Ordinal).ToArray();
                    if (members.Length < 2) continue;
                    var nearest = ys.Where(y => y != cohort.Key).Select(y => Math.Abs((double)y-cohort.Key)).DefaultIfEmpty(1d).Min();
                    // A power-of-two offset preserves the original binary pixel positions on reparent.
                    // Entire cohort stays within1/4 of the nearest distinct Y, and below1/128world.
                    var limit = Math.Min(1d/128d, nearest/4d);
                    var unit = (float)Math.Pow(2d, Math.Floor(Math.Log(limit/(members.Length+1), 2d)));
                    for (var i = 0; i < members.Length; i++)
                    {
                        var renderer = members[i];
                        if (renderer.GetComponentInParent<UnityEngine.Rendering.SortingGroup>() != null || renderer.GetComponentsInChildren<SpriteRenderer>(true).Length != 1)
                            throw new ArgumentException("Fine tie proxy requires one independent sprite: " + keys[renderer]);
                        var world = renderer.transform.position; var bias = (i-(members.Length-1)*.5f)*unit;
                        var sortY = world.y+bias;
                        if (bias == 0f) continue; // The middle sprite may stay at its original exact foot-Y.
                        if (sortY == world.y) throw new ArgumentException("Fine tie proxy is below float precision: " + keys[renderer]);
                        var proxy = Child(renderer.transform.parent, renderer.name + "_FootSortProxy");
                        proxy.transform.position = new Vector3(world.x,sortY,world.z);
                        var group = proxy.AddComponent<UnityEngine.Rendering.SortingGroup>(); group.sortingLayerID=renderer.sortingLayerID; group.sortingOrder=renderer.sortingOrder;
                        renderer.transform.SetParent(proxy.transform,true);
                        if (!renderer.transform.position.Equals(world)) throw new InvalidOperationException("Fine tie proxy changed sprite/collider world anchor: " + keys[renderer]);
                        records.Add(keys[renderer]+"@"+world.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"=>"+sortY.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }
            report.FineDepthTieProxyCount=records.Count; report.FineDepthTieSelections=records.ToArray(); report.FineDepthTieSelectionHash=Hash(string.Join("\n",records));
        }

        private static string StableHierarchyKey(Transform transform)
        {
            var parts=new List<string>();
            while (transform != null) { parts.Add(transform.name+"["+transform.GetSiblingIndex().ToString("D4",System.Globalization.CultureInfo.InvariantCulture)+"]"); transform=transform.parent; }
            parts.Reverse(); return string.Join("/",parts);
        }

        private static bool WantsFineContour(StudyABlockSceneSettings settings)
        {
            var supported = settings == null || settings.Case == StudyABlockSceneCase.Plaza || settings.Case == StudyABlockSceneCase.ForestPath || settings.Case == StudyABlockSceneCase.PlazaAndPath;
            var fresh = settings == null || (settings.PlazaPrefab == null && settings.ForestPathPrefab == null && settings.BridgeBankPrefab == null);
            return settings != null && settings.EnableFineSoilContour || (fresh && supported);
        }


        private static StudyASoilContourPolygon[] ReadBridgeRoadPolygons(StudyABlockAssembly assembly, StudyABlockSceneSettings settings)
        {
            var polygons=new List<StudyASoilContourPolygon>();
            var material=AssetDatabase.LoadAssetAtPath<Material>(ForestRestartAssetBuilder.ArtRoot+"/ForestRestartUnlit.mat");
            foreach(var block in assembly.Blocks)
            {
                var prefab=Template(settings,block.Definition.Kind);
                if(prefab==null)
                {
                    if(block.Definition.Kind!=StudyABlockKind.BridgeBank)throw new ArgumentException("Fine bridge pair requires an explicitly authored Fine ForestPath Native Prefab; review single first.");
                    polygons.Add(StudyABridgeRiverRenderer.DefaultRoadPolygon(block.Definition,block.Origin));continue;
                }
                var controls=prefab.GetComponentsInChildren<StudyASoilContour>(true);
                if(controls.Length!=1||controls[0].transform!=prefab.transform)throw new ArgumentException("Fine bridge road requires one editable root Soil contour in each supplied Native.");
                var polygon=new StudyASoilContourPolygon{BlockId=block.Definition.Id,Origin=block.Origin,Extent=block.Definition.Extent,PixelStep=controls[0].PixelStep,Points=controls[0].Points?.ToArray()};
                var errors=StudyASoilContourRenderer.ValidatePolygon(polygon);if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors));
                foreach(var key in new[]{"Ground","Soil"})
                {
                    var map=prefab.transform.Find("Grid/"+key+"Tilemap").GetComponent<Tilemap>();var renderer=map.GetComponent<TilemapRenderer>();
                    if(map.color!=Color.white||renderer.sharedMaterial!=material||renderer.HasPropertyBlock()||renderer.sortingLayerName!="MapGround"||renderer.sortingOrder!=(key=="Ground"?0:2)||renderer.mode!=TilemapRenderer.Mode.Chunk||(key=="Soil"&&renderer.enabled))
                        throw new ArgumentException("Fine road preserves custom Native ground/soil tint/material/sorting by refusing replacement.");
                    var seen=new HashSet<Sprite>();
                    foreach(var cell in map.cellBounds.allPositionsWithin)
                    {
                        if(!map.HasTile(cell))continue;var tile=map.GetTile<Tile>(cell);
                        if(tile==null||tile.GetType()!=typeof(Tile)||tile.gameObject!=null||tile.color!=Color.white||tile.flags!=TileFlags.LockColor||tile.colliderType!=Tile.ColliderType.None
                            ||tile.transform!=Matrix4x4.identity||map.GetColor(cell)!=Color.white||map.GetTransformMatrix(cell)!=Matrix4x4.identity||tile.sprite==null
                            ||(seen.Add(tile.sprite)&&!StudyATerrainAssets.IsApprovedGroundSprite(tile.sprite)))throw new ArgumentException("Fine road preserves custom Native tile/style/object by refusing overpaint.");
                    }
                }
                polygons.Add(polygon);
            }
            return polygons.ToArray();
        }

        private static IEnumerable<Tuple<string,Vector2>> FineBridgeFixedSlots(int landscapeVersion)
        {
            // One wooded bank with overlapping crowns; the opposite bank stays low and open.
            yield return Tuple.Create("treeLarge",new Vector2(1.25f,.125f));yield return Tuple.Create("treeMedium",new Vector2(2.875f,.8125f));
            yield return Tuple.Create("treeLarge",new Vector2(1.75f,8.125f));yield return Tuple.Create("treeLarge",new Vector2(3.125f,8.875f));yield return Tuple.Create("treeMedium",new Vector2(1f,10.375f));
            yield return Tuple.Create("shrub",new Vector2(8.75f,8.125f));yield return Tuple.Create("rock",new Vector2(8.125f,9.375f));
            yield return Tuple.Create("shrub",new Vector2(9.5f,1.375f));yield return Tuple.Create("rock",new Vector2(8.375f,2f));
            if(landscapeVersion>=2)
            {
                // Separate upper/lower islands lean into the low bank props without filling the central sight line.
                yield return Tuple.Create("treeMedium",new Vector2(9.875f,8.375f));yield return Tuple.Create("shrub",new Vector2(10.6875f,7.8125f));
                yield return Tuple.Create("treeMedium",new Vector2(10.625f,.625f));yield return Tuple.Create("shrub",new Vector2(11.125f,.4375f));
            }
        }

        private static IEnumerable<Tuple<string,Vector2>> FineBridgeBackdropSlots(RectInt bounds, int landscapeVersion)
        {
            foreach(var slot in BackdropSlots(bounds))
                if(!(slot.Item2.x>bounds.xMax+.5f&&slot.Item2.y>.5f&&slot.Item2.y<12.5f))yield return slot;
            var x=bounds.xMax+1.75f;
            yield return Tuple.Create("shrub",new Vector2(x,2.125f));yield return Tuple.Create("rock",new Vector2(x+.25f,8.625f));
            yield return Tuple.Create("shrub",new Vector2(x-.375f,10.875f));
            if(landscapeVersion>=2)
            {
                yield return Tuple.Create("treeMedium",new Vector2(bounds.xMax+1.75f,10.375f));
                yield return Tuple.Create("treeMedium",new Vector2(bounds.xMax+3.125f,1.375f));
            }
        }

        private static StudyASoilContourPolygon[] ReadFinePolygons(StudyABlockAssembly assembly, StudyABlockSceneSettings settings)
        {
            var polygons = new List<StudyASoilContourPolygon>();
            foreach (var block in assembly.Blocks)
            {
                if (block.Definition.Kind == StudyABlockKind.BridgeBank) throw new ArgumentException("Fine soil prototype currently supports Plaza/ForestPath; bridge-bank requires its own authored outline.");
                if (block.Definition.WaterCells.Count != 0 || block.Definition.BridgeCells.Count != 0 || block.Definition.BridgeFloorCells.Count != 0 || block.Definition.StoneCells.Count != 0)
                    throw new ArgumentException("Fine soil cannot cover authored Water/BridgeFloor/Stone masks; disable Fine or provide a dedicated compatible contour renderer.");
                var prefab = Template(settings,block.Definition.Kind);
                if (prefab == null) { polygons.Add(StudyASoilContourRenderer.DefaultPolygon(block.Definition,block.Origin)); continue; }
                var controls = prefab.GetComponents<StudyASoilContour>();
                if (controls.Length != 1) throw new ArgumentException("Enabled fine soil requires one root StudyASoilContour in the supplied Native Prefab: " + prefab.name);
                var control = controls[0];
                var polygon = new StudyASoilContourPolygon { BlockId=block.Definition.Id, Origin=block.Origin, Extent=block.Definition.Extent, PixelStep=control.PixelStep, Points=control.Points?.ToArray() };
                var errors = StudyASoilContourRenderer.ValidatePolygon(polygon); if (errors.Count>0) throw new ArgumentException(string.Join("; ",errors));
                // An opt-in cannot silently erase custom authored soil tiles/objects/styles.
                var soil = prefab.transform.Find("Grid/SoilTilemap").GetComponent<Tilemap>();
                if (soil.color != Color.white) throw new ArgumentException("Fine soil cannot flatten authored SoilTilemap tint; disable Fine or use native white tint.");
                foreach (var cell in soil.cellBounds.allPositionsWithin)
                {
                    if (!soil.HasTile(cell)) continue;
                    var tile=soil.GetTile(cell) as Tile;
                    if (tile==null || tile.GetType()!=typeof(Tile) || !StudyATerrainAssets.IsApprovedGroundSprite(tile.sprite) || !tile.sprite.name.StartsWith("dirt_",StringComparison.Ordinal)
                        || tile.color!=Color.white || tile.transform!=Matrix4x4.identity || tile.gameObject!=null || soil.GetColor(cell)!=Color.white || soil.GetTransformMatrix(cell)!=Matrix4x4.identity)
                        throw new ArgumentException("Fine soil cannot replace custom authored soil tile/style/object: " + prefab.name + "/" + cell);
                }
                polygons.Add(polygon);
            }
            return polygons.ToArray();
        }

        private static void RequireFinePoiSupport(StudyABlockAssembly assembly, StudyABlockSceneSettings settings, Dictionary<string,StudyAStarterProp> props, StudyASoilMask mask)
        {
            foreach (var block in assembly.Blocks)
            {
                var prefab=Template(settings,block.Definition.Kind);
                foreach (var role in new[] { "fountain", "shop" })
                {
                    var anchors=prefab==null ? FixedSlots(block.Definition.Kind).Where(s=>s.Item1==role).Select(s=>s.Item2)
                        : prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sprite==props[role].Sprite).Select(r=>(Vector2)r.transform.position-(Vector2)prefab.transform.position);
                    foreach (var local in anchors)
                    {
                        var rect=props[role].Metrics.LocalFootprint; rect.position+=(Vector2)block.Origin+local;
                        if (!mask.ContainsRect(rect)) throw new ArgumentException("Fine soil leaves actual Native POI footprint outside clearing: "+block.Definition.Id+"/"+role);
                    }
                }
            }
        }

        private static void ApplyFineSoil(StudyASoilMask mask, StudyASoilContourPolygon[] polygons, StudyATerrainSpriteSet sprites, int seed,
            Tilemap coarse, Transform decorations, string runRoot, StudyABlockSceneReport report)
        {
            foreach (var cell in coarse.cellBounds.allPositionsWithin)
            {
                var tile=coarse.GetTile(cell) as Tile;
                if (tile?.sprite != null && tile.sprite.name.StartsWith("dirt_",StringComparison.Ordinal)) coarse.SetTile(cell,null);
            }
            var path=runRoot+"/FineSoil/global-soil.png";
            var sprite=StudyASoilContourRenderer.WriteSprite(mask,sprites,seed,path,out var selection);
            var renderer=SpriteObject(decorations,"FineSoilSurface_NoCollision",sprite,(Vector2)mask.Origin,coarse.GetComponent<TilemapRenderer>().sharedMaterial).GetComponent<SpriteRenderer>();
            renderer.sortingLayerName="MapGround"; renderer.sortingOrder=10;
            report.FineSoilPolygons=polygons; report.FineSoilPixelsPerUnit=64; report.FineSoilOpaquePixelCount=mask.Pixels.Count(p=>p==255);
            report.FineSoilMaskSha256=StudyASoilContourRenderer.Hash(mask.Pixels); report.FineSoilTexturePath=path; report.FineSoilVariantSelectionHash=selection;
            report.FineSoilRecipe="Native GroundR2 soil64 texels copied1:1; global cell-seeded variant; alpha0/255 at8/16pixel step-block center; edge inclusive; Texture2D/QA mask bottom-origin. Coarse Soil is independent structural authoring metadata, not visible outline; Road conservatively protects the actor route. Mainroute/Spawn/all3 seam lanes use actual actor .48x.55 offset .22; runtime collision remains structural Walk/MapCollision.";
        }

        private static IEnumerable<Tuple<Vector2Int,Vector2Int,Vector2>> FineBoundarySamples(StudyASoilMask mask)
        {
            const int step=8;
            for (var y=0;y<mask.Height;y+=step) for (var x=0;x<mask.Width;x+=step)
            {
                if (mask.Pixels[y*mask.Width+x]==0) continue;
                foreach (var direction in Directions)
                {
                    if ((direction.x==0 ? x : y)%32!=0) continue;
                    var nx=x+direction.x*step; var ny=y+direction.y*step;
                    if (nx>=0 && ny>=0 && nx<mask.Width && ny<mask.Height && mask.Pixels[ny*mask.Width+nx]!=0) continue;
                    var center=(Vector2)mask.Origin+new Vector2((x+step*.5f)/64f,(y+step*.5f)/64f);
                    yield return Tuple.Create(new Vector2Int(x,y),direction,center+(Vector2)direction*(step*.5f/64f));
                }
            }
        }

        private static void BuildForestDetails(StudyABlockAssembly assembly, int seed, StudyAForestDetailSpriteSet sprites,
            Dictionary<string, StudyAStarterProp> starter, Transform decorations, Transform obstacles, Material material, StudyABlockSceneReport report, StudyASoilMask fineMask = null, StudyABridgeRiverFinePlan fineBridgePlan = null, int bridgeLandscapeVersion = 0, int artFinishVersion = 0)
        {
            var groundRoot = Child(decorations, "ForestDetails"); var bodyRoot = Child(obstacles, "ForestDetailStumps");
            Physics2D.SyncTransforms();
            var bodies = obstacles.GetComponentsInChildren<BoxCollider2D>().Select(c => new Rect(c.bounds.min, c.bounds.size)).ToList();
            var nativeY = new HashSet<int>(obstacles.GetComponentsInChildren<SpriteRenderer>().Select(r => Mathf.RoundToInt(r.transform.position.y * 64f)));
            var detailY = new HashSet<int>(); var groundVisuals = new List<Tuple<int, Rect>>(); var selected = new List<string>(); var roles = new List<string>(); var paths = new List<string>();
            var anchors = new List<Vector2>(); var footprints = new List<Rect>(); var detailBodies = new List<Rect>();
            var number = 0;
            StudyAForestDetailProp Pick(string role, string id)
            {
                var variants = sprites.RoleDefs[role].OrderBy(p => p.Metrics.Name, StringComparer.Ordinal).ToArray();
                return variants[StudyATerrainTileSelector.SelectVariant(unchecked(seed ^ StableInt("detail/" + id)), Vector2Int.zero, variants.Length)];
            }
            bool Place(StudyAForestDetailProp prop, string id, Vector2 position, bool stepping = false)
            {
                var m = prop.Metrics;
                // Keep the native canvas origin on a pixel; authored half-pixel X pivots stay intact.
                position = (new Vector2(Mathf.Round(position.x * 64f - m.PixelPivot.x), Mathf.Round(position.y * 64f - m.PixelPivot.y)) + m.PixelPivot) / 64f;
                var yPixel = Mathf.RoundToInt(position.y * 64f);
                if (m.ColliderEnabled && yPixel % 2 == 0) yPixel++;
                if (m.ColliderEnabled) while (detailY.Contains(yPixel) || nativeY.Contains(yPixel)) yPixel += 2;
                position.y = yPixel / 64f;
                var visual = m.LocalVisualBounds; visual.position += position;
                // Ground sprites need a tie break only where their visible bounds overlap, keeping edge clusters local.
                while (!m.ColliderEnabled && groundVisuals.Any(v => v.Item1 == yPixel && v.Item2.Overlaps(visual)))
                { yPixel++; position.y = yPixel / 64f; visual = m.LocalVisualBounds; visual.position += position; }
                var footprint = m.LocalFootprint; footprint.position += position;
                if (!Contains(new RectInt(0, 0, fineBridgePlan != null ? fineBridgePlan.Width / fineBridgePlan.PixelsPerUnit : Width, Height), visual)) return false;
                if (fineBridgePlan != null ? fineBridgePlan.WaterIntersectsRect(visual) || fineBridgePlan.DeckBounds.Any(d=>d.Overlaps(visual)) : assembly.WaterCells.Concat(assembly.BridgeFloorCells).Any(c => visual.Overlaps(new Rect((Vector2)c, Vector2.one)))) return false;
                if (!stepping && (fineMask == null ? assembly.RoadCells.Any(c => visual.Overlaps(new Rect((Vector2)c, Vector2.one))) :
                    assembly.MainRoutes.Any(route => StudyASoilContourRenderer.Samples(route, 32).Any(foot => visual.Overlaps(StudyASoilContourRenderer.ActorRect(foot)))))) return false;
                if (stepping && (fineMask == null ? Cells(footprint).Any(c => !assembly.SoilCells.Contains(c)) : !fineMask.ContainsRect(footprint))) return false;
                if (bodies.Any(b => b.Overlaps(footprint))) return false;
                var body = m.LocalCollider; body.position += position;
                if (m.ColliderEnabled)
                {
                    if (fineBridgePlan != null ? !fineBridgePlan.IsFreeRect(footprint) : Cells(footprint).Any(c => !assembly.WalkCells.Contains(c))) return false;
                    if (body.Overlaps(new Rect(assembly.Spawn + new Vector2(-0.24f, -0.055f), new Vector2(0.48f, 0.55f)))) return false;
                    if (assembly.RoadCells.Any(c => body.Overlaps(new Rect((Vector2)c, Vector2.one))) || bodies.Any(b => b.Overlaps(body))) return false;
                }
                var go = SpriteObject(m.ColliderEnabled ? bodyRoot.transform : groundRoot.transform, "detail_" + m.Role + "_" + (number++).ToString("000"), prop.Sprite, position, material);
                if (m.ColliderEnabled)
                {
                    var min = Vector2Int.FloorToInt(footprint.min); var max = Vector2Int.CeilToInt(footprint.max);
                    ConfigureCollision(go, max - min, min - Vector2Int.FloorToInt(position), m.LocalCollider.size, m.LocalCollider.center);
                    bodies.Add(body); detailBodies.Add(body);
                }
                else go.GetComponent<SpriteRenderer>().sortingLayerName = "MapDetail";
                if (m.ColliderEnabled) detailY.Add(yPixel); else groundVisuals.Add(Tuple.Create(yPixel, visual));
                anchors.Add(position); footprints.Add(footprint); roles.Add(m.Role); paths.Add(m.DerivedPath);
                selected.Add(id + "=" + m.Name + "@" + position.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + position.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
            IEnumerable<Vector2Int> Cells(Rect rect)
            {
                for (var y = Mathf.FloorToInt(rect.yMin); y < Mathf.CeilToInt(rect.yMax); y++)
                    for (var x = Mathf.FloorToInt(rect.xMin); x < Mathf.CeilToInt(rect.xMax); x++) yield return new Vector2Int(x, y);
            }
            Vector2 NearestRoute(Vector2 point)
            {
                var closest = assembly.Spawn; var distance = float.MaxValue;
                foreach (var route in assembly.MainRoutes) for (var i = 0; i < route.Count; i++)
                {
                    var a = route[Math.Max(0, i - 1)]; var b = route[i]; var delta = b - a;
                    var t = delta.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
                    var p = a + t * delta; var d = (point - p).sqrMagnitude;
                    if (d < distance) { closest = p; distance = d; }
                }
                return closest;
            }
            // POI branches follow actual authored Sprite anchors, including saved Native inputs.
            foreach (var role in new[] { "fountain", "shop" })
            {
                var pois = obstacles.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite == starter[role].Sprite)
                    .OrderBy(r => r.transform.position.x).ThenBy(r => r.transform.position.y).ToArray();
                for (var n = 0; n < pois.Length; n++)
                {
                    var poi = (Vector2)pois[n].transform.position; var end = poi + new Vector2(0f, -0.625f); var start = NearestRoute(end);
                    var count = Mathf.Clamp(Mathf.FloorToInt(Vector2.Distance(start, end) / 0.5f), 2, 6);
                    for (var i = 0; artFinishVersion == 0 && i < count; i++)
                    {
                        var id = role + n + "/stone" + i; var t = (i + 0.85f) / (count + 0.15f);
                        var p = Vector2.Lerp(start, end, t); p.x += (i % 2 == 0 ? -0.0625f : 0.046875f);
                        Place(Pick("stepping", id), id, p, true);
                    }
                    var offsets = role == "fountain"
                        ? new[] { new Vector2(-1.1875f, -0.25f), new Vector2(-1.046875f, -0.6875f), new Vector2(1.0625f, -0.5625f) }
                        : new[] { new Vector2(-1.5f, -0.453125f), new Vector2(1.59375f, -0.5f), new Vector2(1.78125f, -1.0625f) };
                    for (var i = 0; i < offsets.Length; i++) { var id = role + n + "/flower" + i; Place(Pick("flower", id), id, poi + offsets[i]); }
                }
            }
            if (artFinishVersion == 1)
            {
                // One curved approach to the plaza facilities, derived from actual moved anchors.
                // The accepted ground-only stepping sprites retain their native texels and scale.
                var fountain = obstacles.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r => r.sprite == starter["fountain"].Sprite);
                var shop = obstacles.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r => r.sprite == starter["shop"].Sprite);
                if (fountain != null && shop != null)
                {
                    var end = ((Vector2)fountain.transform.position + (Vector2)shop.transform.position) * .5f + new Vector2(0f,-.625f);
                    var start = NearestRoute(end); var control = Vector2.Lerp(start,end,.5f) + Vector2.left * .625f;
                    var count = Mathf.Clamp(Mathf.FloorToInt(Vector2.Distance(start,end)/.5f),3,6);
                    for (var i=0;i<count;i++)
                    {
                        var t = (i+.8f)/(count+.1f); var p = (1-t)*(1-t)*start + 2*(1-t)*t*control + t*t*end;
                        var id = "plaza/shared-curved-approach/stone" + i; Place(Pick("stepping",id),id,p,true);
                    }
                }
            }
            foreach (var block in assembly.Blocks)
            {
                if (block.Definition.Kind != StudyABlockKind.Plaza && block.Definition.Kind != StudyABlockKind.ForestPath) continue;
                var id = block.Definition.Id + "/stump";
                var local = block.Definition.Kind == StudyABlockKind.Plaza ? new Vector2(6.03125f, 2.953125f) : new Vector2(5.25f, 2.984375f);
                Place(Pick("stump", id), id, (Vector2)block.Origin + local);
                var flowers = block.Definition.Kind == StudyABlockKind.Plaza
                    ? new[] { new Vector2(1.9375f, 7.078125f), new Vector2(3.125f, 7.109375f) }
                    : new[] { new Vector2(0.875f, 7.078125f), new Vector2(6.75f, 7.171875f) };
                for (var i = 0; i < flowers.Length; i++) { id = block.Definition.Id + "/edgeflower" + i; Place(Pick("flower", id), id, (Vector2)block.Origin + flowers[i]); }
            }
            if(fineBridgePlan!=null && bridgeLandscapeVersion>=2)
            {
                foreach(var block in assembly.Blocks.Where(b=>b.Definition.Kind==StudyABlockKind.BridgeBank))
                {
                    var groups=new[]{new Vector2(9.375f,7.6875f),new Vector2(9.6875f,2.6875f)};
                    for(var n=0;n<groups.Length;n++)
                    {
                        for(var i=0;i<2;i++)
                        {
                            var id=block.Definition.Id+"/east-cohort"+n+"/flower"+i;
                            Place(Pick("flower",id),id,(Vector2)block.Origin+groups[n]+new Vector2(i*.40625f,i==0?0f:.171875f));
                        }
                        for(var i=0;i<3;i++)
                        {
                            var id=block.Definition.Id+"/east-cohort"+n+"/grass"+i;
                            Place(Pick("grass",id),id,(Vector2)block.Origin+groups[n]+new Vector2(.5625f+i*.203125f,-.21875f+(i%2)*.125f));
                        }
                    }
                }
            }
            // Short, uneven clusters on the grass side of the global soil outline; no centerline flower rhythm.
            if (fineMask == null) foreach (var cell in assembly.SoilCells.OrderBy(c => c.y).ThenBy(c => c.x))
            {
                foreach (var dir in Directions)
                {
                    if (assembly.SoilCells.Contains(cell + dir)) continue;
                    var id = "edge/" + cell.x + "," + cell.y + "/" + dir.x + "," + dir.y;
                    if (StudyATerrainTileSelector.SelectVariant(unchecked(seed ^ StableInt(id)), cell, 5) >= 2) continue;
                    var count = 1 + StudyATerrainTileSelector.SelectVariant(unchecked(seed ^ StableInt("cluster/" + id)), cell, 3);
                    for (var i = 0; i < count; i++)
                    {
                        var item = id + "/grass" + i; var prop = Pick("grass", item); var v = prop.Metrics.LocalVisualBounds;
                        var tangent = new Vector2(-dir.y, dir.x); var p = (Vector2)cell + Vector2.one * 0.5f + tangent * ((i - 0.65f) * 0.21875f);
                        if (dir.x > 0) p.x = cell.x + 1 - v.xMin + 0.015625f;
                        if (dir.x < 0) p.x = cell.x - v.xMax - 0.015625f;
                        if (dir.y > 0) p.y = cell.y + 1 - v.yMin + 0.015625f;
                        if (dir.y < 0) p.y = cell.y - v.yMax - 0.015625f;
                        Place(prop, item, p);
                    }
                }
            }
            if (fineMask != null)
            {
                // Sparse low-art clusters follow the actual8px/16px contour, not the conservative macro soil.
                foreach (var sample in FineBoundarySamples(fineMask))
                {
                    var id = "fine-edge/" + sample.Item1.x + "," + sample.Item1.y + "/" + sample.Item2.x + "," + sample.Item2.y;
                    if (StudyATerrainTileSelector.SelectVariant(unchecked(seed ^ StableInt(id)), sample.Item1, 5) >= 2) continue;
                    var count = 1 + StudyATerrainTileSelector.SelectVariant(unchecked(seed ^ StableInt("cluster/"+id)), sample.Item1, 2);
                    for (var i = 0; i < count; i++)
                    {
                        var item = id + "/grass" + i; var prop = Pick("grass",item); var bounds = prop.Metrics.LocalVisualBounds;
                        var normal = sample.Item2; var tangent = new Vector2(-normal.y,normal.x);
                        var position = sample.Item3 + tangent * ((i-.5f)*.21875f);
                        // Low tufts can straddle the bank slightly; no route-foot coverage or POI front coverage.
                        if (normal.x > 0) position.x -= bounds.xMin + .0625f;
                        if (normal.x < 0) position.x -= bounds.xMax - .0625f;
                        if (normal.y > 0) position.y -= bounds.yMin + .0625f;
                        if (normal.y < 0) position.y -= bounds.yMax - .0625f;
                        var visual = new Rect(bounds.position+position,bounds.size);
                        var poiFront = obstacles.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.sprite==starter["shop"].Sprite || r.sprite==starter["fountain"].Sprite)
                            .Any(r=>visual.Overlaps(new Rect((Vector2)r.transform.position+new Vector2(-1.25f,-.75f),new Vector2(2.5f,1f))));
                        if (!poiFront) Place(prop,item,position);
                    }
                }
            }
            report.ForestDetailSelections = selected.ToArray(); report.ForestDetailSelectionHash = Hash(string.Join("\n", selected));
            report.ForestDetailRoles = roles.ToArray(); report.ForestDetailPaths = paths.ToArray(); report.ForestDetailAnchors = anchors.ToArray();
            report.ForestDetailFootprints = footprints.ToArray(); report.ForestDetailColliderBounds = detailBodies.ToArray();
        }

        private static void BuildForestBackdrop(StudyABlockAssembly assembly, Dictionary<string, StudyAStarterProp> props, Transform parent, Material material, StudyABlockSceneReport report, bool fineBridge = false, int bridgeLandscapeVersion = 0)
        {
            var root = Child(parent, "ForestBackdrop");
            var footprints = new List<Rect>(); var bodies = new List<Rect>(); var anchors = new List<Vector2>(); var selections = new List<string>();
            Physics2D.SyncTransforms();
            var existingBodies = parent.GetComponentsInChildren<BoxCollider2D>().Select(c => new Rect(c.bounds.min, c.bounds.size)).ToList();
            var index = 0;
            foreach (var slot in fineBridge ? FineBridgeBackdropSlots(assembly.Bounds, bridgeLandscapeVersion) : BackdropSlots(assembly.Bounds))
            {
                var prop = props[slot.Item1]; var footprint = prop.Metrics.LocalFootprint; footprint.position += slot.Item2;
                var body = prop.Metrics.LocalCollider; body.position += slot.Item2;
                if (assembly.WalkCells.Any(c => footprint.Overlaps(new Rect((Vector2)c, Vector2.one))))
                    throw new InvalidDataException("Backdrop足元が歩行領域へ侵入: " + slot.Item2);
                // Water ports are non-walking connections; keep their roots and visible deck clear too.
                if (assembly.WaterCells.Concat(assembly.BridgeFloorCells).Any(c => footprint.Overlaps(new Rect((Vector2)c, Vector2.one)))) continue;
                if (bodies.Concat(existingBodies).Any(b => b.Overlaps(body))) throw new InvalidDataException("Backdrop Collision重複: " + slot.Item2);
                CreateProp(root.transform, slot.Item1 + "_backdrop_" + (index++).ToString("00"), prop, slot.Item2, material);
                footprints.Add(footprint); bodies.Add(body); anchors.Add(slot.Item2);
                selections.Add(slot.Item1 + "@" + slot.Item2.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + slot.Item2.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            report.BackdropFootprints = footprints.ToArray(); report.BackdropColliderBounds = bodies.ToArray(); report.BackdropAnchors = anchors.ToArray();
            report.BackdropSelections = selections.ToArray(); report.BackdropSelectionHash = Hash(string.Join("\n", selections));
        }

        private static IEnumerable<Tuple<string, Vector2>> BackdropSlots(RectInt bounds)
        {
            // Authored perimeter clusters; roots remain outside walking land while canopies continue beyond the viewport.
            var bottom = new[] { new Vector2(-0.625f,-0.875f), new Vector2(1.125f,-0.5f), new Vector2(2.75f,-1.125f), new Vector2(4.5f,-0.625f), new Vector2(6.5f,-1.125f), new Vector2(8f,-0.75f), new Vector2(10f,-1.25f), new Vector2(11.5f,-0.625f), new Vector2(13f,-1f), new Vector2(15f,-0.5f), new Vector2(16.75f,-1.125f), new Vector2(18.375f,-0.5f), new Vector2(20.25f,-1f), new Vector2(22f,-0.625f), new Vector2(24f,-0.875f) };
            for (var i = 0; i < bottom.Length; i++) yield return Tuple.Create(i % 2 == 0 ? "treeLarge" : "treeMedium", bottom[i]);
            var sideY = new[] { 1.5f, 3.125f, 5.25f, 7.5f, 9.25f, 11.375f };
            var left = bounds.xMin - 2.125f; var right = bounds.xMax + 2.125f;
            foreach (var x in new[] { left, right })
                for (var i = 0; i < sideY.Length; i++) yield return Tuple.Create(i % 2 == 0 ? "treeLarge" : "treeMedium", new Vector2(x + (i % 2 == 0 ? 0f : 0.25f), sideY[i]));
            // Single-block studies have wider padding; fill it without changing their authored terrain or ports.
            if (left > 1.5f) foreach (var x in new[] { -0.75f, left - 2.5f })
                for (var i = 0; i < sideY.Length; i++) yield return Tuple.Create(i % 2 == 0 ? "treeLarge" : "treeMedium", new Vector2(x, sideY[i] + 0.375f));
            if (right < 22.5f) foreach (var x in new[] { 24.125f, right + 2.5f })
                for (var i = 0; i < sideY.Length; i++) yield return Tuple.Create(i % 2 == 0 ? "treeLarge" : "treeMedium", new Vector2(x, sideY[i] + 0.375f));
            var top = new[] { new Vector2(-0.75f,13.75f), new Vector2(2.125f,14.125f), new Vector2(4f,13.625f), new Vector2(6.25f,14.375f), new Vector2(8f,14f), new Vector2(9.625f,13.625f), new Vector2(11.75f,14.125f), new Vector2(13.5f,13.625f), new Vector2(15.625f,14.25f), new Vector2(17.5f,13.625f), new Vector2(19.5f,14.125f), new Vector2(21.25f,13.75f), new Vector2(23.125f,14.125f), new Vector2(24.5f,13.625f) };
            for (var i = 0; i < top.Length; i++) yield return Tuple.Create(i % 2 == 0 ? "treeLarge" : "treeMedium", top[i]);
        }

        private static void BridgePosts(StudyAPlacedBlock block, Sprite sprite, Transform parent, Material material)
        {
            var bounds = Bounds(block.Definition.BridgeFloorCells.Select(c => c + block.Origin));
            var n = 0;
            foreach (var x in new[] { bounds.xMin - 0.125f, bounds.xMax + 0.125f }) foreach (var y in new[] { bounds.yMin - 0.25f, bounds.yMax })
            {
                var go = SpriteObject(parent, block.Definition.Id + "_diagnostic_bridge_post_" + n++, sprite, new Vector2(x, y), material);
                ConfigureCollision(go, Vector2Int.one, Vector2Int.zero, new Vector2(0.25f, 0.25f), new Vector2(0f, 0.125f));
            }
        }

        private static Vector2[] OrderedRoute(StudyABlockAssembly assembly)
        {
            var points = new List<Vector2>();
            foreach (var route in assembly.MainRoutes) points.AddRange(route);
            return points.ToArray();
        }

        private static void ValidateActualColliders(GameObject map, Vector2[] route, HashSet<Vector2Int> road)
        {
            var colliders = map.GetComponentsInChildren<Collider2D>().Where(c => c.enabled && !c.isTrigger).ToArray();
            for (var i = 0; i < route.Length; i++)
            {
                var previous = route[Math.Max(0, i - 1)]; var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(previous, route[i]) * 16f));
                for (var step = 0; step <= steps; step++)
                {
                    var point = Vector2.Lerp(previous, route[i], (float)step / steps);
                    var foot = new Bounds(point + new Vector2(0f, 0.22f), new Vector3(0.48f, 0.55f, 0f));
                    // Composite AABBには開口も含まれるため、BoxだけactualBoundsで確認。TilemapはCell別に検査。
                    foreach (var collider in colliders.OfType<BoxCollider2D>())
                        if (collider.bounds.Intersects(foot)) throw new InvalidDataException("主導線にprop足元Collider: " + collider.transform.parent.name);
                    var tiles = map.transform.Find("Grid/CollisionTilemap").GetComponent<Tilemap>();
                    for (var y = Mathf.FloorToInt(foot.min.y); y < Mathf.CeilToInt(foot.max.y); y++) for (var x = Mathf.FloorToInt(foot.min.x); x < Mathf.CeilToInt(foot.max.x); x++)
                        if (tiles.HasTile(new Vector3Int(x, y, 0))) throw new InvalidDataException("主導線の足元がCollisionTileへ接触: " + point);
                }
            }
        }

        private static Dictionary<string, Tile> TilesForRun(string root, StudyATerrainSpriteSet terrainSprites, StudyABridgeRiverSpriteSet bridgeArt = null)
        {
            var result = new Dictionary<string, Tile>();
            foreach (var entry in terrainSprites.ByName.Where(e => e.Value != terrainSprites.Hero).OrderBy(e => e.Key))
            {
                var tile = ScriptableObject.CreateInstance<Tile>(); tile.name = "prototype_" + entry.Key; tile.sprite = entry.Value; tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, root + "/Tiles/" + tile.name + ".asset"); result.Add(entry.Value.name, tile);
            }
            result.Add("grass", result[terrainSprites.GrassVariants[0].name]); result.Add("soil", result[terrainSprites.SoilVariants[0].name]);
            // Only unsupported water/bridge pieces retain an explicit diagnostic representation.
            foreach (var key in new[] { "water", "deck", "collision", "post" })
            {
                Sprite sprite = key == "water" && bridgeArt != null ? bridgeArt.Water : null;
                if (key != "collision" && sprite == null)
                {
                    var width = key == "post" ? 16 : 64; var height = key == "post" ? 48 : 64;
                    var pixels = new Color32[width * height];
                    for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                    {
                        var color = key == "water" ? new Color32(57, 117, 147, 255) : new Color32(130, 85, 47, 255);
                        if (key == "deck" && (y % 16 == 0 || x == 0)) color = new Color32(81, 53, 32, 255);
                        if (key == "water" && y % 16 == 7 && x % 16 < 8) color = new Color32(81, 147, 167, 255);
                        pixels[y * width + x] = color;
                    }
                    var texture = new Texture2D(width, height, TextureFormat.RGBA32, false); texture.SetPixels32(pixels); texture.Apply();
                    var path = root + "/Tiles/diagnostic_" + key + ".png";
                    try { File.WriteAllBytes(ProjectPath(path), texture.EncodeToPNG()); } finally { Object.DestroyImmediate(texture); }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 64; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
                    var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings); textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                    textureSettings.spriteAlignment = (int)SpriteAlignment.Custom; textureSettings.spritePivot = key == "post" ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f); importer.SetTextureSettings(textureSettings); importer.SaveAndReimport();
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                var tile = ScriptableObject.CreateInstance<Tile>(); tile.sprite = sprite; tile.colliderType = key == "collision" ? Tile.ColliderType.Grid : Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, root + "/Tiles/diagnostic_" + key + ".asset"); result.Add(key, tile);
            }
            return result;
        }

        private static void DiagnosticLabel(Transform parent, string caseName)
        {
            var go = Child(parent, "StructuralDiagnosticLabel"); go.transform.localPosition = new Vector3(0.25f, 15.8f, 0f);
            var text = go.AddComponent<TextMesh>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "TERRAIN PROTOTYPE  |  " + caseName + "  |  STATIC HERO";
            text.fontSize = 32; text.characterSize = 0.12f; text.anchor = TextAnchor.UpperLeft; text.color = new Color32(255, 235, 155, 255);
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = text.font.material; renderer.sortingLayerName = "MapForeground"; renderer.sortingOrder = 100;
        }

        private static Material MaterialForRun(string root)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ForestRestartAssetBuilder.ArtRoot + "/ForestRestartUnlit.mat"); if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Sprite Unlit shaderがありません。");
            var material = new Material(shader); AssetDatabase.CreateAsset(material, root + "/StudyABlockUnlit.mat"); return material;
        }

        private static UnityEngine.Tilemaps.Tilemap Tilemap(Transform parent, string name, string layer, int order, Material material, bool collision = false)
        {
            var go = Child(parent, name); var tilemap = go.AddComponent<UnityEngine.Tilemaps.Tilemap>(); var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = layer; renderer.sortingOrder = order; renderer.sharedMaterial = material;
            renderer.mode = name == "ForegroundTilemap" ? TilemapRenderer.Mode.Individual : TilemapRenderer.Mode.Chunk;
            if (collision)
            {
                go.layer = LayerMask.NameToLayer("MapCollision"); renderer.enabled = false; go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                go.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
                go.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            return tilemap;
        }

        private static GameObject SpriteObject(Transform parent, string name, Sprite sprite, Vector2 position, Material material)
        {
            var go = Child(parent, name); go.transform.localPosition = position; var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
            renderer.sortingLayerName = "WorldObjects"; renderer.sortingOrder = 0; renderer.spriteSortPoint = SpriteSortPoint.Pivot; return go;
        }
        private static void ConfigureCollision(GameObject root, Vector2Int footprint, Vector2Int offset, Vector2 size, Vector2 center)
        {
            root.AddComponent<MapObstacleVisualMarker>().Configure(footprint, offset, MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Box, size, center);
            var body = Child(root.transform, "CollisionBody"); body.layer = LayerMask.NameToLayer("MapCollision"); body.transform.localPosition = center;
            var collider = body.AddComponent<BoxCollider2D>(); collider.size = size; collider.offset = Vector2.zero;
        }
        private static void Socket(Transform parent, string id, MapSocketKind kind, Vector2 position)
        {
            var go = Child(parent, id); go.layer = LayerMask.NameToLayer("MapInteraction"); go.transform.localPosition = position;
            go.AddComponent<MapSocketMarker>().Configure(kind, Vector2.one, MapSocketFacing.Any, "study-a-structural", id);
        }
        private static StudyABlockDefinition Preset(StudyABlockKind kind, int seed, bool fine = false)
        {
            var definition = kind == StudyABlockKind.Plaza ? StudyABlockPresets.Plaza(seed) : kind == StudyABlockKind.ForestPath ? StudyABlockPresets.ForestPath(seed) : StudyABlockPresets.BridgeBank(seed);
            if (fine && kind != StudyABlockKind.BridgeBank) StudyASoilContourRenderer.ConfigureDefaultRoute(definition);
            return definition;
        }
        private static GameObject Template(StudyABlockSceneSettings settings, StudyABlockKind kind) => kind == StudyABlockKind.Plaza ? settings.PlazaPrefab : kind == StudyABlockKind.ForestPath ? settings.ForestPathPrefab : settings.BridgeBankPrefab;
        private static void AssignTemplate(StudyABlockSceneSettings settings, StudyABlockKind kind, GameObject prefab) { if (kind == StudyABlockKind.Plaza) settings.PlazaPrefab = prefab; else if (kind == StudyABlockKind.ForestPath) settings.ForestPathPrefab = prefab; else settings.BridgeBankPrefab = prefab; }
        private static void RequireValid(StudyABlockAssembly assembly) { var errors = assembly.Validate(); if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors)); }
        private static GameObject Child(Transform parent, string name) { var go = new GameObject(name); if (parent != null) go.transform.SetParent(parent, false); return go; }
        private static Vector3Int Cell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);
        private static bool Contains(Rect outer, Rect inner) => inner.width > 0f && inner.height > 0f && inner.xMin >= outer.xMin && inner.yMin >= outer.yMin && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
        private static bool Contains(RectInt outer, Rect inner) => Contains(new Rect(outer.position, outer.size), inner);
        private static IEnumerable<Vector2Int> Enumerate(RectInt bounds) { foreach (var cell in bounds.allPositionsWithin) yield return cell; }
        private static void Paint(UnityEngine.Tilemaps.Tilemap tilemap, IEnumerable<Vector2Int> cells, TileBase tile) { foreach (var cell in cells) tilemap.SetTile(Cell(cell), tile); }
        private static Vector2Int[] Sorted(IEnumerable<Vector2Int> cells) => cells.Distinct().OrderBy(c => c.y).ThenBy(c => c.x).ToArray();
        private static Rect Bounds(IEnumerable<Vector2Int> cells) { var data = cells.ToArray(); return new Rect(data.Min(c => c.x), data.Min(c => c.y), data.Max(c => c.x) - data.Min(c => c.x) + 1, data.Max(c => c.y) - data.Min(c => c.y) + 1); }
        private static int StableInt(string value) { unchecked { var hash = 2166136261u; foreach (var c in value) { hash ^= c; hash *= 16777619u; } return (int)hash; } }
        private static string Hash(string value) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2"))); }
        private static string ProjectPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/'); var parent = path.Substring(0, slash); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
        private static void EnsureCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Play/compile中には生成しません。");
            for (var i = 0; i < SceneManager.sceneCount; i++)
            { var scene = SceneManager.GetSceneAt(i); if (scene.isDirty || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("保存済みclean Sceneから実行してください。ユーザーSceneは保存しません。"); }
        }
    }
}
