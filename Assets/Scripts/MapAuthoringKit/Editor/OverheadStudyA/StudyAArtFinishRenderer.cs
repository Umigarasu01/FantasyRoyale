using System;
using System.Collections.Generic;
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
    /// <summary>7原本の検査結果と、形状を変えない描画追加だけを記録する。</summary>
    [Serializable]
    public sealed class StudyAArtFinishReport
    {
        public bool Enabled, EngineVisuallyReviewed;
        public int ArtFinishVersion = 1, DecorativeColliderCount, BeamCount, RippleCount, BankPixelCount, RipplePixelCount;
        public int[] BankDirectionPixelCounts;
        public string Recipe, InputFingerprint, SourceManifestSha256, SourceReadmeSha256, SourceValidationSha256;
        public string[] SourcePaths, SourcePngSha256, SourceMetaSha256;
        public Vector2Int[] SourceNativeSizes;
        public Vector2[] SourcePixelPivots, BeamAnchors, ExistingPostAnchors, RippleAnchors;
        public string[] RippleSourceNames, HiddenPreviousBankRenderers;
        public string BankTexturePath, RippleTexturePath, BeamTexturePath, BankPixelsSha256, RipplePixelsSha256;
        public string VisibleWaterSha256Before, VisibleWaterSha256After;
        public string WaterCollisionSha256Before, WaterCollisionSha256After, FineFreeWalkSha256Before, FineFreeWalkSha256After;
        public Rect[] DeckBoundsBefore, DeckBoundsAfter;
        public string BeamSortContract;
    }

    /// <summary>細い水輪郭へ方向別岸をclipし、native橋桁と少数の波紋だけを追加する。</summary>
    public static class StudyAArtFinishRenderer
    {
        public const int Version = 1, PixelsPerUnit = 64;
        public const string InputRoot = "Assets/Art/Map/OverheadStudyA/Input/BridgeShoreFinishV1";
        private static readonly string[] Platforms =
            { "Standalone", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo Switch", "tvOS" };

        private sealed class Spec
        {
            public string Name, Hash;
            public int Width, Height;
            public Vector2 Pivot;
            public Spec(string name, string hash, int width, int height, int px, int py)
            { Name=name; Hash=hash; Width=width; Height=height; Pivot=new Vector2(px,py); }
            public string Path => InputRoot + "/Assets/" + Name + ".png";
        }
        private sealed class Artwork
        {
            public Spec Spec;
            public Color32[] Pixels;
            public Sprite Sprite;
            public string MetaHash;
        }
        private sealed class WaterPortSeam
        {
            public int Direction, Contour, TangentMin, TangentMax;
            public RectInt LipClip;
        }
        // N/E/S/Wは水に対する岸の位置。各素材は独立した照明を持ち、回転・反転をしない。
        private static readonly Spec[] Specs =
        {
            new Spec("bank_lip_W","4a49f92c570ec0115982b6b3531d1a870dca1cdef7b5caee8c75860fcbcbee11",20,64,13,32),
            new Spec("bank_lip_E","2ad881c665a8ee9090b34059a0f0fcf9105f52903364946188b2a058bc085821",20,64,7,32),
            new Spec("bank_lip_N","58c08a213506ddffc6ffc21ea27e9c93cba2f09ad8ed9f6996ffbd16bc258347",64,20,32,10),
            new Spec("bank_lip_S","6c78b8d1ec39c5360b748d87454cc745ad6e89abee3c67cd9025afb6e7c1e087",64,16,32,9),
            new Spec("bridge_edgebeam_EW","cd24ab79d71aa12a5a116ad343b308112d695b3ac9fdc880c6573bb65f152328",256,24,128,0),
            new Spec("water_ripple_48","03d603a08bfb589f37fe55766b8fd81dc2a57660397ba3dd87de9d46e5ff54f1",48,16,24,8),
            new Spec("water_ripple_32","ff941081709985143b703f0f6dcc6871aea4e92d94a533f35c280fc402895b63",32,12,16,6)
        };
        [Serializable] private sealed class Manifest
        {
            public int asset_count = 0, ppu = 0;
            public string filter_mode = null, texture_format = null, compression = null, mesh_type = null;
            public bool mipmaps = true, sRGB = false;
            public ManifestAsset[] assets = null;
        }
        [Serializable] private sealed class ManifestAsset
        {
            public string file = null, sha256 = null;
            public int[] size_px = null;
            public float[] pivot_px_bottom_left = null;
        }

        /// <summary>新しい専用Inputのmetaだけを設定する。原本PNG bytesと既存地形Assetを変更しない。</summary>
        public static void PrepareApprovedInputs()
        {
            var art = ReadSources(false);
            // 全7件とmetaを最初の設定変更より前に検査する。
            foreach(var a in art) { ProjectPath(a.Spec.Path); ProjectPath(a.Spec.Path+".meta"); }
            foreach(var a in art)
            {
                AssetDatabase.ImportAsset(a.Spec.Path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureImport(a.Spec.Path,a.Spec.Width,a.Spec.Height,a.Spec.Pivot);
                if(Hash(File.ReadAllBytes(ProjectPath(a.Spec.Path)))!=a.Spec.Hash)throw new IOException("Art finish source PNG changed: "+a.Spec.Name);
            }
            RequireApprovedInputs();
        }

        /// <summary>Builderの出力前ガード。7枚の原本・native導入設定が成立しないと描画を始めない。</summary>
        public static void RequireApprovedInputs() { ReadSources(true); }

        public static StudyAArtFinishReport Apply(StudyABridgeRiverFinePlan fine, Transform decorations, Transform obstacles, Material material, string outputRoot)
        {
            RequireFine(fine); RequireUnitParent(decorations); RequireUnitParent(obstacles);
            if(material==null)throw new ArgumentNullException(nameof(material));
            var art=ReadSources(true);
            var bankPath=outputRoot+"/art-finish-bank-lips.png";
            var ripplePath=outputRoot+"/art-finish-water-ripples.png";
            var beamPath=outputRoot+"/bridge-edgebeam.png";
            RequireFresh(bankPath); RequireFresh(ripplePath); RequireFresh(beamPath);
            if(!Directory.Exists(ProjectPath(outputRoot)))throw new DirectoryNotFoundException("Builder must create the fresh ArtFinish folder first.");
            if(decorations.Find("ArtFinish_Shore_NoCollision")!=null||decorations.Find("ArtFinish_Ripples_NoCollision")!=null||
                obstacles.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.name.StartsWith("ArtFinish_BridgeBeam_",StringComparison.Ordinal)))
                throw new InvalidOperationException("Art finish may be applied once to a fresh run.");
            var oldBanks=decorations.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r=>r.name=="BridgeRiver_Shore_NoCollision").ToArray();
            foreach(var renderer in oldBanks)
                if(renderer.GetComponentsInChildren<Collider2D>(true).Length!=0||renderer.sortingLayerName!="MapGround"||
                    renderer.sharedMaterial!=material)throw new InvalidOperationException("Custom old shore renderer is preserved by refusing replacement.");

            var waterBefore=Hash(fine.VisibleWaterMask); var collisionBefore=Hash(fine.WaterCollisionMask); var freeBefore=Hash(fine.FineFreeWalkMask);
            var decksBefore=fine.DeckBounds.ToArray();
            var bankPixels=ComposeBanks(fine,art,out var directionCounts);
            var ripplePixels=ComposeRipples(fine,bankPixels,art,out var rippleAnchors,out var rippleNames);
            var beam=fine.BridgeStructureVersion == 0 ? WriteNativeCopy(art[4],beamPath) : null;
            var banks=WriteSprite(fine.Width,fine.Height,bankPixels,bankPath,Vector2.zero);
            var ripples=WriteSprite(fine.Width,fine.Height,ripplePixels,ripplePath,Vector2.zero);
            var generated=new List<GameObject>();
            generated.Add(AddSprite(decorations,"ArtFinish_Shore_NoCollision",banks,(Vector2)fine.Origin,material,"MapGround",8));
            generated.Add(AddSprite(decorations,"ArtFinish_Ripples_NoCollision",ripples,(Vector2)fine.Origin,material,"MapGround",6));
            var anchors=new List<Vector2>();
            for(var i=0;i<fine.DeckBounds.Length && fine.BridgeStructureVersion == 0;i++)
            {
                var deck=fine.DeckBounds[i];
                var origin=new Vector2(deck.center.x,deck.yMin);
                // 北側の+1px物理床はそのまま。桁は原本の4x3デッキの両端に置く。
                var north=origin+Vector2.up*3f;
                anchors.Add(origin); anchors.Add(north);
                generated.Add(AddSprite(obstacles,"ArtFinish_BridgeBeam_South_"+i+"_NoCollision",beam,origin,material,"WorldObjects",0));
                generated.Add(AddSprite(obstacles,"ArtFinish_BridgeBeam_North_"+i+"_NoCollision",beam,north,material,"WorldObjects",0));
            }
            foreach(var renderer in oldBanks)renderer.enabled=false; // run-owned粗い岸だけをhide。Fine水/床/patchは変更しない。
            var colliderCount=generated.Sum(go=>go.GetComponentsInChildren<Collider2D>(true).Length);
            if(colliderCount!=0||waterBefore!=Hash(fine.VisibleWaterMask)||collisionBefore!=Hash(fine.WaterCollisionMask)||
                freeBefore!=Hash(fine.FineFreeWalkMask)||!decksBefore.SequenceEqual(fine.DeckBounds))
                throw new InvalidOperationException("Decorative art finish changed geometry or added collision.");
            return new StudyAArtFinishReport
            {
                Enabled=true,EngineVisuallyReviewed=false,ArtFinishVersion=Version,DecorativeColliderCount=colliderCount,
                BeamCount=anchors.Count,RippleCount=rippleAnchors.Length,BankPixelCount=bankPixels.Count(p=>p.a==255),
                RipplePixelCount=ripplePixels.Count(p=>p.a==255),BankDirectionPixelCounts=directionCounts,
                Recipe=fine.BridgeStructureVersion == 1 ? "ArtFinish1 shore/ripple overlays on the separate BridgeStructure1 candidate: native64/binary-alpha direction-specific bank strips sample1:1 along the fine water cardinal contour; nearest contour wins at turns, no rotated/flipped bank, explicit4x1.5u DeckBounds excluded. Declared native water port apertures remain open without end caps. Sparse unchanged native ripple texels stay inside visible water and outside deck/banks. Existing run-owned rough shore hidden. No legacy4x3deck, posts or thin beams are created in BridgeStructure1. This display-only overlay does not mutate the supplied candidate floor/water/free-walk masks or add collision; the candidate's changed floor aperture and independent corner/rail bodies are described in the separate BridgeStructure report." : "Native64/binary-alpha direction-specific bank strips sample1:1 along the existing fine water cardinal contour; nearest contour wins at turns, no rotated/flipped bank, full DeckBounds including north+1px excluded. Declared water port cells at each block outer boundary remain open: no end-cap stamp and native lip-depth clipping only across that port aperture. Two upright native4u beams at deck bottom and bottom+3u. Sparse unchanged ripple texels strictly inside visible water and outside deck/banks. Existing run-owned rough shore hidden; fine water, deck, posts, collision and route geometry unchanged.",
                InputFingerprint=Hash(Encoding.UTF8.GetBytes(string.Join("\n",art.Select(a=>a.Spec.Path+"="+a.Spec.Hash+";meta="+a.MetaHash)))),
                SourceManifestSha256=Hash(File.ReadAllBytes(ProjectPath(InputRoot+"/manifest.json"))),
                SourceReadmeSha256=Hash(File.ReadAllBytes(ProjectPath(InputRoot+"/README.txt"))),
                SourceValidationSha256=Hash(File.ReadAllBytes(ProjectPath(InputRoot+"/validation-report.json"))),
                SourcePaths=art.Select(a=>a.Spec.Path).ToArray(),SourcePngSha256=art.Select(a=>a.Spec.Hash).ToArray(),
                SourceMetaSha256=art.Select(a=>a.MetaHash).ToArray(),
                SourceNativeSizes=art.Select(a=>new Vector2Int(a.Spec.Width,a.Spec.Height)).ToArray(),
                SourcePixelPivots=art.Select(a=>a.Spec.Pivot).ToArray(),BeamAnchors=anchors.ToArray(),
                ExistingPostAnchors=obstacles.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r=>r.name.Contains("bridge_post")&&!r.name.StartsWith("ArtFinish_",StringComparison.Ordinal)).Select(r=>(Vector2)r.transform.position).ToArray(),
                RippleAnchors=rippleAnchors,RippleSourceNames=rippleNames,
                HiddenPreviousBankRenderers=oldBanks.Select(r=>r.name).ToArray(),
                BankTexturePath=bankPath,RippleTexturePath=ripplePath,BeamTexturePath=beam == null ? "" : beamPath,
                BankPixelsSha256=Hash(RgbaBytes(bankPixels)),RipplePixelsSha256=Hash(RgbaBytes(ripplePixels)),
                VisibleWaterSha256Before=waterBefore,VisibleWaterSha256After=Hash(fine.VisibleWaterMask),
                WaterCollisionSha256Before=collisionBefore,WaterCollisionSha256After=Hash(fine.WaterCollisionMask),
                FineFreeWalkSha256Before=freeBefore,FineFreeWalkSha256After=Hash(fine.FineFreeWalkMask),
                DeckBoundsBefore=decksBefore,DeckBoundsAfter=fine.DeckBounds.ToArray(),
                BeamSortContract=fine.BridgeStructureVersion == 1 ? "BridgeStructure1 has zero legacy beam renderers. Its separate native Bridge01 back/front assemblies use WorldObjects/order0 independent SortingGroups at source post-foot Y; all three graphic centers and shared native160x96 pivots remain unchanged. See BridgeStructure.LayerSorting and LayerSortAnchors for the actual candidate contract." : "WorldObjects/order0/Pivot, independent beam per edge. Exact graphic anchors unchanged. Builder ResolveFineDepthTies orders ArtFinish_BridgeBeam_ last for a positive invisible north sort-anchor bias; existing posts overpaint joints while actor depth remains world-Y."
            };
        }

        private static Color32[] ComposeBanks(StudyABridgeRiverFinePlan fine,Artwork[] art,out int[] counts)
        {
            var output=new Color32[fine.Width*fine.Height]; var scores=Enumerable.Repeat(int.MaxValue,output.Length).ToArray();
            var selected=new byte[output.Length]; counts=new int[4];var seams=WaterPortSeams(fine,art);
            // 一行/列ずつnative輪郭へ沿わせる。巨大stripを毎頂点へ重ねず、角では最短距離の1方向だけを採用。
            for(var y=0;y<fine.Height;y++)for(var x=0;x<fine.Width;x++)
            {
                if(!Water(fine,x,y))continue;
                if(x>0&&!Water(fine,x-1,y))StampBank(fine,art[0],output,scores,selected,seams,0,x,y,true);
                if(x+1<fine.Width&&!Water(fine,x+1,y))StampBank(fine,art[1],output,scores,selected,seams,1,x+1,y,true);
                if(y+1<fine.Height&&!Water(fine,x,y+1))StampBank(fine,art[2],output,scores,selected,seams,2,y+1,x,false);
                if(y>0&&!Water(fine,x,y-1))StampBank(fine,art[3],output,scores,selected,seams,3,y,x,false);
            }
            for(var i=0;i<output.Length;i++)if(output[i].a==255)counts[selected[i]]++;
            return output;
        }

        private static void StampBank(StudyABridgeRiverFinePlan fine,Artwork art,Color32[] output,int[] scores,byte[] selected,
            WaterPortSeam[] seams,int direction,int contour,int tangent,bool vertical)
        {
            if(seams.Any(s=>s.Direction==direction&&s.Contour==contour&&tangent>=s.TangentMin&&tangent<s.TangentMax))return;
            var spec=art.Spec; var pivot=(int)(vertical?spec.Pivot.x:spec.Pivot.y);
            var length=vertical?spec.Width:spec.Height;
            var phase=Mod(tangent+(vertical?fine.Origin.y:fine.Origin.x)*PixelsPerUnit,64);
            for(var normal=0;normal<length;normal++)
            {
                var x=vertical?contour+normal-pivot:tangent; var y=vertical?tangent:contour+normal-pivot;
                if(x<0||y<0||x>=fine.Width||y>=fine.Height||InDeck(fine,x,y)||
                    seams.Any(s=>s.LipClip.Contains(new Vector2Int(x,y))))continue;
                var color=art.Pixels[vertical?phase*spec.Width+normal:normal*spec.Width+phase];
                if(color.a==0)continue;
                // 土側と水側の向きが現在の細輪郭に合わないtexelをclipする。
                var waterSide=direction==0?normal>=pivot:direction==1?normal<pivot:direction==2?normal<pivot:normal>=pivot;
                if(Water(fine,x,y)!=waterSide)continue;
                var score=Math.Abs(normal-pivot)*4+direction;
                var index=y*fine.Width+x;
                if(score>=scores[index])continue;
                output[index]=color;scores[index]=score;selected[index]=(byte)direction;
            }
        }

        private static WaterPortSeam[] WaterPortSeams(StudyABridgeRiverFinePlan fine,Artwork[] art)
        {
            var seams=new List<WaterPortSeam>();
            foreach(var block in fine.Assembly.Blocks)
                foreach(var port in block.Definition.Ports.Where(p=>p.Terrain==StudyATerrain.Water))
                {
                    var direction=port.Direction==StudyAPortDirection.West?0:port.Direction==StudyAPortDirection.East?1:
                        port.Direction==StudyAPortDirection.North?2:port.Direction==StudyAPortDirection.South?3:
                        throw new InvalidDataException("Water port direction is invalid.");
                    var vertical=direction<2;var spec=art[direction].Spec;
                    var pivot=(int)(vertical?spec.Pivot.x:spec.Pivot.y);var depth=vertical?spec.Width:spec.Height;
                    var bounds=block.Bounds;
                    var worldContour=direction==0?bounds.xMin:direction==1?bounds.xMax:direction==2?bounds.yMax:bounds.yMin;
                    var contour=(worldContour-(vertical?fine.Origin.x:fine.Origin.y))*PixelsPerUnit;
                    foreach(var localCell in port.Cells)
                    {
                        if(!block.Definition.Bounds.Contains(localCell)||block.Definition.Bounds.Contains(localCell+port.Offset))
                            throw new InvalidDataException("Water port must lie on its declared block outer boundary.");
                        var cell=localCell+block.Origin;
                        var tangent=((vertical?cell.y:cell.x)-(vertical?fine.Origin.y:fine.Origin.x))*PixelsPerUnit;
                        seams.Add(new WaterPortSeam
                        {
                            Direction=direction,Contour=contour,TangentMin=tangent,TangentMax=tangent+PixelsPerUnit,
                            LipClip=vertical?new RectInt(contour-pivot,tangent,depth,PixelsPerUnit):
                                new RectInt(tangent,contour-pivot,PixelsPerUnit,depth)
                        });
                    }
                }
            return seams.ToArray();
        }

        private static Color32[] ComposeRipples(StudyABridgeRiverFinePlan fine,Color32[] banks,Artwork[] art,out Vector2[] anchors,out string[] names)
        {
            var output=new Color32[fine.Width*fine.Height];var accepted=new List<Vector2>();var acceptedNames=new List<string>();
            foreach(var deck in fine.DeckBounds)
            {
                var rows=new[]{deck.yMin-.5f,deck.yMin-1.75f,deck.yMin+3.5f,deck.yMin+4.75f};
                for(var n=0;n<rows.Length;n++)
                {
                    var source=art[5+n%2];var y=Mathf.RoundToInt((rows[n]-fine.Origin.y)*PixelsPerUnit);
                    if(y<0||y>=fine.Height)continue;
                    var x0=Mathf.Max(0,Mathf.FloorToInt((deck.center.x-2-fine.Origin.x)*PixelsPerUnit));
                    var x1=Mathf.Min(fine.Width,Mathf.CeilToInt((deck.center.x+2-fine.Origin.x)*PixelsPerUnit));
                    var run=Enumerable.Range(x0,Math.Max(0,x1-x0)).Where(x=>Water(fine,x,y)).ToArray();
                    if(run.Length==0)continue;
                    var west=n%2==0;var anchorX=west?run.Min()+(int)source.Spec.Pivot.x+6:run.Max()+1-(source.Spec.Width-(int)source.Spec.Pivot.x)-6;
                    for(var step=0;step<3;step++)
                    {
                        var candidate=anchorX+(west?step*8:-step*8);
                        if(!CanPlaceRipple(fine,banks,output,source,candidate,y))continue;
                        StampRipple(output,fine.Width,source,candidate,y);
                        accepted.Add(new Vector2(fine.Origin.x+candidate/(float)PixelsPerUnit,fine.Origin.y+y/(float)PixelsPerUnit));
                        acceptedNames.Add(source.Spec.Name);break;
                    }
                }
            }
            anchors=accepted.ToArray();names=acceptedNames.ToArray();return output;
        }
        private static bool CanPlaceRipple(StudyABridgeRiverFinePlan fine,Color32[] banks,Color32[] output,Artwork source,int ax,int ay)
        {
            var left=ax-(int)source.Spec.Pivot.x;var bottom=ay-(int)source.Spec.Pivot.y;
            for(var y=0;y<source.Spec.Height;y++)for(var x=0;x<source.Spec.Width;x++)
            {
                if(source.Pixels[y*source.Spec.Width+x].a==0)continue;
                var px=left+x;var py=bottom+y;
                if(!Water(fine,px,py)||InDeck(fine,px,py))return false;
                var index=py*fine.Width+px;if(banks[index].a!=0||output[index].a!=0)return false;
            }
            return true;
        }
        private static void StampRipple(Color32[] output,int width,Artwork source,int ax,int ay)
        {
            var left=ax-(int)source.Spec.Pivot.x;var bottom=ay-(int)source.Spec.Pivot.y;
            for(var y=0;y<source.Spec.Height;y++)for(var x=0;x<source.Spec.Width;x++)
                if(source.Pixels[y*source.Spec.Width+x].a==255)output[(bottom+y)*width+left+x]=source.Pixels[y*source.Spec.Width+x];
        }
        private static bool Water(StudyABridgeRiverFinePlan fine,int x,int y)=>x>=0&&y>=0&&x<fine.Width&&y<fine.Height&&fine.VisibleWaterMask[y*fine.Width+x]==255;
        private static bool InDeck(StudyABridgeRiverFinePlan fine,int x,int y)
        {
            var point=new Vector2(fine.Origin.x+(x+.5f)/PixelsPerUnit,fine.Origin.y+(y+.5f)/PixelsPerUnit);
            return fine.DeckBounds.Any(r=>r.Contains(point));
        }
        private static int Mod(int value,int period)=>((value%period)+period)%period;

        private static Artwork[] ReadSources(bool requireImport)
        {
            var manifestBytes=File.ReadAllBytes(ProjectPath(InputRoot+"/manifest.json"));
            var manifest=JsonUtility.FromJson<Manifest>(Encoding.UTF8.GetString(manifestBytes).TrimStart('\uFEFF'));
            if(manifest==null||manifest.asset_count!=7||manifest.ppu!=64||manifest.filter_mode!="Point"||manifest.texture_format!="RGBA32"||
                manifest.compression!="None"||manifest.mipmaps||!manifest.sRGB||manifest.mesh_type!="FullRect"||manifest.assets==null||manifest.assets.Length!=7)
                throw new InvalidDataException("Bridge-shore native7 manifest differs from the approved package.");
            File.ReadAllBytes(ProjectPath(InputRoot+"/README.txt"));File.ReadAllBytes(ProjectPath(InputRoot+"/validation-report.json"));
            var result=new List<Artwork>();
            foreach(var spec in Specs)
            {
                var entry=manifest.assets.SingleOrDefault(a=>a!=null&&a.file=="Assets/"+spec.Name+".png");
                if(entry==null||entry.sha256!=spec.Hash||entry.size_px==null||!entry.size_px.SequenceEqual(new[]{spec.Width,spec.Height})||
                    entry.pivot_px_bottom_left==null||entry.pivot_px_bottom_left.Length!=2||
                    entry.pivot_px_bottom_left[0]!=spec.Pivot.x||entry.pivot_px_bottom_left[1]!=spec.Pivot.y)
                    throw new InvalidDataException("Source dimensions/pivot/hash differ: "+spec.Name);
                var bytes=File.ReadAllBytes(ProjectPath(spec.Path));
                if(Hash(bytes)!=spec.Hash)throw new InvalidDataException("Source PNG SHA256 differs: "+spec.Name);
                var pixels=ReadPixels(bytes,spec.Width,spec.Height);
                if(pixels.Any(p=>(p.a!=0&&p.a!=255)||(p.a==0&&(p.r!=0||p.g!=0||p.b!=0))))
                    throw new InvalidDataException("Source requires binary alpha and transparent RGB zero: "+spec.Name);
                if(spec.Name.StartsWith("bank_lip_",StringComparison.Ordinal))RequireRepeat(spec,pixels);
                var a=new Artwork{Spec=spec,Pixels=pixels};
                if(requireImport)
                {
                    RequireImport(spec.Path,spec.Width,spec.Height,spec.Pivot);
                    a.Sprite=AssetDatabase.LoadAssetAtPath<Sprite>(spec.Path);
                    a.MetaHash=Hash(File.ReadAllBytes(ProjectPath(spec.Path+".meta")));
                }
                result.Add(a);
            }
            return result.ToArray();
        }
        private static void RequireRepeat(Spec spec,Color32[] pixels)
        {
            var vertical=spec.Width==20;
            for(var i=0;i<(vertical?spec.Width:spec.Height);i++)
                for(var margin=0;margin<2;margin++)
                {
                    var first=vertical?pixels[margin*spec.Width+i]:pixels[i*spec.Width+margin];
                    var last=vertical?pixels[(spec.Height-1-margin)*spec.Width+i]:pixels[i*spec.Width+spec.Width-1-margin];
                    if(!first.Equals(last))throw new InvalidDataException("Shore repeat endpoints differ: "+spec.Name);
                }
        }
        private static Color32[] ReadPixels(byte[] bytes,int width,int height)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!texture.LoadImage(bytes,false)||texture.width!=width||texture.height!=height)throw new InvalidDataException("Native PNG dimensions differ.");
                return texture.GetPixels32();
            }
            finally{Object.DestroyImmediate(texture);}
        }
        private static Sprite WriteNativeCopy(Artwork source,string path)
        {
            RequireFresh(path);File.WriteAllBytes(ProjectPath(path),File.ReadAllBytes(ProjectPath(source.Spec.Path)));
            ConfigureImport(path,source.Spec.Width,source.Spec.Height,source.Spec.Pivot);
            RequireImport(path,source.Spec.Width,source.Spec.Height,source.Spec.Pivot);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Sprite WriteSprite(int width,int height,Color32[] pixels,string path,Vector2 pixelPivot)
        {
            RequireFresh(path);var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try{texture.SetPixels32(pixels);texture.Apply(false,false);File.WriteAllBytes(ProjectPath(path),texture.EncodeToPNG());}
            finally{Object.DestroyImmediate(texture);}
            ConfigureImport(path,width,height,pixelPivot);RequireImport(path,width,height,pixelPivot);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void ConfigureImport(string path,int width,int height,Vector2 pixelPivot)
        {
            ProjectPath(path);ProjectPath(path+".meta");
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidDataException("TextureImporter missing: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=PixelsPerUnit;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.crunchedCompression=false;
            importer.alphaIsTransparency=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=4096;importer.isReadable=false;
            foreach(var platform in Platforms)if(importer.GetPlatformTextureSettings(platform).overridden)importer.ClearPlatformTextureSettings(platform);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(pixelPivot.x/width,pixelPivot.y/height);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(settings);
            var defaults=importer.GetDefaultPlatformTextureSettings();defaults.format=TextureImporterFormat.RGBA32;
            defaults.textureCompression=TextureImporterCompression.Uncompressed;defaults.maxTextureSize=4096;importer.SetPlatformTextureSettings(defaults);
            importer.SaveAndReimport();
        }
        private static void RequireImport(string path,int width,int height,Vector2 pixelPivot)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var rect=new Rect(0,0,width,height);
            if(importer==null||sprite==null||sprite.packed||sprite.rect!=rect||sprite.textureRect!=rect||
                sprite.pixelsPerUnit!=64||sprite.pivot!=pixelPivot||sprite.GetPhysicsShapeCount()!=0||
                sprite.texture==null||sprite.texture.width!=width||sprite.texture.height!=height||sprite.texture.format!=TextureFormat.RGBA32||
                importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=SpriteImportMode.Single||
                importer.spritePixelsPerUnit!=64||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||
                importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.crunchedCompression||
                importer.alphaSource!=TextureImporterAlphaSource.FromInput||importer.alphaIsTransparency||!importer.sRGBTexture||
                importer.wrapMode!=TextureWrapMode.Clamp||importer.npotScale!=TextureImporterNPOTScale.None||
                Platforms.Any(p=>importer.GetPlatformTextureSettings(p).overridden))
                throw new InvalidDataException("Native64/Point/RGBA32/noPhysics import contract failed: "+path);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            if(settings.spriteMeshType!=SpriteMeshType.FullRect||settings.spriteAlignment!=(int)SpriteAlignment.Custom||
                settings.spriteGenerateFallbackPhysicsShape||settings.spritePivot!=new Vector2(pixelPivot.x/width,pixelPivot.y/height))
                throw new InvalidDataException("FullRect/custom native pivot contract failed: "+path);
        }
        private static GameObject AddSprite(Transform parent,string name,Sprite sprite,Vector2 anchor,Material material,string layer,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=new Vector3(anchor.x,anchor.y,0);
            go.transform.localScale=Vector3.one;go.transform.localRotation=Quaternion.identity;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;
            renderer.sortingLayerName=layer;renderer.sortingOrder=order;renderer.spriteSortPoint=SpriteSortPoint.Pivot;
            renderer.flipX=false;renderer.flipY=false;renderer.color=Color.white;return go;
        }
        private static void RequireFine(StudyABridgeRiverFinePlan fine)
        {
            if(fine==null||fine.Assembly==null||fine.PixelsPerUnit!=64||fine.Width<=0||fine.Height<=0||fine.Width>4096||fine.Height>4096||
                fine.VisibleWaterMask==null||fine.WaterCollisionMask==null||fine.FineFreeWalkMask==null||
                fine.VisibleWaterMask.Length!=checked(fine.Width*fine.Height)||fine.WaterCollisionMask.Length!=fine.VisibleWaterMask.Length||
                fine.FineFreeWalkMask.Length!=fine.VisibleWaterMask.Length||fine.DeckBounds==null||fine.DeckBounds.Length==0)
                throw new InvalidDataException("Existing native64 fine water/deck plan is required.");
            if(fine.VisibleWaterMask.Any(v=>v!=0&&v!=255))throw new InvalidDataException("Visible fine water must have binary alpha.");
            foreach(var deck in fine.DeckBounds)
                if(deck.width!=4f || (fine.BridgeStructureVersion == 1 ? deck.height != 1.5f : (deck.height!=3f&&deck.height!=3f+1f/64f)))
                    throw new InvalidDataException("Deck bounds differ from the explicit bridge snapshot.");
        }
        private static void RequireUnitParent(Transform parent)
        {
            if(parent==null||parent.rotation!=Quaternion.identity||parent.lossyScale!=Vector3.one)
                throw new InvalidOperationException("Art finish parent requires upright unit transform.");
        }
        private static void RequireFresh(string relative)
        {
            if(!relative.StartsWith(StudyABlockSceneBuilder.ArtRoot+"/",StringComparison.Ordinal)||
                !relative.Contains("/ArtFinish/"))throw new IOException("Fresh run-owned ArtFinish output is required.");
            var path=ProjectPath(relative);var meta=ProjectPath(relative+".meta");
            if(File.Exists(path)||File.Exists(meta))throw new IOException("Art finish refuses existing output: "+relative);
        }
        private static string ProjectPath(string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains("\\")||
                relative.Split('/').Any(s=>s=="."||s==".."))throw new IOException("Safe project relative path required.");
            var root=Directory.GetParent(Application.dataPath).FullName;var result=Path.GetFullPath(Path.Combine(root,relative));
            if(!result.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Path escaped project.");
            for(var path=result;!path.Equals(root,StringComparison.OrdinalIgnoreCase);path=Path.GetDirectoryName(path))
                if((File.Exists(path)||Directory.Exists(path))&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse path is not used.");
            return result;
        }
        private static byte[] RgbaBytes(Color32[] pixels)
        {
            var bytes=new byte[pixels.Length*4];for(var i=0;i<pixels.Length;i++)
            {bytes[i*4]=pixels[i].r;bytes[i*4+1]=pixels[i].g;bytes[i*4+2]=pixels[i].b;bytes[i*4+3]=pixels[i].a;}return bytes;
        }
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
