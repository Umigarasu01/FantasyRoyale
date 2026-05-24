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
        private const string ArtFolder = "Assets/Art/Prototype";
        private const int CharacterPixelsPerUnit = 16;

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

            CreateCamera();
            CreateHud(gameController);
            CreateField(sprites);
            CreateEnemies(sprites, player);
            CreateChests(sprites, gameController);

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
            WriteSprite("SlashEffect.png", 24, 16, DrawSlashEffect, 16);
            WriteSprite("GrassTile.png", 16, 16, DrawGrassTile, 16);
            WriteSprite("StoneTile.png", 16, 16, DrawStoneTile, 16);

            AssetDatabase.Refresh();

            return new PrototypeSprites
            {
                PlayerIdle = new[] { LoadSprite("PlayerIdle0.png"), LoadSprite("PlayerIdle1.png") },
                PlayerWalk = new[] { LoadSprite("PlayerWalk0.png"), LoadSprite("PlayerWalk1.png") },
                SlimeIdle = new[] { LoadSprite("SlimeIdle0.png"), LoadSprite("SlimeIdle1.png") },
                SlimeWalk = new[] { LoadSprite("SlimeWalk0.png"), LoadSprite("SlimeWalk1.png") },
                ChestClosed = LoadSprite("ChestClosed.png"),
                ChestOpen = LoadSprite("ChestOpen.png"),
                SlashEffect = LoadSprite("SlashEffect.png"),
                Grass = LoadSprite("GrassTile.png"),
                Stone = LoadSprite("StoneTile.png")
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
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Clear(texture);
            draw(texture);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
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

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.backgroundColor = new Color(0.07f, 0.11f, 0.09f);
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
            CreateSpriteObject("Ground", sprites.Grass, Vector2.zero, new Vector2(18f, 11f), Color.white, -10);

            CreateWall(sprites.Stone, "Wall_Top", new Vector2(0f, 5.75f), new Vector2(18f, 0.5f));
            CreateWall(sprites.Stone, "Wall_Bottom", new Vector2(0f, -5.75f), new Vector2(18f, 0.5f));
            CreateWall(sprites.Stone, "Wall_Left", new Vector2(-9.25f, 0f), new Vector2(0.5f, 11f));
            CreateWall(sprites.Stone, "Wall_Right", new Vector2(9.25f, 0f), new Vector2(0.5f, 11f));
        }

        private static void CreateWall(Sprite stone, string name, Vector2 position, Vector2 scale)
        {
            var wall = CreateSpriteObject(name, stone, position, scale, Color.white, 0);
            wall.AddComponent<BoxCollider2D>();
        }

        private static void CreateEnemies(PrototypeSprites sprites, PrototypePlayerController2D player)
        {
            var positions = new[]
            {
                new Vector2(-5f, 2.6f),
                new Vector2(4.8f, 2.1f),
                new Vector2(-3.8f, -3.2f),
                new Vector2(5.4f, -2.8f)
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

        private static void CreateChests(PrototypeSprites sprites, PrototypeGameController controller)
        {
            var positions = new[]
            {
                new Vector2(-6f, -3.9f),
                new Vector2(0f, 3.6f),
                new Vector2(6.2f, 3.6f),
                new Vector2(3.2f, -3.7f)
            };

            foreach (var position in positions)
            {
                var chestObject = CreateSpriteObject("Chest", sprites.ChestClosed, position, Vector2.one, Color.white, 6);
                chestObject.AddComponent<BoxCollider2D>().isTrigger = true;
                var chest = chestObject.AddComponent<PrototypeChest>();
                chest.Initialize(controller);
                chest.ConfigureVisual(sprites.ChestOpen);
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

        private static void DrawStoneTile(Texture2D texture)
        {
            Fill(texture, new Color32(67, 61, 58, 255));
            Rect(texture, 0, 7, 16, 1, new Color32(42, 38, 36, 255));
            Rect(texture, 5, 0, 1, 7, new Color32(42, 38, 36, 255));
            Rect(texture, 11, 8, 1, 8, new Color32(42, 38, 36, 255));
            Rect(texture, 2, 11, 4, 1, new Color32(93, 84, 77, 255));
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
            public Sprite SlashEffect;
            public Sprite Grass;
            public Sprite Stone;
        }
    }
}
