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
    public sealed class StudyAForestDetailSettings
    {
        public string InputRoot = StudyAForestDetailAssets.DefaultInputRoot;
        public string DerivedRoot = StudyAForestDetailAssets.DefaultDerivedRoot;
    }

    [Serializable]
    public sealed class StudyAForestDetailMetrics
    {
        public string Name, Role;
        public Vector2Int NativeSize, DerivedSize;
        public Vector2 PixelPivot, Pivot, WorldSize;
        public RectInt AlphaBounds;
        public Rect LocalVisualBounds, LocalFootprint, LocalCollider;
        public bool ColliderEnabled;
        public string SourceSha256, SourcePath, DerivedPath, GroundingNote;
    }

    public sealed class StudyAForestDetailProp
    {
        public Sprite Sprite;
        public StudyAForestDetailMetrics Metrics;
    }

    public sealed class StudyAForestDetailSpriteSet
    {
        public Dictionary<string, Sprite> ByName;
        public Dictionary<string, StudyAForestDetailProp[]> RoleDefs;
        public Dictionary<string, StudyAForestDetailMetrics> Metrics;
        public string Fingerprint;
    }

    public sealed class StudyAForestDetailReadiness
    {
        public bool DetailsImportReady => Errors.Count == 0 && AssetInfos.Count == 12;
        public bool DerivedPrepared, SourceVisuallyReviewed, DerivedVisuallyReviewed;
        public bool DetailsReady => DetailsImportReady && DerivedPrepared && SourceVisuallyReviewed && DerivedVisuallyReviewed;
        public bool FullTargetReady => false;
        public string Fingerprint, SourceManifestSha256;
        public readonly List<StudyAForestDetailMetrics> AssetInfos = new List<StudyAForestDetailMetrics>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public override string ToString() =>
            $"Study A forest details: importReady={DetailsImportReady}, detailsReady={DetailsReady}, " +
            $"sourceReviewed={SourceVisuallyReviewed}, derivedReviewed={DerivedVisuallyReviewed}, fullTargetReady=false\n" +
            string.Join("\n", Errors.Select(e => "ERROR: " + e).Concat(Warnings.Select(w => "WARNING: " + w)));
    }

    /// <summary>Imports only the twelve supplied native runtime assets; no source artwork processing.</summary>
    public static class StudyAForestDetailAssets
    {
        public const string DefaultInputRoot = OverheadStudyASettings.ArtRoot + "/Input/ForestDetailsR1";
        public const string DefaultDerivedRoot = OverheadStudyASettings.ArtRoot + "/DerivedForestDetailsR1";
        public const string SourceManifestFileName = "detail-manifest.json";
        public const string SourceReviewFileName = "forest-detail-source-review.json";
        public const string ImportMarkerFileName = "forest-detail-import-marker.json";
        public const string MetricsManifestFileName = "forest-detail-metrics.json";
        public const string ApprovedManifestSha256 = "355784c4ed105aa023556a3650b4edae901f4877bceca49c2b7cf47a22be6a9c";
        public const float PixelsPerUnit = 64;
        private sealed class Spec
        {
            public readonly string Name, Role, Hash;
            public readonly Vector2Int Size;
            public readonly int[] TopBounds;
            public readonly Vector2 PixelPivot;
            public readonly int OpaquePixels, PaletteColors;
            public Spec(string name, string role, string hash, int width, int height, int left, int top,
                int right, int bottom, float pivotX, float pivotY, int opaque, int palette)
            {
                Name = name; Role = role; Hash = hash; Size = new Vector2Int(width, height);
                TopBounds = new[] { left, top, right, bottom }; PixelPivot = new Vector2(pivotX, pivotY);
                OpaquePixels = opaque; PaletteColors = palette;
            }
        }
        private static readonly Spec[] Specs =
        {
            new Spec("flower_white", "flower", "3ae5ff5acc76091efc2a3bf326292deb1aa0bc721e50da3ab163064e4deac1e9", 48, 32, 2, 10, 46, 30, 24.0f, 2.0f, 507, 16),
            new Spec("flower_yellow", "flower", "2100d49303c7370df5e4cd17a200121040a31536cfceebafed60e2b5fa0df85e", 48, 32, 2, 8, 46, 30, 24.0f, 2.0f, 524, 14),
            new Spec("flower_pink", "flower", "1b6f5203027e5e3deb261c0c92415f3d57ae09fe1b57dee700181c3cf8368d40", 48, 32, 2, 12, 46, 30, 24.0f, 2.0f, 425, 18),
            new Spec("grass_tuft_01", "grass", "36353926866c46500a0d3a7b32740cee5f797b482cb032eb343da07ab36a7998", 24, 32, 2, 11, 22, 30, 12.0f, 2.0f, 165, 9),
            new Spec("grass_tuft_02", "grass", "d6259337c23caa58f9a643ff27528f9162a75afcd493f484573b8a51fbe8f68d", 24, 32, 3, 7, 22, 30, 12.5f, 2.0f, 183, 8),
            new Spec("stepping_stone_01", "stepping", "6bf4f9f189330a7fd118a30a0470a1a7d37457f76085d5692c57b6b628724a71", 48, 32, 6, 6, 42, 30, 24.0f, 2.0f, 641, 8),
            new Spec("stepping_stone_02", "stepping", "33ade6955a66bfef3849b1c71643056090e76771602eaa90d25491bb824c86b7", 48, 32, 9, 4, 39, 30, 24.0f, 2.0f, 537, 8),
            new Spec("stepping_stone_03", "stepping", "b580b51ce25a4cd644d1de214d3ba54d22bd25d60b931d67a70b820c2a1dbc3f", 48, 32, 3, 5, 44, 30, 23.5f, 2.0f, 775, 8),
            new Spec("stepping_stone_04", "stepping", "fc3f69bebba5dd8e9cc4c656e9e635574ca345428647a360397e9c91aefafc21", 48, 32, 7, 6, 40, 30, 23.5f, 2.0f, 579, 7),
            new Spec("tree_stump", "stump", "6043a497a091f15765dc4f4d038e16877525c4f050e863256e0622302d6df8a7", 64, 64, 3, 13, 61, 62, 32.0f, 2.0f, 1749, 17),
            new Spec("grass_patch_01", "grass", "c817a5cadd580fdf3c22505e6348c374d5d885fd6773926c418af41f9ec3b245", 32, 32, 2, 14, 30, 29, 16.0f, 3.0f, 224, 8),
            new Spec("grass_patch_02", "grass", "d005123c0c4880f1d26510a2c2d07bad6cb6e9fcd5a4d55186dfb0cacedb5a75", 32, 32, 1, 16, 30, 30, 15.5f, 2.0f, 171, 8),
        };
        [Serializable] private sealed class RawManifest
        {
            public RawImport import_settings = null; public RawSprite[] sprites = null;
        }
        [Serializable] private sealed class RawImport
        {
            public string texture_type = null, sprite_mode = null, filter_mode = null, compression = null, wrap_mode = null, mesh_type = null;
            public int pixels_per_unit = 0;
            public bool generate_mipmaps = true, alpha_is_transparency = false, generate_physics_shape = true;
        }
        [Serializable] private sealed class RawSprite
        {
            public string name = null, file = null, sha256 = null;
            public int[] dimensions = null, visible_bounds_top_left_exclusive = null, alpha_values = null;
            public int ppu = 0, visible_baseline_top_left_y = 0, opaque_pixels = 0, opaque_palette_count = 0;
            public float[] pivot_unity_normalized = null, pivot_unity_pixels_bottom_left = null;
            public RawCollider collider = null;
        }
        [Serializable] private sealed class RawCollider
        {
            public bool enabled = true;
            public RecommendedCollider recommended_if_gameplay_requires = null;
        }
        [Serializable] private sealed class RecommendedCollider
        {
            public string type = null; public float[] size_world = null, offset_world = null;
        }
        [Serializable] private sealed class Review
        {
            public bool visuallyReviewed; public string inputFingerprint, reviewer, evidence, reviewedAtUtc;
        }
        [Serializable] private sealed class FileRecord { public string name, sha256, metaSha256; }
        [Serializable] private sealed class Marker
        {
            public int schemaVersion = 1; public string kind = "forest-details-r1-native-copy";
            public string inputFingerprint; public Review derivedReview; public FileRecord[] files;
        }
        [Serializable] private sealed class MetricsManifest
        {
            public int schemaVersion = 1; public string kind = "forest-details-r1-only";
            public float pixelsPerUnit = PixelsPerUnit; public int integerDownsample = 1;
            public bool detailsImportReady, derivedPrepared, sourceVisuallyReviewed, derivedVisuallyReviewed, detailsReady;
            public bool fullTargetReady = false;
            public string inputFingerprint, sourceManifestSha256, sourceManifestPath;
            public StudyAForestDetailMetrics[] assets;
        }

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Forest Details/Check Twelve Sprites (Read Only)")]
        public static void CheckReadinessMenu() => Debug.Log(CheckReadiness().ToString());
        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Forest Details/Prepare Derived Twelve Sprites")]
        public static void PrepareMenu() => Debug.Log(PrepareDerived().ToString());

        public static StudyAForestDetailReadiness CheckReadiness(StudyAForestDetailSettings settings = null)
        {
            settings = settings ?? new StudyAForestDetailSettings();
            var report = new StudyAForestDetailReadiness();
            try
            {
                ValidateRoots(settings);
                var manifest = File.ReadAllBytes(PathFor(settings.InputRoot + "/" + SourceManifestFileName));
                report.SourceManifestSha256 = StudyATerrainAssets.Hash(manifest);
                if (report.SourceManifestSha256 != ApprovedManifestSha256) throw new InvalidDataException("Forest details manifest is not the supplied approved runtime original.");
                ValidateManifest(JsonUtility.FromJson<RawManifest>(Encoding.UTF8.GetString(manifest).TrimStart('\uFEFF')));
                foreach (var spec in Specs) report.AssetInfos.Add(Inspect(spec, settings));
                var fingerprint = new StringBuilder("StudyAForestDetails-native-copy-v1\n64\n1\nexplicit-solid-stump-.5-.25-offset-.125\n");
                fingerprint.AppendLine(ApprovedManifestSha256); fingerprint.AppendLine(settings.InputRoot); fingerprint.AppendLine(settings.DerivedRoot);
                foreach (var info in report.AssetInfos.OrderBy(i => i.Name, StringComparer.Ordinal)) fingerprint.AppendLine(JsonUtility.ToJson(info));
                report.Fingerprint = StudyATerrainAssets.Hash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
                try { report.SourceVisuallyReviewed = Matches(Read<Review>(PathFor(settings.DerivedRoot + "/" + SourceReviewFileName)), report.Fingerprint); }
                catch (Exception ex) { report.Warnings.Add("Forest source review record is invalid: " + ex.Message); }
                try { CheckDerived(report, settings); }
                catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Warnings.Add("Forest Derived state is invalid: " + ex.Message); }
                if (!report.SourceVisuallyReviewed) report.Warnings.Add("The twelve actual runtime PNGs require an explicit source image review.");
                if (!report.DerivedVisuallyReviewed) report.Warnings.Add("Imported native sprites require an explicit derived review.");
                report.Warnings.Add("Partial forest-detail kit only. Native Assets are the runtime inputs; original generation Source files are not part of this ZIP.");
            }
            catch (Exception ex) { report.Errors.Add(ex.Message); }
            return report;
        }

        public static StudyAForestDetailReadiness PrepareDerived(StudyAForestDetailSettings settings = null)
        {
            settings = settings ?? new StudyAForestDetailSettings();
            var report = CheckReadiness(settings);
            if (!report.DetailsImportReady) return report;
            try
            {
                var sourceBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var info in report.AssetInfos)
                {
                    PathFor(info.DerivedPath); PathFor(info.DerivedPath + ".meta");
                    var bytes = File.ReadAllBytes(PathFor(info.SourcePath));
                    if (StudyATerrainAssets.Hash(bytes) != info.SourceSha256) throw new IOException("Forest native input changed during preparation: " + info.Name);
                    sourceBytes.Add(info.Name, bytes);
                }
                var recheck = CheckReadiness(settings);
                if (!recheck.DetailsImportReady || recheck.Fingerprint != report.Fingerprint) throw new IOException("Forest input manifest or metrics changed during preparation.");
                Preflight(settings); Directory.CreateDirectory(PathFor(settings.DerivedRoot));
                var marker = new Marker { inputFingerprint = report.Fingerprint, files = Array.Empty<FileRecord>() };
                Write(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
                var files = new List<FileRecord>();
                foreach (var info in report.AssetInfos)
                {
                    File.WriteAllBytes(PathFor(info.DerivedPath), sourceBytes[info.Name]);
                    ConfigureImport(info);
                    files.Add(new FileRecord { name = info.Name, sha256 = info.SourceSha256,
                        metaSha256 = StudyATerrainAssets.Hash(File.ReadAllBytes(PathFor(info.DerivedPath + ".meta"))) });
                }
                marker.files = files.ToArray(); Write(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
                report = CheckReadiness(settings); WriteMetrics(report, settings);
            }
            catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Errors.Add("Forest Derived preparation failed: " + ex.Message); }
            return report;
        }

        public static StudyAForestDetailReadiness RecordSourceVisualReview(string reviewer, string evidence, StudyAForestDetailSettings settings = null)
        {
            settings = settings ?? new StudyAForestDetailSettings(); StudyATerrainAssets.ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings);
            if (!report.DetailsImportReady) throw new InvalidOperationException(report.ToString());
            Preflight(settings); Directory.CreateDirectory(PathFor(settings.DerivedRoot));
            Write(settings.DerivedRoot + "/" + SourceReviewFileName, NewReview(report.Fingerprint, reviewer, evidence));
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        public static StudyAForestDetailReadiness RecordDerivedVisualReview(string reviewer, string evidence, StudyAForestDetailSettings settings = null)
        {
            settings = settings ?? new StudyAForestDetailSettings(); StudyATerrainAssets.ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings);
            if (!report.DetailsImportReady || !report.DerivedPrepared || !report.SourceVisuallyReviewed) throw new InvalidOperationException(report.ToString());
            Preflight(settings);
            var marker = Read<Marker>(PathFor(settings.DerivedRoot + "/" + ImportMarkerFileName));
            marker.derivedReview = NewReview(report.Fingerprint, reviewer, evidence); Write(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        public static StudyAForestDetailSpriteSet LoadSpriteSet(StudyAForestDetailSettings settings = null)
        {
            var report = CheckReadiness(settings);
            if (!report.DetailsReady) throw new InvalidOperationException(report.ToString());
            var props = report.AssetInfos.Select(i => new StudyAForestDetailProp { Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(i.DerivedPath), Metrics = i }).ToArray();
            return new StudyAForestDetailSpriteSet
            {
                ByName = props.ToDictionary(p => p.Metrics.Name, p => p.Sprite, StringComparer.Ordinal),
                RoleDefs = props.GroupBy(p => p.Metrics.Role).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal),
                Metrics = report.AssetInfos.ToDictionary(i => i.Name, StringComparer.Ordinal), Fingerprint = report.Fingerprint
            };
        }

        private static StudyAForestDetailMetrics Inspect(Spec spec, StudyAForestDetailSettings settings)
        {
            var source = settings.InputRoot + "/Assets/" + spec.Name + ".png";
            var pixels = StudyATerrainAssets.ReadPng(PathFor(source), spec.Hash, spec.Size);
            var minX = spec.Size.x; var minY = spec.Size.y; var maxX = -1; var maxY = -1; var opaque = 0;
            var palette = new HashSet<uint>();
            for (var y = 0; y < spec.Size.y; y++) for (var x = 0; x < spec.Size.x; x++)
            {
                var pixel = pixels[y * spec.Size.x + x];
                if (pixel.a == 0)
                {
                    if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0) throw new InvalidDataException("Transparent runtime RGB must remain zero: " + spec.Name);
                    continue;
                }
                if (x == 0 || y == 0 || x == spec.Size.x - 1 || y == spec.Size.y - 1) throw new InvalidDataException("Forest sprite outside canvas edge must be transparent: " + spec.Name);
                opaque++; palette.Add((uint)((pixel.r << 16) | (pixel.g << 8) | pixel.b));
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            var topBounds = new[] { minX, spec.Size.y - maxY - 1, maxX + 1, spec.Size.y - minY };
            if (!topBounds.SequenceEqual(spec.TopBounds) || opaque != spec.OpaquePixels || palette.Count != spec.PaletteColors ||
                Math.Abs(spec.PixelPivot.x - (minX + maxX + 1) * .5f) > .00001f || spec.PixelPivot.y != minY)
                throw new InvalidDataException("Forest actual alpha bounds/baseline/palette differ from the supplied manifest: " + spec.Name);
            var visual = new Rect((minX - spec.PixelPivot.x) / PixelsPerUnit, (minY - spec.PixelPivot.y) / PixelsPerUnit,
                (maxX - minX + 1) / PixelsPerUnit, (maxY - minY + 1) / PixelsPerUnit);
            var stump = spec.Role == "stump";
            return new StudyAForestDetailMetrics
            {
                Name = spec.Name, Role = spec.Role, NativeSize = spec.Size, DerivedSize = spec.Size, PixelPivot = spec.PixelPivot,
                Pivot = new Vector2(spec.PixelPivot.x / spec.Size.x, spec.PixelPivot.y / spec.Size.y),
                WorldSize = new Vector2(spec.Size.x / PixelsPerUnit, spec.Size.y / PixelsPerUnit),
                AlphaBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1), LocalVisualBounds = visual,
                LocalFootprint = stump ? new Rect(-.25f, 0, .5f, .25f) : visual,
                LocalCollider = stump ? new Rect(-.25f, 0, .5f, .25f) : Rect.zero, ColliderEnabled = stump,
                SourceSha256 = spec.Hash, SourcePath = source, DerivedPath = settings.DerivedRoot + "/" + spec.Name + ".png",
                GroundingNote = stump ? "Source default collider is disabled. Parent explicitly requests the recommended solid lower-root box (.5,.25), offset (0,.125)." :
                    "Passable low ground detail. Visible alpha bounds are the placement footprint; no collider or generated sprite physics shape."
            };
        }

        private static StudyATerrainMetrics ImportMetrics(StudyAForestDetailMetrics info) => new StudyATerrainMetrics
        {
            Name = info.Name, NativeSize = info.NativeSize, DerivedSize = info.DerivedSize,
            PixelPivot = info.PixelPivot, Pivot = info.Pivot, DerivedPath = info.DerivedPath
        };
        private static void ConfigureImport(StudyAForestDetailMetrics info)
        {
            StudyATerrainAssets.ConfigureImport(ImportMetrics(info));
            var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
            var ts = new TextureImporterSettings(); importer.ReadTextureSettings(ts);
            ts.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(ts); importer.SaveAndReimport();
        }
        private static void CheckDerived(StudyAForestDetailReadiness report, StudyAForestDetailSettings settings)
        {
            var marker = Read<Marker>(PathFor(settings.DerivedRoot + "/" + ImportMarkerFileName));
            if (marker == null || marker.schemaVersion != 1 || marker.kind != "forest-details-r1-native-copy" || marker.inputFingerprint != report.Fingerprint ||
                marker.files == null || marker.files.Length != 12)
            { report.Warnings.Add("No prepared forest detail marker for the current twelve assets."); return; }
            foreach (var info in report.AssetInfos)
            {
                var file = marker.files.SingleOrDefault(f => f != null && f.name == info.Name);
                var path = PathFor(info.DerivedPath); var meta = PathFor(info.DerivedPath + ".meta");
                if (file == null || file.sha256 != info.SourceSha256 || !File.Exists(path) || StudyATerrainAssets.Hash(File.ReadAllBytes(path)) != file.sha256 ||
                    !File.Exists(meta) || StudyATerrainAssets.Hash(File.ReadAllBytes(meta)) != file.metaSha256)
                { report.Warnings.Add("Forest Derived PNG/meta changed: " + info.Name); return; }
                if (!StudyAGroundR2Assets.SpriteContractMatches(ImportMetrics(info)))
                { report.Warnings.Add("Forest Derived native/PPU64/Point/RGBA32/FullRect/pivot settings differ: " + info.Name); return; }
                var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
                var ts = new TextureImporterSettings(); importer.ReadTextureSettings(ts);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath);
                if (ts.spriteGenerateFallbackPhysicsShape || sprite.GetPhysicsShapeCount() != 0)
                { report.Warnings.Add("Forest generated/custom sprite physics shapes must remain disabled: " + info.Name); return; }
            }
            report.DerivedPrepared = true; report.DerivedVisuallyReviewed = Matches(marker.derivedReview, report.Fingerprint);
        }

        private static void ValidateManifest(RawManifest raw)
        {
            if (raw == null || raw.sprites == null || raw.sprites.Length != 12) throw new InvalidDataException("The supplied twelve-sprite forest detail manifest is required.");
            var s = raw.import_settings;
            if (s == null || s.texture_type != "Sprite (2D and UI)" || s.sprite_mode != "Single" || s.pixels_per_unit != 64 || s.filter_mode != "Point" ||
                s.compression != "None" || s.generate_mipmaps || s.wrap_mode != "Clamp" || !s.alpha_is_transparency || s.mesh_type != "Full Rect" || s.generate_physics_shape)
                throw new InvalidDataException("Forest detail import declaration differs.");
            foreach (var spec in Specs)
            {
                var a = raw.sprites.SingleOrDefault(i => i != null && i.name == spec.Name);
                var pivot = new Vector2(spec.PixelPivot.x / spec.Size.x, spec.PixelPivot.y / spec.Size.y);
                if (a == null || a.file != "Assets/" + spec.Name + ".png" || a.sha256 != spec.Hash || a.ppu != 64 ||
                    a.dimensions == null || !a.dimensions.SequenceEqual(new[] { spec.Size.x, spec.Size.y }) ||
                    a.visible_bounds_top_left_exclusive == null || !a.visible_bounds_top_left_exclusive.SequenceEqual(spec.TopBounds) ||
                    a.visible_baseline_top_left_y != spec.TopBounds[3] || a.opaque_pixels != spec.OpaquePixels || a.opaque_palette_count != spec.PaletteColors ||
                    a.alpha_values == null || !a.alpha_values.SequenceEqual(new[] { 0, 255 }) || a.collider == null || a.collider.enabled ||
                    a.pivot_unity_pixels_bottom_left == null || !a.pivot_unity_pixels_bottom_left.SequenceEqual(new[] { spec.PixelPivot.x, spec.PixelPivot.y }) ||
                    a.pivot_unity_normalized == null || a.pivot_unity_normalized.Length != 2 ||
                    Vector2.Distance(new Vector2(a.pivot_unity_normalized[0], a.pivot_unity_normalized[1]), pivot) > .00001f)
                    throw new InvalidDataException("Forest sprite native/pivot/collider declaration differs: " + spec.Name);
                if (spec.Role == "stump")
                {
                    var c = a.collider.recommended_if_gameplay_requires;
                    if (c == null || c.type != "BoxCollider2D" || c.size_world == null || !c.size_world.SequenceEqual(new[] { .5f, .25f }) ||
                        c.offset_world == null || !c.offset_world.SequenceEqual(new[] { 0f, .125f })) throw new InvalidDataException("Stump recommended root collider differs.");
                }
            }
        }
        private static void ValidateRoots(StudyAForestDetailSettings settings)
        {
            StudyATerrainAssets.ValidateRoots(new StudyATerrainSettings { InputRoot = settings.InputRoot, DerivedRoot = settings.DerivedRoot });
            var derived = PathFor(settings.DerivedRoot).TrimEnd(Path.DirectorySeparatorChar);
            foreach (var protectedRoot in new[] { StudyATerrainAssets.R1InputRoot, StudyATerrainAssets.R1DerivedRoot,
                StudyATerrainAssets.R2InputRoot, StudyATerrainAssets.R2DerivedRoot, StudyAStarterAssets.DefaultInputRoot, StudyAStarterAssets.DefaultDerivedRoot })
            {
                var protectedPath = PathFor(protectedRoot).TrimEnd(Path.DirectorySeparatorChar);
                if (derived.Equals(protectedPath, StringComparison.OrdinalIgnoreCase) || derived.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    protectedPath.StartsWith(derived + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Forest Derived must not overlap preserved ground, hero, or starter source/derived directories.");
            }
        }
        private static void Preflight(StudyAForestDetailSettings settings)
        {
            foreach (var file in new[] { SourceReviewFileName, ImportMarkerFileName, MetricsManifestFileName })
            { PathFor(settings.DerivedRoot + "/" + file); PathFor(settings.DerivedRoot + "/" + file + ".meta"); }
        }
        private static void WriteMetrics(StudyAForestDetailReadiness report, StudyAForestDetailSettings settings) =>
            Write(settings.DerivedRoot + "/" + MetricsManifestFileName, new MetricsManifest
            {
                detailsImportReady = report.DetailsImportReady, derivedPrepared = report.DerivedPrepared,
                sourceVisuallyReviewed = report.SourceVisuallyReviewed, derivedVisuallyReviewed = report.DerivedVisuallyReviewed, detailsReady = report.DetailsReady,
                inputFingerprint = report.Fingerprint, sourceManifestSha256 = report.SourceManifestSha256,
                sourceManifestPath = settings.InputRoot + "/" + SourceManifestFileName, assets = report.AssetInfos.ToArray()
            });
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
