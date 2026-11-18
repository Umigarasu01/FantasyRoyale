using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>
    /// 店の画面と持ち物の画面で、物を掴んで運ぶ見た目。掴んだ物は元の枠ごと持ち上がり、すぐに絵だけの正方形へ縮んで
    /// カーソルの右下へ寄る。元の枠は運んでいる間薄くする。落とせる枠には金の囲みを脈打たせ、カーソルの下の枠は囲みを濃くして少し大きくする。
    /// 何を運べるか、どこへ落とせるかは呼ぶ側が決める。
    /// </summary>
    internal sealed class HudDragVisual
    {
        // 掴んだ枠は、この秒数で元の枠の形から正方形へ縮み、カーソルへ寄る。
        private const float ShrinkSeconds = 0.12f;
        private const float IconSize = 56f;
        private const float SourceAlpha = 0.35f;
        private const float PulseSpeed = 6f;
        private const float HoverScale = 1.08f;
        private const float GlowPadding = 10f;
        private static readonly Vector2 Offset = new Vector2(36f, -36f);

        private readonly RectTransform root;
        private readonly HudSkin skin;
        private readonly GameHud hud;
        private readonly Vector2 squareSize;
        private readonly Image frame;
        private readonly Image icon;
        private readonly List<(Image Frame, Image Glow)> lit = new List<(Image, Image)>();
        private readonly Dictionary<Image, Image> glows = new Dictionary<Image, Image>();
        private Vector2 startSize;
        private Vector3 startPosition;
        private float startTime;
        private CanvasGroup source;
        private RectTransform hovered;

        public HudDragVisual(GameHud hud, RectTransform root, HudSkin skin, Vector2 squareSize, Color frameColour)
        {
            this.root = root;
            this.skin = skin;
            this.hud = hud;
            this.squareSize = squareSize;
            frame = HudLayout.NewPanel("Drag", root, skin.Panel, squareSize, HudLayout.Centre);
            frame.color = frameColour;
            icon = HudLayout.NewImage("Icon", frame.rectTransform, null, new Vector2(IconSize, IconSize), HudLayout.Centre);
            icon.preserveAspect = true;
            frame.gameObject.SetActive(false);
        }

        public bool IsActive => frame.gameObject.activeSelf;
        public Sprite Sprite => icon.sprite;
        public Vector2 FrameSize => frame.rectTransform.sizeDelta;

        /// <summary>落とせる枠として光らせている枠の数。</summary>
        public int TargetCount => lit.Count;

        public bool IsTarget(Image targetFrame)
        {
            foreach (var (litFrame, _) in lit)
            {
                if (litFrame == targetFrame)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>sourceの枠ごと掴む。絵がなければ掴まない。</summary>
        public void Begin(RectTransform sourceRect, Sprite sprite, IEnumerable<Image> targets)
        {
            End();
            if (sprite == null || sourceRect == null)
            {
                return;
            }

            source = sourceRect.GetComponent<CanvasGroup>();
            if (source == null)
            {
                source = sourceRect.gameObject.AddComponent<CanvasGroup>();
            }

            source.alpha = SourceAlpha;
            foreach (var target in targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var glow = GetGlow(target);
                glow.enabled = true;
                glow.transform.SetAsLastSibling();
                lit.Add((target, glow));
            }

            icon.sprite = sprite;
            startSize = sourceRect.rect.size;
            startPosition = sourceRect.TransformPoint(sourceRect.rect.center);
            startTime = Time.unscaledTime;
            frame.rectTransform.sizeDelta = startSize;
            frame.rectTransform.position = startPosition;
            frame.gameObject.SetActive(true);
            frame.transform.SetAsLastSibling();
        }

        /// <summary>カーソルへ寄せ、hoveredFrameが落とせる枠なら強く光らせる。</summary>
        public void Move(Vector2 screenPosition, Image hoveredFrame)
        {
            if (!IsActive)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(root, screenPosition + Offset, hud.EventCamera, out var world))
            {
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - startTime) / ShrinkSeconds));
                frame.rectTransform.sizeDelta = Vector2.Lerp(startSize, squareSize, t);
                frame.rectTransform.position = Vector3.Lerp(startPosition, world, t);
            }

            var target = hoveredFrame != null && IsTarget(hoveredFrame) ? hoveredFrame : null;
            SetHovered(target == null ? null : target.rectTransform);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed);
            foreach (var (litFrame, glow) in lit)
            {
                glow.color = new Color(1f, 1f, 1f, litFrame == target ? 1f : 0.3f + 0.5f * pulse);
            }
        }

        public void End()
        {
            frame.gameObject.SetActive(false);
            icon.sprite = null;
            if (source != null)
            {
                source.alpha = 1f;
                source = null;
            }

            foreach (var (_, glow) in lit)
            {
                if (glow != null)
                {
                    glow.enabled = false;
                }
            }

            lit.Clear();
            SetHovered(null);
        }

        private void SetHovered(RectTransform target)
        {
            if (hovered == target)
            {
                return;
            }

            if (hovered != null)
            {
                hovered.localScale = Vector3.one;
            }

            hovered = target;
            if (target != null)
            {
                target.localScale = Vector3.one * HoverScale;
            }
        }

        // 枠に重ねる金の囲み。四角い枠には四角の囲み、丸い枠には丸い囲みを、初めて要るときに作って使い回す。
        private Image GetGlow(Image target)
        {
            if (glows.TryGetValue(target, out var glow) && glow != null)
            {
                return glow;
            }

            var size = target.rectTransform.rect.size + Vector2.one * GlowPadding;
            glow = target.type == Image.Type.Sliced
                ? HudLayout.NewPanel("DropGlow", target.rectTransform, skin.Select, size, HudLayout.Centre)
                : HudLayout.NewImage("DropGlow", target.rectTransform, skin.SelectRound, size, HudLayout.Centre);
            glow.enabled = false;
            glows[target] = glow;
            return glow;
        }
    }
}
