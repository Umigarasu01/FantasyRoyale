using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>元画像の機械的な正規化、接続Tile、Prefab、Seed付き検証Sceneを生成する。</summary>
    public static class ForestRestartAssetBuilder
    {
        public const string ArtRoot = "Assets/Art/Map/ForestRestart";
        public const string SourceRoot = ArtRoot + "/Source";
        public const string SpriteRoot = ArtRoot + "/Sprites";
        public const string QaRoot = ArtRoot + "/QA";
        public const string TileRoot = "Assets/Data/MapAuthoring/Tiles/ForestRestart";
        public const string PrefabRoot = "Assets/Prefabs/MapAuthoring/ForestRestart";
        public const string ReferenceScenePath = "Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity";
        public const string SceneRoot = "Assets/Scenes/MapAuthoring/ForestRestart";
        public const int DefaultSeed = 20261003;
        public const float FootPivot = 0.16f;
        public const byte ObjectAlphaThreshold = 250;
        public static readonly int[] PreviewSeeds = { DefaultSeed, 20261004, 20261005, 20261006 };

        public static float GetFootPivot(string name)
        {
            var height = GbaMapSpritePostprocessor.GetSpriteHeight(name);
            return Mathf.Round(height * FootPivot) / height;
        }

        [Serializable]
        public sealed class SeedQa
        {
            public int seed;
            public string layoutHash;
            public bool logicalValid;
            public int reachableCells;
            public int roadCells;
            public int obstacleCount;
            public int waterCells;
            public int bridgeCells;
            public int cliffCells;
            public int landmarkCandidates;
            public bool includesLandmarkCandidates;
            public string[] errors;
            public string scene;
            public bool sceneValidated;
            public bool sceneValid;
            public string[] sceneIssues;
        }

        [Serializable]
        public sealed class KitQa
        {
            public string schema = "fantasyroyale.forest-composition.qa.v2";
            public string note = "Editor生成検証。Sceneが配置の正本。店と泉は構図検証候補でGameplay機能を持たない。画像の正式採用は未承認。";
            public SeedQa[] seeds;
        }

        public static IEnumerable<string> ExpectedSpriteNames()
        {
            for (var i = 0; i < 8; i++) yield return $"grass_{i:00}";
            for (var mask = 0; mask < 16; mask++)
                for (var variant = 0; variant < 4; variant++)
                    yield return $"road_{mask:x2}_{variant:00}";
            yield return "oak";
            yield return "grove";
            yield return "undergrowth";
            yield return "hero";
        }

        [MenuItem("FantasyRoyale/Map Authoring/Rebuild Forest Restart Kit")]
        public static void BuildCompleteKit()
        {
            BuildAssets();
            var results = new List<SeedQa>();
            for (var index = 0; index < 40; index++)
            {
                var seed = DefaultSeed + index;
                var includeLandmarks = ForestCompositionVisuals.GetSettings().includeLandmarkCandidates;
                var plan = ForestRestartLayout.Generate(seed, includeLandmarks);
                var check = ForestRestartLayout.Validate(plan);
                var row = new SeedQa
                {
                    seed = seed,
                    layoutHash = ForestRestartLayout.ComputeLayoutHash(plan),
                    logicalValid = check.IsValid,
                    reachableCells = check.ReachableCellCount,
                    roadCells = plan.RoadCells.Count,
                    obstacleCount = plan.Obstacles.Count,
                    waterCells = plan.WaterCells.Count,
                    bridgeCells = plan.BridgeCells.Count,
                    cliffCells = plan.CliffCells.Count,
                    landmarkCandidates = plan.Landmarks.Count,
                    includesLandmarkCandidates = includeLandmarks,
                    errors = check.Errors.ToArray()
                };
                if (!check.IsValid) throw new InvalidOperationException($"Seed {seed}: {string.Join(", ", row.errors)}");
                if (index < PreviewSeeds.Length)
                {
                    row.scene = $"{SceneRoot}/ForestCompositionSeed{seed}.unity";
                    BuildScene(seed, row.scene, row);
                }
                results.Add(row);
            }
            File.WriteAllText(QaRoot + "/generation-validation.json", JsonUtility.ToJson(new KitQa { seeds = results.ToArray() }, true));
            AssetDatabase.Refresh();
            Debug.Log($"Forest Restart Kit: {results.Count} Seedを検証、{PreviewSeeds.Length} Sceneを生成しました。");
        }

        public static void BuildAssets()
        {
            EnsureNoUntitledScene();
            ForestCompositionVisuals.CheckSourceAssets();
            foreach (var folder in new[] { SpriteRoot, QaRoot, TileRoot, PrefabRoot, SceneRoot }) EnsureFolder(folder);
            var grass = LoadPng(SourceRoot + "/grass-surface.png");
            var dirt = LoadPng(SourceRoot + "/dirt-surface.png");
            try
            {
                using (var grassSurface = new OwnedTexture(Resize(grass, 128, 128, false)))
                using (var dirtSurface = new OwnedTexture(Resize(dirt, 128, 128, false)))
                {
                    File.WriteAllBytes(ArtRoot + "/grass-normalized.png", grassSurface.Texture.EncodeToPNG());
                    File.WriteAllBytes(ArtRoot + "/dirt-normalized.png", dirtSurface.Texture.EncodeToPNG());
                    for (var variant = 0; variant < 8; variant++)
                        WriteTile($"grass_{variant:00}", grassSurface.Texture, variant % 4 * 32, variant / 4 * 32, -1, 0);
                    for (var mask = 0; mask < 16; mask++)
                        for (var variant = 0; variant < 4; variant++)
                            WriteTile($"road_{mask:x2}_{variant:00}", dirtSurface.Texture, variant % 2 * 32, variant / 2 * 32, mask, variant);
                }
            }
            finally { Object.DestroyImmediate(grass); Object.DestroyImmediate(dirt); }

            NormalizeObject("oak", 128, 128);
            NormalizeObject("grove", 192, 128);
            NormalizeObject("undergrowth", 96, 96);
            NormalizeObject("hero", 32, 48);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in ExpectedSpriteNames()) AssetDatabase.ImportAsset(SpriteRoot + "/" + name + ".png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            BuildTileAssets();
            var previousActive = SceneManager.GetActiveScene();
            var prefabScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(prefabScene);
                BuildPrefab("oak", new Vector2Int(4, 2), new Vector2Int(-2, -1), new Vector2(1.25f, 0.625f));
                BuildPrefab("grove", new Vector2Int(6, 2), new Vector2Int(-3, -1), new Vector2(3f, 0.9375f));
                BuildPrefab("undergrowth", new Vector2Int(3, 2), new Vector2Int(-1, -1), new Vector2(1.625f, 0.5f));
                BuildPrefab("hero", Vector2Int.one, Vector2Int.zero, Vector2.zero);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(prefabScene, true);
            }
            AssetDatabase.SaveAssets();
            ForestCompositionVisuals.BuildAssets();
        }

        [MenuItem("FantasyRoyale/Map Authoring/Generate Configured Forest Composition")]
        public static void BuildConfiguredComposition()
        {
            var settings = ForestCompositionVisuals.GetSettings();
            BuildRepresentativeSeed(settings.seed);
        }

        /// <summary>既存76素材とReference Sceneを保持し、一つのSeedだけを比較Sceneへ生成する。</summary>
        public static void BuildRepresentativeSeed(int seed)
        {
            EnsureNoUntitledScene();
            ForestCompositionVisuals.BuildAssets();
            var includeLandmarks = ForestCompositionVisuals.GetSettings().includeLandmarkCandidates;
            var plan = ForestRestartLayout.Generate(seed, includeLandmarks);
            var check = ForestRestartLayout.Validate(plan);
            var row = new SeedQa
            {
                seed = seed, layoutHash = ForestRestartLayout.ComputeLayoutHash(plan),
                logicalValid = check.IsValid, reachableCells = check.ReachableCellCount,
                roadCells = plan.RoadCells.Count, obstacleCount = plan.Obstacles.Count,
                waterCells = plan.WaterCells.Count, bridgeCells = plan.BridgeCells.Count,
                cliffCells = plan.CliffCells.Count, landmarkCandidates = plan.Landmarks.Count,
                includesLandmarkCandidates = includeLandmarks,
                errors = check.Errors.ToArray(), scene = $"{SceneRoot}/ForestCompositionSeed{seed}.unity"
            };
            if (!check.IsValid) throw new InvalidOperationException(string.Join("; ", row.errors));
            BuildScene(seed, row.scene, row);
            File.WriteAllText(QaRoot + $"/composition-seed-{seed}-validation.json",
                JsonUtility.ToJson(new KitQa { seeds = new[] { row } }, true));
            AssetDatabase.Refresh();
            Debug.Log($"Forest Composition Seed {seed}: {row.scene}");
        }

        private sealed class OwnedTexture : IDisposable
        {
            public readonly Texture2D Texture;
            public OwnedTexture(Texture2D texture) { Texture = texture; }
            public void Dispose() { Object.DestroyImmediate(Texture); }
        }

        internal static Texture2D LoadPng(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Forest Restart元画像がありません。", path);
            var result = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!result.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(result); throw new InvalidOperationException("PNGを読めません: " + path); }
            return result;
        }

        private static void NormalizeObject(string key, int width, int height)
        {
            var source = LoadPng(SourceRoot + "/" + key + ".png");
            try
            {
                using (var output = new OwnedTexture(Resize(source, width, height, true)))
                    File.WriteAllBytes(SpriteRoot + "/" + key + ".png", output.Texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(source); }
        }

        /// <summary>Nearest samplingと透明BBoxのaspect保持だけを行い、色や形状を描き直さない。</summary>
        internal static Texture2D Resize(Texture2D source, int width, int height, bool fitTransparentBounds)
        {
            var pixels = source.GetPixels32();
            var minX = 0; var minY = 0; var maxX = source.width - 1; var maxY = source.height - 1;
            if (fitTransparentBounds)
            {
                minX = source.width; minY = source.height; maxX = -1; maxY = -1;
                for (var y = 0; y < source.height; y++) for (var x = 0; x < source.width; x++)
                {
                    if (pixels[y * source.width + x].a < ObjectAlphaThreshold) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                if (maxX < minX) throw new InvalidOperationException("元画像のAlphaが全て透明です。");
            }
            var sourceWidth = maxX - minX + 1; var sourceHeight = maxY - minY + 1;
            var outputWidth = width; var outputHeight = height; var offsetX = 0; var offsetY = 0;
            if (fitTransparentBounds)
            {
                offsetY = Mathf.RoundToInt(height * FootPivot);
                var scale = Mathf.Min((width - 4f) / sourceWidth, (height - offsetY - 2f) / sourceHeight);
                outputWidth = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
                outputHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
                offsetX = (width - outputWidth) / 2;
            }
            var output = new Color32[width * height];
            for (var y = 0; y < outputHeight; y++) for (var x = 0; x < outputWidth; x++)
            {
                var sx = minX + Mathf.Min(sourceWidth - 1, Mathf.FloorToInt((x + 0.5f) * sourceWidth / outputWidth));
                var sy = minY + Mathf.Min(sourceHeight - 1, Mathf.FloorToInt((y + 0.5f) * sourceHeight / outputHeight));
                var color = pixels[sy * source.width + sx];
                if (fitTransparentBounds) color.a = color.a >= ObjectAlphaThreshold ? (byte)255 : (byte)0;
                output[(y + offsetY) * width + x + offsetX] = color;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(output); texture.Apply(); return texture;
        }

        private static void WriteTile(string name, Texture2D source, int sourceX, int sourceY, int mask, int variant)
        {
            var output = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = source.GetPixels32();
            var result = new Color32[1024];
            for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
            {
                var color = pixels[(sourceY + y) * source.width + sourceX + x];
                // 全差分の対向辺へ同じSource列を置き、組合せに依存する境界の色段差を防ぐ。
                if (mask < 0)
                {
                    if (x == 0 || x == 31) color = pixels[y * source.width];
                    if (y == 0 || y == 31) color = pixels[x];
                    if ((x == 0 || x == 31) && (y == 0 || y == 31)) color = pixels[0];
                    color.a = 255;
                }
                else
                {
                    if (x == 0 || x == 31) color = pixels[y * source.width];
                    if (y == 0 || y == 31) color = pixels[x];
                    if ((x == 0 || x == 31) && (y == 0 || y == 31)) color = pixels[0];
                }
                if (mask >= 0 && !RoadPixelIsOpaque(mask, variant, x, y)) color.a = 0;
                result[y * 32 + x] = color;
            }
            output.SetPixels32(result); output.Apply();
            File.WriteAllBytes(SpriteRoot + "/" + name + ".png", output.EncodeToPNG());
            Object.DestroyImmediate(output);
        }

        /// <summary>接続辺中央20pxと直交bitのAND角を固定し、非接続辺だけ微小な輪郭差分を付ける。</summary>
        public static bool RoadPixelIsOpaque(int mask, int variant, int x, int y)
        {
            if (mask == 15) return true;
            var n = (mask & 1) != 0; var e = (mask & 2) != 0; var s = (mask & 4) != 0; var w = (mask & 8) != 0;
            if (y >= 30 && n) return (x >= 6 && x < 26) || (x < 6 && w) || (x >= 26 && e);
            if (y < 2 && s) return (x >= 6 && x < 26) || (x < 6 && w) || (x >= 26 && e);
            if (x >= 30 && e) return (y >= 6 && y < 26) || (y < 6 && s) || (y >= 26 && n);
            if (x < 2 && w) return (y >= 6 && y < 26) || (y < 6 && s) || (y >= 26 && n);
            // 接続Collarへ触れず、露出側だけ2px刻みの決定的な4px幅の起伏を作る。
            // 方向ごとに別Hashを使い、左右上下が同じ階段を繰り返さないようにする。
            if (!w && x < ExposedRoadEdgeDepth(variant, 3, y)) return false;
            if (!e && x >= 32 - ExposedRoadEdgeDepth(variant, 1, y)) return false;
            if (!s && y < ExposedRoadEdgeDepth(variant, 2, x)) return false;
            if (!n && y >= 32 - ExposedRoadEdgeDepth(variant, 0, x)) return false;
            if (x < 6 && y < 6) return w && s;
            if (x < 6 && y >= 26) return w && n;
            if (x >= 26 && y < 6) return e && s;
            if (x >= 26 && y >= 26) return e && n;
            return true;
        }

        private static int ExposedRoadEdgeDepth(int variant, int side, int coordinate)
        {
            unchecked
            {
                var hash = (uint)(variant + 1) * 0x9e3779b9u
                           ^ (uint)(side + 1) * 0x85ebca6bu
                           ^ (uint)(coordinate / 2 + 1) * 0xc2b2ae35u;
                hash ^= hash >> 16; hash *= 0x7feb352du;
                hash ^= hash >> 15; hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return 4 + (int)(hash % 5u);
            }
        }

        private static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "/" + name + ".png");
            if (sprite == null) throw new InvalidOperationException("Sprite Import未完了: " + name);
            return sprite;
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static void BuildTileAssets()
        {
            var grass = GetOrCreateAsset<GroundVariationTile>(TileRoot + "/GrassVariation.asset");
            grass.Configure(Enumerable.Range(0, 8).Select(i => Sprite($"grass_{i:00}")).ToArray(), DefaultSeed);
            EditorUtility.SetDirty(grass);
            var road = GetOrCreateAsset<RoadConnectionRuleTile>(TileRoot + "/DirtRoad.asset");
            road.Configure("forest-restart-road", DefaultSeed); road.m_DefaultSprite = Sprite("road_0f_00");
            road.m_DefaultColliderType = Tile.ColliderType.None; road.m_TilingRules.Clear();
            var positions = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
            for (var mask = 0; mask < 16; mask++)
            {
                var rule = new RuleTile.TilingRule { m_Id = mask, m_ColliderType = Tile.ColliderType.None,
                    m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random, m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                    m_RandomTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                    m_Sprites = Enumerable.Range(0, 4).Select(i => Sprite($"road_{mask:x2}_{i:00}")).ToArray() };
                rule.m_NeighborPositions.Clear(); rule.m_Neighbors.Clear();
                for (var i = 0; i < 4; i++) { rule.m_NeighborPositions.Add(positions[i]); rule.m_Neighbors.Add((mask & 1 << i) != 0 ? RuleTile.TilingRuleOutput.Neighbor.This : RuleTile.TilingRuleOutput.Neighbor.NotThis); }
                road.m_TilingRules.Add(rule);
            }
            EditorUtility.SetDirty(road);
            var collision = GetOrCreateAsset<Tile>(TileRoot + "/Collision.asset");
            collision.sprite = null; collision.colliderType = Tile.ColliderType.Grid; EditorUtility.SetDirty(collision);
        }

        internal static Material GetMaterial()
        {
            var path = ArtRoot + "/ForestRestartUnlit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static void BuildPrefab(string key, Vector2Int footprint, Vector2Int offset, Vector2 collisionSize)
        {
            var root = new GameObject(key);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = Sprite(key); renderer.sharedMaterial = GetMaterial();
                renderer.sortingLayerName = "WorldObjects"; renderer.sortingOrder = 0; renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                var collisionOffset = key == "grove" ? new Vector2(0f, 0.15625f) : new Vector2(0f, 0.0625f);
                if (key != "hero") ConfigureCollision(root, footprint, offset, collisionSize, collisionOffset);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/" + key + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        internal static void ConfigureCollision(GameObject root, Vector2Int footprint, Vector2Int offset, Vector2 size, Vector2 collisionOffset)
        {
            var marker = root.GetComponent<MapObstacleVisualMarker>();
            if (marker == null) marker = root.AddComponent<MapObstacleVisualMarker>();
            marker.Configure(footprint, offset, MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Box, size, collisionOffset);
            var body = root.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            if (body == null) { body = new GameObject(MapObstacleVisualMarker.CollisionBodyName).transform; body.SetParent(root.transform, false); }
            body.gameObject.layer = LayerMask.NameToLayer("MapCollision"); body.localPosition = collisionOffset; body.localRotation = Quaternion.identity;
            var collider = body.GetComponent<BoxCollider2D>();
            if (collider == null) collider = body.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size; collider.offset = Vector2.zero;
        }

        public static void BuildScene(int seed, string path) => BuildScene(seed, path, null);

        private static void BuildScene(int seed, string path, SeedQa row)
        {
            EnsureNoUntitledScene();
            var plan = ForestRestartLayout.Generate(seed, ForestCompositionVisuals.GetSettings().includeLandmarkCandidates);
            var validation = ForestRestartLayout.Validate(plan);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("; ", validation.Errors));
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var previousActive = SceneManager.GetActiveScene();
            var previousTarget = SceneManager.GetSceneByPath(path);
            var wasLoaded = previousTarget.IsValid() && previousTarget.isLoaded;
            if (wasLoaded && previousTarget.isDirty)
                throw new InvalidOperationException("対象Sceneに未保存の変更があります。保存してから再生成してください: " + path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var saved = false;
            var targetClosed = false;
            try
            {
                SceneManager.SetActiveScene(scene);
                var mapRoot = new GameObject("MapRoot"); SceneManager.MoveGameObjectToScene(mapRoot, scene);
                var grid = Child(mapRoot.transform, "Grid"); grid.AddComponent<Grid>();
                var ground = MakeTilemap(grid.transform, "GroundTilemap", "MapGround", 0, false);
                var water = MakeTilemap(grid.transform, "WaterTilemap", "MapGround", 5, false);
                var terrain = MakeTilemap(grid.transform, "TerrainTilemap", "MapGround", 10, false);
                var collision = MakeTilemap(grid.transform, "CollisionTilemap", "Default", 0, true);
                MakeTilemap(grid.transform, "ForegroundTilemap", "MapForeground", 0, false);
                var decorations = Child(mapRoot.transform, "Decorations");
                var obstacles = Child(mapRoot.transform, "ObstacleVisuals");
                var props = Child(mapRoot.transform, "Props");
                var sockets = Child(mapRoot.transform, "Sockets"); sockets.layer = LayerMask.NameToLayer("MapInteraction");
                var grassTile = AssetDatabase.LoadAssetAtPath<GroundVariationTile>(TileRoot + "/GrassVariation.asset");
                var roadTile = AssetDatabase.LoadAssetAtPath<RoadConnectionRuleTile>(TileRoot + "/DirtRoad.asset");
                var collisionTile = AssetDatabase.LoadAssetAtPath<Tile>(TileRoot + "/Collision.asset");
                if (grassTile == null || roadTile == null || collisionTile == null)
                    throw new InvalidOperationException("既存Forest Restart Tileがありません。Rebuild Forest Restart Kitで生成してください。");
                foreach (var cell in plan.GroundCells) ground.SetTile(new Vector3Int(cell.x, cell.y, 0), grassTile);
                foreach (var cell in plan.WaterCells) water.SetTile(new Vector3Int(cell.x, cell.y, 0), ForestCompositionVisuals.WaterTile());
            foreach (var cell in plan.RoadCells)
                if (!plan.BridgeCells.Contains(cell)) terrain.SetTile(new Vector3Int(cell.x, cell.y, 0), roadTile);
            ForestCompositionRoadEdges.Apply(plan, terrain);
                foreach (var cell in plan.BlockedCells)
                    if (cell.x == plan.Bounds.xMin || cell.x == plan.Bounds.xMax - 1 || cell.y == plan.Bounds.yMin || cell.y == plan.Bounds.yMax - 1
                        || (plan.WaterCells.Contains(cell) && !plan.BridgeCells.Contains(cell)) || plan.CliffCells.Contains(cell))
                        collision.SetTile(new Vector3Int(cell.x, cell.y, 0), collisionTile);
                var index = 0;
                foreach (var placement in plan.Obstacles)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/" + placement.AssetKey + ".prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, obstacles.transform);
                    instance.name = placement.AssetKey == "oak" ? $"prop_tree_medium_restart_{index++:000}" : $"{placement.AssetKey}_{index++:000}";
                    instance.transform.localPosition = placement.Position;
                    var rect = placement.Footprint;
                    var anchor = Vector2Int.FloorToInt(placement.Position);
                    var footprintStart = Vector2Int.FloorToInt(rect.min);
                    var footprintEnd = Vector2Int.CeilToInt(rect.max);
                    ConfigureCollision(instance, footprintEnd - footprintStart, footprintStart - anchor, placement.CollisionRect.size, placement.CollisionRect.center - placement.Position);
                }
                ForestCompositionVisuals.Populate(plan, decorations.transform, obstacles.transform);
                CreateSocket(sockets.transform, "player-start", MapSocketKind.PlayerStart, plan.Spawn);
                for (var i = 0; i < plan.EventDestinations.Count; i++) CreateSocket(sockets.transform, $"forest-restart-event-{i:00}", MapSocketKind.Event, plan.EventDestinations[i]);
                for (var i = 0; i < plan.Entrances.Count; i++) CreateSocket(sockets.transform, $"forest-restart-entrance-{i:00}", MapSocketKind.Landmark, plan.Entrances[i]);
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/hero.prefab"), props.transform);
                hero.name = "ConceptScaleHero"; hero.transform.localPosition = new Vector3(plan.Spawn.x + 0.5f, plan.Spawn.y + 0.5f);
                var cameraObject = Child(mapRoot.transform, "Main Camera"); cameraObject.tag = "MainCamera";
                var settings = ForestCompositionVisuals.GetSettings();
                var focusSize = Mathf.Clamp(settings.focusOrthographicSize, 6f, 12f);
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = focusSize;
                camera.transform.localPosition = new Vector3(plan.Spawn.x + 0.5f, plan.Spawn.y + 0.5f, -10f); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(24, 43, 30, 255); camera.transparencySortMode = TransparencySortMode.CustomAxis; camera.transparencySortAxis = Vector3.up;
                foreach (var tilemap in new[] { ground, water, terrain, collision }) { tilemap.CompressBounds(); tilemap.RefreshAllTiles(); }
                Physics2D.SyncTransforms();
                var report = MapAuthoringValidator.ValidateScene(scene, true);
                if (row != null) { row.sceneValidated = true; row.sceneValid = !report.HasErrors; row.sceneIssues = report.Issues.Select(issue => $"{issue.Code}: {issue.Message}").ToArray(); }
                if (report.HasErrors) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
                var originalPosition = camera.transform.position; var originalSize = camera.orthographicSize;
                camera.transform.position = new Vector3(plan.Bounds.center.x, plan.Bounds.center.y, -10f); camera.orthographicSize = 16f;
                CaptureCamera(camera, QaRoot + $"/composition-seed-{seed}-overview.png", 1536, 1024);
                camera.transform.position = new Vector3(plan.Spawn.x - 1.5f, plan.Spawn.y + 6.5f, -10f); camera.orthographicSize = 9f;
                CaptureCamera(camera, QaRoot + $"/composition-seed-{seed}-focused.png", 1536, 1024);
                // 全POIの俯瞰とは別に、Characterの見かけの大きさを揃えて広場と森林縁の密度を比較する。
                camera.transform.position = new Vector3(plan.Spawn.x + 0.5f, plan.Spawn.y + 0.5f, -10f); camera.orthographicSize = 8f;
                CaptureCamera(camera, QaRoot + $"/composition-seed-{seed}-game-scale.png", 1536, 1024);
                camera.transform.position = new Vector3(plan.Spawn.x + 0.5f, plan.Spawn.y + 0.5f, -10f); camera.orthographicSize = 6f;
                CaptureCamera(camera, QaRoot + $"/composition-seed-{seed}-detail.png", 768, 768);
                if (plan.BridgeCells.Count > 0)
                {
                    var heroPosition = hero.transform.position;
                    var bridgeCell = plan.BridgeCells.OrderBy(cell => cell.y).ThenBy(cell => cell.x).ElementAt(plan.BridgeCells.Count / 4);
                    hero.transform.position = new Vector3(bridgeCell.x + 0.5f, bridgeCell.y + 0.5f, 0f);
                    camera.transform.position = new Vector3(bridgeCell.x + 0.5f, bridgeCell.y + 0.5f, -10f); camera.orthographicSize = 4f;
                    CaptureCamera(camera, QaRoot + $"/composition-seed-{seed}-bridge-standing.png", 1024, 768);
                    hero.transform.position = heroPosition;
                }
                camera.transform.position = originalPosition; camera.orthographicSize = originalSize;
                if (wasLoaded) { EditorSceneManager.CloseScene(previousTarget, true); targetClosed = true; }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Scene保存失敗: " + path);
                saved = true;
            }
            finally
            {
                if (saved && wasLoaded && previousActive == previousTarget) SceneManager.SetActiveScene(scene);
                else if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                if ((!saved || !wasLoaded) && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (!saved && targetClosed)
                {
                    var restored = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (previousActive == previousTarget) SceneManager.SetActiveScene(restored);
                }
            }
        }

        private static void CreateSocket(Transform parent, string id, MapSocketKind kind, Vector2Int cell)
        {
            var go = Child(parent, id); go.layer = LayerMask.NameToLayer("MapInteraction");
            go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f);
            go.AddComponent<MapSocketMarker>().Configure(kind, Vector2.one, MapSocketFacing.Any, "forest-restart", id,
                newInteractionRadiusOverride: kind == MapSocketKind.Event ? 2f : 0f,
                newEventPlacementMode: MapEventPlacementMode.PoolCandidate);
        }

        internal static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name); child.transform.SetParent(parent, false); return child;
        }

        private static Tilemap MakeTilemap(Transform parent, string name, string sortingLayer, int order, bool collision)
        {
            var go = Child(parent, name); var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>(); renderer.sortingLayerName = sortingLayer; renderer.sortingOrder = order;
            renderer.mode = name == "ForegroundTilemap" ? TilemapRenderer.Mode.Individual : TilemapRenderer.Mode.Chunk;
            renderer.sharedMaterial = GetMaterial();
            if (collision)
            {
                go.layer = LayerMask.NameToLayer("MapCollision"); renderer.enabled = false;
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                go.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
                go.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            return tilemap;
        }

        public static void CaptureCamera(Camera camera, string path, int width, int height)
        {
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point };
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var suppressed = new List<Renderer>();
            try
            {
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (renderer.gameObject.scene == camera.gameObject.scene || renderer.forceRenderingOff) continue;
                    renderer.forceRenderingOff = true; suppressed.Add(renderer);
                }
                target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height;
                camera.Render(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive; camera.ResetAspect();
                foreach (var renderer in suppressed) if (renderer != null) renderer.forceRenderingOff = false;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>UnityのAdditive生成制約を事前に説明し、利用者の未保存Sceneは操作しない。</summary>
        private static void EnsureNoUntitledScene()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded || !string.IsNullOrEmpty(scene.path)) continue;
                throw new InvalidOperationException(
                    "未保存のSceneが開いているためForest Restart Kitを生成できません。" +
                    "現在のSceneを保存するか、保存済みSceneを開いてから実行してください。Sceneの内容は変更していません。");
            }
        }
    }
}
