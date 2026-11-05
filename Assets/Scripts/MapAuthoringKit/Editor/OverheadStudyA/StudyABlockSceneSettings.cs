using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    public enum StudyABlockSceneCase { Plaza, ForestPath, BridgeBank, PlazaAndPath, PathAndBridgeBank, PlazaPathAndBridgeBank }
    public enum StudyABlockTerrainRevision { Current, R1, GroundR2 }

    /// <summary>独立したブロック構造検証の設定。既存Referenceや通常Game設定を変更しない。</summary>
    public sealed class StudyABlockSceneSettings : ScriptableObject
    {
        public int Seed = 20261003;
        public StudyABlockSceneCase Case = StudyABlockSceneCase.PlazaAndPath;
        [Tooltip("Currentは既存Nativeの地表版を尊重。新規入力では現行版を使用し、保存時に版を確定する。")]
        public StudyABlockTerrainRevision TerrainRevision;
        [Tooltip("旧保存設定の0は旧構図を維持。新規生成では2を保存する。")]
        public int CompositionVersion;
        [Tooltip("Optional visual finish snapshot. 0 preserves existing saved studies; 1 adds reviewed composition, forest depth and bridge/shore overlays.")]
        public int ArtFinishVersion;
        [Tooltip("PASS2の非歩行領域を森で囲む。旧保存設定では追加しない。")]
        public bool EnableForestBackdrop;
        [Tooltip("保存設定ではfalseなら追加なし。Prefab未指定の新規入力はレビュー済みDetailsがあれば有効にし、trueを保存する。")]
        public bool EnableForestDetails;
        [Tooltip("Native closed contour clips unchanged R2 soil texels in8/16px steps. Old saved inputs keep false; fresh Plaza/Path study saves explicit true.")]
        public bool EnableFineSoilContour;
        [Tooltip("明示opt-inだけで承認BridgeRiver5素材を表示。旧保存設定falseは診断water/deckを維持する。")]
        public bool EnableBridgeRiverArt;
        [Tooltip("明示opt-inでNative water閉輪郭を表示・進入禁止Colliderの共通正本にする。旧falseのmacro水は維持。")]
        public bool EnableFineBridgeRiverContours;
        [Tooltip("Fine bridge landscape snapshot: 0 keeps legacy supplied Native composition; fresh BridgeBank input saves version2.")]
        public int BridgeRiverLandscapeVersion;
        [Tooltip("Separate bridge structure trial: 0 preserves saved R4; 1 uses Bridge01 native layers with a 4x1.5u floor and explicit pillar bodies.")]
        public int BridgeStructureVersion;
        [HideInInspector] public string BridgeStructureFingerprint;
        [Tooltip("Separate same-layout foliage trials: 0 preserves saved studies; 1 changes canopy interiors (P); 2 mixes original/V01/V02 by stable tree ID; 3 changes only local ground contact colors.")]
        public int FoliageExperimentVersion;
        [HideInInspector] public string FoliageExperimentFingerprint;
        [HideInInspector] public string BridgeRiverFingerprint;
        [Tooltip("未指定の区画だけ、新しい出力先へauthoring Prefabを作る。既存Prefabは上書きしない。")]
        public GameObject PlazaPrefab;
        public GameObject ForestPathPrefab;
        public GameObject BridgeBankPrefab;
        public bool EnableLimitedVariation = true;
        [Range(0f, 1f)] public float VariationProbability = 0.65f;
        [Range(0, 8)] public int MaxVariablePropsPerBlock = 3;
        public bool IncludeDiagnosticLabel = true;
        [Tooltip("通常Camera比較用の初期値は8。製品Cameraの上限ではない。")]
        public float CameraOrthographicSize = 8f;
    }
}
