using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>森林の比較Sceneを同じSeedと表示倍率で再生成するEditor用設定。</summary>
    public sealed class ForestCompositionSettings : ScriptableObject
    {
        public int seed = ForestRestartAssetBuilder.DefaultSeed;
        [Range(6f, 12f)] public float focusOrthographicSize = 8f;
        [Tooltip("店と泉の構図候補を生成する。商取引や回復などのGameplay機能は付けない。") ]
        public bool includeLandmarkCandidates = true;
    }
}
