using System;
using UnityEngine;

namespace FantasyRoyale.Playtest.OverheadStudyA
{
    /// <summary>Explicit present/absent capability for the isolated tree and bridge study; no combat rules.</summary>
    [DisallowMultipleComponent]
    public sealed class StudyAIndependentObjectState : MonoBehaviour
    {
        public string ObjectId;
        public GameObject[] PresentObjects = Array.Empty<GameObject>();
        public GameObject[] AbsentObjects = Array.Empty<GameObject>();
        public Rect[] NavigationRectanglesLocal = Array.Empty<Rect>();
        public Rect[] AbsentNavigationRectanglesLocal = Array.Empty<Rect>();
        [SerializeField] private bool present = true;

        public bool Present => present;
        public bool NavigationRegistered => isActiveAndEnabled && Validate() &&
            (present ? NavigationRectanglesLocal : AbsentNavigationRectanglesLocal)?.Length > 0;
        public int StateChanges { get; private set; }
        public string Failure { get; private set; } = string.Empty;

        public bool ValidateContract() => Validate();

        private void OnEnable() => Apply();

        public void SetPresent(bool value)
        {
            if (!Validate()) return;
            if (present != value) StateChanges++;
            present = value;
            Apply();
        }

        [ContextMenu("Study: remove owned object (Play only)")]
        private void RemoveOwnedObject()
        {
            if (Application.isPlaying) SetPresent(false);
        }

        [ContextMenu("Study: restore owned object (Play only)")]
        private void RestoreOwnedObject()
        {
            if (Application.isPlaying) SetPresent(true);
        }

        /// <summary>Owned navigation handles follow the object state; no permanent tree-solid raster is modified.</summary>
        public Rect[] CurrentNavigationRectangles()
        {
            if (!NavigationRegistered) return Array.Empty<Rect>();
            var rectangles = present ? NavigationRectanglesLocal : AbsentNavigationRectanglesLocal;
            var result = new Rect[rectangles.Length];
            for (var i = 0; i < result.Length; i++)
            {
                var local = rectangles[i];
                var a = transform.TransformPoint(new Vector3(local.xMin, local.yMin, 0));
                var b = transform.TransformPoint(new Vector3(local.xMax, local.yMax, 0));
                result[i] = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                    Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
            return result;
        }

        private void Apply()
        {
            if (!Validate()) return;
            foreach (var item in PresentObjects) item.SetActive(present);
            foreach (var item in AbsentObjects) item.SetActive(!present);
        }

        private bool Validate()
        {
            Failure = string.Empty;
            var scale = transform.lossyScale;
            if (Quaternion.Angle(transform.rotation, Quaternion.identity) > .001f ||
                !Finite(scale.x) || !Finite(scale.y) || !Finite(scale.z) ||
                scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
            { Failure = "Owned collision/navigation transforms must remain unrotated with finite positive scale."; return false; }
            if (PresentObjects == null || AbsentObjects == null || NavigationRectanglesLocal == null ||
                AbsentNavigationRectanglesLocal == null)
            { Failure = "Explicit object-owned references are required."; return false; }
            foreach (var item in PresentObjects)
                if (!ValidOwnedChild(item)) { Failure = "Present visual/body reference is not an owned child."; return false; }
            foreach (var item in AbsentObjects)
                if (!ValidOwnedChild(item)) { Failure = "Absent water-blocker reference is not an owned child."; return false; }
            foreach (var item in PresentObjects)
                foreach (var absent in AbsentObjects)
                    if (item == absent || item.transform.IsChildOf(absent.transform) || absent.transform.IsChildOf(item.transform))
                    { Failure = "Present and absent ownership roots must be separate."; return false; }
            foreach (var rectangles in new[] { NavigationRectanglesLocal, AbsentNavigationRectanglesLocal })
                foreach (var rectangle in rectangles)
                    if (rectangle.width <= 0 || rectangle.height <= 0 || float.IsNaN(rectangle.x) ||
                        float.IsNaN(rectangle.y) || float.IsInfinity(rectangle.x) || float.IsInfinity(rectangle.y) ||
                        float.IsNaN(rectangle.width) || float.IsNaN(rectangle.height) ||
                        float.IsInfinity(rectangle.width) || float.IsInfinity(rectangle.height))
                    { Failure = "Owned navigation rectangles must be finite, positive explicit bodies."; return false; }
            return true;
        }

        private bool ValidOwnedChild(GameObject item) => item != null && item != gameObject &&
            item.transform.IsChildOf(transform);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
