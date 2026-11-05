using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;

// Captured only after roads, courts, facilities and the existing native forest are composed.
// The physical boundary consumes this snapshot and never furnishes its own boundary.
public class NemoBlockDesignFreeze : MonoBehaviour
{
    public NemoBoundarySupport[] Forest;
    public MeshFilter[] Ground;
    public int SpriteCount;
    public string VisualDigest;
    public bool NoVisualMutation;
    public static string Digest(NemoMap map,Transform exclude=null){var text=new StringBuilder();foreach(var r in map.World.GetComponentsInChildren<SpriteRenderer>().Where(s=>exclude==null||!s.transform.IsChildOf(exclude)))text.Append(r.name).Append('|').Append(r.sprite.name).Append('|').Append(r.sprite.rect).Append('|').Append(r.transform.position.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(r.transform.position.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(r.color).Append('|').Append(r.flipX).Append('|').Append(r.flipY).Append(';');foreach(var f in map.World.GetComponentsInChildren<MeshFilter>().Where(s=>exclude==null||!s.transform.IsChildOf(exclude))){text.Append(f.transform.position.x).Append(f.transform.position.y);foreach(var v in f.sharedMesh.vertices)text.Append(v.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(v.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(";");foreach(int t in f.sharedMesh.triangles)text.Append(t).Append(",");}using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));}
    public static NemoBlockDesignFreeze Capture(NemoMap map){var result=new GameObject("Fixed block level design | before physical boundaries").AddComponent<NemoBlockDesignFreeze>();result.transform.SetParent(map.World.transform);result.Ground=map.World.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>().sharedMaterial.mainTexture==map.Library.Dirt).ToArray();result.SpriteCount=map.World.GetComponentsInChildren<SpriteRenderer>().Length;result.VisualDigest=Digest(map);result.Forest=map.World.transform.Cast<Transform>().Where(t=>t.name.Contains(" / tree-")).Select(t=>{string id=t.name.Substring(t.name.LastIndexOf(" / ")+3);var proto=map.Library.Get(id);var sprites=t.GetComponentsInChildren<SpriteRenderer>();return new NemoBoundarySupport{prototype=id,tree=true,foot=(Vector2)sprites[0].transform.position-new Vector2(proto.visuals[0].x,proto.visuals[0].y),visuals=sprites};}).ToArray();if(map.Exterior!=null)result.Forest=result.Forest.Concat(map.Exterior.Forest).ToArray();if(result.Forest.Length==0)throw new Exception("No completed native forest design");return result;}
    public void VerifyUnchanged(NemoMap map){NoVisualMutation=SpriteCount==map.World.GetComponentsInChildren<SpriteRenderer>().Length&&VisualDigest==Digest(map);if(!NoVisualMutation)throw new Exception("Physical boundary changed the fixed block visual design");}
}
