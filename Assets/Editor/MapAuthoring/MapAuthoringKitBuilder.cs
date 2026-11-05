using FantasyRoyale.MapAuthoringKit.Editor;

namespace FantasyRoyale.Editor.MapAuthoring
{
    /// <summary>旧Menu呼出し互換。素材の再構築はForestRestart Kitへ委譲する。</summary>
    public static class MapAuthoringKitBuilder
    {
        public static void RebuildCompleteKit() => ForestRestartAssetBuilder.BuildCompleteKit();
        public static void CaptureSamplePreview() => ForestRestartAssetBuilder.BuildCompleteKit();
    }
}
