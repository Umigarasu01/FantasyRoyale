using UnityEngine;

namespace FantasyRoyale.Playtest.OverheadStudyA
{
    /// <summary>Runs only the supplied independent water-highlight frames in the component study.</summary>
    [DisallowMultipleComponent]
    public sealed class StudyAIndependentWaterAnimation : MonoBehaviour
    {
        public SpriteRenderer Surface;
        public Sprite[] Frames;
        public float FramesPerSecond = 8f;
        public bool Paused;

        public int FrameIndex { get; private set; }
        public int FrameChanges { get; private set; }
        public int CompletedLoops { get; private set; }
        public string Failure { get; private set; } = string.Empty;

        private double elapsed;
        private bool ready;

        private void OnEnable()
        {
            Failure = string.Empty;
            ready = false;
            elapsed = 0;
            FrameIndex = FrameChanges = CompletedLoops = 0;
            if (Surface == null || Frames == null || Frames.Length < 2 ||
                float.IsNaN(FramesPerSecond) || float.IsInfinity(FramesPerSecond) || FramesPerSecond <= 0f)
            {
                Failure = "A water renderer, registered frames and positive frame rate are required.";
                return;
            }
            var first = Frames[0];
            if (first == null) { Failure = "Water frame0 is missing."; return; }
            foreach (var frame in Frames)
                if (frame == null || frame.rect != first.rect || frame.pivot != first.pivot ||
                    frame.pixelsPerUnit != first.pixelsPerUnit)
                {
                    Failure = "Water frames must retain the same native canvas, pivot and pixels-per-unit.";
                    return;
                }
            Surface.sprite = first;
            ready = true;
        }

        private void Update()
        {
            if (!ready || Paused) return;
            elapsed += Time.deltaTime;
            var frameNumber = (long)(elapsed * FramesPerSecond);
            var next = (int)(frameNumber % Frames.Length);
            CompletedLoops = (int)(frameNumber / Frames.Length);
            if (next == FrameIndex) return;
            FrameIndex = next;
            Surface.sprite = Frames[next];
            FrameChanges++;
        }
    }
}
