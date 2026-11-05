using System;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>独立した素材試作の仮設定。newしてもProject内にAssetを作らない。</summary>
    [Serializable]
    public sealed class OverheadStudyASettings
    {
        public const string ArtRoot = "Assets/Art/Map/OverheadStudyA";
        public int Seed = 20261003;
        public int WorldWidth = 24;
        public int WorldHeight = 16;
        public float PixelsPerUnit = 64f;
        public int IntegerDownsample = 1;
        public string InputRoot = ArtRoot + "/Input";
        public string DerivedRoot = ArtRoot + "/Derived";
        public string QaRoot = ArtRoot + "/QA";
        public string ManifestFileName = "asset-manifest.json";
        public bool BinaryAlphaMask = false;
        public byte AlphaThreshold = 128;
    }

    [Serializable]
    public sealed class OverheadStudyAManifest
    {
        public int schemaVersion = 1;
        public bool approvedForImport;
        public OverheadStudyAAssetDefinition[] entries;
    }

    /// <summary>明示したnative整数cropのみ使用。nullなら原本の全領域。</summary>
    [Serializable]
    public sealed class OverheadStudyAPixelRect
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public RectInt Value => new RectInt(x, y, width, height);
    }

    /// <summary>接地Pivotからの相対world座標。表示Canvasとは独立した契約。</summary>
    [Serializable]
    public sealed class OverheadStudyAWorldRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public Rect Value => new Rect(x, y, width, height);
    }

    [Serializable]
    public sealed class OverheadStudyAAssetDefinition
    {
        public string name;
        public string role;
        public string file;
        public string reviewedSourceSha256;
        public OverheadStudyAPixelRect sourceRect;
        // 共通整数縮小後のCanvas寸法。0はcrop全体の寸法。小さくしてcrop/fitしない。
        public int canvasWidth;
        public int canvasHeight;
        // 原本の左下を(0,0)とするnative pixel。未指定ならcropの中央。
        public bool hasFootPixelPivot;
        public float footPixelX;
        public float footPixelY;
        // 任意の宣言値との一致検査だけ。個別のworld scaleやPPUは変更しない。
        public float worldWidth;
        public float worldHeight;
        public OverheadStudyAWorldRect footprint;
        public OverheadStudyAWorldRect collisionRect;
        public OverheadStudyAWorldRect walkFloor;
    }
}
