using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Presentation only: the existing roads, water, owners and collision recipe stay authoritative.
[ExecuteAlways]
public class NemoVisualFinish : MonoBehaviour
{
    public int Version=1,TransitionStamps,DetailClusters;
    public string Reference="Approved four-panel proposal / StudyA R4";
    public Texture2D DirtTexture;public MeshRenderer[] DirtTargets;
    void OnEnable(){ApplyTerrainBlocks();}
    public void ApplyTerrainBlocks(){if(DirtTexture==null||DirtTargets==null)return;var properties=new MaterialPropertyBlock();foreach(var target in DirtTargets){if(target==null)continue;target.GetPropertyBlock(properties);properties.SetTexture("_MainTex",DirtTexture);properties.SetColor("_Color",target.name.StartsWith("native earth bank")?new Color(.64f,.51f,.40f,1):Color.white);target.SetPropertyBlock(properties);}}
}

public partial class NemoMap
{
    public bool EnableVisualFinish=true;
    public NemoVisualFinish VisualFinish;
    public Texture2D[] FinishGrassSources;public Texture2D FinishDirtSource;public Sprite[] FinishGroundDetails;
    struct FinishTriangle { public Vector2 a,b,c; public Rect bounds; }
    sealed class FinishQuads
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        public readonly List<Vector2> uv=new List<Vector2>();
        public readonly List<int> triangles=new List<int>();
        public void Add(Vector2 center,Vector2 size,Rect textureRect)
        {
            int n=vertices.Count;Vector2 lo=center-size*.5f,hi=center+size*.5f;
            vertices.Add(new Vector3(lo.x,lo.y));vertices.Add(new Vector3(hi.x,lo.y));vertices.Add(new Vector3(hi.x,hi.y));vertices.Add(new Vector3(lo.x,hi.y));
            uv.Add(textureRect.min);uv.Add(new Vector2(textureRect.xMax,textureRect.yMin));uv.Add(textureRect.max);uv.Add(new Vector2(textureRect.xMin,textureRect.yMax));
            triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
        }
    }
    static uint FinishHash(int x,int y,int seed=0)
    {
        unchecked { uint h=(uint)(x*374761393+y*668265263+seed*1274126177);h=(h^(h>>13))*1274126177;return h^(h>>16); }
    }
    static float FinishCross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static bool FinishInside(FinishTriangle t,Vector2 p)
    {
        if(!t.bounds.Contains(p))return false;
        float a=FinishCross(t.b-t.a,p-t.a),b=FinishCross(t.c-t.b,p-t.b),c=FinishCross(t.a-t.c,p-t.c);
        return (a>=0&&b>=0&&c>=0)||(a<=0&&b<=0&&c<=0);
    }
    Color32[] FinishRead(Texture2D source)
    {
        if(source.isReadable)return source.GetPixels32();var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);var prior=RenderTexture.active;Graphics.Blit(source,rt);RenderTexture.active=rt;
        var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();var pixels=copy.GetPixels32();UnityEngine.Object.DestroyImmediate(copy);RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);return pixels;
    }
    Texture2D FinishAtlas()
    {
        var texture=new Texture2D(256,192,TextureFormat.RGBA32,false);texture.name="Nemo native-pixel grass soil transition atlas";
        var pixels=new Color32[256*192];
        Color32[] grass={new Color32(66,91,36,255),new Color32(85,113,41,255),new Color32(109,134,47,255),new Color32(139,157,58,255),new Color32(77,102,38,255)};
        Color32[] soil={new Color32(183,129,65,255),new Color32(205,148,79,255),new Color32(220,164,89,255),new Color32(163,112,57,255)};
        for(int variant=0;variant<16;variant++)for(int y=0;y<48;y++)for(int x=0;x<64;x++)
        {
            uint h=FinishHash(x/3,y/3,variant+43);float nx=(x-31.5f)/31.5f,ny=(y-20f)/23f;
            bool turf=variant<12;
            float boundary=.11f+.04f*Mathf.Sin(x*.35f+variant)+.035f*Mathf.Sin(x*.77f+variant*3);
            bool on=turf?(nx*nx+ny*ny<boundary+(h%100)*.0008f && h%13!=0):(nx*nx+ny*ny<.50f && h%7==0);
            if(turf && y>20 && y<43){int stem=(x+variant*7)%13;on|=stem<=1&&y<29+(FinishHash(x/13,variant)%13)&&Mathf.Abs(nx)<.28f;}
            if(!on)continue;
            Color32 c=turf?grass[FinishHash(x/2,y/2,variant)%grass.Length]:soil[FinishHash(x/2,y/2,variant)%soil.Length];
            pixels[(variant/4*48+y)*256+variant%4*64+x]=c;
        }
        if(FinishGroundDetails!=null&&FinishGroundDetails.Length>=4)
        {
            for(int variant=0;variant<12;variant++)
            {
                var source=FinishGroundDetails[variant%4];if(source==null)continue;var data=FinishRead(source.texture);var rect=source.rect;
                for(int y=0;y<48;y++)for(int x=0;x<64;x++)pixels[(variant/4*48+y)*256+variant%4*64+x]=new Color32(0,0,0,0);
                int dx=32-(int)rect.width/2,dy=8;
                for(int y=0;y<rect.height&&y+dy<48;y++)for(int x=0;x<rect.width&&x+dx<64;x++)
                {
                    int sx=variant%3==1?(int)rect.width-1-x:x;var c=data[((int)rect.y+y)*source.texture.width+(int)rect.x+sx];pixels[(variant/4*48+y+dy)*256+variant%4*64+x+dx]=c;
                }
            }
        }
        texture.SetPixels32(pixels);texture.Apply();texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;Transient.Add(texture);return texture;
    }
    Rect FinishUV(int variant)=>new Rect((variant%4)*.25f,(variant/4)*.25f,.25f,.25f);
    void FinishMesh(string name,FinishQuads data,Texture2D texture,int order,Transform parent)
    {
        if(data.vertices.Count==0)return;
        var mesh=new Mesh();mesh.name=name;mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(data.vertices);mesh.SetTriangles(data.triangles,0);mesh.SetUVs(0,data.uv);mesh.RecalculateBounds();Transient.Add(mesh);
        var go=Group(name,parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var render=go.AddComponent<MeshRenderer>();render.sharedMaterial=Mat(texture);render.sortingOrder=order;
    }
    Material FinishTint(MeshRenderer renderer,Color tint)
    {
        var material=new Material(renderer.sharedMaterial);material.color=tint;Transient.Add(material);renderer.sharedMaterial=material;return material;
    }
    Texture2D FinishGrass()
    {
        var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.name="Nemo native-pixel subdued meadow";
        Color32[] palette={new Color32(83,112,42,255),new Color32(91,120,45,255),new Color32(103,131,48,255),new Color32(112,139,52,255),new Color32(122,147,55,255)};
        var pixels=new Color32[65536];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float broad=Mathf.Sin(x*.045f+y*.027f)+Mathf.Sin(x*.021f-y*.073f)*.5f;
            int index=Mathf.Clamp((int)(2+broad*.40f+(FinishHash(x/3,y/2,59)%100)/90f-.55f),0,4);
            pixels[x+y*256]=palette[index];
        }
        if(FinishGrassSources!=null&&FinishGrassSources.Length>0)
        {
            var data=FinishGrassSources.Select(FinishRead).ToArray();
            for(int y=0;y<256;y++)for(int x=0;x<256;x++){int variant=(int)(FinishHash(x/64,y/64,37)%(uint)data.Length);var source=FinishGrassSources[variant];pixels[x+y*256]=data[variant][(y%source.height)*source.width+x%source.width];}
        }
        texture.SetPixels32(pixels);texture.Apply();texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Repeat;Transient.Add(texture);return texture;
    }
    Texture2D FinishWater()
    {
        var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.name="Nemo native-pixel varied current";
        Color32[] palette={new Color32(17,78,121,255),new Color32(20,91,133,255),new Color32(23,105,145,255),new Color32(27,117,154,255),new Color32(35,130,166,255)};
        var pixels=new Color32[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float broad=Mathf.Sin(x*.055f+y*.075f)+Mathf.Sin(x*.15f-y*.035f)*.65f;
            int color=Mathf.Clamp((int)(2+broad*.65f+(FinishHash(x/3,y/2,84)%100)/160f-.3f),0,4);
            pixels[x+y*256]=palette[color];
            uint h=FinishHash(x/29,y/39,197);
            if((x+(int)(h%19))%29<4 && (y+(int)(h%27))%39==0 && h%3!=0)pixels[x+y*256]=new Color32(148,199,207,255);
        }
        texture.SetPixels32(pixels);texture.Apply();texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Repeat;Transient.Add(texture);return texture;
    }
    void FinishMillWater(Texture2D water)
    {
        foreach(var sr in World.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.sprite!=null&&r.sprite.name=="mill-channel").ToArray())
        {
            var original=sr.sprite;var source=original.texture;Color32[] colors;
            if(source.isReadable)colors=source.GetPixels32();else
            {
                var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);var prior=RenderTexture.active;Graphics.Blit(source,rt);RenderTexture.active=rt;
                var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();colors=copy.GetPixels32();UnityEngine.Object.DestroyImmediate(copy);RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);
            }
            var waves=water.GetPixels32();for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)
            {
                int k=x+y*source.width;var c=colors[k];bool blue=c.b>c.r*1.15f&&c.b>c.g*1.03f;bool stroke=c.r>175&&c.g>175&&c.b>175&&x>32&&x<112;
                bool core=x>33&&x<111;bool stone=c.r>70&&Math.Abs(c.r-c.g)<15&&Math.Abs(c.g-c.b)<15;if(c.a!=0&&(blue||stroke||(core&&!stone))){var value=waves[x%256+(y%256)*256];value.a=c.a;colors[k]=value;}
            }
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);texture.name="Native mill channel with matched water";texture.SetPixels32(colors);texture.Apply();texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;Transient.Add(texture);
            var sprite=Sprite.Create(texture,original.rect,new Vector2(original.pivot.x/original.rect.width,original.pivot.y/original.rect.height),64,0,SpriteMeshType.FullRect);sprite.name="mill-channel-finish";Transient.Add(sprite);sr.sprite=sprite;BindSpriteMaterial(sr);
        }
    }
    public void ApplyVisualFinish()
    {
        if(!EnableVisualFinish||VisualFinish!=null)return;
        var root=Group("Visual finish | grass soil bank contact | no collision");VisualFinish=root.AddComponent<NemoVisualFinish>();
        var nativeFlowers=FinishGroundDetails==null?null:FinishGroundDetails.Skip(4).Select(source=>{if(source.pixelsPerUnit==64)return source;var sprite=Sprite.Create(source.texture,source.rect,new Vector2(source.pivot.x/source.rect.width,source.pivot.y/source.rect.height),64,0,SpriteMeshType.FullRect);sprite.name=source.name+"-native64";Transient.Add(sprite);return sprite;}).ToArray();
        var oldMeshes=World.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null&&!f.transform.IsChildOf(root.transform)).ToArray();
        var soil=new List<FinishTriangle>();var grid=new Dictionary<Vector2Int,List<FinishTriangle>>();
        var edges=new List<(Vector2 a,Vector2 b)>();
        foreach(var filter in oldMeshes)
        {
            var render=filter.GetComponent<MeshRenderer>();if(render==null)continue;
            if(render.sharedMaterial.mainTexture==Library.Grass[0]&&!filter.name.Contains("beyond")){var material=new Material(render.sharedMaterial);material.mainTexture=FinishGrass();Transient.Add(material);render.sharedMaterial=material;filter.sharedMesh.uv=filter.sharedMesh.vertices.Select(v=>new Vector2(v.x*64/256,v.y*64/256)).ToArray();}
            if(render.sharedMaterial.mainTexture!=Library.Dirt)continue;
            bool bank=filter.name.StartsWith("native earth bank");
            FinishTint(render,bank?new Color(.54f,.46f,.47f,1):new Color(.88f,.86f,1.02f,1));
            if(bank)continue;
            var verts=filter.sharedMesh.vertices.Select(v=>(Vector2)filter.transform.TransformPoint(v)).ToArray();var indices=filter.sharedMesh.triangles;
            var counts=new Dictionary<(int,int),int>();
            void Edge(int a,int b){var key=a<b?(a,b):(b,a);counts[key]=counts.TryGetValue(key,out int n)?n+1:1;}
            for(int k=0;k<indices.Length;k+=3)
            {
                int ia=indices[k],ib=indices[k+1],ic=indices[k+2];var min=Vector2.Min(verts[ia],Vector2.Min(verts[ib],verts[ic]));var max=Vector2.Max(verts[ia],Vector2.Max(verts[ib],verts[ic]));
                var tri=new FinishTriangle{a=verts[ia],b=verts[ib],c=verts[ic],bounds=new Rect(min,max-min)};soil.Add(tri);
                for(int y=Mathf.FloorToInt(min.y/2);y<=Mathf.FloorToInt(max.y/2);y++)for(int x=Mathf.FloorToInt(min.x/2);x<=Mathf.FloorToInt(max.x/2);x++)
                {var key=new Vector2Int(x,y);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<FinishTriangle>();list.Add(tri);}
                Edge(ia,ib);Edge(ib,ic);Edge(ic,ia);
            }
            foreach(var edge in counts.Where(e=>e.Value==1))edges.Add((verts[edge.Key.Item1],verts[edge.Key.Item2]));
        }
        if(FinishDirtSource!=null){var dirt=new Texture2D(FinishDirtSource.width,FinishDirtSource.height,TextureFormat.RGBA32,false);dirt.name="Native dirt with point repeating sampling";dirt.SetPixels32(FinishRead(FinishDirtSource));dirt.Apply();dirt.filterMode=FilterMode.Point;dirt.wrapMode=TextureWrapMode.Repeat;Transient.Add(dirt);VisualFinish.DirtTexture=dirt;}
        VisualFinish.DirtTargets=oldMeshes.Select(f=>f.GetComponent<MeshRenderer>()).Where(r=>r!=null&&r.sharedMaterial.mainTexture==Library.Dirt).ToArray();VisualFinish.ApplyTerrainBlocks();
        bool OnSoil(Vector2 p)=>grid.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x/2),Mathf.FloorToInt(p.y/2)),out var list)&&list.Any(t=>FinishInside(t,p));
        bool Interior(Vector2 p)=>p.x>1&&p.x<143&&p.y>1&&p.y<107;
        bool Water(Vector2 p)=>RiverNetwork!=null&&RiverNetwork.WaterBodies.Any(c=>c.bounds.Contains(p));
        var atlas=FinishAtlas();var turfQuads=new FinishQuads();var earthQuads=new FinishQuads();
        foreach(var edge in edges)
        {
            var dir=(edge.b-edge.a).normalized;var normal=new Vector2(-dir.y,dir.x);int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(edge.a,edge.b)/.30f));
            for(int k=0;k<count;k++)
            {
                var p=Vector2.Lerp(edge.a,edge.b,(k+.5f)/count);if(!Interior(p)||Water(p))continue;
                bool left=OnSoil(p+normal*.08f),right=OnSoil(p-normal*.08f);if(left==right)continue;var outward=left?-normal:normal;
                uint h=FinishHash(Mathf.RoundToInt(p.x*64),Mathf.RoundToInt(p.y*64),Seed+93);if(h%3==0||FinishHash(Mathf.FloorToInt(p.x/2),Mathf.FloorToInt(p.y/2),Seed+149)%5==0)continue;
                var center=p-outward*(.06f+(h%13)/100f);center=new Vector2(Mathf.Round(center.x*64)/64,Mathf.Round(center.y*64)/64);
                turfQuads.Add(center,new Vector2(1,.75f),FinishUV((int)(h%12)));VisualFinish.TransitionStamps++;
                if(h%5==0)earthQuads.Add(p+outward*.18f,new Vector2(1,.75f),FinishUV(12+(int)(h%4)));
            }
        }
        if(RiverNetwork!=null)
        {
            var water=FinishWater();FinishMillWater(water);
            foreach(var f in oldMeshes.Where(f=>f.name.StartsWith("native deep water")||f.name.StartsWith("native shallow shoreline")))
            {
                var mat=new Material(f.GetComponent<MeshRenderer>().sharedMaterial);mat.mainTexture=water;if(f.name.StartsWith("native shallow shoreline"))mat.color=new Color(.73f,.86f,.92f,1);Transient.Add(mat);f.GetComponent<MeshRenderer>().sharedMaterial=mat;
                f.sharedMesh.uv=f.sharedMesh.vertices.Select(v=>new Vector2(v.x*64/256,v.y*64/256)).ToArray();
            }
            foreach(var block in RiverNetwork.Design.blocks)for(int segment=1;segment<block.points.Length;segment++)
            {
                var a=block.points[segment-1];var b=block.points[segment];var dir=(b-a).normalized;var n=new Vector2(-dir.y,dir.x);int count=Mathf.CeilToInt(Vector2.Distance(a,b)/.35f);
                for(int k=0;k<count;k++)for(int side=-1;side<=1;side+=2)
                {
                    float t=(k+.5f)/count;var p=Vector2.Lerp(a,b,t);float width=Mathf.Lerp(block.widths[segment-1],block.widths[segment],t)/2;
                    var edge=p+n*side*(width+.50f);if(!Interior(edge)||OnSoil(edge)||AllBridges.Any(bridge=>Mathf.Abs(edge.y-bridge.West.y)<1.0f&&Mathf.Abs(edge.x-(bridge.West.x+bridge.East.x)/2)<3.4f))continue;
                    uint h=FinishHash(Mathf.RoundToInt(edge.x*32),Mathf.RoundToInt(edge.y*32),Seed+187);
                    turfQuads.Add(edge,new Vector2(1,.75f),FinishUV((int)(h%12)));VisualFinish.TransitionStamps++;
                }
            }
            var reeds=Library.River.SelectMany(owner=>owner.visuals).Where(v=>v.sprite!=null&&(v.path.IndexOf("plant",StringComparison.OrdinalIgnoreCase)>=0||v.path.IndexOf("reeds",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();var plantedShore=new List<Vector2>();
            foreach(var block in RiverNetwork.Design.blocks)for(int segment=1;segment<block.points.Length;segment++)
            {
                var a=block.points[segment-1];var b=block.points[segment];var dir=(b-a).normalized;var normal=new Vector2(-dir.y,dir.x);int count=Mathf.CeilToInt(Vector2.Distance(a,b)/3.5f);
                for(int k=0;k<count;k++)for(int side=-1;side<=1;side+=2)
                {
                    float t=(k+.5f)/count;var p=Vector2.Lerp(a,b,t);float width=Mathf.Lerp(block.widths[segment-1],block.widths[segment],t)/2;
                    var foot=p+normal*side*(width+.30f);if(!Interior(foot)||OnSoil(foot)||AllBridges.Any(bridge=>Mathf.Abs(foot.y-bridge.West.y)<1.6f&&Mathf.Abs(foot.x-(bridge.West.x+bridge.East.x)/2)<4.0f))continue;
                    uint h=FinishHash(Mathf.RoundToInt(foot.x*16),Mathf.RoundToInt(foot.y*16),Seed+479);if(h%3==0||plantedShore.Any(p0=>Vector2.Distance(p0,foot)<2.8f))continue;plantedShore.Add(foot);
                    var group=Group("Shore vegetation group",root.transform);
                    if(reeds.Length>0){var source=reeds[h%reeds.Length];var sr=group.AddComponent<SpriteRenderer>();sr.sprite=source.sprite;BindSpriteMaterial(sr);sr.sortingOrder=10000-Mathf.RoundToInt(foot.y*64);group.transform.position=foot;}
                    if(h%4==0){var rock=Library.Get("rock-cluster");var at=p+normal*side*(width-.16f);if(rock!=null)foreach(var visual in rock.visuals)Visual(group.transform,visual,at,at.y);}
                    VisualFinish.DetailClusters++;
                }
            }
            foreach(var bridge in AllBridges)
            {
                var shadowTexture=new Texture2D(32,16,TextureFormat.RGBA32,false);var pixels=new Color32[512];
                for(int y=0;y<16;y++)for(int x=0;x<32;x++)if(x>2+(y%3)&&x<29-(y%2)&&y<12)pixels[x+y*32]=new Color32(42,34,23,(byte)(y<7?65:32));
                shadowTexture.SetPixels32(pixels);shadowTexture.Apply();shadowTexture.filterMode=FilterMode.Point;Transient.Add(shadowTexture);
                var data=new FinishQuads();foreach(var foot in new[]{bridge.West,bridge.East})data.Add(foot+new Vector2(.1f,-.06f),new Vector2(.50f,.25f),new Rect(0,0,1,1));
                int before=bridge.transform.childCount;FinishMesh("Bridge landing contact shadow",data,shadowTexture,-16990,bridge.transform);
                if(bridge.transform.childCount>before)bridge.Visuals=bridge.Visuals.Concat(new[]{bridge.transform.GetChild(before).gameObject}).ToArray();
            }
        }
        FinishMesh("Grass tufts interrupt existing soil edges",turfQuads,atlas,-17300,root.transform);
        FinishMesh("Small exposed soil flecks in grass",earthQuads,atlas,-17299,root.transform);
        foreach(var tree in placed.Where(p=>p.tree).ToArray())
        {
            uint h=FinishHash(Mathf.RoundToInt(tree.foot.x*64),Mathf.RoundToInt(tree.foot.y*64),Seed+311);if(h%11!=0)continue;
            var foot=tree.foot+new Vector2((h%2==0?-1:1)*.9f,-.2f);
            if(!Interior(foot)||Water(foot)||OnSoil(foot)||RouteDistance(foot)<1.3f)continue;
            var group=Group("Small woodland cluster "+VisualFinish.DetailClusters,root.transform);
            foreach(var id in new[]{"bush"})
            {
                var proto=Library.Get(id);if(proto==null)continue;var at=foot+(id=="flowers"?new Vector2(.55f,-.10f):Vector2.zero);
                foreach(var visual in proto.visuals)Visual(group.transform,visual,at,at.y);
            }
            if(nativeFlowers!=null&&nativeFlowers.Length>=3){var flower=nativeFlowers[h%3];var go=Group("Native coloured flowers",group.transform);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=flower;BindSpriteMaterial(sr);go.transform.position=foot+new Vector2(.45f,-.14f);sr.sortingOrder=10000-Mathf.RoundToInt(foot.y*64);}
            else {var flowers=Library.Get("flowers");if(flowers!=null)foreach(var visual in flowers.visuals)Visual(group.transform,visual,foot+new Vector2(.45f,-.14f),foot.y);}
            if(h%3==0)
            {
                var proto=Library.Get("rock-cluster");var at=tree.foot+new Vector2(-.40f,.16f);
                if(proto!=null)foreach(var visual in proto.visuals)Visual(group.transform,visual,at,at.y);
            }
            VisualFinish.DetailClusters++;
        }
        var shadowTex=new Texture2D(32,16,TextureFormat.RGBA32,false);var shadowPixels=new Color32[512];for(int y=0;y<16;y++)for(int x=0;x<32;x++)if(x>3+(y%2)&&x<28-(y%3)&&y<12)shadowPixels[x+y*32]=new Color32(31,48,20,(byte)(y<8?68:38));shadowTex.SetPixels32(shadowPixels);shadowTex.Apply();shadowTex.filterMode=FilterMode.Point;Transient.Add(shadowTex);var shadows=new FinishQuads();
        foreach(var tree in placed.Where(p=>p.tree&&Interior(p.foot))){shadows.Add(tree.foot+new Vector2(.19f,-.08f),new Vector2(1.0f,.35f),new Rect(0,0,1,1));}
        FinishMesh("Short woodland contact shadows",shadows,shadowTex,-17301,root.transform);
        foreach(Transform owner in World.transform)if(owner.name.Contains(" / tree-"))foreach(var sr in owner.GetComponentsInChildren<SpriteRenderer>())if(sr.name.Contains("canopy")){uint h=FinishHash(Mathf.RoundToInt(sr.transform.position.x*64),Mathf.RoundToInt(sr.transform.position.y*64),Seed+829);float shade=(h%5)*.018f;sr.color=new Color(1-shade,1-shade*.55f,1-shade*.30f,sr.color.a);}
    }
}
