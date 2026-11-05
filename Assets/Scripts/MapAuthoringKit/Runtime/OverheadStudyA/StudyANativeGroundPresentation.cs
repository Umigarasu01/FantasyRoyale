using System;
using System.Linq;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    [Serializable]
    public sealed class StudyANativeGroundPresentationProfile
    {
        public string Schema = "FantasyRoyale.NativeGroundPresentation.v1";
        public string Id;
        public int Version = 1, PixelsPerUnit = 64;
        public string RecipeId = "E", TargetLocationId = "C";
        public StudyANativeGroundPresentationEntry[] Entries;
    }

    [Serializable]
    public sealed class StudyANativeGroundPresentationEntry
    {
        public string SourceTexturePath, SourceTextureSHA256, SourceTextureMetaSHA256;
        public Vector2Int SourceExtentPx;
        // Both source and target rectangles use native bottom-left pixel coordinates.
        public RectInt SourceRectPx, TargetRectLocalPx;
        public int RotationDegrees;
    }

    [Serializable]
    public sealed class StudyANativeGroundPresentationPlan
    {
        public bool Applied;
        public string ProfileId, RecipeId, TargetLocationId;
        public int PixelsPerUnit = 64;
        public Vector2Int TargetOriginPx, TargetExtentPx;
        public StudyANativeGroundPresentationPlacement[] Placements = Array.Empty<StudyANativeGroundPresentationPlacement>();
    }

    [Serializable]
    public sealed class StudyANativeGroundPresentationPlacement
    {
        public string SourceTexturePath, SourceTextureSHA256, SourceTextureMetaSHA256;
        public Vector2Int SourceExtentPx;
        public RectInt SourceRectPx, TargetRectLocalPx, TargetRectWorldPx;
        public Vector2 WorldCenterPx;
        public int RotationDegrees;
    }

    /// <summary>Pure native-pixel ground presentation plan. No assets, rendering, navigation or geometry are modified.</summary>
    public static class StudyANativeGroundPresentation
    {
        public static StudyANativeGroundPresentationPlan BuildPlan(StudyANativeGroundPresentationProfile profile,
            StudyANativeLocationAssembly assembly, string recipeId)
        {
            Need(profile != null && assembly != null && assembly.Blocks != null && assembly.Connections != null,
                "Presentation requires a profile and the actual generated assembly.");
            Need(profile.Schema == "FantasyRoyale.NativeGroundPresentation.v1" && profile.Version == 1 &&
                !string.IsNullOrWhiteSpace(profile.Id) && profile.PixelsPerUnit == 64 && profile.RecipeId == "E" &&
                profile.TargetLocationId == "C" && profile.Entries != null && profile.Entries.Length >= 1 && profile.Entries.Length <= 2,
                "Only the bounded E/C ground presentation profile at native64PPU is supported.");
            Need(recipeId == "E" || recipeId == "S0", "Presentation requires a recognized S0/E recipe.");
            foreach (var entry in profile.Entries)
            {
                Need(entry != null && ValidAssetPath(entry.SourceTexturePath) && Hash(entry.SourceTextureSHA256) &&
                    Hash(entry.SourceTextureMetaSHA256), "A presentation entry requires explicit immutable source PNG/meta hashes.");
                Need((entry.SourceExtentPx.x == 512 && entry.SourceExtentPx.y == 384) ||
                    (entry.SourceExtentPx.x == 640 && entry.SourceExtentPx.y == 512), "Only original C or D native ground dimensions are supported.");
                Need(entry.RotationDegrees == 0 && Inside(entry.SourceRectPx, entry.SourceExtentPx) &&
                    entry.TargetRectLocalPx.width == entry.SourceRectPx.width && entry.TargetRectLocalPx.height == entry.SourceRectPx.height &&
                    Inside(entry.TargetRectLocalPx, new Vector2Int(512, 384)), "Presentation copies unrotated source pixels1:1 entirely inside the C floor.");
                Need(entry.TargetRectLocalPx.x >= 0 && entry.TargetRectLocalPx.y >= 128 &&
                    entry.TargetRectLocalPx.x + entry.TargetRectLocalPx.width <= 64 &&
                    entry.TargetRectLocalPx.y + entry.TargetRectLocalPx.height <= 256,
                    "Ground presentation must stay inside the reviewed C west64x128px terminal region and preserve all accepted contacts.");
            }
            for (var i = 0; i < profile.Entries.Length; i++)
            for (var j = i + 1; j < profile.Entries.Length; j++)
                Need(!Overlap(profile.Entries[i].TargetRectLocalPx, profile.Entries[j].TargetRectLocalPx),
                    "Opaque presentation rectangles must not overlap; their render order must be irrelevant.");
            var result = new StudyANativeGroundPresentationPlan { ProfileId = profile.Id, RecipeId = recipeId,
                TargetLocationId = profile.TargetLocationId, PixelsPerUnit = 64 };
            // Auto selecting S0 keeps its source-native pixels unchanged.
            if (recipeId == "S0") return result;
            Need(assembly.Blocks.Count == 4 && assembly.Blocks.All(b => b != null) &&
                assembly.Blocks.Select(b => b.LocationId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "A", "B", "C", "D" }),
                "E ground presentation requires the recognized complete four-location assembly.");
            foreach (var connection in assembly.Connections)
                Need(connection != null && connection.FromBlock >= 0 && connection.FromBlock < 4 &&
                    connection.ToBlock >= 0 && connection.ToBlock < 4, "Assembly connection indices are invalid.");
            var cIndex = assembly.Blocks.FindIndex(b => b.LocationId == "C");
            var c = assembly.Blocks[cIndex];
            Need(c.ExtentPx.x == 512 && c.ExtentPx.y == 384 &&
                !assembly.Connections.Any(k => (k.FromBlock == cIndex && k.FromPortId == "C.W.road") ||
                    (k.ToBlock == cIndex && k.ToPortId == "C.W.road")), "The C west road must be unused before it may be visually terminated.");
            Need(assembly.Connections.Count == 3 && Link(assembly, "A", "A.E.road", "B", "B.W.road") &&
                Link(assembly, "B", "B.E.road", "D", "D.W.road") && Link(assembly, "D", "D.S.road", "C", "C.N.road"),
                "Only the recognized A-B-D-C E connection graph supports this presentation.");
            result.Applied = true; result.TargetOriginPx = c.OriginPx; result.TargetExtentPx = c.ExtentPx;
            result.Placements = profile.Entries.Select(e => new StudyANativeGroundPresentationPlacement {
                SourceTexturePath = e.SourceTexturePath, SourceTextureSHA256 = e.SourceTextureSHA256,
                SourceTextureMetaSHA256 = e.SourceTextureMetaSHA256, SourceExtentPx = e.SourceExtentPx,
                SourceRectPx = e.SourceRectPx, TargetRectLocalPx = e.TargetRectLocalPx, RotationDegrees = 0,
                TargetRectWorldPx = new RectInt(c.OriginPx.x + e.TargetRectLocalPx.x, c.OriginPx.y + e.TargetRectLocalPx.y,
                    e.TargetRectLocalPx.width, e.TargetRectLocalPx.height),
                WorldCenterPx = new Vector2(c.OriginPx.x + e.TargetRectLocalPx.x + e.TargetRectLocalPx.width * .5f,
                    c.OriginPx.y + e.TargetRectLocalPx.y + e.TargetRectLocalPx.height * .5f)
            }).ToArray();
            return result;
        }

        private static bool Link(StudyANativeLocationAssembly a, string from, string fromPort, string to, string toPort) =>
            a.Connections.Any(k => (a.Blocks[k.FromBlock].LocationId == from && k.FromPortId == fromPort &&
                a.Blocks[k.ToBlock].LocationId == to && k.ToPortId == toPort) ||
                (a.Blocks[k.ToBlock].LocationId == from && k.ToPortId == fromPort &&
                a.Blocks[k.FromBlock].LocationId == to && k.FromPortId == toPort));
        private static bool Inside(RectInt r, Vector2Int extent) => r.x >= 0 && r.y >= 0 && r.width > 0 && r.height > 0 &&
            (long)r.x + r.width <= extent.x && (long)r.y + r.height <= extent.y;
        private static bool Overlap(RectInt a, RectInt b) => a.x < (long)b.x + b.width && (long)a.x + a.width > b.x &&
            a.y < (long)b.y + b.height && (long)a.y + a.height > b.y;
        private static bool Hash(string s) => s != null && s.Length == 64 && s.All(Uri.IsHexDigit);
        private static bool ValidAssetPath(string s) => s != null && s.StartsWith("Assets/", StringComparison.Ordinal) &&
            s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !s.Contains(":") && !s.Contains("\\") &&
            s.Split('/').All(p => p.Length != 0 && p != "." && p != ".." && !p.EndsWith(" ", StringComparison.Ordinal) && !p.EndsWith(".", StringComparison.Ordinal));
        private static void Need(bool valid, string message) { if (!valid) throw new ArgumentException(message); }
    }
}
