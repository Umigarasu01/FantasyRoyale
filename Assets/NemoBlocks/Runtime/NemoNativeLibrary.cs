using System;
using UnityEngine;

[Serializable] public class NativeVisual { public string path,name; public float x,y,pivotX,pivotY; public float[] rect,color; public bool flipX,flipY; public int order; public Sprite sprite; }
[Serializable] public class NativeBody { public float x,y,width,height; public Rect Rect=>new Rect(x,y,width,height); }
[Serializable] public class NativePrototype { public string id; public NativeVisual[] visuals; public float[] bounds; public NativeBody[] bodies; public Rect Bounds=>new Rect(bounds[0],bounds[1],bounds[2],bounds[3]); }
[Serializable] public class NativeRiverOwner { public string id,kind; public NativeVisual[] visuals; public NativeBody[] present,absent; }
[Serializable] public class NativeInput { public NativePrototype[] prototypes; public NativeRiverOwner[] river; public string grassSource,hero; }
public class NemoNativeLibrary : ScriptableObject
{
    public NativePrototype[] Prototypes; public NativeRiverOwner[] River; public Sprite Hero; public Texture2D[] Grass; public Texture2D Dirt;
    public NativePrototype Get(string id)=>Array.Find(Prototypes,p=>p.id==id);
}
