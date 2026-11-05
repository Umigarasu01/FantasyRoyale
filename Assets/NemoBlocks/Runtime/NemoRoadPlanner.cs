using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Roads belong to parcels. River outlets are held by NemoRiverDesign, independently.
public sealed class NemoRoadPlanner
{
    sealed class Grid
    {
        public NemoParcel parcel;public int w,h;public bool[] free,reachable;public Vector2 hub;readonly Rect[] bodies;const float step=.5f;
        public Vector2 Point(int k)=>parcel.rect.position+new Vector2(k%w*step,k/w*step);
        public int Index(Vector2 p)=>Mathf.Clamp(Mathf.RoundToInt((p.x-parcel.rect.xMin)/step),0,w-1)+Mathf.Clamp(Mathf.RoundToInt((p.y-parcel.rect.yMin)/step),0,h-1)*w;
        public bool Clear(Vector2 a,Vector2 b){int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.12f));for(int k=0;k<=n;k++){var p=Vector2.Lerp(a,b,(float)k/n);if(!parcel.rect.Contains(p)||bodies.Any(r=>r.Contains(p)))return false;}return true;}
        public Grid(NemoParcel p,List<Rect> all)
        {
            parcel=p;bodies=all.Where(r=>r.Overlaps(p.rect)).Select(r=>new Rect(r.x-.36f,r.y-.36f,r.width+.72f,r.height+.72f)).ToArray();w=Mathf.RoundToInt(p.rect.width/step)+1;h=Mathf.RoundToInt(p.rect.height/step)+1;free=new bool[w*h];reachable=new bool[w*h];for(int k=0;k<free.Length;k++){var q=Point(k);free[k]=q.x>=p.rect.xMin+.65f&&q.x<=p.rect.xMax-.65f&&q.y>=p.rect.yMin+.65f&&q.y<=p.rect.yMax-.65f&&!bodies.Any(r=>r.Contains(q));}
            var options=Enumerable.Range(0,free.Length).Where(k=>free[k]).OrderBy(k=>(Point(k)-p.hub).sqrMagnitude).ToArray();if(options.Length==0){if(!p.land)throw new Exception("No free hub in "+p.id+" "+p.kind);return;}hub=Point(options[0]);parcel.hub=hub;var queue=new Queue<int>();queue.Enqueue(options[0]);reachable[options[0]]=true;while(queue.Count>0){int current=queue.Dequeue();foreach(var k in Neighbours(current))if(!reachable[k]){reachable[k]=true;queue.Enqueue(k);}}
        }
        IEnumerable<int> Neighbours(int k){int x=k%w,y=k/w;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dy==0)continue;int xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=w||yy>=h)continue;int other=xx+yy*w;if(!free[other])continue;if(dx!=0&&dy!=0&&(!free[xx+y*w]||!free[x+yy*w]))continue;yield return other;}}
        int Endpoint(Vector2 p){int center=Index(p);var options=new List<int>();int x=center%w,y=center/w;for(int yy=Mathf.Max(0,y-2);yy<=Mathf.Min(h-1,y+2);yy++)for(int xx=Mathf.Max(0,x-2);xx<=Mathf.Min(w-1,x+2);xx++){int k=xx+yy*w;if(reachable[k]&&Clear(Point(k),p))options.Add(k);}return options.Count==0?-1:options.OrderBy(k=>(Point(k)-p).sqrMagnitude).First();}
        public bool Reaches(Vector2 p)=>Endpoint(p)>=0;
        sealed class Heap
        {
            readonly List<(int id,float score)> items=new List<(int,float)>();public int Count=>items.Count;
            public void Push(int id,float score){items.Add((id,score));int k=items.Count-1;while(k>0){int p=(k-1)/2;if(items[p].score<=score)break;items[k]=items[p];k=p;}items[k]=(id,score);}
            public int Pop(){int result=items[0].id;var last=items[items.Count-1];items.RemoveAt(items.Count-1);if(items.Count==0)return result;int k=0;while(k*2+1<items.Count){int child=k*2+1;if(child+1<items.Count&&items[child+1].score<items[child].score)child++;if(items[child].score>=last.score)break;items[k]=items[child];k=child;}items[k]=last;return result;}
        }
        public Vector2[] Route(Vector2 start,Vector2 end)
        {
            if(Clear(start,end))return Rounded(new[]{start,end});int first=Endpoint(start),last=Endpoint(end);if(first<0||last<0)throw new Exception("Unreachable parcel entry "+parcel.id+" "+end);var cost=Enumerable.Repeat(float.MaxValue,free.Length).ToArray();var previous=Enumerable.Repeat(-1,free.Length).ToArray();var closed=new bool[free.Length];var heap=new Heap();cost[first]=0;heap.Push(first,0);while(heap.Count>0){int k=heap.Pop();if(closed[k])continue;if(k==last)break;closed[k]=true;foreach(int q in Neighbours(k)){if(closed[q])continue;float score=cost[k]+Vector2.Distance(Point(k),Point(q));if(score>=cost[q])continue;cost[q]=score;previous[q]=k;heap.Push(q,score+Vector2.Distance(Point(q),Point(last)));}}
            if(first!=last&&previous[last]<0)throw new Exception("Route search failed "+parcel.id);var path=new List<Vector2>{end};int at=last;path.Add(Point(at));while(at!=first){at=previous[at];path.Add(Point(at));}path.Add(start);path.Reverse();var result=new List<Vector2>{start};int index=0;while(index<path.Count-1){int next=path.Count-1;while(next>index+1&&!Clear(path[index],path[next]))next--;if(Vector2.Distance(result.Last(),path[next])>.001f)result.Add(path[next]);index=next;}return Rounded(result.ToArray());
        }
        public Vector2[] Rounded(Vector2[] source)
        {
            var simplified=new List<Vector2>();foreach(var point in source){if(simplified.Count>0&&Vector2.Distance(simplified.Last(),point)<.001f)continue;while(simplified.Count>=2&&Vector2.Dot((point-simplified.Last()).normalized,(simplified.Last()-simplified[simplified.Count-2]).normalized)>.9999f)simplified.RemoveAt(simplified.Count-1);simplified.Add(point);}var path=simplified.ToArray();
            var result=new List<Vector2>{path[0]};for(int k=1;k<path.Length-1;k++){var p=path[k];float distance=Mathf.Min(1.25f,Mathf.Min(Vector2.Distance(path[k-1],p),Vector2.Distance(p,path[k+1]))*.35f);var a=p+(path[k-1]-p).normalized*distance;var b=p+(path[k+1]-p).normalized*distance;var arc=Enumerable.Range(0,9).Select(i=>{float t=i/8f;return (1-t)*(1-t)*a+2*(1-t)*t*p+t*t*b;}).ToArray();if(Enumerable.Range(1,arc.Length-1).All(i=>Clear(arc[i-1],arc[i]))){result.AddRange(arc);}else result.Add(p);}result.Add(path.Last());return Sample(result.ToArray());
        }
        static Vector2[] Sample(Vector2[] path){var result=new List<Vector2>{path[0]};for(int k=1;k<path.Length;k++){int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(path[k-1],path[k])/.4f));for(int i=1;i<=n;i++)result.Add(Vector2.Lerp(path[k-1],path[k],(float)i/n));}return result.ToArray();}
    }
    readonly NemoPlan plan;readonly Grid[] grids;readonly Dictionary<int,List<Vector2[]>> streets=new Dictionary<int,List<Vector2[]>>();readonly NemoPlaced[] features;public NemoRoadOutlet[] Outlets;
    public NemoRoadPlanner(NemoPlan plan,List<Rect> obstacles,NemoPlaced[] features){this.plan=plan;this.features=features;grids=plan.parcels.Select(p=>new Grid(p,obstacles)).ToArray();foreach(var p in plan.parcels){var list=new List<Vector2[]>();foreach(var feature in features.Where(f=>f.parcel==p.id)){var entry=feature.foot+Vector2.down*.65f;if(!grids[p.id].Reaches(entry))throw new Exception("Facility approach disconnected "+p.id+" "+feature.prototype);list.Add(grids[p.id].Route(p.hub,entry));}streets[p.id]=list;}}
    public float Width(NemoLink link)=>plan.parcels[link.a].large||plan.parcels[link.b].large?1.65f:plan.parcels[link.a].land&&plan.parcels[link.b].land?.85f:1.05f;
    static bool Seam(Rect a,Rect b,out Vector2 center,out Vector2 normal,out Vector2 axis,out float length)
    {
        center=normal=axis=Vector2.zero;length=0;if(a.xMax==b.xMin||a.xMin==b.xMax){float lo=Mathf.Max(a.yMin,b.yMin),hi=Mathf.Min(a.yMax,b.yMax);length=hi-lo;center=new Vector2(a.xMax==b.xMin?a.xMax:a.xMin,(lo+hi)/2);normal=a.xMax==b.xMin?Vector2.right:Vector2.left;axis=Vector2.up;}else if(a.yMax==b.yMin||a.yMin==b.yMax){float lo=Mathf.Max(a.xMin,b.xMin),hi=Mathf.Min(a.xMax,b.xMax);length=hi-lo;center=new Vector2((lo+hi)/2,a.yMax==b.yMin?a.yMax:a.yMin);normal=a.yMax==b.yMin?Vector2.up:Vector2.down;axis=Vector2.right;}return length>=3;
    }
    Vector2[] Local(NemoParcel parcel,Vector2 port,Vector2 normal)
    {
        var grid=grids[parcel.id];var inner=port-normal*.95f;var bases=streets[parcel.id];var chosen=bases.SelectMany(path=>Enumerable.Range(0,path.Length).Select(k=>(path,k))).OrderBy(x=>(x.path[x.k]-inner).sqrMagnitude).FirstOrDefault();Vector2[] route;
        if(chosen.path!=null){route=chosen.path.Take(chosen.k+1).Concat(grid.Route(chosen.path[chosen.k],inner).Skip(1)).ToArray();}else route=grid.Route(parcel.hub,inner);return grid.Rounded(route.Concat(new[]{port}).ToArray());
    }
    public void Build()
    {
        var candidates=new List<NemoLink>();var normals=new Dictionary<NemoLink,Vector2>();var old=plan.links;var rng=new System.Random(plan.seed+45133);
        for(int a=0;a<plan.parcels.Length;a++)for(int b=a+1;b<plan.parcels.Length;b++){var pa=plan.parcels[a];var pb=plan.parcels[b];if(pa.large&&pb.large||pa.quiet&&pb.quiet)continue;if(!Seam(pa.rect,pb.rect,out var center,out var normal,out var axis,out float span))continue;var prior=old.FirstOrDefault(e=>e.a==a&&e.b==b);var ports=new List<Vector2>();if(prior!=null)ports.Add(prior.port);int count=Mathf.Max(1,Mathf.FloorToInt((span-2.8f)/.5f));for(int i=0;i<=count;i++)ports.Add(center+axis*Mathf.Lerp(-span/2+1.4f,span/2-1.4f,(float)i/Mathf.Max(1,count)));
            foreach(var port in ports.OrderBy(p=>prior==null?Vector2.Distance(p,center):Vector2.Distance(p,prior.port))){var inA=port-normal*.95f;var inB=port+normal*.95f;if(!grids[a].Reaches(inA)||!grids[b].Reaches(inB)||!grids[a].Clear(inA,port-normal*.001f)||!grids[b].Clear(inB,port+normal*.001f))continue;var e=new NemoLink{a=a,b=b,port=port};candidates.Add(e);normals[e]=normal;break;}
        }
        var selected=new List<NemoLink>();var active=new HashSet<int>();var parent=new int[plan.parcels.Length];var major=new int[parent.Length];int Find(int k){while(parent[k]!=k)k=parent[k];return k;}bool Verge(NemoLink e)=>(plan.parcels[e.a].land||plan.parcels[e.a].large)&&(plan.parcels[e.b].land||plan.parcels[e.b].large);bool Allowed(NemoLink e)=>!Verge(e)||Find(e.a)==Find(e.b)||major[Find(e.a)]+major[Find(e.b)]<=1;
        void Track(NemoLink e){if(!Verge(e))return;int a=Find(e.a),b=Find(e.b);if(a!=b){parent[b]=a;major[a]+=major[b];}}
        bool Complete()=>plan.parcels.All(p=>p.land||active.Contains(p.id));int attempt=0;
        while(attempt++<128){selected.Clear();active.Clear();active.Add(0);for(int k=0;k<parent.Length;k++){parent[k]=k;major[k]=plan.parcels[k].large?1:0;}while(!Complete()){var options=candidates.Where(e=>active.Contains(e.a)!=active.Contains(e.b)&&Allowed(e)).ToArray();if(options.Length==0)break;var weighted=options.Select(e=>(edge:e,score:rng.NextDouble()+(old.Any(o=>o.a==e.a&&o.b==e.b)?-.5:0))).OrderBy(x=>x.score).First().edge;selected.Add(weighted);Track(weighted);active.Add(weighted.a);active.Add(weighted.b);}if(Complete())break;}
        if(!Complete())throw new Exception("Designed roads cannot connect locations seed "+plan.seed+" missing "+string.Join(",",plan.parcels.Where(p=>!p.land&&!active.Contains(p.id)).Select(p=>p.id)));
        bool trimmed;do{trimmed=false;foreach(var p in plan.parcels.Where(p=>p.land)){var edges=selected.Where(e=>e.a==p.id||e.b==p.id).ToArray();if(edges.Length==1){selected.Remove(edges[0]);trimmed=true;}}}while(trimmed);
        var outlets=new List<NemoRoadOutlet>();foreach(var e in selected){var n=normals[e];var a=plan.parcels[e.a];var b=plan.parcels[e.b];e.points=Local(a,e.port,n).Concat(Local(b,e.port,-n).Reverse().Skip(1)).ToArray();float width=Width(e);outlets.Add(new NemoRoadOutlet{parcel=e.a,neighbour=e.b,position=e.port,direction=n,width=width});outlets.Add(new NemoRoadOutlet{parcel=e.b,neighbour=e.a,position=e.port,direction=-n,width=width});}Outlets=outlets.ToArray();plan.links=selected.ToArray();foreach(var p in plan.parcels){p.degree=selected.Count(e=>e.a==p.id||e.b==p.id);p.shape=p.degree<=1?"pocket":p.degree==2?"bend-or-pass":p.degree==3?"three-way":p.degree==4?"four-way":"many-mouth-junction";}
    }
    public NemoApproach[] Approaches(NemoPlaced[] records)=>records.Select(f=>new NemoApproach{parcel=f.parcel,prototype=f.prototype,points=grids[f.parcel].Route(plan.parcels[f.parcel].hub,f.foot+Vector2.down*.65f)}).ToArray();
    public NemoApproach[] WorkApproaches(NemoPlaced[] records){var result=new List<NemoApproach>();foreach(var f in records.Where(f=>!f.critical&&!f.tree&&NemoMap.IsWorkSite(f.prototype))){var grid=grids[f.parcel];var preferred=f.foot+Vector2.down*.65f;var options=new List<Vector2>{preferred};for(float y=-2;y<=1;y+=.25f)for(float x=-2.5f;x<=2.5f;x+=.25f)options.Add(preferred+new Vector2(x,y));var candidates=options.Where(grid.Reaches).OrderBy(p=>(p-preferred).sqrMagnitude).ToArray();if(candidates.Length==0)throw new Exception("Work site has no reachable entry: "+f.prototype+" parcel "+f.parcel);result.Add(new NemoApproach{parcel=f.parcel,prototype=f.prototype+" work yard",points=grid.Route(plan.parcels[f.parcel].hub,candidates[0])});}return result.ToArray();}
    public NemoApproach BankApproach(int parcel,Vector2 bank,string name)=>new NemoApproach{parcel=parcel,prototype=name,points=grids[parcel].Route(plan.parcels[parcel].hub,bank)};
}
