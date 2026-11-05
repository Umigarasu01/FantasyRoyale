using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    // Camera transparency sort overrides are transient; restore this study's foot-Y contract on scene load.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class StudyACameraYSort : MonoBehaviour
    {
        private void OnEnable()
        {
            var camera = GetComponent<Camera>();
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.up;
        }
    }
}
