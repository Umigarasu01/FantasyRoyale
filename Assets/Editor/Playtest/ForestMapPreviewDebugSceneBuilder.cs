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
        public const string GearDataFolder = "Assets/Data/Gameplay/Gear";
        public const string EnemySpritesPath = "Assets/Data/Gameplay/Characters/SlimeSprites.asset";

        private const string HeroFolder = "Assets/Art/Generated/ForestLook/Hero";
        private const string HeroArmourId = "knight";

        // Sets/<id>のFolderが防具一つになる。定義AssetはSets/<id>.assetへ置く。
        private const string SetsFolder = "Assets/Art/Generated/ForestLook/Sets";
        private const string SetsDataFolder = "Assets/Data/Gameplay/Characters/Sets";
        private const string GearFolder = "Assets/Art/Generated/ForestLook/Gear";
        private const string HandFolder = "Assets/Art/Generated/ForestLook/Hand";
        private const string SmearFolder = "Assets/Art/Generated/ForestLook/Smear";
        private const string GameFolder = "Assets/Art/Generated/ForestLook/Game";
        private const string SwordActionPath = "Assets/Data/Gameplay/Actions/SwordSlash.asset";
        private const string SwordSecondStepPath = "Assets/Data/Gameplay/Actions/SwordSlash2.asset";
        private const string SwordThirdStepPath = "Assets/Data/Gameplay/Actions/SwordSlash3.asset";
        private const string SwordEquipmentPath = "Assets/Data/Gameplay/Equipment/SwordEquipment.asset";
        private const string BowEquipmentPath = "Assets/Data/Gameplay/Equipment/BowEquipment.asset";
        private const string HealingFountainPath = "Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float PixelsPerUnit = 64f;
        private const int SortingOffset = 8000;
        private const int GroundSortingOrder = -30000;
        // 走りの1周のコマ数は絵によって違う。この数までの続いた番号のコマを読む。
        private const int HeroMaxRunFrames = 12;
        private const int HeroSwingFrameCount = 4;
        // 体のコマは96 pixelの高さで、足元は下から4 pixelにある。build_hero_frames.pyのCELL_HEIGHTとFOOT_MARGINに合わせる。
        private const float HeroPivotY = 4f / 96f;

        // 攻撃の上半身の絵は208x192の中で、兜の位置(104, 88)を軸にする。
        private static readonly Vector2 UpperPivot = new Vector2(0.5f, 1f - 88f / 192f);
        private static readonly Vector2 LungePivot = new Vector2(0.5f, 1f - 100f / 128f);

        // 頭の絵は96x96の中で、兜の位置(48, 40)を軸にする。
        private static readonly Vector2 HeadPivot = new Vector2(0.5f, 1f - 40f / 96f);
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
        [Serializable] private class HeroAnchorFrame { public string name; public Vector2 mainHand; public Vector2 offHand; public Vector2 back; public float mainHandAngle; public Vector2 head; public float mainHandFront; public float mainHandBack; }
        [Serializable] private class HeroStance { public string name; public Vector2 head; }
        [Serializable] private class HeroAnchorData { public HeroAnchorFrame[] frames; public HeroStance[] stances; }
        [Serializable] private class GearView { public string view; public string file; public float pivotX; public float pivotY; }
        [Serializable] private class GearItem { public string name; public string slot; public GearView[] views; public string[] rotations; public string[] backRotations; public bool heldInFist; }
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
            var heroSprites = LoadOrCreateBody(HeroSpritesPath, HeroFolder, HeroArmourId, anchors);
            foreach (var folder in AssetDatabase.GetSubFolders(SetsFolder))
            {
                var id = Path.GetFileName(folder);
                LoadOrCreateBody($"{SetsDataFolder}/{id}.asset", folder, id, anchors);
            }

            var gear = LoadOrCreateGear();
            LinkSwordCombo();
            var sword = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(SwordEquipmentPath);
            var bow = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(BowEquipmentPath);
            sword.SetGear(new[] { gear["sword"], gear["shield"] });
            bow.SetGear(new[] { gear["bow"], gear["quiver"] });
            EditorUtility.SetDirty(sword);
            EditorUtility.SetDirty(bow);
            AssetDatabase.SaveAssets();

            var catalog = CharacterEquipmentCatalogUpdater.Refresh();
            var problems = FindCatalogProblems(catalog);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"部位と装備を組み合わせられません: {string.Join(" / ", problems)}");
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
                catalog,
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
        /// 一覧の全ての防具の絵が揃い、全ての胴で全ての武器を持てることを調べる。
        /// </summary>
        private static List<string> FindCatalogProblems(CharacterEquipmentCatalog catalog)
        {
            var problems = catalog.FindIdProblems();
            foreach (var part in catalog.Armour)
            {
                problems.AddRange(part.FindAnchorProblems());
                if (!part.Covers(CharacterArmourSlot.Torso))
                {
                    continue;
                }

                foreach (var weapon in catalog.Weapons)
                {
                    problems.AddRange(part.FindEquipmentProblems(weapon));
                }
            }

            return problems;
        }

        /// <summary>
        /// 片手剣の三段をつなぐ。調整した数値は作り直しても残す。
        /// </summary>
        private static void LinkSwordCombo()
        {
            var first = AssetDatabase.LoadAssetAtPath<CharacterActionDefinition>(SwordActionPath);
            var second = LoadOrCreateSwordStep(SwordSecondStepPath, "sword-slash-2", 20, new CharacterActionTiming(0.1f, 0.1f, 0.2f), 1.4f, 180f);
            var third = LoadOrCreateSwordStep(SwordThirdStepPath, "sword-slash-3", 35, new CharacterActionTiming(0.12f, 0.1f, 0.3f), 1.4f, 180f);
            first.SetNextInCombo(second);
            second.SetNextInCombo(third);
            EditorUtility.SetDirty(first);
            EditorUtility.SetDirty(second);
            EditorUtility.SetDirty(third);
        }

        private static CharacterActionDefinition LoadOrCreateSwordStep(
            string path,
            string actionId,
            int damage,
            CharacterActionTiming timing,
            float range,
            float arcDegrees)
        {
            var step = AssetDatabase.LoadAssetAtPath<CharacterActionDefinition>(path);
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<CharacterActionDefinition>();
                step.ConfigureMelee(actionId, damage, timing, range, arcDegrees);
                AssetDatabase.CreateAsset(step, path);
            }

            return step;
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
                    var pivot = entry.name.StartsWith("hero-upper") ? UpperPivot : new Vector2(0.5f, HeroPivotY);
                    ImportSprite($"{HandFolder}/{entry.name}-main.png", pivot);
                    ImportSprite($"{HandFolder}/{entry.name}-off.png", pivot);
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
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{HandFolder}/{entry.name}-off.png"),
                    entry.head / PixelsPerUnit,
                    entry.mainHandFront / PixelsPerUnit,
                    entry.mainHandBack / PixelsPerUnit);
            }

            foreach (var stance in anchorData.stances)
            {
                anchors[stance.name] = new CharacterSpriteAnchors(Vector2.zero, Vector2.zero, Vector2.zero, head: stance.head / PixelsPerUnit);
            }

            return anchors;
        }

        /// <summary>
        /// フォルダにある頭、胴、足の絵をSpriteとして取り込み、定義Assetへまとめる。ない部位は持たない定義になる。
        /// 調整した再生速度と表示名は作り直しても残す。表示名を入れるまではidを見せる。
        /// </summary>
        private static CharacterSpriteSetDefinition LoadOrCreateBody(
            string assetPath,
            string folder,
            string id,
            Dictionary<string, CharacterSpriteAnchors> anchors)
        {
            var views = new[] { "down", "downside", "side", "upside", "up" };
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var view in views)
                {
                    ImportIfPresent($"{folder}/hero-head-{view}.png", HeadPivot);
                    ImportIfPresent($"{folder}/hero-torso-idle-{view}.png", new Vector2(0.5f, HeroPivotY));
                    ImportIfPresent($"{folder}/hero-legs-idle-{view}.png", new Vector2(0.5f, HeroPivotY));
                    for (var frame = 0; frame < HeroMaxRunFrames; frame++)
                    {
                        ImportIfPresent($"{folder}/hero-torso-{view}-{frame}.png", new Vector2(0.5f, HeroPivotY));
                        ImportIfPresent($"{folder}/hero-torso-sword-{view}-{frame}.png", new Vector2(0.5f, HeroPivotY));
                        ImportIfPresent($"{folder}/hero-legs-{view}-{frame}.png", new Vector2(0.5f, HeroPivotY));
                    }

                    for (var frame = 0; frame < HeroSwingFrameCount; frame++)
                    {
                        ImportIfPresent($"{folder}/hero-torso-swing-{view}-{frame}.png", UpperPivot);
                        ImportIfPresent($"{folder}/hero-legs-lunge-{view}-{frame}.png", LungePivot);
                        if (frame > 0)
                        {
                            ImportSprite($"{SmearFolder}/hero-upper-swing-{view}-{frame}.png", UpperPivot);
                        }
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
                if (!AssetDatabase.IsValidFolder(SetsDataFolder))
                {
                    AssetDatabase.CreateFolder("Assets/Data/Gameplay/Characters", "Sets");
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
            definition.SetId(id);
            if (string.IsNullOrEmpty(definition.DisplayName))
            {
                definition.SetDisplayName(id);
            }

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
            var frames = LoadFrames(folder, $"hero-torso-{view}", HeroMaxRunFrames);
            var legs = LoadFrames(folder, $"hero-legs-{view}", HeroMaxRunFrames);
            var frameAnchors = new CharacterSpriteAnchors[frames.Length];
            for (var frame = 0; frame < frames.Length; frame++)
            {
                frameAnchors[frame] = anchors[$"hero-{view}-{frame}"];
            }

            var swing = LoadFrames(folder, $"hero-torso-swing-{view}", HeroSwingFrameCount);
            var standLegs = LoadFrames(folder, $"hero-legs-lunge-{view}", HeroSwingFrameCount);
            var smears = new Sprite[swing.Length];
            var swingAnchors = new CharacterSpriteAnchors[swing.Length];
            for (var frame = 0; frame < swing.Length; frame++)
            {
                var name = $"hero-upper-swing-{view}-{frame}";
                smears[frame] = frame > 0 ? AssetDatabase.LoadAssetAtPath<Sprite>($"{SmearFolder}/{name}.png") : null;
                swingAnchors[frame] = anchors[name];
            }

            var standHeads = new Vector2[standLegs.Length];
            for (var frame = 0; frame < standLegs.Length; frame++)
            {
                standHeads[frame] = anchors[$"hero-legs-lunge-{view}-{frame}"].Head;
            }

            var idle = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/hero-torso-idle-{view}.png");
            var idleLegs = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/hero-legs-idle-{view}.png");
            var head = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/hero-head-{view}.png");
            var motions = swing.Length > 0 || standLegs.Length > 0
                ? new[] { new CharacterMotionFrames(CharacterActionMotion.Swing, swing, swingAnchors, smears, standLegs, standHeads) }
                : new CharacterMotionFrames[0];

            // 片手剣の型の走りの胴。剣の腕を下ろして拳を握った胴で、手の位置は共有の値である。
            var swordRun = LoadFrames(folder, $"hero-torso-sword-{view}", HeroMaxRunFrames);
            var swordAnchors = new CharacterSpriteAnchors[swordRun.Length];
            for (var frame = 0; frame < swordRun.Length; frame++)
            {
                swordAnchors[frame] = anchors[$"hero-sword-{view}-{frame}"];
            }

            var weaponRuns = swordRun.Length > 0
                ? new[] { new CharacterRunFrames(CharacterActionMotion.Swing, swordRun, swordAnchors) }
                : new CharacterRunFrames[0];
            return new CharacterSpriteView(idle, frames, anchors[$"hero-idle-{view}"], frameAnchors, idleLegs, legs, motions, head, weaponRuns);
        }

        /// <summary>
        /// 0から続いた番号のコマを、最初に欠けた番号の手前まで読む。この部位を持たない定義では空を返す。
        /// </summary>
        private static Sprite[] LoadFrames(string folder, string prefix, int most)
        {
            var frames = new List<Sprite>();
            for (var frame = 0; frame < most; frame++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{prefix}-{frame}.png");
                if (sprite == null)
                {
                    break;
                }

                frames.Add(sprite);
            }

            return frames.ToArray();
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

                    foreach (var rotation in item.backRotations ?? new string[0])
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
                    definition.ConfigureRotations(
                        slot, Array.ConvertAll(item.rotations, Load), Array.ConvertAll(item.backRotations ?? new string[0], Load));
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
        private static void ImportIfPresent(string path, Vector2 pivot)
        {
            if (File.Exists(path))
            {
                ImportSprite(path, pivot);
            }
        }

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
