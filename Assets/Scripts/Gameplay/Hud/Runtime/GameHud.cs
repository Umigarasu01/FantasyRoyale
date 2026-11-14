using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>
    /// 黒漆のHUD。左上の枠にハートとコインを並べ、使える物の上に調べるキー（F）の吹き出しを出す。店の画面（`Shop`）も持つ。
    /// ゲームの状態は読まず、呼ぶ側が渡した値だけを表示する。見た目の決まりは
    /// `docs/fantasy-royale/product-specs/art/ui-design.md`にある。
    /// 寸法は全て1920×1080の画面での値で、`CanvasScaler`が実際の画面の高さに合わせて拡大縮小する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private const int CanvasSortingOrder = 100;
        private const float ScreenMargin = 24f;
        private const float PaddingX = 14f;
        private const float PaddingY = 12f;
        private static readonly Vector2 HeartSize = new Vector2(36f, 32f);
        private const float HeartGap = 4f;
        private const float RowGap = 6f;
        // 硬貨の絵は側面の厚みと王冠を見せるので、数字の高さより少し大きくする。
        private const float CoinSize = 30f;
        private const float CoinTextGap = 8f;
        private const float CoinTextWidth = 96f;
        // 数字のピクセルフォントは、1920×1080の画面で元の格子の2倍に描く。
        private const int NumberFontMultiple = 2;
        // ピクセルフォントがないときの組み込みフォントの大きさ。
        private const int FallbackFontSize = 26;
        // ハートが少なくても、コインと3桁の数字が枠に収まる幅。
        private const float CoinRowMinWidth = 96f;
        private const float PromptBodySize = 52f;
        private const float PromptKeySize = 32f;
        private static readonly Vector2 PointerSize = new Vector2(14f, 10f);
        // 吹き出しの三角は上の帯で枠の下の金の縁を隠すので、その分だけ枠へ重ねる。
        private const float PointerOverlap = 3f;
        private const float CursorSize = 32f;
        private const float ManaOrbSize = 16f;
        private const float ManaOrbGap = 3f;

        // カーソルの絵の中で矢印の先端がある位置（左下からの割合）。build_hud_sprites.pyのCURSOR_SHAPEの先頭と同じ。
        private static readonly Vector2 CursorTip = new Vector2(0.06f, 0.97f);

        private readonly List<Image> hearts = new List<Image>();
        private readonly List<HeartFill> heartFills = new List<HeartFill>();
        private readonly List<Image> manaOrbs = new List<Image>();
        private int litManaOrbs = -1;
        private RectTransform coinIcon;

        private HudSkin skin;
        private RectTransform statusPanel;
        private Text coinText;
        private int coins = -1;
        private RectTransform prompt;
        private readonly List<PixelText> pixelTexts = new List<PixelText>();
        private Canvas canvas;
        private float fontScale = -1f;
        private RectTransform cursor;

        /// <summary>今並んでいるハートの数。</summary>
        public int HeartCount => hearts.Count;

        /// <summary>表示しているコインの数。</summary>
        public int DisplayedCoins => coins;

        /// <summary>コインの数の文字。</summary>
        public string CoinText => coinText.text;

        public bool IsPromptVisible => prompt.gameObject.activeSelf;

        /// <summary>吹き出しの三角の先の、画面の位置（pixel）。</summary>
        public Vector2 PromptScreenPosition => prompt.position;

        /// <summary>左上の枠の大きさ（1920×1080の画面での値）。</summary>
        public Vector2 StatusPanelSize => statusPanel.sizeDelta;

        /// <summary>数字に使っているフォントと、その大きさ（Canvasの拡大縮小の前の値）。</summary>
        public Font NumberFont => coinText.font;
        public int NumberFontSize => coinText.fontSize;

        /// <summary>画面の下の真ん中の技の枠。</summary>
        public LoadoutBar Loadout { get; private set; }

        /// <summary>今並んでいるMPの玉の数と、満ちている数。</summary>
        public int ManaOrbCount => manaOrbs.Count;
        public int LitManaOrbs => Mathf.Max(0, litManaOrbs);

        /// <summary>持ち物の画面。開くまでは隠れている。</summary>
        public InventoryScreen Inventory { get; private set; }

        /// <summary>店の画面。開くまでは隠れている。</summary>
        public ShopScreen Shop { get; private set; }

        public bool IsCursorVisible => cursor.gameObject.activeSelf;

        /// <summary>カーソルの先端の、画面の位置（pixel）。</summary>
        public Vector2 CursorScreenPosition => RectTransformUtility.WorldToScreenPoint(EventCamera, cursor.position);

        /// <summary>CanvasのCamera。Screen Space Overlayではnullで、画面の位置の当たりを調べるときに渡す。</summary>
        internal Camera EventCamera => canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        /// <summary>画面に重ねるCanvasごとHUDを作る。</summary>
        public static GameHud Create(HudSkin skin)
        {
            if (skin == null)
            {
                throw new ArgumentNullException(nameof(skin));
            }

            var root = new GameObject("GameHud", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            // ピクセルフォントのドットを画面の画素に揃える。
            canvas.pixelPerfect = true;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f;

            var hud = root.AddComponent<GameHud>();
            hud.Build(skin);
            return hud;
        }

        public HeartFill GetHeartFill(int index)
        {
            return heartFills[index];
        }

        /// <summary>HPをハートで見せる。ハート1つ分のHPはゲームの決めごとなので、呼ぶ側が渡す。</summary>
        public void SetHealth(int currentHealth, int maximumHealth, int healthPerHeart)
        {
            var count = HeartGauge.CountHearts(maximumHealth, healthPerHeart);
            if (count != hearts.Count)
            {
                ResizeHearts(count);
            }

            for (var index = 0; index < hearts.Count; index++)
            {
                var fill = HeartGauge.GetFill(index, currentHealth, healthPerHeart);
                if (heartFills[index] != fill)
                {
                    heartFills[index] = fill;
                    hearts[index].sprite = skin.GetHeart(fill);
                }
            }
        }

        /// <summary>
        /// MPを、ハートの下に並べた青い玉で見せる。玉1つ分のMPはゲームの決めごとなので、呼ぶ側が渡す。
        /// 玉は満ちた分だけ光り、端数は光らせない。最大が0なら玉の段を出さない。
        /// </summary>
        public void SetMana(int currentMana, int maximumMana, int manaPerOrb)
        {
            var perOrb = Mathf.Max(1, manaPerOrb);
            var count = maximumMana <= 0 ? 0 : Mathf.CeilToInt(maximumMana / (float)perOrb);
            if (count != manaOrbs.Count)
            {
                while (manaOrbs.Count < count)
                {
                    manaOrbs.Add(NewImage($"Mana{manaOrbs.Count}", statusPanel, skin.ManaFull, Vector2.one * ManaOrbSize, new Vector2(0f, 1f)).GetComponent<Image>());
                }

                while (manaOrbs.Count > count)
                {
                    Destroy(manaOrbs[manaOrbs.Count - 1].gameObject);
                    manaOrbs.RemoveAt(manaOrbs.Count - 1);
                }

                litManaOrbs = -1;
                LayoutStatus();
            }

            var lit = Mathf.Clamp(currentMana / perOrb, 0, count);
            if (lit == litManaOrbs)
            {
                return;
            }

            litManaOrbs = lit;
            for (var index = 0; index < manaOrbs.Count; index++)
            {
                manaOrbs[index].sprite = index < lit ? skin.ManaFull : skin.ManaEmpty;
            }
        }

        public void SetCoins(int value)
        {
            if (value == coins)
            {
                return;
            }

            coins = value;
            coinText.text = value.ToString();
        }

        /// <summary>調べるキーの吹き出しを、三角の先が画面のscreenPosition（pixel）に来るように出す。</summary>
        public void ShowPrompt(Vector2 screenPosition)
        {
            prompt.position = screenPosition;
            if (!prompt.gameObject.activeSelf)
            {
                prompt.gameObject.SetActive(true);
            }
        }

        public void HidePrompt()
        {
            if (prompt.gameObject.activeSelf)
            {
                prompt.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 金の矢印のカーソルを、先端が画面のscreenPosition（pixel）に来るように出す。HUDと店の画面より手前に描く。
        /// OSのカーソルを隠すかどうかは呼ぶ側が決める。
        /// </summary>
        public void ShowCursor(Vector2 screenPosition)
        {
            if (cursor == null)
            {
                return;
            }

            if (EventCamera != null
                && RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform, screenPosition, EventCamera, out var world))
            {
                cursor.position = world;
            }
            else
            {
                cursor.position = screenPosition;
            }

            if (!cursor.gameObject.activeSelf)
            {
                cursor.gameObject.SetActive(true);
            }
        }

        public void HideCursor()
        {
            if (cursor != null && cursor.gameObject.activeSelf)
            {
                cursor.gameObject.SetActive(false);
            }
        }

        private void Build(HudSkin newSkin)
        {
            skin = newSkin;
            var root = (RectTransform)transform;

            statusPanel = NewImage("Status", root, skin.Panel, Vector2.zero, new Vector2(0f, 1f));
            statusPanel.anchoredPosition = new Vector2(ScreenMargin, -ScreenMargin);
            statusPanel.GetComponent<Image>().type = Image.Type.Sliced;

            coinIcon = NewImage("Coin", statusPanel, skin.Coin, Vector2.one * CoinSize, new Vector2(0f, 1f));

            canvas = GetComponent<Canvas>();
            var text = HudLayout.NewText("Coins", statusPanel, skin.NumberFont, new Vector2(CoinTextWidth, CoinSize), HudLayout.TopLeft, TextAnchor.MiddleLeft);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(0f, -2f);
            coinText = text;
            RegisterPixelText(text, NumberFontMultiple);
            SetCoins(0);

            BuildPrompt(root);
            ResizeHearts(0);
            Loadout = new LoadoutBar(this, root, skin);
            Inventory = new InventoryScreen(this, root, skin);
            Shop = new ShopScreen(this, root, skin);

            // カーソルは最後に作り、何よりも手前に描く。
            var cursorImage = HudLayout.NewImage("Cursor", root, skin.Cursor, Vector2.one * CursorSize, Vector2.zero);
            cursor = cursorImage.rectTransform;
            cursor.pivot = CursorTip;
            cursor.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (canvas != null && !Mathf.Approximately(canvas.scaleFactor, fontScale))
            {
                fontScale = canvas.scaleFactor;
                foreach (var pixelText in pixelTexts)
                {
                    Fit(pixelText);
                }
            }
        }

        /// <summary>
        /// ピクセルフォントの文字を登録する。画面上でいつも元の格子のmultiple倍（1920×1080の画面で）に描き、
        /// Canvasの倍率が変わったら大きさを合わせ直す。
        /// </summary>
        internal void RegisterPixelText(Text text, int multiple)
        {
            var pixelText = new PixelText(text, multiple);
            pixelTexts.Add(pixelText);
            Fit(pixelText);
        }

        private void Fit(PixelText pixelText)
        {
            var text = pixelText.Text;
            if (text == null)
            {
                return;
            }

            if (skin.NumberFont == null)
            {
                // ピクセルフォントがないときは組み込みのフォントを太字で使う。
                text.fontSize = FallbackFontSize * pixelText.Multiple / NumberFontMultiple;
                text.fontStyle = FontStyle.Bold;
                return;
            }

            // ピクセルフォントは太字の書体そのものを使い、Unityの擬似的な太字をかけない。
            text.fontStyle = FontStyle.Normal;
            var scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            text.fontSize = PixelFontSizing.GetFontSize(skin.NumberFontCell, pixelText.Multiple, scale);
        }

        private readonly struct PixelText
        {
            public PixelText(Text text, int multiple)
            {
                Text = text;
                Multiple = multiple;
            }

            public Text Text { get; }
            public int Multiple { get; }
        }

        private void BuildPrompt(RectTransform root)
        {
            prompt = (RectTransform)new GameObject("Prompt", typeof(RectTransform)).transform;
            prompt.SetParent(root, false);
            prompt.anchorMin = Vector2.zero;
            prompt.anchorMax = Vector2.zero;
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.sizeDelta = new Vector2(PromptBodySize, PromptBodySize + PointerSize.y - PointerOverlap);

            // 三角を後に作り、枠より手前に描いて枠の下の金の縁を隠す。
            var body = NewImage("Body", prompt, skin.Panel, Vector2.one * PromptBodySize, new Vector2(0.5f, 0f));
            body.GetComponent<Image>().type = Image.Type.Sliced;
            body.anchoredPosition = new Vector2(0f, PointerSize.y - PointerOverlap);
            var key = NewImage("Key", body, skin.InteractKey, Vector2.one * PromptKeySize, new Vector2(0.5f, 0.5f));
            key.anchoredPosition = Vector2.zero;
            var pointer = NewImage("Pointer", prompt, skin.Pointer, PointerSize, new Vector2(0.5f, 0f));
            pointer.anchoredPosition = Vector2.zero;
            prompt.gameObject.SetActive(false);
        }

        private void ResizeHearts(int count)
        {
            while (hearts.Count < count)
            {
                var heart = NewImage($"Heart{hearts.Count}", statusPanel, skin.HeartFull, HeartSize, new Vector2(0f, 1f));
                heart.anchoredPosition = new Vector2(PaddingX + hearts.Count * (HeartSize.x + HeartGap), -PaddingY);
                hearts.Add(heart.GetComponent<Image>());
                heartFills.Add(HeartFill.Full);
            }

            while (hearts.Count > count)
            {
                var last = hearts.Count - 1;
                Destroy(hearts[last].gameObject);
                hearts.RemoveAt(last);
                heartFills.RemoveAt(last);
            }

            LayoutStatus();
        }

        /// <summary>左上の枠の段を並べる。上からハート、MPの玉（あれば）、コインで、枠の大きさを中身に合わせる。</summary>
        private void LayoutStatus()
        {
            var y = -(PaddingY + HeartSize.y + RowGap);
            for (var index = 0; index < manaOrbs.Count; index++)
            {
                manaOrbs[index].rectTransform.anchoredPosition = new Vector2(PaddingX + 2f + index * (ManaOrbSize + ManaOrbGap), y);
            }

            if (manaOrbs.Count > 0)
            {
                y -= ManaOrbSize + RowGap;
            }

            coinIcon.anchoredPosition = new Vector2(PaddingX, y);
            coinText.rectTransform.anchoredPosition = new Vector2(PaddingX + CoinSize + CoinTextGap, y);

            var heartsWidth = hearts.Count * HeartSize.x + Mathf.Max(0, hearts.Count - 1) * HeartGap;
            var orbsWidth = manaOrbs.Count * ManaOrbSize + Mathf.Max(0, manaOrbs.Count - 1) * ManaOrbGap;
            statusPanel.sizeDelta = new Vector2(
                PaddingX * 2f + Mathf.Max(heartsWidth, orbsWidth, CoinRowMinWidth),
                PaddingY * 2f - y + CoinSize - PaddingY);
        }

        private static RectTransform NewImage(string name, RectTransform parent, Sprite sprite, Vector2 size, Vector2 anchor)
        {
            return HudLayout.NewImage(name, parent, sprite, size, anchor).rectTransform;
        }
    }
}
