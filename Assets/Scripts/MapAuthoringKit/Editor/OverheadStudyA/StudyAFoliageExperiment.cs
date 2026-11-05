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
    public sealed class StudyAFoliageExperimentRecord
    {
        public string StableId, Role, OriginalSpritePath, SelectedSpritePath, SelectedSourcePath, SelectedSourceSha256;
        public int StableIndexWithinSize, Variant;
        public Vector2 FootAnchor;
        public Color Tint;
    }

    [Serializable]
    public sealed class StudyAFoliageExperimentReport
    {
        public bool Enabled, RendererPosesUnchanged, ColliderGraphUnchanged;
        public int Version, TreeCount, LargeTreeCount, MediumTreeCount, ChangedSpriteCount;
        public string InputRoot, Fingerprint, SelectionHash, ColliderGraphBefore, ColliderGraphAfter, Recipe;
        public StudyAFoliageExperimentRecord[] Selections;
        public string[] RuntimeSpritePaths;
    }

    /// <summary>Sprite-only P/V comparison, after the baseline forest finish and depth-tie resolution.</summary>
    public static class StudyAFoliageExperiment
    {
        public const string InputRoot = "Assets/Art/Map/OverheadStudyA/Input/FoliageControlledPV";
        private const int Ppu = 64;
        private static readonly string[] Files = {
            "sources/tree_large.png", "sources/tree_medium.png",
            "P/tree_large.png", "P/tree_medium.png",
            "V/tree_large_variant_01.png", "V/tree_large_variant_02.png",
            "V/tree_medium_variant_01.png", "V/tree_medium_variant_02.png", "manifest.json"
        };
        private static readonly string[] Hashes = {
            "24a9bc74865dead44cef7ebb02b351553df762e74cdfc2d4640c5b5e1dfd8250",
            "eb8eef3d1baaa39efdacf2012fe0cee90120a831c148ded6de05ab9bda496c62",
            "48b9e3d21bf475218abc50187875e12128341c182b32ed49b708d91f31a5671e",
            "e38645740221307ba1ac10df84012bcff3773015e8c8da1760a2233e0bc5645e",
            "be1d2e72f2aac52ca1d53f79b85ad2a34de8f8ea1bd62ec1a65b938691f74c1b",
            "34aa644ba6b9f856dde71d1eefd2dcce90cbf290d1e577a593b344047e514f19",
            "54998c4083a7b2aed86c6f4c712cc31915f1d0e554868d2ad1b00c7973a2a1ab",
            "413321f0be1e645e508d5422aca6db2d0568448bdaea38f45748f49fb970aca7",
            "8c29f0259fcfac333f1af00e8f7384cb51d984de07f18ff933db6941decfef17"
        };
        [Serializable] private sealed class Manifest { public Asset[] assets = Array.Empty<Asset>(); }
        [Serializable] private sealed class Asset
        {
            public string file = "", sha256 = "";
            public int pixels_per_unit = 0;
            public int[] canvas_px = Array.Empty<int>(), pivot_pixels_bottom_left = Array.Empty<int>();
        }
        private sealed class Tree
        {
            public SpriteRenderer Renderer;
            public string Role, Key;
            public Sprite Original;
            public Transform Parent;
            public Vector3 LocalPosition, LocalScale;
            public Quaternion LocalRotation;
            public Matrix4x4 WorldMatrix;
            public Color Tint;
            public Material Material;
            public int SortingLayer, SortingOrder;
            public SpriteSortPoint SortPoint;
            public bool FlipX, FlipY;
        }

        public static string Fingerprint() => Hash(Encoding.UTF8.GetBytes(string.Join("\n", Files.Select((f, i) => f + "=" + Hashes[i]))));

        public static void RequireApprovedInputs(string savedFingerprint = null)
        {
            if (!string.IsNullOrEmpty(savedFingerprint) && savedFingerprint != Fingerprint())
                throw new InvalidDataException("Saved foliage experiment fingerprint changed; refusing silent migration.");
            for (var i = 0; i < Files.Length; i++)
                if (Hash(File.ReadAllBytes(ProjectPath(InputRoot + "/" + Files[i]))) != Hashes[i])
                    throw new InvalidDataException("Foliage source bytes changed: " + Files[i]);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ProjectPath(InputRoot + "/manifest.json"), Encoding.UTF8));
            if (manifest == null || manifest.assets == null || manifest.assets.Length != 8)
                throw new InvalidDataException("Foliage manifest is incomplete.");
            var pixels = new Color32[8][];
            for (var i = 0; i < 8; i++)
            {
                var size = Size(Files[i]);
                var item = manifest.assets.Single(a => a.file == Files[i]);
                if (item.sha256 != Hashes[i] || item.pixels_per_unit != Ppu || !item.canvas_px.SequenceEqual(new[] { size, size }) ||
                    !item.pivot_pixels_bottom_left.SequenceEqual(new[] { size / 2, 8 }))
                    throw new InvalidDataException("Foliage native metadata changed: " + Files[i]);
                pixels[i] = ReadPixels(File.ReadAllBytes(ProjectPath(InputRoot + "/" + Files[i])), size);
            }
            for (var sizeIndex = 0; sizeIndex < 2; sizeIndex++)
            {
                var source = pixels[sizeIndex]; var paint = pixels[sizeIndex + 2];
                var size = sizeIndex == 0 ? 256 : 192; var keptRows = sizeIndex == 0 ? 41 : 30;
                for (var i = 0; i < source.Length; i++)
                    if (source[i].a != paint[i].a || (i / size < keptRows && !source[i].Equals(paint[i])))
                        throw new InvalidDataException("P must preserve the exact alpha and lower trunk/root slice.");
            }
        }

        public static StudyAFoliageExperimentReport Apply(Transform obstacleRoot, Dictionary<string, StudyAStarterProp> approvedProps,
            int version, string savedFingerprint, string outputRoot)
        {
            if (version < 1 || version > 2) throw new ArgumentOutOfRangeException(nameof(version), "Only reviewed P1/V2 sprite trials are implemented.");
            if (obstacleRoot == null || approvedProps == null || EditorUtility.IsPersistent(obstacleRoot) ||
                !obstacleRoot.gameObject.scene.IsValid() || !string.IsNullOrEmpty(obstacleRoot.gameObject.scene.path))
                throw new InvalidOperationException("Foliage experiments accept only a fresh generated Scene, never a saved Scene or source Prefab.");
            if (!outputRoot.StartsWith(StudyABlockSceneBuilder.ArtRoot + "/", StringComparison.Ordinal) || Directory.Exists(ProjectPath(outputRoot)))
                throw new IOException("Fresh dedicated foliage output under BlockStudies is required.");
            RequireApprovedInputs(savedFingerprint);
            var trees = obstacleRoot.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null)
                .Select(r => new { Renderer = r, Approved = approvedProps.Values.FirstOrDefault(p => p != null && p.Sprite == r.sprite) })
                .Where(t => t.Approved != null && (t.Approved.Metrics.Role == "treeLarge" || t.Approved.Metrics.Role == "treeMedium"))
                .Select(t => new Tree { Renderer = t.Renderer, Role = t.Approved.Metrics.Role, Key = Hierarchy(t.Renderer.transform),
                    Original = t.Renderer.sprite, Parent = t.Renderer.transform.parent, LocalPosition = t.Renderer.transform.localPosition,
                    LocalScale = t.Renderer.transform.localScale, LocalRotation = t.Renderer.transform.localRotation,
                    WorldMatrix = t.Renderer.transform.localToWorldMatrix, Tint = t.Renderer.color, Material = t.Renderer.sharedMaterial,
                    SortingLayer = t.Renderer.sortingLayerID, SortingOrder = t.Renderer.sortingOrder, SortPoint = t.Renderer.spriteSortPoint,
                    FlipX = t.Renderer.flipX, FlipY = t.Renderer.flipY })
                .OrderBy(t => t.Key, StringComparer.Ordinal).ToArray();
            if (trees.Length == 0 || trees.Select(t => t.Key).Distinct().Count() != trees.Length)
                throw new InvalidDataException("Foliage comparison requires unique approved tree hierarchy IDs.");
            foreach (var tree in trees)
            {
                var size = tree.Role == "treeLarge" ? 256 : 192;
                if (tree.Original.rect != new Rect(0, 0, size, size) || tree.Original.pixelsPerUnit != Ppu ||
                    tree.Original.pivot != new Vector2(size / 2, 8) || tree.Renderer.transform.lossyScale != Vector3.one ||
                    tree.Renderer.transform.rotation != Quaternion.identity)
                    throw new InvalidDataException("Baseline tree native dimensions/pivot/pose differ; refusing compensation.");
            }
            var before = ColliderGraph(obstacleRoot);
            var indices = version == 1 ? new[] { 2, 3 } : new[] { 4, 5, 6, 7 };
            var runtimeSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var paths = new List<string>();
            foreach (var index in indices)
            {
                var path = outputRoot + "/" + Files[index];
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                if (File.Exists(ProjectPath(path))) throw new IOException("Foliage output already exists: " + path);
                File.WriteAllBytes(ProjectPath(path), File.ReadAllBytes(ProjectPath(InputRoot + "/" + Files[index])));
                ConfigureImport(path, Size(Files[index])); RequireImport(path, Size(Files[index]));
                if (Hash(File.ReadAllBytes(ProjectPath(path))) != Hashes[index]) throw new IOException("Foliage runtime copy changed source pixels.");
                runtimeSprites.Add(Files[index], AssetDatabase.LoadAssetAtPath<Sprite>(path)); paths.Add(path);
            }
            var records = new List<StudyAFoliageExperimentRecord>();
            foreach (var cohort in trees.GroupBy(t => t.Role).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var stableIndex = 0;
                foreach (var tree in cohort.OrderBy(t => t.Key, StringComparer.Ordinal))
                {
                    var stem = tree.Role == "treeLarge" ? "tree_large" : "tree_medium";
                    var variant = version == 1 ? 1 : stableIndex % 3;
                    var file = version == 1 ? "P/" + stem + ".png" : variant == 0 ? "sources/" + stem + ".png" : "V/" + stem + "_variant_0" + variant + ".png";
                    if (version == 1 || variant != 0) tree.Renderer.sprite = runtimeSprites[file];
                    RequirePreserved(tree);
                    var index = Array.IndexOf(Files, file);
                    records.Add(new StudyAFoliageExperimentRecord { StableId = tree.Key, Role = tree.Role, StableIndexWithinSize = stableIndex++,
                        Variant = variant, OriginalSpritePath = AssetDatabase.GetAssetPath(tree.Original), SelectedSpritePath = AssetDatabase.GetAssetPath(tree.Renderer.sprite),
                        SelectedSourcePath = InputRoot + "/" + file, SelectedSourceSha256 = Hashes[index], FootAnchor = tree.Renderer.transform.position, Tint = tree.Tint });
                }
            }
            var after = ColliderGraph(obstacleRoot);
            if (before != after) throw new InvalidOperationException("Sprite-only foliage mutation changed the collider graph.");
            records = records.OrderBy(r => r.StableId, StringComparer.Ordinal).ToList();
            var selections = records.Select(r => r.StableId + "=" + r.Role + ":" + r.Variant + ":" + r.SelectedSourceSha256);
            return new StudyAFoliageExperimentReport { Enabled = true, Version = version, InputRoot = InputRoot, Fingerprint = Fingerprint(),
                TreeCount = trees.Length, LargeTreeCount = trees.Count(t => t.Role == "treeLarge"), MediumTreeCount = trees.Count(t => t.Role == "treeMedium"),
                ChangedSpriteCount = trees.Count(t => t.Renderer.sprite != t.Original), ColliderGraphBefore = before, ColliderGraphAfter = after,
                ColliderGraphUnchanged = true, RendererPosesUnchanged = true, SelectionHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", selections))),
                Selections = records.ToArray(), RuntimeSpritePaths = paths.ToArray(),
                Recipe = version == 1 ? "P1: exact original alpha/root, broader internal canopy clusters; one sprite per size. Sprite only, after unchanged forest finish and depth ties."
                    : "V2: ordinal stable hierarchy IDs, separately within each size, modulo3 = original/V01/V02. Crown and some trunk details vary; approximate silhouette test. Sprite only, after unchanged forest finish and depth ties." };
        }

        private static void RequirePreserved(Tree t)
        {
            var r = t.Renderer; var p = r.transform;
            if (p.parent != t.Parent || p.localPosition != t.LocalPosition || p.localScale != t.LocalScale || p.localRotation != t.LocalRotation || p.localToWorldMatrix != t.WorldMatrix ||
                r.color != t.Tint || r.sharedMaterial != t.Material || r.sortingLayerID != t.SortingLayer || r.sortingOrder != t.SortingOrder ||
                r.spriteSortPoint != t.SortPoint || r.flipX != t.FlipX || r.flipY != t.FlipY || r.sprite.bounds != t.Original.bounds)
                throw new InvalidOperationException("Foliage replacement changed baseline pose/material/tint/sorting/bounds: " + t.Key);
        }
        private static string ColliderGraph(Transform root) => Hash(Encoding.UTF8.GetBytes(string.Join("\n", root.GetComponentsInChildren<Collider2D>(true)
            .OrderBy(c => Hierarchy(c.transform), StringComparer.Ordinal).ThenBy(c => c.GetType().FullName, StringComparer.Ordinal)
            .Select(c => Hierarchy(c.transform) + ":" + c.GetType().FullName + ":" + EditorJsonUtility.ToJson(c)))));
        private static string Hierarchy(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name + "[" + t.GetSiblingIndex().ToString("D4", CultureInfo.InvariantCulture) + "]"); t = t.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
        private static int Size(string file) => file.Contains("tree_large") ? 256 : 192;
        private static Color32[] ReadPixels(byte[] bytes, int size)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false) || texture.width != size || texture.height != size)
                    throw new InvalidDataException("Foliage PNG native dimensions differ.");
                var pixels = texture.GetPixels32();
                if (pixels.Any(p => p.a != 0 && p.a != 255) || pixels.Any(p => p.a == 0 && (p.r != 0 || p.g != 0 || p.b != 0)))
                    throw new InvalidDataException("Foliage PNG must retain binary alpha and zero hidden RGB.");
                return pixels;
            }
            finally { Object.DestroyImmediate(texture); }
        }
        private static void ConfigureImport(string path, int size)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); var i = AssetImporter.GetAtPath(path) as TextureImporter;
            if (i == null) throw new InvalidDataException("Foliage TextureImporter missing.");
            i.textureType = TextureImporterType.Sprite; i.spriteImportMode = SpriteImportMode.Single; i.spritePixelsPerUnit = Ppu;
            i.filterMode = FilterMode.Point; i.mipmapEnabled = false; i.textureCompression = TextureImporterCompression.Uncompressed; i.crunchedCompression = false;
            i.alphaSource = TextureImporterAlphaSource.FromInput; i.alphaIsTransparency = true; i.sRGBTexture = true; i.wrapMode = TextureWrapMode.Clamp;
            i.npotScale = TextureImporterNPOTScale.None; i.maxTextureSize = 2048; i.anisoLevel = 0; i.isReadable = false;
            foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps", "Server" })
                if (i.GetPlatformTextureSettings(platform).overridden) i.ClearPlatformTextureSettings(platform);
            var s = new TextureImporterSettings(); i.ReadTextureSettings(s); s.spriteAlignment = (int)SpriteAlignment.Custom; s.spritePivot = new Vector2(.5f, 8f / size);
            s.spriteMeshType = SpriteMeshType.FullRect; s.spriteGenerateFallbackPhysicsShape = false; i.SetTextureSettings(s);
            var d = i.GetDefaultPlatformTextureSettings(); d.format = TextureImporterFormat.RGBA32; d.textureCompression = TextureImporterCompression.Uncompressed;
            d.maxTextureSize = 2048; i.SetPlatformTextureSettings(d); i.SaveAndReimport();
        }
        private static void RequireImport(string path, int size)
        {
            var i = AssetImporter.GetAtPath(path) as TextureImporter; var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (i == null || sprite == null || sprite.rect != new Rect(0, 0, size, size) || sprite.pixelsPerUnit != Ppu || sprite.pivot != new Vector2(size / 2, 8) ||
                sprite.GetPhysicsShapeCount() != 0 || sprite.packed || sprite.texture.format != TextureFormat.RGBA32 ||
                i.filterMode != FilterMode.Point || i.mipmapEnabled || i.textureCompression != TextureImporterCompression.Uncompressed || i.crunchedCompression ||
                i.spriteImportMode != SpriteImportMode.Single || i.alphaSource != TextureImporterAlphaSource.FromInput || !i.alphaIsTransparency || !i.sRGBTexture)
                throw new InvalidDataException("Foliage native import contract failed: " + path);
            var s = new TextureImporterSettings(); i.ReadTextureSettings(s);
            if (s.spriteMeshType != SpriteMeshType.FullRect || s.spriteGenerateFallbackPhysicsShape || s.spriteAlignment != (int)SpriteAlignment.Custom || s.spritePivot != new Vector2(.5f, 8f / size))
                throw new InvalidDataException("Foliage FullRect/pivot contract failed: " + path);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var i = path.LastIndexOf('/'); if (i <= 0) throw new IOException("Project asset folder required.");
            EnsureFolder(path.Substring(0, i)); AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
        private static string ProjectPath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains("\\") || relative.Split('/').Any(s => s == "." || s == ".."))
                throw new IOException("Safe project-relative path required.");
            var root = Directory.GetParent(Application.dataPath).FullName; var result = Path.GetFullPath(Path.Combine(root, relative));
            if (!result.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Foliage path escaped project.");
            for (var p = result; !p.Equals(root, StringComparison.OrdinalIgnoreCase); p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse foliage path refused.");
            return result;
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
