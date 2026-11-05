using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyABridgeRiverSettings
    {
        public string InputRoot = StudyABridgeRiverAssets.DefaultInputRoot;
        public string DerivedRoot = StudyABridgeRiverAssets.DefaultDerivedRoot;
    }

    [Serializable]
    public sealed class StudyABridgeRiverMetrics
    {
        public string Name, Role, SourceSha256, SourcePath, DerivedPath, GroundingNote;
        public Vector2Int NativeSize, DerivedSize;
        public Vector2 PixelPivot, Pivot, WorldSize;
        public RectInt AlphaBounds;
        public Rect LocalVisualBounds, LocalFootprint, LocalCollider;
        // Metadata only: the builder preserves the existing four manual post boxes.
        public bool ColliderEnabled;
        public TextureWrapMode WrapMode;
    }

    public sealed class StudyABridgeRiverSpriteSet
    {
        public Sprite Water, Deck, Post, BankLarge, BankSmall;
        public Dictionary<string, Sprite> ByName;
        public Dictionary<string, StudyABridgeRiverMetrics> Metrics;
        public string Fingerprint;
    }

    public sealed class StudyABridgeRiverReadiness
    {
        public bool BridgeRiverImportReady => Errors.Count == 0 && AssetInfos.Count == 5;
        public bool DerivedPrepared, SourceVisuallyReviewed, DerivedVisuallyReviewed;
        public bool BridgeRiverReady => BridgeRiverImportReady && DerivedPrepared && SourceVisuallyReviewed && DerivedVisuallyReviewed;
        public bool FullTargetReady => false;
        public string Fingerprint, SourceManifestSha256;
        public readonly List<StudyABridgeRiverMetrics> AssetInfos = new List<StudyABridgeRiverMetrics>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public override string ToString() =>
            $"Study A bridge/river: importReady={BridgeRiverImportReady}, bridgeRiverReady={BridgeRiverReady}, " +
            $"sourceReviewed={SourceVisuallyReviewed}, derivedReviewed={DerivedVisuallyReviewed}, fullTargetReady=false\n" +
            string.Join("\n", Errors.Select(e => "ERROR: " + e).Concat(Warnings.Select(w => "WARNING: " + w)));
    }

    /// <summary>Imports the five approved runtime PNGs without processing or modifying any source importer.</summary>
    public static class StudyABridgeRiverAssets
    {
        public const string DefaultInputRoot = OverheadStudyASettings.ArtRoot + "/Input/BridgeRiverV1";
        public const string DefaultDerivedRoot = OverheadStudyASettings.ArtRoot + "/DerivedBridgeRiverV1";
        public const string SourceManifestFileName = "manifest.json";
        public const string SourceReviewFileName = "bridge-river-source-review.json";
        public const string ImportMarkerFileName = "bridge-river-import-marker.json";
        public const string MetricsManifestFileName = "bridge-river-metrics.json";
        public const string ApprovedManifestSha256 = "6391943338efc42a1922bf476c47b95fc66a53a73eba231c722da5239bc2284b";
        public const float PixelsPerUnit = 64;
        private const string MarkerKind = "bridge-river-v1-native-copy";
        private const string MetricsKind = "bridge-river-v1-only";
        private static readonly string[] Platforms = { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps", "tvOS", "PS4", "XboxOne", "Nintendo Switch" };

        private sealed class Spec
        {
            public readonly string Name, Role, Hash;
            public readonly Vector2Int Size;
            public readonly Vector2 PixelPivot;
            public readonly int[] TopBounds, Alpha;
            public readonly int Palette;
            public readonly TextureWrapMode Wrap;
            public Spec(string name, string role, string hash, int w, int h, float px, float py,
                int left, int top, int right, int bottom, int palette, bool opaque, TextureWrapMode wrap)
            {
                Name = name; Role = role; Hash = hash; Size = new Vector2Int(w, h); PixelPivot = new Vector2(px, py);
                TopBounds = new[] { left, top, right, bottom }; Palette = palette; Alpha = opaque ? new[] { 255 } : new[] { 0, 255 }; Wrap = wrap;
            }
        }
        private static readonly Spec[] Specs =
        {
            new Spec("bridge_deck", "deck", "1f45f12fd1558af87ff0bf4da1c33fb7b30dfec083d392d2319ead4099356468", 256, 192, 128, 0, 0, 0, 256, 192, 20, true, TextureWrapMode.Clamp),
            new Spec("bridge_post", "post", "b935e2a45e71d499940dacaa6fc2531d4e1b77633a6da749317ad87a557ba59c", 16, 48, 8, 0, 0, 0, 16, 48, 16, false, TextureWrapMode.Clamp),
            new Spec("water_01", "water", "a5b233182b4ce2404863c859bf773514d35fc3fefd33a7ee2c80fc05bf5bb243", 64, 64, 32, 32, 0, 0, 64, 64, 10, true, TextureWrapMode.Repeat),
            new Spec("riverbank_large", "bank", "aedca0eab5a3f64fac58e4880411826a05294b3a478ea165a7edef2ce4cc8b9a", 64, 64, 32, 8, 2, 2, 62, 62, 32, false, TextureWrapMode.Clamp),
            new Spec("riverbank_small", "bank", "38ee81c266379e6c2a41a555091a5a696c0db6972cd585adcb800de9191a610d", 32, 48, 16, 4, 2, 2, 30, 46, 20, false, TextureWrapMode.Clamp),
        };
        [Serializable] private sealed class RawManifest
        {
            public string package = null, generated_with = null, filter_mode = null, texture_format = null, compression = null, mesh_type = null, color_space = null, pivot_coordinate_system = null;
            public int asset_count = 0, ppu = 0; public bool mipmaps = true; public RawAsset[] assets = null; public RawLayout layout_contract = null;
        }
        [Serializable] private sealed class RawAsset
        {
            public string file = null, role = null, wrap_mode = null, sha256 = null;
            public int[] size_px = null, visible_bounds_png_ltrb = null, visible_bounds_bottom_left_xywh = null, alpha_values = null;
            public float[] world_size = null, pivot_px = null, pivot_normalized = null; public int opaque_color_count = 0;
        }
        [Serializable] private sealed class RawLayout
        {
            public int river_width_world = 0; public int[] bridge_walkable_rect_world = null;
            public string bridge_axis = null; public bool deck_has_no_posts = false, deck_rect_filled = false;
        }
        [Serializable] private sealed class Review
        {
            public bool visuallyReviewed; public string inputFingerprint, reviewer, evidence, reviewedAtUtc;
        }
        [Serializable] private sealed class FileRecord { public string name, sha256, metaSha256; }
        [Serializable] private sealed class Marker
        {
            public int schemaVersion = 1; public string kind = MarkerKind, inputFingerprint; public Review derivedReview; public FileRecord[] files;
        }
        [Serializable] private sealed class MetricsManifest
        {
            public int schemaVersion = 1; public string kind = MetricsKind;
            public float pixelsPerUnit = PixelsPerUnit; public int integerDownsample = 1;
            public bool bridgeRiverImportReady, derivedPrepared, sourceVisuallyReviewed, derivedVisuallyReviewed, bridgeRiverReady;
            public bool fullTargetReady = false; public string inputFingerprint, sourceManifestSha256, sourceManifestPath;
            public StudyABridgeRiverMetrics[] assets;
        }

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Bridge River/Check Five Sprites (Read Only)")]
        public static void CheckReadinessMenu() => Debug.Log(CheckReadiness().ToString());
        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Bridge River/Prepare Derived Five Sprites")]
        public static void PrepareMenu() => Debug.Log(PrepareDerived().ToString());

        public static StudyABridgeRiverReadiness CheckReadiness(StudyABridgeRiverSettings settings = null)
        {
            settings = settings ?? new StudyABridgeRiverSettings(); var report = new StudyABridgeRiverReadiness();
            try
            {
                ValidateRoots(settings);
                var manifest = File.ReadAllBytes(PathFor(settings.InputRoot + "/" + SourceManifestFileName));
                report.SourceManifestSha256 = StudyATerrainAssets.Hash(manifest);
                if (report.SourceManifestSha256 != ApprovedManifestSha256) throw new InvalidDataException("Bridge/river manifest differs from the approved runtime original.");
                ValidatePackageFile(settings, "README.txt", "2ceac192fa32eebe79800c2bec02910af35e91540dc779159dfd76078f8fb4dc");
                ValidatePackageFile(settings, "validation-report.json", "871e15307ad7aaeefa67b8d0627f9022c89467b4ac9f73f14e56a21d536da357");
                ValidateManifest(JsonUtility.FromJson<RawManifest>(Encoding.UTF8.GetString(manifest).TrimStart('\uFEFF')));
                foreach (var spec in Specs) report.AssetInfos.Add(Inspect(spec, settings));
                var fp = new StringBuilder("StudyABridgeRiver-native-copy-v1\n64\n1\nmanual-post-.25-.25-offset-.125\n");
                fp.AppendLine(ApprovedManifestSha256); fp.AppendLine(settings.InputRoot); fp.AppendLine(settings.DerivedRoot);
                foreach (var info in report.AssetInfos.OrderBy(i => i.Name, StringComparer.Ordinal)) fp.AppendLine(JsonUtility.ToJson(info));
                report.Fingerprint = StudyATerrainAssets.Hash(Encoding.UTF8.GetBytes(fp.ToString()));
                try { report.SourceVisuallyReviewed = Matches(Read<Review>(PathFor(settings.DerivedRoot + "/" + SourceReviewFileName)), report.Fingerprint); }
                catch (Exception ex) { report.Warnings.Add("Bridge/river source review record is invalid: " + ex.Message); }
                try { CheckDerived(report, settings); }
                catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Warnings.Add("Bridge/river Derived state is invalid: " + ex.Message); }
                if (!report.SourceVisuallyReviewed) report.Warnings.Add("All five actual native PNGs require an explicit source image review.");
                if (!report.DerivedVisuallyReviewed) report.Warnings.Add("Actual imported sprites require an explicit derived image/import review.");
                report.Warnings.Add("Five-material kit only: two bank stamps are not a continuous shoreline/foam kit; source artwork/prompts are excluded from this runtime ZIP.");
            }
            catch (Exception ex) { report.Errors.Add(ex.Message); }
            return report;
        }

        public static StudyABridgeRiverReadiness PrepareDerived(StudyABridgeRiverSettings settings = null)
        {
            settings = settings ?? new StudyABridgeRiverSettings(); var report = CheckReadiness(settings);
            if (!report.BridgeRiverImportReady) return report;
            try
            {
                var bytes = report.AssetInfos.ToDictionary(i => i.Name, i => File.ReadAllBytes(PathFor(i.SourcePath)), StringComparer.Ordinal);
                foreach (var info in report.AssetInfos) if (StudyATerrainAssets.Hash(bytes[info.Name]) != info.SourceSha256) throw new IOException("Bridge/river input changed during preparation: " + info.Name);
                var recheck = CheckReadiness(settings);
                if (!recheck.BridgeRiverImportReady || recheck.Fingerprint != report.Fingerprint) throw new IOException("Bridge/river manifest or native metrics changed during preparation.");
                Preflight(report, settings); Directory.CreateDirectory(PathFor(settings.DerivedRoot));
                var marker = new Marker { inputFingerprint = report.Fingerprint, files = Array.Empty<FileRecord>() };
                Write(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
                var files = new List<FileRecord>();
                foreach (var info in report.AssetInfos)
                {
                    File.WriteAllBytes(PathFor(info.DerivedPath), bytes[info.Name]); ConfigureImport(info);
                    files.Add(new FileRecord { name = info.Name, sha256 = info.SourceSha256,
                        metaSha256 = StudyATerrainAssets.Hash(File.ReadAllBytes(PathFor(info.DerivedPath + ".meta"))) });
                }
                marker.files = files.ToArray(); Write(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
                report = CheckReadiness(settings); WriteMetrics(report, settings);
            }
            catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Errors.Add("Bridge/river Derived preparation failed: " + ex.Message); }
            return report;
        }

        public static StudyABridgeRiverReadiness RecordSourceVisualReview(string reviewer, string evidence, StudyABridgeRiverSettings settings = null)
        {
            settings = settings ?? new StudyABridgeRiverSettings(); StudyATerrainAssets.ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings); if (!report.BridgeRiverImportReady) throw new InvalidOperationException(report.ToString());
            Preflight(report, settings); Directory.CreateDirectory(PathFor(settings.DerivedRoot));
            Write(settings.DerivedRoot + "/" + SourceReviewFileName, NewReview(report.Fingerprint, reviewer, evidence));
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        public static StudyABridgeRiverReadiness RecordDerivedVisualReview(string reviewer, string evidence, StudyABridgeRiverSettings settings = null)
        {
            settings = settings ?? new StudyABridgeRiverSettings(); StudyATerrainAssets.ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings);
            if (!report.BridgeRiverImportReady || !report.DerivedPrepared || !report.SourceVisuallyReviewed) throw new InvalidOperationException(report.ToString());
            Preflight(report, settings);
            var marker = Read<Marker>(PathFor(settings.DerivedRoot + "/" + ImportMarkerFileName));
            marker.derivedReview = NewReview(report.Fingerprint, reviewer, evidence); Write(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        public static StudyABridgeRiverSpriteSet LoadSpriteSet(StudyABridgeRiverSettings settings = null)
        {
            var report = CheckReadiness(settings); if (!report.BridgeRiverReady) throw new InvalidOperationException(report.ToString());
            var byName = report.AssetInfos.ToDictionary(i => i.Name, i => AssetDatabase.LoadAssetAtPath<Sprite>(i.DerivedPath), StringComparer.Ordinal);
            return new StudyABridgeRiverSpriteSet
            {
                Water = byName["water_01"], Deck = byName["bridge_deck"], Post = byName["bridge_post"],
                BankLarge = byName["riverbank_large"], BankSmall = byName["riverbank_small"], ByName = byName,
                Metrics = report.AssetInfos.ToDictionary(i => i.Name, StringComparer.Ordinal), Fingerprint = report.Fingerprint
            };
        }

        private static StudyABridgeRiverMetrics Inspect(Spec spec, StudyABridgeRiverSettings settings)
        {
            var source = settings.InputRoot + "/Assets/" + spec.Name + ".png";
            var pixels = StudyATerrainAssets.ReadPng(PathFor(source), spec.Hash, spec.Size);
            var minX = spec.Size.x; var minY = spec.Size.y; var maxX = -1; var maxY = -1;
            var palette = new HashSet<uint>(); var alpha = new HashSet<int>();
            for (var y = 0; y < spec.Size.y; y++) for (var x = 0; x < spec.Size.x; x++)
            {
                var p = pixels[y * spec.Size.x + x]; alpha.Add(p.a);
                if (p.a == 0)
                {
                    if (p.r != 0 || p.g != 0 || p.b != 0) throw new InvalidDataException("Transparent native RGB must remain zero: " + spec.Name);
                    continue;
                }
                palette.Add((uint)((p.r << 16) | (p.g << 8) | p.b));
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            var topBounds = new[] { minX, spec.Size.y - maxY - 1, maxX + 1, spec.Size.y - minY };
            if (!topBounds.SequenceEqual(spec.TopBounds) || !alpha.OrderBy(a => a).SequenceEqual(spec.Alpha) || palette.Count != spec.Palette)
                throw new InvalidDataException("Bridge/river native alpha bounds/palette differ: " + spec.Name);
            if (spec.Role == "water") for (var i = 0; i < 64; i++)
                if (!pixels[i].Equals(pixels[63 * 64 + i]) || !pixels[i * 64].Equals(pixels[i * 64 + 63]))
                    throw new InvalidDataException("Water opposite native RGBA edges must be exact.");
            var visual = new Rect((minX - spec.PixelPivot.x) / PixelsPerUnit, (minY - spec.PixelPivot.y) / PixelsPerUnit,
                (maxX - minX + 1) / PixelsPerUnit, (maxY - minY + 1) / PixelsPerUnit);
            var post = spec.Role == "post";
            return new StudyABridgeRiverMetrics
            {
                Name = spec.Name, Role = spec.Role, NativeSize = spec.Size, DerivedSize = spec.Size, PixelPivot = spec.PixelPivot,
                Pivot = new Vector2(spec.PixelPivot.x / spec.Size.x, spec.PixelPivot.y / spec.Size.y),
                WorldSize = new Vector2(spec.Size.x / PixelsPerUnit, spec.Size.y / PixelsPerUnit),
                AlphaBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1), LocalVisualBounds = visual,
                LocalFootprint = post ? new Rect(-.125f, 0, .25f, .25f) : visual,
                LocalCollider = post ? new Rect(-.125f, 0, .25f, .25f) : Rect.zero, ColliderEnabled = post, WrapMode = spec.Wrap,
                SourceSha256 = spec.Hash, SourcePath = source, DerivedPath = settings.DerivedRoot + "/" + spec.Name + ".png",
                GroundingNote = post ? "Builder-owned existing manual post box (.25,.25), offset(0,.125); the imported sprite has no generated/custom physics shape." :
                    "Appearance only, no sprite collider/physics shape. Deck is ground below Actor; water blocking remains the existing logical water mask; banks are passable details."
            };
        }

        private static StudyATerrainMetrics ImportMetrics(StudyABridgeRiverMetrics info) => new StudyATerrainMetrics
        { Name = info.Name, NativeSize = info.NativeSize, DerivedSize = info.DerivedSize, PixelPivot = info.PixelPivot, Pivot = info.Pivot, DerivedPath = info.DerivedPath };
        private static void ConfigureImport(StudyABridgeRiverMetrics info)
        {
            StudyATerrainAssets.ConfigureImport(ImportMetrics(info));
            var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
            importer.wrapMode = info.WrapMode;
            var ts = new TextureImporterSettings(); importer.ReadTextureSettings(ts);
            ts.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(ts); importer.SaveAndReimport();
        }
        private static bool SpriteContractMatches(StudyABridgeRiverMetrics info)
        {
            var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath); var rect = new Rect(0, 0, info.NativeSize.x, info.NativeSize.y);
            if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.spritePixelsPerUnit != PixelsPerUnit || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression ||
                importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency || importer.wrapMode != info.WrapMode ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.sRGBTexture || importer.anisoLevel != 0 ||
                importer.maxTextureSize < Math.Max(info.NativeSize.x, info.NativeSize.y) || importer.GetDefaultPlatformTextureSettings().format != TextureImporterFormat.RGBA32 ||
                Platforms.Any(p => importer.GetPlatformTextureSettings(p).overridden) || sprite == null || sprite.packed || sprite.rect != rect || sprite.textureRect != rect ||
                sprite.texture == null || sprite.texture.format != TextureFormat.RGBA32 || sprite.texture.width != info.NativeSize.x || sprite.texture.height != info.NativeSize.y ||
                Math.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > .00001f || Vector2.Distance(sprite.pivot, info.PixelPivot) > .0001f || sprite.GetPhysicsShapeCount() != 0) return false;
            var ts = new TextureImporterSettings(); importer.ReadTextureSettings(ts);
            return ts.spriteMeshType == SpriteMeshType.FullRect && ts.spriteAlignment == (int)SpriteAlignment.Custom &&
                Vector2.Distance(importer.spritePivot, info.Pivot) <= .00001f && !ts.spriteGenerateFallbackPhysicsShape;
        }
        private static void CheckDerived(StudyABridgeRiverReadiness report, StudyABridgeRiverSettings settings)
        {
            var marker = Read<Marker>(PathFor(settings.DerivedRoot + "/" + ImportMarkerFileName));
            if (marker == null || marker.schemaVersion != 1 || marker.kind != MarkerKind || marker.inputFingerprint != report.Fingerprint || marker.files == null || marker.files.Length != 5)
            { report.Warnings.Add("No prepared bridge/river marker for the current five assets."); return; }
            foreach (var info in report.AssetInfos)
            {
                var file = marker.files.SingleOrDefault(f => f != null && f.name == info.Name);
                if (file == null || file.sha256 != info.SourceSha256 || !File.Exists(PathFor(info.DerivedPath)) ||
                    StudyATerrainAssets.Hash(File.ReadAllBytes(PathFor(info.DerivedPath))) != file.sha256 || !File.Exists(PathFor(info.DerivedPath + ".meta")) ||
                    StudyATerrainAssets.Hash(File.ReadAllBytes(PathFor(info.DerivedPath + ".meta"))) != file.metaSha256)
                { report.Warnings.Add("Bridge/river Derived PNG/meta changed: " + info.Name); return; }
                if (!SpriteContractMatches(info)) { report.Warnings.Add("Bridge/river native/import/physics-shape contract differs: " + info.Name); return; }
            }
            report.DerivedPrepared = true; report.DerivedVisuallyReviewed = Matches(marker.derivedReview, report.Fingerprint);
        }

        private static void ValidatePackageFile(StudyABridgeRiverSettings settings, string name, string expectedHash)
        {
            if (StudyATerrainAssets.Hash(File.ReadAllBytes(PathFor(settings.InputRoot + "/" + name))) != expectedHash)
                throw new InvalidDataException("Bridge/river runtime package document differs: " + name);
        }
        private static void ValidateManifest(RawManifest raw)
        {
            if (raw == null || raw.package != "FantasyRoyale A bridge and river v1" || raw.generated_with != "built-in image_gen" || raw.asset_count != 5 || raw.ppu != 64 ||
                raw.filter_mode != "Point" || raw.texture_format != "RGBA32" || raw.compression != "None" || raw.mipmaps || raw.mesh_type != "FullRect" || raw.color_space != "sRGB" ||
                raw.pivot_coordinate_system != "pixels from bottom-left of full texture" || raw.assets == null || raw.assets.Length != 5)
                throw new InvalidDataException("The exact approved five-material bridge/river manifest is required.");
            var layout = raw.layout_contract;
            if (layout == null || layout.river_width_world != 2 || layout.bridge_walkable_rect_world == null || !layout.bridge_walkable_rect_world.SequenceEqual(new[] { 4, 3 }) ||
                layout.bridge_axis != "east-west over north-south river" || !layout.deck_has_no_posts || !layout.deck_rect_filled) throw new InvalidDataException("Bridge/river layout declaration differs.");
            foreach (var spec in Specs)
            {
                var a = raw.assets.SingleOrDefault(i => i != null && i.file == "Assets/" + spec.Name + ".png");
                var bl = new[] { spec.TopBounds[0], spec.Size.y - spec.TopBounds[3], spec.TopBounds[2] - spec.TopBounds[0], spec.TopBounds[3] - spec.TopBounds[1] };
                if (a == null || a.role != spec.Role || a.sha256 != spec.Hash || a.wrap_mode != spec.Wrap.ToString() || a.opaque_color_count != spec.Palette ||
                    a.size_px == null || !a.size_px.SequenceEqual(new[] { spec.Size.x, spec.Size.y }) || a.alpha_values == null || !a.alpha_values.SequenceEqual(spec.Alpha) ||
                    a.visible_bounds_png_ltrb == null || !a.visible_bounds_png_ltrb.SequenceEqual(spec.TopBounds) ||
                    a.visible_bounds_bottom_left_xywh == null || !a.visible_bounds_bottom_left_xywh.SequenceEqual(bl) ||
                    a.pivot_px == null || !a.pivot_px.SequenceEqual(new[] { spec.PixelPivot.x, spec.PixelPivot.y }) ||
                    !PairMatches(a.world_size, new Vector2(spec.Size.x / PixelsPerUnit, spec.Size.y / PixelsPerUnit)) ||
                    !PairMatches(a.pivot_normalized, new Vector2(spec.PixelPivot.x / spec.Size.x, spec.PixelPivot.y / spec.Size.y)))
                    throw new InvalidDataException("Bridge/river native metrics declaration differs: " + spec.Name);
            }
        }
        private static bool PairMatches(float[] value, Vector2 expected) => value != null && value.Length == 2 &&
            Vector2.Distance(new Vector2(value[0], value[1]), expected) <= .00001f;
        private static void ValidateRoots(StudyABridgeRiverSettings settings)
        {
            StudyATerrainAssets.ValidateRoots(new StudyATerrainSettings { InputRoot = settings.InputRoot, DerivedRoot = settings.DerivedRoot });
            var derived = PathFor(settings.DerivedRoot).TrimEnd(Path.DirectorySeparatorChar);
            foreach (var root in new[] { OverheadStudyASettings.ArtRoot + "/Input", OverheadStudyASettings.ArtRoot + "/Reference", StudyATerrainAssets.R1InputRoot,
                StudyATerrainAssets.R1DerivedRoot, StudyATerrainAssets.R2InputRoot, StudyATerrainAssets.R2DerivedRoot, StudyAStarterAssets.DefaultInputRoot,
                StudyAStarterAssets.DefaultDerivedRoot, StudyAForestDetailAssets.DefaultInputRoot, StudyAForestDetailAssets.DefaultDerivedRoot })
            {
                var protectedPath = PathFor(root).TrimEnd(Path.DirectorySeparatorChar);
                if (Overlaps(derived, protectedPath)) throw new InvalidDataException("Bridge/river Derived overlaps preserved source, reference, ground, Hero, starter or forest details.");
            }
        }
        private static bool Overlaps(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static void Preflight(StudyABridgeRiverReadiness report, StudyABridgeRiverSettings settings)
        {
            ValidateRoots(settings); var root = PathFor(settings.DerivedRoot);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in Specs.Select(s => s.Name + ".png").Concat(new[] { SourceReviewFileName, ImportMarkerFileName, MetricsManifestFileName }))
            { allowed.Add(name); allowed.Add(name + ".meta"); PathFor(settings.DerivedRoot + "/" + name); PathFor(settings.DerivedRoot + "/" + name + ".meta"); }
            if (!Directory.Exists(root)) return;
            if (Directory.GetDirectories(root).Length != 0 || Directory.GetFiles(root).Any(p => !allowed.Contains(Path.GetFileName(p))))
                throw new IOException("Bridge/river Derived contains unrelated content; refusing overwrite.");
            var markerPath = root + "/" + ImportMarkerFileName;
            var marker = Read<Marker>(markerPath);
            if (File.Exists(markerPath) && (marker == null || marker.kind != MarkerKind || marker.schemaVersion != 1 || marker.inputFingerprint != report.Fingerprint))
                throw new IOException("Existing bridge/river Derived marker is not owned by this input.");
            var metricsPath = root + "/" + MetricsManifestFileName; var metrics = Read<MetricsManifest>(metricsPath);
            if (File.Exists(metricsPath) && (metrics == null || metrics.kind != MetricsKind || metrics.schemaVersion != 1 || metrics.inputFingerprint != report.Fingerprint))
                throw new IOException("Existing bridge/river metrics are not owned by this input.");
            var sourceReviewPath = root + "/" + SourceReviewFileName;
            if (File.Exists(sourceReviewPath) && !Matches(Read<Review>(sourceReviewPath), report.Fingerprint)) throw new IOException("Existing source review does not belong to this input.");
            foreach (var info in report.AssetInfos)
            {
                var png = PathFor(info.DerivedPath); var meta = PathFor(info.DerivedPath + ".meta");
                if (!File.Exists(png) && !File.Exists(meta)) continue;
                var record = marker?.files?.SingleOrDefault(f => f != null && f.name == info.Name);
                if (record == null || record.sha256 != info.SourceSha256 || !File.Exists(png) || StudyATerrainAssets.Hash(File.ReadAllBytes(png)) != record.sha256 ||
                    !File.Exists(meta) || StudyATerrainAssets.Hash(File.ReadAllBytes(meta)) != record.metaSha256)
                    throw new IOException("Existing bridge/river PNG/meta is unowned or edited; refusing overwrite: " + info.Name);
            }
        }
        private static void WriteMetrics(StudyABridgeRiverReadiness report, StudyABridgeRiverSettings settings) => Write(settings.DerivedRoot + "/" + MetricsManifestFileName,
            new MetricsManifest { bridgeRiverImportReady = report.BridgeRiverImportReady, derivedPrepared = report.DerivedPrepared,
                sourceVisuallyReviewed = report.SourceVisuallyReviewed, derivedVisuallyReviewed = report.DerivedVisuallyReviewed, bridgeRiverReady = report.BridgeRiverReady,
                inputFingerprint = report.Fingerprint, sourceManifestSha256 = report.SourceManifestSha256,
                sourceManifestPath = settings.InputRoot + "/" + SourceManifestFileName, assets = report.AssetInfos.ToArray() });
        private static bool Matches(Review review, string fingerprint) => review != null && review.visuallyReviewed && review.inputFingerprint == fingerprint &&
            !string.IsNullOrWhiteSpace(review.reviewer) && !string.IsNullOrWhiteSpace(review.evidence) &&
            DateTime.TryParse(review.reviewedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
        private static Review NewReview(string fingerprint, string reviewer, string evidence) => new Review
        { visuallyReviewed = true, inputFingerprint = fingerprint, reviewer = reviewer, evidence = evidence, reviewedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) };
        private static string PathFor(string path) => StudyATerrainAssets.ProjectPath(path);
        private static T Read<T>(string path) where T : class => File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF')) : null;
        private static void Write<T>(string path, T value) => File.WriteAllText(PathFor(path), JsonUtility.ToJson(value, true), new UTF8Encoding(false));
    }
}
