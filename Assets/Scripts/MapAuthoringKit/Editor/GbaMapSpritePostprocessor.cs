using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>検証Kitの正規化済みSpriteだけにPixel ArtのImport契約を適用する。</summary>
    public sealed class GbaMapSpritePostprocessor : AssetPostprocessor
    {
        public const string SpriteRoot = "Assets/Art/Map/ForestRestart/Sprites";
        public const float RequiredPixelsPerUnit = 32f;
        public const int RequiredTileSize = 32;

        private void OnPreprocessTexture()
        {
            if (!IsProductionMapPng(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = RequiredPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            settings.spritePivot = name.StartsWith("grass_", StringComparison.Ordinal)
                                   || name.StartsWith("road_", StringComparison.Ordinal)
                ? new Vector2(0.5f, 0.5f)
                : new Vector2(0.5f, ForestRestartAssetBuilder.GetFootPivot(name));
            importer.SetTextureSettings(settings);
        }

        public static int GetSpriteHeight(string name)
        {
            if (name == "hero") return 48;
            if (name == "undergrowth") return 96;
            return 128;
        }

        /// <summary>元画像、Concept、QAを変更せず、生成された表示PNGだけを対象とする。</summary>
        public static bool IsProductionMapPng(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var normalized = path.Replace('\\', '/');
            return normalized.StartsWith(SpriteRoot + "/", StringComparison.OrdinalIgnoreCase)
                   && normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }
    }
}
