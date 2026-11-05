using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    /// <summary>Native local closed river outline; the generated scene saves its shared render/collision raster.</summary>
    public sealed class StudyABridgeRiverContour : MonoBehaviour
    {
        public string BlockId;
        public Vector2Int Extent = new Vector2Int(12, 12);
        // Native preview provenance is independent of the editable controls; the editor builder records it.
        [HideInInspector] public string PreviewWaterGuid, PreviewWaterPngSha256, PreviewWaterMetaSha256;
        [HideInInspector] public string PreviewRoadGuid, PreviewRoadPngSha256, PreviewRoadMetaSha256;
        [Tooltip("Ordered native-local XY controls. Last point closes to first. No runtime regeneration.")]
        public List<Vector2> Points = new List<Vector2>();
        [Tooltip("Binary contour sampling in native64px/world pixels:8 or16. No blur or alpha gradients.")]
        public int PixelStep = 8;

        private void OnDrawGizmosSelected()
        {
            if (Points == null || Points.Count < 2) return;
            Gizmos.color = new Color(.1f, .6f, 1f, 1f);
            for (var i = 0; i < Points.Count; i++)
                Gizmos.DrawLine(transform.TransformPoint(Points[i]), transform.TransformPoint(Points[(i + 1) % Points.Count]));
        }
    }
}
