using System;
using UnityEngine;

namespace FantasyRoyale.Playtest.OverheadStudyA
{
    /// <summary>Small live occupancy registry for this fixed component study, not the production AI pathfinder.</summary>
    [DisallowMultipleComponent]
    public sealed class StudyAIndependentNavigation : MonoBehaviour
    {
        public Rect FrameWorldBounds;
        public int PixelsPerUnit = 64;
        public string GeometryRecipeSha256;
        public Rect[] StaticBlockedRectanglesWorld = Array.Empty<Rect>();
        public StudyAIndependentObjectState[] Owners = Array.Empty<StudyAIndependentObjectState>();

        public string Failure { get; private set; } = string.Empty;
        public int ActiveOwnerRectangleCount
        {
            get
            {
                if (!Validate()) return -1;
                var count = 0;
                foreach (var owner in Owners) count += owner.CurrentNavigationRectangles().Length;
                return count;
            }
        }

        public bool IsBlockedPoint(Vector2 point)
        {
            if (!Validate() || !Finite(point.x) || !Finite(point.y) || !FrameWorldBounds.Contains(point)) return true;
            foreach (var rectangle in StaticBlockedRectanglesWorld)
                if (rectangle.Contains(point)) return true;
            foreach (var owner in Owners)
                foreach (var rectangle in owner.CurrentNavigationRectangles())
                    if (rectangle.Contains(point)) return true;
            return false;
        }

        /// <summary>Pixel-cell centre query in the explicit64PPU fixture registry; no art-alpha lookup.</summary>
        public bool IsCellBlocked(Vector2Int cell) => IsBlockedPoint(FrameWorldBounds.position +
            ((Vector2)cell + Vector2.one * .5f) / PixelsPerUnit);

        private bool Validate()
        {
            Failure = string.Empty;
            if (PixelsPerUnit <= 0 || !Finite(FrameWorldBounds.x) || !Finite(FrameWorldBounds.y) ||
                !Finite(FrameWorldBounds.width) || !Finite(FrameWorldBounds.height) ||
                FrameWorldBounds.width <= 0 || FrameWorldBounds.height <= 0 ||
                StaticBlockedRectanglesWorld == null || Owners == null)
            { Failure = "Explicit frame and static/navigation-owner references are required."; return false; }
            foreach (var rectangle in StaticBlockedRectanglesWorld)
                if (!Finite(rectangle.x) || !Finite(rectangle.y) || !Finite(rectangle.width) ||
                    !Finite(rectangle.height) || rectangle.width <= 0 || rectangle.height <= 0)
                { Failure = "Static navigation bodies must be finite positive rectangles."; return false; }
            foreach (var owner in Owners)
                if (owner == null || owner.gameObject.scene != gameObject.scene ||
                    (owner.gameObject.activeInHierarchy && !owner.enabled) || !owner.ValidateContract())
                { Failure = "Navigation owner is missing, invalid or belongs to a different Scene."; return false; }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
