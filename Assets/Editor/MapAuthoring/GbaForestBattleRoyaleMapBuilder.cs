using FantasyRoyale.MapAuthoringKit.Editor;

namespace FantasyRoyale.Editor.MapAuthoring
{
    /// <summary>旧Reference Builderの呼出し互換。Scene path/GUIDを維持して新Kitへ委譲する。</summary>
    public static class GbaForestBattleRoyaleMapBuilder
    {
        public const string ReferenceScenePath = ForestRestartAssetBuilder.ReferenceScenePath;
        public static void BuildReferenceMap()
        {
            ForestRestartAssetBuilder.BuildAssets();
            ForestRestartAssetBuilder.BuildScene(ForestRestartAssetBuilder.DefaultSeed, ReferenceScenePath);
        }
        public static void CaptureReferencePreviews() => ForestRestartAssetBuilder.BuildCompleteKit();
    }
}
