using System;
using System.Collections.Generic;
using System.IO;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.MapAuthoringKit;
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
        public const string HeroLeatherSpritesPath = "Assets/Data/Gameplay/Characters/HeroLeatherSprites.asset";
        public const string GearDataFolder = "Assets/Data/Gameplay/Gear";
        public const string EnemySpritesPath = "Assets/Data/Gameplay/Characters/SlimeSprites.asset";

        private const string HeroFolder = "Assets/Art/Generated/ForestLook/Hero";
        private const string LeatherFolder = "Assets/Art/Generated/ForestLook/Sets/leather";
        private const string GearFolder = "Assets/Art/Generated/ForestLook/Gear";
        private const string HandFolder = "Assets/Art/Generated/ForestLook/Hand";
        private const string GameFolder = "Assets/Art/Generated/ForestLook/Game";
        private const string SwordEquipmentPath = "Assets/Data/Gameplay/Equipment/SwordEquipment.asset";
        private const string BowEquipmentPath = "Assets/Data/Gameplay/Equipment/BowEquipment.asset";
        private const string HealingFountainPath = "Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float PixelsPerUnit = 64f;
        private const int SortingOffset = 8000;
        private const int GroundSortingOrder = -30000;
        private const int HeroFrameCount = 4;
        private const int HeroAttackFrameCount = 3;
        private const float HeroPivotY = 4f / 80f;
        private const int EnemyFrameCount = 4;
        private const float EnemyFramesPerSecond = 7f;
        private const float EnemyPivotY = 4f / 76f;
        private const float ChestPivotY = 0.08f;

        // 村の広場を開始位置にする。
        private static readonly Vector2 SpawnPosition = new Vector2(60f, 51f);

        // 敵の置き場所の候補。魔物の巣を中心に、砦、鉱山、洞窟、廃屋、祠の周りから試合ごとに選ぶ。
        private static readonly Vector2[] EnemySpots =
        {
            new Vector2(109.5f, 40.5f), new Vector2(115f, 40.5f), new Vector2(108f, 34f), new Vector2(116f, 34f),
            new Vector2(113f, 31f), new Vector2(107f, 24f), new Vector2(118f, 24f), new Vector2(32f, 23f),
            new Vector2(39f, 22f), new Vector2(35f, 63f), new Vector2(30f, 61f), new Vector2(73f, 90f),
            new Vector2(82f, 92f), new Vector2(122f, 70f), new Vector2(109f, 72f), new Vector2(44f, 41f),
            new Vector2(22f, 30f), new Vector2(96f, 30f), new Vector2(36f, 50f), new Vector2(100f, 40f)
        };

        // 宝箱の置き場所の候補。敵の近くや、道から外れた行き止まりから試合ごとに選ぶ。
        private static readonly Vector2[] ChestSpots =
        {
            new Vector2(113f, 25.2f), new Vector2(26f, 85f), new Vector2(36f, 63.8f),
            new Vector2(124f, 67f), new Vector2(83f, 96f), new Vector2(120f, 18f),
            new Vector2(18f, 40f), new Vector2(100f, 62f), new Vector2(68f, 20f)
        };

        // 屋台の手前。ここでEを押すとショップが開く。
        private static readonly Vector2[] ShopSpots = { new Vector2(50f, 46f), new Vector2(55f, 44f) };

        // 回復できる場所。村の噴水と祠の泉の手前に立つ。
        private static readonly Vector2[] HealingSpots = { new Vector2(61f, 44.6f), new Vector2(120f, 64f) };

        [Serializable] private class MapObject { public string part; public int x; public int y; public int sort; }
        [Serializable] private class MapTile { public string file; public int x; public int y; }
        [Serializable] private class MapSprite { public string name; public float pivotX; public float pivotY; }
        [Serializable] private class MapData { public int[] size; public float[] origin; public MapTile[] tiles; public MapSprite[] sprites; public MapObject[] objects; }
        [Serializable] private class HeroAnchorFrame { public string name; public Vector2 mainHand; public Vector2 offHand; public Vector2 back; public float mainHandAngle; }
        [Serializable] private class HeroAnchorData { public HeroAnchorFrame[] frames; }
        [Serializable] private class GearView { public string view; public string file; public float pivotX; public float pivotY; }
        [Serializable] private class GearItem { public string name; public string slot; public GearView[] views; public string[] rotations; public bool heldInFist; }
        [Serializable] private class GearData { public GearItem[] items; }
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
            var anchors = LoadHeroAnchors();
            var heroSprites = LoadOrCreateBody(HeroSpritesPath, HeroFolder, "騎士の鎧", anchors);
            var leatherSprites = LoadOrCreateBody(HeroLeatherSpritesPath, LeatherFolder, "革の装備", anchors);
            var armourSets = new[] { heroSprites, leatherSprites };
            var gear = LoadOrCreateGear();
            var sword = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(SwordEquipmentPath);
            var bow = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(BowEquipmentPath);
            sword.SetGear(new[] { gear["sword"], gear["shield"] });
            bow.SetGear(new[] { gear["bow"], gear["quiver"] });
            EditorUtility.SetDirty(sword);
            EditorUtility.SetDirty(bow);

            var problems = new List<string>();
            foreach (var armourSet in armourSets)
            {
                problems.AddRange(armourSet.FindAnchorProblems());
                problems.AddRange(armourSet.FindEquipmentProblems(sword));
                problems.AddRange(armourSet.FindEquipmentProblems(bow));
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"全身セットと装備を組み合わせられません: {string.Join(" / ", problems)}");
            }

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
                new[] { sword, bow },
                armourSets,
                LoadChestSprite("chest-closed"),
                LoadChestSprite("chest-open"),
                EnemySpots,
                ChestSpots,
                AssetDatabase.LoadAssetAtPath<HealingFountainEventDefinition>(HealingFountainPath),
                HealingSpots,
                ShopSpots);

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
        /// 手と背中の位置、拳の絵を読む。全身セットは騎士の着替えなので、全てのセットがこの値を共有する。
        /// </summary>
        private static Dictionary<string, CharacterSpriteAnchors> LoadHeroAnchors()
        {
            var anchorData = JsonUtility.FromJson<HeroAnchorData>(File.ReadAllText($"{HeroFolder}/anchors.json"));
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in anchorData.frames)
                {
                    ImportSprite($"{HandFolder}/{entry.name}-main.png", new Vector2(0.5f, HeroPivotY));
                    ImportSprite($"{HandFolder}/{entry.name}-off.png", new Vector2(0.5f, HeroPivotY));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var anchors = new Dictionary<string, CharacterSpriteAnchors>();
            foreach (var entry in anchorData.frames)
            {
                anchors[entry.name] = new CharacterSpriteAnchors(
                    entry.mainHand / PixelsPerUnit,
                    entry.offHand / PixelsPerUnit,
                    entry.back / PixelsPerUnit,
                    entry.mainHandAngle,
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{HandFolder}/{entry.name}-main.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{HandFolder}/{entry.name}-off.png"));
            }

            return anchors;
        }

        /// <summary>
        /// 全身セット1つの立ち姿、走り、攻撃のコマをSpriteとして取り込み、定義Assetへまとめる。
        /// 調整した再生速度は作り直しても残す。
        /// </summary>
        private static CharacterSpriteSetDefinition LoadOrCreateBody(
            string assetPath,
            string folder,
            string displayName,
            Dictionary<string, CharacterSpriteAnchors> anchors)
        {
            var views = new[] { "down", "downside", "side", "upside", "up" };
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var view in views)
                {
                    ImportSprite($"{folder}/hero-idle-{view}.png", new Vector2(0.5f, HeroPivotY));
                    for (var frame = 0; frame < HeroFrameCount; frame++)
                    {
                        ImportSprite($"{folder}/hero-{view}-{frame}.png", new Vector2(0.5f, HeroPivotY));
                    }

                    for (var frame = 0; frame < HeroAttackFrameCount; frame++)
                    {
                        ImportSprite($"{folder}/hero-attack-{view}-{frame}.png", new Vector2(0.5f, HeroPivotY));
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var definition = AssetDatabase.LoadAssetAtPath<CharacterSpriteSetDefinition>(assetPath);
            if (definition == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data/Gameplay/Characters"))
                {
                    AssetDatabase.CreateFolder("Assets/Data/Gameplay", "Characters");
                }

                definition = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            definition.Configure(
                definition.FramesPerSecond,
                LoadBodyView(folder, "down", anchors),
                LoadBodyView(folder, "downside", anchors),
                LoadBodyView(folder, "side", anchors),
                LoadBodyView(folder, "upside", anchors),
                LoadBodyView(folder, "up", anchors));
            definition.SetDisplayName(displayName);
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

        private static CharacterSpriteView LoadBodyView(string folder, string view, Dictionary<string, CharacterSpriteAnchors> anchors)
        {
            var frames = new Sprite[HeroFrameCount];
            var frameAnchors = new CharacterSpriteAnchors[HeroFrameCount];
            for (var frame = 0; frame < HeroFrameCount; frame++)
            {
                var name = $"hero-{view}-{frame}";
                frames[frame] = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
                frameAnchors[frame] = anchors[name];
            }

            var attack = new Sprite[HeroAttackFrameCount];
            var attackAnchors = new CharacterSpriteAnchors[HeroAttackFrameCount];
            for (var frame = 0; frame < HeroAttackFrameCount; frame++)
            {
                var name = $"hero-attack-{view}-{frame}";
                attack[frame] = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
                attackAnchors[frame] = anchors[name];
            }

            var idle = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/hero-idle-{view}.png");
            var swing = new CharacterMotionFrames(CharacterActionMotion.Swing, attack, attackAnchors);
            return new CharacterSpriteView(idle, frames, anchors[$"hero-idle-{view}"], frameAnchors, new[] { swing });
        }

        /// <summary>
        /// `gear.json`の部品をSpriteとして取り込み、部品ごとの定義Assetへまとめる。
        /// 新しい部品はシートと一覧へ足すだけで、ここは変わらない。
        /// </summary>
        private static Dictionary<string, CharacterGearDefinition> LoadOrCreateGear()
        {
            var data = JsonUtility.FromJson<GearData>(File.ReadAllText($"{GearFolder}/gear.json"));
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var item in data.items)
                {
                    foreach (var view in item.views)
                    {
                        ImportSprite($"{GearFolder}/{view.file}.png", new Vector2(view.pivotX, view.pivotY));
                    }

                    foreach (var rotation in item.rotations)
                    {
                        ImportSprite($"{GearFolder}/{rotation}.png", new Vector2(0.5f, 0.5f));
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (!AssetDatabase.IsValidFolder(GearDataFolder))
            {
                AssetDatabase.CreateFolder("Assets/Data/Gameplay", "Gear");
            }

            var result = new Dictionary<string, CharacterGearDefinition>();
            foreach (var item in data.items)
            {
                var path = $"{GearDataFolder}/{item.name}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<CharacterGearDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<CharacterGearDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{GearFolder}/{file}.png");
                var slot = (CharacterGearSlot)Enum.Parse(typeof(CharacterGearSlot), item.slot);
                if (item.rotations.Length > 0)
                {
                    definition.ConfigureRotations(slot, Array.ConvertAll(item.rotations, Load));
                }
                else
                {
                    definition.Configure(
                        slot,
                        Load($"{item.name}-down"),
                        Load($"{item.name}-downside"),
                        Load($"{item.name}-side"),
                        Load($"{item.name}-upside"),
                        Load($"{item.name}-up"));
                }

                definition.SetHeldInFist(item.heldInFist);
                EditorUtility.SetDirty(definition);
                result[item.name] = definition;
            }

            return result;
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
