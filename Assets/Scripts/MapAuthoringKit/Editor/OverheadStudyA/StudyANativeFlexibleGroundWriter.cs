using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyANativeFlexibleGroundResult
    {
        [NonSerialized] public Color32[] PixelsBL;
        public int WidthPx, HeightPx, Seed, CollarPx, VisibleSoilWidthPx;
        public int FloorPixels, TransparentPixels, SoilPixels, NativeEdgeDetailPixels;
        public int CollarComparedPixels, CollarMismatchPixels, OutsideFloorOpaquePixels;
        public int GrassPatchCount, SoilPatchCount, PatchCandidateCount;
        public int EndpointTransitionSourcePixels;
        public int EndpointTransitionRejectedTerrainPixels, EndpointTransitionTerrainChangedPixels;
        public int LeftTransitionMinDepthPx, LeftTransitionMaxDepthPx;
        public int RightTransitionMinDepthPx, RightTransitionMaxDepthPx;
        public long EndpointTransitionSelectedCost;
        public long SelectedPatchOverlapRGBError;
        public int LeftArtSoilLowBL, LeftArtSoilHighExclusiveBL;
        public int RightArtSoilLowBL, RightArtSoilHighExclusiveBL;
        public string PixelSHA256BL, SourceBEPixelSHA256BL, SourceDWPixelSHA256BL;
        public string Method = "native-colour32-quilt64-overlap16-mincut-v1/native-edge-column-detail-v1/endpoint-native-extension-terrain-matched-mincut-v3";
        public string CoordinateContract = "x+y*width; all pixels and floor arrays bottom-origin; source local row equals J local row";
        public string Qualification = "Candidate ground only; source metadata/admission, saved assets, GPU appearance and actual Physics are checked by caller.";
    }

    /// <summary>
    /// Pure procedural candidate ground. Source arrays are read-only; no importer,
    /// source texture, prefab, scene, collider, owner, foot or navigation mutation.
    /// The caller pins/decodes received source PNGs and saves a fresh output asset.
    /// </summary>
    public static class StudyANativeFlexibleGroundWriter
    {
        private const int PatchPx = 64, OverlapPx = 16, Candidates = 8;
        private const int ArtFanPx = 64, FringePx = 12;

        private struct Region
        {
            public Color32[] Pixels;
            public int Width;
            public RectInt Rect;
            public Region(Color32[] pixels, int width, RectInt rect)
            { Pixels = pixels; Width = width; Rect = rect; }
        }

        private struct Patch
        {
            public Region Region;
            public int X, Y;
            public Color32 Pixel(int x, int y) => Region.Pixels[(Y + y) * Region.Width + X + x];
        }

        private struct SoilRun
        {
            public int Low, High;
            public SoilRun(int low, int high) { Low = low; High = high; }
        }

        public static StudyANativeFlexibleGroundResult BuildPixels(int widthPx, int heightPx, int seed,
            byte[] floorMaskBL, float[] soilCentreYByXBL,
            Color32[] acceptedBEPixelsBL, int beWidth, int beHeight,
            Color32[] acceptedDWPixelsBL, int dwWidth, int dwHeight,
            int collarPx = 32, int soilWidthPx = 96)
        {
            if ((widthPx != 256 && widthPx != 384 && widthPx != 512) || heightPx != 384)
                throw new ArgumentException("Pilot ground supports only 4/6/8 by 6 world units at PPU64.");
            if (collarPx != 32 || soilWidthPx != 96)
                throw new ArgumentException("This candidate writer requires pinned 32px collars and visible 96px soil.");
            if (floorMaskBL == null || floorMaskBL.Length != checked(widthPx * heightPx) ||
                soilCentreYByXBL == null || soilCentreYByXBL.Length != widthPx)
                throw new ArgumentException("The exact floor mask and per-column centreline must match the native bbox.");
            RequireSource(acceptedBEPixelsBL, beWidth, beHeight, 384, 640, "B_E");
            RequireSource(acceptedDWPixelsBL, dwWidth, dwHeight, 640, 512, "D_W");
            for (var x = 0; x < widthPx; x++)
            {
                var c = soilCentreYByXBL[x];
                if (float.IsNaN(c) || float.IsInfinity(c) || c < 0 || c > heightPx)
                    throw new ArgumentException("Nonfinite or out-of-bbox soil centreline.");
                if ((x < collarPx && Math.Abs(c - 297f) > .001f) ||
                    (x >= widthPx - collarPx && Math.Abs(c - 169f) > .001f))
                    throw new ArgumentException("Endpoint centreline must retain west297/east169 local pixels.");
                if (x < collarPx || x >= widthPx - collarPx)
                    for (var y = 0; y < heightPx; y++)
                        if (floorMaskBL[y * widthPx + x] == 0)
                            throw new ArgumentException("The entire 384px contact and all 32px endpoint floor collars must be present.");
            }

            var result = new StudyANativeFlexibleGroundResult
            {
                WidthPx = widthPx, HeightPx = heightPx, Seed = seed, CollarPx = collarPx,
                VisibleSoilWidthPx = soilWidthPx,
                SourceBEPixelSHA256BL = Hash(acceptedBEPixelsBL),
                SourceDWPixelSHA256BL = Hash(acceptedDWPixelsBL)
            };
            // These source regions contain native grass/soil, never independent owners.
            // Regions are BL texel coordinates. Region selection is versioned in this code.
            var grassRegions = new[]
            {
                new Region(acceptedDWPixelsBL, dwWidth, new RectInt(32, 440, 224, 72)),
                new Region(acceptedDWPixelsBL, dwWidth, new RectInt(0, 0, 256, 96)),
                new Region(acceptedDWPixelsBL, dwWidth, new RectInt(448, 0, 192, 96)),
                new Region(acceptedBEPixelsBL, beWidth, new RectInt(0, 568, 384, 72))
            };
            var soilRegions = new[]
            {
                new Region(acceptedDWPixelsBL, dwWidth, new RectInt(256, 256, 256, 112))
            };
            var rng = unchecked((uint)seed ^ 0x71a92bcdu);
            var grass = Quilt(widthPx, heightPx, grassRegions, ref rng, result, false);
            var soil = Quilt(widthPx, heightPx, soilRegions, ref rng, result, true);
            var left = ReadMainSoilRun(acceptedBEPixelsBL, beWidth, beHeight, beWidth - collarPx, 297);
            var right = ReadMainSoilRun(acceptedDWPixelsBL, dwWidth, dwHeight, collarPx - 1, 169);
            result.LeftArtSoilLowBL = left.Low; result.LeftArtSoilHighExclusiveBL = left.High;
            result.RightArtSoilLowBL = right.Low; result.RightArtSoilHighExclusiveBL = right.High;

            const int detailStartX = 128, detailCount = 192;
            var nativeRuns = new SoilRun[detailCount];
            for (var i = 0; i < detailCount; i++)
                nativeRuns[i] = ReadMainSoilRun(acceptedBEPixelsBL, beWidth, beHeight, detailStartX + i, 297);
            var phase = (int)(Next(ref rng) % detailCount);
            var pixels = new Color32[checked(widthPx * heightPx)];
            for (var x = collarPx; x < widthPx - collarPx; x++)
            {
                var centre = soilCentreYByXBL[x];
                var slope = (soilCentreYByXBL[Math.Min(widthPx - 1, x + 1)] -
                    soilCentreYByXBL[Math.Max(0, x - 1)]) * .5f;
                var halfVerticalWidth = soilWidthPx * .5f * Mathf.Sqrt(1 + slope * slope);
                var low = centre - halfVerticalWidth; var high = centre + halfVerticalWidth;
                var l = Smooth((x - collarPx + .5f) / ArtFanPx);
                var r = Smooth((widthPx - collarPx - x - .5f) / ArtFanPx);
                if (l < 1)
                {
                    low = Mathf.Lerp(left.Low + centre - 297, low, l);
                    high = Mathf.Lerp(left.High + centre - 297, high, l);
                }
                if (r < 1)
                {
                    low = Mathf.Lerp(right.Low + centre - 169, low, r);
                    high = Mathf.Lerp(right.High + centre - 169, high, r);
                }
                var k = PositiveMod(x + phase, detailCount);
                var run = nativeRuns[k];
                var detailStrength = Math.Min(l, r);
                var jitterLow = Mathf.Clamp(run.Low - MeanEdge(nativeRuns, k, false), -6, 6) * detailStrength;
                var jitterHigh = Mathf.Clamp(run.High - MeanEdge(nativeRuns, k, true), -6, 6) * detailStrength;
                low += jitterLow; high += jitterHigh;
                if (high <= low) throw new InvalidOperationException("Soil fan lost its visible width.");
                for (var y = 0; y < heightPx; y++)
                {
                    var i = y * widthPx + x;
                    if (floorMaskBL[i] == 0) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                    var yy = y + .5f;
                    var isSoil = yy >= low && yy < high;
                    var colour = isSoil ? soil[i] : grass[i];
                    if (isSoil) result.SoilPixels++;
                    // Copy the received native boundary detail, not an 8/16px binary stair.
                    // Each selected output colour is one received source texel, no colour interpolation.
                    var toLow = yy - low; var toHigh = yy - high;
                    var nearLow = Math.Abs(toLow) <= FringePx;
                    var nearHigh = Math.Abs(toHigh) <= FringePx;
                    if (detailStrength > .25f && (nearLow || nearHigh))
                    {
                        var useLow = nearLow && (!nearHigh || Math.Abs(toLow) < Math.Abs(toHigh));
                        var sourceEdge = useLow ? run.Low : run.High;
                        var sourceY = Mathf.FloorToInt(sourceEdge + (useLow ? toLow : toHigh));
                        if (sourceY >= 0 && sourceY < beHeight)
                        {
                            colour = acceptedBEPixelsBL[sourceY * beWidth + detailStartX + k];
                            result.NativeEdgeDetailPixels++;
                        }
                    }
                    colour.a = 255; pixels[i] = colour;
                }
            }
            // Ordinary-camera inspection exposed the straight junction between the
            // procedural field and the immutable 32px collar. Continue received native
            // strips into the free interior, then cut along a pixel-wise low-error seam.
            // Only new J pixels outside the immutable collar are changed by this pass.
            // Texture matching must never expand the generated soil profile into
            // grass. Snapshot art classes outside the immutable endpoint collars.
            // This is a visual invariant only; the supplied floor mask remains
            // the sole floor, body and navigation authority.
            var terrainBeforeTransition = new byte[pixels.Length];
            for (var y = 0; y < heightPx; y++)
                for (var x = collarPx; x < widthPx - collarPx; x++)
                {
                    var i = y * widthPx + x;
                    if (floorMaskBL[i] != 0)
                        terrainBeforeTransition[i] = SoilColour(pixels[i]) ? (byte)2 : (byte)1;
                }
            ApplyEndpointTransition(pixels, floorMaskBL, widthPx, heightPx, seed,
                acceptedBEPixelsBL, beWidth, true, result);
            ApplyEndpointTransition(pixels, floorMaskBL, widthPx, heightPx, seed,
                acceptedDWPixelsBL, dwWidth, false, result);
            for (var y = 0; y < heightPx; y++)
                for (var x = collarPx; x < widthPx - collarPx; x++)
                {
                    var i = y * widthPx + x;
                    if (floorMaskBL[i] != 0 && terrainBeforeTransition[i] !=
                        (SoilColour(pixels[i]) ? (byte)2 : (byte)1))
                        result.EndpointTransitionTerrainChangedPixels++;
                }
            if (result.EndpointTransitionTerrainChangedPixels != 0)
                throw new InvalidOperationException("Native transition changed the generated soil/grass art profile.");
            // Restore and verify every fixed endpoint texel last. These are source-local BL rows.
            for (var y = 0; y < heightPx; y++)
                for (var depth = 0; depth < collarPx; depth++)
                {
                    pixels[y * widthPx + depth] = acceptedBEPixelsBL[y * beWidth + beWidth - 1 - depth];
                    pixels[y * widthPx + widthPx - 1 - depth] = acceptedDWPixelsBL[y * dwWidth + depth];
                }
            for (var y = 0; y < heightPx; y++) for (var x = 0; x < widthPx; x++)
            {
                var i = y * widthPx + x;
                if (floorMaskBL[i] == 0)
                {
                    result.TransparentPixels++;
                    if (pixels[i].a != 0) result.OutsideFloorOpaquePixels++;
                }
                else result.FloorPixels++;
                if (x < collarPx || x >= widthPx - collarPx)
                {
                    var expected = x < collarPx ? acceptedBEPixelsBL[y * beWidth + beWidth - 1 - x] :
                        acceptedDWPixelsBL[y * dwWidth + widthPx - 1 - x];
                    result.CollarComparedPixels++;
                    if (!Equal(pixels[i], expected)) result.CollarMismatchPixels++;
                }
            }
            if (result.CollarMismatchPixels != 0 || result.OutsideFloorOpaquePixels != 0)
                throw new InvalidOperationException("Candidate failed native collar or floor-alpha preservation.");
            if (Hash(acceptedBEPixelsBL) != result.SourceBEPixelSHA256BL ||
                Hash(acceptedDWPixelsBL) != result.SourceDWPixelSHA256BL)
                throw new InvalidOperationException("Source arrays were changed by candidate generation.");
            result.PixelsBL = pixels; result.PixelSHA256BL = Hash(pixels);
            return result;
        }

        private static void ApplyEndpointTransition(Color32[] pixels, byte[] floorMask,
            int width, int height, int seed, Color32[] source, int sourceWidth,
            bool left, StudyANativeFlexibleGroundResult report)
        {
            // d=0 is the shared boundary; d0..31 is immutable. d32..47 is a
            // terrain-matched native continuation; the irregular seam lies in
            // d48..95. Every copied texel, including d32..47, must retain the
            // procedural soil/grass class to avoid copying source plaza spurs.
            const int extensionStart = 32, seamLow = 48, seamHigh = 96;
            const int columns = seamHigh - seamLow;
            var costs = new long[height * columns]; var previous = new int[costs.Length];
            for (var y = 0; y < height; y++)
            {
                var desiredDepth = 72f + NativeSeamPrior(y, seed, left);
                for (var k = 0; k < columns; k++)
                {
                    var depth = seamLow + k;
                    var x = left ? depth : width - 1 - depth;
                    var sourceX = left ? sourceWidth - 1 - depth : depth;
                    var i = y * width + x;
                    long local = Mathf.FloorToInt(Math.Abs(depth - desiredDepth) * 5f);
                    if (floorMask[i] != 0)
                    {
                        var candidate = source[y * sourceWidth + sourceX];
                        local += RGBError(pixels[i], candidate) * 8L;
                        // This is art matching only, never floor/body/Nav admission.
                        if (SoilColour(pixels[i]) != SoilColour(candidate)) local += 512;
                    }
                    var bestPrevious = k;
                    if (y > 0)
                    {
                        for (var kk = Math.Max(0, k - 1); kk <= Math.Min(columns - 1, k + 1); kk++)
                            if (costs[(y - 1) * columns + kk] < costs[(y - 1) * columns + bestPrevious]) bestPrevious = kk;
                        local += costs[(y - 1) * columns + bestPrevious];
                    }
                    costs[y * columns + k] = local;
                    previous[y * columns + k] = bestPrevious;
                }
            }
            var end = 0;
            for (var k = 1; k < columns; k++)
                if (costs[(height - 1) * columns + k] < costs[(height - 1) * columns + end]) end = k;
            report.EndpointTransitionSelectedCost += costs[(height - 1) * columns + end];
            var seam = new int[height];
            var min = seamHigh; var max = seamLow;
            for (var y = height - 1; y >= 0; y--)
            {
                seam[y] = seamLow + end; min = Math.Min(min, seam[y]); max = Math.Max(max, seam[y]);
                end = previous[y * columns + end];
            }
            if (left) { report.LeftTransitionMinDepthPx = min; report.LeftTransitionMaxDepthPx = max; }
            else { report.RightTransitionMinDepthPx = min; report.RightTransitionMaxDepthPx = max; }
            for (var y = 0; y < height; y++)
                for (var depth = extensionStart; depth < seam[y]; depth++)
                {
                    var x = left ? depth : width - 1 - depth; var i = y * width + x;
                    if (floorMask[i] == 0) continue;
                    var sourceX = left ? sourceWidth - 1 - depth : depth;
                    var candidate = source[y * sourceWidth + sourceX];
                    if (SoilColour(candidate) != SoilColour(pixels[i]))
                    {
                        report.EndpointTransitionRejectedTerrainPixels++;
                        continue;
                    }
                    pixels[i] = candidate;
                    report.EndpointTransitionSourcePixels++;
                }
        }

        private static float NativeSeamPrior(int row, int seed, bool left)
        {
            // A gentle deterministic prior prevents a new all-row rectangular band.
            // Native RGB error and terrain agreement still select the actual 1px cut.
            var block = row / 64; var t = (row % 64 + .5f) / 64f;
            return Mathf.Lerp(SeamNoise(block, seed, left), SeamNoise(block + 1, seed, left), Smooth(t));
        }

        private static float SeamNoise(int block, int seed, bool left)
        {
            unchecked
            {
                var value = (uint)block * 374761393u ^ (uint)seed ^ (left ? 0x972d12abu : 0x517ac1edu);
                value ^= value >> 13; value *= 1274126177u; value ^= value >> 16;
                return (int)(value % 25u) - 12;
            }
        }

        private static void RequireSource(Color32[] pixels, int w, int h, int expectedW, int expectedH, string id)
        {
            if (w != expectedW || h != expectedH || pixels == null || pixels.Length != checked(w * h))
                throw new ArgumentException("Received source dimensions changed: " + id);
            foreach (var p in pixels) if (p.a != 255)
                throw new ArgumentException("Received native source must remain RGBA8 opaque: " + id);
        }

        // This predicate describes art only. It never controls floor, bodies or navigation.
        private static bool SoilColour(Color32 p) => p.r > p.g + 16 && p.r > p.b + 32;

        private static SoilRun ReadMainSoilRun(Color32[] pixels, int width, int height, int x, int anchor)
        {
            // Select the contiguous main road run containing its retained approach anchor.
            // Do not span distant dirt flecks or classify yellow leaf highlights as soil.
            if (anchor < 0 || anchor >= height || !SoilColour(pixels[anchor * width + x]))
                throw new ArgumentException("Received ground has no usable native soil profile at column " + x);
            var low = anchor; var high = anchor + 1;
            while (low > 0 && SoilColour(pixels[(low - 1) * width + x])) low--;
            while (high < height && SoilColour(pixels[high * width + x])) high++;
            if (high - low < 32)
                throw new ArgumentException("Received soil approach run is too short at column " + x);
            return new SoilRun(low, high);
        }

        private static float MeanEdge(SoilRun[] runs, int k, bool high)
        {
            var sum = 0f;
            for (var n = -8; n <= 8; n++)
            {
                var run = runs[PositiveMod(k + n, runs.Length)]; sum += high ? run.High : run.Low;
            }
            return sum / 17;
        }

        private static Color32[] Quilt(int width, int height, Region[] regions, ref uint rng,
            StudyANativeFlexibleGroundResult report, bool soil)
        {
            var output = new Color32[checked(width * height)];
            var step = PatchPx - OverlapPx;
            for (var y0 = 0; y0 < height; y0 += step)
                for (var x0 = 0; x0 < width; x0 += step)
                {
                    var w = Math.Min(PatchPx, width - x0); var h = Math.Min(PatchPx, height - y0);
                    var hasLeft = x0 > 0; var hasBottom = y0 > 0;
                    Patch best = default(Patch); var bestCost = long.MaxValue;
                    for (var candidate = 0; candidate < Candidates; candidate++)
                    {
                        var region = regions[(int)(Next(ref rng) % (uint)regions.Length)];
                        if (region.Rect.width < PatchPx || region.Rect.height < PatchPx)
                            throw new ArgumentException("Native quilt regions must fit a whole 64px patch.");
                        var patch = new Patch { Region = region,
                            X = region.Rect.x + (int)(Next(ref rng) % (uint)(region.Rect.width - PatchPx + 1)),
                            Y = region.Rect.y + (int)(Next(ref rng) % (uint)(region.Rect.height - PatchPx + 1)) };
                        var cost = OverlapCost(output, width, x0, y0, w, h, patch, hasLeft, hasBottom);
                        report.PatchCandidateCount++;
                        if (cost < bestCost) { best = patch; bestCost = cost; }
                    }
                    report.SelectedPatchOverlapRGBError += bestCost;
                    if (soil) report.SoilPatchCount++; else report.GrassPatchCount++;
                    var left = hasLeft ? CutVertical(output, width, x0, y0, w, h, best) : null;
                    var bottom = hasBottom ? CutHorizontal(output, width, x0, y0, w, h, best) : null;
                    for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                    {
                        if ((left != null && x < left[y]) || (bottom != null && y < bottom[x])) continue;
                        output[(y0 + y) * width + x0 + x] = best.Pixel(x, y);
                    }
                }
            return output;
        }

        private static long OverlapCost(Color32[] output, int width, int x0, int y0, int w, int h,
            Patch patch, bool left, bool bottom)
        {
            long cost = 0;
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                if ((left && x < OverlapPx) || (bottom && y < OverlapPx))
                    cost += RGBError(output[(y0 + y) * width + x0 + x], patch.Pixel(x, y));
            return cost;
        }

        private static int[] CutVertical(Color32[] output, int width, int x0, int y0, int w, int h, Patch patch)
        {
            var columns = Math.Min(OverlapPx, w);
            var costs = new long[columns * h]; var previous = new int[columns * h];
            for (var y = 0; y < h; y++) for (var x = 0; x < columns; x++)
            {
                long cost = RGBError(output[(y0 + y) * width + x0 + x], patch.Pixel(x, y));
                var prev = x;
                if (y > 0)
                {
                    for (var xx = Math.Max(0, x - 1); xx <= Math.Min(columns - 1, x + 1); xx++)
                        if (costs[(y - 1) * columns + xx] < costs[(y - 1) * columns + prev]) prev = xx;
                    cost += costs[(y - 1) * columns + prev];
                }
                costs[y * columns + x] = cost; previous[y * columns + x] = prev;
            }
            var end = 0;
            for (var x = 1; x < columns; x++) if (costs[(h - 1) * columns + x] < costs[(h - 1) * columns + end]) end = x;
            var cut = new int[h];
            for (var y = h - 1; y >= 0; y--) { cut[y] = end; end = previous[y * columns + end]; }
            return cut;
        }

        private static int[] CutHorizontal(Color32[] output, int width, int x0, int y0, int w, int h, Patch patch)
        {
            var rows = Math.Min(OverlapPx, h);
            var costs = new long[rows * w]; var previous = new int[rows * w];
            for (var x = 0; x < w; x++) for (var y = 0; y < rows; y++)
            {
                long cost = RGBError(output[(y0 + y) * width + x0 + x], patch.Pixel(x, y));
                var prev = y;
                if (x > 0)
                {
                    for (var yy = Math.Max(0, y - 1); yy <= Math.Min(rows - 1, y + 1); yy++)
                        if (costs[yy * w + x - 1] < costs[prev * w + x - 1]) prev = yy;
                    cost += costs[prev * w + x - 1];
                }
                costs[y * w + x] = cost; previous[y * w + x] = prev;
            }
            var end = 0;
            for (var y = 1; y < rows; y++) if (costs[y * w + w - 1] < costs[end * w + w - 1]) end = y;
            var cut = new int[w];
            for (var x = w - 1; x >= 0; x--) { cut[x] = end; end = previous[end * w + x]; }
            return cut;
        }

        private static int RGBError(Color32 a, Color32 b) =>
            Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
        private static bool Equal(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        private static int PositiveMod(int n, int modulus) { var r = n % modulus; return r < 0 ? r + modulus : r; }
        private static uint Next(ref uint state)
        {
            unchecked { state = state * 1664525u + 1013904223u; return state; }
        }
        private static string Hash(Color32[] pixels)
        {
            var bytes = new byte[checked(pixels.Length * 4)];
            for (var i = 0; i < pixels.Length; i++)
            { var p = pixels[i]; var o = i * 4; bytes[o] = p.r; bytes[o + 1] = p.g; bytes[o + 2] = p.b; bytes[o + 3] = p.a; }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLower(CultureInfo.InvariantCulture);
        }
    }
}
