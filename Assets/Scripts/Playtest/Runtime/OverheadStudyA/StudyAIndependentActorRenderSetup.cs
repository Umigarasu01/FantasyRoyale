using UnityEngine;
using UnityEngine.Rendering;

namespace FantasyRoyale.Playtest.OverheadStudyA
{
    /// <summary>Keeps the actual study Actor's visual parts under one foot-based sort key.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class StudyAIndependentActorRenderSetup : MonoBehaviour
    {
        public StudyABlockMovementDiagnostic Diagnostic;
        public string ExpectedMapScenePath;
        public bool Applied { get; private set; }
        public string Failure { get; private set; } = string.Empty;

        private void LateUpdate()
        {
            if (Applied || Failure.Length != 0 || Diagnostic == null || !Diagnostic.Ready) return;
            if (Diagnostic.Actor == null || Diagnostic.MapScenePath != ExpectedMapScenePath ||
                Diagnostic.Actor.gameObject.scene.path != ExpectedMapScenePath)
            { Failure = "Actual Actor must belong to the expected independent component map."; return; }
            var actor = Diagnostic.Actor.gameObject;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0 || System.Array.Exists(renderers, r => r.sprite == null))
            { Failure = "Actual Actor visual renderers are missing."; return; }
            var group = actor.GetComponent<SortingGroup>();
            if (group == null) group = actor.AddComponent<SortingGroup>();
            group.sortingLayerName = "WorldObjects";
            group.sortingOrder = 0;
            foreach (var renderer in renderers)
            {
                renderer.sortingLayerName = "WorldObjects";
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
            Applied = true;
        }
    }
}
