using System.Collections;
using System.IO;
using FantasyRoyale.Gameplay.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>
    /// Editorを開かずにHUDの見た目を確かめるための撮影。名前で選んだときだけ動く。
    /// Screen Space OverlayのCanvasはCameraの描画に写らないので、撮る間だけScreen Space Cameraへ替え、
    /// 1920×1080で`Logs/HudCapture/`へ書き出す。グラフィックスのあるBatchmode（-nographicsなし）で実行する。
    /// </summary>
    [Explicit("見た目を撮るときだけ名前で選んで実行する")]
    public sealed class ForestMapHudCaptureDebug
    {
        private const string ScenePath = "Assets/ForestMapPreview/ForestMapPreviewDebug.unity";
        private const int Width = 1920;
        private const int Height = 1080;

        [UnityTest]
        public IEnumerator CaptureLoadoutBar()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            ForestMapPreviewDebug preview = null;
            for (var frame = 0; frame < 300 && (preview == null || !preview.IsReady); frame++)
            {
                preview = Object.FindAnyObjectByType<ForestMapPreviewDebug>();
                yield return null;
            }

            Assert.That(preview, Is.Not.Null);
            preview.SetMouseIgnoredForTests(true);
            preview.Hud.HideCursor();

            // 剣に2つ目の魔法を入れ、使用枠に回復薬、カバンに魔法と弓を入れ、HPとMPを少し減らした所を撮る。
            preview.Inventory.TryAddToBag(new CharacterItem("ice", CharacterItemKind.Magic));
            preview.TryUseBagItem(preview.Inventory.FindInBag("ice"));
            preview.AddCoins(60);
            preview.TryBuyPotion();
            preview.Inventory.TryAddToBag(new CharacterItem("fire", CharacterItemKind.Magic));
            preview.Inventory.TryAddToBag(preview.EquipmentCatalog.FindWeapon("bow").CreateItem());
            preview.PlayerHealth.ApplyDamage(30);
            preview.Mana.TrySpend(15);
            preview.TryUseSkill();
            yield return new WaitForSeconds(1f);
            yield return Capture(preview, "loadout-sword.png");

            // 両手の弓へ持ち替えると、魔法の枠は1つになり、剣と盾がカバンへ入る。
            preview.TryUseBagItem(preview.Inventory.FindInBag("bow"));
            yield return Capture(preview, "loadout-bow.png");

            // 持ち物の画面。頭を外して空の枠の影の絵も見せ、使用枠の1つ目の回復薬を選んだ所を撮る。
            preview.Inventory.TryUnequip(FantasyRoyale.Gameplay.Characters.CharacterEquipSlot.Head);
            preview.OpenInventory();
            preview.SelectInventoryCell(preview.Hud.Inventory.FindCell(Gameplay.Hud.InventoryCellKind.Quick, 0));
            yield return null;
            preview.SelectInventoryCell(preview.Hud.Inventory.FindCell(Gameplay.Hud.InventoryCellKind.Quick, 0));
            yield return Capture(preview, "inventory-screen.png");

            // 武器屋の画面。右の枠に装備、使用枠、カバンが出る。
            preview.CloseInventory();
            preview.PlayerActor.Body.position = preview.GetShopPosition(0);
            preview.PlayerObject.transform.position = preview.GetShopPosition(0);
            yield return new WaitForFixedUpdate();
            preview.TryOpenShop();
            yield return null;
            yield return Capture(preview, "shop-screen.png");
        }

        private static IEnumerator Capture(ForestMapPreviewDebug preview, string fileName)
        {
            var camera = preview.GameplayCamera;
            var texture = new RenderTexture(Width, Height, 24);
            var canvas = preview.Hud.GetComponent<Canvas>();
            camera.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            yield return null;
            yield return null;

            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "HudCapture");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, fileName), image.EncodeToPNG());

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            camera.targetTexture = null;
            Object.Destroy(image);
            texture.Release();
            Object.Destroy(texture);
        }
    }
}
