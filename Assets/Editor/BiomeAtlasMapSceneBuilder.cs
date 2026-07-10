using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.Editor
{
    /// <summary>
    /// 生成済みバイオーム仮アトラスから、探索検証用のTilemap Sceneを組み立てるEditor専用ビルダー。
    /// 完成素材ではなく、森・火山・雪を同じ構造で差し替えられるか確認するための入口として使う。
    /// </summary>
    public static class BiomeAtlasMapSceneBuilder
    {
        private const string AtlasPath = "Assets/Art/Generated/BiomeAtlas/biome-tile-object-atlas.png";
        private const string OutputRoot = "Assets/Art/Generated/BiomeAtlas/Runtime";
        private const string TileOutputRoot = OutputRoot + "/Tiles";
        private const string ObjectOutputRoot = OutputRoot + "/Objects";
        private const string TileAssetRoot = "Assets/Data/BiomeAtlas/Tiles";
        private const string PrefabRoot = "Assets/Prefabs/BiomeAtlas";
        private const string ScenePath = "Assets/Scenes/BiomeAtlasExplorationMapScene.unity";

        private const int MapWidth = 96;
        private const int MapHeight = 72;
        private const int CellSize = 90;
        private const int Step = 101;
        private const int StartX = 9;
        private const int ForestY = 9;
        private const int VolcanoY = 421;
        private const int SnowY = 841;
        private const int ObjectPixelsPerUnit = 90;
        private const int TerrainInset = 8;

        private enum Biome
        {
            Forest,
            Volcano,
            Snow
        }

        private enum TerrainKind
        {
            Ground,
            GroundDetail,
            Path,
            Stone,
            Liquid,
            Cliff,
            Wall
        }

        private sealed class BiomeTiles
        {
            public Tile Ground;
            public Tile GroundDetail;
            public Tile Path;
            public Tile Stone;
            public Tile Liquid;
            public Tile Cliff;
            public Tile Wall;
        }

        private sealed class BiomeObjects
        {
            public GameObject Tree;
            public GameObject Rock;
            public GameObject Stump;
            public GameObject Log;
            public GameObject Crystal;
        }

        /// <summary>
        /// Unityメニューから、仮アトラスの切り出しと探索Scene生成をまとめて実行する。
        /// </summary>
        [MenuItem("FantasyRoyale/Build Biome Atlas Exploration Map")]
        public static void BuildScene()
        {
            EnsureDirectories();

            var atlas = LoadAtlasTexture();
            var tiles = CreateTiles(atlas);
            var objects = CreateObjectPrefabs(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            var grid = new GameObject("Grid").AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var groundMap = CreateTilemap(grid.transform, "Ground", 0, false);
            var detailMap = CreateTilemap(grid.transform, "Details", 1, false);
            var collisionMap = CreateTilemap(grid.transform, "Collision", 2, true);
            var objectRoot = new GameObject("Objects").transform;

            PaintMap(groundMap, detailMap, collisionMap, tiles);
            PlaceObjects(objectRoot, objects);
            CreatePlayerStart();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Biome atlas exploration map generated: {ScenePath}");
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(TileOutputRoot);
            Directory.CreateDirectory(ObjectOutputRoot);
            Directory.CreateDirectory(TileAssetRoot);
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory("Assets/Scenes");
        }

        private static Texture2D LoadAtlasTexture()
        {
            if (!File.Exists(AtlasPath))
            {
                throw new FileNotFoundException("Biome atlas texture was not found.", AtlasPath);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(AtlasPath));
            texture.filterMode = FilterMode.Point;
            texture.name = "BiomeAtlasSource";
            return texture;
        }

        private static Dictionary<Biome, BiomeTiles> CreateTiles(Texture2D atlas)
        {
            return new Dictionary<Biome, BiomeTiles>
            {
                [Biome.Forest] = CreateBiomeTiles(atlas, Biome.Forest, ForestY),
                [Biome.Volcano] = CreateBiomeTiles(atlas, Biome.Volcano, VolcanoY),
                [Biome.Snow] = CreateBiomeTiles(atlas, Biome.Snow, SnowY),
            };
        }

        private static BiomeTiles CreateBiomeTiles(Texture2D atlas, Biome biome, int topY)
        {
            var prefix = biome.ToString().ToLowerInvariant();
            return new BiomeTiles
            {
                Ground = CreateTileFromCell(atlas, $"{prefix}_ground", 0, 0, topY, Tile.ColliderType.None, true),
                GroundDetail = CreateTileFromCell(atlas, $"{prefix}_ground_detail", 1, 0, topY, Tile.ColliderType.None, true),
                Path = CreateTileFromCell(atlas, $"{prefix}_path", 3, 1, topY, Tile.ColliderType.None, true),
                Stone = CreateTileFromCell(atlas, $"{prefix}_stone", 4, 1, topY, Tile.ColliderType.None, true),
                Liquid = CreateTileFromCell(atlas, $"{prefix}_liquid", 6, 1, topY, Tile.ColliderType.Grid, true),
                Cliff = CreateTileFromCell(atlas, $"{prefix}_cliff", 8, 1, topY, Tile.ColliderType.Grid, false),
                Wall = CreateTileFromCell(atlas, $"{prefix}_wall", 9, 0, topY, Tile.ColliderType.Grid, false),
            };
        }

        private static Tile CreateTileFromCell(
            Texture2D atlas,
            string name,
            int column,
            int row,
            int topY,
            Tile.ColliderType colliderType,
            bool trimTerrainBorder)
        {
            var inset = trimTerrainBorder ? TerrainInset : 0;
            var size = CellSize - inset * 2;
            var sprite = CreateSpriteAsset(
                atlas,
                $"{TileOutputRoot}/{name}.png",
                StartX + column * Step + inset,
                topY + row * Step + inset,
                size,
                size,
                size);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.sprite = sprite;
            tile.colliderType = colliderType;

            var assetPath = $"{TileAssetRoot}/{name}.asset";
            DeleteAssetIfExists(assetPath);
            AssetDatabase.CreateAsset(tile, assetPath);
            return tile;
        }

        private static Dictionary<Biome, BiomeObjects> CreateObjectPrefabs(Texture2D atlas)
        {
            return new Dictionary<Biome, BiomeObjects>
            {
                [Biome.Forest] = CreateBiomeObjects(atlas, Biome.Forest, ForestY),
                [Biome.Volcano] = CreateBiomeObjects(atlas, Biome.Volcano, VolcanoY),
                [Biome.Snow] = CreateBiomeObjects(atlas, Biome.Snow, SnowY),
            };
        }

        private static BiomeObjects CreateBiomeObjects(Texture2D atlas, Biome biome, int topY)
        {
            var prefix = biome.ToString().ToLowerInvariant();
            return new BiomeObjects
            {
                Tree = CreateObjectPrefab(atlas, $"{prefix}_tree", 10, 0, topY, new Vector2(0.55f, 0.45f)),
                Rock = CreateObjectPrefab(atlas, $"{prefix}_rock", 11, 3, topY, new Vector2(0.7f, 0.55f)),
                Stump = CreateObjectPrefab(atlas, $"{prefix}_stump", 10, 1, topY, new Vector2(0.55f, 0.35f)),
                Log = CreateObjectPrefab(atlas, $"{prefix}_log", 12, 1, topY, new Vector2(0.9f, 0.35f)),
                Crystal = CreateObjectPrefab(atlas, $"{prefix}_crystal", 11, 2, topY, new Vector2(0.65f, 0.55f)),
            };
        }

        private static GameObject CreateObjectPrefab(
            Texture2D atlas,
            string name,
            int column,
            int row,
            int topY,
            Vector2 colliderScale)
        {
            var sprite = CreateSpriteAsset(atlas, $"{ObjectOutputRoot}/{name}.png", StartX + column * Step, topY + row * Step, CellSize, CellSize, ObjectPixelsPerUnit);
            var instance = new GameObject(name);
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 5;

            var collider = instance.AddComponent<BoxCollider2D>();
            collider.size = colliderScale;
            collider.offset = new Vector2(0f, -0.18f);

            var prefabPath = $"{PrefabRoot}/{name}.prefab";
            DeleteAssetIfExists(prefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static Sprite CreateSpriteAsset(Texture2D atlas, string outputPath, int sourceX, int sourceTopY, int width, int height, int pixelsPerUnit)
        {
            var crop = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var sourceY = atlas.height - sourceTopY - height;
            var pixels = atlas.GetPixels(sourceX, sourceY, width, height);
            crop.SetPixels(pixels);
            crop.filterMode = FilterMode.Point;
            crop.Apply();

            File.WriteAllBytes(outputPath, crop.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(outputPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static Tilemap CreateTilemap(Transform grid, string name, int sortingOrder, bool collision)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(grid, false);

            var tilemap = gameObject.AddComponent<Tilemap>();
            var renderer = gameObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (collision)
            {
                gameObject.AddComponent<TilemapCollider2D>();
            }

            return tilemap;
        }

        private static void PaintMap(
            Tilemap groundMap,
            Tilemap detailMap,
            Tilemap collisionMap,
            IReadOnlyDictionary<Biome, BiomeTiles> tiles)
        {
            for (var y = 0; y < MapHeight; y++)
            {
                for (var x = 0; x < MapWidth; x++)
                {
                    var biome = GetBiome(x, y);
                    var tileSet = tiles[biome];
                    var cell = ToCell(x, y);

                    groundMap.SetTile(cell, ((x * 17 + y * 31) % 9 == 0) ? tileSet.GroundDetail : tileSet.Ground);

                    if (IsBorder(x, y) || IsWallPatch(x, y, biome))
                    {
                        collisionMap.SetTile(cell, tileSet.Wall);
                    }
                }
            }

            PaintPaths(groundMap, tiles);
            PaintLiquids(groundMap, collisionMap, tiles);
            PaintCliffs(collisionMap, tiles);
        }

        private static void PaintPaths(Tilemap groundMap, IReadOnlyDictionary<Biome, BiomeTiles> tiles)
        {
            for (var y = 3; y < MapHeight - 3; y++)
            {
                var centerX = 46 + Mathf.RoundToInt(Mathf.Sin(y * 0.18f) * 3f);
                PaintBrush(groundMap, centerX, y, 2, tiles[GetBiome(centerX, y)].Stone);
            }

            for (var i = 0; i < 62; i++)
            {
                var t = i / 61f;
                var x = Mathf.RoundToInt(Mathf.Lerp(8f, 46f, t) + Mathf.Sin(t * Mathf.PI * 3f) * 5f);
                var y = Mathf.RoundToInt(Mathf.Lerp(22f, 52f, t));
                PaintBrush(groundMap, x, y, 2, tiles[GetBiome(x, y)].Path);
            }

            for (var i = 0; i < 52; i++)
            {
                var t = i / 51f;
                var x = Mathf.RoundToInt(Mathf.Lerp(47f, 84f, t));
                var y = Mathf.RoundToInt(Mathf.Lerp(26f, 16f, t) + Mathf.Sin(t * Mathf.PI * 2f) * 4f);
                PaintBrush(groundMap, x, y, 2, tiles[GetBiome(x, y)].Path);
            }

            PaintClearing(groundMap, 18, 46, 8, 5, tiles[Biome.Forest].Path);
            PaintClearing(groundMap, 21, 17, 9, 5, tiles[Biome.Forest].Path);
            PaintClearing(groundMap, 75, 18, 9, 5, tiles[Biome.Snow].Path);
            PaintClearing(groundMap, 72, 56, 8, 5, tiles[Biome.Volcano].Path);
        }

        private static void PaintLiquids(Tilemap groundMap, Tilemap collisionMap, IReadOnlyDictionary<Biome, BiomeTiles> tiles)
        {
            PaintEllipse(groundMap, collisionMap, 20, 58, 8, 5, tiles[Biome.Forest].Liquid);
            PaintEllipse(groundMap, collisionMap, 78, 55, 13, 8, tiles[Biome.Volcano].Liquid);
            PaintEllipse(groundMap, collisionMap, 77, 12, 11, 6, tiles[Biome.Snow].Liquid);
        }

        private static void PaintCliffs(Tilemap collisionMap, IReadOnlyDictionary<Biome, BiomeTiles> tiles)
        {
            for (var y = 35; y < 67; y++)
            {
                var x = 60 + Mathf.RoundToInt(Mathf.Sin(y * 0.3f) * 2f);
                PaintBrush(collisionMap, x, y, 1, tiles[GetBiome(x, y)].Cliff);
            }

            for (var x = 55; x < 92; x++)
            {
                var y = 38 + Mathf.RoundToInt(Mathf.Sin(x * 0.25f) * 2f);
                PaintBrush(collisionMap, x, y, 1, tiles[GetBiome(x, y)].Cliff);
            }

            for (var x = 55; x < 92; x++)
            {
                var y = 25 + Mathf.RoundToInt(Mathf.Sin(x * 0.22f) * 2f);
                if (x % 3 != 0)
                {
                    PaintBrush(collisionMap, x, y, 1, tiles[GetBiome(x, y)].Cliff);
                }
            }
        }

        private static void PlaceObjects(Transform root, IReadOnlyDictionary<Biome, BiomeObjects> objects)
        {
            var placements = new (Biome biome, GameObject prefab, int x, int y)[]
            {
                (Biome.Forest, objects[Biome.Forest].Tree, 8, 60),
                (Biome.Forest, objects[Biome.Forest].Tree, 15, 10),
                (Biome.Forest, objects[Biome.Forest].Tree, 32, 55),
                (Biome.Forest, objects[Biome.Forest].Rock, 32, 30),
                (Biome.Forest, objects[Biome.Forest].Stump, 23, 47),
                (Biome.Forest, objects[Biome.Forest].Log, 30, 48),
                (Biome.Volcano, objects[Biome.Volcano].Tree, 62, 63),
                (Biome.Volcano, objects[Biome.Volcano].Rock, 68, 45),
                (Biome.Volcano, objects[Biome.Volcano].Crystal, 82, 45),
                (Biome.Volcano, objects[Biome.Volcano].Stump, 88, 61),
                (Biome.Volcano, objects[Biome.Volcano].Log, 67, 58),
                (Biome.Snow, objects[Biome.Snow].Tree, 66, 8),
                (Biome.Snow, objects[Biome.Snow].Tree, 88, 18),
                (Biome.Snow, objects[Biome.Snow].Rock, 63, 24),
                (Biome.Snow, objects[Biome.Snow].Crystal, 80, 27),
                (Biome.Snow, objects[Biome.Snow].Log, 88, 9),
            };

            for (var i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(placement.prefab, root);
                instance.transform.position = GridToWorld(placement.x, placement.y);
                instance.name = $"{placement.biome}_{placement.prefab.name}_{i:00}";
            }
        }

        private static void CreatePlayerStart()
        {
            var player = new GameObject("PlayerStart_DebugMover");
            player.transform.position = GridToWorld(46, 35);

            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.6f, 0.8f);

            player.AddComponent<FantasyRoyale.Unity.Exploration.PlayerMotor2D>();
            player.AddComponent<FantasyRoyale.Unity.Exploration.KeyboardPlayerInput>();

            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "Visual";
            marker.transform.SetParent(player.transform, false);
            marker.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreatePlayerMaterial();
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<MeshCollider>());
        }

        private static Material CreatePlayerMaterial()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            material.color = new Color(0.25f, 0.9f, 1f, 1f);
            return material;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        }

        private static Biome GetBiome(int x, int y)
        {
            if (x >= 56 && y >= 40)
            {
                return Biome.Volcano;
            }

            if (x >= 54 && y <= 28)
            {
                return Biome.Snow;
            }

            return Biome.Forest;
        }

        private static bool IsBorder(int x, int y)
        {
            return x < 3 || y < 3 || x >= MapWidth - 3 || y >= MapHeight - 3;
        }

        private static bool IsWallPatch(int x, int y, Biome biome)
        {
            if (biome == Biome.Forest)
            {
                return (x < 13 && y > 35) || (x < 27 && y < 9) || (x > 34 && x < 42 && y > 51);
            }

            if (biome == Biome.Volcano)
            {
                return (x > 84 && y > 42) || (x > 58 && x < 65 && y > 58);
            }

            return (x > 85 && y < 23) || (x > 54 && x < 62 && y < 12);
        }

        private static void PaintBrush(Tilemap tilemap, int centerX, int centerY, int radius, TileBase tile)
        {
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
                    {
                        continue;
                    }

                    if (Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY) <= radius + 1)
                    {
                        tilemap.SetTile(ToCell(x, y), tile);
                    }
                }
            }
        }

        private static void PaintClearing(Tilemap tilemap, int centerX, int centerY, int radiusX, int radiusY, TileBase tile)
        {
            for (var y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                for (var x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    var normalized = Mathf.Pow((x - centerX) / (float)radiusX, 2f) + Mathf.Pow((y - centerY) / (float)radiusY, 2f);
                    if (normalized <= 1f)
                    {
                        tilemap.SetTile(ToCell(x, y), tile);
                    }
                }
            }
        }

        private static void PaintEllipse(Tilemap groundMap, Tilemap collisionMap, int centerX, int centerY, int radiusX, int radiusY, TileBase tile)
        {
            for (var y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                for (var x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
                    {
                        continue;
                    }

                    var normalized = Mathf.Pow((x - centerX) / (float)radiusX, 2f) + Mathf.Pow((y - centerY) / (float)radiusY, 2f);
                    if (normalized <= 1f)
                    {
                        var cell = ToCell(x, y);
                        groundMap.SetTile(cell, tile);
                        collisionMap.SetTile(cell, tile);
                    }
                }
            }
        }

        private static Vector3Int ToCell(int x, int y)
        {
            return new Vector3Int(x - MapWidth / 2, y - MapHeight / 2, 0);
        }

        private static Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(x - MapWidth / 2 + 0.5f, y - MapHeight / 2 + 0.5f, 0f);
        }
    }
}
