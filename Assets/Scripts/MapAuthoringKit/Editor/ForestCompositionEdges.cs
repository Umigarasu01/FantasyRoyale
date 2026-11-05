using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>既存PNGの規格化と矩形切出しだけで、必要寸法の橋と連続する水際を組み立てる。</summary>
    public static class ForestCompositionEdges
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public static IEnumerable<string> SpriteNames()
        {
            yield return "bridge_floor"; yield return "bridge_rail_far"; yield return "bridge_rail_near";
            yield return "bridge_post_far_w"; yield return "bridge_post_far_e";
            yield return "bridge_post_near_w"; yield return "bridge_post_near_e";
            yield return "bank_w"; yield return "bank_e"; yield return "bank_rim";
        }
        public static Vector2Int Dimensions(string name)
        {
            switch (name)
            {
                case "bridge_floor": return new Vector2Int(32,32);
                case "bridge_rail_far": return new Vector2Int(32,8);
                case "bridge_rail_near": return new Vector2Int(32,12);
                case "bridge_post_far_w": case "bridge_post_far_e":
                case "bridge_post_near_w": case "bridge_post_near_e": return new Vector2Int(12,24);
                case "bank_w": case "bank_e": return new Vector2Int(16,32);
                case "bank_rim": return new Vector2Int(32,16);
                default: throw new ArgumentException("未定義の切出しSpriteです。",nameof(name));
            }
        }
        public static Vector2 Pivot(string name)
        {
            if (name == "bridge_floor" || name.StartsWith("bank_",StringComparison.Ordinal)) return new Vector2(0.5f,0.5f);
            return new Vector2(0.5f,Mathf.Round(Dimensions(name).y * ForestRestartAssetBuilder.FootPivot)/Dimensions(name).y);
        }
        public static void BuildAssets()
        {
            var bridge = ForestRestartAssetBuilder.LoadPng(ForestCompositionVisuals.SpriteRoot+"/bridge.png");
            Texture2D native = null;
            try
            {
                // 9x6unitの画像をそのまま拡大配置しない。Uniform nearest半寸で木/Characterの描込粒度へ寄せ、床を反復する。
                native = ForestRestartAssetBuilder.Resize(bridge,144,96,false);
                WriteCrop(native,"bridge_floor",59,42,32,32);
                WriteCrop(native,"bridge_rail_far",48,84,32,8);
                WriteCrop(native,"bridge_rail_near",48,19,32,12);
                WriteCrop(native,"bridge_post_far_w",4,12,12,24);
                WriteCrop(native,"bridge_post_far_e",126,12,12,24);
                WriteCrop(native,"bridge_post_near_w",4,12,12,24);
                WriteCrop(native,"bridge_post_near_e",126,12,12,24);
            }
            finally { Object.DestroyImmediate(bridge); if(native != null) Object.DestroyImmediate(native); }
            var cliff = ForestRestartAssetBuilder.LoadPng(ForestCompositionVisuals.SpriteRoot+"/cliff.png");
            try
            {
                // 専用側面Artは未導入。既存の左右端と草/岩の上端をnativeで使い、水際の接地線を連続させる。
                WriteCrop(cliff,"bank_w",0,32,16,32);
                WriteCrop(cliff,"bank_e",176,32,16,32);
                WriteCrop(cliff,"bank_rim",80,64,32,16);
            }
            finally { Object.DestroyImmediate(cliff); }
        }
        private static void WriteCrop(Texture2D source,string name,int left,int bottom,int width,int height)
        {
            var pixels=source.GetPixels32(); var output=new Color32[width*height];
            for(var y=0;y<height;y++) Array.Copy(pixels,(bottom+y)*source.width+left,output,y*width,width);
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try { texture.SetPixels32(output); texture.Apply(); File.WriteAllBytes(ForestCompositionVisuals.SpriteRoot+"/"+name+".png",texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }
        private static SpriteRenderer Place(Transform parent,string name,string key,Vector2 point,bool ground)
        {
            var go=ForestRestartAssetBuilder.Child(parent,name);
            go.transform.localPosition=new Vector3(Mathf.Round(point.x*32)/32,Mathf.Round(point.y*32)/32);
            var renderer=go.AddComponent<SpriteRenderer>(); renderer.sprite=ForestCompositionVisuals.Sprite(key);
            renderer.sharedMaterial=ForestRestartAssetBuilder.GetMaterial();
            renderer.sortingLayerName=ground?"MapDetail":"WorldObjects"; renderer.sortingOrder=0; renderer.spriteSortPoint=SpriteSortPoint.Pivot;
            return renderer;
        }
        public static void Bridges(ForestRestartPlan plan,Transform parent)
        {
            var pending=new HashSet<Vector2Int>(plan.BridgeCells); var index=0;
            while(pending.Count>0)
            {
                var start=pending.OrderBy(c=>c.y).ThenBy(c=>c.x).First();
                var queue=new Queue<Vector2Int>(); var cells=new List<Vector2Int>(); queue.Enqueue(start);pending.Remove(start);
                while(queue.Count>0)
                {
                    var cell=queue.Dequeue();cells.Add(cell);
                    foreach(var d in Directions) if(pending.Remove(cell+d)) queue.Enqueue(cell+d);
                }
                var minX=cells.Min(c=>c.x);var maxX=cells.Max(c=>c.x);var minY=cells.Min(c=>c.y);var maxY=cells.Max(c=>c.y);
                if(maxX-minX+1>6 || maxY-minY+1!=3) throw new InvalidOperationException("橋は最大6Cell長、3Cell通行幅の規格です。");
                var root=ForestRestartAssetBuilder.Child(parent,$"bridge_crossing_{index++:00}");
                for(var y=minY;y<=maxY;y++) for(var x=minX;x<=maxX;x++)
                    Place(root.transform,$"deck_{x}_{y}","bridge_floor",new Vector2(x+0.5f,y+0.5f),true);
                for(var x=minX;x<=maxX;x++)
                {
                    Place(root.transform,$"far_rail_{x}","bridge_rail_far",new Vector2(x+0.5f,maxY+1.0625f),false);
                    Place(root.transform,$"near_rail_{x}","bridge_rail_near",new Vector2(x+0.5f,minY-0.1875f),false);
                }
                Place(root.transform,"far_w","bridge_post_far_w",new Vector2(minX-0.125f,maxY+0.90625f),false);
                Place(root.transform,"far_e","bridge_post_far_e",new Vector2(maxX+1.125f,maxY+0.90625f),false);
                Place(root.transform,"near_w","bridge_post_near_w",new Vector2(minX-0.125f,minY-0.25f),false);
                Place(root.transform,"near_e","bridge_post_near_e",new Vector2(maxX+1.125f,minY-0.25f),false);
            }
        }
        public static void Banks(ForestRestartPlan plan,Transform parent)
        {
            var index=0;
            foreach(var row in plan.WaterCells.GroupBy(c=>c.y).OrderBy(g=>g.Key))
            {
                var left=row.Min(c=>c.x);var right=row.Max(c=>c.x);var y=row.Key;
                // 接岸口は開け、川の両端に岩の厚みを持つ連続した半Cellの岸を重ねる。
                if(!plan.BridgeCells.Contains(new Vector2Int(left,y)))
                    Place(parent,$"bank_w_{index++:000}","bank_w",new Vector2(left-0.15625f,y+0.5f),true);
                if(!plan.BridgeCells.Contains(new Vector2Int(right,y)))
                    Place(parent,$"bank_e_{index++:000}","bank_e",new Vector2(right+1.15625f,y+0.5f),true);
                // 横方向に露出した岸にも同じ地形由来の草/岩の縁を置き、階段の水平部分をつなぐ。
                foreach(var cell in row)
                    if(!plan.BridgeCells.Contains(cell) && !plan.WaterCells.Contains(cell+Vector2Int.down)
                        && plan.Bounds.Contains(cell+Vector2Int.down))
                        Place(parent,$"bank_rim_{index++:000}","bank_rim",new Vector2(cell.x+0.5f,cell.y),true);
            }
        }
    }
}
