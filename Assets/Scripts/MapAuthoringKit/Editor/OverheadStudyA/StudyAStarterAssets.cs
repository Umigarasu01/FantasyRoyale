using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>承認された6小物だけを扱い、全地形Kitの準備状態と混同しない。</summary>
    [Serializable]
    public sealed class StudyAStarterSettings
    {
        public string InputRoot = StudyAStarterAssets.DefaultInputRoot;
        public string DerivedRoot = StudyAStarterAssets.DefaultDerivedRoot;
    }

    /// <summary>個別に見た接地点・地面占有範囲・小さな衝突範囲をnative pixelから定義する。</summary>
    [Serializable]
    public sealed class StudyAStarterMetrics
    {
        public string Name;
        public string Role;
        public Vector2Int NativeSize;
        public Vector2Int DerivedSize;
        public Vector2 PixelPivot;
        public Vector2 Pivot;
        public Vector2 WorldSize;
        public Rect LocalFootprint;
        public Rect LocalCollider;
        public RectInt AlphaBounds;
        public string SourceSha256;
        public string SourcePath;
        public string DerivedPath;
        public string GroundingNote;
    }

    /// <summary>Scene BuilderがSpriteと物理用の個別指標を同じ契約で受け取る。</summary>
    public sealed class StudyAStarterProp
    {
        public Sprite Sprite;
        public StudyAStarterMetrics Metrics;
    }

    /// <summary>画像整合性、導入設定、目視確認、全体不足を別々に報告する。</summary>
    public sealed class StudyAStarterReadiness
    {
        public bool StarterImportReady => Errors.Count == 0 && AssetInfos.Count == 6;
        public bool DerivedPrepared;
        public bool SourceVisuallyReviewed;
        public bool DerivedVisuallyReviewed;
        public bool StarterPropsReady => StarterImportReady && DerivedPrepared && SourceVisuallyReviewed && DerivedVisuallyReviewed;
        public bool FullTargetReady => false;
        public readonly string[] MissingFullTargetRoles = { "ground", "water", "bridge", "hero", "flower" };
        public string Fingerprint;
        public string SourceManifestSha256;
        public readonly List<StudyAStarterMetrics> AssetInfos = new List<StudyAStarterMetrics>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public override string ToString()
        {
            return $"Study A starter: importReady={StarterImportReady}, propsReady={StarterPropsReady}, " +
                $"sourceReviewed={SourceVisuallyReviewed}, derivedReviewed={DerivedVisuallyReviewed}, " +
                $"fullTargetReady=false, missing={string.Join(",", MissingFullTargetRoles)}\n" +
                string.Join("\n", Errors.Select(e => "ERROR: " + e).Concat(Warnings.Select(w => "WARNING: " + w)));
        }
    }

    /// <summary>加工済みStarter原本を再加工せずコピーし、派生側だけにSprite設定を付ける。</summary>
    public static class StudyAStarterAssets
    {
        public const string DefaultInputRoot = OverheadStudyASettings.ArtRoot + "/Input/StarterPackage";
        public const string DefaultDerivedRoot = OverheadStudyASettings.ArtRoot + "/DerivedStarter";
        public const float PixelsPerUnit = 64f;
        public const int IntegerDownsample = 1;
        public const string SourceManifestFileName = "asset-manifest.json";
        public const string MetricsManifestFileName = "starter-metrics.json";
        public const string ImportMarkerFileName = "starter-import-marker.json";
        public const string SourceReviewFileName = "starter-source-review.json";
        // Unity 6.4の公開TextureImporter APIに記載されたplatform名。
        private static readonly string[] TexturePlatforms =
            { "Standalone", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo Switch", "tvOS" };

        private sealed class Spec
        {
            public readonly string Name, Role, Hash, Note;
            public readonly Vector2Int Size;
            public readonly int[] TopBounds;
            public readonly Vector2 Foot;
            public readonly Rect FootprintPixels, ColliderPixels;
            public Spec(string name, string role, string hash, int width, int height, int[] bounds,
                Vector2 foot, Rect footprint, Rect collider, string note)
            {
                Name = name; Role = role; Hash = hash; Size = new Vector2Int(width, height);
                TopBounds = bounds; Foot = foot; FootprintPixels = footprint; ColliderPixels = collider; Note = note;
            }
        }

        // Pixel Rectは左下原点。樹冠・屋根全体をCollisionに使わない。数値は6枚の実画像から個別指定。
        private static readonly Spec[] Specs =
        {
            new Spec("tree_large", "treeLarge", "24a9bc74865dead44cef7ebb02b351553df762e74cdfc2d4640c5b5e1dfd8250",
                256, 256, new[] {4,30,252,252}, new Vector2(128,8), new Rect(103,4,52,24), new Rect(117,9,22,14),
                "接地は樹冠下の根元。Collisionは幹と根の中央だけ。"),
            new Spec("tree_medium", "treeMedium", "eb8eef3d1baaa39efdacf2012fe0cee90120a831c148ded6de05ab9bda496c62",
                192, 192, new[] {4,37,188,188}, new Vector2(96,8), new Rect(80,4,32,20), new Rect(88,8,17,12),
                "小さい幹の根元で接地。葉の広がりは通行判定から除外。"),
            new Spec("fountain_low", "fountain", "06c165d419ca77a732dc9ef2f50217bd9e140ab933428d3844f21025eb54a10c",
                128, 128, new[] {4,9,124,124}, new Vector2(64,9), new Rect(10,7,108,92), new Rect(22,15,84,70),
                "低い円形水盤の前縁で接地。中央水盤の地面占有範囲をCollisionにする。"),
            new Spec("stall_roof", "shop", "1f9544c6b976e0964ef179350f7bd532692fc86dd7d62542b2539ba26576ba72",
                192, 160, new[] {4,9,188,156}, new Vector2(96,8), new Rect(12,4,168,36), new Rect(30,8,132,18),
                "下端の支柱・カウンターで接地。屋根と上段の布をCollisionにしない。"),
            new Spec("bush_cluster", "shrub", "23576de7986087b0e5a50141c6b2cb77bec8ccdad3552492c33cc14e4740f1d0",
                96, 64, new[] {4,14,92,60}, new Vector2(48,8), new Rect(12,6,72,22), Rect.zero,
                "下側の葉の根元で接地。Starterでは歩ける装飾としてCollisionなし。"),
            new Spec("rock_cluster", "rock", "259dad38eab20944373583d3313e6f77dc6fcb80de413efee16215fe8298b6e7",
                96, 64, new[] {4,5,92,60}, new Vector2(48,8), new Rect(8,5,80,32), new Rect(19,8,60,25),
                "下側の石の接地点で接地。突出した上側と苔を避けて下部石塊をCollisionにする。")
        };

        [Serializable] private sealed class RawManifest { public string status = null; public string reference = null; public RawAsset[] assets = null; }
        [Serializable] private sealed class RawAsset
        {
            public string name = null; public string file = null; public int[] canvas = null; public int[] visible_bounds = null;
            public string source_file = null; public int[] source_crop = null; public int alpha_threshold = 0;
            public int palette_max = 0; public int provisional_ppu = 0; public string pivot_note = null; public string sha256 = null;
        }
        [Serializable] private sealed class ReviewRecord
        {
            public bool visuallyReviewed;
            public string inputFingerprint;
            public string reviewer;
            public string evidence;
            public string reviewedAtUtc;
        }
        [Serializable] private sealed class DerivedRecord { public string name; public string sha256; public string metaSha256; }
        [Serializable] private sealed class ImportMarker
        {
            public int schemaVersion = 1;
            public string inputFingerprint;
            public ReviewRecord derivedReview;
            public DerivedRecord[] files;
        }
        [Serializable] private sealed class MetricsManifest
        {
            public int schemaVersion = 1;
            public string kind = "six-prop-starter-only";
            public float pixelsPerUnit = PixelsPerUnit;
            public int integerDownsample = IntegerDownsample;
            public bool starterImportReady;
            public bool derivedPrepared;
            public bool sourceVisuallyReviewed;
            public bool derivedVisuallyReviewed;
            public bool starterPropsReady;
            public bool fullTargetReady = false;
            public string[] missingFullTargetRoles;
            public string inputFingerprint;
            public string sourceManifestSha256;
            public string sourceManifestPath;
            public StudyAStarterMetrics[] assets;
        }

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Starter/Check Six Props (Read Only)")]
        public static void CheckReadinessMenu() => Debug.Log(CheckReadiness().ToString());

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Starter/Prepare Derived Six Props")]
        public static void PrepareMenu() => Debug.Log(PrepareDerivedAssets().ToString());

        public static StudyAStarterReadiness CheckReadiness(StudyAStarterSettings settings = null)
        {
            settings = settings ?? new StudyAStarterSettings();
            var report = new StudyAStarterReadiness();
            try
            {
                ValidateRoots(settings);
                var rawPath = Child(ProjectPath(settings.InputRoot), SourceManifestFileName);
                if (!File.Exists(rawPath)) throw new FileNotFoundException("Starter原本Manifestがありません。", rawPath);
                var rawBytes = File.ReadAllBytes(rawPath);
                var raw = JsonUtility.FromJson<RawManifest>(Encoding.UTF8.GetString(rawBytes).TrimStart('\uFEFF'));
                if (raw == null || raw.assets == null || raw.assets.Length != Specs.Length)
                    throw new InvalidDataException("加工済み6小物のassets配列が必要です。全地形Manifestとは別契約です。");
                report.SourceManifestSha256 = Hash(rawBytes);
                foreach (var spec in Specs)
                {
                    try
                    {
                        var entry = raw.assets.SingleOrDefault(a => a != null && a.name == spec.Name);
                        if (entry == null || entry.file != "Assets/" + spec.Name + ".png" || entry.sha256 != spec.Hash ||
                            entry.canvas == null || !entry.canvas.SequenceEqual(new[] {spec.Size.x,spec.Size.y}) ||
                            entry.visible_bounds == null || !entry.visible_bounds.SequenceEqual(spec.TopBounds) ||
                            entry.provisional_ppu != 64)
                            throw new InvalidDataException("原本Manifestのname/file/hash/寸法/可視範囲/PPUが承認Packageと違います。");
                        report.AssetInfos.Add(Inspect(spec, settings));
                    }
                    catch (Exception ex) { report.Errors.Add(spec.Name + ": " + ex.Message); }
                }
                if (!report.StarterImportReady) return report;
                report.Fingerprint = Fingerprint(rawBytes, report.AssetInfos, settings);
                var reviewPath = ProjectPath(settings.DerivedRoot + "/" + SourceReviewFileName);
                // Derived記録の破損で、正しい原本からの再準備まで拒否しない。
                try { report.SourceVisuallyReviewed = ReviewMatches(ReadJson<ReviewRecord>(reviewPath), report.Fingerprint); }
                catch (Exception ex) { report.Warnings.Add("Sourceレビュー記録が無効です: " + ex.Message); }
                try { CheckDerived(report, settings); }
                catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Warnings.Add("Derived記録が無効です: " + ex.Message); }
                if (!report.SourceVisuallyReviewed) report.Warnings.Add("Source目視記録が未確認です。実画像確認後の明示記録が必要です。");
                if (!report.DerivedVisuallyReviewed) report.Warnings.Add("Derived目視記録が未確認です。導入だけで目視済みにしません。");
                report.Warnings.Add("6小物だけの部分Kitです。ground/water/bridge/hero/flower不足、FullTargetReady=false。");
            }
            catch (Exception ex) { report.Errors.Add(ex.Message); }
            return report;
        }

        /// <summary>承認Packageの整合性を確認し、同一PNG bytesをDerivedだけへコピーする。レビューは更新しない。</summary>
        public static StudyAStarterReadiness PrepareDerivedAssets(StudyAStarterSettings settings = null)
        {
            settings = settings ?? new StudyAStarterSettings();
            var report = CheckReadiness(settings);
            if (!report.StarterImportReady) return report;
            try
            {
                var bytesByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var info in report.AssetInfos)
                {
                    // .metaへのlinkも全件、最初のwrite/ImportAssetより前に拒否する。
                    ProjectPath(info.DerivedPath); ProjectPath(info.DerivedPath + ".meta");
                    var bytes = File.ReadAllBytes(ProjectPath(info.SourcePath));
                    if (Hash(bytes) != info.SourceSha256) throw new IOException("準備中に原本が変わりました: " + info.Name);
                    bytesByName.Add(info.Name, bytes);
                }
                if (CheckReadiness(settings).Fingerprint != report.Fingerprint) throw new IOException("準備中にManifest/指標が変わりました。");
                ProjectPath(settings.DerivedRoot + "/" + ImportMarkerFileName);
                ProjectPath(settings.DerivedRoot + "/" + MetricsManifestFileName);
                Directory.CreateDirectory(ProjectPath(settings.DerivedRoot));
                // 途中失敗でも前回のDerived承認を残さない。Sourceレビューは別Fileで維持する。
                var marker = new ImportMarker { inputFingerprint = report.Fingerprint, files = Array.Empty<DerivedRecord>() };
                WriteJson(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
                var records = new List<DerivedRecord>();
                foreach (var info in report.AssetInfos)
                {
                    File.WriteAllBytes(ProjectPath(info.DerivedPath), bytesByName[info.Name]);
                    ConfigureImport(info);
                    records.Add(new DerivedRecord { name = info.Name, sha256 = info.SourceSha256,
                        metaSha256 = Hash(File.ReadAllBytes(ProjectPath(info.DerivedPath + ".meta"))) });
                }
                marker.files = records.ToArray();
                WriteJson(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
                report = CheckReadiness(settings);
                WriteMetrics(report, settings);
            }
            catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Errors.Add("Derived準備失敗: " + ex.Message); }
            return report;
        }

        /// <summary>Source実画像を見た担当者が、証拠を明示して記録する。自動導入からは呼ばない。</summary>
        public static StudyAStarterReadiness RecordSourceVisualReview(string reviewer, string evidence, StudyAStarterSettings settings = null)
        {
            settings = settings ?? new StudyAStarterSettings();
            var report = CheckReadiness(settings);
            if (!report.StarterImportReady) throw new InvalidOperationException(report.ToString());
            ValidateReviewText(reviewer, evidence);
            Directory.CreateDirectory(ProjectPath(settings.DerivedRoot));
            WriteJson(settings.DerivedRoot + "/" + SourceReviewFileName, NewReview(report.Fingerprint, reviewer, evidence));
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        /// <summary>導入後Sprite実画像を見た担当者が、現在bytes/metaに限るレビューを記録する。</summary>
        public static StudyAStarterReadiness RecordDerivedVisualReview(string reviewer, string evidence, StudyAStarterSettings settings = null)
        {
            settings = settings ?? new StudyAStarterSettings();
            var report = CheckReadiness(settings);
            if (!report.StarterImportReady || !report.DerivedPrepared || !report.SourceVisuallyReviewed)
                throw new InvalidOperationException("Sourceレビューと有効Derivedが必要です。\n" + report);
            ValidateReviewText(reviewer, evidence);
            var marker = ReadJson<ImportMarker>(ProjectPath(settings.DerivedRoot + "/" + ImportMarkerFileName));
            marker.derivedReview = NewReview(report.Fingerprint, reviewer, evidence);
            WriteJson(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        /// <summary>両方の目視記録が現在の6素材に一致するときだけ、Scene用Sprite/物理指標を返す。</summary>
        public static Dictionary<string, StudyAStarterProp> Load(StudyAStarterSettings settings = null)
        {
            settings = settings ?? new StudyAStarterSettings();
            var report = CheckReadiness(settings);
            if (!report.StarterPropsReady) throw new InvalidOperationException(report.ToString());
            var result = new Dictionary<string, StudyAStarterProp>(StringComparer.Ordinal);
            foreach (var info in report.AssetInfos)
                result.Add(info.Role, new StudyAStarterProp { Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath), Metrics = info });
            return result;
        }

        private static StudyAStarterMetrics Inspect(Spec spec, StudyAStarterSettings settings)
        {
            var sourcePath = settings.InputRoot + "/Assets/" + spec.Name + ".png";
            var bytes = File.ReadAllBytes(ProjectPath(sourcePath));
            if (Hash(bytes) != spec.Hash) throw new InvalidDataException("原本SHA256が承認Packageと違います。");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 || !texture.LoadImage(bytes, false))
                    throw new InvalidDataException("PNGを読めません。");
                if (texture.width != spec.Size.x || texture.height != spec.Size.y) throw new InvalidDataException("native寸法が違います。");
                var pixels = texture.GetPixels32();
                var minX = texture.width; var minY = texture.height; var maxX = -1; var maxY = -1;
                for (var y = 0; y < texture.height; y++) for (var x = 0; x < texture.width; x++)
                {
                    var alpha = pixels[y * texture.width + x].a;
                    if (alpha != 0 && alpha != 255) throw new InvalidDataException("承認加工済みPackageはbinary alphaが必要です。");
                    if (alpha == 0) continue;
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
                var topBounds = new[] { minX, texture.height - maxY - 1, maxX + 1, texture.height - minY };
                if (!topBounds.SequenceEqual(spec.TopBounds)) throw new InvalidDataException("実alpha範囲がManifestと違います。");
                return new StudyAStarterMetrics
                {
                    Name = spec.Name, Role = spec.Role, NativeSize = spec.Size, DerivedSize = spec.Size,
                    PixelPivot = spec.Foot, Pivot = new Vector2(spec.Foot.x / spec.Size.x, spec.Foot.y / spec.Size.y),
                    WorldSize = new Vector2(spec.Size.x / PixelsPerUnit, spec.Size.y / PixelsPerUnit),
                    LocalFootprint = LocalRect(spec.FootprintPixels, spec.Foot),
                    LocalCollider = spec.ColliderPixels == Rect.zero ? Rect.zero : LocalRect(spec.ColliderPixels, spec.Foot),
                    AlphaBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    SourceSha256 = spec.Hash, SourcePath = sourcePath,
                    DerivedPath = settings.DerivedRoot + "/" + spec.Name + ".png", GroundingNote = spec.Note
                };
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void ConfigureImport(StudyAStarterMetrics info)
        {
            AssetDatabase.ImportAsset(info.DerivedPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Derived TextureImporterがありません: " + info.DerivedPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192; importer.anisoLevel = 0; importer.sRGBTexture = true;
            foreach (var platform in TexturePlatforms)
                if (importer.GetPlatformTextureSettings(platform).overridden) importer.ClearPlatformTextureSettings(platform);
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect; textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = info.Pivot; importer.SetTextureSettings(textureSettings); importer.SaveAndReimport();
        }

        private static void CheckDerived(StudyAStarterReadiness report, StudyAStarterSettings settings)
        {
            var marker = ReadJson<ImportMarker>(ProjectPath(settings.DerivedRoot + "/" + ImportMarkerFileName));
            if (marker == null || marker.schemaVersion != 1 || marker.inputFingerprint != report.Fingerprint ||
                marker.files == null || marker.files.Length != Specs.Length)
            { report.Warnings.Add("現在の6素材に一致するDerived markerがありません。"); return; }
            foreach (var info in report.AssetInfos)
            {
                var record = marker.files.SingleOrDefault(f => f != null && f.name == info.Name);
                var path = ProjectPath(info.DerivedPath);
                var metaPath = ProjectPath(info.DerivedPath + ".meta");
                if (record == null || record.sha256 != info.SourceSha256 || !File.Exists(path) ||
                    Hash(File.ReadAllBytes(path)) != record.sha256 || !File.Exists(metaPath) ||
                    Hash(File.ReadAllBytes(metaPath)) != record.metaSha256)
                { report.Warnings.Add("Derived bytes/metaが現在のmarkerと違います: " + info.Name); return; }
                var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath);
                var nativeRect = new Rect(0, 0, info.NativeSize.x, info.NativeSize.y);
                if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.spritePixelsPerUnit != PixelsPerUnit || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression ||
                    importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp ||
                    importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.sRGBTexture || importer.anisoLevel != 0 ||
                    importer.maxTextureSize < Math.Max(info.NativeSize.x, info.NativeSize.y) ||
                    sprite == null || sprite.packed || sprite.rect != nativeRect || sprite.textureRect != nativeRect || sprite.texture == null ||
                    sprite.texture.format != TextureFormat.RGBA32 ||
                    sprite.texture.width != info.NativeSize.x || sprite.texture.height != info.NativeSize.y ||
                    Math.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > 0.00001f || Vector2.Distance(sprite.pivot, info.PixelPivot) > 0.0001f)
                { report.Warnings.Add("Derived寸法/Point/PPU/透過設定が契約と違います: " + info.Name); return; }
                if (TexturePlatforms.Any(platform => importer.GetPlatformTextureSettings(platform).overridden))
                { report.Warnings.Add("Derivedにplatform overrideが残っています: " + info.Name); return; }
                var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
                if (textureSettings.spriteMeshType != SpriteMeshType.FullRect || textureSettings.spriteAlignment != (int)SpriteAlignment.Custom ||
                    Vector2.Distance(importer.spritePivot, info.Pivot) > 0.00001f)
                { report.Warnings.Add("Derived FullRect/custom pivotが契約と違います: " + info.Name); return; }
            }
            report.DerivedPrepared = true;
            report.DerivedVisuallyReviewed = ReviewMatches(marker.derivedReview, report.Fingerprint);
        }

        private static void WriteMetrics(StudyAStarterReadiness report, StudyAStarterSettings settings)
        {
            WriteJson(settings.DerivedRoot + "/" + MetricsManifestFileName, new MetricsManifest
            {
                starterImportReady = report.StarterImportReady, derivedPrepared = report.DerivedPrepared,
                sourceVisuallyReviewed = report.SourceVisuallyReviewed, derivedVisuallyReviewed = report.DerivedVisuallyReviewed,
                starterPropsReady = report.StarterPropsReady, missingFullTargetRoles = report.MissingFullTargetRoles,
                inputFingerprint = report.Fingerprint, sourceManifestSha256 = report.SourceManifestSha256,
                sourceManifestPath = settings.InputRoot + "/" + SourceManifestFileName, assets = report.AssetInfos.ToArray()
            });
        }
        private static Rect LocalRect(Rect pixelRect, Vector2 foot) => new Rect((pixelRect.x - foot.x) / PixelsPerUnit,
            (pixelRect.y - foot.y) / PixelsPerUnit, pixelRect.width / PixelsPerUnit, pixelRect.height / PixelsPerUnit);
        private static string Fingerprint(byte[] manifest, IEnumerable<StudyAStarterMetrics> infos, StudyAStarterSettings settings)
        {
            var text = new StringBuilder("StudyAStarter-native-copy-v1\n64\n1\n");
            text.AppendLine(Hash(manifest)); text.AppendLine(settings.InputRoot); text.AppendLine(settings.DerivedRoot);
            foreach (var info in infos.OrderBy(i => i.Name, StringComparer.Ordinal)) text.AppendLine(JsonUtility.ToJson(info));
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        private static bool ReviewMatches(ReviewRecord review, string fingerprint) => review != null && review.visuallyReviewed &&
            review.inputFingerprint == fingerprint && !string.IsNullOrWhiteSpace(review.reviewer) &&
            !string.IsNullOrWhiteSpace(review.evidence) && !string.IsNullOrWhiteSpace(review.reviewedAtUtc);
        private static ReviewRecord NewReview(string fingerprint, string reviewer, string evidence) => new ReviewRecord
        {
            visuallyReviewed = true, inputFingerprint = fingerprint, reviewer = reviewer, evidence = evidence,
            reviewedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        private static void ValidateReviewText(string reviewer, string evidence)
        {
            if (string.IsNullOrWhiteSpace(reviewer) || string.IsNullOrWhiteSpace(evidence))
                throw new ArgumentException("実画像を見た担当者名と確認証拠を明示してください。自動レビューは行いません。");
        }
        private static T ReadJson<T>(string path) where T : class => File.Exists(path)
            ? JsonUtility.FromJson<T>(File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF')) : null;
        private static void WriteJson<T>(string relative, T value) => File.WriteAllText(ProjectPath(relative), JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static void ValidateRoots(StudyAStarterSettings settings)
        {
            var art = ProjectPath(OverheadStudyASettings.ArtRoot);
            var input = ProjectPath(settings.InputRoot); var derived = ProjectPath(settings.DerivedRoot);
            if (!Within(art, input) || !Within(art, derived) || Within(input, derived) || Within(derived, input))
                throw new InvalidDataException("InputとDerivedStarterは独立したStudy A配下に分離してください。");
        }
        private static string ProjectPath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Project相対Pathが必要です。");
            return Child(Directory.GetParent(Application.dataPath).FullName, relative);
        }
        private static string Child(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("相対Pathが必要です。");
            var result = Path.GetFullPath(Path.Combine(root, relative));
            if (!Within(root, result) || result.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Pathが指定Rootを外れています。");
            for (var path = result; !path.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase); path = Path.GetDirectoryName(path))
                if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Source/Derivedはsymlink/junctionを使いません。");
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
