using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Editable native-pixel authoring recipes. All opaque colours come from the existing art.
// Coordinates are integer pixels; there is no image resampling, antialiasing or Transform scaling.
public static class NemoPixelWorkshop
{
    const string Folder="Assets/NemoBlocks/Art/NemoAuthored64";
    static bool ready;
    static Color32[] wood,stone,leaf,water,dark,roof;
    static readonly Dictionary<Color32,Color32[]> nativeTones=new Dictionary<Color32,Color32[]>();
    static Color32[] grassPixels;static int grassW,grassH;
    class Pen
    {
        public int W,H;public Color32[] pixels;public bool nativeGrass;int seed;
        public Pen(int w,int h,int salt){W=w;H=h;seed=salt;pixels=new Color32[w*h];}
        int Hash(int x,int y){unchecked{return (x*73856093^y*19349663^seed*83492791)&0x7fffffff;}}
        public void Dot(int x,int y,Color32 c){if(x>=0&&y>=0&&x<W&&y<H)pixels[(H-1-y)*W+x]=c;}
        Color32 Grain(Color32[] pal,int x,int y,int light)
        {
            if(nativeGrass&&pal==leaf){var c=grassPixels[(y%grassH)*grassW+x%grassW];return new Color32(c.r,c.g,c.b,255);}
            int n=Hash(x/(pal==wood?8:2),y/2)%37;int k=Mathf.Clamp(light+(n<2?-1:n>33?1:0),0,pal.Length-1);
            var tone=pal[k];if(nativeTones.TryGetValue(tone,out var samples))return samples[Hash(x,y)%samples.Length];return tone;
        }
        public void Poly(Color32[] pal,int light,params int[] xy)
        {
            int minX=xy.Where((v,i)=>i%2==0).Min(),maxX=xy.Where((v,i)=>i%2==0).Max();
            int minY=xy.Where((v,i)=>i%2!=0).Min(),maxY=xy.Where((v,i)=>i%2!=0).Max();
            for(int y=Mathf.Max(0,minY);y<=Mathf.Min(H-1,maxY);y++)for(int x=Mathf.Max(0,minX);x<=Mathf.Min(W-1,maxX);x++){
                bool inside=false;for(int i=0,j=xy.Length-2;i<xy.Length;j=i,i+=2){int ax=xy[i],ay=xy[i+1],bx=xy[j],by=xy[j+1];if((ay>y)!=(by>y)&&x<(float)(bx-ax)*(y-ay)/(by-ay)+ax)inside=!inside;}
                if(inside)Dot(x,y,Grain(pal,x,y,light));
            }
        }
        public void Ellipse(int cx,int cy,int rx,int ry,Color32[] pal,int light)
        {
            for(int y=Mathf.Max(0,cy-ry);y<=Mathf.Min(H-1,cy+ry);y++)for(int x=Mathf.Max(0,cx-rx);x<=Mathf.Min(W-1,cx+rx);x++)if((float)(x-cx)*(x-cx)/(rx*rx)+(float)(y-cy)*(y-cy)/(ry*ry)<=1)Dot(x,y,pal==null?new Color32(0,0,0,0):Grain(pal,x,y,light+(x<cx-rx/3&&y<cy?1:0)));
        }
        public void Line(int ax,int ay,int bx,int by,Color32[] pal,int light,int width=2)
        {
            int n=Mathf.Max(Mathf.Abs(bx-ax),Mathf.Abs(by-ay));for(int i=0;i<=n;i++){int x=n==0?ax:Mathf.RoundToInt(Mathf.Lerp(ax,bx,(float)i/n)),y=n==0?ay:Mathf.RoundToInt(Mathf.Lerp(ay,by,(float)i/n));for(int yy=y-width/2;yy<=y+width/2;yy++)for(int xx=x-width/2;xx<=x+width/2;xx++)Dot(xx,yy,Grain(pal,xx,yy,light));}
        }
        public void Rock(int x,int y,int w,int h,int tone)
        {
            Poly(dark,1,x,y+h/3,x+w/3,y,x+w*4/5,y+h/7,x+w,y+h*4/5,x+w*3/4,y+h,x+w/6,y+h*9/10);
            Poly(stone,tone,x+3,y+h/3,x+w/3,y+3,x+w*4/5,y+h/7+3,x+w-4,y+h*4/5-3,x+w*3/4,y+h-4,x+w/6,y+h*9/10-3);
            Poly(stone,Mathf.Min(4,tone+1),x+5,y+h/3,x+w/3,y+5,x+w*4/5,y+h/7+5,x+w*3/5,y+h/3,x+w/4,y+h/2);
            Poly(stone,Mathf.Max(0,tone-1),x+w*3/5,y+h/3,x+w*4/5,y+h/7+5,x+w-5,y+h*4/5-4,x+w*3/4,y+h-5);
            Line(x+6,y+h/3,x+w/3,y+5,stone,4,2);Line(x+w/3,y+5,x+w*4/5-4,y+h/7+4,stone,4,2);
            Line(x+w*3/4,y+h/2,x+w*3/4,y+h*4/5,dark,2,2);
            Line(x+w/5,y+h*2/3,x+w*2/5,y+h*3/5,dark,2,1);Line(x+w*2/5,y+h*3/5,x+w/2,y+h*4/5,dark,2,1);
        }
        public void Moss(int x,int y,int w,int h){Ellipse(x,y,w,h,leaf,2);for(int i=0;i<7;i++)Ellipse(x-w+i*w/3,y+h/2+(i%3)*2,Mathf.Max(2,w/5),Mathf.Max(2,h/2),leaf,i%3+1);}
    }
    static Color32[] Palette(NemoNativeLibrary library,string id,params int[] rgb)
    {
        string path=library.Get(id).visuals[0].path;var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!tex.LoadImage(File.ReadAllBytes(path)))throw new Exception("Source colour decoding failed: "+path);var source=tex.GetPixels32().Where(c=>c.a>=245).Select(c=>new Color32(c.r,c.g,c.b,255)).Distinct().ToArray();UnityEngine.Object.DestroyImmediate(tex);if(source.Length==0)throw new Exception("No opaque source colour samples: "+path);
        var result=new List<Color32>();for(int i=0;i<rgb.Length;i+=3){int a=rgb[i],b=rgb[i+1],c=rgb[i+2];var closest=source.OrderBy(v=>(v.r-a)*(v.r-a)+(v.g-b)*(v.g-b)+(v.b-c)*(v.b-c)).Take(24).ToArray();result.Add(closest[0]);nativeTones[closest[0]]=closest;}return result.ToArray();
    }
    static Sprite Save(string name,Pen pen,int pivotY)
    {
        var texture=new Texture2D(pen.W,pen.H,TextureFormat.RGBA32,false);texture.SetPixels32(pen.pixels);texture.Apply();string path=Folder+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=64;var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spritePivot=new Vector2(.5f,(float)pivotY/pen.H);settings.spriteAlignment=(int)SpriteAlignment.Custom;importer.SetTextureSettings(settings);importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    }
    static NativePrototype Prototype(string id,Sprite sprite,NativeBody[] bodies,float[] bounds=null)
    {
        return new NativePrototype{id=id,visuals=new[]{new NativeVisual{path=AssetDatabase.GetAssetPath(sprite),name=id,sprite=sprite,rect=new[]{0f,0f,sprite.rect.width,sprite.rect.height},pivotX=sprite.pivot.x,pivotY=sprite.pivot.y,color=new[]{1f,1f,1f,1f}}},bounds=bounds??new[]{-sprite.pivot.x/64,-sprite.pivot.y/64,sprite.rect.width/64,sprite.rect.height/64},bodies=bodies};
    }
    static NativeBody Box(float x,float y,float w,float h)=>new NativeBody{x=x,y=y,width=w,height=h};
    static Pen Cart()
    {
        var p=new Pen(288,224,11);
        p.Line(67,137,18,204,wood,0,9);p.Line(109,156,64,217,wood,0,8);p.Line(67,137,18,204,wood,3,3);p.Line(109,156,64,217,wood,2,2);
        p.Poly(wood,0,54,64,212,48,255,111,92,143);p.Poly(wood,3,58,66,209,52,249,110,93,137);
        for(int i=0;i<7;i++)p.Line(61+i*22,66-i*2,96+i*21,137-i*4,wood,1,2);
        p.Poly(wood,1,55,66,94,138,94,176,54,112);p.Poly(wood,2,94,138,254,109,253,157,94,183);
        for(int i=0;i<3;i++)p.Line(97,146+i*12,249,119+i*12,wood,0,2);
        p.Line(95,139,254,111,wood,4,4);p.Line(96,177,252,155,wood,0,5);
        p.Poly(wood,2,106,40,164,33,181,59,122,71,106,40);p.Poly(wood,1,122,71,181,59,181,100,122,114);p.Poly(wood,0,107,40,122,71,122,114,106,83);
        p.Line(127,79,175,68,wood,4,3);p.Line(126,102,176,91,wood,0,3);p.Line(127,78,176,96,wood,3,5);p.Line(176,68,128,104,wood,3,5);
        for(int x=55;x<=242;x+=187){p.Ellipse(x+5,168,32,40,dark,1);p.Ellipse(x,165,31,39,wood,0);p.Ellipse(x,165,25,32,wood,4);p.Ellipse(x,165,20,26,dark,1);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;p.Line(x,165,x+Mathf.RoundToInt(23*Mathf.Cos(a)),165+Mathf.RoundToInt(30*Mathf.Sin(a)),wood,3,4);}p.Ellipse(x,165,7,8,wood,1);p.Ellipse(x-2,163,3,3,stone,3);}
        return p;
    }
    static Pen Wheel()
    {
        var p=new Pen(224,280,21);p.Ellipse(122,153,85,112,dark,1);p.Ellipse(120,151,82,109,wood,1);p.Ellipse(109,150,78,109,wood,0);p.Ellipse(109,150,70,100,wood,4);p.Ellipse(109,150,60,88,null,0);
        for(int i=0;i<12;i++){float a=i*Mathf.PI/6;int x=109+Mathf.RoundToInt(70*Mathf.Cos(a)),y=150+Mathf.RoundToInt(101*Mathf.Sin(a));p.Line(x,y,x+15,y-2,wood,0,11);p.Line(x,y-4,x+15,y-6,wood,3,3);p.Line(109,150,109+Mathf.RoundToInt(66*Mathf.Cos(a)),150+Mathf.RoundToInt(94*Mathf.Sin(a)),wood,1,7);p.Line(108,147,109+Mathf.RoundToInt(66*Mathf.Cos(a)),147+Mathf.RoundToInt(94*Mathf.Sin(a)),wood,3,2);}
        p.Ellipse(109,150,15,20,wood,0);p.Ellipse(107,147,10,14,wood,3);p.Line(77,151,121,145,wood,0,11);p.Line(77,147,121,141,wood,4,3);p.Rock(55,231,45,28,2);return p;
    }
    static Pen Channel()
    {
        var p=new Pen(144,512,28);p.Poly(water,1,32,0,109,0,109,512,32,512);
        for(int y=0;y<512;y+=32){p.Rock(9,y,30,37,2);p.Rock(108,y+8,29,35,2);p.Line(45,y+7,83,y+3,water,4,2);p.Line(57,y+18,101,y+14,water,3,1);}
        p.Moss(14,91,12,7);p.Moss(119,373,13,8);return p;
    }
    static Pen Cave(bool mine)
    {
        var p=new Pen(512,352,mine?43:42);p.Poly(dark,1,12,328,22,176,83,65,181,22,311,16,415,69,487,201,505,331);
        int[][] blocks={new[]{26,170,100,111},new[]{60,79,128,116},new[]{154,22,140,100},new[]{276,17,132,108},new[]{388,73,109,141},new[]{25,267,119,74},new[]{386,265,113,74}};
        foreach(var b in blocks)p.Rock(b[0],b[1],b[2],b[3],2);
        p.Poly(dark,0,132,329,144,218,177,146,250,124,321,145,367,212,384,328);
        p.Poly(dark,1,159,316,169,225,190,177,249,151,310,170,341,220,360,317);
        p.Poly(dark,0,190,302,198,235,220,209,285,196,312,251,321,302);
        p.Rock(96,164,70,121,3);p.Rock(359,166,64,129,1);p.Rock(123,112,85,64,3);p.Rock(301,107,97,65,2);
        p.Line(180,319,354,319,dark,1,5);p.Moss(102,100,40,11);p.Moss(368,86,39,13);p.Moss(430,275,25,9);p.Moss(59,317,28,7);
        if(mine){p.Poly(wood,0,124,106,385,90,391,124,126,139);p.Poly(wood,3,129,108,381,95,385,115,131,128);p.Poly(wood,1,129,123,152,126,152,330,127,330);p.Poly(wood,2,365,113,385,117,387,331,363,331);p.Line(135,132,139,324,wood,4,3);p.Line(371,120,374,324,wood,4,3);p.Line(150,183,200,129,wood,2,12);p.Line(317,123,368,166,wood,1,12);for(int y=300;y<352;y+=13)p.Line(212-(y-300)/3,y,285+(y-300)/3,y,wood,2,7);p.Line(227,286,207,351,stone,2,5);p.Line(272,286,292,351,stone,3,5);}
        return p;
    }
    static Pen Cliff(int width)
    {
        var p=new Pen(width,320,51+width);int w=width;
        p.Poly(dark,1,10,270,17,127,62,78,w/3,39,w*2/3,48,w-52,27,w-17,72,w-9,279,w-59,300,w/2,285,59,302);
        p.Poly(stone,1,20,265,25,139,77,121,w/3,117,w*2/3,101,w-31,95,w-21,273,w-58,287,w/2,273,58,289);
        for(int x=30,i=0;x<w-47;x+=61,i++){
            int y=126+(i%3)*13;int bottom=275+(i%2)*10;
            p.Poly(stone,i%2+1,x,y,x+51,y-16,x+58,bottom-37,x+33,bottom,x+3,bottom-13);
            p.Line(x+5,y+7,x+16,bottom-17,stone,3,3);p.Line(x+50,y+16,x+46,bottom-30,dark,2,2);
            p.Line(x+7,y+54,x+43,y+42,dark,1,2);p.Line(x+19,y+88,x+55,y+79,stone,3,2);
        }
        p.nativeGrass=true;
        p.Poly(leaf,1,17,126,61,79,w/3,40,w*2/3,49,w-51,29,w-18,75,w-20,121,w*2/3,142,w/3,138,82,169,26,158);
        p.Poly(leaf,3,30,121,67,88,w/3,51,w*2/3,60,w-58,40,w-32,79,w-34,110,w*2/3,129,w/3,125,79,153,37,146);
        p.nativeGrass=false;
        for(int x=34,i=0;x<w-25;x+=29,i++)p.Moss(x,145+(i%4)*4-14,14,8);
        for(int x=34,i=0;x<w-29;x+=43,i++){p.Line(x,79+(i%4)*13,x+14,76+(i%4)*13,leaf,4,2);if(i%3==0)p.Rock(x,270+(i%2)*8,49,28,2);}
        p.Moss(w-63,277,22,7);return p;
    }
    static Pen RuinedHouse()
    {
        var p=new Pen(512,360,61);p.Poly(dark,1,55,323,96,152,410,135,461,318,390,343,108,348);p.Poly(leaf,0,112,187,382,168,419,306,337,323,130,323);
        p.Poly(stone,2,94,166,109,103,182,126,189,257,110,306,80,308);p.Poly(stone,3,95,163,108,111,174,132,173,249,108,290,89,290);
        for(int y=149,i=0;y<=237;y+=18,i++){p.Line(104,y,171,y+8,stone,1,1);p.Line(106,y-2,171,y+6,stone,4,1);int joint=i%2==0?132:151;p.Line(joint,y+4,joint-1,y+19,stone,1,1);}
        p.Poly(wood,1,99,114,191,29,290,111,277,126,192,50,113,126);p.Line(192,47,194,241,wood,1,12);p.Line(108,129,272,127,wood,1,11);p.Line(127,116,192,63,wood,3,4);
        p.Poly(roof,2,83,129,111,97,160,58,155,93,172,100,148,120,118,122,110,140);for(int i=0;i<4;i++)p.Line(99+i*14,128-i*11,128+i*8,121-i*9,roof,0,2);
        for(int x=111;x<428;x+=38){int height=x<210?65:x>285?42:15;p.Rock(x,327-height,39,height,2);}
        p.Poly(stone,1,356,134,397,134,433,229,423,315,387,302,366,215);p.Line(371,147,389,292,wood,0,9);p.Line(368,146,388,290,wood,3,3);
        p.Line(177,274,343,309,wood,1,10);p.Line(190,289,329,250,wood,2,7);p.Line(224,314,271,314,stone,4,5);p.Moss(126,299,30,12);p.Moss(402,307,29,13);p.Moss(333,319,20,7);return p;
    }
    static Pen Spring()
    {
        var p=new Pen(320,224,71);p.Poly(dark,1,34,89,99,43,186,28,280,75,307,130,279,189,179,207,65,188,16,143);
        p.Poly(water,1,41,91,102,52,183,37,270,82,292,130,268,179,174,196,72,179,27,142);
        p.Poly(water,2,75,97,122,65,182,61,244,98,265,134,245,162,168,182,91,165,49,140);
        for(int i=0;i<7;i++){p.Line(64+i*17,117+i%3*15,108+i*17,110+i%3*15,water,3,2);}p.Ellipse(145,105,17,4,water,4);p.Ellipse(148,94,8,12,water,3);p.Line(148,82,144,103,water,4,3);
        p.Rock(34,65,56,39,2);p.Rock(17,132,52,42,3);p.Rock(260,82,50,42,2);p.Rock(235,166,52,30,2);p.Moss(61,74,20,8);p.Moss(273,143,21,10);return p;
    }
    static Pen Altar()
    {
        var p=new Pen(192,256,81);p.Rock(34,213,124,35,2);p.Rock(48,191,98,27,3);p.Poly(wood,0,37,91,55,95,55,208,37,208);p.Poly(wood,2,140,90,155,93,155,208,140,208);p.Poly(dark,1,53,96,140,95,140,190,53,190);
        p.Poly(roof,0,20,88,96,20,172,87,162,100,96,40,28,103);p.Poly(roof,2,27,87,96,29,163,87,132,93,96,45,56,92);for(int i=0;i<5;i++)p.Line(40+i*12,89-i*9,94,42+i*10,roof,0,2);
        p.Rock(73,143,48,53,3);p.Ellipse(97,135,15,19,stone,3);p.Line(89,121,96,115,stone,4,3);p.Ellipse(94,130,3,3,dark,1);p.Ellipse(101,130,3,3,dark,1);p.Rock(47,182,20,19,2);p.Rock(131,182,20,19,2);p.Line(58,183,58,163,wood,4,5);p.Line(141,183,141,163,wood,4,5);p.Ellipse(58,159,3,6,roof,4);p.Ellipse(141,159,3,6,roof,4);p.Moss(35,223,21,9);p.Moss(151,223,17,8);return p;
    }
    static Pen Nest()
    {
        var p=new Pen(256,176,91);p.Ellipse(128,109,110,54,dark,1);p.Ellipse(125,102,104,48,wood,1);p.Ellipse(125,100,79,33,dark,0);
        for(int i=0;i<36;i++){float a=i*Mathf.PI/18;int x=125+Mathf.RoundToInt(94*Mathf.Cos(a)),y=103+Mathf.RoundToInt(43*Mathf.Sin(a));p.Line(x-12,y-9,x+11,y+7,wood,i%3+1,3);}
        foreach(int x in new[]{89,128,167}){int y=x==128?85:107;p.Ellipse(x,y,20,29,stone,3);p.Line(x-9,y-17,x-4,y-10,dark,2,2);p.Line(x-4,y-10,x-8,y-4,dark,2,2);}p.Moss(30,119,16,7);p.Moss(226,114,17,7);return p;
    }
    static Pen HerbBed()
    {
        var p=new Pen(256,160,99);p.Poly(wood,0,18,72,201,35,242,92,58,135);p.Poly(wood,1,25,74,199,43,231,89,59,126);
        for(int j=0;j<3;j++)for(int i=0;i<7;i++){int x=48+i*23+j*8,y=78-i*4+j*16;p.Line(x,y+9,x,y-5,leaf,1,3);p.Ellipse(x-5,y,7,4,leaf,2);p.Ellipse(x+5,y-5,6,4,leaf,3);if((i+j)%3==0)p.Ellipse(x,y-10,3,3,roof,4);}
        p.Line(23,75,61,126,wood,3,4);p.Line(60,130,235,93,wood,3,4);return p;
    }
    public static void Ensure(NemoNativeLibrary library)
    {
        if(ready)return;Directory.CreateDirectory(Folder);var grassSource=new Texture2D(2,2,TextureFormat.RGBA32,false);grassSource.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(library.Grass[0])));grassW=grassSource.width;grassH=grassSource.height;grassPixels=grassSource.GetPixels32();UnityEngine.Object.DestroyImmediate(grassSource);
        wood=Palette(library,"logging-shed",34,24,15,65,39,20,105,65,30,151,103,48,193,146,77);
        stone=Palette(library,"ruined-arch",35,40,31,62,67,48,98,104,83,139,145,120,189,192,163);
        leaf=Palette(library,"ruined-arch",24,43,14,45,66,18,75,103,24,112,138,32,151,166,48);
        water=Palette(library,"fountain",12,47,68,16,85,129,18,121,173,51,166,218,164,225,241);
        dark=Palette(library,"ruined-arch",10,15,9,20,26,17,37,43,31,57,61,43,77,83,65);
        roof=Palette(library,"inn",60,27,16,96,42,20,146,57,25,195,98,35,233,171,62);
        var own=new List<NativePrototype>();
        own.Add(Prototype("loaded-cart",Save("loaded-cart",Cart(),12),new[]{Box(-1.7f,0,3.7f,1.1f)}));
        var wheel=Save("waterwheel",Wheel(),12);var channel=Save("mill-channel",Channel(),256);
        var baseMill=library.Get("logging-shed");var millVisuals=baseMill.visuals.Select(v=>new NativeVisual{path=v.path,name=v.name,sprite=v.sprite,x=v.x,y=v.y,pivotX=v.pivotX,pivotY=v.pivotY,rect=v.rect,color=v.color,order=v.order}).ToList();
        millVisuals.Add(new NativeVisual{path=AssetDatabase.GetAssetPath(channel),name="water sluice",sprite=channel,x=5.8f,y=.7f,rect=new[]{0f,0f,144f,512f},color=new[]{1f,1f,1f,1f},order=-1});
        millVisuals.Add(new NativeVisual{path=AssetDatabase.GetAssetPath(wheel),name="vertical waterwheel",sprite=wheel,x=3.8f,y=.05f,rect=new[]{0f,0f,224f,280f},color=new[]{1f,1f,1f,1f},order=1});
        own.Add(new NativePrototype{id="watermill",visuals=millVisuals.ToArray(),bounds=new[]{-3.4f,-3.3f,10.35f,8.85f},bodies=baseMill.bodies.Concat(new[]{Box(4.85f,-3.3f,1.65f,8),Box(2.8f,0,1.8f,.9f)}).ToArray()});
        var caveBodies=new[]{Box(-3.7f,0,2.15f,1.5f),Box(1.55f,0,2.15f,1.5f),Box(-1.55f,2.0f,3.1f,.45f)};
        own.Add(Prototype("cave-mouth",Save("cave-mouth",Cave(false),24),caveBodies));own.Add(Prototype("mine-mouth",Save("mine-mouth",Cave(true),24),caveBodies));
        own.Add(Prototype("cliff-face",Save("cliff-face",Cliff(640),12),new[]{Box(-4.55f,0,9.1f,4.25f)}));
        own.Add(Prototype("cliff-face-small",Save("cliff-face-small",Cliff(384),12),new[]{Box(-2.55f,0,5.1f,4.25f)}));
        own.Add(Prototype("broken-house",Save("broken-house",RuinedHouse(),24),new[]{Box(-3.4f,0,2.5f,1.4f),Box(.9f,0,2.5f,1.4f),Box(-.9f,1.8f,1.8f,.5f)}));
        own.Add(Prototype("natural-spring",Save("natural-spring",Spring(),12),new[]{Box(-1.9f,.2f,3.8f,2.4f)}));
        own.Add(Prototype("wayside-shrine",Save("wayside-shrine",Altar(),12),new[]{Box(-.85f,0,1.7f,.65f)}));
        own.Add(Prototype("monster-nest",Save("monster-nest",Nest(),12),new[]{Box(-1.6f,0,3.2f,1.4f)}));
        own.Add(Prototype("herb-bed",Save("herb-bed",HerbBed(),12),new NativeBody[0]));
        var identifiers=new HashSet<string>(own.Select(p=>p.id));library.Prototypes=library.Prototypes.Where(p=>!identifiers.Contains(p.id)).Concat(own).ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();ready=true;
        File.WriteAllText(Path.Combine(NemoBuilder.Evidence,"authored-pixel-assets.json"),JsonUtility.ToJson(new Assets{success=own.All(p=>p.visuals.All(v=>v.sprite.pixelsPerUnit==64)),scope="New editable integer-pixel authoring recipes; opaque colour samples from native original sprites; no original image mutation/resampling",assets=own.ToArray()},true));
    }
    [Serializable]class Assets{public bool success;public string scope;public NativePrototype[] assets;}
}
