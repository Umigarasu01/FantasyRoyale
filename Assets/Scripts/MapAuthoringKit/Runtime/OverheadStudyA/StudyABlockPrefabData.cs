using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    /// <summary>Prefab制作時の明示的な契約。生成SceneはTilemap/Sprite/Colliderへ展開し、この情報を持ち込まない。</summary>
    [DisallowMultipleComponent]
    public sealed class StudyABlockPrefabData : MonoBehaviour
    {
        public StudyABlockDefinition Definition;
    }
}
