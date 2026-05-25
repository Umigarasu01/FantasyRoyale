using System.Collections.Generic;
using System.IO;
using FantasyRoyale.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.EditorTools
{
    /// <summary>
    /// 一人用プロトタイプをすぐ触れるように、仮ピクセル素材とScene配置をまとめて生成するEditorツール。
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/PrototypeSoloScene.unity";
        private const string ArtFolder = "Assets/Art/Prototype/Generated";
        private const int CharacterPixelsPerUnit = 32;
        private const float MapWidth = 72f;
        private const float MapHeight = 44f;

        private static readonly Color32 Transparent = new(0, 0, 0, 0);
        private static readonly Color32 Ink = new(28, 24, 22, 255);
        private static readonly Color32 White = new(238, 230, 210, 255);

        [MenuItem("FantasyRoyale/Build Solo Prototype Scene")]
        public static void BuildSoloPrototypeScene()
        {
            var sprites = EnsurePrototypeSprites();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PrototypeSoloScene";

            var player = CreatePlayer(sprites);
            var gameController = CreateGameController(player);

            CreateCamera(player.transform);
            CreateHud(gameController);
            CreateField(sprites);
            CreateTerrainObstacles(sprites);
            CreateLocationDetails(sprites);
            CreateEnemies(sprites, player);
            CreateMerchants(sprites, gameController);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 外部素材に依存せず、最低限のキャラクター性が出る仮ピクセル素材を生成する。
        /// </summary>
        private static PrototypeSprites EnsurePrototypeSprites()
        {
            EnsureFolder("Assets", "Art");
            EnsureFolder("Assets/Art", "Prototype");
            EnsureFolder("Assets/Art/Prototype", "Generated");

            WriteSprite("PlayerIdle0.png", 16, 16, texture =>
            {
                DrawHero(texture, 0, new Color32(62, 139, 255, 255), new Color32(245, 185, 118, 255));
            });
            WriteSprite("PlayerIdle1.png", 16, 16, texture =>
            {
                DrawHero(texture, 1, new Color32(74, 156, 255, 255), new Color32(245, 185, 118, 255));
            });
            WriteSprite("PlayerWalk0.png", 16, 16, texture =>
            {
                DrawHero(texture, -1, new Color32(62, 139, 255, 255), new Color32(245, 185, 118, 255));
            });
            WriteSprite("PlayerWalk1.png", 16, 16, texture =>
            {
                DrawHero(texture, 2, new Color32(82, 168, 255, 255), new Color32(245, 185, 118, 255));
            });

            WriteSprite("SlimeIdle0.png", 16, 16, texture =>
            {
                DrawSlime(texture, 0, new Color32(186, 63, 92, 255));
            });
            WriteSprite("SlimeIdle1.png", 16, 16, texture =>
            {
                DrawSlime(texture, 1, new Color32(209, 72, 104, 255));
            });
            WriteSprite("SlimeWalk0.png", 16, 16, texture =>
            {
                DrawSlime(texture, -1, new Color32(186, 63, 92, 255));
            });
            WriteSprite("SlimeWalk1.png", 16, 16, texture =>
            {
                DrawSlime(texture, 2, new Color32(218, 79, 111, 255));
            });

            WriteSprite("ChestClosed.png", 16, 16, DrawClosedChest);
            WriteSprite("ChestOpen.png", 16, 16, DrawOpenChest);
            WriteSprite("Merchant.png", 16, 16, DrawMerchant);
            WriteSprite("SlashEffect.png", 24, 16, DrawSlashEffect);
            WriteSprite("GrassTile.png", 16, 16, DrawGrassTile);
            WriteSprite("SnowTile.png", 16, 16, DrawSnowTile);
            WriteSprite("VolcanoTile.png", 16, 16, DrawVolcanoTile);
            WriteSprite("LavaTile.png", 16, 16, DrawLavaTile);
            WriteSprite("DirtTile.png", 16, 16, DrawDirtTile);
            WriteSprite("StoneTile.png", 16, 16, DrawStoneTile);
            WriteSprite("WaterTile.png", 16, 16, DrawWaterTile);
            WriteSprite("PathStoneTile.png", 16, 16, DrawPathStoneTile);
            WriteSprite("Tree.png", 16, 16, DrawTree);
            WriteSprite("PineTree.png", 16, 16, DrawPineTree);
            WriteSprite("Rock.png", 16, 16, DrawRock);
            WriteSprite("SnowRock.png", 16, 16, DrawSnowRock);
            WriteSprite("VolcanoRock.png", 16, 16, DrawVolcanoRock);
            WriteSprite("Bush.png", 16, 16, DrawBush);
            WriteSprite("TallGrass.png", 16, 16, DrawTallGrass);
            WriteSprite("FlowerPatch.png", 16, 16, DrawFlowerPatch);
            WriteSprite("DeadTree.png", 16, 16, DrawDeadTree);
            WriteSprite("Mushroom.png", 16, 16, DrawMushroom);
            WriteSprite("House.png", 24, 24, DrawHouse);
            WriteSprite("SnowCabin.png", 24, 24, DrawSnowCabin);
            WriteSprite("Ruin.png", 24, 24, DrawRuin);
            WriteSprite("Tent.png", 24, 16, DrawTent);
            WriteSprite("Sign.png", 16, 16, DrawSign);
            WriteSprite("Bridge.png", 24, 16, DrawBridge);

            AssetDatabase.Refresh();

            return new PrototypeSprites
            {
                PlayerIdle = new[] { LoadSprite("PlayerIdle0.png"), LoadSprite("PlayerIdle1.png") },
                PlayerWalk = new[] { LoadSprite("PlayerWalk0.png"), LoadSprite("PlayerWalk1.png") },
                SlimeIdle = new[] { LoadSprite("SlimeIdle0.png"), LoadSprite("SlimeIdle1.png") },
                SlimeWalk = new[] { LoadSprite("SlimeWalk0.png"), LoadSprite("SlimeWalk1.png") },
                ChestClosed = LoadSprite("ChestClosed.png"),
                ChestOpen = LoadSprite("ChestOpen.png"),
                Merchant = LoadSprite("Merchant.png"),
                SlashEffect = LoadSprite("SlashEffect.png"),
                Grass = LoadSprite("GrassTile.png"),
                Snow = LoadSprite("SnowTile.png"),
                Volcano = LoadSprite("VolcanoTile.png"),
                Lava = LoadSprite("LavaTile.png"),
                Dirt = LoadSprite("DirtTile.png"),
                Stone = LoadSprite("StoneTile.png"),
                Water = LoadSprite("WaterTile.png"),
                PathStone = LoadSprite("PathStoneTile.png"),
                Tree = LoadSprite("Tree.png"),
                PineTree = LoadSprite("PineTree.png"),
                Rock = LoadSprite("Rock.png"),
                SnowRock = LoadSprite("SnowRock.png"),
                VolcanoRock = LoadSprite("VolcanoRock.png"),
                Bush = LoadSprite("Bush.png"),
                TallGrass = LoadSprite("TallGrass.png"),
                FlowerPatch = LoadSprite("FlowerPatch.png"),
                DeadTree = LoadSprite("DeadTree.png"),
                Mushroom = LoadSprite("Mushroom.png"),
                House = LoadSprite("House.png"),
                SnowCabin = LoadSprite("SnowCabin.png"),
                Ruin = LoadSprite("Ruin.png"),
                Tent = LoadSprite("Tent.png"),
                Sign = LoadSprite("Sign.png"),
                Bridge = LoadSprite("Bridge.png")
            };
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Sprite LoadSprite(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + fileName);
        }

        private static void WriteSprite(string fileName, int width, int height, System.Action<Texture2D> draw, int pixelsPerUnit = CharacterPixelsPerUnit)
        {
            var path = ArtFolder + "/" + fileName;
            if (!File.Exists(path))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Clear(texture);
                draw(texture);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static PrototypePlayerController2D CreatePlayer(PrototypeSprites sprites)
        {
            var playerObject = CreateSpriteObject("Player", sprites.PlayerIdle[0], Vector2.zero, Vector2.one, Color.white, 10);
            var body = playerObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var collider = playerObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.36f;

            var health = playerObject.AddComponent<PrototypeHealth>();
            health.Initialize(12);

            var player = playerObject.AddComponent<PrototypePlayerController2D>();
            player.ConfigureForPrototype(Physics2D.DefaultRaycastLayers, Physics2D.DefaultRaycastLayers, 12, 2);
            AddSpriteAnimator(playerObject, sprites.PlayerIdle, sprites.PlayerWalk, 7f);
            AssignAttackEffect(player, CreateAttackEffectTemplate(sprites.SlashEffect));
            return player;
        }

        private static PrototypeGameController CreateGameController(PrototypePlayerController2D player)
        {
            var gameObject = new GameObject("PrototypeGameController");
            var controller = gameObject.AddComponent<PrototypeGameController>();

            var serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("player").objectReferenceValue = player;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static void CreateCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.5f;
            camera.backgroundColor = new Color(0.07f, 0.11f, 0.09f);

            var follow = cameraObject.AddComponent<PrototypeCameraFollow>();
            follow.Initialize(target, new Vector2(-MapWidth * 0.5f + 9f, -MapHeight * 0.5f + 6f), new Vector2(MapWidth * 0.5f - 9f, MapHeight * 0.5f - 6f));
        }

        private static void CreateHud(PrototypeGameController controller)
        {
            var canvasObject = new GameObject("PrototypeHudCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var hudObject = new GameObject("PrototypeHud");
            hudObject.transform.SetParent(canvasObject.transform, false);
            var hud = hudObject.AddComponent<PrototypeHud>();

            var hitPointText = CreateHudText("HitPointText", canvasObject.transform, new Vector2(18f, -18f), "HP");
            var coinText = CreateHudText("CoinText", canvasObject.transform, new Vector2(18f, -46f), "Coins");
            var attackText = CreateHudText("AttackText", canvasObject.transform, new Vector2(18f, -74f), "Attack");
            var latestItemText = CreateHudText("LatestItemText", canvasObject.transform, new Vector2(18f, -112f), "Latest: None");
            var messageText = CreateHudText("MessageText", canvasObject.transform, new Vector2(18f, -140f), "WASD/Arrow: Move  Space: Attack  E: Open");

            var hudSerialized = new SerializedObject(hud);
            hudSerialized.FindProperty("hitPointText").objectReferenceValue = hitPointText;
            hudSerialized.FindProperty("coinText").objectReferenceValue = coinText;
            hudSerialized.FindProperty("attackText").objectReferenceValue = attackText;
            hudSerialized.FindProperty("latestItemText").objectReferenceValue = latestItemText;
            hudSerialized.FindProperty("messageText").objectReferenceValue = messageText;
            hudSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("hud").objectReferenceValue = hud;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text CreateHudText(string name, Transform parent, Vector2 anchoredPosition, string text)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(520f, 26f);

            var textComponent = textObject.AddComponent<Text>();
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 18;
            textComponent.color = Color.white;
            textComponent.text = text;
            return textComponent;
        }

        private static void CreateField(PrototypeSprites sprites)
        {
            CreateSpriteObject("Grasslands", sprites.Grass, new Vector2(-18f, 0f), new Vector2(36f, 44f), Color.white, -10);
            CreateSpriteObject("Snowfield", sprites.Snow, new Vector2(18f, 11f), new Vector2(36f, 22f), Color.white, -9);
            CreateSpriteObject("VolcanicWaste", sprites.Volcano, new Vector2(18f, -11f), new Vector2(36f, 22f), Color.white, -9);
            CreateSpriteObject("CentralRoad", sprites.Dirt, new Vector2(0f, 0f), new Vector2(7f, 44f), Color.white, -8);
            CreateSpriteObject("OldStonePath_North", sprites.PathStone, new Vector2(-12f, 10f), new Vector2(18f, 5f), Color.white, -7);
            CreateSpriteObject("OldStonePath_South", sprites.PathStone, new Vector2(-10f, -9f), new Vector2(15f, 5f), Color.white, -7);
            CreateBlockingSprite("Lake", sprites.Water, new Vector2(23f, 12f), new Vector2(13f, 8f), -7);
            CreateBlockingSprite("ForestPond", sprites.Water, new Vector2(-27f, 11f), new Vector2(7f, 5f), -7);
            CreateBlockingSprite("LavaRiver", sprites.Lava, new Vector2(25f, -12f), new Vector2(4f, 16f), -7);
            CreateBlockingSprite("LavaPool", sprites.Lava, new Vector2(10f, -17f), new Vector2(10f, 5f), -7);

            CreateWall(sprites.Stone, "Wall_Top", new Vector2(0f, MapHeight * 0.5f + 0.25f), new Vector2(MapWidth, 0.5f));
            CreateWall(sprites.Stone, "Wall_Bottom", new Vector2(0f, -MapHeight * 0.5f - 0.25f), new Vector2(MapWidth, 0.5f));
            CreateWall(sprites.Stone, "Wall_Left", new Vector2(-MapWidth * 0.5f - 0.25f, 0f), new Vector2(0.5f, MapHeight));
            CreateWall(sprites.Stone, "Wall_Right", new Vector2(MapWidth * 0.5f + 0.25f, 0f), new Vector2(0.5f, MapHeight));
        }

        private static void CreateTerrainObstacles(PrototypeSprites sprites)
        {
            var grassObstacles = new[]
            {
                (sprite: sprites.Tree, name: "Tree", position: new Vector2(-30f, 4f), scale: new Vector2(1.6f, 1.6f)),
                (sprite: sprites.Tree, name: "Tree", position: new Vector2(-24f, -15f), scale: new Vector2(1.8f, 1.8f)),
                (sprite: sprites.Tree, name: "Tree", position: new Vector2(-14f, 15f), scale: new Vector2(1.7f, 1.7f)),
                (sprite: sprites.Rock, name: "Rock", position: new Vector2(-6f, -10f), scale: new Vector2(1.4f, 1.2f)),
            };

            var snowObstacles = new[]
            {
                (sprite: sprites.PineTree, name: "SnowPine", position: new Vector2(8f, 16f), scale: new Vector2(1.8f, 1.8f)),
                (sprite: sprites.PineTree, name: "SnowPine", position: new Vector2(30f, 16f), scale: new Vector2(1.8f, 1.8f)),
                (sprite: sprites.SnowRock, name: "SnowRock", position: new Vector2(17f, 7f), scale: new Vector2(1.6f, 1.3f)),
                (sprite: sprites.SnowRock, name: "SnowRock", position: new Vector2(28f, 5f), scale: new Vector2(1.4f, 1.2f)),
            };

            var volcanoObstacles = new[]
            {
                (sprite: sprites.VolcanoRock, name: "VolcanoRock", position: new Vector2(7f, -7f), scale: new Vector2(1.7f, 1.4f)),
                (sprite: sprites.VolcanoRock, name: "VolcanoRock", position: new Vector2(18f, -19f), scale: new Vector2(1.8f, 1.5f)),
                (sprite: sprites.VolcanoRock, name: "VolcanoRock", position: new Vector2(31f, -8f), scale: new Vector2(1.6f, 1.3f)),
            };

            foreach (var obstacle in grassObstacles)
            {
                CreateBlockingSprite(obstacle.name, obstacle.sprite, obstacle.position, obstacle.scale, 2);
            }

            foreach (var obstacle in snowObstacles)
            {
                CreateBlockingSprite(obstacle.name, obstacle.sprite, obstacle.position, obstacle.scale, 2);
            }

            foreach (var obstacle in volcanoObstacles)
            {
                CreateBlockingSprite(obstacle.name, obstacle.sprite, obstacle.position, obstacle.scale, 2);
            }
        }

        private static void CreateLocationDetails(PrototypeSprites sprites)
        {
            CreateForestVillage(sprites);
            CreateSnowOutpost(sprites);
            CreateVolcanoCamp(sprites);
            CreateRuinsAndWilds(sprites);
        }

        private static void CreateForestVillage(PrototypeSprites sprites)
        {
            CreateBlockingSprite("ForestHouse", sprites.House, new Vector2(-24f, 4f), new Vector2(1.8f, 1.8f), 5);
            CreateBlockingSprite("ForestTent", sprites.Tent, new Vector2(-18f, -2f), new Vector2(1.6f, 1.6f), 5);
            CreateSpriteObject("ForestSign", sprites.Sign, new Vector2(-16f, 3.5f), Vector2.one, Color.white, 6);
            CreateSpriteObject("ForestBridge", sprites.Bridge, new Vector2(-27f, 8f), new Vector2(1.5f, 1.4f), Color.white, 4);

            for (var i = 0; i < 9; i++)
            {
                var x = -32f + i * 3.2f;
                CreateSpriteObject("TallGrass", sprites.TallGrass, new Vector2(x, -3.5f + (i % 3) * 2f), Vector2.one, Color.white, 3);
                CreateSpriteObject("FlowerPatch", sprites.FlowerPatch, new Vector2(x + 1.4f, 5.5f + (i % 2) * 1.8f), Vector2.one, Color.white, 3);
            }
        }

        private static void CreateSnowOutpost(PrototypeSprites sprites)
        {
            CreateBlockingSprite("SnowCabin", sprites.SnowCabin, new Vector2(14f, 16f), new Vector2(1.8f, 1.8f), 5);
            CreateBlockingSprite("FrozenRuin", sprites.Ruin, new Vector2(30f, 10f), new Vector2(1.4f, 1.4f), 5);
            CreateSpriteObject("SnowSign", sprites.Sign, new Vector2(10f, 9f), Vector2.one, Color.white, 6);

            for (var i = 0; i < 7; i++)
            {
                CreateBlockingSprite("SnowPineCluster", sprites.PineTree, new Vector2(6f + i * 4f, 19f - (i % 2) * 3f), new Vector2(1.4f, 1.4f), 3);
                CreateSpriteObject("SnowBush", sprites.Bush, new Vector2(9f + i * 3.5f, 6f + (i % 3) * 2f), Vector2.one, Color.white, 3);
            }
        }

        private static void CreateVolcanoCamp(PrototypeSprites sprites)
        {
            CreateBlockingSprite("AshRuin", sprites.Ruin, new Vector2(14f, -9f), new Vector2(1.6f, 1.6f), 5);
            CreateBlockingSprite("LavaTent", sprites.Tent, new Vector2(30f, -17f), new Vector2(1.5f, 1.5f), 5);
            CreateSpriteObject("AshSign", sprites.Sign, new Vector2(22f, -5f), Vector2.one, Color.white, 6);

            for (var i = 0; i < 8; i++)
            {
                CreateBlockingSprite("DeadTree", sprites.DeadTree, new Vector2(6f + i * 3.6f, -5f - (i % 4) * 3.5f), new Vector2(1.2f, 1.2f), 3);
                CreateSpriteObject("VolcanoRockDetail", sprites.VolcanoRock, new Vector2(9f + i * 3f, -19f + (i % 2) * 2f), Vector2.one, Color.white, 3);
            }
        }

        private static void CreateRuinsAndWilds(PrototypeSprites sprites)
        {
            CreateBlockingSprite("CentralRuin", sprites.Ruin, new Vector2(-3f, 13f), new Vector2(1.5f, 1.5f), 5);
            CreateBlockingSprite("SouthernRuin", sprites.Ruin, new Vector2(-11f, -17f), new Vector2(1.4f, 1.4f), 5);

            for (var i = 0; i < 12; i++)
            {
                CreateSpriteObject("Bush", sprites.Bush, new Vector2(-33f + i * 5.2f, -18f + (i % 4) * 2.2f), Vector2.one, Color.white, 3);
                CreateSpriteObject("Mushroom", sprites.Mushroom, new Vector2(-29f + i * 4.5f, -14f + (i % 3) * 2.4f), Vector2.one, Color.white, 4);
            }
        }

        private static void CreateWall(Sprite stone, string name, Vector2 position, Vector2 scale)
        {
            var wall = CreateSpriteObject(name, stone, position, scale, Color.white, 0);
            wall.AddComponent<BoxCollider2D>();
        }

        private static GameObject CreateBlockingSprite(string name, Sprite sprite, Vector2 position, Vector2 scale, int sortingOrder)
        {
            var blocker = CreateSpriteObject(name, sprite, position, scale, Color.white, sortingOrder);
            blocker.AddComponent<BoxCollider2D>();
            return blocker;
        }

        private static void CreateEnemies(PrototypeSprites sprites, PrototypePlayerController2D player)
        {
            var positions = new[]
            {
                new Vector2(-7f, 3f),
                new Vector2(-14f, 8f),
                new Vector2(-25f, -9f),
                new Vector2(-30f, 13f),
                new Vector2(8f, 13f),
                new Vector2(18f, 17f),
                new Vector2(29f, 7f),
                new Vector2(11f, -8f),
                new Vector2(21f, -15f),
                new Vector2(31f, -3f),
                new Vector2(-9f, -15f),
                new Vector2(0f, 10f)
            };

            foreach (var position in positions)
            {
                var enemyObject = CreateSpriteObject("Slime", sprites.SlimeIdle[0], position, Vector2.one, Color.white, 8);
                var body = enemyObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.freezeRotation = true;

                var collider = enemyObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.34f;

                enemyObject.AddComponent<PrototypeHealth>();
                var enemy = enemyObject.AddComponent<PrototypeEnemy>();
                enemy.Initialize(player.transform, player, 2);
                AddSpriteAnimator(enemyObject, sprites.SlimeIdle, sprites.SlimeWalk, 5f);
            }
        }

        private static void CreateMerchants(PrototypeSprites sprites, PrototypeGameController controller)
        {
            var positions = new[]
            {
                new Vector2(2.5f, 1.5f),
                new Vector2(-27f, 14f),
                new Vector2(24f, 14f),
                new Vector2(26f, -14f)
            };

            foreach (var position in positions)
            {
                var merchantObject = CreateSpriteObject("Merchant", sprites.Merchant, position, new Vector2(1.35f, 1.35f), Color.white, 12);
                merchantObject.AddComponent<BoxCollider2D>().isTrigger = true;
                var merchant = merchantObject.AddComponent<PrototypeMerchant>();
                merchant.Initialize(controller, 10);
            }
        }

        private static GameObject CreateSpriteObject(string name, Sprite sprite, Vector2 position, Vector2 scale, Color color, int sortingOrder)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return gameObject;
        }

        private static void AddSpriteAnimator(GameObject target, Sprite[] idleSprites, Sprite[] moveSprites, float framesPerSecond)
        {
            var animatorType = System.Type.GetType("FantasyRoyale.Prototype.PrototypeSpriteAnimator, Assembly-CSharp");
            if (animatorType == null)
            {
                return;
            }

            var animator = target.AddComponent(animatorType);
            var serializedObject = new SerializedObject(animator);
            AssignSpriteArray(serializedObject.FindProperty("idleSprites"), idleSprites);
            AssignSpriteArray(serializedObject.FindProperty("moveSprites"), moveSprites);
            serializedObject.FindProperty("framesPerSecond").floatValue = framesPerSecond;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Component CreateAttackEffectTemplate(Sprite slashSprite)
        {
            var effectType = System.Type.GetType("FantasyRoyale.Prototype.PrototypeAttackEffect, Assembly-CSharp");
            if (effectType == null)
            {
                return null;
            }

            var effectObject = CreateSpriteObject("AttackEffectTemplate", slashSprite, new Vector2(0f, -20f), Vector2.one, Color.white, 20);
            effectObject.SetActive(false);
            return effectObject.AddComponent(effectType);
        }

        private static void AssignAttackEffect(PrototypePlayerController2D player, Component attackEffect)
        {
            if (attackEffect == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(player);
            serializedObject.FindProperty("attackEffectPrefab").objectReferenceValue = attackEffect;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSpriteArray(SerializedProperty property, IReadOnlyList<Sprite> sprites)
        {
            property.arraySize = sprites.Count;
            for (var i = 0; i < sprites.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var scene in scenes)
            {
                if (scene.path == scenePath)
                {
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[^1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;
        }

        private static void Clear(Texture2D texture)
        {
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, Transparent);
                }
            }
        }

        private static void DrawHero(Texture2D texture, int bounce, Color32 cloak, Color32 skin)
        {
            var y = Mathf.Clamp(bounce, -1, 2);
            Rect(texture, 5, 9 + y, 6, 4, skin);
            Rect(texture, 4, 7 + y, 8, 3, cloak);
            Rect(texture, 3, 4 + y, 10, 4, cloak);
            Rect(texture, 6, 2, 2, 3 + Mathf.Max(0, y), new Color32(58, 48, 42, 255));
            Rect(texture, 9, 2, 2, 3 + Mathf.Max(0, -y), new Color32(58, 48, 42, 255));
            Rect(texture, 6, 11 + y, 1, 1, Ink);
            Rect(texture, 9, 11 + y, 1, 1, Ink);
            Rect(texture, 11, 6 + y, 3, 1, White);
            Rect(texture, 13, 5 + y, 1, 1, White);
            OutlineOpaque(texture, Ink);
        }

        private static void DrawSlime(Texture2D texture, int squash, Color32 body)
        {
            var height = squash > 0 ? 7 : 8;
            var width = squash < 0 ? 11 : 10;
            Rect(texture, 3, 3, width, height, body);
            Rect(texture, 4, 10, 8, 2, body);
            Rect(texture, 5, 7, 2, 2, White);
            Rect(texture, 10, 7, 2, 2, White);
            Rect(texture, 6, 7, 1, 1, Ink);
            Rect(texture, 10, 7, 1, 1, Ink);
            Rect(texture, 7, 5, 3, 1, Ink);
            OutlineOpaque(texture, Ink);
        }

        private static void DrawClosedChest(Texture2D texture)
        {
            Rect(texture, 3, 4, 10, 7, new Color32(135, 82, 35, 255));
            Rect(texture, 4, 8, 8, 3, new Color32(184, 114, 43, 255));
            Rect(texture, 7, 4, 2, 7, new Color32(232, 180, 72, 255));
            Rect(texture, 3, 7, 10, 1, Ink);
            Rect(texture, 7, 6, 2, 2, White);
            OutlineOpaque(texture, Ink);
        }

        private static void DrawOpenChest(Texture2D texture)
        {
            Rect(texture, 3, 3, 10, 5, new Color32(116, 68, 32, 255));
            Rect(texture, 3, 10, 10, 3, new Color32(184, 114, 43, 255));
            Rect(texture, 5, 8, 6, 2, new Color32(255, 226, 99, 255));
            Rect(texture, 7, 3, 2, 5, new Color32(232, 180, 72, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawMerchant(Texture2D texture)
        {
            Rect(texture, 5, 10, 6, 3, new Color32(226, 168, 104, 255));
            Rect(texture, 4, 7, 8, 4, new Color32(82, 64, 151, 255));
            Rect(texture, 3, 4, 10, 4, new Color32(104, 76, 168, 255));
            Rect(texture, 4, 12, 8, 2, new Color32(188, 118, 44, 255));
            Rect(texture, 3, 13, 10, 1, new Color32(228, 174, 75, 255));
            Rect(texture, 5, 6, 6, 2, new Color32(208, 158, 77, 255));
            Rect(texture, 6, 11, 1, 1, Ink);
            Rect(texture, 9, 11, 1, 1, Ink);
            Rect(texture, 11, 5, 3, 3, new Color32(150, 92, 38, 255));
            Rect(texture, 12, 6, 1, 1, new Color32(255, 224, 100, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawSlashEffect(Texture2D texture)
        {
            Rect(texture, 4, 7, 12, 2, new Color32(255, 255, 255, 230));
            Rect(texture, 7, 9, 10, 2, new Color32(177, 226, 255, 210));
            Rect(texture, 10, 11, 8, 1, new Color32(102, 181, 255, 180));
            Rect(texture, 15, 8, 4, 2, new Color32(255, 246, 166, 210));
            Rect(texture, 3, 6, 4, 1, new Color32(255, 255, 255, 150));
            Rect(texture, 2, 9, 4, 1, new Color32(177, 226, 255, 130));
        }

        private static void DrawGrassTile(Texture2D texture)
        {
            Fill(texture, new Color32(36, 90, 52, 255));
            Rect(texture, 1, 2, 2, 1, new Color32(62, 125, 63, 255));
            Rect(texture, 6, 10, 3, 1, new Color32(72, 146, 72, 255));
            Rect(texture, 12, 4, 2, 1, new Color32(55, 112, 58, 255));
            Rect(texture, 3, 13, 2, 1, new Color32(26, 70, 42, 255));
        }

        private static void DrawSnowTile(Texture2D texture)
        {
            Fill(texture, new Color32(204, 228, 235, 255));
            Rect(texture, 0, 0, 16, 3, new Color32(176, 208, 222, 255));
            Rect(texture, 2, 11, 5, 1, new Color32(238, 248, 250, 255));
            Rect(texture, 10, 5, 3, 1, new Color32(238, 248, 250, 255));
            Rect(texture, 5, 3, 2, 2, new Color32(151, 183, 202, 255));
        }

        private static void DrawVolcanoTile(Texture2D texture)
        {
            Fill(texture, new Color32(76, 59, 55, 255));
            Rect(texture, 0, 0, 16, 2, new Color32(42, 36, 35, 255));
            Rect(texture, 2, 8, 5, 1, new Color32(113, 82, 67, 255));
            Rect(texture, 10, 12, 4, 1, new Color32(124, 55, 40, 255));
            Rect(texture, 7, 4, 2, 2, new Color32(201, 74, 35, 255));
        }

        private static void DrawLavaTile(Texture2D texture)
        {
            Fill(texture, new Color32(167, 45, 25, 255));
            Rect(texture, 1, 2, 12, 2, new Color32(255, 132, 37, 255));
            Rect(texture, 4, 9, 10, 2, new Color32(255, 209, 74, 255));
            Rect(texture, 0, 14, 16, 1, new Color32(90, 25, 25, 255));
            Rect(texture, 9, 5, 3, 1, new Color32(255, 237, 142, 255));
        }

        private static void DrawDirtTile(Texture2D texture)
        {
            Fill(texture, new Color32(112, 82, 52, 255));
            Rect(texture, 0, 0, 16, 2, new Color32(83, 61, 41, 255));
            Rect(texture, 2, 6, 4, 1, new Color32(146, 108, 68, 255));
            Rect(texture, 9, 12, 5, 1, new Color32(92, 67, 45, 255));
            Rect(texture, 12, 3, 2, 1, new Color32(160, 122, 80, 255));
        }

        private static void DrawStoneTile(Texture2D texture)
        {
            Fill(texture, new Color32(67, 61, 58, 255));
            Rect(texture, 0, 7, 16, 1, new Color32(42, 38, 36, 255));
            Rect(texture, 5, 0, 1, 7, new Color32(42, 38, 36, 255));
            Rect(texture, 11, 8, 1, 8, new Color32(42, 38, 36, 255));
            Rect(texture, 2, 11, 4, 1, new Color32(93, 84, 77, 255));
        }

        private static void DrawWaterTile(Texture2D texture)
        {
            Fill(texture, new Color32(40, 189, 178, 255));
            Rect(texture, 0, 0, 16, 2, new Color32(26, 122, 148, 255));
            Rect(texture, 2, 5, 7, 1, new Color32(99, 231, 217, 255));
            Rect(texture, 8, 10, 6, 1, new Color32(32, 153, 181, 255));
            Rect(texture, 3, 13, 3, 1, new Color32(158, 255, 235, 255));
        }

        private static void DrawPathStoneTile(Texture2D texture)
        {
            Fill(texture, new Color32(128, 174, 153, 255));
            Rect(texture, 1, 1, 5, 4, new Color32(150, 198, 179, 255));
            Rect(texture, 8, 2, 6, 3, new Color32(103, 145, 132, 255));
            Rect(texture, 3, 9, 4, 4, new Color32(162, 206, 187, 255));
            Rect(texture, 10, 10, 5, 4, new Color32(111, 154, 139, 255));
            Rect(texture, 0, 6, 16, 1, new Color32(78, 111, 102, 255));
        }

        private static void DrawTree(Texture2D texture)
        {
            Rect(texture, 7, 2, 3, 5, new Color32(92, 58, 32, 255));
            Rect(texture, 4, 6, 9, 4, new Color32(38, 103, 48, 255));
            Rect(texture, 3, 9, 11, 4, new Color32(45, 132, 58, 255));
            Rect(texture, 5, 12, 7, 3, new Color32(67, 157, 72, 255));
            Rect(texture, 6, 10, 3, 1, new Color32(99, 183, 86, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawPineTree(Texture2D texture)
        {
            Rect(texture, 7, 2, 3, 4, new Color32(96, 63, 39, 255));
            Rect(texture, 4, 5, 9, 4, new Color32(35, 89, 82, 255));
            Rect(texture, 3, 8, 11, 4, new Color32(42, 116, 106, 255));
            Rect(texture, 5, 11, 7, 4, new Color32(70, 149, 139, 255));
            Rect(texture, 5, 13, 6, 1, new Color32(226, 242, 245, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawRock(Texture2D texture)
        {
            Rect(texture, 3, 4, 10, 6, new Color32(92, 90, 84, 255));
            Rect(texture, 5, 9, 7, 3, new Color32(121, 117, 108, 255));
            Rect(texture, 8, 6, 4, 2, new Color32(64, 61, 58, 255));
            Rect(texture, 4, 5, 3, 1, new Color32(153, 149, 137, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawSnowRock(Texture2D texture)
        {
            DrawRock(texture);
            Rect(texture, 4, 10, 8, 2, new Color32(228, 244, 248, 255));
            Rect(texture, 6, 12, 4, 1, new Color32(246, 253, 255, 255));
        }

        private static void DrawVolcanoRock(Texture2D texture)
        {
            Rect(texture, 3, 4, 10, 7, new Color32(65, 51, 48, 255));
            Rect(texture, 5, 9, 7, 3, new Color32(97, 69, 58, 255));
            Rect(texture, 8, 5, 3, 2, new Color32(210, 76, 38, 255));
            Rect(texture, 9, 6, 2, 1, new Color32(255, 190, 66, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawBush(Texture2D texture)
        {
            Rect(texture, 3, 4, 10, 5, new Color32(36, 116, 54, 255));
            Rect(texture, 2, 7, 12, 4, new Color32(54, 151, 64, 255));
            Rect(texture, 5, 10, 7, 3, new Color32(91, 183, 73, 255));
            Rect(texture, 4, 8, 1, 1, new Color32(166, 225, 97, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawTallGrass(Texture2D texture)
        {
            for (var x = 2; x < 15; x += 2)
            {
                Rect(texture, x, 2, 1, 8, new Color32(40, 112, 49, 255));
                Rect(texture, x + 1, 4, 1, 7, new Color32(104, 185, 70, 255));
            }

            Rect(texture, 1, 2, 14, 2, new Color32(48, 132, 54, 255));
        }

        private static void DrawFlowerPatch(Texture2D texture)
        {
            Rect(texture, 7, 2, 1, 5, new Color32(47, 122, 53, 255));
            Rect(texture, 6, 6, 3, 1, new Color32(255, 230, 85, 255));
            Rect(texture, 7, 7, 1, 1, new Color32(247, 88, 90, 255));
            Rect(texture, 4, 3, 1, 1, new Color32(255, 112, 130, 255));
            Rect(texture, 11, 5, 1, 1, new Color32(255, 220, 76, 255));
        }

        private static void DrawDeadTree(Texture2D texture)
        {
            Rect(texture, 7, 2, 3, 8, new Color32(82, 71, 60, 255));
            Rect(texture, 5, 8, 8, 2, new Color32(112, 97, 79, 255));
            Rect(texture, 4, 11, 5, 2, new Color32(132, 109, 83, 255));
            Rect(texture, 10, 10, 3, 2, new Color32(132, 109, 83, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawMushroom(Texture2D texture)
        {
            Rect(texture, 6, 3, 4, 5, new Color32(244, 219, 164, 255));
            Rect(texture, 3, 8, 10, 4, new Color32(221, 68, 54, 255));
            Rect(texture, 5, 11, 6, 2, new Color32(255, 105, 70, 255));
            Rect(texture, 5, 9, 1, 1, White);
            Rect(texture, 10, 10, 1, 1, White);
            OutlineOpaque(texture, Ink);
        }

        private static void DrawHouse(Texture2D texture)
        {
            DrawBuilding(texture, new Color32(176, 73, 48, 255), new Color32(191, 137, 76, 255));
        }

        private static void DrawSnowCabin(Texture2D texture)
        {
            DrawBuilding(texture, new Color32(83, 116, 138, 255), new Color32(176, 129, 76, 255));
            Rect(texture, 5, 20, 14, 2, new Color32(238, 250, 253, 255));
        }

        private static void DrawBuilding(Texture2D texture, Color32 roof, Color32 wall)
        {
            Rect(texture, 4, 5, 16, 10, wall);
            Rect(texture, 3, 14, 18, 3, new Color32(104, 68, 43, 255));
            Rect(texture, 2, 16, 20, 4, roof);
            Rect(texture, 5, 20, 14, 2, new Color32(217, 111, 62, 255));
            Rect(texture, 7, 5, 4, 6, new Color32(78, 52, 36, 255));
            Rect(texture, 14, 9, 4, 4, new Color32(88, 126, 134, 255));
            Rect(texture, 15, 10, 2, 2, new Color32(160, 224, 216, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawRuin(Texture2D texture)
        {
            Rect(texture, 3, 4, 5, 12, new Color32(103, 116, 108, 255));
            Rect(texture, 10, 4, 4, 9, new Color32(91, 103, 97, 255));
            Rect(texture, 16, 4, 5, 14, new Color32(112, 124, 116, 255));
            Rect(texture, 3, 16, 18, 3, new Color32(78, 91, 86, 255));
            Rect(texture, 5, 19, 4, 1, new Color32(152, 170, 149, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawTent(Texture2D texture)
        {
            Rect(texture, 3, 3, 18, 2, new Color32(86, 54, 37, 255));
            Rect(texture, 5, 5, 14, 5, new Color32(197, 71, 63, 255));
            Rect(texture, 8, 10, 8, 4, new Color32(232, 109, 80, 255));
            Rect(texture, 11, 3, 2, 7, new Color32(54, 38, 32, 255));
            Rect(texture, 2, 2, 20, 1, new Color32(45, 33, 29, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawSign(Texture2D texture)
        {
            Rect(texture, 7, 2, 2, 7, new Color32(101, 66, 38, 255));
            Rect(texture, 3, 8, 10, 5, new Color32(169, 105, 45, 255));
            Rect(texture, 5, 10, 6, 1, new Color32(245, 189, 83, 255));
            OutlineOpaque(texture, Ink);
        }

        private static void DrawBridge(Texture2D texture)
        {
            Fill(texture, new Color32(96, 61, 35, 255));
            for (var x = 0; x < 24; x += 4)
            {
                Rect(texture, x, 0, 1, 16, new Color32(55, 39, 28, 255));
            }

            Rect(texture, 0, 3, 24, 1, new Color32(174, 111, 54, 255));
            Rect(texture, 0, 11, 24, 1, new Color32(174, 111, 54, 255));
            Rect(texture, 1, 7, 22, 2, new Color32(135, 85, 42, 255));
        }

        private static void Fill(Texture2D texture, Color32 color)
        {
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }

        private static void Rect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (var yy = y; yy < y + height; yy++)
            {
                for (var xx = x; xx < x + width; xx++)
                {
                    if (xx >= 0 && yy >= 0 && xx < texture.width && yy < texture.height)
                    {
                        texture.SetPixel(xx, yy, color);
                    }
                }
            }
        }

        private static void OutlineOpaque(Texture2D texture, Color32 outline)
        {
            var outlinePixels = new List<Vector2Int>();
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    if (texture.GetPixel(x, y).a > 0f)
                    {
                        AddOutlinePixel(texture, outlinePixels, x + 1, y);
                        AddOutlinePixel(texture, outlinePixels, x - 1, y);
                        AddOutlinePixel(texture, outlinePixels, x, y + 1);
                        AddOutlinePixel(texture, outlinePixels, x, y - 1);
                    }
                }
            }

            foreach (var pixel in outlinePixels)
            {
                texture.SetPixel(pixel.x, pixel.y, outline);
            }
        }

        private static void AddOutlinePixel(Texture2D texture, List<Vector2Int> pixels, int x, int y)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            {
                return;
            }

            if (texture.GetPixel(x, y).a <= 0f)
            {
                pixels.Add(new Vector2Int(x, y));
            }
        }

        private sealed class PrototypeSprites
        {
            public Sprite[] PlayerIdle;
            public Sprite[] PlayerWalk;
            public Sprite[] SlimeIdle;
            public Sprite[] SlimeWalk;
            public Sprite ChestClosed;
            public Sprite ChestOpen;
            public Sprite Merchant;
            public Sprite SlashEffect;
            public Sprite Grass;
            public Sprite Snow;
            public Sprite Volcano;
            public Sprite Lava;
            public Sprite Dirt;
            public Sprite Stone;
            public Sprite Water;
            public Sprite PathStone;
            public Sprite Tree;
            public Sprite PineTree;
            public Sprite Rock;
            public Sprite SnowRock;
            public Sprite VolcanoRock;
            public Sprite Bush;
            public Sprite TallGrass;
            public Sprite FlowerPatch;
            public Sprite DeadTree;
            public Sprite Mushroom;
            public Sprite House;
            public Sprite SnowCabin;
            public Sprite Ruin;
            public Sprite Tent;
            public Sprite Sign;
            public Sprite Bridge;
        }
    }
}
