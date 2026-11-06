using System;
using System.Collections.Generic;
using System.IO;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Playtest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Editor.Playtest
{
    /// <summary>
    /// `Tools/MapAuthoring/build_forest_map.py`の出力から、森マップを走る確認用Sceneを作る。
    /// 出力は再生成できるのでGitへ入れず、Sceneも同じ`Assets/ForestMapPreview`へ置く。
    /// </summary>
    public static class ForestMapPreviewDebugSceneBuilder
    {
        public const string Root = "Assets/ForestMapPreview";
        public const string ScenePath = Root + "/ForestMapPreviewDebug.unity";
        public const string HeroSpritesPath = "Assets/Data/Gameplay/Characters/HeroSprites.asset";
        public const string EnemySpritesPath = "Assets/Data/Gameplay/Characters/SlimeSprites.asset";

        private const string HeroFolder = "Assets/Art/Generated/ForestLook/Hero";
        private const string GameFolder = "Assets/Art/Generated/ForestLook/Game";
        private const string SwordEquipmentPath = "Assets/Data/Gameplay/Equipment/SwordEquipment.asset";
        private const string BowEquipmentPath = "Assets/Data/Gameplay/Equipment/BowEquipment.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float PixelsPerUnit = 64f;
        private const int SortingOffset = 8000;
        private const int GroundSortingOrder = -30000;
        private const int HeroFrameCount = 4;
        private const float HeroPivotY = 4f / 80f;
        private const int EnemyFrameCount = 4;
        private const float EnemyFramesPerSecond = 7f;
        private const float EnemyPivotY = 4f / 76f;
        private const float ChestPivotY = 0.08f;

        // 村の広場を開始位置にする。
        private static readonly Vector2 SpawnPosition = new Vector2(60f, 51f);

        // 敵は魔物の巣を中心に、砦、鉱山、洞窟、廃屋、祠の周りへ置く。
        private static readonly Vector2[] EnemySpots =
        {
            new Vector2(109.5f, 40.5f), new Vector2(115f, 40.5f), new Vector2(108f, 34f), new Vector2(116f, 34f),
            new Vector2(113f, 31f), new Vector2(107f, 24f), new Vector2(118f, 24f), new Vector2(32f, 23f),
            new Vector2(39f, 22f), new Vector2(35f, 63f), new Vector2(30f, 61f), new Vector2(73f, 90f),
            new Vector2(82f, 92f), new Vector2(122f, 70f), new Vector2(109f, 72f), new Vector2(44f, 41f)
        };

        // 宝箱は敵の近くや、道から外れた行き止まりへ置く。
        private static readonly Vector2[] ChestSpots =
        {
            new Vector2(113f, 25.2f), new Vector2(26f, 85f), new Vector2(36f, 63.8f),
            new Vector2(124f, 67f), new Vector2(83f, 96f)
        };

        [Serializable] private class MapObject { public string part; public int x; public int y; public int sort; }
        [Serializable] private class MapTile { public string file; public int x; public int y; }
        [Serializable] private class MapSprite { public string name; public float pivotX; public float pivotY; }
        [Serializable] private class MapData { public int[] size; public float[] origin; public MapTile[] tiles; public MapSprite[] sprites; public MapObject[] objects; }
        [Serializable] private class MapBox { public float x; public float y; public float w; public float h; }
        [Serializable] private class MapCollision { public MapBox[] rects; }

        /// <summary>
        /// Map出力を読み込んでSceneを作り直す。対話実行では編集中のSceneの保存を先に確認する。
        /// </summary>
        [MenuItem("FantasyRoyale/Playtest/Build Forest Map Preview Debug Scene")]
        public static void BuildScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            AssetDatabase.Refresh();
            var data = JsonUtility.FromJson<MapData>(File.ReadAllText(Root + "/objects.json"));
            var collision = JsonUtility.FromJson<MapCollision>(File.ReadAllText(Root + "/collision.json"));
            var heroSprites = LoadOrCreateHeroSprites();
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException($"InputActionAssetがありません: {InputActionsPath}");
            }

            ImportMapSprites(data);
            var sprites = new Dictionary<string, Sprite>();
            foreach (var info in data.sprites)
            {
                sprites[info.name] = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Parts/{info.name}.png");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var left = data.origin[0];
            var width = data.size[0] / PixelsPerUnit;
            var height = data.size[1] / PixelsPerUnit;
            var top = data.origin[1] + height;
            var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            Vector2 ToWorld(float x, float y) => new Vector2(left + x / PixelsPerUnit, top - y / PixelsPerUnit);

            var baseMap = new GameObject("BaseMap").transform;
            foreach (var tile in data.tiles)
            {
                var ground = new GameObject(tile.file).AddComponent<SpriteRenderer>();
                ground.transform.SetParent(baseMap, false);
                ground.transform.position = ToWorld(tile.x, tile.y);
                ground.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{tile.file}");
                ground.sharedMaterial = material;
                ground.sortingOrder = GroundSortingOrder;
            }

            var objects = new GameObject("Objects").transform;
            foreach (var item in data.objects)
            {
                if (!sprites.TryGetValue(item.part, out var sprite) || sprite == null)
                {
                    throw new InvalidOperationException($"Spriteがありません: {item.part}");
                }

                var renderer = new GameObject(item.part).AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(objects, false);
                renderer.transform.position = ToWorld(item.x, item.y);
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = item.sort - SortingOffset;
            }

            AddCollision(collision, left, top, width, height);

            var cameraObject = new GameObject("Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            var bootstrap = new GameObject("ForestMapPreviewDebug").AddComponent<ForestMapPreviewDebug>();
            bootstrap.Configure(
                inputActions,
                heroSprites,
                camera,
                new Rect(left, top - height, width, height),
                SpawnPosition);
            bootstrap.ConfigureGame(
                LoadOrCreateEnemySprites(),
                new[]
                {
                    AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(SwordEquipmentPath),
                    AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(BowEquipmentPath)
                },
                LoadChestSprite("chest-closed"),
                LoadChestSprite("chest-open"),
                EnemySpots,
                ChestSpots);

            if (!EditorSceneManager.SaveScene(scene, ScenePath, false))
            {
                throw new InvalidOperationException($"Sceneを保存できません: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Forest Map Preview Debug Scene built: {ScenePath}, objects={data.objects.Length}");
        }

        /// <summary>
        /// 水、森、幹、施設の判定をMapCollision Layerへ置き、Map端にも壁を足す。
        /// </summary>
        private static void AddCollision(MapCollision collision, float left, float top, float width, float height)
        {
            var solid = new GameObject("Collision") { layer = LayerMask.NameToLayer("MapCollision") };
            foreach (var box in collision.rects)
            {
                var collider = solid.AddComponent<BoxCollider2D>();
                collider.offset = new Vector2(
                    left + (box.x + box.w * 0.5f) / PixelsPerUnit,
                    top - (box.y + box.h * 0.5f) / PixelsPerUnit);
                collider.size = new Vector2(box.w / PixelsPerUnit, box.h / PixelsPerUnit);
            }

            var edges = new[]
            {
                new Rect(left - 1f, top - height, 1f, height),
                new Rect(left + width, top - height, 1f, height),
                new Rect(left, top, width, 1f),
                new Rect(left, top - height - 1f, width, 1f)
            };
            foreach (var edge in edges)
            {
                var collider = solid.AddComponent<BoxCollider2D>();
                collider.offset = edge.center;
                collider.size = edge.size;
            }
        }

        private static void ImportMapSprites(MapData data)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var tile in data.tiles)
                {
                    ImportSprite($"{Root}/{tile.file}", new Vector2(0f, 1f));
                }

                foreach (var info in data.sprites)
                {
                    ImportSprite($"{Root}/Parts/{info.name}.png", new Vector2(info.pivotX, info.pivotY));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        /// <summary>
        /// 立ち姿と走りのコマをSpriteとして取り込み、騎士の定義Assetへまとめる。
        /// 調整した再生速度は作り直しても残す。
        /// </summary>
        private static CharacterSpriteSetDefinition LoadOrCreateHeroSprites()
        {
            var views = new[] { "down", "downside", "side", "upside", "up" };
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var view in views)
                {
                    ImportSprite($"{HeroFolder}/hero-idle-{view}.png", new Vector2(0.5f, HeroPivotY));
                    for (var frame = 0; frame < HeroFrameCount; frame++)
                    {
                        ImportSprite($"{HeroFolder}/hero-{view}-{frame}.png", new Vector2(0.5f, HeroPivotY));
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var definition = AssetDatabase.LoadAssetAtPath<CharacterSpriteSetDefinition>(HeroSpritesPath);
            if (definition == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data/Gameplay/Characters"))
                {
                    AssetDatabase.CreateFolder("Assets/Data/Gameplay", "Characters");
                }

                definition = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
                AssetDatabase.CreateAsset(definition, HeroSpritesPath);
            }

            definition.Configure(
                definition.FramesPerSecond,
                LoadHeroView("down"),
                LoadHeroView("downside"),
                LoadHeroView("side"),
                LoadHeroView("upside"),
                LoadHeroView("up"));
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>
        /// 跳ねる4コマをSpriteとして取り込み、全ての向きで同じ絵を使う定義Assetへまとめる。
        /// </summary>
        private static CharacterSpriteSetDefinition LoadOrCreateEnemySprites()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                for (var frame = 0; frame < EnemyFrameCount; frame++)
                {
                    ImportSprite($"{GameFolder}/slime-{frame}.png", new Vector2(0.5f, EnemyPivotY));
                }

                ImportSprite($"{GameFolder}/chest-closed.png", new Vector2(0.5f, ChestPivotY));
                ImportSprite($"{GameFolder}/chest-open.png", new Vector2(0.5f, ChestPivotY));
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var frames = new Sprite[EnemyFrameCount];
            for (var frame = 0; frame < EnemyFrameCount; frame++)
            {
                frames[frame] = AssetDatabase.LoadAssetAtPath<Sprite>($"{GameFolder}/slime-{frame}.png");
            }

            var definition = AssetDatabase.LoadAssetAtPath<CharacterSpriteSetDefinition>(EnemySpritesPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
                AssetDatabase.CreateAsset(definition, EnemySpritesPath);
            }

            var view = new CharacterSpriteView(frames[0], frames);
            definition.Configure(EnemyFramesPerSecond, view, view, view, view, view);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Sprite LoadChestSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{GameFolder}/{name}.png");
        }

        private static CharacterSpriteView LoadHeroView(string view)
        {
            var frames = new Sprite[HeroFrameCount];
            for (var frame = 0; frame < HeroFrameCount; frame++)
            {
                frames[frame] = AssetDatabase.LoadAssetAtPath<Sprite>($"{HeroFolder}/hero-{view}-{frame}.png");
            }

            var idle = AssetDatabase.LoadAssetAtPath<Sprite>($"{HeroFolder}/hero-idle-{view}.png");
            return new CharacterSpriteView(idle, frames);
        }

        /// <summary>
        /// 64 PPU、Point、非圧縮のSpriteとして取り込み、ドットを崩さない。
        /// </summary>
        private static void ImportSprite(string path, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
