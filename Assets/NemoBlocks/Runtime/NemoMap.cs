using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random=System.Random;

[Serializable] public class NemoPlaced { public int parcel; public string prototype,purpose; public Vector2 foot; public Rect visual; public bool critical,tree; }
[Serializable] public class NemoSkipped { public int parcel;public string prototype,purpose;public bool critical; }
[Serializable] public class NemoApproach {public int parcel;public string prototype;public Vector2[] points;}
public partial class NemoMap : MonoBehaviour
{
    public NemoBlockDesignFreeze Design;public NemoNativeLibrary Library;public NemoPlan Plan;public GameObject World,Overlay;public NemoBridge Bridge;public NemoBoundary Boundary;
    public NemoPlaced[] Placements;public NemoSkipped[] Skipped;public NemoApproach[] Approaches=new NemoApproach[0];public int Seed;public string Mode="Standard";
    public List<UnityEngine.Object> Transient=new List<UnityEngine.Object>();
    readonly List<NemoPlaced> placed=new List<NemoPlaced>();readonly List<NemoSkipped> skipped=new List<NemoSkipped>();
    readonly List<Rect> visualReserved=new List<Rect>();readonly List<Rect> bodyReserved=new List<Rect>();
    Random rng;Dictionary<Texture2D,Material> mats;
    public static float Distance(Vector2 p,Vector2 a,Vector2 b){var d=b-a;float t=d.sqrMagnitude==0?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+t*d);}
    static Rect At(Rect r,Vector2 p)=>new Rect(r.position+p,r.size);
    static Rect Grow(Rect r,float pad)=>new Rect(r.x-pad,r.y-pad,r.width+2*pad,r.height+2*pad);
    float RouteDistance(Vector2 p){float d=999;foreach(var e in Plan.links)for(int n=1;n<e.points.Length;n++)d=Mathf.Min(d,Distance(p,e.points[n-1],e.points[n]));foreach(var e in Approaches)for(int n=1;n<e.points.Length;n++)d=Mathf.Min(d,Distance(p,e.points[n-1],e.points[n]));return d;}
    bool RouteOverlap(Rect r,float margin){float radius=r.size.magnitude*.5f;return RouteDistance(r.center)<radius+margin;}
    bool Safe(NativePrototype proto,Vector2 foot,Rect area,bool tree)
    {
        var bounds=At(proto.Bounds,foot);if(!area.Contains(bounds.min)||!area.Contains(bounds.max))return false;
        if(!DesignedPlacing&&RouteOverlap(bounds,tree?.9f:1.0f))return false;
        if(visualReserved.Any(v=>Grow(v,.25f).Overlaps(bounds)))return false;
        if(proto.bodies.Any(b=>bodyReserved.Any(v=>Grow(v,.15f).Overlaps(At(b.Rect,foot)))))return false;
        return true;
    }
    GameObject Group(string name,Transform parent=null){var g=new GameObject(name);g.transform.SetParent(parent??World.transform);return g;}
    SpriteRenderer Visual(Transform parent,NativeVisual v,Vector2 origin,float footY,bool ground=false)
    {
        var go=Group(v.name,parent);go.transform.position=new Vector3(origin.x+v.x,origin.y+v.y,0);var r=go.AddComponent<SpriteRenderer>();r.sprite=v.sprite;BindSpriteMaterial(r);r.flipX=v.flipX;r.flipY=v.flipY;
        r.color=v.color!=null&&v.color.Length==4?new Color(v.color[0],v.color[1],v.color[2],v.color[3]):Color.white;r.sortingOrder=ground?-17000+v.order:10000-Mathf.RoundToInt(footY*64)+v.order;return r;
    }
    BoxCollider2D Body(Transform parent,NativeBody b,Vector2 origin,string name,bool active=true)
    {
        var go=Group(name,parent);go.transform.position=origin+b.Rect.center;var c=go.AddComponent<BoxCollider2D>();c.size=b.Rect.size;c.enabled=active;if(active)bodyReserved.Add(At(b.Rect,origin));return c;
    }
    void Put(NativePrototype proto,Vector2 foot,NemoParcel parcel,bool critical=false,bool tree=false)
    {
        var group=Group(parcel.kind+" / "+proto.id);foreach(var v in proto.visuals)Visual(group.transform,v,foot,foot.y,v.name=="shadow"||v.name=="contact");foreach(var b in proto.bodies)Body(group.transform,b,foot,proto.id+" Body");
        var record=new NemoPlaced{parcel=parcel.id,prototype=proto.id,purpose=parcel.kind,foot=foot,visual=At(proto.Bounds,foot),critical=critical,tree=tree};placed.Add(record);if(!tree)visualReserved.Add(record.visual);
        if(!tree&&proto.Bounds.width>=1.7f&&proto.id!="crop-row")Ellipse(foot+new Vector2(0,.25f),Mathf.Min(proto.Bounds.width*.48f+.4f,4.5f),Mathf.Min(proto.Bounds.height*.10f+.7f,1.8f),Library.Dirt,-19000,"facility worn ground");
    }
    bool Place(NemoParcel parcel,string id,Vector2 preferred,bool critical)
    {
        var proto=Library.Get(id);if(proto==null)throw new Exception("Missing native prototype "+id);
        var candidates=new List<Vector2>();for(float y=parcel.rect.yMin+.5f;y<parcel.rect.yMax-.5f;y+=.5f)for(float x=parcel.rect.xMin+.5f;x<parcel.rect.xMax-.5f;x+=.5f)candidates.Add(new Vector2(x,y));
        foreach(var foot in candidates.OrderBy(v=>(v-preferred).sqrMagnitude)){
            if(DesignedPlacing&&critical){var entry=foot+Vector2.down*.65f;if(entry.x<parcel.rect.xMin+.75f||entry.x>parcel.rect.xMax-.75f||entry.y<parcel.rect.yMin+.75f||entry.y>parcel.rect.yMax-.75f||bodyReserved.Any(b=>Grow(b,.36f).Contains(entry))||proto.bodies.Any(b=>Grow(At(b.Rect,foot),.36f).Contains(entry)))continue;}
            if(Safe(proto,foot,parcel.rect,false)){Put(proto,foot,parcel,critical);if(DesignedPlacing&&critical)visualReserved.Add(new Rect(foot.x-.7f,foot.y-1.4f,1.4f,1.2f));return true;}
        }
        skipped.Add(new NemoSkipped{parcel=parcel.id,prototype=id,purpose=parcel.kind,critical=critical});return false;
    }
    void Recipe(NemoParcel p)
    {
        if(p.land||p.quiet)return;
        string[] ids;
        switch(p.kind){
            case "central-inn":ids=new[]{"inn","timber-hut","market-stall","well","tool-bench","loaded-cart","fountain"};break;
            case "fort-ruins":ids=new[]{"watchtower","ruined-arch","ruined-column","ruined-column","fence","crate"};break;
            case "riverside-village":ids=new[]{"watermill","timber-hut","crop-row","well","loaded-cart","log-stack"};break;
            case "large-camp":ids=new[]{"tent","tent","tent","fire","log-stack","fence","crate"};break;
            case "abandoned-yard":ids=new[]{"broken-house","well","fence","stump","bush","flowers","flowers"};break;
            case "mine-workshop":ids=new[]{"mine-mouth","logging-shed","log-stack","loaded-cart","tool-bench","crate"};break;
            case "cargo-layby":ids=new[]{"loaded-cart","crate","tool-bench","bush"};break;
            case "dungeon-mouth":ids=new[]{"cave-mouth","rock-cluster","bush","flowers"};break;
            case "spring":ids=new[]{"natural-spring","rock-cluster","bush","flowers"};break;
            case "shrine":ids=new[]{"wayside-shrine","flowers","bush","rock-cluster"};break;
            case "checkpoint":ids=new[]{"watchtower","fence","fence","crate"};break;
            case "monster-nest":ids=new[]{"monster-nest","bush","stump","rock-cluster"};break;
            case "logging-clearing":ids=new[]{"logging-shed","log-stack","stump","stump","tool-bench"};break;
            case "small-camp":ids=new[]{"tent","fire","crate","fence"};break;
            case "cliff-road":ids=new[]{p.rect.width>=18?"cliff-face":"cliff-face-small","rock-cluster","bush","stump"};break;
            case "orchard":ids=new[]{"crop-row","crop-row","fence","well"};break;
            case "herb-garden":ids=new[]{"herb-bed","herb-bed","tool-bench","fence","flowers"};break;
            case "rest-stop":ids=new[]{"timber-hut","well","fire","flowers"};break;
            case "river-crossing":ids=new[]{"tool-bench","crate","flowers"};break;
            default:return;
        }
        Vector2 site=p.rect.center+new Vector2(-p.rect.width*.23f,p.rect.height*.08f);
        var local=new[]{Vector2.zero,new Vector2(4,-.7f),new Vector2(-3.5f,-1),new Vector2(2,-3),new Vector2(-3,3),new Vector2(4.5f,2),new Vector2(0,-3.5f)};
        for(int i=0;i<ids.Length;i++){
            if(UseDesignedRivers&&p.kind=="riverside-village"&&i==0)continue;
            Vector2 preferred=site+local[i%local.Length];
            if(p.large){var targets=new[]{new Vector2(-p.rect.width*.23f,p.rect.height*.13f),new Vector2(p.rect.width*.23f,-p.rect.height*.22f),new Vector2(-p.rect.width*.18f,-p.rect.height*.27f),new Vector2(p.rect.width*.26f,p.rect.height*.19f),new Vector2(-p.rect.width*.33f,0),new Vector2(0,p.rect.height*.22f),new Vector2(p.rect.width*.10f,-p.rect.height*.28f)};preferred=p.rect.center+targets[i%targets.Length];}
            if(p.kind=="central-inn"&&i==0)preferred=p.hub+new Vector2(5,-3);
            if(UseDesignedRivers&&p.kind=="river-crossing"&&i==0)preferred=RiverNetwork.Design.mainBridge+new Vector2((Seed&1)==0?4.8f:-4.8f,2);
            bool critical=i==0||(p.large&&i==1)||(p.kind=="mine-workshop"&&i==1);
            int before=placed.Count;if(Place(p,ids[i],preferred,critical)&&i==0){site=placed[before].foot;}
        }
    }
    void ApproachesAndCourts()
    {
        var list=new List<NemoApproach>();
        foreach(var feature in placed.Where(x=>x.critical||(!x.tree&&x.prototype!="flowers"&&x.prototype!="bush"&&x.prototype!="stump"&&x.prototype!="rock-cluster"&&x.prototype!="fence"&&x.prototype!="cliff-face"&&x.prototype!="cliff-face-small"))){
            var p=Plan.parcels[feature.parcel];var entry=feature.foot+Vector2.down*.65f;
            if(!feature.critical&&bodyReserved.Any(b=>Grow(b,.32f).Contains(entry))){var choices=new List<Vector2>();for(float y=-2;y<=1;y+=.25f)for(float x=-2;x<=2;x+=.25f){var point=entry+new Vector2(x,y);if(p.rect.Contains(point)&&!bodyReserved.Any(b=>Grow(b,.36f).Contains(point)))choices.Add(point);}if(choices.Count==0)throw new Exception("Work site has no open entry: "+feature.prototype);entry=choices.OrderBy(c=>(c-entry).sqrMagnitude).First();}
            if(bodyReserved.Any(b=>Grow(b,.32f).Contains(entry)))throw new Exception("Facility entry closed: "+feature.purpose+" / "+feature.prototype);
            var points=Navigate(p,entry);list.Add(new NemoApproach{parcel=p.id,prototype=feature.prototype,points=points});
            Road(points,"short facility approach / "+feature.prototype,p.large?1.2f:.85f);
            if(p.kind=="central-inn"&&feature.prototype=="inn"){Ellipse(entry+new Vector2(-.8f,.1f),4.4f,1.65f,Library.Dirt,-18550,"inn forecourt");}
            if(p.kind=="fort-ruins"&&feature.prototype=="watchtower"){Ellipse(entry+new Vector2(1,0),2.7f,1.35f,Library.Dirt,-18550,"fort offset court");}
            if(p.kind=="large-camp"&&feature.prototype=="tent"){Ellipse(entry+new Vector2(-1,-.15f),2.5f,1.55f,Library.Dirt,-18550,"camp trampled clearing");}
        }
        Approaches=list.ToArray();
    }
    Material Mat(Texture2D texture)
    {
        if(mats.TryGetValue(texture,out var m))return m;m=new Material(Shader.Find("Sprites/Default"));m.mainTexture=texture;mats[texture]=m;Transient.Add(m);return m;
    }
    void MeshObject(string name,List<Vector3> vertices,List<int> triangles,Texture2D texture,int order)
    {
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x*64/texture.width,v.y*64/texture.height)).ToList());mesh.RecalculateBounds();Transient.Add(mesh);
        var go=Group(name);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=Mat(texture);r.sortingOrder=order;
    }
    void Floor()
    {
        var verts=new List<Vector3>{new Vector3(0,0),new Vector3(144,0),new Vector3(144,108),new Vector3(0,108)};MeshObject("continuous native grass",verts,new List<int>{0,2,1,0,3,2},Library.Grass[0],-20000);
    }
    void Road(Vector2[] points,string name,float width=1.7f)
    {
        var verts=new List<Vector3>();var tris=new List<int>();
        for(int i=0;i<points.Length;i++){
            var d=(points[Mathf.Min(points.Length-1,i+1)]-points[Mathf.Max(0,i-1)]).normalized;var normal=new Vector2(-d.y,d.x);float w=width*.5f+.07f*Mathf.Sin(points[i].x*3.1f+points[i].y*2.3f);verts.Add(points[i]+normal*w);verts.Add(points[i]-normal*w);
            if(i>0){int k=i*2;tris.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}
        }
        MeshObject(name,verts,tris,Library.Dirt,-18500);
    }
    void Ellipse(Vector2 c,float rx,float ry,Texture2D tex,int order,string name)
    {
        var verts=new List<Vector3>{c};var tris=new List<int>();for(int i=0;i<=28;i++){float a=i*Mathf.PI*2/28;float n=1+.09f*Mathf.Sin(a*5+c.x);verts.Add(c+new Vector2(Mathf.Cos(a)*rx*n,Mathf.Sin(a)*ry*n));if(i>0)tris.AddRange(new[]{0,i+1,i});}MeshObject(name,verts,tris,tex,order);
    }
    void River(NemoParcel parcel)
    {
        var origin=parcel.rect.center+new Vector2(-3,-5);var group=Group("Native river + removable bridge");Bridge=group.AddComponent<NemoBridge>();var present=new List<BoxCollider2D>();var absent=new List<BoxCollider2D>();var visuals=new List<GameObject>();
        foreach(var owner in Library.River){var sub=Group(owner.kind,group.transform);bool bridge=owner.kind=="RiverBridge";foreach(var v in owner.visuals){bool ground=v.name.Contains("water")||v.name.Contains("bank")||v.name.Contains("foam")||v.name.Contains("current");var sr=Visual(sub.transform,v,origin,origin.y+4.734375f,ground);if(bridge)visuals.Add(sr.gameObject);}foreach(var b in owner.present){var co=Body(sub.transform,b,origin,owner.kind);if(bridge)present.Add(co);}foreach(var b in owner.absent)absent.Add(Body(sub.transform,b,origin,"Bridge absent gap",false));}
        Bridge.PresentBodies=present.ToArray();Bridge.GapBodies=absent.ToArray();Bridge.Visuals=visuals.ToArray();Bridge.West=origin+new Vector2(5.03125f,4.734375f);Bridge.East=origin+new Vector2(10.03125f,4.734375f);
        parcel.hub=origin+new Vector2(4.0f,4.734375f);visualReserved.Add(new Rect(origin.x,origin.y,12,10));
    }
    bool ClearSegment(Vector2 a,Vector2 b)
    {
        int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.15f));for(int i=0;i<=n;i++){var p=Vector2.Lerp(a,b,(float)i/n);if(bodyReserved.Any(r=>Grow(r,.31f).Contains(p)))return false;}return true;
    }
    Vector2[] Navigate(NemoParcel p,Vector2 target)
    {
        const float step=.25f;int w=Mathf.RoundToInt(p.rect.width/step)+1,h=Mathf.RoundToInt(p.rect.height/step)+1;
        int Index(Vector2 v)=>Mathf.Clamp(Mathf.RoundToInt((v.x-p.rect.x)/step),0,w-1)+Mathf.Clamp(Mathf.RoundToInt((v.y-p.rect.y)/step),0,h-1)*w;
        Vector2 Point(int k)=>p.rect.position+new Vector2(k%w*step,k/w*step);
        var obstacles=bodyReserved.Where(r=>r.Overlaps(p.rect)).Select(r=>Grow(r,.32f)).ToArray();var blocked=new bool[w*h];for(int k=0;k<blocked.Length;k++)blocked[k]=obstacles.Any(r=>r.Contains(Point(k)));
        int start=Index(p.hub),goal=Index(target);blocked[start]=blocked[goal]=false;var cost=Enumerable.Repeat(float.MaxValue,w*h).ToArray();var came=Enumerable.Repeat(-1,w*h).ToArray();var open=new List<int>{start};var closed=new bool[w*h];cost[start]=0;
        int[] dx={1,-1,0,0,1,1,-1,-1},dy={0,0,1,-1,1,-1,1,-1};
        while(open.Count>0){int current=open.OrderBy(k=>cost[k]+Vector2.Distance(Point(k),Point(goal))).First();open.Remove(current);if(current==goal)break;closed[current]=true;int x=current%w,y=current/w;for(int d=0;d<8;d++){int xx=x+dx[d],yy=y+dy[d];if(xx<0||yy<0||xx>=w||yy>=h)continue;int k=xx+yy*w;if(blocked[k]||closed[k])continue;if(d>=4&&(blocked[x+yy*w]||blocked[xx+y*w]))continue;float next=cost[current]+step*(d<4?1:1.414214f);if(next>=cost[k])continue;cost[k]=next;came[k]=current;if(!open.Contains(k))open.Add(k);}}
        if(came[goal]<0)throw new Exception("Native river route unavailable "+p.id);
        var route=new List<Vector2>{target};int at=goal;while(at!=start){at=came[at];route.Add(Point(at));}route.Add(p.hub);route.Reverse();var smooth=new List<Vector2>{p.hub};int i=0;while(i<route.Count-1){int next=route.Count-1;while(next>i+1&&!ClearSegment(route[i],route[next]))next--;smooth.Add(route[next]);i=next;}return smooth.ToArray();
    }
    void Forest()
    {
        var trees=Library.Prototypes.Where(p=>p.id.StartsWith("tree-")).ToArray();var world=new Rect(0,0,144,108);
        for(float y=.8f;y<106;y+=2.2f)for(float x=1;x<142;x+=2.2f){float wave=Mathf.Sin(x*.13f+y*.21f);if(rng.NextDouble()>(wave>0?.90:.65))continue;var foot=new Vector2(Mathf.Round((x+(float)rng.NextDouble()*.8f)*64)/64,Mathf.Round((y+(float)rng.NextDouble()*.7f)*64)/64);var proto=trees[rng.Next(trees.Length)];if(!Safe(proto,foot,world,true))continue;var owner=Plan.parcels.First(p=>p.rect.Contains(foot));if(owner.kind=="quiet-meadow"&&Vector2.Distance(foot,owner.hub)<Mathf.Min(owner.rect.width,owner.rect.height)*.33f)continue;Put(proto,foot,owner,false,true);}
        // Quiet places retain a grass edge, shade, flowers and low shrubs without a facility.
        foreach(var p in Plan.parcels.Where(p=>p.quiet))for(int i=0;i<6;i++)Place(p,i%2==0?"flowers":"bush",p.rect.center+new Vector2(i%3*2-2,i<3?3:-4),false);
    }
    public void Generate(int seed)
    {
        if(UseDesignedRivers){GenerateDesigned(seed);return;}
        if(World!=null){if(Application.isPlaying)World.SetActive(false);DestroyImmediate(World);}foreach(var o in Transient)if(o!=null)DestroyImmediate(o);Transient.Clear();placed.Clear();skipped.Clear();Approaches=new NemoApproach[0];visualReserved.Clear();bodyReserved.Clear();mats=new Dictionary<Texture2D,Material>();Seed=seed;rng=new Random(seed);Plan=NemoLayout.Generate(seed);if(Plan.errors.Length>0)throw new Exception(string.Join(";",Plan.errors));World=new GameObject("Generated map | seed "+seed+" | Standard");World.transform.SetParent(transform);Floor();
        var riverParcel=Plan.parcels.First(p=>p.kind=="river-crossing");River(riverParcel);
        // Repair only native terrain routes. All other parcel paths retain their smooth portal tangents.
        foreach(var e in Plan.links.Where(e=>e.a==riverParcel.id||e.b==riverParcel.id)){
            bool first=e.a==riverParcel.id;var other=Plan.parcels[first?e.b:e.a];Vector2 outward=(e.port-other.rect.center).normalized;var normal=other.rect.xMin==e.port.x?Vector2.left:other.rect.xMax==e.port.x?Vector2.right:other.rect.yMin==e.port.y?Vector2.down:Vector2.up;
            var native=Navigate(riverParcel,e.port);var curve=NemoLayout.Within(other,e.port,normal,seed+other.id);e.points=first?native.Concat(curve.Reverse().Skip(1)).ToArray():curve.Concat(native.Reverse().Skip(1)).ToArray();
        }
        foreach(var e in Plan.links){var a=Plan.parcels[e.a];var b=Plan.parcels[e.b];float width=a.large||b.large?1.65f:a.land&&b.land?.85f:1.05f;Road(e.points,"soil link "+e.a+" to "+e.b,width);}
        foreach(var p in Plan.parcels){if(!p.land)Ellipse(p.hub,p.large?1.2f:.60f,p.large?.90f:.50f,Library.Dirt,-18500,"small worn junction");Recipe(p);}
        ApproachesAndCourts();Forest();ExtendExteriorDesign();Placements=placed.ToArray();Skipped=skipped.ToArray();Design=NemoBlockDesignFreeze.Capture(this);Boundary=NemoBoundary.Build(this);Overlay=Group("Diagnostic packing and paths");Overlay.SetActive(false);foreach(var p in Plan.parcels)DrawLine(new[]{p.rect.min,new Vector2(p.rect.xMax,p.rect.yMin),p.rect.max,new Vector2(p.rect.xMin,p.rect.yMax),p.rect.min},p.large?Color.yellow:new Color(.2f,.75f,1,1));Physics2D.SyncTransforms();
    }
    readonly Dictionary<Texture2D,Material> spriteMaterials=new Dictionary<Texture2D,Material>();
    public void BindSpriteMaterial(SpriteRenderer sr){if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline==null)return;var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");if(shader==null)throw new Exception("Project URP sprite shader unavailable");var texture=sr.sprite.texture;if(!spriteMaterials.TryGetValue(texture,out var material)||material==null){material=new Material(shader);material.mainTexture=texture;spriteMaterials[texture]=material;Transient.Add(material);}sr.sharedMaterial=material;}
    public static bool IsWorkSite(string id)=>id!="flowers"&&id!="bush"&&id!="stump"&&id!="rock-cluster"&&id!="fence"&&id!="cliff-face"&&id!="cliff-face-small"; public void StabilizeSpriteDepth(){int index=0;foreach(var sr in World.GetComponentsInChildren<SpriteRenderer>(true)){var p=sr.transform.position;p.z=-0.000001f*(++index);sr.transform.position=p;}}
    void DrawLine(Vector2[] points,Color color){var g=Group("logical parcel",Overlay.transform);var lr=g.AddComponent<LineRenderer>();lr.positionCount=points.Length;lr.SetPositions(points.Select(p=>new Vector3(p.x,p.y,-1)).ToArray());lr.sharedMaterial=new Material(Shader.Find("Sprites/Default"));Transient.Add(lr.sharedMaterial);lr.startColor=lr.endColor=color;lr.startWidth=lr.endWidth=.10f;lr.sortingOrder=30000;}
}
