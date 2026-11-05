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
    /// <summary>Ground-only revision. Reuses the reviewed R1 hero; never writes an R1 image or review.</summary>
    internal static class StudyAGroundR2Assets
    {
        internal const string ManifestHash = "6e141a25a3c08bdc3e23fe1cbce8c1069cb1a567e42a28d36a55e992cfead2c3";
        internal const string AtlasHash = "237e0514b8aa3cc2b8f86813816402e6444d18edc838eb6ef57de31ce5de20db";
        private static readonly string[] Platforms =
            { "Standalone", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo Switch", "tvOS" };
        private sealed class Spec
        {
            public readonly string Name, Hash; public readonly int X, Y;
            public Spec(string name, string hash, int x, int y) { Name = name; Hash = hash; X = x; Y = y; }
        }
        private static readonly Spec[] Specs =
        {
            new Spec("grass_01", "cae2358d1bba4c0f3fef9bb7cfcdb21e30363d1603651e32a2b7e3d227ba6cb4", 0, 0),
            new Spec("grass_02", "4b9d6389c0c7820ac77b17512ec1559c9274c6e0a56cd2af90a964a49ae607c8", 64, 0),
            new Spec("dirt_01", "be684099d91755e1a6a88f43ae097106f0195450f603fe6ca8bbff7331b5e6d4", 128, 0),
            new Spec("dirt_02", "8789b69f1075a35517bfe771a2abc95a4573b8595f3aeee75a4f79b11a99e511", 192, 0),
            new Spec("dirt_edge_N", "cb30f132686128482c59e8dbec06957babc232e6c99300ac684f541b1d04f973", 0, 64),
            new Spec("dirt_edge_E", "ce289cde337697744c0f90e949ec980374e337ec6eff6a1ba5a35c0761d911b4", 64, 64),
            new Spec("dirt_edge_S", "085fc5e76f41e5db3bef67491019ced7829d6605c4c484d9fc118a0b140a4b19", 128, 64),
            new Spec("dirt_edge_W", "0844b0e994b5231ee3462159bf839701d891ef2d995916bf513c2dbbaff72ccf", 192, 64),
            new Spec("dirt_convex_NW", "571a0e16c8b623b422ef3b718a34a20ab1e37c0afef91f36c0d3b1a4b43f75c6", 0, 128),
            new Spec("dirt_convex_NE", "fa7ed096de9b8f569e703ee67dea2a989d342f236533ed7d917aa8be1ab1e472", 64, 128),
            new Spec("dirt_convex_SE", "fe6253bd1254c1be92f46c53d87c1db3c0b95f7ef8718b0c15ac1ea666e05e4a", 128, 128),
            new Spec("dirt_convex_SW", "affa47973a39f151b4e839bd574fa3e40a05a3d96bdf134e3b4a2b6b6893c1ca", 192, 128),
            new Spec("dirt_concave_NW", "f5758c106e98ef2599df8226dfcc1f7fba4746bcd6ad70168838d3002b1b27c3", 0, 192),
            new Spec("dirt_concave_NE", "c141484f40cfe23e40b4fef2e04341d4c0ab1b79f5696ec9bb847f5f8fa8aa02", 64, 192),
            new Spec("dirt_concave_SE", "4e747977b066b77d8e30675066ed3554f85e7ed56f3e961650923132602a74c7", 128, 192),
            new Spec("dirt_concave_SW", "14123506d7f2e6cc1a20938d05429fd7356ba27a082f9960327e449c01036484", 192, 192),
        };
        [Serializable] private sealed class Raw
        {
            public string atlas = null; public string revision = null; public RawTile[] tiles = null;
            public ImportSettings import_settings = null;
        }
        [Serializable] private sealed class RawTile
        {
            public string name = null; public string file = null; public string sha256 = null;
            public int[] rect_top_left = null; public int[] rect_unity_bottom_left = null;
            public int ppu = 0; public float[] pivot = null;
        }
        [Serializable] private sealed class ImportSettings
        {
            public int pixels_per_unit = 0; public string filter = null; public bool mipmaps = true;
            public string compression = null; public bool sRGB = false; public string alpha = null; public float[] pivot = null;
        }
        [Serializable] private sealed class Review
        {
            public bool visuallyReviewed; public string inputFingerprint, reviewer, evidence, reviewedAtUtc;
        }
        [Serializable] private sealed class FileRecord
        {
            public string name, sha256, metaSha256;
        }
        [Serializable] private sealed class Marker
        {
            public int schemaVersion = 1; public string revision = "r2-ground-clusters";
            public string inputFingerprint; public Review derivedReview; public FileRecord[] files;
        }
        [Serializable] private sealed class Metrics
        {
            public int schemaVersion = 1; public string kind = "ground-r2-and-reused-r1-static-hero";
            public float pixelsPerUnit = 64; public int integerDownsample = 1;
            public bool terrainImportReady, derivedPrepared, sourceVisuallyReviewed, derivedVisuallyReviewed, approvedTerrainReady, heroReady;
            public bool fullTargetReady = false; public string[] missingFullTargetRoles;
            public string inputFingerprint, sourceManifestSha256, sourceManifestPath, atlasSourceSha256;
            public StudyATerrainMetrics[] assets;
        }
        private static StudyATerrainSettings LegacySettings => new StudyATerrainSettings
        { InputRoot = StudyATerrainAssets.LegacyInputRoot, DerivedRoot = StudyATerrainAssets.LegacyDerivedRoot };

        internal static bool UsesR2(StudyATerrainSettings settings)
        {
            if (string.Equals(settings.InputRoot?.Replace('\\', '/').TrimEnd('/'), StudyATerrainAssets.DefaultInputRoot, StringComparison.OrdinalIgnoreCase)) return true;
            try
            {
                var path = StudyATerrainAssets.ProjectPath(settings.InputRoot + "/" + StudyATerrainAssets.SourceManifestFileName);
                return File.Exists(path) && StudyATerrainAssets.Hash(File.ReadAllBytes(path)) == ManifestHash;
            }
            catch { return false; }
        }

        internal static bool TryGroundHash(string path, out string hash)
        {
            var spec = Specs.FirstOrDefault(s => path == StudyATerrainAssets.R2DerivedRoot + "/" + s.Name + ".png");
            hash = spec?.Hash;
            return spec != null;
        }

        internal static StudyATerrainReadiness CheckReadiness(StudyATerrainSettings settings)
        {
            var report = new StudyATerrainReadiness();
            try
            {
                ValidateRoots(settings);
                var manifest = File.ReadAllBytes(PathFor(settings.InputRoot + "/" + StudyATerrainAssets.SourceManifestFileName));
                report.SourceManifestSha256 = StudyATerrainAssets.Hash(manifest);
                if (report.SourceManifestSha256 != ManifestHash) throw new InvalidDataException("Ground R2 manifest differs from the supplied approved original.");
                var raw = JsonUtility.FromJson<Raw>(Encoding.UTF8.GetString(manifest).TrimStart('\uFEFF'));
                ValidateManifest(raw);
                var pixels = new Dictionary<string, Color32[]>(StringComparer.Ordinal);
                foreach (var spec in Specs)
                {
                    Color32[] image;
                    var role = spec.Name.StartsWith("grass_", StringComparison.Ordinal) ? "grass" :
                        spec.Name.StartsWith("dirt_0", StringComparison.Ordinal) ? "soil" : "soilBoundary";
                    report.AssetInfos.Add(StudyATerrainAssets.Inspect(spec.Name, role, spec.Hash,
                        new Vector2Int(64, 64), new Vector2(.5f, .5f), settings, out image));
                    pixels.Add(spec.Name, image);
                }
                Color32[] atlas;
                AtlasInfo(settings, out atlas);
                foreach (var spec in Specs)
                    for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++)
                        if (!atlas[(192 - spec.Y + y) * 256 + spec.X + x].Equals(pixels[spec.Name][y * 64 + x]))
                            throw new InvalidDataException("R2 atlas crop differs from its individual tile: " + spec.Name);
                StudyATerrainAssets.ValidateSurfaceBorders(pixels["grass_01"], pixels["grass_02"], "R2 grass");
                StudyATerrainAssets.ValidateSurfaceBorders(pixels["dirt_01"], pixels["dirt_02"], "R2 soil");
                StudyATerrainAssets.ValidateBoundaryAlpha(pixels);
                var legacy = StudyATerrainAssets.CheckReadiness(LegacySettings);
                if (!legacy.HeroReady) throw new InvalidDataException("The unchanged R1 hero and its existing reviews must remain valid. " + legacy);
                foreach (var info in legacy.AssetInfos.Where(i => i.Role == "soilBoundary"))
                {
                    var old = StudyATerrainAssets.ReadPng(PathFor(info.SourcePath), info.SourceSha256, new Vector2Int(64, 64));
                    var current = pixels[info.Name];
                    for (var i = 0; i < old.Length; i++)
                        if (old[i].a != current[i].a || (old[i].a != 0 && (old[i].g > old[i].r) != (current[i].g > current[i].r)))
                            throw new InvalidDataException("R2 changed a reviewed boundary alpha mask or grass-tip position: " + info.Name);
                }
                // This metrics entry points to the unchanged R1 sprite. It is never part of an R2 write operation.
                report.AssetInfos.Add(legacy.AssetInfos.Single(i => i.Role == "hero"));
                var fingerprint = new StringBuilder("StudyAGroundR2-native-copy-v1\n64\n1\n");
                fingerprint.AppendLine(ManifestHash); fingerprint.AppendLine(AtlasHash);
                fingerprint.AppendLine(settings.InputRoot); fingerprint.AppendLine(settings.DerivedRoot); fingerprint.AppendLine(legacy.Fingerprint);
                foreach (var info in report.AssetInfos.OrderBy(i => i.Name, StringComparer.Ordinal)) fingerprint.AppendLine(JsonUtility.ToJson(info));
                report.Fingerprint = StudyATerrainAssets.Hash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
                try { report.SourceVisuallyReviewed = Matches(Read<Review>(PathFor(settings.DerivedRoot + "/" + StudyATerrainAssets.SourceReviewFileName)), report.Fingerprint); }
                catch (Exception ex) { report.Warnings.Add("R2 source review is invalid: " + ex.Message); }
                try { CheckDerived(report, settings); }
                catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Warnings.Add("R2 Derived state is invalid: " + ex.Message); }
                if (!report.SourceVisuallyReviewed) report.Warnings.Add("R2 actual ground images require their own explicit source review.");
                if (!report.DerivedVisuallyReviewed) report.Warnings.Add("R2 imported sprites require their own explicit derived review.");
                report.Warnings.Add("R2 replaces ground pixels only and reuses the reviewed R1 static hero. Water/bridge/flower remain missing; FullTargetReady=false.");
            }
            catch (Exception ex) { report.Errors.Add(ex.Message); }
            return report;
        }

        internal static StudyATerrainReadiness PrepareDerived(StudyATerrainSettings settings)
        {
            var report = CheckReadiness(settings);
            if (!report.TerrainImportReady) return report;
            try
            {
                var infos = CopyInfos(report, settings);
                var bytesByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var info in infos)
                {
                    PathFor(info.DerivedPath); PathFor(info.DerivedPath + ".meta");
                    var bytes = File.ReadAllBytes(PathFor(info.SourcePath));
                    if (StudyATerrainAssets.Hash(bytes) != info.SourceSha256) throw new IOException("R2 source changed during preparation: " + info.Name);
                    bytesByName.Add(info.Name, bytes);
                }
                var recheck = CheckReadiness(settings);
                if (!recheck.TerrainImportReady || recheck.Fingerprint != report.Fingerprint) throw new IOException("R2 source or legacy hero changed during preparation.");
                StudyATerrainAssets.PreflightGeneratedPaths(settings);
                Directory.CreateDirectory(PathFor(settings.DerivedRoot));
                var marker = new Marker { inputFingerprint = report.Fingerprint, files = Array.Empty<FileRecord>() };
                Write(settings.DerivedRoot + "/" + StudyATerrainAssets.ImportMarkerFileName, marker);
                var records = new List<FileRecord>();
                foreach (var info in infos)
                {
                    File.WriteAllBytes(PathFor(info.DerivedPath), bytesByName[info.Name]);
                    StudyATerrainAssets.ConfigureImport(info);
                    records.Add(new FileRecord { name = info.Name, sha256 = info.SourceSha256,
                        metaSha256 = StudyATerrainAssets.Hash(File.ReadAllBytes(PathFor(info.DerivedPath + ".meta"))) });
                }
                marker.files = records.ToArray();
                Write(settings.DerivedRoot + "/" + StudyATerrainAssets.ImportMarkerFileName, marker);
                report = CheckReadiness(settings); WriteMetrics(report, settings);
            }
            catch (Exception ex) { report.DerivedPrepared = false; report.DerivedVisuallyReviewed = false; report.Errors.Add("R2 preparation failed: " + ex.Message); }
            return report;
        }

        internal static StudyATerrainReadiness RecordSourceVisualReview(string reviewer, string evidence, StudyATerrainSettings settings)
        {
            StudyATerrainAssets.ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings);
            if (!report.TerrainImportReady) throw new InvalidOperationException(report.ToString());
            StudyATerrainAssets.PreflightGeneratedPaths(settings); Directory.CreateDirectory(PathFor(settings.DerivedRoot));
            Write(settings.DerivedRoot + "/" + StudyATerrainAssets.SourceReviewFileName, NewReview(report.Fingerprint, reviewer, evidence));
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        internal static StudyATerrainReadiness RecordDerivedVisualReview(string reviewer, string evidence, StudyATerrainSettings settings)
        {
            StudyATerrainAssets.ValidateReviewText(reviewer, evidence);
            var report = CheckReadiness(settings);
            if (!report.TerrainImportReady || !report.DerivedPrepared || !report.SourceVisuallyReviewed) throw new InvalidOperationException(report.ToString());
            StudyATerrainAssets.PreflightGeneratedPaths(settings);
            var marker = Read<Marker>(PathFor(settings.DerivedRoot + "/" + StudyATerrainAssets.ImportMarkerFileName));
            marker.derivedReview = NewReview(report.Fingerprint, reviewer, evidence);
            Write(settings.DerivedRoot + "/" + StudyATerrainAssets.ImportMarkerFileName, marker);
            report = CheckReadiness(settings); WriteMetrics(report, settings); return report;
        }

        internal static StudyATerrainSpriteSet LoadSpriteSet(StudyATerrainSettings settings)
        {
            var report = CheckReadiness(settings);
            if (!report.ApprovedTerrainReady || !report.HeroReady) throw new InvalidOperationException(report.ToString());
            var names = report.AssetInfos.ToDictionary(i => i.Name, i => AssetDatabase.LoadAssetAtPath<Sprite>(i.DerivedPath), StringComparer.Ordinal);
            return new StudyATerrainSpriteSet
            {
                ByName = names, GrassVariants = new[] { names["grass_01"], names["grass_02"] },
                SoilVariants = new[] { names["dirt_01"], names["dirt_02"] },
                Boundaries = StudyATerrainAssets.BoundaryNames.ToDictionary(p => p.Key, p => names[p.Value]),
                Hero = names["hero_idle"], HeroMetrics = report.AssetInfos.Single(i => i.Role == "hero"), Fingerprint = report.Fingerprint
            };
        }

        private static StudyATerrainMetrics[] CopyInfos(StudyATerrainReadiness report, StudyATerrainSettings settings)
        {
            Color32[] ignored;
            return report.AssetInfos.Where(i => i.Role != "hero").Concat(new[] { AtlasInfo(settings, out ignored) }).ToArray();
        }
        private static StudyATerrainMetrics AtlasInfo(StudyATerrainSettings settings, out Color32[] pixels) =>
            StudyATerrainAssets.Inspect("terrain_atlas", "atlas", AtlasHash, new Vector2Int(256, 256), new Vector2(.5f, .5f), settings, out pixels);

        private static void CheckDerived(StudyATerrainReadiness report, StudyATerrainSettings settings)
        {
            var marker = Read<Marker>(PathFor(settings.DerivedRoot + "/" + StudyATerrainAssets.ImportMarkerFileName));
            if (marker == null || marker.schemaVersion != 1 || marker.revision != "r2-ground-clusters" || marker.inputFingerprint != report.Fingerprint ||
                marker.files == null || marker.files.Length != 17)
            { report.Warnings.Add("No prepared R2 sixteen-tile plus atlas marker for the current fingerprint."); return; }
            foreach (var info in CopyInfos(report, settings))
            {
                var file = marker.files.SingleOrDefault(f => f != null && f.name == info.Name);
                var path = PathFor(info.DerivedPath); var meta = PathFor(info.DerivedPath + ".meta");
                if (file == null || file.sha256 != info.SourceSha256 || !File.Exists(path) || StudyATerrainAssets.Hash(File.ReadAllBytes(path)) != file.sha256 ||
                    !File.Exists(meta) || StudyATerrainAssets.Hash(File.ReadAllBytes(meta)) != file.metaSha256)
                { report.Warnings.Add("R2 Derived PNG/meta changed: " + info.Name); return; }
                if (!SpriteContractMatches(info))
                { report.Warnings.Add("R2 Derived native/Point/PPU64/RGBA32/FullRect settings differ: " + info.Name); return; }
            }
            report.DerivedPrepared = true;
            report.DerivedVisuallyReviewed = Matches(marker.derivedReview, report.Fingerprint);
        }

        internal static bool SpriteContractMatches(StudyATerrainMetrics info)
        {
            var importer = AssetImporter.GetAtPath(info.DerivedPath) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(info.DerivedPath);
            var rect = new Rect(0, 0, info.NativeSize.x, info.NativeSize.y);
            if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.spritePixelsPerUnit != 64 || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression ||
                importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.sRGBTexture || importer.anisoLevel != 0 ||
                importer.maxTextureSize < Math.Max(info.NativeSize.x, info.NativeSize.y) ||
                importer.GetDefaultPlatformTextureSettings().format != TextureImporterFormat.RGBA32 ||
                Platforms.Any(p => importer.GetPlatformTextureSettings(p).overridden) ||
                sprite == null || sprite.packed || sprite.rect != rect || sprite.textureRect != rect || sprite.texture == null ||
                sprite.texture.format != TextureFormat.RGBA32 || sprite.texture.width != info.NativeSize.x || sprite.texture.height != info.NativeSize.y ||
                Math.Abs(sprite.pixelsPerUnit - 64) > .00001f || Vector2.Distance(sprite.pivot, info.PixelPivot) > .0001f) return false;
            var ts = new TextureImporterSettings(); importer.ReadTextureSettings(ts);
            return ts.spriteMeshType == SpriteMeshType.FullRect && ts.spriteAlignment == (int)SpriteAlignment.Custom &&
                Vector2.Distance(importer.spritePivot, info.Pivot) <= .00001f;
        }

        private static void ValidateManifest(Raw raw)
        {
            if (raw == null || raw.revision != "r2-ground-clusters" || raw.atlas != "Assets/terrain_atlas.png" || raw.tiles == null || raw.tiles.Length != 16)
                throw new InvalidDataException("The supplied ground-only R2 manifest is required.");
            foreach (var spec in Specs)
            {
                var tile = raw.tiles.SingleOrDefault(t => t != null && t.name == spec.Name);
                if (tile == null || tile.file != "Assets/" + spec.Name + ".png" || tile.sha256 != spec.Hash || tile.ppu != 64 ||
                    tile.rect_top_left == null || !tile.rect_top_left.SequenceEqual(new[] { spec.X, spec.Y, 64, 64 }) ||
                    tile.rect_unity_bottom_left == null || !tile.rect_unity_bottom_left.SequenceEqual(new[] { spec.X, 192 - spec.Y, 64, 64 }) ||
                    tile.pivot == null || !tile.pivot.SequenceEqual(new[] { .5f, .5f })) throw new InvalidDataException("R2 tile declaration differs: " + spec.Name);
            }
            var s = raw.import_settings;
            if (s == null || s.pixels_per_unit != 64 || s.filter != "Point" || s.mipmaps || s.compression != "None" || !s.sRGB || s.alpha != "binary" ||
                s.pivot == null || !s.pivot.SequenceEqual(new[] { .5f, .5f })) throw new InvalidDataException("R2 import declaration differs.");
        }
        private static void ValidateRoots(StudyATerrainSettings settings)
        {
            StudyATerrainAssets.ValidateRoots(settings);
            var derived = PathFor(settings.DerivedRoot).TrimEnd(Path.DirectorySeparatorChar);
            foreach (var old in new[] { StudyATerrainAssets.LegacyInputRoot, StudyATerrainAssets.LegacyDerivedRoot })
            {
                var protectedPath = PathFor(old).TrimEnd(Path.DirectorySeparatorChar);
                if (derived.Equals(protectedPath, StringComparison.OrdinalIgnoreCase) || derived.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    protectedPath.StartsWith(derived + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("R2 Derived must not overlap the preserved R1 input or derived directory.");
            }
        }
        private static void WriteMetrics(StudyATerrainReadiness report, StudyATerrainSettings settings) =>
            Write(settings.DerivedRoot + "/" + StudyATerrainAssets.MetricsManifestFileName, new Metrics
            {
                terrainImportReady = report.TerrainImportReady, derivedPrepared = report.DerivedPrepared,
                sourceVisuallyReviewed = report.SourceVisuallyReviewed, derivedVisuallyReviewed = report.DerivedVisuallyReviewed,
                approvedTerrainReady = report.ApprovedTerrainReady, heroReady = report.HeroReady,
                missingFullTargetRoles = report.MissingFullTargetRoles, inputFingerprint = report.Fingerprint,
                sourceManifestSha256 = report.SourceManifestSha256, sourceManifestPath = settings.InputRoot + "/" + StudyATerrainAssets.SourceManifestFileName,
                atlasSourceSha256 = AtlasHash, assets = report.AssetInfos.ToArray()
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
