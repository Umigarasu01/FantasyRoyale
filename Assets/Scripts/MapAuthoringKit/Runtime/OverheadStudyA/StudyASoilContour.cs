using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    /// <summary>Native block-local closed visible-soil outline. Structural Tilemap masks remain separate.</summary>
    public sealed class StudyASoilContour : MonoBehaviour
    {
        [Tooltip("Ordered local XY outline. The last point closes to the first; edit these points in the native Prefab.")]
        public List<Vector2> Points = new List<Vector2>();
        [Tooltip("Binary edge steps in native 64px/world pixels. Supported values: 8 or 16. Controls need not lie on this grid.")]
        public int PixelStep = 8;

        private void OnDrawGizmosSelected()
        {
            if (Points == null || Points.Count < 2) return;
            Gizmos.color = new Color(1f, 0.65f, 0.15f, 1f);
            for (var i = 0; i < Points.Count; i++)
                Gizmos.DrawLine(transform.TransformPoint(Points[i]), transform.TransformPoint(Points[(i + 1) % Points.Count]));
        }
    }
}
