using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyAForestArtTintRecord
    {
        public string Hierarchy, SpritePath, Zone;
        public Vector2 FootAnchor;
        public Color Tint;
    }

    [Serializable]
    public sealed class StudyAForestArtFinishReport
    {
        public bool Enabled, ColliderGraphUnchanged;
        public int Version = 1, TreeTintCount, WarmPlazaTreeCount, SkippedCustomTreeCount;
        public int UnderstoryOpaquePixels, ContactShadowPixels, AddedColliderCount;
        public string UnderstoryTexturePath, ContactShadowTexturePath, UnderstoryMaskSha256, ContactShadowMaskSha256;
        public string SelectionHash, ColliderGraphBefore, ColliderGraphAfter, Recipe;
        public StudyAForestArtTintRecord[] Tints;
        public string[] SkippedTrees;
    }

    /// <summary>Opt-in display finish for a fresh run. Existing artwork, poses and all collision data remain intact.</summary>
    public static class StudyAForestArtFinishRenderer
    {
        public const int Version = 1, PixelsPerUnit = 64;
        public const string RootName = "ForestArtFinish_NoCollision";
        private sealed class Prop
        {
            public SpriteRenderer Renderer;
            public StudyAStarterProp Approved;
            public Vector2 Anchor;
            public Rect Foot;
            public string Key;
            public bool Tree => Approved.Metrics.Role == "treeLarge" || Approved.Metrics.Role == "treeMedium";
        }
        private static readonly Color[] DeepTints =
        {
            new Color(.76f, .85f, .95f, 1f), new Color(.80f, .88f, .97f, 1f), new Color(.73f, .83f, .94f, 1f)
        };

        public static StudyAForestArtFinishReport Apply(Transform obstacleRoot, Transform decorativeRoot, Material material,
            string outputRoot, StudyABlockAssembly assembly, StudyABridgeRiverFinePlan finePlan,
            Dictionary<string, StudyAStarterProp> approvedProps, StudyASoilMask roadMask = null)
        {
            if (obstacleRoot == null || decorativeRoot == null || material == null || assembly == null || finePlan == null || approvedProps == null)
                throw new ArgumentNullException("Forest finish requires explicit generated roots, approved props and the shared fine plan.");
            if (EditorUtility.IsPersistent(obstacleRoot) || EditorUtility.IsPersistent(decorativeRoot) ||
                !obstacleRoot.gameObject.scene.IsValid() || obstacleRoot.gameObject.scene != decorativeRoot.gameObject.scene ||
                !string.IsNullOrEmpty(obstacleRoot.gameObject.scene.path) || decorativeRoot.Find(RootName) != null)
                throw new InvalidOperationException("Forest finish only accepts a fresh unsaved generated Scene, never an existing Scene or source Prefab.");
            if (finePlan.Assembly == null || finePlan.Assembly.StableHash() != assembly.StableHash() || finePlan.Width <= 0 || finePlan.Height <= 0 ||
                finePlan.PixelsPerUnit != PixelsPerUnit || finePlan.VisibleWaterMask == null || finePlan.WaterCollisionMask == null ||
                finePlan.VisibleWaterMask.LongLength != (long)finePlan.Width * finePlan.Height || finePlan.WaterCollisionMask.LongLength != (long)finePlan.Width * finePlan.Height)
                throw new ArgumentException("Forest finish requires the same assembly and a complete native PPU64 fine raster.");
            if (decorativeRoot.position != Vector3.zero || decorativeRoot.rotation != Quaternion.identity || decorativeRoot.lossyScale != Vector3.one)
                throw new ArgumentException("Native ground finish requires an identity decorative root.");
            RequireOutput(outputRoot);
            if (roadMask != null && (roadMask.Width != finePlan.Width || roadMask.Height != finePlan.Height || roadMask.Origin != finePlan.Origin ||
                roadMask.PixelsPerUnit != PixelsPerUnit || roadMask.Pixels == null || roadMask.Pixels.LongLength != (long)finePlan.Width * finePlan.Height))
                throw new ArgumentException("Fine road clip must share the native frame.");

            var before = ColliderGraph(obstacleRoot);
            var props = obstacleRoot.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null)
                .Select(r => new { Renderer = r, Approved = approvedProps.Values.FirstOrDefault(p => p != null && p.Sprite == r.sprite) })
                .Where(p => p.Approved != null)
                .Select(p => new Prop { Renderer = p.Renderer, Approved = p.Approved, Anchor = (Vector2)p.Renderer.transform.position,
                    Foot = Translate(p.Approved.Metrics.LocalFootprint, (Vector2)p.Renderer.transform.position), Key = Hierarchy(p.Renderer.transform) })
                .OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
            if (props.Any(p => p.Renderer.transform.lossyScale != Vector3.one || p.Renderer.transform.rotation != Quaternion.identity))
                throw new ArgumentException("Approved feet must retain native unit scale and orientation.");
            var skipped = props.Where(p => p.Tree && !Eligible(p.Renderer, material)).Select(p => p.Key + ": authored tint/material/propertyblock/shape retained").ToList();
            var eligible = props.Where(p => Eligible(p.Renderer, material)).ToArray();
            var trees = eligible.Where(p => p.Tree).ToArray();
            var plazas = assembly.Blocks.Where(b => b.Definition.Kind == StudyABlockKind.Plaza).ToArray();
            var clearing = plazas.SelectMany(b => b.Definition.SoilCells.Except(b.Definition.RoadCells).Select(c => (Vector2)(c + b.Origin) + Vector2.one * .5f)).ToArray();
            if (clearing.Length == 0) clearing = plazas.SelectMany(b => b.Definition.SoilCells.Select(c => (Vector2)(c + b.Origin) + Vector2.one * .5f)).ToArray();
            if (trees.Length == 0 || clearing.Length == 0) throw new InvalidDataException("Explicit approved tree feet and plaza soil are required.");
            var palette = UnderstoryPalette(approvedProps.Values.Single(p => p.Metrics.Role == "shrub").Sprite);
            var forbidden = GroundForbidden(assembly, finePlan);
            var underForbidden = (byte[])forbidden.Clone();
            if (roadMask != null) for (var i = 0; i < underForbidden.Length; i++) if (roadMask.Pixels[i] != 0) underForbidden[i] = 255;
            foreach (var p in props.Where(p => p.Approved.Metrics.Role == "shop" || p.Approved.Metrics.Role == "fountain"))
                FillRect(underForbidden, finePlan, Expand(p.Foot, .125f));

            var underMask = new byte[forbidden.Length]; var nearRootMask = new byte[forbidden.Length]; var shadowMask = new byte[forbidden.Length];
            var opaqueWidths = trees.Select(p => p.Renderer.sprite).Distinct().ToDictionary(s => s, OpaqueWidth);
            var rootRadii = trees.ToDictionary(p => p, p => Mathf.Clamp(Mathf.Min(opaqueWidths[p.Renderer.sprite] * .22f, p.Foot.width * .80f + .125f), .32f, .85f));
            foreach (var p in trees)
            {
                var radiusY = Mathf.Clamp(p.Foot.height * .35f + .08f, .16f, .25f);
                UnderstoryBrush(underMask, finePlan, p.Foot.center, rootRadii[p], radiusY, assembly.Seed);
                UnderstoryBrush(nearRootMask, finePlan, p.Foot.center, rootRadii[p] * .46f, radiusY * .68f, assembly.Seed);
            }
            for (var i = 0; i < trees.Length; i++) for (var j = i + 1; j < trees.Length; j++)
            {
                var a = trees[i].Foot.center; var b = trees[j].Foot.center;
                if (Mathf.Abs(a.y - b.y) > .3f || Vector2.Distance(a, b) > Mathf.Min(1.5f, (rootRadii[trees[i]] + rootRadii[trees[j]]) * .9f)) continue;
                var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 8));
                for (var n = 0; n <= steps; n++) UnderstoryBrush(underMask, finePlan, Vector2.Lerp(a, b, n / (float)steps), .18f, .11f, assembly.Seed);
            }
            foreach (var p in eligible)
            {
                var center = new Vector2(p.Foot.center.x + 6f / PixelsPerUnit, p.Foot.yMin - 3f / PixelsPerUnit);
                Brush(shadowMask, finePlan, center, Mathf.Clamp(p.Foot.width * .43f, .18f, .92f), .0859375f, assembly.Seed, false);
            }
            for (var i = 0; i < forbidden.Length; i++)
            {
                if (underForbidden[i] != 0) underMask[i] = 0;
                if (forbidden[i] != 0) shadowMask[i] = 0;
            }
            var underPixels = new Color32[underMask.Length]; var shadowPixels = new Color32[shadowMask.Length];
            for (var y = 0; y < finePlan.Height; y++) for (var x = 0; x < finePlan.Width; x++)
            {
                var i = y * finePlan.Width + x;
                if (underMask[i] != 0) underPixels[i] = palette[(nearRootMask[i] != 0 ? 0 : 2) + (int)(Mix(assembly.Seed, x / 4, y / 4, 41) % 2)];
                if (shadowMask[i] != 0) shadowPixels[i] = new Color32(22, 38, 32, 105);
            }
            Directory.CreateDirectory(ProjectPath(outputRoot));
            var underPath = outputRoot + "/forest-undergrowth.png"; var shadowPath = outputRoot + "/forest-contact-shadow.png";
            var underSprite = WriteSprite(underPixels, finePlan.Width, finePlan.Height, underPath);
            var shadowSprite = WriteSprite(shadowPixels, finePlan.Width, finePlan.Height, shadowPath);
            var root = new GameObject(RootName); root.transform.SetParent(decorativeRoot, false);
            Ground(root.transform, "ContinuousUndergrowth_NoCollision", underSprite, material, 12, finePlan.Origin);
            Ground(root.transform, "HardContactShadow_NoCollision", shadowSprite, material, 13, finePlan.Origin);
            var tints = new List<StudyAForestArtTintRecord>(); var warm = 0;
            foreach (var p in trees)
            {
                var r = p.Renderer;
                var near = clearing.Min(c => Vector2.SqrMagnitude(c - p.Anchor)) <= 2.25f * 2.25f;
                var selected = near && Mix(assembly.Seed, Mathf.RoundToInt(p.Anchor.x * 64), Mathf.RoundToInt(p.Anchor.y * 64), 73) % 3 == 0;
                var tint = selected ? new Color(.99f, 1f, .78f, 1f) : near ? new Color(.90f, .95f, .98f, 1f)
                    : DeepTints[Mix(assembly.Seed, Mathf.RoundToInt(p.Anchor.x * 64), Mathf.RoundToInt(p.Anchor.y * 64), 79) % (uint)DeepTints.Length];
                r.color = tint; if (selected) warm++;
                tints.Add(new StudyAForestArtTintRecord { Hierarchy = p.Key, SpritePath = AssetDatabase.GetAssetPath(r.sprite), FootAnchor = p.Anchor,
                    Zone = selected ? "plaza-yellowgreen" : near ? "plaza-transition" : "deep-cool", Tint = tint });
            }
            var after = ColliderGraph(obstacleRoot); var added = root.GetComponentsInChildren<Collider2D>(true).Length;
            if (before != after || added != 0) throw new InvalidDataException("Display finish changed existing collision graph or introduced a collider.");
            return new StudyAForestArtFinishReport
            {
                Enabled = true, ColliderGraphUnchanged = true, TreeTintCount = tints.Count, WarmPlazaTreeCount = warm,
                SkippedCustomTreeCount = skipped.Count, AddedColliderCount = added, UnderstoryOpaquePixels = underMask.Count(p => p != 0),
                ContactShadowPixels = shadowMask.Count(p => p != 0), UnderstoryTexturePath = underPath, ContactShadowTexturePath = shadowPath,
                UnderstoryMaskSha256 = Hash(underMask), ContactShadowMaskSha256 = Hash(shadowMask), ColliderGraphBefore = before, ColliderGraphAfter = after,
                Tints = tints.ToArray(), SkippedTrees = skipped.ToArray(), SelectionHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", tints.Select(t =>
                    t.Hierarchy + "|" + t.Zone + "|" + t.Tint.r.ToString("R", CultureInfo.InvariantCulture) + "," + t.Tint.g.ToString("R", CultureInfo.InvariantCulture) + "," + t.Tint.b.ToString("R", CultureInfo.InvariantCulture))))),
                Recipe = "Version1 display-only: unchanged approved tree sprites/poses/light pattern, white standard-material renderers receive non-HDR alpha1 multiplication; selected plaza-soil neighbours yellowgreen, deep canopy cooler/darker. Compact native64 understory follows actual opaque tree width and physical foot width; only close same-height roots connect. Hard4px/2px edge clusters, no blur; grass-dominant green blended toward the closest approved shrub shade, darker near trunks and lighter on exposed fringe, without copying leaf texels. Custom renderers are excluded from tint and brush sources. Short hard right/down actual-foot shadows alpha105. Main3u roads/Actor paths/ports, visible river and exact physical DeckBounds excluded; understory also excludes fine soil and POI feet, shadows may touch POI front feet. MapGround12/13, no collider/Tilemap/route mutation."
            };
        }

        private static byte[] GroundForbidden(StudyABlockAssembly assembly, StudyABridgeRiverFinePlan fine)
        {
            var bytes = (byte[])fine.VisibleWaterMask.Clone();
            foreach (var r in fine.DeckBounds) FillRect(bytes, fine, r);
            foreach (var cell in assembly.RoadCells) FillRect(bytes, fine, new Rect(cell.x, cell.y, 1, 1));
            foreach (var route in assembly.MainRoutes) for (var i = 0; i < route.Count; i++)
            {
                var a = route[Math.Max(0, i - 1)] + new Vector2(0, .22f); var b = route[i] + new Vector2(0, .22f);
                var rect = Rect.MinMaxRect(Mathf.Min(a.x,b.x)-.24f, Mathf.Min(a.y,b.y)-.275f, Mathf.Max(a.x,b.x)+.24f, Mathf.Max(a.y,b.y)+.275f);
                FillRect(bytes, fine, rect);
            }
            foreach (var b in assembly.Blocks) foreach (var port in b.Definition.Ports.Where(p => p.IsWalkable))
                foreach (var cell in port.Cells) FillRect(bytes, fine, new Rect(cell.x + b.Origin.x, cell.y + b.Origin.y, 1, 1));
            FillRect(bytes, fine, new Rect(assembly.Spawn.x-.5f, assembly.Spawn.y-.5f, 1, 1));
            return bytes;
        }
        private static void Brush(byte[] mask, StudyABridgeRiverFinePlan fine, Vector2 center, float rx, float ry, int seed, bool irregular)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt((center.x-rx*1.15f-fine.Origin.x)*64)); var x1 = Mathf.Min(fine.Width, Mathf.CeilToInt((center.x+rx*1.15f-fine.Origin.x)*64));
            var y0 = Mathf.Max(0, Mathf.FloorToInt((center.y-ry*1.15f-fine.Origin.y)*64)); var y1 = Mathf.Min(fine.Height, Mathf.CeilToInt((center.y+ry*1.15f-fine.Origin.y)*64));
            for (var y=y0;y<y1;y++) for (var x=x0;x<x1;x++)
            {
                var dx=(fine.Origin.x+(x+.5f)/64-center.x)/rx; var dy=(fine.Origin.y+(y+.5f)/64-center.y)/ry;
                var edge=irregular ? 1f+((int)(Mix(seed,x/8,y/8,43)%16)-7)*.018f : 1f;
                if(dx*dx+dy*dy<=edge)mask[y*fine.Width+x]=255;
            }
        }
        private static void UnderstoryBrush(byte[] mask, StudyABridgeRiverFinePlan fine, Vector2 center, float rx, float ry, int seed)
        {
            var x0=Mathf.Max(0,Mathf.FloorToInt((center.x-rx-fine.Origin.x)*64));var x1=Mathf.Min(fine.Width,Mathf.CeilToInt((center.x+rx-fine.Origin.x)*64));
            var y0=Mathf.Max(0,Mathf.FloorToInt((center.y-ry-fine.Origin.y)*64));var y1=Mathf.Min(fine.Height,Mathf.CeilToInt((center.y+ry-fine.Origin.y)*64));
            for(var y=y0;y<y1;y++)for(var x=x0;x<x1;x++)
            {
                var dx=(fine.Origin.x+(x+.5f)/64-center.x)/rx;var dy=(fine.Origin.y+(y+.5f)/64-center.y)/ry;var d=dx*dx+dy*dy;
                var coarse=Mix(seed,x/4,y/4,43);var detail=Mix(seed,x/2,y/2,47);
                var edge=.80f+(coarse%7)*.025f+(detail%5)*.0125f;
                if(d>edge||d>.62f&&(coarse%5==0||detail%7==0))continue;
                mask[y*fine.Width+x]=255;
            }
        }
        private static float OpaqueWidth(Sprite sprite)
        {
            var temp=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!temp.LoadImage(File.ReadAllBytes(ProjectPath(AssetDatabase.GetAssetPath(sprite))),false))throw new IOException("Approved tree PNG unreadable.");
                var pixels=temp.GetPixels32();var left=temp.width;var right=-1;
                for(var y=0;y<temp.height;y++)for(var x=0;x<temp.width;x++)if(pixels[y*temp.width+x].a==255){left=Mathf.Min(left,x);right=Mathf.Max(right,x);}
                if(right<left||sprite.pixelsPerUnit!=PixelsPerUnit)throw new InvalidDataException("Approved tree requires native opaque pixels at PPU64.");
                return (right-left+1)/(float)PixelsPerUnit;
            }
            finally{Object.DestroyImmediate(temp);}
        }
        private static void FillRect(byte[] mask, StudyABridgeRiverFinePlan fine, Rect r)
        {
            var x0=Mathf.Max(0,Mathf.FloorToInt((r.xMin-fine.Origin.x)*64));var y0=Mathf.Max(0,Mathf.FloorToInt((r.yMin-fine.Origin.y)*64));
            var x1=Mathf.Min(fine.Width,Mathf.CeilToInt((r.xMax-fine.Origin.x)*64));var y1=Mathf.Min(fine.Height,Mathf.CeilToInt((r.yMax-fine.Origin.y)*64));
            for(var y=y0;y<y1;y++)for(var x=x0;x<x1;x++)mask[y*fine.Width+x]=255;
        }
        private static Color32[] UnderstoryPalette(Sprite shrub)
        {
            var temp = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!temp.LoadImage(File.ReadAllBytes(ProjectPath(AssetDatabase.GetAssetPath(shrub))),false))throw new IOException("Approved shrub PNG unreadable.");
                var colors=temp.GetPixels32().Where(c=>c.a==255&&c.g>c.r&&c.g>c.b).GroupBy(c=>c.r+","+c.g+","+c.b)
                    .Select(g=>g.First()).OrderBy(c=>c.r*299+c.g*587+c.b*114).ToArray();
                if(colors.Length<4)throw new InvalidDataException("Approved shrub needs four green source shades.");
                var grassPath=OverheadStudyASettings.ArtRoot+"/DerivedGroundR2/grass_01.png";
                if(!temp.LoadImage(File.ReadAllBytes(ProjectPath(grassPath)),false))throw new IOException("Approved GroundR2 grass PNG unreadable.");
                var grass=temp.GetPixels32().Where(c=>c.a==255&&c.g>c.r&&c.g>c.b).GroupBy(c=>c.r+","+c.g+","+c.b).OrderByDescending(g=>g.Count()).Select(g=>g.First()).First();
                var nearest=colors.OrderBy(c=>(c.r-grass.r)*(c.r-grass.r)+(c.g-grass.g)*(c.g-grass.g)+(c.b-grass.b)*(c.b-grass.b)).First();
                var blended=Color.Lerp((Color)grass,(Color)nearest,.25f);
                return new[]{(Color32)(blended*.84f),(Color32)(blended*.88f),(Color32)(blended*.97f),(Color32)blended}.Select(c=>new Color32(c.r,c.g,c.b,255)).ToArray();
            }
            finally{Object.DestroyImmediate(temp);}
        }
        private static Sprite WriteSprite(Color32[] pixels,int width,int height,string path)
        {
            if(File.Exists(ProjectPath(path)))throw new IOException("Art finish output exists.");
            var temp=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try{temp.SetPixels32(pixels);temp.Apply(false,false);File.WriteAllBytes(ProjectPath(path),temp.EncodeToPNG());}
            finally{Object.DestroyImmediate(temp);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=64;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.npotScale=TextureImporterNPOTScale.None;
            var textureSize=Mathf.Max(2048,Mathf.NextPowerOfTwo(Mathf.Max(width,height)));
            importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=textureSize;importer.isReadable=false;
            var s=new TextureImporterSettings();importer.ReadTextureSettings(s);s.spriteAlignment=(int)SpriteAlignment.Custom;s.spritePivot=Vector2.zero;
            s.spriteMeshType=SpriteMeshType.FullRect;s.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(s);
            var platform=importer.GetDefaultPlatformTextureSettings();platform.format=TextureImporterFormat.RGBA32;platform.textureCompression=TextureImporterCompression.Uncompressed;platform.maxTextureSize=textureSize;importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.rect.width!=width||sprite.rect.height!=height||sprite.pixelsPerUnit!=64||sprite.pivot!=Vector2.zero||sprite.GetPhysicsShapeCount()!=0)
                throw new InvalidDataException("Native display PNG size/pivot/PPU/no-physics contract failed.");
            return sprite;
        }
        private static void Ground(Transform parent,string name,Sprite sprite,Material material,int order,Vector2Int origin)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(origin.x,origin.y,0);var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;r.sortingLayerName="MapGround";r.sortingOrder=order;r.spriteSortPoint=SpriteSortPoint.Pivot;}
        private static bool Eligible(SpriteRenderer r,Material material)=>r.color.Equals(Color.white)&&r.sharedMaterial==material&&!r.HasPropertyBlock()&&!r.flipX&&!r.flipY&&r.drawMode==SpriteDrawMode.Simple&&r.maskInteraction==SpriteMaskInteraction.None;
        private static string ColliderGraph(Transform root) => Hash(Encoding.UTF8.GetBytes(string.Join("\n",root.GetComponentsInChildren<Collider2D>(true)
            .OrderBy(c=>Hierarchy(c.transform),StringComparer.Ordinal).Select(c=>Hierarchy(c.transform)+"|"+c.GetType().FullName+"|"+EditorJsonUtility.ToJson(c)+"|"+
                c.transform.position.ToString("R")+"|"+c.transform.rotation.ToString("R")+"|"+c.transform.lossyScale.ToString("R")))));
        private static string Hierarchy(Transform t){var parts=new List<string>();for(;t!=null;t=t.parent)parts.Add(t.name);parts.Reverse();return string.Join("/",parts);}
        private static Rect Translate(Rect r,Vector2 p){r.position+=p;return r;}
        private static Rect Expand(Rect r,float p)=>Rect.MinMaxRect(r.xMin-p,r.yMin-p,r.xMax+p,r.yMax+p);
        private static uint Mix(int seed,int x,int y,int salt){unchecked{var h=(uint)seed^(uint)x*73856093u^(uint)y*19349663u^(uint)salt*83492791u;h^=h>>13;h*=1274126177u;return h^(h>>16);}}
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private static string ProjectPath(string p)=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,p.Replace('/',Path.DirectorySeparatorChar));
        private static void RequireOutput(string path)
        {
            if(string.IsNullOrEmpty(path)||!path.StartsWith(StudyABlockSceneBuilder.ArtRoot+"/",StringComparison.Ordinal)||path.Contains("\\")||path.Split('/').Any(s=>string.IsNullOrEmpty(s)||s=="."||s=="..")||
                Directory.Exists(ProjectPath(path))||File.Exists(ProjectPath(path)))throw new IOException("Fresh run-owned forest art directory required.");
            var allowed=Path.GetFullPath(ProjectPath(StudyABlockSceneBuilder.ArtRoot)).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            var actual=Path.GetFullPath(ProjectPath(path));
            if(!actual.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new IOException("Forest art output must stay inside its generated art root.");
            for(var parent=Directory.GetParent(actual);parent!=null;parent=parent.Parent)
                if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Forest art output cannot traverse a reparse directory.");
        }
    }
}
