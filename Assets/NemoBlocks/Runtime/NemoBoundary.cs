using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public class NemoBoundaryWall {public int a,b;public bool outside;public Vector2[] points;public EdgeCollider2D collider;public int[] supportIds;}
[Serializable] public class NemoBoundaryGate {public int a,b;public Vector2 port,start,end;}
public class NemoBoundary : MonoBehaviour
{
    public const float GateHalfWidth=1.60f,WallRadius=0f;
    public NemoBoundaryWall[] Walls;public NemoBoundaryGate[] Gates;public Vector2[] AddedTreeFeet;public int NativeTreeCount;public int NativeBarrierCount;public NemoBoundarySupport[] Supports;public NemoBoundaryOmission[] Omissions;public int AlignmentVersion=7;public NemoWalkRegion WalkRegion;
    static float Q(float n)=>Mathf.Round(n*64)/64;
    static Vector2 Q(Vector2 p)=>new Vector2(Q(p.x),Q(p.y));
    static Rect Grow(Rect r,float n)=>new Rect(r.xMin-n,r.yMin-n,r.width+n*2,r.height+n*2);
    static bool Seam(Rect a,Rect b,out Vector2 start,out Vector2 end)
    {
        start=end=Vector2.zero;
        if(Mathf.Abs(a.xMax-b.xMin)<.01f||Mathf.Abs(b.xMax-a.xMin)<.01f){float lo=Mathf.Max(a.yMin,b.yMin),hi=Mathf.Min(a.yMax,b.yMax);if(hi-lo<=.01f)return false;float x=Mathf.Abs(a.xMax-b.xMin)<.01f?a.xMax:a.xMin;start=new Vector2(x,lo);end=new Vector2(x,hi);return true;}
        if(Mathf.Abs(a.yMax-b.yMin)<.01f||Mathf.Abs(b.yMax-a.yMin)<.01f){float lo=Mathf.Max(a.xMin,b.xMin),hi=Mathf.Min(a.xMax,b.xMax);if(hi-lo<=.01f)return false;float y=Mathf.Abs(a.yMax-b.yMin)<.01f?a.yMax:a.yMin;start=new Vector2(lo,y);end=new Vector2(hi,y);return true;}
        return false;
    }
    static Vector2[] Line(Vector2 start,Vector2 end,int salt,bool outside)
    {
        float length=Vector2.Distance(start,end);int n=Mathf.Max(1,Mathf.CeilToInt(length/1.25f));var d=(end-start).normalized;var normal=new Vector2(-d.y,d.x);var p=new Vector2[n+1];
        for(int i=0;i<=n;i++){float t=(float)i/n;float wave=outside?0:.18f*Mathf.Sin(t*Mathf.PI)*Mathf.Sin(length*t*.73f+salt*1.37f);p[i]=Q(Vector2.Lerp(start,end,t)+normal*wave);}p[0]=start;p[n]=end;return p;
    }
    public static void PlanWalls(NemoPlan plan,out NemoBoundaryWall[] walls,out NemoBoundaryGate[] gates)
    {
        var result=new List<NemoBoundaryWall>();var openings=new List<NemoBoundaryGate>();
        void Add(int a,int b,Vector2 start,Vector2 end,bool outside){
            var direction=(end-start).normalized;float length=Vector2.Distance(start,end);
            var ports=plan.links.Where(e=>(e.a==a&&e.b==b)||(e.a==b&&e.b==a)).OrderBy(e=>Vector2.Dot(e.port-start,direction)).ToArray();float cursor=0;
            foreach(var e in ports){float at=Vector2.Dot(e.port-start,direction);float lo=Mathf.Max(0,at-GateHalfWidth),hi=Mathf.Min(length,at+GateHalfWidth);
                if(lo-cursor>.01f)result.Add(new NemoBoundaryWall{a=a,b=b,outside=outside,points=Line(start+direction*cursor,start+direction*lo,a*97+b*7+plan.seed,outside)});
                openings.Add(new NemoBoundaryGate{a=a,b=b,port=e.port,start=start+direction*lo,end=start+direction*hi});cursor=Mathf.Max(cursor,hi);}
            if(length-cursor>.01f)result.Add(new NemoBoundaryWall{a=a,b=b,outside=outside,points=Line(start+direction*cursor,end,a*97+b*7+plan.seed,outside)});
        }
        foreach(var a in plan.parcels){foreach(var b in plan.parcels.Where(p=>p.id>a.id))if(Seam(a.rect,b.rect,out var start,out var end))Add(a.id,b.id,start,end,false);
            var r=a.rect;if(r.xMin==0)Add(a.id,-1,new Vector2(0,r.yMin),new Vector2(0,r.yMax),true);if(r.xMax==144)Add(a.id,-1,new Vector2(144,r.yMin),new Vector2(144,r.yMax),true);
            if(r.yMin==0)Add(a.id,-1,new Vector2(r.xMin,0),new Vector2(r.xMax,0),true);if(r.yMax==108)Add(a.id,-1,new Vector2(r.xMin,108),new Vector2(r.xMax,108),true);}
        walls=result.ToArray();gates=openings.ToArray();
    }

    public static NemoBoundary Build(NemoMap map)
    {
        if(map.Design==null)throw new Exception("Complete block design before deriving physical boundaries");var root=new GameObject("Physical boundaries follow existing native forest | no decorative additions");root.transform.SetParent(map.World.transform);var boundary=root.AddComponent<NemoBoundary>();PlanWalls(map.Plan,out var packingWalls,out boundary.Gates);boundary.Supports=map.Design.Forest;boundary.WalkRegion=NemoWalkRegion.Build(map,root.transform);var walls=new List<NemoBoundaryWall>();var omissions=new List<NemoBoundaryOmission>();var water=boundary.WalkRegion.WaterTerrain;
        // Keep every segment, including collinear shoreline/world-edge segments.
        // Omitting a vertex near water deleted its entire adjoining segment in v6.
        // Water and the design contour may overlap safely; bridge openings come from the ground mask.
        foreach(var contour in boundary.WalkRegion.Contours){if(contour.points.Length<3)continue;var go=new GameObject("Complete design-derived forest and shore Collider "+walls.Count);go.transform.SetParent(root.transform);var collider=go.AddComponent<EdgeCollider2D>();collider.points=contour.points;collider.edgeRadius=WallRadius;walls.Add(new NemoBoundaryWall{a=-2,b=walls.Count,points=contour.points,collider=collider,supportIds=new int[0]});}
        boundary.Walls=walls.ToArray();boundary.Omissions=omissions.ToArray();boundary.AddedTreeFeet=new Vector2[0];boundary.NativeTreeCount=map.Design.Forest.Length;boundary.NativeBarrierCount=0;map.Design.VerifyUnchanged(map);Physics2D.SyncTransforms();return boundary;
    }
}

[Serializable] public class NemoBoundarySupport {public string prototype;public Vector2 foot;public bool tree;public SpriteRenderer[] visuals;}
[Serializable] public class NemoBoundaryOmission {public Vector2 point;public string reason;}
