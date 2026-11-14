using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.Editor.Hud
{
    /// <summary>
    /// `Tools/UiAuthoring/build_hud_sprites.py`が書くHUDの絵に、滑らかに描くUI用の取り込み設定を当てる。
    /// 絵は1920×1080の画面の2倍の解像度で描いてあるので、CanvasのReference Pixels Per Unit（100）の2倍を当てる。
    /// `Keepers/`の店主の顔は`Tools/UiAuthoring/build_shop_art.py`がドット絵を整数倍に拡大した物なので、
    /// 点をぼかさないようPoint filterで取り込む。
    /// </summary>
    public sealed class HudSpritePostprocessor : AssetPostprocessor
    {
        public const string SpriteRoot = "Assets/Art/Generated/Hud";
        public const float PixelsPerUnit = 200f;
        public const string PanelName = "hud-panel";
        public const string SelectName = "hud-select";
        public const string KeeperFolder = SpriteRoot + "/Keepers/";
        /// <summary>枠の9分割の境界（絵のpixel）。角の丸みと金の縁がこの内側に収まる。</summary>
        public const int PanelBorder = 32;

        private void OnPreprocessTexture()
        {
            if (!IsHudPng(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = assetPath.Replace('\\', '/').StartsWith(KeeperFolder, StringComparison.OrdinalIgnoreCase)
                ? FilterMode.Point
                : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            settings.spriteBorder = name == PanelName || name == SelectName
                ? Vector4.one * PanelBorder
                : Vector4.zero;
            importer.SetTextureSettings(settings);
        }

        public static bool IsHudPng(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = path.Replace('\\', '/');
            return normalized.StartsWith(SpriteRoot + "/", StringComparison.OrdinalIgnoreCase)
                   && normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }
    }
}
