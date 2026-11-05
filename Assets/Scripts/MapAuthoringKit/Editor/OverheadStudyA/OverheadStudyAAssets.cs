using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    public sealed class OverheadStudyAAssetInfo
    {
        public OverheadStudyAAssetDefinition Definition;
        public Vector2Int NativeSize;
        public RectInt SourceRect;
        public RectInt AlphaBounds;
        public Vector2Int DerivedSize;
        public Vector2 PixelPivot;
        public Vector2 Pivot;
        public Vector2 WorldSize;
        public Rect LocalFootprint;
        public Rect LocalCollider;
        public Rect LocalWalkFloor;
        public int TransparentPixels;
        public int SemiTransparentPixels;
        public string SourceSha256;
        internal string SourcePath;
        internal string DerivedPath;
    }

    public sealed class OverheadStudyAReadiness
    {
        public bool IsReady => Errors.Count == 0;
        public bool VisuallyReviewed;
        public string Fingerprint;
        public OverheadStudyAManifest Manifest;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<OverheadStudyAAssetInfo> AssetInfos = new List<OverheadStudyAAssetInfo>();

        public override string ToString()
        {
            var lines = new List<string> { $"OverheadStudyA: importReady={IsReady}, visuallyReviewed={VisuallyReviewed}, assets={AssetInfos.Count}" };
            lines.AddRange(Errors.Select(s => "ERROR: " + s));
            lines.AddRange(Warnings.Select(s => "WARNING: " + s));
            lines.AddRange(AssetInfos.Select(a => $"{a.Definition.name} [{a.Definition.role}] native={a.NativeSize.x}x{a.NativeSize.y}, crop={a.SourceRect}, derived={a.DerivedSize.x}x{a.DerivedSize.y}, alphaBounds={a.AlphaBounds}, transparent={a.TransparentPixels}, semiAlpha={a.SemiTransparentPixels}"));
            return string.Join("\n", lines);
        }
    }

    /// <summary>原本をbytesで読む。Source TextureImporter、色、個別縮尺を変更しない。</summary>
    public static class OverheadStudyAAssets
    {
        public const string MarkerFileName = "import-marker.json";
        public static readonly string[] RequiredRoles = { "grass", "soil", "water", "tree", "shrub", "rock", "flower", "hero", "fountain", "shop", "bridge" };
        private static readonly Dictionary<string, Sprite> loadedSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static Material transientMaterial;

        [Serializable]
        private sealed class MarkerFile
        {
            public string name;
            public string sha256;
            public string metaSha256;
        }

        [Serializable]
        private sealed class ImportMarker
        {
            public int schemaVersion = 1;
            public string inputFingerprint;
            public bool visuallyReviewed;
            public MarkerFile[] files;
        }

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Check Input Readiness (Read Only)")]
        public static void CheckReadinessMenu()
        {
            var report = CheckReadiness();
            if (report.IsReady) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        public static OverheadStudyAReadiness Validate(OverheadStudyASettings settings = null) => CheckReadiness(settings);

        public static OverheadStudyAReadiness CheckReadiness(OverheadStudyASettings settings = null)
        {
            settings = settings ?? new OverheadStudyASettings();
            var report = new OverheadStudyAReadiness();
            try
            {
                ValidateSettings(settings);
                var input = ProjectPath(settings.InputRoot);
                var manifestPath = SafeChild(input, settings.ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    report.Errors.Add("Manifestがありません: " + settings.InputRoot + "/" + settings.ManifestFileName);
                    report.Errors.Add("必要role: " + string.Join(", ", RequiredRoles));
                    return report;
                }
                var manifestBytes = File.ReadAllBytes(manifestPath);
                report.Manifest = JsonUtility.FromJson<OverheadStudyAManifest>(Encoding.UTF8.GetString(manifestBytes).TrimStart('\uFEFF'));
                if (report.Manifest == null || report.Manifest.schemaVersion != 1 || report.Manifest.entries == null)
                    throw new InvalidDataException("schemaVersion=1 と entries 配列が必要です。");
                if (!report.Manifest.approvedForImport) report.Errors.Add("ManifestのapprovedForImportがfalseです。原本をレビューして明示承認してください。");
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in report.Manifest.entries)
                {
                    try
                    {
                        if (entry == null) throw new InvalidDataException("null entryがあります。");
                        if (!ValidName(entry.name) || !names.Add(entry.name)) throw new InvalidDataException("nameが不正または重複しています: " + entry.name);
                        if (!ValidName(entry.role)) throw new InvalidDataException("roleが不正です: " + entry.role);
                        report.AssetInfos.Add(Inspect(entry, settings, input, report.Warnings, report.Manifest.approvedForImport));
                    }
                    catch (Exception ex) { report.Errors.Add((entry?.name ?? "entry") + ": " + ex.Message); }
                }
                foreach (var role in RequiredRoles)
                    if (!report.AssetInfos.Any(a => a.Definition.role == role)) report.Errors.Add("必要roleが不足しています: " + role);
                if (!report.IsReady) return report;
                report.Fingerprint = Fingerprint(manifestBytes, report.AssetInfos, settings);
                CheckReviewMarker(report, settings);
            }
            catch (Exception ex) { report.Errors.Add(ex.Message); }
            return report;
        }

        public static OverheadStudyAAssetDefinition DefinitionForRole(OverheadStudyAManifest manifest, string role)
        {
            return manifest?.entries?.FirstOrDefault(e => e != null && e.role == role);
        }

        public static OverheadStudyAReadiness ImportApprovedAssets(OverheadStudyASettings settings = null) => PrepareApprovedAssets(settings);

        /// <summary>全入力が成立してからDerivedだけを生成。毎回visual reviewを失効させる。</summary>
        public static OverheadStudyAReadiness PrepareApprovedAssets(OverheadStudyASettings settings = null)
        {
            settings = settings ?? new OverheadStudyASettings();
            var report = CheckReadiness(settings);
            if (!report.IsReady) return report;
            var outputs = new List<KeyValuePair<OverheadStudyAAssetInfo, byte[]>>();
            try
            {
                // 出力write前に全原本を再hash。検査と処理の間の差替えも検出する。
                foreach (var info in report.AssetInfos)
                {
                    var bytes = File.ReadAllBytes(info.SourcePath);
                    if (Sha256(bytes) != info.SourceSha256) throw new IOException("検査後に原本が変わりました: " + info.Definition.file);
                    var source = Decode(bytes);
                    Texture2D derived = null;
                    try
                    {
                        var sourcePixels = source.GetPixels32();
                        var pixels = new Color32[info.DerivedSize.x * info.DerivedSize.y];
                        var crop = info.SourceRect;
                        var width = crop.width / settings.IntegerDownsample;
                        var height = crop.height / settings.IntegerDownsample;
                        var left = (info.DerivedSize.x - width) / 2;
                        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                        {
                            var color = sourcePixels[(crop.y + y * settings.IntegerDownsample) * source.width + crop.x + x * settings.IntegerDownsample];
                            if (settings.BinaryAlphaMask) color.a = color.a >= settings.AlphaThreshold ? (byte)255 : (byte)0;
                            pixels[y * info.DerivedSize.x + left + x] = color;
                        }
                        derived = new Texture2D(info.DerivedSize.x, info.DerivedSize.y, TextureFormat.RGBA32, false);
                        derived.SetPixels32(pixels); derived.Apply();
                        outputs.Add(new KeyValuePair<OverheadStudyAAssetInfo, byte[]>(info, derived.EncodeToPNG()));
                    }
                    finally { Object.DestroyImmediate(source); if (derived != null) Object.DestroyImmediate(derived); }
                }
                // Manifestを含むrecipeも再確認してからwriteする。
                var current = CheckReadiness(settings);
                if (!current.IsReady || current.Fingerprint != report.Fingerprint) throw new IOException("処理中に入力または設定が変わりました。再検査してください。");
                Directory.CreateDirectory(ProjectPath(settings.DerivedRoot));
                // 途中失敗でも以前の承認markerが残らないよう、先に失効させる。
                WriteMarker(settings, new ImportMarker { inputFingerprint = report.Fingerprint, visuallyReviewed = false, files = Array.Empty<MarkerFile>() });
                foreach (var output in outputs)
                {
                    File.WriteAllBytes(ProjectPath(output.Key.DerivedPath), output.Value);
                    ConfigureImport(output.Key, settings);
                }
                WriteMarker(settings, new ImportMarker
                {
                    inputFingerprint = report.Fingerprint,
                    visuallyReviewed = false,
                    files = outputs.Select(o => new MarkerFile
                    {
                        name = o.Key.Definition.name, sha256 = Sha256(o.Value),
                        metaSha256 = Sha256(File.ReadAllBytes(ProjectPath(o.Key.DerivedPath + ".meta")))
                    }).ToArray()
                });
                loadedSprites.Clear();
                return CheckReadiness(settings);
            }
            catch (Exception ex) { report.VisuallyReviewed = false; report.Errors.Add("Derived生成失敗: " + ex.Message); return report; }
        }

        public static Dictionary<string, Sprite> LoadApprovedSprites(OverheadStudyASettings settings = null)
        {
            settings = settings ?? new OverheadStudyASettings();
            var report = CheckReadiness(settings);
            if (!report.IsReady || !report.VisuallyReviewed) throw new InvalidOperationException(report.ToString());
            var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var info in report.AssetInfos)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath);
                if (!SpriteMatches(sprite, info, settings)) throw new InvalidDataException("Derived Sprite寸法/PPU/pivotが契約と不一致です: " + info.DerivedPath);
                result.Add(info.Definition.name, sprite);
            }
            loadedSprites.Clear();
            foreach (var pair in result) loadedSprites.Add(pair.Key, pair.Value);
            return result;
        }

        public static Sprite Sprite(string key)
        {
            if (!loadedSprites.TryGetValue(key, out var sprite)) throw new InvalidOperationException("LoadApprovedSprites後に有効なnameを指定してください: " + key);
            return sprite;
        }

        public static Material Material
        {
            get
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Map/ForestRestart/ForestRestartUnlit.mat");
                if (existing != null) return existing; // 読取だけ。呼出側で変更するならnew Materialで複製する。
                if (transientMaterial != null) return transientMaterial;
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Sprite用Unlit shaderがありません。");
                transientMaterial = new Material(shader) { name = "OverheadStudyAUnlit" };
                return transientMaterial;
            }
        }

        private static OverheadStudyAAssetInfo Inspect(OverheadStudyAAssetDefinition entry, OverheadStudyASettings settings, string input, List<string> warnings, bool approved)
        {
            var sourcePath = SafeChild(input, entry.file);
            if (!string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("fileはPNGを指定してください。");
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("原本がありません: " + entry.file);
            if (new FileInfo(sourcePath).Length > 32 * 1024 * 1024) throw new InvalidDataException("原本が32MiBを超えています。");
            var bytes = File.ReadAllBytes(sourcePath);
            var sourceHash = Sha256(bytes);
            if (approved && (entry.reviewedSourceSha256 == null || !Regex.IsMatch(entry.reviewedSourceSha256, "^[A-Fa-f0-9]{64}$")))
                throw new InvalidDataException("approvedForImport=trueには各原本のreviewedSourceSha256が必要です。");
            if (!string.IsNullOrEmpty(entry.reviewedSourceSha256) && !sourceHash.Equals(entry.reviewedSourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("原本SHA256がレビュー承認時と一致しません。差替え後の明示reviewが必要です。");
            var source = Decode(bytes);
            try
            {
                var crop = entry.sourceRect?.Value ?? new RectInt(0, 0, source.width, source.height);
                if (crop.width <= 0 || crop.height <= 0 || crop.x < 0 || crop.y < 0 || (long)crop.x + crop.width > source.width || (long)crop.y + crop.height > source.height)
                    throw new InvalidDataException("sourceRectは原本内の正の整数領域を指定してください。");
                if (crop.width % settings.IntegerDownsample != 0 || crop.height % settings.IntegerDownsample != 0)
                    throw new InvalidDataException("sourceRect寸法が共通IntegerDownsampleで割り切れません。個別fitは行いません。");
                var width = crop.width / settings.IntegerDownsample; var height = crop.height / settings.IntegerDownsample;
                var canvasWidth = entry.canvasWidth == 0 ? width : entry.canvasWidth;
                var canvasHeight = entry.canvasHeight == 0 ? height : entry.canvasHeight;
                if (canvasWidth < width || canvasHeight < height || canvasWidth > 8192 || canvasHeight > 8192)
                    throw new InvalidDataException("Canvasは縮小後のcrop以上、8192px以下が必要です。小さいCanvasへfitしません。");
                var nativePivot = entry.hasFootPixelPivot ? new Vector2(entry.footPixelX, entry.footPixelY) : new Vector2(crop.x + crop.width * 0.5f, crop.y + crop.height * 0.5f);
                if (!Finite(nativePivot.x) || !Finite(nativePivot.y) || nativePivot.x < crop.x || nativePivot.x > crop.xMax || nativePivot.y < crop.y || nativePivot.y > crop.yMax)
                    throw new InvalidDataException("footPixelPivotはsourceRect内のnative座標を指定してください。");
                var pixelPivot = (nativePivot - new Vector2(crop.x, crop.y)) / settings.IntegerDownsample + new Vector2((canvasWidth - width) / 2, 0);
                if (Mathf.Abs(pixelPivot.x - Mathf.Round(pixelPivot.x)) > 0.0001f || Mathf.Abs(pixelPivot.y - Mathf.Round(pixelPivot.y)) > 0.0001f)
                    throw new InvalidDataException("縮小後のfootPixelPivotが整数pixelに一致しません。明示pivot/canvas設定を見直してください。自動丸めは行いません。");
                var worldSize = new Vector2(canvasWidth, canvasHeight) / settings.PixelsPerUnit;
                if (!Finite(worldSize.x) || !Finite(worldSize.y)) throw new InvalidDataException("共通PPUから有限のworld寸法を求められません。");
                CheckWorldDimension(entry.worldWidth, worldSize.x, "worldWidth"); CheckWorldDimension(entry.worldHeight, worldSize.y, "worldHeight");
                var info = new OverheadStudyAAssetInfo
                {
                    Definition = entry, NativeSize = new Vector2Int(source.width, source.height), SourceRect = crop,
                    DerivedSize = new Vector2Int(canvasWidth, canvasHeight), PixelPivot = pixelPivot,
                    Pivot = new Vector2(pixelPivot.x / canvasWidth, pixelPivot.y / canvasHeight), WorldSize = worldSize,
                    LocalFootprint = WorldRect(entry.footprint), LocalCollider = WorldRect(entry.collisionRect), LocalWalkFloor = WorldRect(entry.walkFloor),
                    SourceSha256 = sourceHash, SourcePath = sourcePath, DerivedPath = settings.DerivedRoot + "/" + entry.name + ".png"
                };
                if (entry.role == "bridge")
                {
                    var visual = new Rect(-Vector2.Scale(info.WorldSize, info.Pivot), info.WorldSize);
                    var floor = info.LocalWalkFloor;
                    if (floor.width <= 0f || floor.height <= 0f || floor.xMin < visual.xMin || floor.yMin < visual.yMin || floor.xMax > visual.xMax || floor.yMax > visual.yMax)
                        throw new InvalidDataException("橋のwalkFloorはSprite表示Bounds内の正の領域である必要があります。実pixelの床との一致は別途reviewしてください。");
                }
                var pixels = source.GetPixels32(); var minX = crop.xMax; var minY = crop.yMax; var maxX = crop.x - 1; var maxY = crop.y - 1; var sampledSurvivors = 0;
                for (var y = crop.y; y < crop.yMax; y++) for (var x = crop.x; x < crop.xMax; x++)
                {
                    var alpha = pixels[y * source.width + x].a;
                    if (alpha == 0) info.TransparentPixels++;
                    else { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                    if (alpha > 0 && alpha < 255) info.SemiTransparentPixels++;
                    if ((x - crop.x) % settings.IntegerDownsample == 0 && (y - crop.y) % settings.IntegerDownsample == 0 &&
                        (settings.BinaryAlphaMask ? alpha >= settings.AlphaThreshold : alpha > 0)) sampledSurvivors++;
                }
                if (maxX < minX || sampledSurvivors == 0) throw new InvalidDataException("共通縮小/明示Alpha処理後に有効なAlpha画素がありません。");
                info.AlphaBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1); // 分析だけ。crop/pivot/scaleには使わない。
                if (!entry.hasFootPixelPivot && entry.role != "grass" && entry.role != "soil" && entry.role != "water") warnings.Add(entry.name + ": footPixelPivot未指定。中央Pivotで仮importし、接地位置のvisual reviewが必要です。");
                if (info.TransparentPixels == 0 && entry.role != "grass" && entry.role != "soil" && entry.role != "water") warnings.Add(entry.name + ": 完全透明画素がありません。背景除去は自動で行いません。");
                if (info.SemiTransparentPixels > 0) warnings.Add(entry.name + (settings.BinaryAlphaMask ? ": 半透明Alphaあり。明示設定BinaryAlphaMaskに従って二値化します。" : ": 半透明Alphaあり。自動二値化は行いません。"));
                return info;
            }
            finally { Object.DestroyImmediate(source); }
        }

        private static void ConfigureImport(OverheadStudyAAssetInfo info, OverheadStudyASettings settings)
        {
            AssetDatabase.ImportAsset(info.DerivedPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Derived TextureImporterがありません: " + info.DerivedPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = settings.PixelsPerUnit; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 8192;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect; textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = info.Pivot; importer.SetTextureSettings(textureSettings); importer.SaveAndReimport();
        }

        private static void CheckReviewMarker(OverheadStudyAReadiness report, OverheadStudyASettings settings)
        {
            try { CheckReviewMarkerContents(report, settings); }
            catch (Exception ex) { report.VisuallyReviewed = false; report.Warnings.Add("marker/Derivedの検査に失敗しました。承認は失効しています: " + ex.Message); }
        }

        private static void CheckReviewMarkerContents(OverheadStudyAReadiness report, OverheadStudyASettings settings)
        {
            var path = ProjectPath(settings.DerivedRoot + "/" + MarkerFileName);
            if (!File.Exists(path)) { report.Warnings.Add("Derived markerがありません。明示importとvisual reviewが必要です。"); return; }
            var marker = JsonUtility.FromJson<ImportMarker>(File.ReadAllText(path));
            if (marker == null || marker.schemaVersion != 1 || marker.inputFingerprint != report.Fingerprint || marker.files == null || marker.files.Length != report.AssetInfos.Count)
            { report.Warnings.Add("入力/recipeが変わったか、markerが不完全です。以前のvisual reviewは失効しています。"); return; }
            foreach (var info in report.AssetInfos)
            {
                var record = marker.files.SingleOrDefault(f => f != null && f.name == info.Definition.name);
                var derived = ProjectPath(info.DerivedPath);
                if (record == null || !File.Exists(derived) || Sha256(File.ReadAllBytes(derived)) != record.sha256)
                { report.Warnings.Add("Derived内容がmarkerと不一致です: " + info.Definition.name); return; }
                var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.spritePixelsPerUnit != settings.PixelsPerUnit || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed || importer.npotScale != TextureImporterNPOTScale.None ||
                    importer.crunchedCompression || importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency ||
                    importer.maxTextureSize < Math.Max(info.DerivedSize.x, info.DerivedSize.y) ||
                    Vector2.Distance(importer.spritePivot, info.Pivot) > 0.00001f)
                { report.Warnings.Add("Derived import設定が契約と不一致です: " + info.Definition.name); return; }
                var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath);
                if (textureSettings.spriteMeshType != SpriteMeshType.FullRect || textureSettings.spriteAlignment != (int)SpriteAlignment.Custom || !SpriteMatches(sprite, info, settings) ||
                    Sha256(File.ReadAllBytes(ProjectPath(info.DerivedPath + ".meta"))) != record.metaSha256)
                { report.Warnings.Add("Derived Sprite/metadataが契約と不一致です: " + info.Definition.name); return; }
            }
            report.VisuallyReviewed = marker.visuallyReviewed;
            if (!report.VisuallyReviewed) report.Warnings.Add("visuallyReviewed=falseです。同一画面を確認してmarkerを明示承認するまでSceneを生成しません。");
        }

        private static void WriteMarker(OverheadStudyASettings settings, ImportMarker marker)
        {
            File.WriteAllText(ProjectPath(settings.DerivedRoot + "/" + MarkerFileName), JsonUtility.ToJson(marker, true), new UTF8Encoding(false));
        }

        private static bool SpriteMatches(Sprite sprite, OverheadStudyAAssetInfo info, OverheadStudyASettings settings)
        {
            if (sprite == null || sprite.packed || sprite.texture == null) return false;
            var expected = new Rect(0, 0, info.DerivedSize.x, info.DerivedSize.y);
            return sprite.rect == expected && sprite.textureRect == expected &&
                   sprite.texture.width == info.DerivedSize.x && sprite.texture.height == info.DerivedSize.y &&
                   Mathf.Abs(sprite.pixelsPerUnit - settings.PixelsPerUnit) <= 0.00001f && Vector2.Distance(sprite.pivot, info.PixelPivot) <= 0.0001f;
        }

        private static void ValidateSettings(OverheadStudyASettings settings)
        {
            if (!Finite(settings.PixelsPerUnit) || settings.PixelsPerUnit <= 0 || settings.IntegerDownsample < 1 || settings.IntegerDownsample > 64)
                throw new InvalidDataException("PPUは正の有限値、共通IntegerDownsampleは1〜64を指定してください。");
            if (settings.WorldWidth <= 0 || settings.WorldHeight <= 0) throw new InvalidDataException("world寸法は正の値を指定してください。");
            var art = ProjectPath(OverheadStudyASettings.ArtRoot);
            var input = ProjectPath(settings.InputRoot); var derived = ProjectPath(settings.DerivedRoot); var qa = ProjectPath(settings.QaRoot);
            if (!Within(art, input) || !Within(art, derived) || !Within(art, qa) || Within(input, derived) || Within(derived, input) || Within(input, qa) || Within(qa, input) || Within(derived, qa) || Within(qa, derived))
                throw new InvalidDataException("Input/Derived/QAは独立OverheadStudyA配下に分離してください。");
            if (settings.BinaryAlphaMask && settings.AlphaThreshold == 0) throw new InvalidDataException("AlphaThreshold=0は透明背景を不透明にするため使用できません。");
        }

        private static string Fingerprint(byte[] manifestBytes, IEnumerable<OverheadStudyAAssetInfo> infos, OverheadStudyASettings settings)
        {
            var text = new StringBuilder("OverheadStudyA-import-v1\n");
            text.AppendLine(Sha256(manifestBytes));
            text.AppendLine(settings.PixelsPerUnit.ToString("R", CultureInfo.InvariantCulture));
            text.AppendLine(settings.IntegerDownsample.ToString(CultureInfo.InvariantCulture));
            text.AppendLine(settings.BinaryAlphaMask.ToString()); text.AppendLine(settings.AlphaThreshold.ToString(CultureInfo.InvariantCulture));
            text.AppendLine(settings.InputRoot); text.AppendLine(settings.DerivedRoot);
            foreach (var info in infos.OrderBy(i => i.Definition.name, StringComparer.Ordinal)) text.AppendLine(info.Definition.name + ":" + info.SourceSha256);
            return Sha256(Encoding.UTF8.GetBytes(text.ToString()));
        }

        private static Texture2D Decode(byte[] bytes)
        {
            if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) throw new InvalidDataException("PNG bytesではありません。");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(bytes, false)) return texture;
            Object.DestroyImmediate(texture); throw new InvalidDataException("PNGをdecodeできません。");
        }

        private static Rect WorldRect(OverheadStudyAWorldRect rectangle)
        {
            if (rectangle == null) return Rect.zero;
            if (!Finite(rectangle.x) || !Finite(rectangle.y) || !Finite(rectangle.width) || !Finite(rectangle.height) || rectangle.width < 0 || rectangle.height < 0)
                throw new InvalidDataException("world Rectは有限値と非負の寸法が必要です。");
            return rectangle.Value;
        }

        private static void CheckWorldDimension(float declared, float actual, string name)
        {
            if (!Finite(declared) || declared < 0 || (declared > 0 && Mathf.Abs(declared - actual) > 0.0001f))
                throw new InvalidDataException(name + "が共通PPU/native寸法と一致しません。個別scaleは推定しません。");
        }

        private static bool ValidName(string name) => name != null && Regex.IsMatch(name, "^[A-Za-z0-9][A-Za-z0-9_-]*$");
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Sha256(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string ProjectPath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Project相対pathが必要です。");
            var root = Directory.GetParent(Application.dataPath).FullName;
            return SafeChild(root, relative);
        }
        private static string SafeChild(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("相対pathが必要です。");
            var result = Path.GetFullPath(Path.Combine(root, relative));
            if (!Within(root, result) || result.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("pathが指定rootから外れています: " + relative);
            // directory junction/symlinkからInput外へ出ない。
            for (var path = result; !path.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase); path = Path.GetDirectoryName(path))
                if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("symlink/junctionは使用しません: " + relative);
            return result;
        }
        private static bool Within(string root, string path)
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            path = Path.GetFullPath(path);
            return path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
