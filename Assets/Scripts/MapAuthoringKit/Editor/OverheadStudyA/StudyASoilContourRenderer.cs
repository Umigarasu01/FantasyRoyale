using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyASoilContourPolygon
    {
        public string BlockId;
        public Vector2Int Origin, Extent;
        public int PixelStep = 8;
        public Vector2[] Points;
    }

    public sealed class StudyASoilMask
    {
        public Vector2Int Origin;
        public int Width, Height, PixelsPerUnit;
        // Native Texture2D order: row 0 is the bottom row. Binary soil alpha, separate from grass.
        public byte[] Pixels;
        public bool ContainsRect(Rect rect)
        {
            var x0 = Mathf.FloorToInt((rect.xMin - Origin.x) * PixelsPerUnit + 0.00001f);
            var y0 = Mathf.FloorToInt((rect.yMin - Origin.y) * PixelsPerUnit + 0.00001f);
            var x1 = Mathf.CeilToInt((rect.xMax - Origin.x) * PixelsPerUnit - 0.00001f);
            var y1 = Mathf.CeilToInt((rect.yMax - Origin.y) * PixelsPerUnit - 0.00001f);
            if (x0 < 0 || y0 < 0 || x1 > Width || y1 > Height) return false;
            for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) if (Pixels[y * Width + x] == 0) return false;
            return true;
        }
    }

    /// <summary>Run-owned binary soil clipping. No source import changes, interpolation, recoloring or collision changes.</summary>
    public static class StudyASoilContourRenderer
    {
        public const int PixelsPerUnit = 64;

        public static StudyASoilContourPolygon DefaultPolygon(StudyABlockDefinition definition, Vector2Int origin)
        {
            Vector2[] points;
            if (definition.Kind == StudyABlockKind.Plaza)
                points = P(0,4, .75f,4, 1.25f,3.8f, 2.5f,3.25f, 3.75f,2.65f, 5.25f,2.125f, 6.5f,2.25f,
                    7.75f,2.9f, 9,3.25f, 10.5f,3.35f, 11.125f,3.5f, 11.25f,4, 12,4, 12,7, 11.25f,7, 11.125f,6.2f,
                    10.5f,6.15f, 9.5f,6.55f, 9.25f,7.1f, 10.3f,7.55f, 10.75f,8.15f, 10.35f,8.85f, 9.25f,9.1f,
                    7.5f,9.3f, 6.25f,9.7f, 4.75f,9.7f, 3.5f,9.125f, 2.5f,8.3f, 1.5f,7.35f, .75f,7, 0,7);
            else if (definition.Kind == StudyABlockKind.ForestPath)
                points = P(0,4, .75f,4, 1.125f,3.5f, 1.75f,3.35f, 3,3.625f, 4.25f,4.1f, 5.5f,4.55f,
                    6.75f,4.6f, 7.125f,4.5f, 7.25f,4, 8,4, 8,7, 7.25f,7, 7,7.1f, 5.75f,7.3f, 4.5f,6.9f,
                    3.25f,6.4f, 2,6.05f, 1.125f,6, .75f,7, 0,7);
            else throw new ArgumentException("Fine soil currently supports Plaza/ForestPath only; bridge-bank stays a structural diagnostic.");
            return new StudyASoilContourPolygon { BlockId = definition.Id, Origin = origin, Extent = definition.Extent, Points = points };
        }

        public static void ConfigureDefaultRoute(StudyABlockDefinition definition)
        {
            var ys = definition.Kind == StudyABlockKind.Plaza
                ? new[] { 5.5f,5.5f,5.5f,5.5f,5.5f,5.5f,5.5f,5.35f,5.1f,4.85f,4.65f,4.75f }
                : new[] { 4.75f,4.65f,4.85f,5.25f,5.65f,5.95f,6f,5.9f };
            if (definition.Kind != StudyABlockKind.Plaza && definition.Kind != StudyABlockKind.ForestPath) return;
            definition.MainRoute = ys.Select((y, x) => new Vector2(x + 0.5f, y)).ToList();
            definition.Spawn = definition.MainRoute[definition.MainRoute.Count / 2];
            // Conservative macro road protection remains distinct from the finer visible soil boundary.
            var road = new HashSet<Vector2Int>(definition.RoadCells);
            foreach (var foot in Samples(definition.MainRoute, 256))
            {
                var rect = ActorRect(foot);
                for (var y = Mathf.FloorToInt(rect.yMin); y < Mathf.CeilToInt(rect.yMax); y++)
                    for (var x = Mathf.FloorToInt(rect.xMin); x < Mathf.CeilToInt(rect.xMax); x++) road.Add(new Vector2Int(x, y));
            }
            definition.RoadCells = road.OrderBy(c => c.y).ThenBy(c => c.x).ToList();
        }

        private static Vector2[] P(params float[] xy)
        {
            var result = new Vector2[xy.Length / 2];
            for (var i = 0; i < result.Length; i++) result[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return result;
        }

        public static List<string> ValidatePolygon(StudyASoilContourPolygon polygon)
        {
            var errors = new List<string>(); var p = polygon?.Points;
            if (polygon == null || p == null || p.Length < 3) { errors.Add("Closed soil outline needs at least three ordered points."); return errors; }
            if (polygon.PixelStep != 8 && polygon.PixelStep != 16) errors.Add("Soil PixelStep must be 8 or 16.");
            if (polygon.Extent.x <= 0 || polygon.Extent.y <= 0) errors.Add("Soil extent must be positive.");
            double area = 0;
            for (var i = 0; i < p.Length; i++)
            {
                var a = p[i]; var b = p[(i + 1) % p.Length];
                if (!Finite(a.x) || !Finite(a.y) || a.x < 0 || a.y < 0 || a.x > polygon.Extent.x || a.y > polygon.Extent.y)
                    errors.Add("Soil point is nonfinite/outside native block: " + i);
                if (a == b) errors.Add("Consecutive soil points coincide: " + i);
                area += (double)a.x * b.y - (double)b.x * a.y;
                for (var j = i + 1; j < p.Length; j++)
                {
                    if (j == i + 1 || (i == 0 && j == p.Length - 1)) continue;
                    if (Intersects(a, b, p[j], p[(j + 1) % p.Length])) errors.Add("Soil outline self-intersects: " + i + "/" + j);
                }
            }
            if (Math.Abs(area) < 0.000001) errors.Add("Soil outline has no area.");
            return errors;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static double Cross(Vector2 a, Vector2 b, Vector2 p) => ((double)b.x - a.x) * (p.y - a.y) - ((double)b.y - a.y) * (p.x - a.x);
        private static bool OnEdge(Vector2 a, Vector2 b, Vector2 p) => Math.Abs(Cross(a, b, p)) <= 0.000001 && p.x >= Math.Min(a.x,b.x)-0.000001 && p.x <= Math.Max(a.x,b.x)+0.000001 && p.y >= Math.Min(a.y,b.y)-0.000001 && p.y <= Math.Max(a.y,b.y)+0.000001;
        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var abC = Cross(a,b,c); var abD = Cross(a,b,d); var cdA = Cross(c,d,a); var cdB = Cross(c,d,b);
            return (abC * abD < 0 && cdA * cdB < 0) || OnEdge(a,b,c) || OnEdge(a,b,d) || OnEdge(c,d,a) || OnEdge(c,d,b);
        }

        // Pure managed sampling: edge inclusive; each step block samples its center. Rows are bottom-origin.
        public static bool ContainsPoint(Vector2[] points, Vector2 point)
        {
            var inside = false;
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length];
                if (OnEdge(a, b, point)) return true;
                if ((a.y > point.y) != (b.y > point.y) && point.x < (double)(b.x-a.x) * (point.y-a.y)/(b.y-a.y)+a.x) inside = !inside;
            }
            return inside;
        }

        public static byte[] Rasterize(StudyASoilContourPolygon[] polygons, Vector2Int origin, int width, int height, int pixelsPerUnit = 64)
        {
            if (pixelsPerUnit != 64 || width <= 0 || height <= 0) throw new ArgumentException("Native raster uses positive dimensions at PPU64.");
            if (polygons == null || polygons.Length == 0) throw new ArgumentException("No closed soil polygons.");
            var w = checked(width * pixelsPerUnit); var h = checked(height * pixelsPerUnit); var result = new byte[checked(w*h)];
            foreach (var polygon in polygons)
            {
                var errors = ValidatePolygon(polygon); if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors));
                var step = polygon.PixelStep;
                for (var y = 0; y < h; y += step) for (var x = 0; x < w; x += step)
                {
                    var sample = (Vector2)origin + new Vector2((x+step*.5f)/pixelsPerUnit, (y+step*.5f)/pixelsPerUnit) - (Vector2)polygon.Origin;
                    if (!ContainsPoint(polygon.Points, sample)) continue;
                    for (var yy = y; yy < Math.Min(y+step,h); yy++) for (var xx = x; xx < Math.Min(x+step,w); xx++) result[yy*w+xx] = 255;
                }
            }
            return result;
        }

        public static StudyASoilMask MakeMask(StudyASoilContourPolygon[] polygons, Vector2Int origin, int width, int height) => new StudyASoilMask
        { Origin = origin, Width = width*64, Height = height*64, PixelsPerUnit = 64, Pixels = Rasterize(polygons, origin, width, height) };

        public static Rect ActorRect(Vector2 foot) => new Rect(foot + new Vector2(-.24f,-.055f), new Vector2(.48f,.55f));
        public static IEnumerable<Vector2> Samples(IList<Vector2> route, int perUnit)
        {
            for (var i = 0; i < route.Count; i++)
            {
                if (i == 0) { yield return route[i]; continue; }
                var count = Math.Max(1, Mathf.CeilToInt(Vector2.Distance(route[i-1],route[i])*perUnit));
                for (var n = 1; n <= count; n++) yield return Vector2.Lerp(route[i-1],route[i],(float)n/count);
            }
        }

        public static void RequireRouteAndPorts(StudyASoilMask mask, StudyABlockAssembly assembly, StudyASoilContourPolygon[] polygons)
        {
            foreach (var route in assembly.MainRoutes)
                foreach (var foot in Samples(route, 256)) RequireFoot(mask, assembly, foot);
            // Joining route endpoints need their own sweep, including the shared seam.
            foreach (var connection in assembly.Connections.Where(c => c.IsWalkable))
            {
                var first = assembly.Blocks[connection.FromBlock]; var second = assembly.Blocks[connection.ToBlock];
                var a = first.Definition.MainRoute.Last() + (Vector2)first.Origin;
                var b = second.Definition.MainRoute.First() + (Vector2)second.Origin;
                foreach (var foot in Samples(new[] { a,connection.Center,b },256)) RequireFoot(mask,assembly,foot);
                var port = first.Definition.Ports[connection.FromPort];
                var otherPort = second.Definition.Ports[connection.ToPort];
                for (var lane = 0; lane < port.Width; lane++)
                {
                    var left = (Vector2)first.Origin + (Vector2)port.Cells.ElementAt(lane) + Vector2.one * .5f;
                    var right = (Vector2)second.Origin + (Vector2)otherPort.Cells.ElementAt(lane) + Vector2.one * .5f;
                    foreach (var foot in Samples(new[] { left,right },256)) RequireFoot(mask,assembly,foot);
                }
            }
            RequireFoot(mask, assembly, assembly.Spawn);
            foreach (var block in assembly.Blocks) foreach (var port in block.Definition.Ports.Where(p => p.Terrain == StudyATerrain.Soil))
            {
                Rect strip;
                if (port.Direction == StudyAPortDirection.West) strip = new Rect(block.Origin.x, block.Origin.y+port.Position.y, .75f,port.Width);
                else if (port.Direction == StudyAPortDirection.East) strip = new Rect(block.Origin.x+block.Definition.Extent.x-.75f,block.Origin.y+port.Position.y,.75f,port.Width);
                else if (port.Direction == StudyAPortDirection.South) strip = new Rect(block.Origin.x+port.Position.x, block.Origin.y, port.Width, .75f);
                else if (port.Direction == StudyAPortDirection.North) strip = new Rect(block.Origin.x+port.Position.x, block.Origin.y+block.Definition.Extent.y-.75f, port.Width, .75f);
                else throw new ArgumentOutOfRangeException(nameof(port.Direction));
                if (!mask.ContainsRect(strip)) throw new ArgumentException("Fine soil does not cover the complete immediate port strip: " + block.Definition.Id + "/" + port.Id);
            }
        }

        private static void RequireFoot(StudyASoilMask mask, StudyABlockAssembly assembly, Vector2 foot)
        {
            var body = ActorRect(foot);
            if (!mask.ContainsRect(body)) throw new ArgumentException("Actual .48x.55/offset .22 actor sweep leaves fine soil: " + foot);
            for (var y = Mathf.FloorToInt(body.yMin); y < Mathf.CeilToInt(body.yMax); y++)
                for (var x = Mathf.FloorToInt(body.xMin); x < Mathf.CeilToInt(body.xMax); x++)
                    if (!assembly.WalkCells.Contains(new Vector2Int(x,y))) throw new ArgumentException("Actor sweep leaves structural Walk mask: " + foot);
        }

        public static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return string.Concat(hash.ComputeHash(bytes).Select(b=>b.ToString("x2",CultureInfo.InvariantCulture))); }

        // Input texels are copied one-for-one. Only alpha is set by the separate binary soil mask.
        public static Color32[] Compose(StudyASoilMask mask, Color32[][] soilVariants, int seed, out string selectionHash)
        {
            if (soilVariants == null || soilVariants.Length == 0 || soilVariants.Any(p=>p==null || p.Length!=64*64)) throw new ArgumentException("Native soil variants must be 64x64.");
            var result = new Color32[mask.Pixels.Length]; var selections = new List<byte>();
            for (var cy = 0; cy < mask.Height/64; cy++) for (var cx = 0; cx < mask.Width/64; cx++)
            {
                var variant = StudyATerrainTileSelector.SelectVariant(seed,new Vector2Int(cx+mask.Origin.x,cy+mask.Origin.y),soilVariants.Length); selections.Add((byte)variant);
                var source = soilVariants[variant];
                for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++)
                {
                    var i = (cy*64+y)*mask.Width+cx*64+x; var color = source[y*64+x];
                    if (color.a != 255) throw new ArgumentException("Soil source is not opaque.");
                    color.a = mask.Pixels[i]; result[i] = color;
                }
            }
            selectionHash = Hash(selections.ToArray()); return result;
        }

        public static Sprite WriteSprite(StudyASoilMask mask, StudyATerrainSpriteSet terrain, int seed, string path, out string selectionHash)
        {
            if (!path.StartsWith(StudyABlockSceneBuilder.ArtRoot+"/",StringComparison.Ordinal) || path.Split('/').Any(p=>p==".." || p==".") || path.Contains("\\") || File.Exists(path)) throw new IOException("Fine soil output must be new and run-owned: " + path);
            var pixels = new List<Color32[]>();
            foreach (var sprite in terrain.SoilVariants)
            {
                var sourcePath = AssetDatabase.GetAssetPath(sprite);
                var source = new Texture2D(2,2,TextureFormat.RGBA32,false);
                try { if (!source.LoadImage(File.ReadAllBytes(sourcePath),false) || source.width!=64 || source.height!=64) throw new InvalidDataException("Fine soil source must be native64 PNG."); pixels.Add(source.GetPixels32()); }
                finally { Object.DestroyImmediate(source); }
            }
            var output = new Texture2D(mask.Width,mask.Height,TextureFormat.RGBA32,false);
            try { output.SetPixels32(Compose(mask,pixels.ToArray(),seed,out selectionHash)); output.Apply(false,false); File.WriteAllBytes(path,output.EncodeToPNG()); }
            finally { Object.DestroyImmediate(output); }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single; importer.spritePixelsPerUnit=64;
            importer.spritePivot=Vector2.zero;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=false; importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp; importer.maxTextureSize=2048; importer.isReadable=false;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings); textureSettings.spriteAlignment=(int)SpriteAlignment.Custom; textureSettings.spriteMeshType=SpriteMeshType.FullRect; textureSettings.spritePivot=Vector2.zero; textureSettings.spriteGenerateFallbackPhysicsShape=false; importer.SetTextureSettings(textureSettings);
            var platform=importer.GetDefaultPlatformTextureSettings(); platform.format=TextureImporterFormat.RGBA32; platform.textureCompression=TextureImporterCompression.Uncompressed; platform.maxTextureSize=2048; importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidDataException("Fine soil sprite import failed.");
        }
    }
}
