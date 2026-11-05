using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyABridge01StructureReport
    {
        public int Version = 1, PixelsPerUnit = 64, BodyRecipeRevision = 2, NorthCornerInsetPixels = 1;
        public bool EnginePixelsReviewed, ActualActorMovementVerified;
        public string InputRoot, Fingerprint, Recipe, BodyCollisionMaskSha256;
        public Rect NominalFloorBounds;
        public Rect[] SourceProposalCornerBodies, CornerBodies, RailBodies, BodyRasterRectangles;
        public Vector2 Anchor, CommonPixelPivot;
        public Vector2[] SourcePostInspectionPoints, LayerSortAnchors;
        public string[] SourceFiles, SourceFileSha256, RuntimeLayerPaths, LayerSorting;
        public int NominalFloorTransparentPixelCount, CompositeTransparentRgbMismatchCount;
        public string CompositeRecomposition;
    }

    /// <summary>Small explicit Bridge01 trial. Source layers retain native size/pivot; ground bodies are independent of image alpha.</summary>
    public static class StudyABridge01Structure
    {
        public const string InputRoot = "Assets/Art/Map/OverheadStudyA/Input/Bridge01RuntimeCandidate";
        public const string BodyRootName = "Bridge01StructuralCollision";
        private const int Ppu = 64, Width = 320, Height = 192;
        private static readonly Vector2 Pivot = new Vector2(160, 96);
        private static readonly string[] Files = {
            "bridge01-floor-support.png", "bridge01-back-assembly.png", "bridge01-front-assembly.png",
            "bridge01-composite.png", "bridge01-manifest.json", "README.txt", "validation-results.json"
        };
        private static readonly string[] Hashes = {
            "e330a3cee1cab6271badb31d41d471f4bbec9b95e82fb06d560cadd235602a36",
            "4d53ec57f23686ed7e1c598438c7f6fc1ab4371314803bbd97952477d8333054",
            "ca7003f8cfd8ae153475c68b1278427cef01f4263b9cc400bcbd9b333d1828bb",
            "65cd3a5196b7b86460a541e870c9d5b862019a7a7c91e26eddcc3ab2fae4b38d",
            "7d2adf498b987fbf6d943e4f5d64b88d28053b0263ff28a91877d0dcca4ef038",
            "ac87733a4dd6b1714915a244318d860b50584816f5b892881929b851e19b56c6",
            "f9ceeaed63014c659e783d0730f849661fcc48a3a7886976ec90ae11e4ee1d99"
        };
        [Serializable] private sealed class Manifest
        {
            public int[] canvas_px = Array.Empty<int>(), pivot_pixels_top_left = Array.Empty<int>(), nominal_floor_pixels_top_left_xywh = Array.Empty<int>();
            public int pixels_per_unit = 0;
        }

        public static string Fingerprint() => Hash(Encoding.UTF8.GetBytes(string.Join("\n", Files.Select((f, i) => f + "=" + Hashes[i]))));

        public static void PrepareApprovedInputs()
        {
            ReadSources();
            for (var i = 0; i < 3; i++) ConfigureImport(InputRoot + "/" + Files[i]);
            RequireApprovedInputs(null);
        }

        public static void RequireApprovedInputs(string savedFingerprint = null)
        {
            ReadSources();
            if (!string.IsNullOrEmpty(savedFingerprint) && savedFingerprint != Fingerprint())
                throw new InvalidDataException("Saved Bridge01 source fingerprint changed; refusing silent migration.");
            for (var i = 0; i < 3; i++) RequireImport(InputRoot + "/" + Files[i]);
        }

        public static Rect[] CornerBodies(StudyABridgeRiverFinePlan fine)
        {
            var c = Floor(fine).center;
            // Body recipe2: first native candidate had7OverlapCapsule failures at footY7.5,
            // although continuous CapsuleCast was clear. Its.005u theoretical upper clearance
            // is insufficient for that actual query/contact profile. Keep width and top fixed;
            // inset only the two proposed north-corner bottom edges by one native pixel.
            // Source manifest/raw layers remain unchanged; this is an explicit physical revision.
            const float inset = 1f / Ppu;
            return new[] { new Rect(c.x - 2, c.y + .5f + inset, 27f / Ppu, .25f - inset), new Rect(c.x + 108f / Ppu, c.y + .5f + inset, 20f / Ppu, .25f - inset) };
        }

        public static Rect[] SourceProposalCornerBodies(StudyABridgeRiverFinePlan fine)
        {
            var c = Floor(fine).center;
            // Untouched source proposal [32,48,27,16] / [268,48,20,16], top-origin.
            return new[] { new Rect(c.x - 2, c.y + .5f, 27f / Ppu, .25f), new Rect(c.x + 108f / Ppu, c.y + .5f, 20f / Ppu, .25f) };
        }

        public static Rect[] RailBodies(StudyABridgeRiverFinePlan fine)
        {
            var c = Floor(fine).center;
            // Manifest proposed ground rail bands. They are separate from their projecting opaque artwork.
            return new[] { new Rect(c.x - 2, c.y + .75f, 4, .3125f), new Rect(c.x - 2, c.y - 1.0625f, 4, .3125f) };
        }

        public static byte[] MakeBodyMask(StudyABridgeRiverFinePlan fine)
        {
            // File hash fixes the source proposal and physical-vs-projected-height notes.
            // Explicit body recipe2 uses the separately reported1px north-corner inset.
            ReadSources();
            var mask = new byte[checked(fine.Width * fine.Height)];
            foreach (var r in CornerBodies(fine).Concat(RailBodies(fine)))
            {
                var x0 = Mathf.RoundToInt((r.xMin - fine.Origin.x) * Ppu); var y0 = Mathf.RoundToInt((r.yMin - fine.Origin.y) * Ppu);
                var x1 = Mathf.RoundToInt((r.xMax - fine.Origin.x) * Ppu); var y1 = Mathf.RoundToInt((r.yMax - fine.Origin.y) * Ppu);
                if (x0 < 0 || y0 < 0 || x1 > fine.Width || y1 > fine.Height) throw new InvalidDataException("Bridge body escaped the output frame.");
                for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) mask[y * fine.Width + x] = 255;
            }
            return mask;
        }

        public static void ClipApproachMask(StudyASoilMask soil, StudyABridgeRiverFinePlan fine)
        {
            var deck = Floor(fine); const float length = 1.5f;
            for (var y = 0; y < soil.Height; y++) for (var x = 0; x < soil.Width; x++)
            {
                var i = y * soil.Width + x; if (soil.Pixels[i] == 0) continue;
                var wx = soil.Origin.x + (x + .5f) / Ppu; var wy = soil.Origin.y + (y + .5f) / Ppu;
                float half;
                if (wx >= deck.xMin - length && wx < deck.xMin) half = Mathf.Lerp(1.5f, .75f, (wx - (deck.xMin - length)) / length);
                else if (wx >= deck.xMax && wx < deck.xMax + length) half = Mathf.Lerp(.75f, 1.5f, (wx - deck.xMax) / length);
                else if (wx >= deck.xMin && wx < deck.xMax) half = .75f;
                else continue;
                if (Mathf.Abs(wy - deck.center.y) > half) soil.Pixels[i] = 0;
            }
        }

        public static void BindBodies(StudyABridgeRiverFinePlan fine, Transform parent)
        {
            Floor(fine);
            if (parent == null || parent.position != Vector3.zero || parent.rotation != Quaternion.identity || parent.lossyScale != Vector3.one || parent.Find(BodyRootName) != null)
                throw new InvalidDataException("Fresh identity map root required for Bridge01 bodies.");
            var layer = LayerMask.NameToLayer("MapCollision"); if (layer < 0) throw new InvalidDataException("MapCollision layer missing.");
            var root = new GameObject(BodyRootName); root.transform.SetParent(parent, false); root.layer = layer;
            var body = root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Static;
            var composite = root.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual; composite.edgeRadius = 0; composite.vertexDistance = .00001f;
            composite.offsetDistance = 0; composite.offset = Vector2.zero; composite.isTrigger = false;
            foreach (var r in fine.BridgeBodyRectangles)
            {
                var box = root.AddComponent<BoxCollider2D>(); box.offset = r.center; box.size = r.size; box.edgeRadius = 0;
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            composite.GenerateGeometry();
        }

        public static StudyABridge01StructureReport Apply(StudyABridgeRiverFinePlan fine, Transform ground, Transform objects, Material material, string outputRoot)
        {
            RequireApprovedInputs(); var deck = Floor(fine); var source = ReadSources();
            if (ground == null || objects == null || material == null || ground.lossyScale != Vector3.one || objects.lossyScale != Vector3.one ||
                objects.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.name.Contains("bridge_post") || r.name.StartsWith("ArtFinish_BridgeBeam_", StringComparison.Ordinal)))
                throw new InvalidDataException("Bridge01 requires no legacy deck/posts/beams and native unit parent transforms.");
            var folder = ProjectPath(outputRoot);
            if (!outputRoot.StartsWith(StudyABlockSceneBuilder.ArtRoot + "/", StringComparison.Ordinal) || !outputRoot.EndsWith("/BridgeRiver/Bridge01", StringComparison.Ordinal) || Directory.Exists(folder))
                throw new IOException("Fresh run-owned Bridge01 output required.");
            Directory.CreateDirectory(folder); var paths = new string[3];
            for (var i = 0; i < 3; i++)
            {
                paths[i] = outputRoot + "/" + Files[i]; File.WriteAllBytes(ProjectPath(paths[i]), File.ReadAllBytes(ProjectPath(InputRoot + "/" + Files[i])));
                ConfigureImport(paths[i]); RequireImport(paths[i]);
            }
            AddSprite(ground, "Bridge01_FloorSupport_NoCollision", paths[0], deck.center, material, "MapGround", 11);
            // Shared graphic centers remain exact. Independent sort proxies use source foot inspection Y, not cap center or common canvas pivot.
            var backFoot = new Vector2(deck.center.x, deck.center.y + 40f / Ppu);
            var frontFoot = new Vector2(deck.center.x, deck.center.y - 67f / Ppu);
            AddSortedLayer(objects, "Bridge01_BackAssembly_NoCollision", paths[1], deck.center, backFoot, material);
            AddSortedLayer(objects, "Bridge01_FrontAssembly_NoCollision", paths[2], deck.center, frontFoot, material);
            var transparentNominal = 0; var hiddenRgb = 0;
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
            {
                var i = y * Width + x; var composite = source[3][i];
                var rgba = source[0][i]; if (source[1][i].a != 0) rgba = source[1][i]; if (source[2][i].a != 0) rgba = source[2][i];
                if (rgba.a == 0 && composite.a == 0 && !rgba.Equals(composite)) hiddenRgb++;
                // Nominal floor source Y48..144 becomes bottom-origin Y48..144. Exclusions occupy top16 rows.
                if (x >= 32 && x < 288 && y >= 48 && y < 144 && !(y >= 128 && (x < 59 || x >= 268)) && composite.a != 255) transparentNominal++;
            }
            var c = deck.center;
            return new StudyABridge01StructureReport {
                InputRoot = InputRoot, Fingerprint = Fingerprint(), Anchor = c, CommonPixelPivot = Pivot, NominalFloorBounds = deck,
                SourceProposalCornerBodies = SourceProposalCornerBodies(fine), CornerBodies = CornerBodies(fine), RailBodies = RailBodies(fine), BodyRasterRectangles = fine.BridgeBodyRectangles,
                BodyCollisionMaskSha256 = fine.BridgeBodyCollisionSha256, SourceFiles = Files.Select(f => InputRoot + "/" + f).ToArray(), SourceFileSha256 = Hashes.ToArray(), RuntimeLayerPaths = paths,
                LayerSortAnchors = new[] { c, backFoot, frontFoot },
                SourcePostInspectionPoints = new[] { c + new Vector2(-120f/Ppu,40f/Ppu), c + new Vector2(120f/Ppu,40f/Ppu), c + new Vector2(-120f/Ppu,-67f/Ppu), c + new Vector2(120f/Ppu,-67f/Ppu) },
                LayerSorting = new[] { "floor:MapGround/order11", "back:WorldObjects/order0/SortingGroup at manifest far post footY", "front:WorldObjects/order0/SortingGroup at manifest near post footY" },
                NominalFloorTransparentPixelCount = transparentNominal, CompositeTransparentRgbMismatchCount = hiddenRgb,
                CompositeRecomposition = "Exact opaque RGBA and alpha match; transparent RGB differences retained in untouched source composite.",
                Recipe = "Separate version1 trial, body recipe revision2: native320x192 binary-alpha source layers at64PPU, common center(26,7.5) for the reviewed three-block study, scale1 and common160x96 pixel pivot. Nominal4x1.5u floor and exact matching water aperture. Source proposed north-corner widths and top edges are retained, but each bottom edge is inset north by exactly1/64u after the first native OverlapCapsule queries rejected7centreline samples with only.005u nominal top clearance. Actual corner bodies and untouched source proposal bodies are reported separately; this is a revised physical proposal, not a claim that the original manifest already specified the inset. Manifest proposed north/south ground rail bands remain unchanged. These corner/rail bodies form an independent binary mask; foreground cap projection is not alpha-traced as collision. Existing3u exterior walking ports remain;1.5u bank approaches taper to floor width. Old4x3 deck/posts/thin beams omitted. Actor capsule and all Physics settings are unchanged. The original failed candidate scene/proof remain preserved; revised actual Capsule movement, corner/rail/water contact and in-engine pixels require fresh verification." };
        }

        private static Rect Floor(StudyABridgeRiverFinePlan fine)
        {
            if (fine == null || fine.BridgeStructureVersion != 1 || fine.PixelsPerUnit != Ppu || fine.DeckBounds == null || fine.DeckBounds.Length != 1 || fine.DeckBounds[0].size != new Vector2(4,1.5f))
                throw new InvalidDataException("Explicit Bridge01 4x1.5u fine plan required.");
            return fine.DeckBounds[0];
        }

        private static Color32[][] ReadSources()
        {
            for (var i = 0; i < Files.Length; i++) if (Hash(File.ReadAllBytes(ProjectPath(InputRoot + "/" + Files[i]))) != Hashes[i])
                throw new InvalidDataException("Bridge01 source bytes changed: " + Files[i]);
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(ProjectPath(InputRoot + "/bridge01-manifest.json"), Encoding.UTF8));
            if (m == null || m.pixels_per_unit != Ppu || !m.canvas_px.SequenceEqual(new[] {Width,Height}) || !m.pivot_pixels_top_left.SequenceEqual(new[]{160,96}) ||
                !m.nominal_floor_pixels_top_left_xywh.SequenceEqual(new[]{32,48,256,96})) throw new InvalidDataException("Bridge01 native canvas/floor/pivot manifest differs.");
            var pixels = Files.Take(4).Select(f => ReadPixels(File.ReadAllBytes(ProjectPath(InputRoot + "/" + f)))).ToArray();
            for (var n = 0; n < pixels.Length; n++) if (pixels[n].Any(p => p.a != 0 && p.a != 255) || (n < 3 && pixels[n].Any(p => p.a == 0 && (p.r != 0 || p.g != 0 || p.b != 0))))
                throw new InvalidDataException("Runtime source layers require binary alpha and zero transparent RGB.");
            for (var i = 0; i < Width * Height; i++)
            {
                var p = pixels[0][i]; if (pixels[1][i].a != 0) p = pixels[1][i]; if (pixels[2][i].a != 0) p = pixels[2][i]; var expected = pixels[3][i];
                if (p.a != expected.a || (p.a != 0 && !p.Equals(expected))) throw new InvalidDataException("Bridge01 layer opaque/alpha recomposition differs.");
            }
            return pixels;
        }

        private static Color32[] ReadPixels(byte[] bytes)
        {
            var t = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try { if (!t.LoadImage(bytes,false) || t.width != Width || t.height != Height) throw new InvalidDataException("Bridge01 native layer size differs."); return t.GetPixels32(); }
            finally { Object.DestroyImmediate(t); }
        }
        private static void AddSortedLayer(Transform parent, string name, string path, Vector2 center, Vector2 foot, Material material)
        {
            var proxy = new GameObject(name + "_FootSortProxy"); proxy.transform.SetParent(parent,false); proxy.transform.position = foot;
            var group = proxy.AddComponent<SortingGroup>(); group.sortingLayerName = "WorldObjects"; group.sortingOrder = 0;
            AddSprite(proxy.transform,name,path,center,material,"WorldObjects",0);
        }
        private static void AddSprite(Transform parent,string name,string path,Vector2 center,Material material,string layer,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=center;
            var r=go.AddComponent<SpriteRenderer>();r.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);r.sharedMaterial=material;
            r.color=Color.white;r.sortingLayerName=layer;r.sortingOrder=order;r.spriteSortPoint=SpriteSortPoint.Pivot;
        }
        private static void ConfigureImport(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var i=AssetImporter.GetAtPath(path) as TextureImporter;
            if(i==null)throw new InvalidDataException("Bridge01 TextureImporter missing.");
            i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=Ppu;
            i.filterMode=FilterMode.Point;i.mipmapEnabled=false;i.textureCompression=TextureImporterCompression.Uncompressed;i.crunchedCompression=false;
            i.alphaSource=TextureImporterAlphaSource.FromInput;i.alphaIsTransparency=true;i.sRGBTexture=true;i.wrapMode=TextureWrapMode.Clamp;
            i.npotScale=TextureImporterNPOTScale.None;i.maxTextureSize=2048;i.isReadable=false;
            foreach(var platform in new[]{"Standalone","Android","iPhone","WebGL","Windows Store Apps","Server"})if(i.GetPlatformTextureSettings(platform).overridden)i.ClearPlatformTextureSettings(platform);
            var s=new TextureImporterSettings();i.ReadTextureSettings(s);s.spriteAlignment=(int)SpriteAlignment.Custom;s.spritePivot=new Vector2(.5f,.5f);
            s.spriteMeshType=SpriteMeshType.FullRect;s.spriteGenerateFallbackPhysicsShape=false;i.SetTextureSettings(s);
            var d=i.GetDefaultPlatformTextureSettings();d.format=TextureImporterFormat.RGBA32;d.textureCompression=TextureImporterCompression.Uncompressed;d.maxTextureSize=2048;i.SetPlatformTextureSettings(d);i.SaveAndReimport();
        }
        private static void RequireImport(string path)
        {
            var i=AssetImporter.GetAtPath(path) as TextureImporter;var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(i==null||s==null||s.rect!=new Rect(0,0,Width,Height)||s.pixelsPerUnit!=Ppu||s.pivot!=Pivot||s.GetPhysicsShapeCount()!=0||s.packed||
                i.filterMode!=FilterMode.Point||i.mipmapEnabled||i.textureCompression!=TextureImporterCompression.Uncompressed||i.crunchedCompression||i.spriteImportMode!=SpriteImportMode.Single||
                i.alphaSource!=TextureImporterAlphaSource.FromInput||!i.alphaIsTransparency||!i.sRGBTexture||s.texture.format!=TextureFormat.RGBA32)
                throw new InvalidDataException("Bridge01 native import contract failed: "+path);
            var cfg=new TextureImporterSettings();i.ReadTextureSettings(cfg);
            if(cfg.spriteMeshType!=SpriteMeshType.FullRect||cfg.spriteGenerateFallbackPhysicsShape||cfg.spriteAlignment!=(int)SpriteAlignment.Custom||cfg.spritePivot!=new Vector2(.5f,.5f))
                throw new InvalidDataException("Bridge01 shared fullrect/pivot contract failed: "+path);
        }
        private static string ProjectPath(string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains("\\")||relative.Split('/').Any(s=>s=="."||s==".."))throw new IOException("Safe project-relative path required.");
            var root=Directory.GetParent(Application.dataPath).FullName;var result=Path.GetFullPath(Path.Combine(root,relative));
            if(!result.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Bridge01 path escaped project.");
            for(var p=result;!p.Equals(root,StringComparison.OrdinalIgnoreCase);p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse Bridge01 path refused.");
            return result;
        }
        private static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
    }
}
