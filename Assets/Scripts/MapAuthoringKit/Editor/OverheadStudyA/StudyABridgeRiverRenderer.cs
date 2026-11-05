using System;
using System.Collections.Generic;
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
    public sealed class StudyABridgeRiverAppearanceReport
    {
        public bool Enabled, FineContoursEnabled, PartialFineForestPathEnabled, PartialFinePlazaEnabled;
        public int LandscapeVersion;
        public string VisibleWaterSha256, WaterCollisionSha256, FineFreeWalkSha256, FineWaterTexturePath;
        public string BridgeRoadSoilMaskSha256, BridgeRoadSoilTexturePath, BridgeRoadSoilSelectionHash;
        public StudyABridgeRiverContourPolygon[] WaterPolygons;
        public StudyASoilContourPolygon[] BridgeRoadPolygons;
        public Vector2Int[] RetiredMacroWaterCells, RetainedSolidCells;
        public string Fingerprint, GeometryMode, Recipe, BankMaskSha256, FoamMaskSha256, SelectionHash, TexturePath;
        public string WaterPath, DeckPath, PostPath;
        public int BankPixelCount, FoamPixelCount;
        public Vector2[] DeckAnchors, PostAnchors, BankPatchAnchors;
        public Rect[] DeckBounds, PostColliderBounds;
        public string[] BankPatchPaths, FoamSourceTexels;
    }

    public sealed class StudyABridgeRiverAppearancePlan
    {
        public int Width, Height, Seed;
        public byte[] BankMask;
        public int[] FoamSourceIndices;
        public StudyABlockAssembly Assembly;
        public readonly List<Vector2> PatchAnchors = new List<Vector2>();
        public readonly List<Sprite> PatchSprites = new List<Sprite>();
    }

    /// <summary>既存のWater/Walk/Bridge/port/Colliderを変えず、承認された5素材だけを新出力へ表示する。</summary>
    public static class StudyABridgeRiverRenderer
    {
        public const int PixelsPerUnit = 64;
        private const float ActorHalfWidth = 0.24f, ActorHalfHeight = 0.275f, ActorOffsetY = 0.22f;

        // All guards run before the caller creates an output folder or scene.
        public static void RequireCompatibleInput(StudyABlockAssembly assembly, IEnumerable<GameObject> prefabs, StudyABridgeRiverSpriteSet art, bool fineContours = false, bool allowPlazaCompanion = false)
        {
            var threeBlocks = allowPlazaCompanion && fineContours && assembly.Blocks.Count == 3
                && assembly.Blocks[0].Definition.Kind == StudyABlockKind.Plaza && assembly.Blocks[1].Definition.Kind == StudyABlockKind.ForestPath && assembly.Blocks[2].Definition.Kind == StudyABlockKind.BridgeBank;
            if (assembly.Blocks.Count > (threeBlocks ? 3 : 2) || assembly.Blocks.Count(b => b.Definition.Kind == StudyABlockKind.BridgeBank) != 1)
                throw new ArgumentException("BridgeRiver art requires one BridgeBank, standalone or paired with one ForestPath.");
            if (assembly.Blocks.Any(b => b.Definition.Kind != StudyABlockKind.BridgeBank &&
                (b.Definition.WaterCells.Count != 0 || b.Definition.BridgeFloorCells.Count != 0)))
                throw new ArgumentException("BridgeRiver cannot silently skin authored water/deck in a different block kind.");
            RequireSprite(art.Water, 64, 64, new Vector2(0.5f, 0.5f));
            RequireSprite(art.Deck, 256, 192, new Vector2(0.5f, 0f));
            if (threeBlocks) ComposeNorthExtendedDeck(ReadPixels(art.Deck)); // Validate opaque native north row before output mutations.
            RequireSprite(art.Post, 16, 48, new Vector2(0.5f, 0f));
            RequireSprite(art.BankLarge, 64, 64, new Vector2(0.5f, 0.125f));
            RequireSprite(art.BankSmall, 32, 48, new Vector2(0.5f, 1f / 12f));
            foreach (var block in assembly.Blocks.Where(b => b.Definition.Kind == StudyABlockKind.BridgeBank))
            {
                var floor = CellBounds(block.Definition.BridgeFloorCells);
                if (floor.width != 4 || floor.height != 3 || block.Definition.BridgeFloorCells.Count != 12)
                    throw new ArgumentException("Approved deck requires the unchanged rectangular4x3 authored BridgeFloor.");
                foreach (var row in block.Definition.WaterCells.GroupBy(c => c.y))
                    if (row.Count() != 2 || row.Max(c => c.x) - row.Min(c => c.x) != 1)
                        throw new ArgumentException("This appearance mode requires the existing contiguous logical2-unit water rows.");
            }
            foreach (var prefab in prefabs.Where(p => p != null).Distinct()) RequireNativeArt(prefab, art, fineContours && prefab.GetComponent<StudyABlockPrefabData>()?.Definition.Kind==StudyABlockKind.BridgeBank);
        }


        public static StudyASoilContourPolygon DefaultRoadPolygon(StudyABlockDefinition definition, Vector2Int origin)
        {
            if (definition.Kind != StudyABlockKind.BridgeBank || definition.Extent != new Vector2Int(12,12)) throw new ArgumentException("Fine bridge road requires12x12 BridgeBank.");
            return new StudyASoilContourPolygon { BlockId=definition.Id, Origin=origin, Extent=definition.Extent, PixelStep=8,
                Points=new[] { new Vector2(0,4),new Vector2(.75f,4),new Vector2(1.5f,4.25f),new Vector2(2.5f,4.5f),new Vector2(3.25f,4.125f),new Vector2(4,4),
                    new Vector2(8,4),new Vector2(8.75f,4.25f),new Vector2(9.75f,4.125f),new Vector2(10.75f,4.25f),new Vector2(11.25f,4),new Vector2(12,4),
                    new Vector2(12,7),new Vector2(11.25f,7),new Vector2(10.5f,6.875f),new Vector2(9.75f,6.875f),new Vector2(9,7.125f),new Vector2(8,7),
                    new Vector2(4,7),new Vector2(3.25f,7.25f),new Vector2(2.5f,7.125f),new Vector2(1.5f,6.875f),new Vector2(.75f,7),new Vector2(0,7) } };
        }

        public static StudyABridgeRiverAppearancePlan MakeFinePlan(StudyABridgeRiverFinePlan fine, int seed, StudyABridgeRiverSpriteSet art, int landscapeVersion = 1)
        {
            var plan = new StudyABridgeRiverAppearancePlan { Width=fine.Width, Height=fine.Height, Seed=seed, Assembly=fine.Assembly,
                BankMask=new byte[fine.Width*fine.Height], FoamSourceIndices=Enumerable.Repeat(-1,fine.Width*fine.Height).ToArray() };
            var distance = new byte[plan.BankMask.Length]; var nearest = new int[distance.Length]; var queue = new Queue<int>();
            // Native pixel distance outside the shared water mask. No end caps outside the authored river domain.
            for (var i=0;i<distance.Length;i++) if (fine.VisibleWaterMask[i]!=0) { distance[i]=1; nearest[i]=i; queue.Enqueue(i); }
            var block = fine.Assembly.Blocks.Single(b=>b.Definition.Kind==StudyABlockKind.BridgeBank);
            while (queue.Count>0)
            {
                var i=queue.Dequeue(); var d=distance[i]; if (d>=21) continue; var x=i%fine.Width; var y=i/fine.Width;
                for (var dy=-1;dy<=1;dy++) for (var dx=-1;dx<=1;dx++)
                {
                    var nx=x+dx;var ny=y+dy;if(nx<0||ny<0||nx>=fine.Width||ny>=fine.Height)continue;
                    var n=ny*fine.Width+nx;if(distance[n]!=0)continue;distance[n]=(byte)(d+1);nearest[n]=nearest[i];queue.Enqueue(n);
                }
            }
            for(var y=0;y<fine.Height;y++)for(var x=0;x<fine.Width;x++)
            {
                var i=y*fine.Width+x;if(distance[i]<=1||distance[i]>21)continue;
                var point=new Vector2((x+.5f)/64f,(y+.5f)/64f);if(!block.Bounds.Contains(Vector2Int.FloorToInt(point)))continue;
                var edge=nearest[i];var thickness=8+(int)(Mix(seed,(edge%fine.Width)/24,(edge/fine.Width)/32,501)%13);
                if(distance[i]-1>thickness || fine.DeckBounds.Any(r=>r.Contains(point)) || ProtectedPixel(fine.Assembly,x,y))continue;
                plan.BankMask[i]=255;
            }
            if(landscapeVersion>=2)
            {
                // Three bounded cohorts, with two unequal native pieces each; bridge/port approaches stay empty.
                AddFinePatch(plan,fine,art.BankLarge,false,seed,8.5f,10.5f,true);
                AddFinePatch(plan,fine,art.BankSmall,false,unchecked(seed^0x196303),8.25f,9.875f,true);
                AddFinePatch(plan,fine,art.BankLarge,true,unchecked(seed^0x34157),1.5f,3.5f,true);
                AddFinePatch(plan,fine,art.BankSmall,true,unchecked(seed^0x79461),1.875f,3.625f,true);
                AddFinePatch(plan,fine,art.BankLarge,true,unchecked(seed^0x36721),7.875f,9.5f,true);
                AddFinePatch(plan,fine,art.BankSmall,true,unchecked(seed^0x53871),8.125f,9.75f,true);
            }
            else
            {
                AddFinePatch(plan,fine,art.BankLarge,false,seed);
                AddFinePatch(plan,fine,art.BankSmall,true,unchecked(seed^0x196303));
            }
            return plan;
        }

        private static void AddFinePatch(StudyABridgeRiverAppearancePlan plan, StudyABridgeRiverFinePlan fine, Sprite sprite, bool east, int seed, float lowY = 1.25f, float highY = 10.625f, bool fullHeightEdge = false)
        {
            var block=fine.Assembly.Blocks.Single(b=>b.Definition.Kind==StudyABlockKind.BridgeBank);
            // Unequal banks and heights. Anchor actual grass artwork against the nearest fine water edge.
            foreach(var row in Enumerable.Range(Mathf.RoundToInt(lowY*64),Mathf.RoundToInt((highY-lowY)*64)).OrderBy(y=>Mix(seed,y,east?1:-1,991)))
            {
                var y=block.Origin.y*64+row; if(y>=fine.Height)continue;
                var hits=Enumerable.Range(block.Origin.x*64,block.Definition.Extent.x*64).Where(x=>fine.VisibleWaterMask[y*fine.Width+x]!=0).ToArray();
                if(hits.Length==0)continue;
                if(fullHeightEdge)
                {
                    // The bank graphic is native and upright: use the extremal shore over its complete vertical canvas.
                    // A single-row tangent could put the other end into a curved river. Version1 keeps its original anchor recipe.
                    var bottom=Mathf.FloorToInt(y+sprite.bounds.min.y*64);var top=Mathf.CeilToInt(y+sprite.bounds.max.y*64);
                    if(bottom<block.Origin.y*64||top>(block.Origin.y+block.Definition.Extent.y)*64)continue;
                    var edge=east?int.MinValue:int.MaxValue;
                    for(var sy=bottom;sy<top;sy++)for(var sx=block.Origin.x*64;sx<(block.Origin.x+block.Definition.Extent.x)*64;sx++)
                        if(fine.VisibleWaterMask[sy*fine.Width+sx]!=0)edge=east?Math.Max(edge,sx):Math.Min(edge,sx);
                    if(edge==int.MinValue||edge==int.MaxValue)continue;hits=new[]{edge};
                }
                var boundary=(east?hits.Last()+1:hits.First())/64f;
                var position=new Vector2(east?boundary-(float)sprite.bounds.min.x+.015625f:boundary-(float)sprite.bounds.max.x-.015625f,y/64f);
                var rect=new Rect(position+(Vector2)sprite.bounds.min,sprite.bounds.size);
                if(!Contains(new Rect(block.Origin,block.Definition.Extent),rect)||fine.WaterIntersectsRect(rect)||fine.DeckBounds.Any(r=>r.Overlaps(rect))||ProtectedRect(fine.Assembly,rect))continue;
                if(fullHeightEdge && (!fine.IsFreeRect(rect) || plan.PatchAnchors.Where((p,i)=>p.y==position.y && rect.Overlaps(new Rect(p+(Vector2)plan.PatchSprites[i].bounds.min,plan.PatchSprites[i].bounds.size))).Any()))continue;
                plan.PatchAnchors.Add(position);plan.PatchSprites.Add(sprite);return;
            }
            throw new InvalidDataException("Fine bank patches lack a safe passable shore anchor.");
        }

        public static void RequireFineNativeScenery(StudyABridgeRiverFinePlan fine, IEnumerable<GameObject> prefabs, Dictionary<string,StudyAStarterProp> props)
        {
            foreach(var block in fine.Assembly.Blocks)
            {
                var prefab=prefabs.FirstOrDefault(p=>p!=null&&p.GetComponent<StudyABlockPrefabData>()?.Definition.Kind==block.Definition.Kind);if(prefab==null)continue;
                var root=prefab.transform.Find("FixedScenery");if(root==null)continue;
                foreach(var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var known=props.Values.FirstOrDefault(p=>p.Sprite==r.sprite);
                    if(known==null)throw new ArgumentException("Fine Native custom scenery has unknown foot metadata; refusing replacement.");
                    if(r.drawMode!=SpriteDrawMode.Simple||r.flipX||r.flipY||r.maskInteraction!=SpriteMaskInteraction.None||r.spriteSortPoint!=SpriteSortPoint.Pivot||r.sortingLayerName!="WorldObjects"||r.sortingOrder!=0)
                        throw new ArgumentException("Fine Native scenery requires an unflipped Simple, unmasked foot-pivot WorldObjects0 renderer: "+r.name);
                    var anchor=(Vector2)(r.transform.position-prefab.transform.position)+(Vector2)block.Origin;
                    var foot=known.Metrics.LocalFootprint;foot.position+=anchor;
                    if(fine.WaterIntersectsRect(foot)||fine.DeckBounds.Any(d=>d.Overlaps(foot)))throw new ArgumentException("Native foot overlaps the new water/deck: "+r.name);
                }
                foreach(var c in root.GetComponentsInChildren<Collider2D>(true))
                {
                    if(!(c is BoxCollider2D box))throw new ArgumentException("Fine Native scenery supports explicitly measured BoxCollider2D only.");
                    var center=(Vector2)(box.transform.position-prefab.transform.position)+(Vector2)block.Origin+box.offset;
                    if(fine.CollisionIntersectsRect(new Rect(center-box.size*.5f,box.size)))throw new ArgumentException("Native body intersects fine water: "+box.name);
                }
            }
        }

        public static void RequireFineScenery(StudyABridgeRiverFinePlan fine, Transform obstacles)
        {
            Physics2D.SyncTransforms();
            foreach(var c in obstacles.GetComponentsInChildren<Collider2D>(true))
            {
                var rect=new Rect(c.bounds.min,c.bounds.size);
                // Backdrop can extend outside the frame; only a water intersection in-frame matters.
                var domain=fine.WorldBounds; var clipped=Rect.MinMaxRect(Mathf.Max(domain.xMin,rect.xMin),Mathf.Max(domain.yMin,rect.yMin),Mathf.Min(domain.xMax,rect.xMax),Mathf.Min(domain.yMax,rect.yMax));
                if(clipped.width>0&&clipped.height>0&&fine.CollisionIntersectsRect(clipped))throw new ArgumentException("Generated scenery body intersects fine water: "+c.name);
            }
        }

        public static StudyASoilMask ClipRoadMask(StudyASoilMask original, StudyABridgeRiverFinePlan fine)
        {
            var mask=new StudyASoilMask{Origin=original.Origin,Width=original.Width,Height=original.Height,PixelsPerUnit=original.PixelsPerUnit,Pixels=(byte[])original.Pixels.Clone()};
            for(var i=0;i<mask.Pixels.Length;i++)if(fine.WaterCollisionMask[i]!=0 || (fine.BridgeBodyCollisionMask != null && fine.BridgeBodyCollisionMask[i]!=0))mask.Pixels[i]=0;
            if (fine.BridgeStructureVersion == 1) StudyABridge01Structure.ClipApproachMask(mask, fine);
            return mask;
        }

        public static Color32[] ComposeFineWater(StudyABridgeRiverFinePlan fine, Color32[] water)
        {
            if(water==null||water.Length!=4096)throw new ArgumentException("Native64 water texels required.");
            var pixels=new Color32[fine.Width*fine.Height];
            for(var y=0;y<fine.Height;y++)for(var x=0;x<fine.Width;x++)
            {var i=y*fine.Width+x;if(fine.VisibleWaterMask[i]==0)continue;var pixel=water[(y%64)*64+x%64];pixel.a=255;pixels[i]=pixel;}
            return pixels;
        }

        public static StudyABridgeRiverAppearanceReport ApplyFine(StudyABridgeRiverAppearancePlan plan, StudyABridgeRiverFinePlan fine, StudyASoilMask road,
            StudyASoilContourPolygon[] roadPolygons,StudyABridgeRiverSpriteSet art,StudyATerrainSpriteSet terrain,Tilemap waterMap,Tilemap terrainMap,Transform parent,Material material,string path,int landscapeVersion = 1)
        {
            var extendedDeckPixels = fine.BridgeStructureVersion == 0 && fine.Assembly.Blocks.Count == 3 ? ComposeNorthExtendedDeck(ReadPixels(art.Deck)) : null;
            Directory.CreateDirectory(path);
            var report=Apply(plan,art,terrain,waterMap,terrainMap,parent,material,path+"/shoreline.png",fine.BridgeStructureVersion == 0);
            if (extendedDeckPixels != null)
            {
                var bridge = fine.Assembly.Blocks.Single(b => b.Definition.Kind == StudyABlockKind.BridgeBank);
                var bounds = fine.DeckBounds.Single();
                if (bounds != new Rect(bridge.Origin.x + 4, bridge.Origin.y + 4, 4, 3 + 1f / PixelsPerUnit))
                    throw new InvalidDataException("The current three-block deck appearance must match its one-native-pixel north aperture.");
                var deckPath = path + "/bridge-deck-north-extended.png";
                var deck = WriteSprite(256, 193, extendedDeckPixels, deckPath, new Vector2(.5f, 0));
                var renderer = parent.Find(bridge.Definition.Id + "_BridgeDeck_NoCollision")?.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sprite != art.Deck) throw new InvalidDataException("Run-owned native deck renderer is missing or was replaced.");
                renderer.sprite = deck;
                var spriteBounds = deck.bounds; var worldBounds = renderer.bounds;
                // Unity gives native Sprite bounds0.2u of Z thickness; this is a2D XY aperture contract.
                if (deck.rect.width != 256 || deck.rect.height != 193 || deck.pixelsPerUnit != PixelsPerUnit || !deck.pivot.Equals(new Vector2(128, 0))
                    || spriteBounds.min.x != -2 || spriteBounds.max.x != 2 || spriteBounds.min.y != 0 || spriteBounds.max.y != 3 + 1f / PixelsPerUnit
                    || worldBounds.min.x != bounds.xMin || worldBounds.min.y != bounds.yMin || worldBounds.max.x != bounds.xMax || worldBounds.max.y != bounds.yMax)
                    throw new InvalidDataException("Generated native deck bounds differ from the physical aperture.");
                report.DeckBounds = fine.DeckBounds.ToArray(); report.DeckPath = deckPath;
            }
            waterMap.GetComponent<TilemapRenderer>().enabled=false;
            foreach(var cell in terrainMap.cellBounds.allPositionsWithin)
                if(terrainMap.GetTile<Tile>(cell)?.sprite?.name.StartsWith("dirt_",StringComparison.Ordinal)==true)terrainMap.SetTile(cell,null);
            var waterPath=path+"/fine-water.png";var waterSprite=WriteSprite(fine.Width,fine.Height,ComposeFineWater(fine,ReadPixels(art.Water)),waterPath);
            GroundSprite(parent,"FineBridgeRiverSurface_NoCollision",waterSprite,(Vector2)fine.Origin,material,5);
            var clipped=ClipRoadMask(road,fine);
            StudyASoilContourRenderer.RequireRouteAndPorts(clipped,fine.Assembly,roadPolygons);
            var roadPath=path+"/bridge-road-soil.png";var roadSprite=StudyASoilContourRenderer.WriteSprite(clipped,terrain,plan.Seed,roadPath,out var selection);
            GroundSprite(parent,"FineBridgeApproachSoil_NoCollision",roadSprite,(Vector2)clipped.Origin,material,10);
            foreach(var polygon in roadPolygons)
            {var go=new GameObject("BridgeRoadOutline_"+polygon.BlockId);go.transform.SetParent(parent,false);go.transform.localPosition=(Vector2)polygon.Origin;
                var c=go.AddComponent<StudyASoilContour>();c.PixelStep=polygon.PixelStep;c.Points=polygon.Points.ToList();}
            report.FineContoursEnabled=true;report.LandscapeVersion=landscapeVersion;
            report.PartialFineForestPathEnabled=roadPolygons.Any(p=>fine.Assembly.Blocks.Any(b=>b.Definition.Id==p.BlockId&&b.Definition.Kind==StudyABlockKind.ForestPath));
            report.PartialFinePlazaEnabled=roadPolygons.Any(p=>fine.Assembly.Blocks.Any(b=>b.Definition.Id==p.BlockId&&b.Definition.Kind==StudyABlockKind.Plaza));
            report.GeometryMode=StudyABridgeRiverFineGeometry.GeometryMode;
            report.Recipe="Editable native closed river8/16px binary raster drives visible water and exact water collision minus full4x3 deck. Old macro water blockers retired; solid cells retained. Native64 water/R2 soil RGB copied1:1 without retint, scaling or foam invention. Exterior banks8-20px vary asymmetrically; approach soil uses closed native controls and is clipped away from blocked water. Structural coarse masks remain connection metadata.";
            if (extendedDeckPixels != null) report.Recipe += " Current32x16 three-block deck north edge extends exactly1/64u: generated256x193 PNG preserves native256x192 RGBA and duplicates native top row191 once at PPU64; full visible deck and water-mask aperture share the reported DeckBounds. Native deck source=" + AssetDatabase.GetAssetPath(art.Deck) + "; original12 logical floor cells and post anchors unchanged.";
            if (fine.BridgeStructureVersion == 1)
            {
                report.DeckBounds = fine.DeckBounds.ToArray(); report.DeckAnchors = fine.DeckBounds.Select(d => d.center).ToArray();
                report.DeckPath = StudyABridge01Structure.InputRoot + "/bridge01-floor-support.png";
                report.PostPath = ""; report.PostAnchors = Array.Empty<Vector2>(); report.PostColliderBounds = Array.Empty<Rect>();
                report.Recipe = "Separate BridgeStructure1: source river/water texels preserved; shared water collider opening is nominal4x1.5u Bridge01 floor. Pillar corner bodies are a distinct mask/collider, not water or opaque-alpha silhouettes. Existing3u exterior ports retained, soil approaches taper to1.5u at the floor. Old deck/posts/beams omitted; native Bridge01 layer and body details are reported independently.";
            }
            report.VisibleWaterSha256=fine.VisibleWaterSha256;report.WaterCollisionSha256=fine.WaterCollisionSha256;report.FineFreeWalkSha256=fine.FineFreeWalkSha256;
            report.FineWaterTexturePath=waterPath;report.BridgeRoadSoilMaskSha256=Hash(clipped.Pixels);report.BridgeRoadSoilTexturePath=roadPath;report.BridgeRoadSoilSelectionHash=selection;
            report.WaterPolygons=fine.Polygons;report.BridgeRoadPolygons=roadPolygons;report.RetiredMacroWaterCells=fine.RetiredMacroWaterCells;report.RetainedSolidCells=fine.RetainedSolidCells;return report;
        }

        public static void AddFineNativePreview(GameObject root,StudyABlockDefinition definition,StudyABridgeRiverSpriteSet art,StudyATerrainSpriteSet terrain,int settingsSeed,Material material,string outputRoot)
        {
            var assembly=StudyABlockGenerator.Single(definition,Vector2Int.zero,settingsSeed);
            var waterPolygon=StudyABridgeRiverFineGeometry.ReadPolygon(definition,Vector2Int.zero,root.GetComponent<StudyABridgeRiverContour>());
            var fine=StudyABridgeRiverFineGeometry.MakePlan(assembly,new[]{waterPolygon},12,12);
            var roadPolygon=DefaultRoadPolygon(definition,Vector2Int.zero);var road=ClipRoadMask(StudyASoilContourRenderer.MakeMask(new[]{roadPolygon},Vector2Int.zero,12,12),fine);
            var folder=outputRoot+"/BridgeRiver";Directory.CreateDirectory(folder);
            var water=WriteSprite(fine.Width,fine.Height,ComposeFineWater(fine,ReadPixels(art.Water)),folder+"/native-fine-water.png");
            var soil=StudyASoilContourRenderer.WriteSprite(road,terrain,settingsSeed,folder+"/native-fine-road.png",out _);
            var proof=root.GetComponent<StudyABridgeRiverContour>();
            proof.PreviewWaterGuid=AssetDatabase.AssetPathToGUID(folder+"/native-fine-water.png");proof.PreviewWaterPngSha256=Hash(File.ReadAllBytes(folder+"/native-fine-water.png"));proof.PreviewWaterMetaSha256=Hash(File.ReadAllBytes(folder+"/native-fine-water.png.meta"));
            proof.PreviewRoadGuid=AssetDatabase.AssetPathToGUID(folder+"/native-fine-road.png");proof.PreviewRoadPngSha256=Hash(File.ReadAllBytes(folder+"/native-fine-road.png"));proof.PreviewRoadMetaSha256=Hash(File.ReadAllBytes(folder+"/native-fine-road.png.meta"));
            GroundSprite(root.transform,"FineBridgeRiverAuthoringPreview_Water",water,Vector2.zero,material,5);
            GroundSprite(root.transform,"FineBridgeRiverAuthoringPreview_RoadSoil",soil,Vector2.zero,material,10);
            root.transform.Find("Grid/WaterTilemap").GetComponent<TilemapRenderer>().enabled=false;
            root.transform.Find("Grid/SoilTilemap").GetComponent<TilemapRenderer>().enabled=false;
        }

        private static void RequireSprite(Sprite sprite, int width, int height, Vector2 pivot)
        {
            if (sprite == null || sprite.rect != new Rect(0, 0, width, height) || sprite.pixelsPerUnit != PixelsPerUnit
                || Vector2.Distance(sprite.pivot, Vector2.Scale(pivot, new Vector2(width, height))) > 0.00001f
                || sprite.GetPhysicsShapeCount() != 0)
                throw new InvalidDataException("BridgeRiver sprite is not the reviewed native full-rect64PPU/no-physics artwork.");
        }

        private static void RequireNativeArt(GameObject prefab, StudyABridgeRiverSpriteSet art, bool fineContours)
        {
            var approvedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ForestRestartAssetBuilder.ArtRoot + "/ForestRestartUnlit.mat");
            foreach (var key in new[] { "Water", "BridgeFloor" })
            {
                var map = prefab.transform.Find("Grid/" + key + "Tilemap")?.GetComponent<Tilemap>();
                if (map == null) throw new ArgumentException("Native bridge mask missing: " + key);
                var mapRenderer = map.GetComponent<TilemapRenderer>();
                if (map.color != Color.white || mapRenderer.sharedMaterial != approvedMaterial || mapRenderer.HasPropertyBlock()
                    || mapRenderer.sortingLayerName != "MapGround" || mapRenderer.sortingOrder != (key == "Water" ? 4 : 6)
                    || mapRenderer.mode != TilemapRenderer.Mode.Chunk || (key == "Water" && (fineContours ? mapRenderer.enabled : !mapRenderer.enabled)))
                    throw new ArgumentException("Custom water/deck tint/material is preserved by refusing BridgeRiver replacement.");
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    if (!map.HasTile(cell)) continue;
                    var tile = map.GetTile<Tile>(cell);
                    if (tile == null || tile.GetType() != typeof(Tile) || tile.gameObject != null || tile.color != Color.white
                        || tile.colliderType != Tile.ColliderType.None || tile.flags != TileFlags.LockColor || tile.transform != Matrix4x4.identity
                        || map.GetColor(cell) != Color.white || map.GetTransformMatrix(cell) != Matrix4x4.identity)
                        throw new ArgumentException("Custom water/deck Tile settings are preserved by refusing replacement.");
                    if (!(key == "Water" && tile.sprite == art.Water) && !IsDiagnosticSprite(tile.sprite, key == "Water" ? "water" : "deck"))
                        throw new ArgumentException("Custom Native water/deck sprite is preserved; enabled replacement supports diagnostic or approved source only.");
                }
            }
            var finePreviews = prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name.StartsWith("FineBridgeRiverAuthoringPreview_", StringComparison.Ordinal)).ToArray();
            if (fineContours)
            {
                if (finePreviews.Length != 2 || finePreviews.Select(r => r.name).Distinct().Count() != 2)
                    throw new ArgumentException("Existing Fine Native requires its two derived authoring previews; controls stay authoritative.");
                foreach (var r in finePreviews)
                {
                    var water = r.name == "FineBridgeRiverAuthoringPreview_Water";
                    if (!water && r.name != "FineBridgeRiverAuthoringPreview_RoadSoil") throw new ArgumentException("Unknown fine Native preview.");
                    var spritePath = AssetDatabase.GetAssetPath(r.sprite);
                    var prefabPath=AssetDatabase.GetAssetPath(prefab);var split=prefabPath.LastIndexOf("/Prefabs/",StringComparison.Ordinal);
                    var expectedPath=split<0 ? "" : prefabPath.Substring(0,split)+"/BridgeRiver/"+(water?"native-fine-water.png":"native-fine-road.png");
                    var proof=prefab.GetComponent<StudyABridgeRiverContour>();
                    if (r.transform.parent != prefab.transform || r.transform.localPosition != Vector3.zero || r.transform.localScale != Vector3.one || r.transform.localRotation != Quaternion.identity
                        || r.sprite == null || r.sprite.rect != new Rect(0,0,768,768) || r.sprite.pivot != Vector2.zero || r.sprite.pixelsPerUnit != 64 || r.sprite.GetPhysicsShapeCount() != 0
                        || !spritePath.StartsWith(StudyABlockSceneBuilder.ArtRoot+"/",StringComparison.Ordinal) || spritePath!=expectedPath
                        || r.drawMode!=SpriteDrawMode.Simple || r.maskInteraction!=SpriteMaskInteraction.None
                        || r.color != Color.white || r.flipX || r.flipY || !r.enabled || !r.gameObject.activeSelf || r.HasPropertyBlock()
                        || r.sharedMaterial != approvedMaterial || r.sortingLayerName != "MapGround" || r.sortingOrder != (water ? 5 : 10) || r.spriteSortPoint != SpriteSortPoint.Pivot
                        || r.GetComponentsInChildren<Collider2D>(true).Length != 0
                        || proof==null || AssetDatabase.AssetPathToGUID(spritePath)!=(water?proof.PreviewWaterGuid:proof.PreviewRoadGuid)
                        || Hash(File.ReadAllBytes(spritePath))!=(water?proof.PreviewWaterPngSha256:proof.PreviewRoadPngSha256)
                        || !File.Exists(spritePath+".meta") || Hash(File.ReadAllBytes(spritePath+".meta"))!=(water?proof.PreviewWaterMetaSha256:proof.PreviewRoadMetaSha256))
                        throw new ArgumentException("Custom Fine Native preview pose/style/art/collider is preserved by refusing replacement.");
                }
            }
            else if (finePreviews.Length != 0) throw new ArgumentException("Fine Native cannot be silently downgraded to macro water; enable its fine contour setting.");
            var previews = prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name.StartsWith("BridgeRiverAuthoringPreview_", StringComparison.Ordinal)).ToArray();
            if (previews.Length > 0)
            {
                var definition = StudyABlockSceneBuilder.ReadPrefabDefinition(prefab);
                var bounds = CellBounds(definition.BridgeFloorCells);
                var expected = new Dictionary<string, Vector2>
                {
                    { "BridgeRiverAuthoringPreview_Deck", new Vector2(bounds.center.x, bounds.yMin) }
                };
                var n = 0;
                foreach (var x in new[] { bounds.xMin - 0.125f, bounds.xMax + 0.125f }) foreach (var y in new[] { bounds.yMin - 0.25f, bounds.yMax })
                    expected.Add("BridgeRiverAuthoringPreview_bridge_post_" + n++, new Vector2(x, y));
                if (previews.Length != 5 || previews.Select(r => r.name).Distinct().Count() != 5)
                    throw new ArgumentException("Native BridgeRiver preview must retain one deck and four separate posts.");
                foreach (var renderer in previews)
                {
                    var deck = renderer.name == "BridgeRiverAuthoringPreview_Deck";
                    if (!expected.TryGetValue(renderer.name, out var position) || renderer.transform.parent != prefab.transform
                        || renderer.transform.localPosition != new Vector3(position.x, position.y, 0f)
                        || renderer.transform.localScale != Vector3.one || renderer.transform.localRotation != Quaternion.identity
                        || renderer.sprite != (deck ? art.Deck : art.Post) || renderer.color != Color.white || renderer.flipX || renderer.flipY
                        || !renderer.enabled || !renderer.gameObject.activeSelf || renderer.HasPropertyBlock()
                        || renderer.sharedMaterial != approvedMaterial || renderer.spriteSortPoint != SpriteSortPoint.Pivot
                        || renderer.sortingLayerName != (deck ? "MapGround" : "WorldObjects") || renderer.sortingOrder != (deck ? 11 : 0)
                        || renderer.GetComponentsInChildren<Collider2D>(true).Length != 0)
                        throw new ArgumentException("Custom Native bridge preview art/pose/style/collider is preserved by refusing replacement.");
                }
            }
            else if (!prefab.transform.Find("Grid/BridgeFloorTilemap").GetComponent<TilemapRenderer>().enabled)
                throw new ArgumentException("Custom hidden Native deck is preserved by refusing replacement.");
            foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name.Contains("bridge_post") && !renderer.name.StartsWith("BridgeRiverAuthoringPreview_", StringComparison.Ordinal))
                    throw new ArgumentException("Custom authored Native post is preserved by refusing replacement, without duplicate generated posts.");
        }

        public static void RequireCompatibleBanks(StudyABridgeRiverAppearancePlan plan, IEnumerable<GameObject> prefabs)
        {
            var approvedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ForestRestartAssetBuilder.ArtRoot + "/ForestRestartUnlit.mat");
            foreach (var block in plan.Assembly.Blocks.Where(b => b.Definition.Kind == StudyABlockKind.BridgeBank))
            {
                var prefab = prefabs.FirstOrDefault(p => p != null && p.GetComponent<StudyABlockPrefabData>()?.Definition.Kind == block.Definition.Kind);
                if (prefab == null) continue;
                var cells = new HashSet<Vector2Int>();
                for (var y = 0; y < plan.Height; y++) for (var x = 0; x < plan.Width; x++)
                    if (plan.BankMask[y * plan.Width + x] == 255) cells.Add(new Vector2Int(x / 64, y / 64) - block.Origin);
                for (var i = 0; i < plan.PatchAnchors.Count; i++)
                {
                    var patch = new Rect(plan.PatchAnchors[i] + (Vector2)plan.PatchSprites[i].bounds.min, plan.PatchSprites[i].bounds.size);
                    for (var y = Mathf.FloorToInt(patch.yMin); y < Mathf.CeilToInt(patch.yMax); y++)
                        for (var x = Mathf.FloorToInt(patch.xMin); x < Mathf.CeilToInt(patch.xMax); x++)
                            if (block.Bounds.Contains(new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y) - block.Origin);
                }
                foreach (var key in new[] { "Ground", "Soil" })
                {
                    var map = prefab.transform.Find("Grid/" + key + "Tilemap").GetComponent<Tilemap>();
                    if (map.color != Color.white || map.GetComponent<TilemapRenderer>().sharedMaterial != approvedMaterial || map.GetComponent<TilemapRenderer>().HasPropertyBlock())
                        throw new ArgumentException("Custom Native bank-land tint/material is preserved by refusing shore overpaint.");
                    var seen = new HashSet<Sprite>();
                    foreach (var cell in cells)
                    {
                        var position = (Vector3Int)cell;
                        if (!map.HasTile(position)) continue;
                        var tile = map.GetTile<Tile>(position);
                        if (tile == null || tile.GetType() != typeof(Tile) || tile.gameObject != null || tile.color != Color.white
                            || tile.flags != TileFlags.LockColor || tile.colliderType != Tile.ColliderType.None || tile.transform != Matrix4x4.identity
                            || map.GetColor(position) != Color.white || map.GetTransformMatrix(position) != Matrix4x4.identity
                            || tile.sprite == null || (seen.Add(tile.sprite) && !StudyATerrainAssets.IsApprovedGroundSprite(tile.sprite)))
                            throw new ArgumentException("Custom Native bank-land tile is preserved by refusing shore overpaint.");
                    }
                }
            }
        }

        private static bool IsDiagnosticSprite(Sprite sprite, string key)
        {
            if (sprite == null || sprite.rect != new Rect(0, 0, 64, 64) || sprite.pixelsPerUnit != PixelsPerUnit || sprite.pivot != new Vector2(32, 32)) return false;
            var path = AssetDatabase.GetAssetPath(sprite);
            if (!path.StartsWith(StudyABlockSceneBuilder.ArtRoot + "/", StringComparison.Ordinal)
                || !path.EndsWith("/Tiles/diagnostic_" + key + ".png", StringComparison.Ordinal)) return false;
            var pixels = ReadPixels(sprite);
            for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++)
            {
                var expected = key == "water" ? new Color32(57, 117, 147, 255) : new Color32(130, 85, 47, 255);
                if (key == "deck" && (y % 16 == 0 || x == 0)) expected = new Color32(81, 53, 32, 255);
                if (key == "water" && y % 16 == 7 && x % 16 < 8) expected = new Color32(81, 147, 167, 255);
                if (!pixels[y * 64 + x].Equals(expected)) return false;
            }
            return true;
        }

        // Pure CPU appearance data. The logical DTO and every source asset are read-only.
        public static StudyABridgeRiverAppearancePlan MakePlan(StudyABlockAssembly assembly, int seed, StudyABridgeRiverSpriteSet art, int worldWidth = 24, int worldHeight = 16)
        {
            var plan = MakeAppearanceData(assembly, seed, ReadPixels(art.Water), worldWidth, worldHeight);
            foreach (var block in assembly.Blocks.Where(b => b.Definition.Kind == StudyABlockKind.BridgeBank))
            {
                var rows = block.Definition.WaterCells.GroupBy(c => c.y).OrderBy(g => g.Key).ToArray();
                AddPatch(plan, art.BankLarge, rows, block, false, seed);
                AddPatch(plan, art.BankSmall, rows, block, true, unchecked(seed ^ 0x196303));
            }
            return plan;
        }

        public static StudyABridgeRiverAppearancePlan MakeAppearanceData(StudyABlockAssembly assembly, int seed, Color32[] sourceWater, int worldWidth = 24, int worldHeight = 16)
        {
            if (assembly == null || sourceWater == null || sourceWater.Length != 4096 || worldWidth <= 0 || worldHeight <= 0 || worldWidth > 24 || worldHeight > 16)
                throw new ArgumentException("Validated assembly/native64 water/24x16 maximum are required.");
            var plan = new StudyABridgeRiverAppearancePlan
            {
                Width = worldWidth * PixelsPerUnit, Height = worldHeight * PixelsPerUnit, Seed = seed, Assembly = assembly,
                BankMask = new byte[worldWidth * worldHeight * PixelsPerUnit * PixelsPerUnit],
                FoamSourceIndices = Enumerable.Repeat(-1, worldWidth * worldHeight * PixelsPerUnit * PixelsPerUnit).ToArray()
            };
            var water = new HashSet<Vector2Int>(assembly.WaterCells);
            var floor = new HashSet<Vector2Int>(assembly.BridgeFloorCells);
            foreach (var block in assembly.Blocks.Where(b => b.Definition.Kind == StudyABlockKind.BridgeBank))
            {
                var bounds = new RectInt(block.Origin, block.Definition.Extent);
                foreach (var cell in block.Definition.WaterCells.Select(c => c + block.Origin).OrderBy(c => c.y).ThenBy(c => c.x))
                {
                    foreach (var normal in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    {
                        if (water.Contains(cell + normal)) continue;
                        // The native N/S river ports are open; do not draw a new shore cap there.
                        if (normal == Vector2Int.up && cell.y == bounds.yMax - 1 || normal == Vector2Int.down && cell.y == bounds.yMin) continue;
                        var hash = Mix(seed, cell.x, cell.y, normal.x * 7 + normal.y * 13);
                        var thickness = ((hash >> 4) & 1) == 0 ? 8 : 16;
                        var minX = cell.x * 64; var minY = cell.y * 64;
                        for (var along = 0; along < 64; along++) for (var distance = 0; distance < thickness; distance++)
                        {
                            var x = normal.x < 0 ? minX - 1 - distance : normal.x > 0 ? minX + 64 + distance : minX + along;
                            var y = normal.y < 0 ? minY - 1 - distance : normal.y > 0 ? minY + 64 + distance : minY + along;
                            if (Within(x, y, plan) && bounds.Contains(new Vector2Int(x / 64, y / 64))
                                && !water.Contains(new Vector2Int(x / 64, y / 64)) && !floor.Contains(new Vector2Int(x / 64, y / 64))
                                && !ProtectedPixel(assembly, x, y)) plan.BankMask[y * plan.Width + x] = 255;
                        }
                    }
                }
            }
            return plan;
        }

        private static void AddPatch(StudyABridgeRiverAppearancePlan plan, Sprite sprite, IGrouping<int, Vector2Int>[] rows, StudyAPlacedBlock block, bool east, int seed)
        {
            foreach (var row in rows.OrderBy(g => Mix(seed, g.Key, east ? 1 : -1, 91)))
            {
                if (row.Key <= 0 || row.Key >= block.Definition.Extent.y - 2) continue;
                var x = east ? row.Max(c => c.x) + 1.4375f : row.Min(c => c.x) - 0.6875f;
                var position = (Vector2)block.Origin + new Vector2(x, row.Key + (east ? 0.3125f : 0.0625f));
                var rect = new Rect(position + (Vector2)sprite.bounds.min, sprite.bounds.size);
                var domain = new Rect(block.Origin, block.Definition.Extent);
                if (!Contains(domain, rect) || plan.Assembly.WaterCells.Concat(plan.Assembly.BridgeFloorCells).Any(c => rect.Overlaps(new Rect(c, Vector2.one)))
                    || ProtectedRect(plan.Assembly, rect)) continue;
                plan.PatchAnchors.Add(position); plan.PatchSprites.Add(sprite); return;
            }
            throw new InvalidDataException("Two safe passable bank patch placements cannot be found; refusing an incomplete skin.");
        }

        private static bool Within(int x, int y, StudyABridgeRiverAppearancePlan plan) => x >= 0 && y >= 0 && x < plan.Width && y < plan.Height;
        private static bool ProtectedPixel(StudyABlockAssembly assembly, int x, int y) => ProtectedRect(assembly, new Rect(x / 64f, y / 64f, 1f / 64f, 1f / 64f));

        public static bool ProtectedRect(StudyABlockAssembly assembly, Rect rect)
        {
            var expanded = Rect.MinMaxRect(rect.xMin - ActorHalfWidth, rect.yMin - ActorHalfHeight, rect.xMax + ActorHalfWidth, rect.yMax + ActorHalfHeight);
            if (expanded.Contains(assembly.Spawn + Vector2.up * ActorOffsetY)) return true;
            foreach (var route in assembly.MainRoutes)
                for (var i = 0; i < route.Count; i++)
                    if (SegmentIntersectsRect(route[Math.Max(0, i - 1)] + Vector2.up * ActorOffsetY, route[i] + Vector2.up * ActorOffsetY, expanded)) return true;
            foreach (var block in assembly.Blocks) foreach (var port in block.Definition.Ports)
            {
                if (port.Terrain == StudyATerrain.Water) continue;
                Rect band;
                var position = port.Position + block.Origin;
                if (port.Direction == StudyAPortDirection.West) band = new Rect(position.x, position.y, 0.75f, port.Width);
                else if (port.Direction == StudyAPortDirection.East) band = new Rect(position.x + 0.25f, position.y, 0.75f, port.Width);
                else if (port.Direction == StudyAPortDirection.South) band = new Rect(position.x, position.y, port.Width, 0.75f);
                else band = new Rect(position.x, position.y + 0.25f, port.Width, 0.75f);
                if (rect.Overlaps(band)) return true;
            }
            return false;
        }

        private static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect rect)
        {
            float lo = 0f, hi = 1f; var delta = b - a;
            return Clip(-delta.x, a.x - rect.xMin, ref lo, ref hi) && Clip(delta.x, rect.xMax - a.x, ref lo, ref hi)
                && Clip(-delta.y, a.y - rect.yMin, ref lo, ref hi) && Clip(delta.y, rect.yMax - a.y, ref lo, ref hi);
        }

        private static bool Clip(float p, float q, ref float lo, ref float hi)
        {
            if (p == 0f) return q >= 0f;
            var t = q / p;
            if (p < 0f) { if (t > hi) return false; lo = Mathf.Max(lo, t); }
            else { if (t < lo) return false; hi = Mathf.Min(hi, t); }
            return true;
        }

        // Exact source-texel RGB, binary alpha. Origin is Unity bottom-left. No blur/retint/rescale.
        public static Color32[] Compose(StudyABridgeRiverAppearancePlan plan, Color32[][] soils, Color32[] water, out string selectionHash)
        {
            if (soils == null || soils.Length == 0 || soils.Any(p => p.Length != 4096) || water == null || water.Length != 4096)
                throw new ArgumentException("Native64x64 soil/water arrays are required.");
            var output = new Color32[plan.Width * plan.Height]; var selected = new List<string>();
            for (var y = 0; y < plan.Height; y++) for (var x = 0; x < plan.Width; x++)
            {
                var i = y * plan.Width + x;
                if (plan.BankMask[i] != 0)
                {
                    var variant = (int)(Mix(plan.Seed, x / 64, y / 64, 811) % (uint)soils.Length);
                    var pixel = soils[variant][(y % 64) * 64 + x % 64]; pixel.a = 255; output[i] = pixel;
                    selected.Add("b:" + i + ":" + variant + ":" + ((y % 64) * 64 + x % 64));
                }
                else if (plan.FoamSourceIndices[i] >= 0)
                {
                    var pixel = water[plan.FoamSourceIndices[i]]; pixel.a = 255; output[i] = pixel;
                    selected.Add("f:" + i + ":" + plan.FoamSourceIndices[i]);
                }
            }
            selectionHash = Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", selected))); return output;
        }

        public static StudyABridgeRiverAppearanceReport Apply(StudyABridgeRiverAppearancePlan plan, StudyABridgeRiverSpriteSet art, StudyATerrainSpriteSet terrain,
            Tilemap waterMap, Tilemap terrainMap, Transform parent, Material material, string path, bool includeLegacyBridge = true)
        {
            var old = waterMap.GetTile<Tile>((Vector3Int)plan.Assembly.WaterCells.OrderBy(c => c.y).ThenBy(c => c.x).First());
            if (old == null) throw new InvalidDataException("Expected water Tile missing after native merge.");
            // Clone the checked style into this new output; source Native tiles remain read-only.
            var tile = Object.Instantiate(old); tile.sprite = art.Water; tile.name = "approved_water_01";
            var tilePath = path.Substring(0, path.LastIndexOf('/')) + "/water.asset";
            if (File.Exists(tilePath)) throw new IOException("Water tile output exists.");
            AssetDatabase.CreateAsset(tile, tilePath);
            foreach (var cell in plan.Assembly.WaterCells) waterMap.SetTile((Vector3Int)cell, tile);
            foreach (var cell in plan.Assembly.BridgeFloorCells) terrainMap.SetTile((Vector3Int)cell, null);
            var waterPixels = ReadPixels(art.Water); var soils = terrain.SoilVariants.Select(ReadPixels).ToArray();
            var pixels = Compose(plan, soils, waterPixels, out var selected);
            var shoreline = WriteSprite(plan.Width, plan.Height, pixels, path);
            GroundSprite(parent, "BridgeRiver_Shore_NoCollision", shoreline, Vector2.zero, material, 6);
            var deckAnchors = new List<Vector2>(); var deckBounds = new List<Rect>(); var postAnchors = new List<Vector2>();
            foreach (var block in plan.Assembly.Blocks.Where(b => includeLegacyBridge && b.Definition.Kind == StudyABlockKind.BridgeBank))
            {
                var bounds = CellBounds(block.Definition.BridgeFloorCells.Select(c => c + block.Origin));
                var anchor = new Vector2(bounds.center.x, bounds.yMin);
                GroundSprite(parent, block.Definition.Id + "_BridgeDeck_NoCollision", art.Deck, anchor, material, 11);
                deckAnchors.Add(anchor); deckBounds.Add(bounds);
                foreach (var x in new[] { bounds.xMin - 0.125f, bounds.xMax + 0.125f }) foreach (var y in new[] { bounds.yMin - 0.25f, bounds.yMax })
                    postAnchors.Add(new Vector2(x, y));
            }
            for (var i = 0; i < plan.PatchAnchors.Count; i++) GroundSprite(parent, "BridgeRiver_BankPatch_" + i, plan.PatchSprites[i], plan.PatchAnchors[i], material, 7);
            var foamBytes = plan.FoamSourceIndices.Select(i => i >= 0 ? (byte)255 : (byte)0).ToArray();
            return new StudyABridgeRiverAppearanceReport
            {
                Enabled = true, Fingerprint = art.Fingerprint, GeometryMode = "MacroExactWaterWithFineExteriorShoreAppearance",
                Recipe = "Logical Water/Bridge/Walk/Solid/ports/colliders unchanged. Native64 water full macro cells. Exterior soil8/16px strips use R2 texels1:1, exclude Deck/continuous Actor/three port lanes; native N/S river ports uncapped. No dedicated foam source supplied: foam omitted; existing native water highlights retained. Two passable uneven bank patches. Underlying64px row shifts remain; this is not a smooth shared water collision contour.",
                BankMaskSha256 = Hash(plan.BankMask), FoamMaskSha256 = Hash(foamBytes), SelectionHash = selected, TexturePath = path,
                WaterPath = AssetDatabase.GetAssetPath(art.Water), DeckPath = AssetDatabase.GetAssetPath(art.Deck), PostPath = AssetDatabase.GetAssetPath(art.Post),
                BankPixelCount = plan.BankMask.Count(p => p == 255), FoamPixelCount = foamBytes.Count(p => p == 255),
                DeckAnchors = deckAnchors.ToArray(), DeckBounds = deckBounds.ToArray(), PostAnchors = postAnchors.ToArray(),
                PostColliderBounds = postAnchors.Select(p => new Rect(p + new Vector2(-0.125f, 0f), Vector2.one * 0.25f)).ToArray(),
                BankPatchAnchors = plan.PatchAnchors.ToArray(), BankPatchPaths = plan.PatchSprites.Select(AssetDatabase.GetAssetPath).ToArray(),
                FoamSourceTexels = plan.FoamSourceIndices.Where(i => i >= 0).Distinct().OrderBy(i => i).Select(i => i % 64 + "," + i / 64 + "=" + waterPixels[i].r + "," + waterPixels[i].g + "," + waterPixels[i].b).ToArray()
            };
        }

        public static void AddNativePreview(GameObject root, StudyABlockDefinition definition, StudyABridgeRiverSpriteSet art, Material material)
        {
            if (definition.Kind != StudyABlockKind.BridgeBank) return;
            var bounds = CellBounds(definition.BridgeFloorCells);
            GroundSprite(root.transform, "BridgeRiverAuthoringPreview_Deck", art.Deck, new Vector2(bounds.center.x, bounds.yMin), material, 11);
            root.transform.Find("Grid/BridgeFloorTilemap").GetComponent<TilemapRenderer>().enabled = false;
            var n = 0;
            foreach (var x in new[] { bounds.xMin - 0.125f, bounds.xMax + 0.125f }) foreach (var y in new[] { bounds.yMin - 0.25f, bounds.yMax })
            {
                var go = GroundSprite(root.transform, "BridgeRiverAuthoringPreview_bridge_post_" + n++, art.Post, new Vector2(x, y), material, 0);
                var renderer = go.GetComponent<SpriteRenderer>(); renderer.sortingLayerName = "WorldObjects"; renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }

        private static GameObject GroundSprite(Transform parent, string name, Sprite sprite, Vector2 position, Material material, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
            renderer.sortingLayerName = "MapGround"; renderer.sortingOrder = order; renderer.spriteSortPoint = SpriteSortPoint.Pivot; return go;
        }

        private static Color32[] ReadPixels(Sprite sprite)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)), false)) throw new IOException("Native PNG cannot be read.");
                return texture.GetPixels32();
            }
            finally { Object.DestroyImmediate(texture); }
        }

        // Unity pixel rows are bottom-to-top. Copying row191 adds only one native north row.
        private static Color32[] ComposeNorthExtendedDeck(Color32[] nativePixels)
        {
            const int width = 256, height = 192;
            if (nativePixels == null || nativePixels.Length != width * height) throw new InvalidDataException("Approved native deck RGBA dimensions changed.");
            for (var x = 0; x < width; x++)
                if (nativePixels[(height - 1) * width + x].a != 255) throw new InvalidDataException("The one-pixel north deck extension requires a fully opaque approved top row.");
            var pixels = new Color32[width * (height + 1)];
            Array.Copy(nativePixels, pixels, nativePixels.Length);
            Array.Copy(nativePixels, (height - 1) * width, pixels, height * width, width);
            return pixels;
        }

        private static Sprite WriteSprite(int width, int height, Color32[] pixels, string path, Vector2? pivot = null)
        {
            if (!path.StartsWith(StudyABlockSceneBuilder.ArtRoot + "/", StringComparison.Ordinal) || path.Contains("\\")
                || path.Split('/').Any(p => p == ".." || p == ".") || File.Exists(path)) throw new IOException("New run-owned BridgeRiver path required.");
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try { texture.SetPixels32(pixels); texture.Apply(false, false); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = false; importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048; importer.isReadable = false;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot ?? Vector2.zero; settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(settings);
            var platform = importer.GetDefaultPlatformTextureSettings(); platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed; platform.maxTextureSize = 2048; importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new IOException("BridgeRiver Sprite import failed.");
        }

        private static Rect CellBounds(IEnumerable<Vector2Int> cells)
        {
            var a = cells.ToArray(); if (a.Length == 0) throw new ArgumentException("BridgeFloor cannot be empty.");
            return Rect.MinMaxRect(a.Min(c => c.x), a.Min(c => c.y), a.Max(c => c.x) + 1, a.Max(c => c.y) + 1);
        }
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin && inner.yMin >= outer.yMin && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
        private static uint Mix(int seed, int x, int y, int salt)
        { unchecked { var h = (uint)seed ^ (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)salt * 83492791u; h ^= h >> 13; h *= 1274126177u; return h ^ h >> 16; } }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2"))); }
    }
}
