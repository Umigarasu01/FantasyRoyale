using UnityEngine;

namespace FantasyRoyale.Playtest.OverheadStudyA
{
    // Isolated painted-block fixture only. The actual Actor/input/physics are owned by the existing diagnostic.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class StudyAPaintedBlockActorRenderSetup : MonoBehaviour
    {
        public StudyABlockMovementDiagnostic Diagnostic;
        public string ExpectedMapScenePath;
        public bool Applied { get; private set; }
        public string Failure { get; private set; } = string.Empty;

        private void LateUpdate()
        {
            if (Applied || Failure.Length != 0) return;
            if (Diagnostic == null || string.IsNullOrEmpty(ExpectedMapScenePath)
                || !ExpectedMapScenePath.StartsWith("Assets/Scenes/MapAuthoring/OverheadStudyA/", System.StringComparison.Ordinal)
                || Diagnostic.MapScenePath != ExpectedMapScenePath)
            { Failure = "Painted-block render fixture requires its explicit diagnostic/new map Scene."; return; }
            if (!Diagnostic.Ready) return;
            var actor = Diagnostic.Actor;
            var renderer = actor != null ? actor.GetComponent<SpriteRenderer>() : null;
            if (actor == null || actor.gameObject.scene.path != ExpectedMapScenePath || renderer == null
                || renderer.sprite == null || renderer.spriteSortPoint != SpriteSortPoint.Pivot)
            { Failure = "The actual diagnostic Actor renderer/Scene does not match the painted-block fixture."; return; }
            renderer.sortingLayerName = "WorldObjects";
            renderer.sortingOrder = 10;
            Applied = renderer.sortingLayerName == "WorldObjects" && renderer.sortingOrder == 10;
            if (!Applied) Failure = "Required painted-block Actor sorting contract was not applied.";
        }
    }
}
