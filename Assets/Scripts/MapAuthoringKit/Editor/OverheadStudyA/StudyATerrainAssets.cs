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
    [Serializable]
    public sealed class StudyATerrainSettings
    {
        public string InputRoot = StudyATerrainAssets.DefaultInputRoot;
        public string DerivedRoot = StudyATerrainAssets.DefaultDerivedRoot;
    }

    [Serializable]
    public sealed class StudyATerrainMetrics
    {
        public string Name;
        public string Role;
        public Vector2Int NativeSize;
        public Vector2Int DerivedSize;
        public Vector2 PixelPivot;
        public Vector2 Pivot;
        public Vector2 WorldSize;
        public Vector2 ColliderSize;
        public Vector2 ColliderOffset;
        public RectInt AlphaBounds;
        public string SourceSha256;
        public string SourcePath;
        public string DerivedPath;
        public string Note;
    }

    public sealed class StudyATerrainReadiness
    {
        public bool TerrainImportReady => Errors.Count == 0 && AssetInfos.Count == 17;
        public bool DerivedPrepared;
        public bool SourceVisuallyReviewed;
        public bool DerivedVisuallyReviewed;
        public bool ApprovedTerrainReady => TerrainImportReady && DerivedPrepared && SourceVisuallyReviewed && DerivedVisuallyReviewed;
        public bool HeroReady => ApprovedTerrainReady && AssetInfos.Any(a => a.Role == "hero");
        public bool FullTargetReady => false;
        public readonly string[] MissingFullTargetRoles = { "water", "bridge", "flower" };
        public string Fingerprint;
        public string SourceManifestSha256;
        public readonly List<StudyATerrainMetrics> AssetInfos = new List<StudyATerrainMetrics>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public override string ToString() =>
            $"Study A terrain: importReady={TerrainImportReady}, terrainReady={ApprovedTerrainReady}, heroReady={HeroReady}, " +
            $"sourceReviewed={SourceVisuallyReviewed}, derivedReviewed={DerivedVisuallyReviewed}, fullTargetReady=false, " +
            $"missing={string.Join(",", MissingFullTargetRoles)}\n" +
            string.Join("\n", Errors.Select(e => "ERROR: " + e).Concat(Warnings.Select(w => "WARNING: " + w)));
    }

    public sealed class StudyATerrainSpriteSet
    {
        public Sprite[] GrassVariants;
        public Sprite[] SoilVariants;
        public Dictionary<StudyASoilTileShape, Sprite> Boundaries;
        public Dictionary<string, Sprite> ByName;
        public Sprite Hero;
        public StudyATerrainMetrics HeroMetrics;
        public string Fingerprint;
    }

    /// <summary>Copies the reviewed supplement without changing source pixels or source importers.</summary>
    public static class StudyATerrainAssets
    {
        public const string LegacyInputRoot = OverheadStudyASettings.ArtRoot + "/Input/TerrainSupplement";
        public const string LegacyDerivedRoot = OverheadStudyASettings.ArtRoot + "/DerivedTerrain";
        public const string DefaultInputRoot = OverheadStudyASettings.ArtRoot + "/Input/GroundR2";
        public const string DefaultDerivedRoot = OverheadStudyASettings.ArtRoot + "/DerivedGroundR2";
        public const string R1InputRoot = LegacyInputRoot;
        public const string R1DerivedRoot = LegacyDerivedRoot;
        public const string R2InputRoot = DefaultInputRoot;
        public const string R2DerivedRoot = DefaultDerivedRoot;
        public const float PixelsPerUnit = 64f;
        public const int IntegerDownsample = 1;
        public const string SourceManifestFileName = "terrain-manifest.json";
        public const string MetricsManifestFileName = "terrain-metrics.json";
        public const string ImportMarkerFileName = "terrain-import-marker.json";
        public const string SourceReviewFileName = "terrain-source-review.json";
        public const string ApprovedManifestSha256 = "2ac3664a9aee5d85fb1149303d92fd7b7a0e854b97033dd5667dfd24e31405b4";
        public const string ApprovedAtlasSha256 = "2777e9b08c64350affc0685e0f00a4a6b65a2760024dc07d5e7a004ed4c7eca6";
        private const string HeroHash = "06750b8ac9cc2f9fcc91853ba65cb3da3a0b3a8235a3540702b4add616bd7356";
        private static readonly string[] TexturePlatforms =
            { "Standalone", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo Switch", "tvOS" };

        private sealed class TileSpec
        {
            public readonly string Name, Hash;
            public readonly int X, Y;
            public TileSpec(string name, string hash, int x, int y) { Name = name; Hash = hash; X = x; Y = y; }
        }
        private static readonly TileSpec[] Tiles =
        {
            new TileSpec("grass_01", "870395190398d83d9c5c4d635d606c93b6d809d264b2af506cdbd56d46945cc6", 0, 0),
            new TileSpec("grass_02", "8317bd15ac12187962ec031b22dde3dd6c0f80e2243f4e200d1f91233eca8ae2", 64, 0),
            new TileSpec("dirt_01", "8eab3bafbfc994f76b04d18a7b4b1b2f55f9d77743e3d21ecfe1f981ee1f971a", 128, 0),
            new TileSpec("dirt_02", "0b31a48e805271c83889ce1f13174f8b44a2b311971dc8f03944b634774b459c", 192, 0),
            new TileSpec("dirt_edge_N", "409f777d1277864fa52b77951823321f01eb76088fedaf6ed8ad8d21c75da895", 0, 64),
            new TileSpec("dirt_edge_E", "0f1e3ad7e307fc61fba380a15d14eb8d9d27835d7962bc2c401f687be8061ac5", 64, 64),
            new TileSpec("dirt_edge_S", "886f3838bba751a63072a22b1af9aa8903c1a8277ce45d207db4f08589753cef", 128, 64),
            new TileSpec("dirt_edge_W", "736947ef42b8afda871c9c9e6cc60ebfb826cf4a6073accf44339bf76450d1c0", 192, 64),
            new TileSpec("dirt_convex_NW", "6aacca24c4b250c879d2039c1995563174ba1f96dbce41eb411bc840d7c10735", 0, 128),
            new TileSpec("dirt_convex_NE", "e57fdb3cdf942cb2fd61c4ac9272bc76bbad8a2cbff6c88b7e35cfcf75519b58", 64, 128),
            new TileSpec("dirt_convex_SE", "f2f18d37c28c47149630c933a5511cc8b189568742784580c5a9c9edb545b9dc", 128, 128),
            new TileSpec("dirt_convex_SW", "51173f6ab72cc45ea1dc52bcd472c636469f500773d9f6617acce62b87284b40", 192, 128),
            new TileSpec("dirt_concave_NW", "b903c7aa4339cefc9f6f733117526a4523d7024fe26a4021f169281cefbc7fef", 0, 192),
            new TileSpec("dirt_concave_NE", "e3559225a966a263f32cd76601726a1090ad8e437f5baad23f82573ce96f7f75", 64, 192),
            new TileSpec("dirt_concave_SE", "ce08fd94758872051088bd5adfe0fb31253e77946e5b3a447a29aada59802f6a", 128, 192),
            new TileSpec("dirt_concave_SW", "080f6ae9d2f58c968c47e82d5700552e7a566a065f2045696da62d2901cea990", 192, 192),
        };
        internal static readonly Dictionary<StudyASoilTileShape, string> BoundaryNames =
            new Dictionary<StudyASoilTileShape, string>
        {
            { StudyASoilTileShape.GrassNorth, "dirt_edge_N" },
            { StudyASoilTileShape.GrassEast, "dirt_edge_E" },
            { StudyASoilTileShape.GrassSouth, "dirt_edge_S" },
            { StudyASoilTileShape.GrassWest, "dirt_edge_W" },
            { StudyASoilTileShape.ConvexGrassNorthWest, "dirt_convex_NW" },
            { StudyASoilTileShape.ConvexGrassNorthEast, "dirt_convex_NE" },
            { StudyASoilTileShape.ConvexGrassSouthEast, "dirt_convex_SE" },
            { StudyASoilTileShape.ConvexGrassSouthWest, "dirt_convex_SW" },
            { StudyASoilTileShape.ConcaveGrassNorthWest, "dirt_concave_NW" },
            { StudyASoilTileShape.ConcaveGrassNorthEast, "dirt_concave_NE" },
            { StudyASoilTileShape.ConcaveGrassSouthEast, "dirt_concave_SE" },
            { StudyASoilTileShape.ConcaveGrassSouthWest, "dirt_concave_SW" }
        };

        [Serializable] private sealed class RawManifest
        {
            public string status = null;
            public string atlas = null;
            public string origin = null;
            public string edge_convention = null;
            public RawTile[] tiles = null;
            public RawHero hero = null;
        }
        [Serializable] private sealed class RawTile
        {
            public string name = null; public string file = null; public string sha256 = null;
            public int[] rect_top_left = null; public int[] rect_unity_bottom_left = null;
            public int ppu = 0; public float[] pivot = null;
        }
        [Serializable] private sealed class RawHero
        {
            public string file = null; public int[] canvas = null; public int ppu = 0;
            public int[] visible_bounds = null; public float[] pivot_normalized = null;
            public int feet_baseline_y_from_top = 0; public float[] collision_world = null;
            public string note = null; public string sha256 = null;
        }
        [Serializable] private sealed class ReviewRecord
        {
            public bool visuallyReviewed; public string inputFingerprint; public string reviewer;
            public string evidence; public string reviewedAtUtc;
        }
        [Serializable] private sealed class DerivedRecord
        {
            public string name; public string sha256; public string metaSha256;
        }
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
            public string kind = "clearing-path-terrain-static-hero-supplement";
            public float pixelsPerUnit = PixelsPerUnit;
            public int integerDownsample = IntegerDownsample;
            public bool terrainImportReady, derivedPrepared, sourceVisuallyReviewed, derivedVisuallyReviewed, approvedTerrainReady, heroReady;
            public bool fullTargetReady = false;
            public string[] missingFullTargetRoles;
            public string inputFingerprint, sourceManifestSha256, sourceManifestPath;
            public StudyATerrainMetrics[] assets;
        }

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Terrain/Check Supplement (Read Only)")]
        public static void CheckReadinessMenu() => Debug.Log(CheckReadiness().ToString());

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Terrain/Prepare Derived Supplement")]
        public static void PrepareMenu() => Debug.Log(PrepareDerived().ToString());

        public static StudyATerrainReadiness CheckReadiness(StudyATerrainSettings settings = null)
        {
            settings = settings ?? new StudyATerrainSettings();
            if (StudyAGroundR2Assets.UsesR2(settings)) return StudyAGroundR2Assets.CheckReadiness(settings);
            var report = new StudyATerrainReadiness();
            try
            {
                ValidateRoots(settings);
                var bytes = File.ReadAllBytes(ProjectPath(settings.InputRoot + "/" + SourceManifestFileName));
                report.SourceManifestSha256 = Hash(bytes);
                if (report.SourceManifestSha256 != ApprovedManifestSha256)
                    throw new InvalidDataException("Terrain supplement manifest is not the reviewed original.");
                var raw = JsonUtility.FromJson<RawManifest>(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                ValidateManifest(raw);
                var pixels = new Dictionary<string, Color32[]>(StringComparer.Ordinal);
                foreach (var spec in Tiles)
                {
                    Color32[] image;
                    report.AssetInfos.Add(Inspect(spec.Name, spec.Name.StartsWith("grass_", StringComparison.Ordinal) ? "grass" :
                        spec.Name.StartsWith("dirt_0", StringComparison.Ordinal) ? "soil" : "soilBoundary", spec.Hash,
                        new Vector2Int(64, 64), new Vector2(.5f, .5f), settings, out image));
                    pixels.Add(spec.Name, image);
                }
                Color32[] heroPixels;
                var hero = Inspect("hero_idle", "hero", HeroHash, new Vector2Int(64, 80), new Vector2(.5f, .1375f), settings, out heroPixels);
                var expectedBounds = new RectInt(8, 11, 48, 61);
                if (hero.AlphaBounds != expectedBounds) throw new InvalidDataException("Hero alpha bounds differ from the declared native bounds.");
                hero.ColliderSize = new Vector2(.48f, .55f);
                hero.ColliderOffset = new Vector2(0, .22f);
                hero.Note = "One static pose. Existing movement collider remains .48 x .55, offset (0,.22). No animation set.";
                report.AssetInfos.Add(hero);
                ValidateAtlas(settings, raw, pixels);
                ValidateSurfaceBorders(pixels["grass_01"], pixels["grass_02"], "grass");
                ValidateSurfaceBorders(pixels["dirt_01"], pixels["dirt_02"], "soil");
                ValidateBoundaryAlpha(pixels);
                report.Fingerprint = Fingerprint(bytes, report.AssetInfos, settings);
                try
                {
                    report.SourceVisuallyReviewed = ReviewMatches(ReadJson<ReviewRecord>(ProjectPath(settings.DerivedRoot + "/" + SourceReviewFileName)), report.Fingerprint);
                }
                catch (Exception ex) { report.Warnings.Add("Source review record is invalid: " + ex.Message); }
                // A corrupt derived marker must invalidate Derived, while preserving the ability to prepare it again.
                try { CheckDerived(report, settings); }
                catch (Exception ex)
                {
                    report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false;
                    report.Warnings.Add("Derived state is invalid: " + ex.Message);
                }
                if (!report.SourceVisuallyReviewed) report.Warnings.Add("Source images have no review record for the current fingerprint.");
                if (!report.DerivedVisuallyReviewed) report.Warnings.Add("Imported sprites have no review record for the current bytes and importer metadata.");
                report.Warnings.Add("Clearing/path prototype only. Water, bridge and flower assets are missing; FullTargetReady=false.");
            }
            catch (Exception ex) { report.Errors.Add(ex.Message); }
            return report;
        }

        /// <summary>Copies exact source PNG bytes. Preparation never records visual approval.</summary>
        public static StudyATerrainReadiness PrepareDerived(StudyATerrainSettings settings = null)
        {
            settings = settings ?? new StudyATerrainSettings();
            if (StudyAGroundR2Assets.UsesR2(settings)) return StudyAGroundR2Assets.PrepareDerived(settings);
            var report = CheckReadiness(settings);
            if (!report.TerrainImportReady) return report;
            try
            {
                var bytesByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var info in report.AssetInfos)
                {
                    ProjectPath(info.DerivedPath); ProjectPath(info.DerivedPath + ".meta");
                    var bytes = File.ReadAllBytes(ProjectPath(info.SourcePath));
                    if (Hash(bytes) != info.SourceSha256) throw new IOException("Source changed during preparation: " + info.Name);
                    bytesByName.Add(info.Name, bytes);
                }
                var recheck = CheckReadiness(settings);
                if (!recheck.TerrainImportReady || recheck.Fingerprint != report.Fingerprint)
                    throw new IOException("Source manifest or pixels changed during preparation.");
                PreflightGeneratedPaths(settings);
                Directory.CreateDirectory(ProjectPath(settings.DerivedRoot));
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
            catch (Exception ex)
            {
                report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false;
                report.Errors.Add("Derived preparation failed: " + ex.Message);
            }
            return report;
        }

        public static StudyATerrainReadiness RecordSourceVisualReview(string reviewer, string evidence, StudyATerrainSettings settings = null)
        {
            settings = settings ?? new StudyATerrainSettings();
            if (StudyAGroundR2Assets.UsesR2(settings)) return StudyAGroundR2Assets.RecordSourceVisualReview(reviewer, evidence, settings);
            ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings);
            if (!report.TerrainImportReady) throw new InvalidOperationException(report.ToString());
            PreflightGeneratedPaths(settings);
            Directory.CreateDirectory(ProjectPath(settings.DerivedRoot));
            WriteJson(settings.DerivedRoot + "/" + SourceReviewFileName, NewReview(report.Fingerprint, reviewer, evidence));
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        public static StudyATerrainReadiness RecordDerivedVisualReview(string reviewer, string evidence, StudyATerrainSettings settings = null)
        {
            settings = settings ?? new StudyATerrainSettings();
            if (StudyAGroundR2Assets.UsesR2(settings)) return StudyAGroundR2Assets.RecordDerivedVisualReview(reviewer, evidence, settings);
            ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings);
            if (!report.TerrainImportReady || !report.DerivedPrepared || !report.SourceVisuallyReviewed)
                throw new InvalidOperationException("Source review and valid Derived sprites are required.\n" + report);
            PreflightGeneratedPaths(settings);
            var marker = ReadJson<ImportMarker>(ProjectPath(settings.DerivedRoot + "/" + ImportMarkerFileName));
            marker.derivedReview = NewReview(report.Fingerprint, reviewer, evidence);
            WriteJson(settings.DerivedRoot + "/" + ImportMarkerFileName, marker);
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        public static StudyATerrainSpriteSet LoadSpriteSet(StudyATerrainSettings settings = null)
        {
            settings = settings ?? new StudyATerrainSettings();
            if (StudyAGroundR2Assets.UsesR2(settings)) return StudyAGroundR2Assets.LoadSpriteSet(settings);
            var report = CheckReadiness(settings);
            if (!report.ApprovedTerrainReady || !report.HeroReady) throw new InvalidOperationException(report.ToString());
            var byName = report.AssetInfos.ToDictionary(i => i.Name, i => AssetDatabase.LoadAssetAtPath<Sprite>(i.DerivedPath), StringComparer.Ordinal);
            return new StudyATerrainSpriteSet
            {
                GrassVariants = new[] { byName["grass_01"], byName["grass_02"] },
                SoilVariants = new[] { byName["dirt_01"], byName["dirt_02"] },
                Boundaries = BoundaryNames.ToDictionary(p => p.Key, p => byName[p.Value]),
                ByName = byName, Hero = byName["hero_idle"],
                HeroMetrics = report.AssetInfos.Single(i => i.Name == "hero_idle"), Fingerprint = report.Fingerprint
            };
        }

        /// <summary>Only unchanged generated ground sprites qualify for automatic revision replacement.</summary>
        public static bool IsApprovedGroundSprite(Sprite sprite)
        {
            if (sprite == null) return false;
            try
            {
                var path = AssetDatabase.GetAssetPath(sprite);
                var spec = Tiles.FirstOrDefault(t => path == LegacyDerivedRoot + "/" + t.Name + ".png");
                string expectedHash;
                if (spec != null) expectedHash = spec.Hash;
                else if (!StudyAGroundR2Assets.TryGroundHash(path, out expectedHash)) return false;
                if (!File.Exists(ProjectPath(path)) || Hash(File.ReadAllBytes(ProjectPath(path))) != expectedHash) return false;
                var info = new StudyATerrainMetrics { DerivedPath = path, NativeSize = new Vector2Int(64, 64),
                    PixelPivot = new Vector2(32, 32), Pivot = new Vector2(.5f, .5f) };
                return StudyAGroundR2Assets.SpriteContractMatches(info);
            }
            catch { return false; }
        }

        private static void ValidateManifest(RawManifest raw)
        {
            if (raw == null || raw.atlas != "Assets/terrain_atlas.png" || raw.tiles == null || raw.tiles.Length != 16 || raw.hero == null)
                throw new InvalidDataException("The actual sixteen-tile/static-hero manifest is required.");
            foreach (var spec in Tiles)
            {
                var entry = raw.tiles.SingleOrDefault(t => t != null && t.name == spec.Name);
                if (entry == null || entry.file != "Assets/" + spec.Name + ".png" || entry.sha256 != spec.Hash || entry.ppu != 64 ||
                    entry.rect_top_left == null || !entry.rect_top_left.SequenceEqual(new[] { spec.X, spec.Y, 64, 64 }) ||
                    entry.rect_unity_bottom_left == null || !entry.rect_unity_bottom_left.SequenceEqual(new[] { spec.X, 192 - spec.Y, 64, 64 }) ||
                    entry.pivot == null || !entry.pivot.SequenceEqual(new[] { .5f, .5f }))
                    throw new InvalidDataException("Manifest name/file/hash/native rectangle/PPU/pivot mismatch: " + spec.Name);
            }
            var h = raw.hero;
            if (h.file != "Assets/hero_idle.png" || h.sha256 != HeroHash || h.ppu != 64 ||
                h.canvas == null || !h.canvas.SequenceEqual(new[] { 64, 80 }) ||
                h.visible_bounds == null || !h.visible_bounds.SequenceEqual(new[] { 8, 8, 56, 69 }) ||
                h.pivot_normalized == null || !h.pivot_normalized.SequenceEqual(new[] { .5f, .1375f }) ||
                h.feet_baseline_y_from_top != 69 || h.collision_world == null || !h.collision_world.SequenceEqual(new[] { .48f, .55f }))
                throw new InvalidDataException("Static hero manifest geometry differs from the reviewed kit.");
        }

        internal static StudyATerrainMetrics Inspect(string name, string role, string hash, Vector2Int size,
            Vector2 pivot, StudyATerrainSettings settings, out Color32[] pixels)
        {
            var source = settings.InputRoot + "/Assets/" + name + ".png";
            pixels = ReadPng(ProjectPath(source), hash, size);
            var minX = size.x; var minY = size.y; var maxX = -1; var maxY = -1;
            for (var y = 0; y < size.y; y++) for (var x = 0; x < size.x; x++)
            {
                if (pixels[y * size.x + x].a == 0) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            if (maxX < 0) throw new InvalidDataException("Empty source image: " + name);
            return new StudyATerrainMetrics
            {
                Name = name, Role = role, NativeSize = size, DerivedSize = size, Pivot = pivot,
                PixelPivot = new Vector2(pivot.x * size.x, pivot.y * size.y),
                WorldSize = new Vector2(size.x / PixelsPerUnit, size.y / PixelsPerUnit),
                AlphaBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1),
                SourceSha256 = hash, SourcePath = source, DerivedPath = settings.DerivedRoot + "/" + name + ".png",
                Note = "Exact native PNG copy, factor 1, PPU 64. Boundary sprites overlay grass on the grass-facing side."
            };
        }

        internal static Color32[] ReadPng(string path, string expectedHash, Vector2Int size)
        {
            var bytes = File.ReadAllBytes(path);
            if (Hash(bytes) != expectedHash) throw new InvalidDataException("Original SHA256 differs from the reviewed kit: " + Path.GetFileName(path));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 || !texture.LoadImage(bytes, false))
                    throw new InvalidDataException("Unreadable PNG: " + path);
                if (texture.width != size.x || texture.height != size.y) throw new InvalidDataException("Native image dimensions differ: " + path);
                var pixels = texture.GetPixels32();
                if (pixels.Any(p => p.a != 0 && p.a != 255)) throw new InvalidDataException("The supplied PNG must retain binary alpha: " + path);
                return pixels;
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void ValidateAtlas(StudyATerrainSettings settings, RawManifest raw, Dictionary<string, Color32[]> individual)
        {
            var atlas = ReadPng(ProjectPath(settings.InputRoot + "/" + raw.atlas), ApprovedAtlasSha256, new Vector2Int(256, 256));
            foreach (var spec in Tiles)
            {
                var image = individual[spec.Name];
                var bottom = 192 - spec.Y;
                for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++)
                    if (!atlas[(bottom + y) * 256 + spec.X + x].Equals(image[y * 64 + x]))
                        throw new InvalidDataException("Atlas RGBA crop differs from the individual PNG: " + spec.Name);
            }
        }

        internal static void ValidateSurfaceBorders(Color32[] first, Color32[] second, string surface)
        {
            if (first.Any(p => p.a != 255) || second.Any(p => p.a != 255))
                throw new InvalidDataException("Grass/soil surfaces must be opaque: " + surface);
            for (var i = 0; i < 64; i++)
                if (!first[i].Equals(second[i]) || !first[63 * 64 + i].Equals(second[63 * 64 + i]) ||
                    !first[i * 64].Equals(second[i * 64]) || !first[i * 64 + 63].Equals(second[i * 64 + 63]))
                    throw new InvalidDataException("Surface variant outer pixels differ: " + surface);
        }

        // Alpha samples confirm which side reveals grass. Opaque green tips near the boundary remain intact.
        internal static void ValidateBoundaryAlpha(Dictionary<string, Color32[]> pixels)
        {
            RequireAlpha(pixels, "dirt_edge_N", 32, 63, 0); RequireAlpha(pixels, "dirt_edge_N", 32, 0, 255);
            RequireAlpha(pixels, "dirt_edge_E", 63, 32, 0); RequireAlpha(pixels, "dirt_edge_E", 0, 32, 255);
            RequireAlpha(pixels, "dirt_edge_S", 32, 0, 0); RequireAlpha(pixels, "dirt_edge_S", 32, 63, 255);
            RequireAlpha(pixels, "dirt_edge_W", 0, 32, 0); RequireAlpha(pixels, "dirt_edge_W", 63, 32, 255);
            foreach (var family in new[] { "convex", "concave" })
            {
                RequireAlpha(pixels, "dirt_" + family + "_NW", 0, 63, 0);
                RequireAlpha(pixels, "dirt_" + family + "_NW", 63, 0, 255);
                RequireAlpha(pixels, "dirt_" + family + "_NE", 63, 63, 0);
                RequireAlpha(pixels, "dirt_" + family + "_NE", 0, 0, 255);
                RequireAlpha(pixels, "dirt_" + family + "_SE", 63, 0, 0);
                RequireAlpha(pixels, "dirt_" + family + "_SE", 0, 63, 255);
                RequireAlpha(pixels, "dirt_" + family + "_SW", 0, 0, 0);
                RequireAlpha(pixels, "dirt_" + family + "_SW", 63, 63, 255);
            }
        }
        private static void RequireAlpha(Dictionary<string, Color32[]> pixels, string name, int x, int y, byte alpha)
        {
            if (pixels[name][y * 64 + x].a != alpha) throw new InvalidDataException("Grass-facing boundary alpha direction differs: " + name);
        }

        internal static void ConfigureImport(StudyATerrainMetrics info)
        {
            AssetDatabase.ImportAsset(info.DerivedPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
            if (importer == null) throw new InvalidDataException("No Derived TextureImporter: " + info.DerivedPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.crunchedCompression = false;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192; importer.anisoLevel = 0; importer.sRGBTexture = true;
            foreach (var platform in TexturePlatforms)
                if (importer.GetPlatformTextureSettings(platform).overridden) importer.ClearPlatformTextureSettings(platform);
            // Explicit RGBA32 also covers the four opaque surfaces, which Automatic may import as RGB24.
            var platformSettings = importer.GetDefaultPlatformTextureSettings();
            platformSettings.format = TextureImporterFormat.RGBA32;
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            platformSettings.crunchedCompression = false;
            platformSettings.maxTextureSize = 8192;
            importer.SetPlatformTextureSettings(platformSettings);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = info.Pivot; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }

        private static void CheckDerived(StudyATerrainReadiness report, StudyATerrainSettings settings)
        {
            var marker = ReadJson<ImportMarker>(ProjectPath(settings.DerivedRoot + "/" + ImportMarkerFileName));
            if (marker == null || marker.schemaVersion != 1 || marker.inputFingerprint != report.Fingerprint || marker.files == null || marker.files.Length != 17)
            { report.Warnings.Add("No Derived marker matching the current seventeen native PNGs."); return; }
            foreach (var info in report.AssetInfos)
            {
                var record = marker.files.SingleOrDefault(f => f != null && f.name == info.Name);
                var path = ProjectPath(info.DerivedPath); var meta = ProjectPath(info.DerivedPath + ".meta");
                if (record == null || record.sha256 != info.SourceSha256 || !File.Exists(path) || Hash(File.ReadAllBytes(path)) != record.sha256 ||
                    !File.Exists(meta) || Hash(File.ReadAllBytes(meta)) != record.metaSha256)
                { report.Warnings.Add("Derived PNG or metadata differs from its preparation marker: " + info.Name); return; }
                var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath);
                var rect = new Rect(0, 0, info.NativeSize.x, info.NativeSize.y);
                if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.spritePixelsPerUnit != PixelsPerUnit || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression ||
                    importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp ||
                    importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.sRGBTexture || importer.anisoLevel != 0 ||
                    importer.maxTextureSize < Math.Max(info.NativeSize.x, info.NativeSize.y) ||
                    sprite == null || sprite.packed || sprite.rect != rect || sprite.textureRect != rect || sprite.texture == null ||
                    sprite.texture.format != TextureFormat.RGBA32 || sprite.texture.width != info.NativeSize.x || sprite.texture.height != info.NativeSize.y ||
                    Math.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > .00001f || Vector2.Distance(sprite.pivot, info.PixelPivot) > .0001f)
                { report.Warnings.Add("Derived native size/Point/PPU/alpha settings differ: " + info.Name); return; }
                if (TexturePlatforms.Any(p => importer.GetPlatformTextureSettings(p).overridden))
                { report.Warnings.Add("Derived retains a platform override: " + info.Name); return; }
                if (importer.GetDefaultPlatformTextureSettings().format != TextureImporterFormat.RGBA32)
                { report.Warnings.Add("Derived default texture format must be explicit RGBA32: " + info.Name); return; }
                var ts = new TextureImporterSettings(); importer.ReadTextureSettings(ts);
                if (ts.spriteMeshType != SpriteMeshType.FullRect || ts.spriteAlignment != (int)SpriteAlignment.Custom ||
                    Vector2.Distance(importer.spritePivot, info.Pivot) > .00001f)
                { report.Warnings.Add("Derived FullRect/custom pivot settings differ: " + info.Name); return; }
            }
            report.DerivedPrepared = true;
            report.DerivedVisuallyReviewed = ReviewMatches(marker.derivedReview, report.Fingerprint);
        }

        private static void WriteMetrics(StudyATerrainReadiness report, StudyATerrainSettings settings) =>
            WriteJson(settings.DerivedRoot + "/" + MetricsManifestFileName, new MetricsManifest
            {
                terrainImportReady = report.TerrainImportReady, derivedPrepared = report.DerivedPrepared,
                sourceVisuallyReviewed = report.SourceVisuallyReviewed, derivedVisuallyReviewed = report.DerivedVisuallyReviewed,
                approvedTerrainReady = report.ApprovedTerrainReady, heroReady = report.HeroReady,
                missingFullTargetRoles = report.MissingFullTargetRoles, inputFingerprint = report.Fingerprint,
                sourceManifestSha256 = report.SourceManifestSha256, sourceManifestPath = settings.InputRoot + "/" + SourceManifestFileName,
                assets = report.AssetInfos.ToArray()
            });

        private static string Fingerprint(byte[] manifest, IEnumerable<StudyATerrainMetrics> infos, StudyATerrainSettings settings)
        {
            var text = new StringBuilder("StudyATerrain-native-copy-v1\n64\n1\n");
            text.AppendLine(Hash(manifest)); text.AppendLine(ApprovedAtlasSha256); text.AppendLine(settings.InputRoot); text.AppendLine(settings.DerivedRoot);
            foreach (var info in infos.OrderBy(i => i.Name, StringComparer.Ordinal)) text.AppendLine(JsonUtility.ToJson(info));
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        private static bool ReviewMatches(ReviewRecord review, string fingerprint) => review != null && review.visuallyReviewed &&
            review.inputFingerprint == fingerprint && !string.IsNullOrWhiteSpace(review.reviewer) && !string.IsNullOrWhiteSpace(review.evidence) &&
            DateTime.TryParse(review.reviewedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
        private static ReviewRecord NewReview(string fingerprint, string reviewer, string evidence) => new ReviewRecord
        {
            visuallyReviewed = true, inputFingerprint = fingerprint, reviewer = reviewer, evidence = evidence,
            reviewedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        internal static void ValidateReviewText(string reviewer, string evidence)
        {
            if (string.IsNullOrWhiteSpace(reviewer) || string.IsNullOrWhiteSpace(evidence))
                throw new ArgumentException("An explicit image reviewer and evidence are required; preparation cannot approve visual quality.");
        }
        internal static void PreflightGeneratedPaths(StudyATerrainSettings settings)
        {
            foreach (var name in new[] { ImportMarkerFileName, SourceReviewFileName, MetricsManifestFileName })
            { ProjectPath(settings.DerivedRoot + "/" + name); ProjectPath(settings.DerivedRoot + "/" + name + ".meta"); }
        }
        private static T ReadJson<T>(string path) where T : class => File.Exists(path)
            ? JsonUtility.FromJson<T>(File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF')) : null;
        private static void WriteJson<T>(string relative, T value) =>
            File.WriteAllText(ProjectPath(relative), JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        internal static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        internal static void ValidateRoots(StudyATerrainSettings settings)
        {
            var art = ProjectPath(OverheadStudyASettings.ArtRoot);
            var input = ProjectPath(settings.InputRoot); var derived = ProjectPath(settings.DerivedRoot);
            if (!Within(art, input) || !Within(art, derived) || input.Equals(art, StringComparison.OrdinalIgnoreCase) ||
                derived.Equals(art, StringComparison.OrdinalIgnoreCase) || Within(input, derived) || Within(derived, input))
                throw new InvalidDataException("Input and Derived must be independent child directories under the Study A art root.");
        }
        internal static string ProjectPath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("A project-relative path is required.");
            return Child(Directory.GetParent(Application.dataPath).FullName, relative);
        }
        private static string Child(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("A relative path is required.");
            root = Path.GetFullPath(root);
            var result = Path.GetFullPath(Path.Combine(root, relative));
            if (!Within(root, result) || result.Equals(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escaped its designated root.");
            for (var path = result; !path.Equals(root, StringComparison.OrdinalIgnoreCase); path = Path.GetDirectoryName(path))
                if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Source/Derived may not use symlinks or junctions.");
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
