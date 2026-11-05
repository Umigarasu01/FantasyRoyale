using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public class NemoRiverOutlet { public Vector2 position,direction; public float width; }
[Serializable] public class NemoRiverBlock { public int id,parcel; public string template; public Vector2[] points,crossingRoad; public float[] widths; public NemoRiverOutlet inlet,outlet;public NemoRoadOutlet[] roadOutlets; }
[Serializable] public class NemoRiverJoin { public int a,b; }
[Serializable] public class NemoRiverDesign { public NemoRiverBlock[] blocks;public NemoRiverJoin[] joins; public Vector2 mainBridge,millBridge,millFoot;public bool millMirror;public int millParcel; }
[Serializable] public class NemoRoadOutlet { public int parcel,neighbour;public Vector2 position,direction;public float width; }
public static class NemoRiverPlan
{
    public const float Width=3;
    static Vector2 Flip(Vector2 p,int seed){if((seed&1)!=0)p.x=144-p.x;if((seed&2)!=0)p.y=108-p.y;return p;}
    static Vector2 FlipDirection(Vector2 d,int seed){if((seed&1)!=0)d.x=-d.x;if((seed&2)!=0)d.y=-d.y;return d;}
    static Rect Unflip(Rect r,int seed){if((seed&1)!=0)r.x=144-r.xMax;if((seed&2)!=0)r.y=108-r.yMax;return r;}
    public static NemoRiverDesign Create(NemoPlan plan)
    {
        int seed=plan.seed;var mill=plan.parcels.First(p=>p.kind=="riverside-village");var r=Unflip(mill.rect,seed);var central=Unflip(plan.parcels[0].rect,seed);
        var main=new Vector2(87.53125f,16.734375f);var channel=r.center+new Vector2(-r.width*.23f,r.height*.08f+.7f);var blocks=new List<NemoRiverBlock>();
        void Add(string template,Vector2[] points,float[] widths=null){var world=points.Select(p=>Flip(p,seed)).ToArray();var sizes=widths??Enumerable.Repeat(Width,points.Length).ToArray();var center=world[world.Length/2];var owner=plan.parcels.FirstOrDefault(p=>p.rect.Contains(center));blocks.Add(new NemoRiverBlock{id=blocks.Count,parcel=owner==null?-1:owner.id,template=template,points=world,widths=sizes,inlet=new NemoRiverOutlet{position=world[0],direction=(world[0]-world[1]).normalized,width=sizes[0]},outlet=new NemoRiverOutlet{position=world.Last(),direction=(world.Last()-world[world.Length-2]).normalized,width=sizes.Last()}});}
        void Straight(Vector2 a,Vector2 b,string purpose="bank-straight"){float distance=Vector2.Distance(a,b);int pieces=Mathf.Max(1,Mathf.CeilToInt(distance/32));for(int k=0;k<pieces;k++){var p=Vector2.Lerp(a,b,(float)k/pieces);var q=Vector2.Lerp(a,b,(float)(k+1)/pieces);int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(p,q)/.4f));Add(purpose,Enumerable.Range(0,n+1).Select(i=>Vector2.Lerp(p,q,(float)i/n)).ToArray());}}
        void Corner(Vector2 a,Vector2 inDirection,Vector2 b,Vector2 outDirection){float radius=Vector2.Distance(a,b)/Mathf.Sqrt(2);float handle=.55228475f*radius;int n=Mathf.Max(8,Mathf.CeilToInt(radius*Mathf.PI/2/.3f));var pts=new Vector2[n+1];for(int k=0;k<=n;k++){float t=(float)k/n,s=1-t;pts[k]=s*s*s*a+3*s*s*t*(a+inDirection*handle)+3*s*t*t*(b-outDirection*handle)+t*t*t*b;}Add("bank-quarter-bend",pts);blocks.Last().inlet.direction=FlipDirection(-inDirection,seed);blocks.Last().outlet.direction=FlipDirection(outDirection,seed);}
        Straight(new Vector2(main.x,-32),new Vector2(main.x,0),"outside-outlet");Straight(new Vector2(main.x,0),main-Vector2.up*4);
        Add("bridge-and-two-bank-approaches",new[]{main-Vector2.up*4,main,main+Vector2.up*4});
        bool right=r.xMin>=100,top=r.yMin>=70;float corridor=right?(central.xMax+100)/2:(40+central.xMin)/2;
        float firstRadius=right&&top?Mathf.Min(3,(corridor-main.x-.75f)/2):3;
        float side=right?1:-1;float targetX=right&&top||!right?corridor:channel.x;
        Straight(main+Vector2.up*4,new Vector2(main.x,32-firstRadius));
        Corner(new Vector2(main.x,32-firstRadius),Vector2.up,new Vector2(main.x+side*firstRadius,32),Vector2.right*side);
        if(top){
            Straight(new Vector2(main.x+side*firstRadius,32),new Vector2(targetX-side*firstRadius,32));
            Corner(new Vector2(targetX-side*firstRadius,32),Vector2.right*side,new Vector2(targetX,32+firstRadius),Vector2.up);
            float upper=right?67:66;Straight(new Vector2(targetX,32+firstRadius),new Vector2(targetX,upper-3));
            Corner(new Vector2(targetX,upper-3),Vector2.up,new Vector2(targetX+side*3,upper),Vector2.right*side);
            Straight(new Vector2(targetX+side*3,upper),new Vector2(channel.x-side*3,upper));
            Corner(new Vector2(channel.x-side*3,upper),Vector2.right*side,new Vector2(channel.x,upper+3),Vector2.up);
            Straight(new Vector2(channel.x,upper+3),channel-Vector2.up*7);
        }else{
            Straight(new Vector2(main.x+3,32),new Vector2(channel.x-3,32));
            Corner(new Vector2(channel.x-3,32),Vector2.right,new Vector2(channel.x,29),Vector2.down);
            Straight(new Vector2(channel.x,29),channel+Vector2.up*7);
        }
        var sluice=new[]{channel-Vector2.up*7,channel-Vector2.up*4,channel,channel+Vector2.up*4,channel+Vector2.up*7};var narrow=new[]{3f,1.65f,1.65f,1.65f,3f};if(!top){Array.Reverse(sluice);Array.Reverse(narrow);}Add("watermill-sluice-and-footbridge",sluice,narrow);
        Straight(channel+Vector2.up*(top?7:-7),new Vector2(channel.x,top?108:0));Straight(new Vector2(channel.x,top?108:0),new Vector2(channel.x,top?140:-32),"outside-outlet");
        var worldChannel=Flip(channel,seed);var result=new NemoRiverDesign{blocks=blocks.ToArray(),joins=Enumerable.Range(0,blocks.Count-1).Select(k=>new NemoRiverJoin{a=k,b=k+1}).ToArray(),mainBridge=Flip(main,seed),millBridge=worldChannel+Vector2.down*2.1f,millFoot=worldChannel-new Vector2((seed&1)==0?5.8f:-5.8f,.7f),millMirror=(seed&1)!=0,millParcel=mill.id};
        foreach(var block in result.blocks.Where(b=>b.template=="bridge-and-two-bank-approaches"||b.template=="watermill-sluice-and-footbridge")){var anchor=block.template=="bridge-and-two-bank-approaches"?result.mainBridge:result.millBridge;block.crossingRoad=new[]{anchor+Vector2.left*4.1f,anchor+Vector2.left*2.7f,anchor+Vector2.right*2.7f,anchor+Vector2.right*4.1f};block.roadOutlets=new[]{new NemoRoadOutlet{parcel=block.parcel,neighbour=-1,position=block.crossingRoad.First(),direction=Vector2.left,width=1.1f},new NemoRoadOutlet{parcel=block.parcel,neighbour=-1,position=block.crossingRoad.Last(),direction=Vector2.right,width=1.1f}};}
        return result;
    }
    public static string[] Validate(NemoRiverDesign design)
    {
        var errors=new List<string>();foreach(var join in design.joins){var a=design.blocks[join.a].outlet;var b=design.blocks[join.b].inlet;if(Vector2.Distance(a.position,b.position)>.0001f||Mathf.Abs(a.width-b.width)>.0001f||Vector2.Dot(a.direction,b.direction)>-.99999f)errors.Add("River outlet mismatch "+join.a+"/"+join.b);}
        foreach(var b in design.blocks){if(b.points.Length<2||b.points.Length!=b.widths.Length||b.widths.Any(w=>w<1.6f))errors.Add("River block invalid "+b.id);bool bridge=b.template=="bridge-and-two-bank-approaches"||b.template=="watermill-sluice-and-footbridge";if(bridge&&(b.roadOutlets==null||b.roadOutlets.Length!=2||b.crossingRoad==null||b.crossingRoad.Length<2||b.crossingRoad.First()!=b.roadOutlets[0].position||b.crossingRoad.Last()!=b.roadOutlets[1].position||b.roadOutlets.Any(p=>Mathf.Abs(p.width-1.1f)>.0001f)||Vector2.Dot(b.roadOutlets[0].direction,Vector2.left)<.99999f||Vector2.Dot(b.roadOutlets[1].direction,Vector2.right)<.99999f))errors.Add("Bridge bank road ports invalid "+b.id);if(!bridge&&b.roadOutlets!=null&&b.roadOutlets.Length>0)errors.Add("Unexpected road ports "+b.id);}
        foreach(var p in new[]{design.blocks.First().inlet.position,design.blocks.Last().outlet.position})if(new Rect(-31,-31,206,170).Contains(p))errors.Add("River end insufficiently outside map");return errors.ToArray();
    }
}
