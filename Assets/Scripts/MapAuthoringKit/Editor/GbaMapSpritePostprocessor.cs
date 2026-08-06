using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>
    /// 正本から構築された単一Production Atlasへ、カタログで定義したSprite分割とPixel Art向けImport設定を適用する。
    /// 画像のPixelは変更せず、見た目の生成責務をUnity側へ持ち込まない。
    /// </summary>
    public sealed class GbaMapSpritePostprocessor : AssetPostprocessor
    {
        public const string ProductionRoot = "Assets/Art/Map/Production/Forest";
        public const string ProductionAtlasPath =
            "Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png";
        public const string ProductionCatalogPath =
            "Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json";
        public const string RequiredSchema = "fantasyroyale.map-authoring-atlas.v4.3";
        public const float RequiredPixelsPerUnit = 32f;
        public const int RequiredAtlasSize = 1024;
        public const int RequiredTileSize = 32;

        /// <summary>
        /// JSONへ保存されたSprite一枚分の名前、Atlas矩形、Pivot、接続情報、出典を保持する。
        /// </summary>
        [Serializable]
        public sealed class AtlasSpriteEntry
        {
            public string name;
            public int x;
            public int y;
            public int width;
            public int height;
            public float pivotX;
            public float pivotY;
            public string family;
            public int mask;
            public string sourcePath;
            public int sourceX;
            public int sourceY;
            public int sourceWidth;
            public int sourceHeight;
        }

        /// <summary>
        /// Production Atlas全体の寸法と、Unityへ登録する全Sprite定義を保持する。
        /// </summary>
        [Serializable]
        public sealed class AtlasCatalog
        {
            public string schema;
            public string atlasPath;
            public int atlasWidth;
            public int atlasHeight;
            public int tileSize;
            public AtlasSpriteEntry[] sprites;
        }

        /// <summary>
        /// 対象AtlasのImport直前に、Texture設定とカタログ由来のMultiple Sprite矩形を強制する。
        /// </summary>
        private void OnPreprocessTexture()
        {
            if (!IsProductionMapPng(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = RequiredPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            // 透明余白を持つTileもCell全面を描画範囲として扱い、Tight Mesh由来の境界欠けを防ぐ。
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);

            if (!TryLoadCatalog(out var catalog, out var error))
            {
                throw new InvalidOperationException($"Production Atlas catalogを読み込めません: {error}");
            }

            ApplyCatalogSlices(importer, catalog);
        }

        /// <summary>
        /// カタログをFile Systemから読み、Import中でもAssetDatabaseの順序に依存せず利用できる形へ変換する。
        /// </summary>
        public static bool TryLoadCatalog(out AtlasCatalog catalog, out string error)
        {
            catalog = null;
            error = string.Empty;
            if (!File.Exists(ProductionCatalogPath))
            {
                error = $"file missing: {ProductionCatalogPath}";
                return false;
            }

            try
            {
                catalog = JsonUtility.FromJson<AtlasCatalog>(File.ReadAllText(ProductionCatalogPath));
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            return ValidateCatalog(catalog, out error);
        }

        /// <summary>
        /// Schema、Atlas寸法、Sprite名、矩形、Pivotを検査し、不完全なCatalogで参照を壊さないようにする。
        /// </summary>
        public static bool ValidateCatalog(AtlasCatalog catalog, out string error)
        {
            if (catalog == null)
            {
                error = "catalog is null";
                return false;
            }

            if (!string.Equals(catalog.schema, RequiredSchema, StringComparison.Ordinal))
            {
                error = $"schema must be {RequiredSchema}: {catalog.schema}";
                return false;
            }

            var normalizedAtlasPath = (catalog.atlasPath ?? string.Empty).Replace('\\', '/');
            if (!string.Equals(normalizedAtlasPath, ProductionAtlasPath, StringComparison.Ordinal))
            {
                error = $"atlasPath must be {ProductionAtlasPath}: {catalog.atlasPath}";
                return false;
            }

            if (catalog.atlasWidth != RequiredAtlasSize || catalog.atlasHeight != RequiredAtlasSize)
            {
                error = $"atlas size must be {RequiredAtlasSize}x{RequiredAtlasSize}: "
                    + $"{catalog.atlasWidth}x{catalog.atlasHeight}";
                return false;
            }

            if (catalog.tileSize != RequiredTileSize)
            {
                error = $"tileSize must be {RequiredTileSize}: {catalog.tileSize}";
                return false;
            }

            if (catalog.sprites == null || catalog.sprites.Length == 0)
            {
                error = "sprites is empty";
                return false;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < catalog.sprites.Length; i++)
            {
                var entry = catalog.sprites[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.name))
                {
                    error = $"sprites[{i}] has no name";
                    return false;
                }

                if (!names.Add(entry.name))
                {
                    error = $"duplicate sprite name: {entry.name}";
                    return false;
                }

                if (entry.width <= 0 || entry.height <= 0
                    || entry.x < 0 || entry.y < 0
                    || entry.x + entry.width > catalog.atlasWidth
                    || entry.y + entry.height > catalog.atlasHeight)
                {
                    error = $"sprite rect is outside atlas: {entry.name}";
                    return false;
                }

                if (entry.pivotX < 0f || entry.pivotX > 1f
                    || entry.pivotY < 0f || entry.pivotY > 1f)
                {
                    error = $"sprite pivot must be normalized: {entry.name}";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.family))
                {
                    error = $"sprite family is empty: {entry.name}";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.sourcePath)
                    || entry.sourceX < 0 || entry.sourceY < 0
                    || entry.sourceWidth <= 0 || entry.sourceHeight <= 0)
                {
                    error = $"sprite source provenance is invalid: {entry.name}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 既存の同名Sprite IDを優先して再利用し、追加Spriteだけ決定的IDを割り当てて矩形を更新する。
        /// </summary>
        private static void ApplyCatalogSlices(TextureImporter importer, AtlasCatalog catalog)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null)
            {
                throw new InvalidOperationException("Sprite Editor data providerを取得できません。");
            }

            dataProvider.InitSpriteEditorDataProvider();
            var existingIds = CollectExistingSpriteIds(dataProvider);
            var spriteRects = new SpriteRect[catalog.sprites.Length];
            var nameIdPairs = new SpriteNameFileIdPair[catalog.sprites.Length];

            for (var i = 0; i < catalog.sprites.Length; i++)
            {
                var entry = catalog.sprites[i];
                if (!existingIds.TryGetValue(entry.name, out var spriteId) || spriteId.Empty())
                {
                    spriteId = CreateStableSpriteId(entry.name);
                }

                spriteRects[i] = new SpriteRect
                {
                    name = entry.name,
                    rect = new Rect(entry.x, entry.y, entry.width, entry.height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(entry.pivotX, entry.pivotY),
                    border = Vector4.zero,
                    spriteID = spriteId
                };
                nameIdPairs[i] = new SpriteNameFileIdPair(entry.name, spriteId);
            }

            dataProvider.SetSpriteRects(spriteRects);
            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider == null)
            {
                throw new InvalidOperationException("Sprite name/file ID providerを取得できません。");
            }

            nameFileIdProvider.SetNameFileIdPairs(nameIdPairs);
            dataProvider.Apply();
        }

        /// <summary>
        /// SpriteRectとName/File ID表の両方を読み、過去ImportのIDを名前単位で回収する。
        /// </summary>
        private static Dictionary<string, GUID> CollectExistingSpriteIds(ISpriteEditorDataProvider dataProvider)
        {
            var result = new Dictionary<string, GUID>(StringComparer.Ordinal);
            var existingRects = dataProvider.GetSpriteRects();
            for (var i = 0; i < existingRects.Length; i++)
            {
                var rect = existingRects[i];
                if (!string.IsNullOrWhiteSpace(rect.name) && !rect.spriteID.Empty())
                {
                    result[rect.name] = rect.spriteID;
                }
            }

            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider == null)
            {
                return result;
            }

            foreach (var pair in nameFileIdProvider.GetNameFileIdPairs())
            {
                var fileGuid = pair.GetFileGUID();
                if (!string.IsNullOrWhiteSpace(pair.name) && !fileGuid.Empty())
                {
                    result[pair.name] = fileGuid;
                }
            }

            return result;
        }

        /// <summary>
        /// 新規SpriteのLocal IDを名前から決定し、異なる端末や再Importでも同じ参照先へ固定する。
        /// </summary>
        private static GUID CreateStableSpriteId(string spriteName)
        {
            var hash = Hash128.Compute($"{RequiredSchema}:{spriteName}");
            return new GUID(hash.ToString());
        }

        /// <summary>
        /// Asset pathが唯一のProduction Atlas PNGかを厳密に判定し、旧分割PNGへ設定を誤適用しない。
        /// </summary>
        public static bool IsProductionMapPng(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalizedPath = path.Replace('\\', '/');
            return string.Equals(normalizedPath, ProductionAtlasPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
