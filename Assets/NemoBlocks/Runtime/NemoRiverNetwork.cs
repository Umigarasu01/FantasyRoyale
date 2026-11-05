using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public class NemoRiverNetwork : MonoBehaviour
{
    public NemoRiverDesign Design;public NemoBridge[] Bridges;public BoxCollider2D[] WaterBodies;public int CatalogueVersion=3;
}
public partial class NemoMap
{
    public bool UseDesignedRivers;public NemoRiverNetwork RiverNetwork;bool DesignedPlacing;
    public NemoBridge[] AllBridges=>UseDesignedRivers&&RiverNetwork!=null?RiverNetwork.Bridges:new[]{Bridge};
    void RiverRibbon(NemoRiverBlock block,Texture2D texture,float extra,int order,string label)
    {
        var verts=new List<Vector3>();var tris=new List<int>();for(int i=0;i<block.points.Length;i++){var point=block.points[i];var tangent=(block.points[Mathf.Min(i+1,block.points.Length-1)]-block.points[Mathf.Max(0,i-1)]).normalized;if(i==0)tangent=-block.inlet.direction;if(i==block.points.Length-1)tangent=block.outlet.direction;var n=new Vector2(-tangent.y,tangent.x);float ripple=i==0||i==block.points.Length-1?0:.06f*Mathf.Sin(point.y*1.8f+point.x*.9f);float half=block.widths[i]/2+extra+ripple;verts.Add(point+n*half);verts.Add(point-n*half);if(i>0){int k=i*2;tris.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}}
        MeshObject(label+" / "+block.id,verts,tris,texture,order);
    }
    void DesignedRiver(NemoRiverDesign design)
    {
        var root=Group("River blocks | independent matched outlets | v3");RiverNetwork=root.AddComponent<NemoRiverNetwork>();RiverNetwork.Design=design;
        var water=Resources.Load<Texture2D>("NemoRiverWater");var shallow=Resources.Load<Texture2D>("NemoRiverShallow");if(water==null||shallow==null)throw new Exception("Native river texture resources missing");
        var colliders=new List<BoxCollider2D>();var anchors=new[]{design.mainBridge,design.millBridge};
        foreach(var block in design.blocks){
            RiverRibbon(block,Library.Dirt,.40f,-19200,"native earth bank");RiverRibbon(block,shallow,0,-17490,"native shallow shoreline");RiverRibbon(block,water,-.30f,-17480,"native deep water");
            for(int i=1;i<block.points.Length;i++){
                var a=block.points[i-1];var b=block.points[i];int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.3f));for(int k=0;k<n;k++){float t0=(float)k/n,t1=(float)(k+1)/n;var p=Vector2.Lerp(a,b,t0);var q=Vector2.Lerp(a,b,t1);var c=(p+q)/2;float width=Mathf.Lerp(block.widths[i-1],block.widths[i],(t0+t1)/2);if(anchors.Any(x=>Mathf.Abs(c.x-x.x)<.05f&&Mathf.Abs(c.y-x.y)<.82f))continue;
                    var go=Group("water / "+block.template,root.transform);go.transform.position=c;var dir=(q-p).normalized;go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg-90);var box=go.AddComponent<BoxCollider2D>();box.size=new Vector2(width,Vector2.Distance(p,q)+.03f);colliders.Add(box);var ext=new Vector2(Mathf.Abs(dir.y)*width+Mathf.Abs(dir.x)*box.size.y,Mathf.Abs(dir.x)*width+Mathf.Abs(dir.y)*box.size.y)*.5f;bodyReserved.Add(new Rect(c-ext,ext*2));
                }
            }
        }
        var bridges=new List<NemoBridge>();var original=Library.River.First(x=>x.kind=="RiverBridge");foreach(var anchor in anchors){var group=Group("Native bridge + both bank approaches",root.transform);var bridge=group.AddComponent<NemoBridge>();var origin=anchor-new Vector2(7.53125f,4.734375f);var visible=new List<GameObject>();foreach(var v in original.visuals)visible.Add(Visual(group.transform,v,origin,anchor.y,v.name.Contains("floor")).gameObject);bridge.Visuals=visible.ToArray();bridge.PresentBodies=original.present.Select(b=>Body(group.transform,b,origin,"native bridge rail")).ToArray();float width=anchor==design.mainBridge?3:1.65f;bridge.GapBodies=new[]{Body(group.transform,new NativeBody{x=anchor.x-width/2,y=anchor.y-.82f,width=width,height=1.64f},Vector2.zero,"removed bridge water",false)};bridge.West=anchor+Vector2.left*2.7f;bridge.East=anchor+Vector2.right*2.7f;bridges.Add(bridge);var module=design.blocks.First(b=>b.template==(bridges.Count==1?"bridge-and-two-bank-approaches":"watermill-sluice-and-footbridge"));Road(module.crossingRoad,"two bank bridge approach",1.1f);}
        RiverNetwork.Bridges=bridges.ToArray();RiverNetwork.WaterBodies=colliders.ToArray();Bridge=bridges[0];
        // The whole crossing, including its bank entrances, is reserved before placing any facilities.
        foreach(var anchor in anchors)visualReserved.Add(new Rect(anchor.x-5.5f,anchor.y-1.5f,11,3));
        foreach(var block in design.blocks){var points=block.points;for(int k=1;k<points.Length;k++){var p=points[k];float w=block.widths[k]/2+.25f;visualReserved.Add(new Rect(p.x-w,p.y-w,w*2,w*2));}}
    }
    NativePrototype MirroredMill(NativePrototype source,bool mirror)
    {
        if(!mirror)return source;return new NativePrototype{id=source.id,bounds=new[]{-source.Bounds.xMax,source.Bounds.yMin,source.Bounds.width,source.Bounds.height},visuals=source.visuals.Select(v=>new NativeVisual{path=v.path,name=v.name,x=-v.x,y=v.y,pivotX=v.pivotX,pivotY=v.pivotY,rect=v.rect,color=v.color,flipX=!v.flipX,flipY=v.flipY,order=v.order,sprite=v.sprite}).ToArray(),bodies=source.bodies.Select(b=>new NativeBody{x=-b.x-b.width,y=b.y,width=b.width,height=b.height}).ToArray()};
    }
    void PlaceDesignedMill(NemoRiverDesign design)
    {
        var p=Plan.parcels[design.millParcel];var proto=MirroredMill(Library.Get("watermill"),design.millMirror);var foot=design.millFoot;var group=Group("riverside-village / watermill attached to continuous river");foreach(var v in proto.visuals)Visual(group.transform,v,foot,foot.y,v.name.Contains("sluice")||v.name=="shadow"||v.name=="contact");foreach(var b in proto.bodies)if(b.height<7)Body(group.transform,b,foot,"native mill body");var rec=new NemoPlaced{parcel=p.id,prototype=proto.id,purpose=p.kind,foot=foot,visual=At(proto.Bounds,foot),critical=true};placed.Add(rec);visualReserved.Add(rec.visual);
    }
    void GenerateDesigned(int seed)
    {
        if(World!=null){if(Application.isPlaying)World.SetActive(false);DestroyImmediate(World);}foreach(var o in Transient)if(o!=null)DestroyImmediate(o);Transient.Clear();placed.Clear();skipped.Clear();Approaches=new NemoApproach[0];visualReserved.Clear();bodyReserved.Clear();mats=new Dictionary<Texture2D,Material>();Seed=seed;rng=new System.Random(seed);Plan=NemoLayout.Generate(seed);World=new GameObject("Generated map | river blocks v3 | seed "+seed+" | Standard");World.transform.SetParent(transform);
        MeshObject("native grass beyond walking boundary",new List<Vector3>{new Vector3(-32,-32),new Vector3(176,-32),new Vector3(176,140),new Vector3(-32,140)},new List<int>{0,2,1,0,3,2},Library.Grass[0],-20001);Floor();var design=NemoRiverPlan.Create(Plan);var errors=NemoRiverPlan.Validate(design);if(errors.Length>0)throw new Exception(string.Join(";",errors));DesignedRiver(design);PlaceDesignedMill(design);
        var river=Plan.parcels.First(p=>p.kind=="river-crossing");river.hub=design.mainBridge+Vector2.right*((seed&1)==0?-3.8f:3.8f);
        DesignedPlacing=true;try{foreach(var p in Plan.parcels)Recipe(p);}finally{DesignedPlacing=false;}
        var roads=new NemoRoadPlanner(Plan,bodyReserved,placed.Where(p=>p.critical).ToArray());roads.Build();Plan.roadOutlets=roads.Outlets;Approaches=roads.Approaches(placed.Where(p=>p.critical).ToArray()).Concat(roads.WorkApproaches(placed.ToArray())).Concat(new[]{roads.BankApproach(river.id,Bridge.West,"main bridge west bank"),roads.BankApproach(river.id,Bridge.East,"main bridge east bank"),roads.BankApproach(design.millParcel,RiverNetwork.Bridges[1].West,"mill bridge west bank"),roads.BankApproach(design.millParcel,RiverNetwork.Bridges[1].East,"mill bridge east bank")}).ToArray();
        foreach(var link in Plan.links)Road(link.points,"designed local street / "+link.a+" to "+link.b,roads.Width(link));foreach(var approach in Approaches)Road(approach.points,"entrance lane / "+approach.prototype,Plan.parcels[approach.parcel].large?1.2f:.85f);
        foreach(var p in Plan.parcels.Where(p=>!p.land))Ellipse(p.hub,.7f,.55f,Library.Dirt,-18550,"worn local junction");Forest();ExtendExteriorDesign();Placements=placed.ToArray();Skipped=skipped.ToArray();ApplyVisualFinish();Design=NemoBlockDesignFreeze.Capture(this);Boundary=NemoBoundary.Build(this);Overlay=Group("Diagnostic parcel and outlet view");Overlay.SetActive(false);foreach(var p in Plan.parcels)DrawLine(new[]{p.rect.min,new Vector2(p.rect.xMax,p.rect.yMin),p.rect.max,new Vector2(p.rect.xMin,p.rect.yMax),p.rect.min},p.large?Color.yellow:new Color(.2f,.75f,1,1));StabilizeSpriteDepth();Physics2D.SyncTransforms();
    }
}
