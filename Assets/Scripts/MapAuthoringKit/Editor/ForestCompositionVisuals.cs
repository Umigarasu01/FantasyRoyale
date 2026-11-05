using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>水辺・段差・目的地・地面の小物をSeed付きPlanから表示へ変換する。</summary>
    public static class ForestCompositionVisuals
    {
        public const string Root = ForestRestartAssetBuilder.ArtRoot + "/Composition";
        public const string SourceRoot = Root + "/Source";
        public const string SpriteRoot = Root + "/Sprites";
        public const string TileRoot = ForestRestartAssetBuilder.TileRoot + "/Composition";
        public const string SettingsPath = Root + "/ForestCompositionSettings.asset";
        public const int WaterVariantCount = 8;
        private static readonly string[] SourceNames = { "bridge", "cliff", "shop", "fountain", "water", "cobbles", "verge" };
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static ForestCompositionSettings GetSettings()
        {
            ForestRestartAssetBuilder.EnsureFolder(Root);
            var settings = AssetDatabase.LoadAssetAtPath<ForestCompositionSettings>(SettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<ForestCompositionSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static IEnumerable<string> ExpectedSpriteNames()
        {
            foreach (var name in SourceNames) if (name != "water") yield return name;
            for (var width = 1; width <= 6; width++) yield return $"cliff_{width:00}";
            for (var i = 0; i < WaterVariantCount; i++) yield return $"water_{i:00}";
            foreach (var name in ForestCompositionEdges.SpriteNames()) yield return name;
        }

        /// <summary>Source欠落を変更前に明示する。既存草地などを代用品として生成しない。</summary>
        public static void CheckSourceAssets()
        {
            foreach (var name in SourceNames)
            {
                var path = SourceRoot + "/" + name + ".png";
                if (!File.Exists(path)) throw new FileNotFoundException("Forest Composition元画像がありません。", path);
            }
        }

        public static void BuildAssets()
        {
            CheckSourceAssets();
            foreach (var folder in new[] { SpriteRoot, TileRoot, ForestRestartAssetBuilder.QaRoot }) ForestRestartAssetBuilder.EnsureFolder(folder);
            foreach (var name in SourceNames)
            {
                if (name == "water") continue;
                var source = ForestRestartAssetBuilder.LoadPng(SourceRoot + "/" + name + ".png");
                Texture2D output = null;
                try
                {
                    var dimensions = SpriteDimensions(name);
                    output = ForestRestartAssetBuilder.Resize(source, dimensions.x, dimensions.y, true);
                    File.WriteAllBytes(SpriteRoot + "/" + name + ".png", output.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(source); if (output != null) Object.DestroyImmediate(output); }
            }
            BuildCliffSlices();
            BuildWaterPixels();
            ForestCompositionEdges.BuildAssets();
            ForestCompositionRoadEdges.BuildAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in ExpectedSpriteNames()) ConfigureImport(name);
            var water = AssetDatabase.LoadAssetAtPath<GroundVariationTile>(TileRoot + "/WaterVariation.asset");
            if (water == null) { water = ScriptableObject.CreateInstance<GroundVariationTile>(); AssetDatabase.CreateAsset(water, TileRoot + "/WaterVariation.asset"); }
            water.Configure(Enumerable.Range(0, WaterVariantCount).Select(i => Sprite($"water_{i:00}")).ToArray(), ForestRestartAssetBuilder.DefaultSeed);
            EditorUtility.SetDirty(water);
            GetSettings();
            AssetDatabase.SaveAssets();
        }

        public static Vector2Int SpriteDimensions(string name)
        {
            if (name.StartsWith("bridge_", StringComparison.Ordinal) || name.StartsWith("bank_", StringComparison.Ordinal))
                return ForestCompositionEdges.Dimensions(name);
            if (name.StartsWith("water_", StringComparison.Ordinal)) return new Vector2Int(32, 32);
            if (name.StartsWith("cliff_", StringComparison.Ordinal))
            {
                var width = int.Parse(name.Substring(6));
                if (width < 1 || width > 6) throw new ArgumentException("Cliff Slice幅は1〜6Cellです。", nameof(name));
                return new Vector2Int(width * 32, 96);
            }
            switch (name)
            {
                case "bridge": return new Vector2Int(288, 192);
                case "cliff": return new Vector2Int(192, 96);
                case "shop": return new Vector2Int(128, 128);
                case "fountain": return new Vector2Int(96, 128);
                case "cobbles": return new Vector2Int(96, 64);
                case "verge": return new Vector2Int(64, 64);
                default: throw new ArgumentException("未定義のComposition Spriteです。", nameof(name));
            }
        }

        /// <summary>短い崖Frontを範囲外へはみ出させず、同じ正規化画像の中央列をnative pixel幅で使う。</summary>
        private static void BuildCliffSlices()
        {
            var source = ForestRestartAssetBuilder.LoadPng(SpriteRoot + "/cliff.png");
            try
            {
                var pixels = source.GetPixels32();
                for (var cells = 1; cells <= 6; cells++)
                {
                    var width = cells * 32; var left = (source.width - width) / 2;
                    var texture = new Texture2D(width, source.height, TextureFormat.RGBA32, false);
                    try
                    {
                        var output = new Color32[width * source.height];
                        for (var y = 0; y < source.height; y++)
                            Array.Copy(pixels, y * source.width + left, output, y * width, width);
                        texture.SetPixels32(output); texture.Apply();
                        File.WriteAllBytes(SpriteRoot + $"/cliff_{cells:00}.png", texture.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(texture); }
                }
            }
            finally { Object.DestroyImmediate(source); }
        }

        public static Vector2 SpritePivot(string name)
        {
            if (name.StartsWith("bridge_", StringComparison.Ordinal) || name.StartsWith("bank_", StringComparison.Ordinal))
                return ForestCompositionEdges.Pivot(name);
            if (name.StartsWith("water_", StringComparison.Ordinal)) return new Vector2(0.5f, 0.5f);
            var height = SpriteDimensions(name).y;
            return new Vector2(0.5f, Mathf.Round(height * ForestRestartAssetBuilder.FootPivot) / height);
        }

        private static void ConfigureImport(string name)
        {
            var path = SpriteRoot + "/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture Importerがありません: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = SpritePivot(name); importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }

        private static void BuildWaterPixels()
        {
            var source = ForestRestartAssetBuilder.LoadPng(SourceRoot + "/water.png");
            Texture2D normalized = null;
            try
            {
                if (source.GetPixels32().Any(pixel => pixel.a != 255)) throw new InvalidOperationException("Water Sourceは完全に不透明なTextureが必要です。");
                normalized = ForestRestartAssetBuilder.Resize(source, 128, 128, false);
                File.WriteAllBytes(Root + "/water-normalized.png", normalized.EncodeToPNG());
                var sourcePixels = normalized.GetPixels32();
                for (var variant = 0; variant < WaterVariantCount; variant++)
                {
                    var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                    try
                    {
                        var output = new Color32[1024];
                        // GroundVariationTileの8差分契約へ合わせ、回転・反転・色変更なしでSourceを切り出す。
                        var startX = variant % 4 * 32; var startY = variant / 4 * 32;
                        for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
                        {
                            var pixel = sourcePixels[(startY + y) * 128 + startX + x];
                            if (x == 0 || x == 31) pixel = sourcePixels[y * 128];
                            if (y == 0 || y == 31) pixel = sourcePixels[x];
                            if ((x == 0 || x == 31) && (y == 0 || y == 31)) pixel = sourcePixels[0];
                            output[y * 32 + x] = pixel;
                        }
                        texture.SetPixels32(output); texture.Apply();
                        File.WriteAllBytes(SpriteRoot + $"/water_{variant:00}.png", texture.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(texture); }
                }
            }
            finally { Object.DestroyImmediate(source); if (normalized != null) Object.DestroyImmediate(normalized); }
        }

        public static GroundVariationTile WaterTile()
        {
            var tile = AssetDatabase.LoadAssetAtPath<GroundVariationTile>(TileRoot + "/WaterVariation.asset");
            if (tile == null) throw new InvalidOperationException("Composition Assetを先に生成してください: WaterVariation.asset");
            return tile;
        }

        public static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "/" + name + ".png");
            if (sprite == null) throw new InvalidOperationException("Composition Sprite Import未完了: " + name);
            return sprite;
        }

        public static void Populate(ForestRestartPlan plan, Transform decorations, Transform obstacles)
        {
            ForestCompositionEdges.Banks(plan, decorations);
            ForestCompositionEdges.Bridges(plan, decorations);
            RenderCliffFaces(plan, decorations);
            var index = 0;
            foreach (var placement in plan.Landmarks)
            {
                var instance = CreateSprite(obstacles, $"landmark_{placement.AssetKey}_{index++:00}", placement.AssetKey, placement.Position);
                var anchor = Vector2Int.FloorToInt(placement.Position);
                var min = Vector2Int.FloorToInt(placement.Footprint.min);
                var max = Vector2Int.CeilToInt(placement.Footprint.max);
                ForestRestartAssetBuilder.ConfigureCollision(instance.gameObject, max - min, min - anchor,
                    placement.CollisionRect.size, placement.CollisionRect.center - placement.Position);
            }
            RenderCobbleRoute(plan, decorations);
            RenderVergeEdges(plan, decorations);
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, string key, Vector2 position, bool groundDetail = false)
        {
            var go = ForestRestartAssetBuilder.Child(parent, name);
            go.transform.localPosition = new Vector3(Snap(position.x), Snap(position.y), 0f);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = Sprite(key);
            renderer.sharedMaterial = ForestRestartAssetBuilder.GetMaterial();
            renderer.sortingLayerName = groundDetail ? "MapDetail" : "WorldObjects";
            renderer.sortingOrder = 0; renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            return renderer;
        }

        private static void RenderCliffFaces(ForestRestartPlan plan, Transform parent)
        {
            var index = 0;
            foreach (var row in plan.CliffFaceCells.GroupBy(cell => cell.y).OrderBy(group => group.Key))
            {
                var cells = row.OrderBy(cell => cell.x).ToArray();
                for (var start = 0; start < cells.Length;)
                {
                    var end = start + 1;
                    while (end < cells.Length && cells[end].x == cells[end - 1].x + 1) end++;
                    for (var x = cells[start].x; x <= cells[end - 1].x;)
                    {
                        var width = Mathf.Min(6, cells[end - 1].x - x + 1);
                        CreateSprite(parent, $"cliff_front_{index++:00}", $"cliff_{width:00}", new Vector2(x + width * 0.5f, row.Key));
                        x += width;
                    }
                    start = end;
                }
            }
        }

        private static void RenderCobbleRoute(ForestRestartPlan plan, Transform parent)
        {
            var fountain = plan.Landmarks.FirstOrDefault(item => item.AssetKey == "fountain");
            if (fountain == null) return;
            var start = new Vector2(plan.Spawn.x + 0.5f, plan.Spawn.y + 0.5f);
            var end = fountain.Position + new Vector2(0f, -1.5f);
            var count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / 2.1f));
            for (var i = 1; i < count; i++)
            {
                var point = Vector2.Lerp(start, end, (float)i / count);
                if (Vector2.Distance(point, start) < 2.1f || Vector2.Distance(point, end) < 1.5f) continue;
                var cell = Vector2Int.FloorToInt(point);
                if (!plan.RoadCells.Contains(cell) || plan.BridgeCells.Contains(cell)) continue;
                CreateSprite(parent, $"cobble_route_{i:00}", "cobbles", point, true);
            }
        }

        private static void RenderVergeEdges(ForestRestartPlan plan, Transform parent)
        {
            var placed = new List<Vector2>();
            var index = 0;
            foreach (var cell in plan.GroundCells.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
            {
                if (cell.x < 2 || cell.x >= plan.Bounds.xMax - 2 || cell.y < 2 || cell.y >= plan.Bounds.yMax - 2
                    || plan.RoadCells.Contains(cell) || plan.WaterCells.Contains(cell) || plan.CliffCells.Contains(cell)) continue;
                var isEdge = Directions.Any(direction => plan.RoadCells.Contains(cell + direction)
                    || plan.WaterCells.Contains(cell + direction) || plan.CliffCells.Contains(cell + direction));
                if (!isEdge || StableHash(plan.Seed, cell) % 4u != 0u) continue;
                var point = new Vector2(cell.x + 0.5f, cell.y + 0.375f);
                if (Vector2.Distance(point, (Vector2)plan.Spawn + Vector2.one * 0.5f) < 2.25f
                    || placed.Any(other => Vector2.Distance(point, other) < 1.55f)
                    || plan.Landmarks.Any(item => item.VisualBounds.Overlaps(new Rect(point - Vector2.one, Vector2.one * 2f)))
                    || plan.Obstacles.Any(item => item.Footprint.Contains(point))) continue;
                CreateSprite(parent, $"verge_edge_{index++:000}", "verge", point); placed.Add(point);
            }
        }

        private static uint StableHash(int seed, Vector2Int cell)
        {
            unchecked
            {
                var value = (uint)seed ^ (uint)cell.x * 0x9e3779b9u ^ (uint)cell.y * 0x85ebca6bu;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
                return value;
            }
        }

        private static float Snap(float value) => Mathf.Round(value * 32f) / 32f;
    }
}
