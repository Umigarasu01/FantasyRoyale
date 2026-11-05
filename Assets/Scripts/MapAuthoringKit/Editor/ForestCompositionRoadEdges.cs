using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>既存路面の色を保ち、9Cell占有の連続境界だけを切り出す。通行Planは変更しない。</summary>
    public static class ForestCompositionRoadEdges
    {
        public const string Root = ForestCompositionVisuals.Root + "/RoadEdges";
        public const string PalettePath = Root + "/RoadEdgeTiles.asset";
        private const int Size = 32;
        private const int AtlasWidth = 16 * Size;
        private const int AtlasHeight = 32 * Size;

        public static int Mask(ForestRestartPlan plan, Vector2Int cell)
        {
            var mask = 0;
            for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                if (plan.RoadCells.Contains(cell + new Vector2Int(x,y))) mask |= 1 << ((y+1)*3+x+1);
            return mask;
        }

        /// <summary>共有辺は両Cellから同じ四つの値を補間する。道Cell中心を必ず残す。</summary>
        public static bool Covered(int mask, int x, int y)
        {
            var dx = x / 31f - 0.5f; var dy = y / 31f - 0.5f;
            var nx = dx < 0 ? -1 : 1; var ny = dy < 0 ? -1 : 1;
            var tx = Mathf.Abs(dx); var ty = Mathf.Abs(dy);
            Func<int,int,float> value = (cx,cy) => (mask & (1 << ((cy+1)*3+cx+1))) != 0 ? 1f : 0f;
            var coverage = value(0,0)*(1-tx)*(1-ty) + value(nx,0)*tx*(1-ty)
                + value(0,ny)*(1-tx)*ty + value(nx,ny)*tx*ty;
            return coverage >= 0.5f;
        }

        public static void BuildAssets()
        {
            ForestRestartAssetBuilder.EnsureFolder(Root);
            for (var variant=0; variant<4; variant++)
            {
                var source = ForestRestartAssetBuilder.LoadPng(ForestRestartAssetBuilder.ArtRoot + $"/Sprites/road_0f_{variant:00}.png");
                var atlas = new Texture2D(AtlasWidth,AtlasHeight,TextureFormat.RGBA32,false);
                try
                {
                    var native = source.GetPixels32(); var output = new Color32[AtlasWidth*AtlasHeight];
                    var slices = new SpriteMetaData[512];
                    for(var mask=0;mask<512;mask++)
                    {
                        var left = mask%16*Size; var bottom = mask/16*Size;
                        for(var y=0;y<Size;y++) for(var x=0;x<Size;x++)
                        {
                            var pixel=native[y*Size+x]; if(!Covered(mask,x,y)) pixel.a=0;
                            output[(bottom+y)*AtlasWidth+left+x]=pixel;
                        }
                        slices[mask]=new SpriteMetaData { name=$"road_edge_{mask:x3}_{variant:00}",rect=new Rect(left,bottom,Size,Size),
                            alignment=(int)SpriteAlignment.Center,pivot=new Vector2(0.5f,0.5f) };
                    }
                    atlas.SetPixels32(output);atlas.Apply();
                    var path=Root+$"/road-edges-{variant:00}.png";File.WriteAllBytes(path,atlas.EncodeToPNG());
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
                    importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.crunchedCompression=false;
                    importer.alphaIsTransparency=true;
                    importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
#pragma warning disable CS0618
                    importer.spritesheet=slices;
#pragma warning restore CS0618
                    importer.SaveAndReimport();
                }
                finally { Object.DestroyImmediate(source);Object.DestroyImmediate(atlas); }
            }
        }

        public static void Apply(ForestRestartPlan plan, Tilemap terrain)
        {
            var sprites = new Dictionary<string,Sprite>();
            for(var variant=0;variant<4;variant++)
                foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(Root+$"/road-edges-{variant:00}.png").OfType<Sprite>())
                    sprites.Add(sprite.name,sprite);
            var tiles=AssetDatabase.LoadAllAssetsAtPath(PalettePath).OfType<Tile>().ToDictionary(tile=>tile.name);
            var changed=false;
            foreach(var cell in plan.GroundCells.OrderBy(c=>c.y).ThenBy(c=>c.x))
            {
                if(plan.WaterCells.Contains(cell)||plan.CliffCells.Contains(cell)) continue;
                var mask=Mask(plan,cell);if(mask==0) continue;
                if(!Enumerable.Range(0,Size*Size).Any(i=>Covered(mask,i%Size,i/Size))) continue;
                var variant=Variant(plan.Seed,cell);
                var key=$"road_edge_{mask:x3}_{variant:00}";
                if(!sprites.TryGetValue(key,out var sprite)) throw new InvalidOperationException("Road edge atlas未生成: "+key);
                if(!tiles.TryGetValue(key,out var tile))
                {
                    tile=ScriptableObject.CreateInstance<Tile>();tile.name=key;
                    if(!File.Exists(PalettePath)) AssetDatabase.CreateAsset(tile,PalettePath);
                    else AssetDatabase.AddObjectToAsset(tile,PalettePath);
                    tiles.Add(key,tile);changed=true;
                }
                if(tile.sprite!=sprite||tile.colliderType!=Tile.ColliderType.None)
                {
                    tile.sprite=sprite;tile.colliderType=Tile.ColliderType.None;EditorUtility.SetDirty(tile);changed=true;
                }
                terrain.SetTile(new Vector3Int(cell.x,cell.y,0),tile);
            }
            if(changed) AssetDatabase.SaveAssets();
        }

        private static int Variant(int seed,Vector2Int cell)
        {
            unchecked
            {
                var value=(uint)seed ^ (uint)cell.x*0x9e3779b9u ^ (uint)cell.y*0x85ebca6bu;
                value ^= value>>16; value *= 0x7feb352du; value ^= value>>15;
                value *= 0x846ca68bu; value ^= value>>16;
                return (int)(value%4);
            }
        }
    }
}
