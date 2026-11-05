using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random=System.Random;

[Serializable] public class NemoParcel { public int id; public string kind,shape; public Rect rect; public Vector2 hub; public bool large,land,quiet; public int degree; }
[Serializable] public class NemoLink { public int a,b; public Vector2 port; public Vector2[] points; }
[Serializable] public class NemoPlan { public int seed,graphAttempts; public NemoParcel[] parcels; public NemoLink[] links; public NemoRoadOutlet[] roadOutlets; public float totalArea; public string[] errors; }
public static class NemoLayout
{
    static float Q(float x)=>Mathf.Round(x*64)/64;
    static Vector2 Q(Vector2 x)=>new Vector2(Q(x.x),Q(x.y));
    static Rect Flip(Rect r,int seed){if((seed&1)!=0)r.x=144-r.xMax;if((seed&2)!=0)r.y=108-r.yMax;return r;}
    static IEnumerable<Rect> Carve(Rect f,Rect r)
    {
        if(!f.Overlaps(r)){yield return f;yield break;}
        float x0=Mathf.Max(f.xMin,r.xMin),x1=Mathf.Min(f.xMax,r.xMax),y0=Mathf.Max(f.yMin,r.yMin),y1=Mathf.Min(f.yMax,r.yMax);
        if(y0>f.yMin)yield return new Rect(f.xMin,f.yMin,f.width,y0-f.yMin);
        if(y1<f.yMax)yield return new Rect(f.xMin,y1,f.width,f.yMax-y1);
        if(x0>f.xMin)yield return new Rect(f.xMin,y0,x0-f.xMin,y1-y0);
        if(x1<f.xMax)yield return new Rect(x1,y0,f.xMax-x1,y1-y0);
    }
    static void Split(Rect r,Random rng,List<Rect> outRects)
    {
        bool wide=r.width>34&&r.height>=3,tall=r.height>34&&r.width>=3;
        if(!wide&&!tall){outRects.Add(r);return;}
        bool vertical=wide&&(!tall||r.width>r.height||rng.Next(2)==0);
        float n=vertical?r.width:r.height;int min=vertical?14:12;
        int cut=rng.Next(min/2,(int)(n-min)/2+1)*2;
        if(vertical){Split(new Rect(r.x,r.y,cut,r.height),rng,outRects);Split(new Rect(r.x+cut,r.y,r.width-cut,r.height),rng,outRects);}
        else{Split(new Rect(r.x,r.y,r.width,cut),rng,outRects);Split(new Rect(r.x,r.y+cut,r.width,r.height-cut),rng,outRects);}
    }
    static bool Adjacent(Rect a,Rect b,out Vector2 port,out Vector2 dir,Random rng)
    {
        port=dir=Vector2.zero;
        float lo,hi;
        if(Mathf.Abs(a.xMax-b.xMin)<.01f||Mathf.Abs(b.xMax-a.xMin)<.01f){lo=Mathf.Max(a.yMin,b.yMin);hi=Mathf.Min(a.yMax,b.yMax);if(hi-lo<3)return false;float j=(float)rng.NextDouble();port=Q(new Vector2(a.xMax==b.xMin?a.xMax:a.xMin,Mathf.Lerp(lo+1.2f,hi-1.2f,j)));dir=a.xMax==b.xMin?Vector2.right:Vector2.left;return true;}
        if(Mathf.Abs(a.yMax-b.yMin)<.01f||Mathf.Abs(b.yMax-a.yMin)<.01f){lo=Mathf.Max(a.xMin,b.xMin);hi=Mathf.Min(a.xMax,b.xMax);if(hi-lo<3)return false;float j=(float)rng.NextDouble();port=Q(new Vector2(Mathf.Lerp(lo+1.2f,hi-1.2f,j),a.yMax==b.yMin?a.yMax:a.yMin));dir=a.yMax==b.yMin?Vector2.up:Vector2.down;return true;}
        return false;
    }
    public static Vector2[] Curve(Vector2 hub,Vector2 port,Vector2 outward,Rect rect,int salt)
    {
        float dist=Vector2.Distance(hub,port);var p1=Vector2.Lerp(hub,port,.32f);var p2=port-outward*Mathf.Min(dist*.26f,3.0f);
        // The portal tangent is shared; curves inside the parcel hide the packing rectangles.
        var perp=new Vector2(outward.y,-outward.x);p1+=perp*Mathf.Min(3.3f,dist*.22f)*((salt&1)==0?1:-1);
        p1.x=Mathf.Clamp(p1.x,rect.xMin+.8f,rect.xMax-.8f);p1.y=Mathf.Clamp(p1.y,rect.yMin+.8f,rect.yMax-.8f);
        p2.x=Mathf.Clamp(p2.x,rect.xMin+.7f,rect.xMax-.7f);p2.y=Mathf.Clamp(p2.y,rect.yMin+.7f,rect.yMax-.7f);
        var points=new List<Vector2>();int steps=Mathf.Max(5,Mathf.CeilToInt(dist/.45f));
        for(int i=0;i<=steps;i++){float t=(float)i/steps,s=1-t;points.Add(Q(s*s*s*hub+3*s*s*t*p1+3*s*t*t*p2+t*t*t*port));}
        return points.ToArray();
    }
    public static Vector2[] Within(NemoParcel p,Vector2 port,Vector2 outward,int salt)
    {
        if(!p.large&&p.degree<3)return Curve(p.hub,port,outward,p.rect,salt);
        // Several entrances join a winding street at different points, rather than radiating from one plaza.
        bool horizontal=p.rect.width>=p.rect.height;float sign=((salt+p.id)&2)==0?1:-1;
        var delta=horizontal?new Vector2(p.rect.width*.25f,p.rect.height*.12f*sign):new Vector2(p.rect.width*.12f*sign,p.rect.height*.25f);
        var normal=new Vector2(-delta.y,delta.x).normalized;var spine=new Vector2[21];
        float bend=Mathf.Min(1.15f,Mathf.Min(p.rect.width,p.rect.height)*.08f);
        for(int k=0;k<=20;k++){float t=(k-10)/10f;var point=p.hub+delta*t+normal*(bend*Mathf.Sin(t*Mathf.PI));point.x=Mathf.Clamp(point.x,p.rect.xMin+.8f,p.rect.xMax-.8f);point.y=Mathf.Clamp(point.y,p.rect.yMin+.8f,p.rect.yMax-.8f);spine[k]=Q(point);}
        int anchor=Enumerable.Range(0,21).OrderBy(k=>(spine[k]-port).sqrMagnitude).First();
        var route=new List<Vector2>();int step=anchor<10?-1:1;for(int k=10;;k+=step){route.Add(spine[k]);if(k==anchor)break;}
        return route.Concat(Curve(spine[anchor],port,outward,p.rect,salt).Skip(1)).ToArray();
    }
    public static NemoPlan Generate(int seed)
    {
        var rng=new Random(seed);var parcels=new List<NemoParcel>();var free=new List<Rect>{new Rect(0,0,144,108)};
        int shift=rng.Next(-2,3)*2;
        var reserved=new[]{new Rect(50+shift,34,40,30),new Rect(4,72,36,32),new Rect(100,70,40,34),new Rect(100,4,40,26),new Rect(44,68,30,36),new Rect(4,4,30,36),new Rect(70,4,26,26)};
        var kinds=new[]{"central-inn","fort-ruins","riverside-village","large-camp","abandoned-yard","mine-workshop","river-crossing"};
        // Outside purposes rotate independently of geometric reflection.
        var outside=new[]{"fort-ruins","riverside-village","large-camp"}.OrderBy(_=>rng.Next()).ToArray();for(int i=0;i<3;i++)kinds[i+1]=outside[i];
        for(int i=0;i<reserved.Length;i++){
            var rect=Flip(reserved[i],seed);parcels.Add(new NemoParcel{id=parcels.Count,kind=kinds[i],rect=rect,hub=Q(rect.center),large=i<4});free=free.SelectMany(f=>Carve(f,rect)).ToList();
        }
        var pieces=new List<Rect>();foreach(var rect in free)Split(rect,rng,pieces);
        string[] bag={"cargo-layby","dungeon-mouth","spring","shrine","checkpoint","monster-nest","logging-clearing","small-camp","cliff-road","orchard","herb-garden","rest-stop"};
        var shuffled=bag.OrderBy(_=>rng.Next()).ToList();
        bool Fits(string kind,Rect r){switch(kind){case "dungeon-mouth":return r.width>=18&&r.height>=12;case "cliff-road":return r.width>=14&&r.height>=20;case "cargo-layby":return r.width>=16&&r.height>=12;case "spring":return r.width>=16&&r.height>=14;case "checkpoint":return r.width>=16&&r.height>=14;default:return true;}}
        foreach(var rect in pieces){bool land=rect.width<12||rect.height<10;string kind="wooded-verge";if(!land){kind=shuffled.FirstOrDefault(k=>Fits(k,rect));if(kind==null){kind="rest-stop";}else shuffled.Remove(kind);}parcels.Add(new NemoParcel{id=parcels.Count,kind=kind,rect=rect,hub=Q(rect.center),land=land});}
        foreach(var p in parcels){if(p.kind=="river-crossing")continue;float jx=(float)rng.NextDouble()-.5f,jy=(float)rng.NextDouble()-.5f;float ratio=p.land?.60f:.22f;var point=p.rect.center+new Vector2(Mathf.Min(4.5f,p.rect.width*ratio)*jx,Mathf.Min(4.5f,p.rect.height*ratio)*jy);point.x=Mathf.Clamp(point.x,p.rect.xMin+.8f,p.rect.xMax-.8f);point.y=Mathf.Clamp(point.y,p.rect.yMin+.8f,p.rect.yMax-.8f);p.hub=Q(point);}
        var candidates=new List<NemoLink>();var dirs=new Dictionary<NemoLink,Vector2>();
        for(int a=0;a<parcels.Count;a++)for(int b=a+1;b<parcels.Count;b++)if(!parcels[a].large||!parcels[b].large){if(Adjacent(parcels[a].rect,parcels[b].rect,out var port,out var dir,rng)){var e=new NemoLink{a=a,b=b,port=port};candidates.Add(e);dirs[e]=dir;}}
        var active=new HashSet<int>{0};var selected=new List<NemoLink>();
        var roadParent=Enumerable.Range(0,parcels.Count).ToArray();var roadLarge=parcels.Select(p=>p.large?1:0).ToArray();
        int Find(int k){while(roadParent[k]!=k)k=roadParent[k];return k;}
        bool RoadOnly(NemoLink e)=>(parcels[e.a].land||parcels[e.a].large)&&(parcels[e.b].land||parcels[e.b].large);
        bool Allowed(NemoLink e){if(!RoadOnly(e))return true;int a=Find(e.a),b=Find(e.b);return a==b||roadLarge[a]+roadLarge[b]<=1;}
        void Track(NemoLink e){if(!RoadOnly(e))return;int a=Find(e.a),b=Find(e.b);if(a!=b){roadParent[b]=a;roadLarge[a]+=roadLarge[b];}}
        int graphAttempts=0;
        bool LocationsReached()=>parcels.All(p=>p.land||active.Contains(p.id));
        while(!LocationsReached()&&graphAttempts<64){
            graphAttempts++;active.Clear();active.Add(0);selected.Clear();for(int k=0;k<roadParent.Length;k++){roadParent[k]=k;roadLarge[k]=parcels[k].large?1:0;}
            while(!LocationsReached()){var options=candidates.Where(e=>active.Contains(e.a)!=active.Contains(e.b)&&Allowed(e)).ToArray();if(options.Length==0)break;var edge=options[rng.Next(options.Length)];selected.Add(edge);Track(edge);active.Add(edge.a);active.Add(edge.b);}
        }
        if(!LocationsReached())throw new Exception("Constrained graph unavailable after 64 attempts for seed "+seed);
        // A few side loops shorten walks without turning every rectangle into a junction.
        foreach(var e in candidates.OrderBy(_=>rng.Next()))if(active.Contains(e.a)&&active.Contains(e.b)&&!selected.Contains(e)&&Allowed(e)&&rng.NextDouble()<.04&&selected.Count(x=>x.a==e.a||x.b==e.a)<4&&selected.Count(x=>x.a==e.b||x.b==e.b)<4){selected.Add(e);Track(e);}
        // Verges fill the rectangular footprint, but an unused verge does not need a road spur.
        bool trimmed;do{trimmed=false;foreach(var p in parcels.Where(p=>p.land)){var edges=selected.Where(e=>e.a==p.id||e.b==p.id).ToArray();if(edges.Length==1){selected.Remove(edges[0]);trimmed=true;}}}while(trimmed);
        foreach(var p in parcels){p.degree=selected.Count(e=>e.a==p.id||e.b==p.id);p.shape=p.degree<=1?"pocket":p.degree==2?"bend-or-pass":p.degree==3?"three-way":p.degree==4?"four-way":"many-mouth-junction";}
        // Empty roads are separate quiet locations. Greedy independent selection caps runs at one.
        var quietCandidates=parcels.Where(p=>!p.large&&!p.land&&p.id>=7&&p.rect.width<30&&p.rect.height<28).OrderBy(_=>rng.Next()).ToArray();
        int quietTarget=Mathf.Max(2,Mathf.RoundToInt(parcels.Count(p=>!p.large&&!p.land)*.16f));int quietCount=0;
        foreach(var p in quietCandidates){if(quietCount>=quietTarget)break;if(selected.Any(e=>(e.a==p.id&&parcels[e.b].quiet)||(e.b==p.id&&parcels[e.a].quiet)))continue;p.quiet=true;p.kind=new[]{"quiet-meadow","quiet-canopy","quiet-bank"}[quietCount++%3];}
        foreach(var e in selected){var a=parcels[e.a];var b=parcels[e.b];var left=Within(a,e.port,dirs[e],seed+a.id);var right=Within(b,e.port,-dirs[e],seed+b.id);e.points=left.Concat(right.Reverse().Skip(1)).ToArray();}
        var errors=new List<string>();float area=parcels.Sum(p=>p.rect.width*p.rect.height);
        if(Mathf.Abs(area-144*108)>.01f)errors.Add("Partition area mismatch");
        for(int a=0;a<parcels.Count;a++)for(int b=a+1;b<parcels.Count;b++)if(parcels[a].rect.Overlaps(parcels[b].rect))errors.Add("Parcel overlap "+a+" "+b);
        if(parcels.Count(p=>p.large)!=4)errors.Add("Large count mismatch");
        return new NemoPlan{seed=seed,graphAttempts=graphAttempts,parcels=parcels.ToArray(),links=selected.ToArray(),totalArea=area,errors=errors.ToArray()};
    }
}
