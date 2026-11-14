using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>HUDと店の画面がuGUIの部品を置くときの共通の手順。寸法は1920×1080の画面での値である。</summary>
    internal static class HudLayout
    {
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        /// <summary>parentのanchorの位置を基準に、同じ点を軸としてsizeの大きさで置く。</summary>
        public static void Place(RectTransform rect, RectTransform parent, Vector2 size, Vector2 anchor)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
        }

        public static RectTransform NewRect(string name, RectTransform parent, Vector2 size, Vector2 anchor)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            Place(rect, parent, size, anchor);
            return rect;
        }

        /// <summary>parentいっぱいに広がる枠を作る。</summary>
        public static RectTransform NewStretch(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>絵を置く。spriteがないときは色だけの四角になる。</summary>
        public static Image NewImage(string name, RectTransform parent, Sprite sprite, Vector2 size, Vector2 anchor)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            Place(image.rectTransform, parent, size, anchor);
            return image;
        }

        /// <summary>9分割で伸ばす枠を置く。</summary>
        public static Image NewPanel(string name, RectTransform parent, Sprite sprite, Vector2 size, Vector2 anchor)
        {
            var image = NewImage(name, parent, sprite, size, anchor);
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Text NewText(string name, RectTransform parent, Font font, Vector2 size, Vector2 anchor, TextAnchor alignment)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            Place(text.rectTransform, parent, size, anchor);
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
