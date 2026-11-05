using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// A deep continuation of the existing woodland, designed before physical contours.
public class NemoExteriorForest : MonoBehaviour
{
    public const float Depth=32;
    public NemoBoundarySupport[] Forest;
    public string InteriorDigestBefore,InteriorDigestAfter;
    public bool InteriorPreserved;
    public int Version=7;
}
public partial class NemoMap
{
    public NemoExteriorForest Exterior;
    public void ExtendExteriorDesign()
    {
        if(Exterior!=null)return;
        var before=NemoBlockDesignFreeze.Digest(this);
        var root=Group("Existing woodland continues beyond all map edges | v7");
        Exterior=root.AddComponent<NemoExteriorForest>();
        var trees=Library.Prototypes.Where(p=>p.id.StartsWith("tree-")).ToArray();
        var random=new System.Random(unchecked(Seed*739391+71007));
        var supports=new List<NemoBoundarySupport>();
        var water=RiverNetwork==null?new Rect[0]:RiverNetwork.WaterBodies.Select(c=>new Rect((Vector2)c.bounds.min,(Vector2)c.bounds.size)).ToArray();
        void Tree(Vector2 foot)
        {
            var proto=trees[random.Next(trees.Length)];var bounds=At(proto.Bounds,foot);
            if(water.Any(r=>Grow(r,.1f).Overlaps(Grow(bounds,-.3f))))return;
            if(foot.x>-3&&foot.x<147&&foot.y>-3&&foot.y<111&&proto.bodies.Any(b=>RouteOverlap(At(b.Rect,foot),.55f)))return;
            var group=Group("outer woodland / "+proto.id,root.transform);var visuals=new List<SpriteRenderer>();
            foreach(var v in proto.visuals)visuals.Add(Visual(group.transform,v,foot,foot.y,v.name=="shadow"||v.name=="contact_shadow"||v.name=="contact"));
            foreach(var b in proto.bodies)Body(group.transform,b,foot,"native outer tree body");
            supports.Add(new NemoBoundarySupport{prototype=proto.id,foot=foot,tree=true,visuals=visuals.ToArray()});
        }
        // Normal-size native trees fill an entire landscape belt, with varied forest fringes.
        // No per-block fence or reallocation of existing facilities/roads is created.
        for(float y=-NemoExteriorForest.Depth;y<=108+NemoExteriorForest.Depth;y+=2.05f)for(float x=-NemoExteriorForest.Depth;x<=144+NemoExteriorForest.Depth;x+=2.05f)
        {
            var foot=new Vector2(x+(float)random.NextDouble()*.65f,y+(float)random.NextDouble()*.65f);
            float west=.05f+.40f*Mathf.Sin(foot.y*.23f),east=143.95f+.4f*Mathf.Sin(foot.y*.18f+.8f);
            float south=-2.8f+.15f*Mathf.Sin(foot.x*.19f),north=108f+.15f*Mathf.Sin(foot.x*.17f+.4f);
            if(foot.x>west&&foot.x<east&&foot.y>south&&foot.y<north)continue;
            Tree(foot);
        }
        // A varied woodland fringe joins the existing forest to the deep exterior mass.
        // These are the same full-size 223px canopies, not scaled-down fence sprites.
        for(float y=-3;y<112;y+=1.75f){float at=y+(float)random.NextDouble()*.35f;Tree(new Vector2(-.55f+.4f*Mathf.Sin(at*.19f),at));Tree(new Vector2(144.55f+.4f*Mathf.Sin(at*.17f+.7f),at));}
        for(float x=-3;x<148;x+=1.75f){float at=x+(float)random.NextDouble()*.35f;Tree(new Vector2(at,-2.8f+.15f*Mathf.Sin(at*.19f)));Tree(new Vector2(at,108f+.15f*Mathf.Sin(at*.17f+.4f)));}
        Exterior.Forest=supports.ToArray();Exterior.InteriorDigestBefore=before;
        Exterior.InteriorDigestAfter=NemoBlockDesignFreeze.Digest(this,Exterior.transform);
        Exterior.InteriorPreserved=before==Exterior.InteriorDigestAfter;
        if(!Exterior.InteriorPreserved)throw new Exception("Exterior changed pre-existing block design");
    }
    public void UpgradeExterior()
    {
        if(Exterior!=null)return;
        ExtendExteriorDesign();
        if(Boundary!=null)DestroyImmediate(Boundary.gameObject);
        if(Design!=null)DestroyImmediate(Design.gameObject);
        Design=NemoBlockDesignFreeze.Capture(this);Boundary=NemoBoundary.Build(this);
        StabilizeSpriteDepth();Physics2D.SyncTransforms();
    }
}
