using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>
    /// 店の画面。店の物を1枚の広い枠にまとめ、上に店主の顔と台詞、その下の左に商品の行の一覧、右に選んだ商品の説明を置く。
    /// 自分の持ち物は右の細い枠に置く。見た目の決まりは`docs/fantasy-royale/product-specs/art/ui-design.md`の店の画面にある。
    /// ゲームの状態は読まず、`Show`で渡された値だけを出す。寸法は1920×1080の画面での値である。
    /// </summary>
    /// <summary>画面の位置が当たった、店の画面の部分。</summary>
    public enum ShopHitKind
    {
        None,
        Item,
        Buy,
        Close
    }

    /// <summary>画面の位置が、店の画面のどこに当たるか。</summary>
    public readonly struct ShopHit
    {
        public ShopHit(ShopHitKind kind, int itemIndex)
        {
            Kind = kind;
            ItemIndex = itemIndex;
        }

        public ShopHitKind Kind { get; }
        /// <summary>品物の行に当たったときの、品物の番号。ほかは-1。</summary>
        public int ItemIndex { get; }
    }

    public sealed class ShopScreen
    {
        /// <summary>一度に見せる行の数。多いときは選んだ行が見えるように送る。</summary>
        public const int VisibleRows = 8;
        public const int StatRows = 3;
        public const int BagSlots = 10;
        public const int QuickSlots = 5;

        private static readonly Vector2 ContainerSize = new Vector2(1560f, 820f);
        private const float ContainerOffsetY = -30f;
        private static readonly Vector2 ShopPanelSize = new Vector2(1100f, 820f);
        private static readonly Vector2 InventoryPanelSize = new Vector2(440f, 820f);
        private const float Margin = 24f;
        private const float ListTop = -216f;
        private static readonly Vector2 RowSize = new Vector2(460f, 64f);
        private const float RowStep = 72f;
        private static readonly Vector2 DetailSize = new Vector2(568f, 588f);
        private const float DetailLeft = 508f;
        private const float StatTop = -320f;
        private const float StatStep = 40f;
        private const float StatBarWidth = 240f;

        // 線形の色空間では、黒の不透明度が数値ほど暗く見えないので濃くする。0.75で後ろが約半分の明るさになる。
        private static readonly Color DimColour = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color RowColour = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color InnerColour = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color GoldText = new Color(0.96f, 0.82f, 0.48f, 1f);
        private static readonly Color MutedText = new Color(0.62f, 0.62f, 0.64f, 1f);
        private static readonly Color DividerColour = new Color(0.55f, 0.42f, 0.18f, 1f);
        private static readonly Color BarBackColour = new Color(0.12f, 0.12f, 0.14f, 1f);
        private static readonly Color BarColour = new Color(0.9f, 0.72f, 0.32f, 1f);
        private static readonly Color BetterColour = new Color(0.45f, 0.9f, 0.4f, 1f);
        private static readonly Color WorseColour = new Color(0.95f, 0.42f, 0.36f, 1f);
        private static readonly Color MessageColour = new Color(0.98f, 0.62f, 0.52f, 1f);
        private static readonly Color KeyLetterColour = new Color(0.07f, 0.07f, 0.08f, 1f);
        // 乗ったキーは白のまま少し大きくする。金にすると、金の矢印のカーソルと見分けにくい。
        private const float KeyHoverScale = 1.12f;

        private readonly GameHud hud;
        private readonly HudSkin skin;
        private readonly RectTransform root;
        private readonly Image portrait;
        private readonly Text keeperName;
        private readonly Text dialogue;
        private readonly Row[] rows = new Row[VisibleRows];
        private readonly Image select;
        private readonly Image arrow;
        private readonly Image detailIcon;
        private readonly Image detailRarity;
        private readonly Text detailName;
        private readonly Text detailStatus;
        private readonly Text detailDescription;
        private readonly Stat[] stats = new Stat[StatRows];
        private readonly Image priceCoin;
        private readonly Text price;
        private readonly Text message;
        private readonly Image head;
        private readonly Image torso;
        private readonly Image legs;
        private readonly Image weapon;
        private readonly Image offHand;
        private readonly Image[] placeholders = new Image[5];
        private readonly Image[] quick = new Image[QuickSlots];
        private readonly Image[] bag = new Image[BagSlots];
        private readonly Key closeKey;
        private readonly Key buyKey;
        private int firstRow;
        private ShopHitKind hovered;

        internal ShopScreen(GameHud hud, RectTransform parent, HudSkin skin)
        {
            this.hud = hud;
            this.skin = skin;
            root = HudLayout.NewStretch("Shop", parent);
            // 左上のハートとコインは店の画面より手前に描き、暗くしない。
            root.SetAsFirstSibling();
            var dim = HudLayout.NewImage("Dim", root, null, Vector2.zero, HudLayout.Centre);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.color = DimColour;

            var container = HudLayout.NewRect("Panels", root, ContainerSize, HudLayout.Centre);
            container.anchoredPosition = new Vector2(0f, ContainerOffsetY);
            var shop = HudLayout.NewPanel("ShopPanel", container, skin.Panel, ShopPanelSize, HudLayout.TopLeft).rectTransform;
            var inventory = HudLayout.NewPanel("InventoryPanel", container, skin.Panel, InventoryPanelSize, HudLayout.TopRight).rectTransform;

            var frame = HudLayout.NewPanel("PortraitFrame", shop, skin.Panel, new Vector2(152f, 152f), HudLayout.TopLeft).rectTransform;
            frame.anchoredPosition = new Vector2(Margin, -Margin);
            portrait = HudLayout.NewImage("Portrait", frame, null, new Vector2(120f, 104f), HudLayout.Centre);
            portrait.preserveAspect = true;
            keeperName = NewText("KeeperName", shop, new Vector2(800f, 32f), new Vector2(196f, -34f), true, TextAnchor.UpperLeft, GoldText);
            dialogue = NewText("Dialogue", shop, new Vector2(800f, 96f), new Vector2(196f, -80f), false, TextAnchor.UpperLeft, Color.white);
            closeKey = NewKey("Close", shop, "F", "閉じる", 44f, new Vector2(-Margin, -Margin));
            var divider = HudLayout.NewImage("Divider", shop, null, new Vector2(ShopPanelSize.x - 2f * Margin, 2f), HudLayout.TopLeft);
            divider.rectTransform.anchoredPosition = new Vector2(Margin, -196f);
            divider.color = DividerColour;

            var list = HudLayout.NewRect("List", shop, new Vector2(RowSize.x, RowStep * VisibleRows), HudLayout.TopLeft);
            list.anchoredPosition = new Vector2(Margin, ListTop);
            for (var index = 0; index < VisibleRows; index++)
            {
                rows[index] = new Row(this, list, index);
            }

            select = HudLayout.NewPanel("Select", list, skin.Select, RowSize + new Vector2(8f, 8f), HudLayout.TopLeft);
            arrow = HudLayout.NewImage("Arrow", list, skin.Arrow, new Vector2(12f, 18f), HudLayout.TopLeft);

            var detail = HudLayout.NewPanel("Detail", shop, skin.Panel, DetailSize, HudLayout.TopLeft);
            detail.color = InnerColour;
            var detailRect = detail.rectTransform;
            detailRect.anchoredPosition = new Vector2(DetailLeft, ListTop);
            detailIcon = NewSlot("Icon", detailRect, new Vector2(Margin, -Margin), 128f, 96f);
            detailRarity = HudLayout.NewImage("Rarity", detailRect, null, new Vector2(4f, 32f), HudLayout.TopLeft);
            detailRarity.rectTransform.anchoredPosition = new Vector2(172f, -28f);
            detailName = NewText("Name", detailRect, new Vector2(360f, 32f), new Vector2(184f, -28f), true, TextAnchor.UpperLeft, Color.white);
            detailStatus = NewText("Status", detailRect, new Vector2(360f, 32f), new Vector2(184f, -68f), false, TextAnchor.UpperLeft, MutedText);
            detailDescription = NewText("Description", detailRect, new Vector2(DetailSize.x - 2f * Margin, 140f), new Vector2(Margin, -170f), false, TextAnchor.UpperLeft, Color.white);
            for (var index = 0; index < StatRows; index++)
            {
                stats[index] = new Stat(this, detailRect, StatTop - index * StatStep);
            }

            var line = HudLayout.NewImage("Divider", detailRect, null, new Vector2(DetailSize.x - 2f * Margin, 2f), HudLayout.TopLeft);
            line.rectTransform.anchoredPosition = new Vector2(Margin, -468f);
            line.color = DividerColour;
            priceCoin = HudLayout.NewImage("Coin", detailRect, skin.Coin, new Vector2(40f, 40f), HudLayout.TopLeft);
            priceCoin.rectTransform.anchoredPosition = new Vector2(Margin, -488f);
            price = NewText("Price", detailRect, new Vector2(200f, 48f), new Vector2(76f, -484f), true, TextAnchor.MiddleLeft, Color.white, 3);
            buyKey = NewKey("Buy", detailRect, "U", "買う", 48f, new Vector2(-Margin, -484f));
            message = NewText("Message", detailRect, new Vector2(DetailSize.x - 2f * Margin, 32f), new Vector2(Margin, -544f), false, TextAnchor.UpperLeft, MessageColour);

            // 持ち物の画面を縮めた形にする。装備は体の形、その下に使用枠とカバン。
            NewText("Title", inventory, new Vector2(300f, 32f), new Vector2(Margin, -Margin), true, TextAnchor.UpperLeft, GoldText).text = "持ち物";
            const float crossLeft = 94f;
            const float crossStep = 88f;
            // メイン武器は右手として右の縦に長い枠、オフハンドは左に置く（2026-11-13、所有者）。
            const float mainHandHeight = 124f;
            var mainHandTop = -72f - crossStep + (mainHandHeight - 80f) * 0.5f;
            head = NewSlot("Head", inventory, new Vector2(crossLeft + crossStep, -72f), 80f, 64f);
            offHand = NewSlot("OffHand", inventory, new Vector2(crossLeft, -72f - crossStep), 80f, 64f);
            torso = NewSlot("Torso", inventory, new Vector2(crossLeft + crossStep, -72f - crossStep), 80f, 64f);
            weapon = NewSlot("Weapon", inventory, new Vector2(crossLeft + crossStep * 2f, mainHandTop), 80f, 64f);
            ((RectTransform)weapon.transform.parent).sizeDelta = new Vector2(80f, mainHandHeight);
            legs = NewSlot("Legs", inventory, new Vector2(crossLeft + crossStep, -72f - crossStep * 2f), 80f, 64f);

            // 空の装備の枠には、何を着ける枠かを影の絵で薄く出す。並びは頭、胴、足、メイン武器、オフハンド。
            var equipmentIcons = new[] { head, torso, legs, weapon, offHand };
            for (var index = 0; index < equipmentIcons.Length; index++)
            {
                var placeholder = HudLayout.NewImage("Placeholder", (RectTransform)equipmentIcons[index].transform.parent, null, new Vector2(64f, 64f), HudLayout.Centre);
                placeholder.preserveAspect = true;
                placeholder.enabled = false;
                placeholder.transform.SetSiblingIndex(0);
                placeholders[index] = placeholder;
            }
            var quickLine = HudLayout.NewImage("Divider", inventory, null, new Vector2(InventoryPanelSize.x - 2f * Margin, 2f), HudLayout.TopLeft);
            quickLine.rectTransform.anchoredPosition = new Vector2(Margin, -348f);
            quickLine.color = DividerColour;
            NewText("QuickTitle", inventory, new Vector2(300f, 32f), new Vector2(Margin, -362f), true, TextAnchor.UpperLeft, GoldText).text = "使用枠";
            for (var index = 0; index < QuickSlots; index++)
            {
                quick[index] = NewSlot($"Quick{index}", inventory, new Vector2(Margin + index * 78f, -402f), 70f, 56f);
            }

            NewText("BagTitle", inventory, new Vector2(300f, 32f), new Vector2(Margin, -496f), true, TextAnchor.UpperLeft, GoldText).text = "カバン";
            for (var index = 0; index < BagSlots; index++)
            {
                bag[index] = NewSlot($"Bag{index}", inventory, new Vector2(Margin + index % 5 * 78f, -536f - index / 5 * 78f), 70f, 56f);
            }

            root.gameObject.SetActive(false);
        }

        public bool IsVisible => root.gameObject.activeSelf;

        /// <summary>見えている行の数。</summary>
        public int RowCount { get; private set; }

        /// <summary>一覧の一番上に見えている商品の番号。</summary>
        public int FirstRow => firstRow;

        public string KeeperNameText => keeperName.text;
        public string DialogueText => dialogue.text;
        public Sprite KeeperPortrait => portrait.sprite;
        public string SelectedNameText => detailName.text;
        public string MessageText => message.text;
        public Sprite WeaponIcon => weapon.sprite;
        public Sprite HeadIcon => head.sprite;
        public Sprite TorsoIcon => torso.sprite;
        public Sprite LegsIcon => legs.sprite;
        public Sprite OffHandIcon => offHand.sprite;
        /// <summary>両手の武器が埋めて、オフハンドの枠を灰色にしているか。</summary>
        public bool IsOffHandGreyed { get; private set; }
        /// <summary>メイン武器とオフハンドの枠に影の絵を出しているか。</summary>
        public bool IsOffHandPlaceholderVisible => placeholders[4].enabled;
        public bool IsHeadPlaceholderVisible => placeholders[0].enabled;
        public Sprite GetQuickIcon(int index) => quick[index].sprite;

        /// <summary>マウスが乗っている所。乗っているキーは少し大きくなり、横の言葉が金になる。</summary>
        public ShopHitKind Hovered => hovered;

        public string GetRowName(int visibleIndex) => rows[visibleIndex].Name.text;
        public string GetRowStatus(int visibleIndex) => rows[visibleIndex].Status.text;
        public Sprite GetBagIcon(int index) => bag[index].sprite;

        /// <summary>店の画面を、渡された値で出す。開いている間に値が変わったら、もう一度呼ぶ。</summary>
        public void Show(ShopScreenModel model)
        {
            portrait.sprite = model.KeeperPortrait;
            portrait.enabled = model.KeeperPortrait != null;
            if (model.KeeperPortrait != null)
            {
                // ドット絵の顔は元の大きさで出し、点を画面の画素に揃える。
                portrait.SetNativeSize();
            }

            keeperName.text = model.KeeperName;
            dialogue.text = model.Dialogue;
            ShowRows(model);
            ShowDetail(model.Selected >= 0 ? model.Items[model.Selected] : null);
            message.text = model.Message;
            message.color = model.MessageIsWarning ? MessageColour : BetterColour;
            ShowInventory(model.Inventory);
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
            SetHover(new ShopHit(ShopHitKind.None, -1));
        }

        /// <summary>
        /// 画面の位置（pixel）が、品物の行、買うキー、閉じるキーのどれに当たるかを返す。キーは横の言葉にも当たる。
        /// </summary>
        public ShopHit HitTest(Vector2 screenPosition)
        {
            if (!IsVisible)
            {
                return new ShopHit(ShopHitKind.None, -1);
            }

            var camera = hud.EventCamera;
            if (buyKey.Contains(screenPosition, camera))
            {
                return new ShopHit(ShopHitKind.Buy, -1);
            }

            if (closeKey.Contains(screenPosition, camera))
            {
                return new ShopHit(ShopHitKind.Close, -1);
            }

            for (var index = 0; index < RowCount; index++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(rows[index].Rect, screenPosition, camera))
                {
                    return new ShopHit(ShopHitKind.Item, firstRow + index);
                }
            }

            return new ShopHit(ShopHitKind.None, -1);
        }

        /// <summary>マウスが乗っている所を示す。買うキーと閉じるキーは少し大きくなり、言葉が金になる。品物の行は、呼ぶ側が選んで金の囲みを動かす。</summary>
        public void SetHover(ShopHit hit)
        {
            hovered = hit.Kind;
            buyKey.SetHover(hit.Kind == ShopHitKind.Buy);
            closeKey.SetHover(hit.Kind == ShopHitKind.Close);
        }

        /// <summary>見えている行の中央の、画面の位置。Testがマウスの位置を作るのに使う。</summary>
        public Vector2 GetRowScreenCentre(int visibleIndex) => ScreenCentre(rows[visibleIndex].Rect);

        public Vector2 BuyScreenCentre => ScreenCentre(buyKey.Cap);

        public Vector2 CloseScreenCentre => ScreenCentre(closeKey.Cap);

        private Vector2 ScreenCentre(RectTransform rect)
        {
            var world = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(hud.EventCamera, world);
        }

        private void ShowRows(ShopScreenModel model)
        {
            var count = model.Items.Count;
            if (model.Selected < firstRow)
            {
                firstRow = model.Selected;
            }
            else if (model.Selected >= firstRow + VisibleRows)
            {
                firstRow = model.Selected - VisibleRows + 1;
            }

            firstRow = Mathf.Clamp(firstRow, 0, Mathf.Max(0, count - VisibleRows));
            RowCount = Mathf.Min(VisibleRows, count);
            for (var index = 0; index < VisibleRows; index++)
            {
                var itemIndex = firstRow + index;
                rows[index].Show(itemIndex < count ? model.Items[itemIndex] : null);
            }

            var selectedRow = model.Selected - firstRow;
            var hasSelection = model.Selected >= 0;
            select.enabled = hasSelection;
            arrow.enabled = hasSelection;
            if (hasSelection)
            {
                var top = -selectedRow * RowStep;
                select.rectTransform.anchoredPosition = new Vector2(-4f, top + 4f);
                select.transform.SetAsLastSibling();
                arrow.rectTransform.anchoredPosition = new Vector2(RowSize.x + 6f, top - (RowSize.y - 18f) / 2f);
                arrow.transform.SetAsLastSibling();
            }
        }

        private void ShowDetail(ShopItemView item)
        {
            SetIcon(detailIcon, item?.Icon);
            detailRarity.enabled = item != null;
            detailRarity.color = item == null ? Color.clear : GetRarityColour(item.Rarity);
            detailName.text = item?.Name ?? string.Empty;
            detailStatus.text = item?.Status ?? string.Empty;
            detailDescription.text = item?.Description ?? string.Empty;
            for (var index = 0; index < StatRows; index++)
            {
                stats[index].Show(item != null && index < item.Stats.Count ? item.Stats[index] : null);
            }

            priceCoin.enabled = item != null;
            price.text = item == null ? string.Empty : item.Price.ToString();
        }

        private void ShowInventory(ShopInventoryView inventory)
        {
            SetIcon(head, inventory.Head);
            SetIcon(torso, inventory.Torso);
            SetIcon(legs, inventory.Legs);
            SetIcon(weapon, inventory.Weapon);
            // 両手の武器が埋めたオフハンドの枠は、絵を出さずに灰色にする。
            SetIcon(offHand, inventory.TwoHanded ? null : inventory.OffHand);
            offHand.transform.parent.GetComponent<Image>().color = inventory.TwoHanded ? new Color(0.22f, 0.22f, 0.24f, 1f) : RowColour;
            IsOffHandGreyed = inventory.TwoHanded;

            // 空の枠の影の絵は、着けたら消す。灰色にしたオフハンドでは、さらに薄くする。
            var worn = new[] { inventory.Head, inventory.Torso, inventory.Legs, inventory.Weapon, inventory.TwoHanded ? null : inventory.OffHand };
            for (var index = 0; index < placeholders.Length; index++)
            {
                var placeholder = index < inventory.EquipmentPlaceholders.Count ? inventory.EquipmentPlaceholders[index] : null;
                placeholders[index].sprite = placeholder;
                placeholders[index].enabled = placeholder != null && worn[index] == null;
                placeholders[index].color = index == 4 && inventory.TwoHanded ? new Color(1f, 1f, 1f, 0.015f) : new Color(1f, 1f, 1f, 0.035f);
            }
            for (var index = 0; index < QuickSlots; index++)
            {
                SetIcon(quick[index], index < inventory.Quick.Count ? inventory.Quick[index] : null);
                quick[index].transform.parent.gameObject.SetActive(index < inventory.Quick.Count);
            }

            // カバンは持っている数だけ枠を出す。
            for (var index = 0; index < BagSlots; index++)
            {
                SetIcon(bag[index], index < inventory.Bag.Count ? inventory.Bag[index] : null);
                bag[index].transform.parent.gameObject.SetActive(index < inventory.Bag.Count);
            }
        }

        public static Color GetRarityColour(ShopRarity rarity)
        {
            switch (rarity)
            {
                case ShopRarity.Rare:
                    return new Color(0.32f, 0.58f, 1f, 1f);
                case ShopRarity.Epic:
                    return new Color(0.72f, 0.38f, 1f, 1f);
                case ShopRarity.Legendary:
                    return new Color(1f, 0.78f, 0.25f, 1f);
                default:
                    return new Color(0.88f, 0.88f, 0.9f, 1f);
            }
        }

        private static void SetIcon(Image icon, Sprite sprite)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        private Text NewText(string name, RectTransform parent, Vector2 size, Vector2 position, bool bold, TextAnchor alignment, Color colour, int multiple = 2)
        {
            var anchor = position.x < 0f ? HudLayout.TopRight : HudLayout.TopLeft;
            var text = HudLayout.NewText(name, parent, bold ? skin.NumberFont : skin.TextFont, size, anchor, alignment);
            text.rectTransform.anchoredPosition = position;
            text.color = colour;
            hud.RegisterPixelText(text, multiple);
            return text;
        }

        /// <summary>枠の中央に絵を置く。返すのは中の絵である。</summary>
        private Image NewSlot(string name, RectTransform parent, Vector2 position, float slotSize, float iconSize)
        {
            var slot = HudLayout.NewPanel(name, parent, skin.Panel, new Vector2(slotSize, slotSize), HudLayout.TopLeft);
            slot.color = RowColour;
            slot.rectTransform.anchoredPosition = position;
            var icon = HudLayout.NewImage("Icon", slot.rectTransform, null, new Vector2(iconSize, iconSize), HudLayout.Centre);
            icon.preserveAspect = true;
            icon.enabled = false;
            return icon;
        }

        /// <summary>キーの絵に文字を乗せ、左に何をするかを書く。positionは右上の角からの位置。</summary>
        private Key NewKey(string name, RectTransform parent, string letter, string label, float size, Vector2 position)
        {
            var key = HudLayout.NewImage(name, parent, skin.KeyCap, new Vector2(size, size), HudLayout.TopRight);
            key.rectTransform.anchoredPosition = position;
            var keyText = NewText("Letter", key.rectTransform, new Vector2(size, size * 0.86f), Vector2.zero, true, TextAnchor.MiddleCenter, KeyLetterColour);
            keyText.text = letter;
            var labelText = NewText(name + "Label", parent, new Vector2(140f, size), new Vector2(position.x - size - 10f, position.y), false, TextAnchor.MiddleRight, Color.white);
            labelText.text = label;
            return new Key(key, labelText);
        }

        /// <summary>押せるキーと、その横の言葉。</summary>
        private sealed class Key
        {
            private readonly Image cap;
            private readonly Text label;

            public Key(Image cap, Text label)
            {
                this.cap = cap;
                this.label = label;
            }

            public RectTransform Cap => cap.rectTransform;

            /// <summary>キーか、横の言葉の右半分（言葉は右寄せで、左半分は空いている）に当たるか。</summary>
            public bool Contains(Vector2 screenPosition, Camera camera)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(cap.rectTransform, screenPosition, camera))
                {
                    return true;
                }

                var labelRect = label.rectTransform;
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(labelRect, screenPosition, camera, out var local)
                       && labelRect.rect.Contains(local)
                       && local.x > labelRect.rect.center.x;
            }

            public void SetHover(bool hover)
            {
                cap.rectTransform.localScale = Vector3.one * (hover ? KeyHoverScale : 1f);
                label.color = hover ? GoldText : Color.white;
            }
        }

        private sealed class Row
        {
            private readonly RectTransform rect;
            private readonly Image rarity;
            private readonly Image icon;
            private readonly Image coin;
            private readonly Text price;

            public Row(ShopScreen screen, RectTransform list, int index)
            {
                var panel = HudLayout.NewPanel($"Row{index}", list, screen.skin.Panel, RowSize, HudLayout.TopLeft);
                panel.color = RowColour;
                rect = panel.rectTransform;
                rect.anchoredPosition = new Vector2(0f, -index * RowStep);
                rarity = HudLayout.NewImage("Rarity", rect, null, new Vector2(4f, 40f), HudLayout.TopLeft);
                rarity.rectTransform.anchoredPosition = new Vector2(12f, -12f);
                icon = HudLayout.NewImage("Icon", rect, null, new Vector2(48f, 48f), HudLayout.TopLeft);
                icon.rectTransform.anchoredPosition = new Vector2(24f, -8f);
                icon.preserveAspect = true;
                Name = screen.NewText("Name", rect, new Vector2(230f, RowSize.y), new Vector2(84f, 0f), true, TextAnchor.MiddleLeft, Color.white);
                coin = HudLayout.NewImage("Coin", rect, screen.skin.Coin, new Vector2(26f, 26f), HudLayout.TopRight);
                coin.rectTransform.anchoredPosition = new Vector2(-104f, -19f);
                price = screen.NewText("Price", rect, new Vector2(84f, RowSize.y), new Vector2(-14f, 0f), true, TextAnchor.MiddleRight, Color.white);
                Status = screen.NewText("Status", rect, new Vector2(160f, RowSize.y), new Vector2(-14f, 0f), false, TextAnchor.MiddleRight, MutedText);
            }

            public Text Name { get; }
            public Text Status { get; }
            public RectTransform Rect => rect;

            public void Show(ShopItemView item)
            {
                rect.gameObject.SetActive(item != null);
                if (item == null)
                {
                    Name.text = string.Empty;
                    Status.text = string.Empty;
                    return;
                }

                rarity.color = GetRarityColour(item.Rarity);
                SetIcon(icon, item.Icon);
                Name.text = item.Name;
                var hasStatus = item.Status.Length > 0;
                Status.text = item.Status;
                coin.enabled = !hasStatus;
                price.text = hasStatus ? string.Empty : item.Price.ToString();
            }
        }

        private sealed class Stat
        {
            private readonly Text label;
            private readonly Image back;
            private readonly Image fill;
            private readonly Text value;
            private readonly Text difference;

            public Stat(ShopScreen screen, RectTransform detail, float top)
            {
                label = screen.NewText("StatLabel", detail, new Vector2(100f, 32f), new Vector2(Margin, top), false, TextAnchor.MiddleLeft, MutedText);
                back = HudLayout.NewImage("StatBack", detail, null, new Vector2(StatBarWidth, 12f), HudLayout.TopLeft);
                back.rectTransform.anchoredPosition = new Vector2(130f, top - 10f);
                back.color = BarBackColour;
                fill = HudLayout.NewImage("StatFill", detail, null, new Vector2(StatBarWidth, 12f), HudLayout.TopLeft);
                fill.rectTransform.anchoredPosition = new Vector2(130f, top - 10f);
                fill.color = BarColour;
                value = screen.NewText("StatValue", detail, new Vector2(64f, 32f), new Vector2(380f, top), true, TextAnchor.MiddleRight, Color.white);
                difference = screen.NewText("StatDifference", detail, new Vector2(90f, 32f), new Vector2(456f, top), true, TextAnchor.MiddleLeft, BetterColour);
            }

            public void Show(ShopStatView stat)
            {
                var visible = stat != null;
                label.enabled = visible;
                back.enabled = visible;
                fill.enabled = visible;
                value.enabled = visible;
                difference.enabled = visible && stat.Difference != 0;
                if (!visible)
                {
                    return;
                }

                label.text = stat.Label;
                fill.rectTransform.sizeDelta = new Vector2(StatBarWidth * Mathf.Clamp01((float)stat.Value / stat.Maximum), 12f);
                value.text = stat.Value.ToString();
                difference.text = stat.Difference > 0 ? $"+{stat.Difference}" : stat.Difference.ToString();
                difference.color = stat.Difference > 0 ? BetterColour : WorseColour;
            }
        }
    }
}
