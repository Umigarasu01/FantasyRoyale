using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>持ち物の画面の枠の種類。</summary>
    public enum InventoryCellKind
    {
        Equipment,
        Skill,
        Magic,
        Quick,
        Bag
    }

    /// <summary>持ち物の画面に出す値。ゲームの状態は持たず、呼ぶ側が所持品から作って渡す。</summary>
    public sealed class InventoryScreenModel
    {
        public InventoryScreenModel(
            IReadOnlyList<Sprite> equipment,
            bool twoHanded,
            Sprite skill,
            IReadOnlyList<Sprite> magics,
            int selectedMagic,
            IReadOnlyList<Sprite> quick,
            IReadOnlyList<Sprite> bag,
            int cursor,
            Sprite detailIcon,
            string detailName,
            string detailDescription,
            string actionLabel,
            string message,
            bool messageIsWarning,
            IReadOnlyList<Sprite> equipmentPlaceholders = null)
        {
            Equipment = equipment;
            TwoHanded = twoHanded;
            Skill = skill;
            Magics = magics ?? new Sprite[0];
            SelectedMagic = selectedMagic;
            Quick = quick ?? new Sprite[0];
            Bag = bag ?? new Sprite[0];
            Cursor = cursor;
            DetailIcon = detailIcon;
            DetailName = detailName ?? string.Empty;
            DetailDescription = detailDescription ?? string.Empty;
            ActionLabel = actionLabel ?? string.Empty;
            Message = message ?? string.Empty;
            MessageIsWarning = messageIsWarning;
            EquipmentPlaceholders = equipmentPlaceholders ?? new Sprite[0];
        }

        /// <summary>頭、胴、足、メイン武器、オフハンドの順の5つの絵。</summary>
        public IReadOnlyList<Sprite> Equipment { get; }

        /// <summary>両手の武器を持っているか。オフハンドの枠はメイン武器と同じ物で埋まって見せる。</summary>
        public bool TwoHanded { get; }

        public Sprite Skill { get; }
        public IReadOnlyList<Sprite> Magics { get; }
        public int SelectedMagic { get; }

        /// <summary>数字キーで使う使用枠。</summary>
        public IReadOnlyList<Sprite> Quick { get; }

        /// <summary>しまっておくカバン。</summary>
        public IReadOnlyList<Sprite> Bag { get; }

        /// <summary>選んでいる枠の番号。並びは装備5つ、技、魔法の枠、使用枠、カバンの順である。</summary>
        public int Cursor { get; }

        public Sprite DetailIcon { get; }
        public string DetailName { get; }
        public string DetailDescription { get; }

        /// <summary>決めるキーで何をするか（着ける、外す、使うなど）。空ならキーを出さない。</summary>
        public string ActionLabel { get; }

        public string Message { get; }
        public bool MessageIsWarning { get; }

        /// <summary>空の装備の枠の背景に薄く出す影の絵。頭、胴、足、メイン武器、オフハンドの順。</summary>
        public IReadOnlyList<Sprite> EquipmentPlaceholders { get; }
    }

    /// <summary>
    /// 持ち物の画面。1枚の広い枠の左上に装備を体の形（上に頭、真ん中に武器と胴とオフハンド、下に足）に置き、その右に固有の技と魔法、
    /// 下に使用枠とカバンを並べる。
    /// 右に選んだ物の説明を置く。魔法とカバンの枠の数は決まっておらず、渡された数だけ並べる。
    /// 枠を選ぶ動きは画面の位置で決めるので、枠の数が変わっても上下左右で隣の枠へ移れる。
    /// 開いている間もゲームは止めない。見た目の決まりは`docs/fantasy-royale/product-specs/art/ui-design.md`の
    /// 「所持品の見せ方」にある。寸法は1920×1080の画面での値である。
    /// </summary>
    public sealed class InventoryScreen
    {
        public const int EquipmentCount = 5;
        public const string ConfirmKey = "U";
        public const string CloseKey = "Tab";

        private static readonly Vector2 PanelSize = new Vector2(1240f, 790f);
        private const float PanelOffsetY = 60f;
        private const float Margin = 28f;
        private const float CellSize = 88f;
        // 装備は体の形に並べる。頭を上、武器と胴とオフハンドを真ん中の段、足を下に置く。
        private const float EquipmentSize = 80f;
        private const float EquipmentStep = 92f;
        // メイン武器の枠は縦に長くし、ほかの装備の枠と見分ける。
        private const float MainHandHeight = 124f;
        private const float MagicSize = 72f;
        private const float CellStep = 104f;
        private const float MagicStep = 80f;
        private const int MagicPerRow = 3;
        private const int BagPerRow = 6;
        private const float UpperRightLeft = 368f;
        private static readonly Vector2 DetailSize = new Vector2(500f, 670f);
        private const float DetailLeft = 712f;

        private static readonly Color DimColour = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color SlotColour = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color InnerColour = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color GoldText = new Color(0.96f, 0.82f, 0.48f, 1f);
        private static readonly Color MutedText = new Color(0.62f, 0.62f, 0.64f, 1f);
        private static readonly Color DividerColour = new Color(0.55f, 0.42f, 0.18f, 1f);
        private static readonly Color GoodColour = new Color(0.45f, 0.9f, 0.4f, 1f);
        private static readonly Color WarningColour = new Color(0.98f, 0.62f, 0.52f, 1f);
        private static readonly Color KeyLetterColour = new Color(0.07f, 0.07f, 0.08f, 1f);
        // 両手の武器が埋めたオフハンドの枠は、灰色にして使えないと見せる。
        private static readonly Color GreyedSlotColour = new Color(0.22f, 0.22f, 0.24f, 1f);
        // 空の装備の枠の影の絵。灰色にしたオフハンドでは、さらに薄くする。
        // 線形の色空間で描くので、白の不透明度は数値より明るく見える。うっすら見える程度に低くする。
        private static readonly Color PlaceholderColour = new Color(1f, 1f, 1f, 0.035f);
        private static readonly Color GreyedPlaceholderColour = new Color(1f, 1f, 1f, 0.015f);

        private readonly GameHud hud;
        private readonly HudSkin skin;
        private readonly RectTransform root;
        private readonly RectTransform panel;
        private readonly List<Cell> cells = new List<Cell>();
        private readonly RectTransform magicRow;
        private readonly RectTransform quickRow;
        private readonly RectTransform bagRow;
        private readonly Image cursor;
        private readonly Image cursorRound;
        private readonly Image detailIcon;
        private readonly Text detailName;
        private readonly Text detailDescription;
        private readonly RectTransform actionKey;
        private readonly Text actionLabel;
        private readonly Text message;
        private int magicCount = -1;
        private int quickCount = -1;
        private int bagCount = -1;

        internal InventoryScreen(GameHud hud, RectTransform parent, HudSkin skin)
        {
            this.hud = hud;
            this.skin = skin;
            root = HudLayout.NewStretch("Inventory", parent);
            // 左上のハートとコインは持ち物の画面より手前に描き、暗くしない。後ろのゲームは動き続けるので薄く暗くする。
            root.SetAsFirstSibling();
            var dim = HudLayout.NewImage("Dim", root, null, Vector2.zero, HudLayout.Centre);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;
            dim.color = DimColour;

            panel = HudLayout.NewPanel("Panel", root, skin.Panel, PanelSize, HudLayout.Centre).rectTransform;
            panel.anchoredPosition = new Vector2(0f, PanelOffsetY);
            NewText("Title", panel, new Vector2(300f, 32f), new Vector2(Margin, -Margin), true, TextAnchor.UpperLeft, GoldText).text = "持ち物";
            var close = NewKey("Close", panel, CloseKey, new Vector2(64f, 36f), HudLayout.TopRight, 2);
            close.anchoredPosition = new Vector2(-Margin, -Margin + 4f);
            var closeLabel = NewText("CloseLabel", panel, new Vector2(120f, 36f), new Vector2(-Margin - 76f, -Margin + 4f), false, TextAnchor.MiddleRight, Color.white);
            closeLabel.text = "閉じる";

            NewText("EquipmentTitle", panel, new Vector2(300f, 28f), new Vector2(Margin, -84f), true, TextAnchor.UpperLeft, GoldText).text = "装備";
            // 並びは頭、胴、足、メイン武器、オフハンドの順で、置き場所は体の形にする。
            // メイン武器は右手として右、オフハンドは左に置く（2026-11-13、所有者）。メイン武器の枠は縦に長い。
            var left = Margin + 8f;
            var top = -120f;
            var mainHandTop = top - EquipmentStep + (MainHandHeight - EquipmentSize) * 0.5f;
            var equipmentPositions = new[]
            {
                new Vector2(left + EquipmentStep, top),
                new Vector2(left + EquipmentStep, top - EquipmentStep),
                new Vector2(left + EquipmentStep, top - EquipmentStep * 2f),
                new Vector2(left + EquipmentStep * 2f, mainHandTop),
                new Vector2(left, top - EquipmentStep)
            };
            for (var index = 0; index < EquipmentCount; index++)
            {
                var size = index == 3 ? new Vector2(EquipmentSize, MainHandHeight) : Vector2.one * EquipmentSize;
                cells.Add(NewCell(InventoryCellKind.Equipment, index, panel, skin.Panel, true, size, equipmentPositions[index], null));
            }


            // 固有の技と魔法は、装備の右に置く。魔法は3つで折り返す。
            NewText("SkillTitle", panel, new Vector2(340f, 28f), new Vector2(UpperRightLeft - 8f, -84f), true, TextAnchor.UpperLeft, GoldText).text = "固有の技と魔法";
            cells.Add(NewCell(InventoryCellKind.Skill, 0, panel, skin.Panel, true, CellSize, new Vector2(UpperRightLeft, -120f), null));
            magicRow = HudLayout.NewRect("Magics", panel, Vector2.zero, HudLayout.TopLeft);
            magicRow.anchoredPosition = new Vector2(UpperRightLeft + CellSize + 20f, -120f);

            NewText("QuickTitle", panel, new Vector2(400f, 28f), new Vector2(Margin, -404f), true, TextAnchor.UpperLeft, GoldText).text = "使用枠（数字キーで使う）";
            quickRow = HudLayout.NewRect("Quick", panel, Vector2.zero, HudLayout.TopLeft);
            quickRow.anchoredPosition = new Vector2(Margin + 8f, -440f);

            NewText("BagTitle", panel, new Vector2(300f, 28f), new Vector2(Margin, -548f), true, TextAnchor.UpperLeft, GoldText).text = "カバン";
            bagRow = HudLayout.NewRect("Bag", panel, Vector2.zero, HudLayout.TopLeft);
            bagRow.anchoredPosition = new Vector2(Margin + 8f, -584f);

            cursor = HudLayout.NewPanel("Cursor", panel, skin.Select, Vector2.one * (CellSize + 10f), HudLayout.TopLeft);
            cursorRound = HudLayout.NewImage("CursorRound", panel, skin.SelectRound, Vector2.one * (MagicSize + 10f), HudLayout.TopLeft);

            var detail = HudLayout.NewPanel("Detail", panel, skin.Panel, DetailSize, HudLayout.TopLeft);
            detail.color = InnerColour;
            var detailRect = detail.rectTransform;
            detailRect.anchoredPosition = new Vector2(DetailLeft, -84f);
            var iconFrame = HudLayout.NewPanel("IconFrame", detailRect, skin.Panel, new Vector2(128f, 128f), HudLayout.TopLeft);
            iconFrame.color = SlotColour;
            iconFrame.rectTransform.anchoredPosition = new Vector2(Margin, -Margin);
            detailIcon = HudLayout.NewImage("Icon", iconFrame.rectTransform, null, new Vector2(96f, 96f), HudLayout.Centre);
            detailIcon.preserveAspect = true;
            detailName = NewText("Name", detailRect, new Vector2(300f, 64f), new Vector2(184f, -Margin), true, TextAnchor.UpperLeft, Color.white);
            detailDescription = NewText("Description", detailRect, new Vector2(DetailSize.x - 2f * Margin, 280f), new Vector2(Margin, -180f), false, TextAnchor.UpperLeft, Color.white);
            var line = HudLayout.NewImage("Divider", detailRect, null, new Vector2(DetailSize.x - 2f * Margin, 2f), HudLayout.TopLeft);
            line.rectTransform.anchoredPosition = new Vector2(Margin, -550f);
            line.color = DividerColour;
            actionKey = NewKey("ActionKey", detailRect, ConfirmKey, new Vector2(44f, 44f), HudLayout.TopLeft, 2);
            actionKey.anchoredPosition = new Vector2(Margin, -570f);
            actionLabel = NewText("ActionLabel", detailRect, new Vector2(300f, 44f), new Vector2(Margin + 60f, -570f), false, TextAnchor.MiddleLeft, Color.white);
            message = NewText("Message", detailRect, new Vector2(DetailSize.x - 2f * Margin, 32f), new Vector2(Margin, -626f), false, TextAnchor.UpperLeft, WarningColour);

            root.gameObject.SetActive(false);
        }

        public bool IsVisible => root.gameObject.activeSelf;

        /// <summary>今並んでいる枠の数。装備5つ、技、魔法の枠、カバンの合計である。</summary>
        public int CellCount => cells.Count;

        public int Cursor { get; private set; }
        public string DetailNameText => detailName.text;
        public string MessageText => message.text;
        public string ActionLabelText => actionKey.gameObject.activeSelf ? actionLabel.text : string.Empty;

        /// <summary>その枠の背景に影の絵を出しているか。装備の枠が空のときに出る。</summary>
        public bool IsPlaceholderVisible(int index) => cells[index].Placeholder != null && cells[index].Placeholder.enabled;
        /// <summary>両手の武器が埋めて、オフハンドの枠を灰色にしているか。</summary>
        public bool IsOffHandGreyed { get; private set; }

        public InventoryCellKind GetCellKind(int index) => cells[index].Kind;

        /// <summary>その種類の何番目の枠か。装備は頭、胴、足、メイン武器、オフハンドの順。</summary>
        public int GetCellSlot(int index) => cells[index].Slot;

        public Sprite GetCellIcon(int index) => cells[index].Icon.enabled ? cells[index].Icon.sprite : null;

        /// <summary>種類と番号から、枠の並びの番号を返す。なければ-1。</summary>
        public int FindCell(InventoryCellKind kind, int slot)
        {
            for (var index = 0; index < cells.Count; index++)
            {
                if (cells[index].Kind == kind && cells[index].Slot == slot)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>渡された値で画面を出す。開いている間に値が変わったら、もう一度呼ぶ。</summary>
        public void Show(InventoryScreenModel model)
        {
            ResizeRow(InventoryCellKind.Magic, model.Magics.Count, ref magicCount, magicRow, skin.SlotRound, false, MagicSize);
            ResizeRow(InventoryCellKind.Quick, model.Quick.Count, ref quickCount, quickRow, skin.Panel, true, CellSize);
            ResizeRow(InventoryCellKind.Bag, model.Bag.Count, ref bagCount, bagRow, skin.Panel, true, CellSize);

            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                switch (cell.Kind)
                {
                    case InventoryCellKind.Equipment:
                        // 両手の武器が埋めたオフハンドの枠は、絵を出さずに灰色にする。
                        var greyed = cell.Slot == EquipmentCount - 1 && model.TwoHanded;
                        var sprite = greyed || cell.Slot >= model.Equipment.Count ? null : model.Equipment[cell.Slot];
                        SetIcon(cell.Icon, sprite);
                        cell.Frame.color = greyed ? GreyedSlotColour : SlotColour;

                        // 空の枠には、何を着ける枠かを影の絵で薄く出し、着けたら消す。
                        var placeholder = cell.Slot < model.EquipmentPlaceholders.Count ? model.EquipmentPlaceholders[cell.Slot] : null;
                        cell.Placeholder.sprite = placeholder;
                        cell.Placeholder.enabled = placeholder != null && sprite == null;
                        cell.Placeholder.color = greyed ? GreyedPlaceholderColour : PlaceholderColour;
                        break;
                    case InventoryCellKind.Skill:
                        SetIcon(cell.Icon, model.Skill);
                        break;
                    case InventoryCellKind.Magic:
                        SetIcon(cell.Icon, model.Magics[cell.Slot]);
                        cell.SetMark(cell.Slot == model.SelectedMagic ? "E" : null);
                        break;
                    case InventoryCellKind.Quick:
                        SetIcon(cell.Icon, model.Quick[cell.Slot]);
                        break;
                    default:
                        SetIcon(cell.Icon, model.Bag[cell.Slot]);
                        break;
                }
            }

            IsOffHandGreyed = model.TwoHanded;
            Cursor = cells.Count == 0 ? -1 : Mathf.Clamp(model.Cursor, 0, cells.Count - 1);
            MoveCursor();

            SetIcon(detailIcon, model.DetailIcon);
            detailName.text = model.DetailName;
            detailDescription.text = model.DetailDescription;
            actionKey.gameObject.SetActive(!string.IsNullOrEmpty(model.ActionLabel));
            actionLabel.text = model.ActionLabel;
            message.text = model.Message;
            message.color = model.MessageIsWarning ? WarningColour : GoodColour;
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// fromの枠から、画面の上でdirectionの向きにある一番近い枠を返す。なければfromのままである。
        /// 向きから外れた枠ほど遠いと数えるので、行の数が違っても、真上や真下の近い枠へ移る。
        /// </summary>
        public int Navigate(int from, Vector2 direction)
        {
            if (from < 0 || from >= cells.Count || direction == Vector2.zero)
            {
                return from;
            }

            var origin = Centre(cells[from]);
            var best = from;
            var bestScore = float.MaxValue;
            for (var index = 0; index < cells.Count; index++)
            {
                var offset = Centre(cells[index]) - origin;
                var along = Vector2.Dot(offset, direction);
                if (index == from || along <= 1f)
                {
                    continue;
                }

                var across = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                var score = along + across * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = index;
                }
            }

            return best;
        }

        /// <summary>画面の位置（pixel）が当たる枠の番号を返す。なければ-1。</summary>
        public int HitTest(Vector2 screenPosition)
        {
            if (!IsVisible)
            {
                return -1;
            }

            for (var index = 0; index < cells.Count; index++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[index].Root, screenPosition, hud.EventCamera))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>枠の中心の画面の位置（pixel）。Testがマウスの位置を作るのに使う。</summary>
        public Vector2 GetCellScreenCentre(int index)
        {
            var rect = cells[index].Root;
            return RectTransformUtility.WorldToScreenPoint(hud.EventCamera, rect.TransformPoint(rect.rect.center));
        }

        private static Vector2 Centre(Cell cell)
        {
            // 画面の位置でなく、枠の中の位置で比べる。どの枠もpanelの子孫なので、panelの中の位置に揃える。
            return cell.Root.parent is RectTransform parent && parent.name != "Panel"
                ? parent.anchoredPosition + cell.Root.anchoredPosition + new Vector2(cell.Root.sizeDelta.x * 0.5f, -cell.Root.sizeDelta.y * 0.5f)
                : cell.Root.anchoredPosition + new Vector2(cell.Root.sizeDelta.x * 0.5f, -cell.Root.sizeDelta.y * 0.5f);
        }

        private void MoveCursor()
        {
            var hasCursor = Cursor >= 0;
            var round = hasCursor && cells[Cursor].Kind == InventoryCellKind.Magic;
            cursor.enabled = hasCursor && !round;
            cursorRound.enabled = round;
            if (!hasCursor)
            {
                return;
            }

            var cell = cells[Cursor];
            var centre = Centre(cell);
            var frame = round ? cursorRound : cursor;
            if (!round)
            {
                // メイン武器の縦に長い枠にも合うよう、囲みを枠の大きさに合わせる。
                cursor.rectTransform.sizeDelta = cell.Root.sizeDelta + Vector2.one * 10f;
            }

            frame.rectTransform.anchoredPosition = centre + new Vector2(-frame.rectTransform.sizeDelta.x * 0.5f, frame.rectTransform.sizeDelta.y * 0.5f);
            frame.transform.SetAsLastSibling();
        }

        // 魔法とカバンの枠の数を合わせる。枠は種類ごとにまとめて並びの最後に置き直し、並びを装備、技、魔法、カバンの順に保つ。
        private void ResizeRow(InventoryCellKind kind, int count, ref int current, RectTransform row, Sprite frame, bool sliced, float size)
        {
            if (count == current)
            {
                return;
            }

            current = count;
            for (var index = cells.Count - 1; index >= 0; index--)
            {
                if (cells[index].Kind == kind)
                {
                    Object.Destroy(cells[index].Root.gameObject);
                    cells.RemoveAt(index);
                }
            }

            var insertAt = kind == InventoryCellKind.Bag ? cells.Count : FindInsertPoint(kind);
            for (var slot = 0; slot < count; slot++)
            {
                // 魔法は3つ、使用枠とカバンは6つで折り返す。
                var perRow = kind == InventoryCellKind.Magic ? MagicPerRow : BagPerRow;
                var step = kind == InventoryCellKind.Magic ? MagicStep : CellStep;
                var lift = kind == InventoryCellKind.Magic ? (CellSize - size) * 0.5f : 0f;
                var position = new Vector2(slot % perRow * step, -lift - slot / perRow * step);
                var cell = NewCell(kind, slot, row, frame, sliced, size, position, null);
                if (kind == InventoryCellKind.Quick)
                {
                    cell.SetNumber((slot + 1).ToString());
                }

                cells.Insert(insertAt + slot, cell);
            }
        }

        // 並びを装備、技、魔法、使用枠、カバンの順に保つため、その種類より前の種類の最後の後ろを返す。
        private int FindInsertPoint(InventoryCellKind kind)
        {
            var insertAt = 0;
            for (var index = 0; index < cells.Count; index++)
            {
                if (cells[index].Kind < kind)
                {
                    insertAt = index + 1;
                }
            }

            return insertAt;
        }

        private Cell NewCell(InventoryCellKind kind, int slot, RectTransform parent, Sprite frame, bool sliced, float size, Vector2 position, string label)
        {
            return NewCell(kind, slot, parent, frame, sliced, Vector2.one * size, position, label);
        }

        private Cell NewCell(InventoryCellKind kind, int slot, RectTransform parent, Sprite frame, bool sliced, Vector2 cellSize, Vector2 position, string label)
        {
            var size = Mathf.Min(cellSize.x, cellSize.y);
            var image = sliced
                ? HudLayout.NewPanel($"{kind}{slot}", parent, frame, cellSize, HudLayout.TopLeft)
                : HudLayout.NewImage($"{kind}{slot}", parent, frame, cellSize, HudLayout.TopLeft);
            image.color = SlotColour;
            image.rectTransform.anchoredPosition = position;
            Image placeholder = null;
            if (kind == InventoryCellKind.Equipment)
            {
                // 影の絵は絵より先に作り、後ろに置く。
                placeholder = HudLayout.NewImage("Placeholder", image.rectTransform, null, Vector2.one * size * 0.74f, HudLayout.Centre);
                placeholder.preserveAspect = true;
                placeholder.enabled = false;
            }

            var icon = HudLayout.NewImage("Icon", image.rectTransform, null, Vector2.one * size * 0.74f, HudLayout.Centre);
            icon.preserveAspect = true;
            icon.enabled = false;
            if (!string.IsNullOrEmpty(label))
            {
                var text = NewText("Label", image.rectTransform, new Vector2(size, 24f), new Vector2(0f, -size - 4f), false, TextAnchor.UpperLeft, MutedText);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.text = label;
            }

            return new Cell(this, kind, slot, image, icon) { Placeholder = placeholder };
        }

        private RectTransform NewKey(string name, RectTransform parent, string letter, Vector2 size, Vector2 anchor, int multiple)
        {
            var cap = HudLayout.NewImage(name, parent, skin.KeyCap, size, anchor);
            cap.type = Image.Type.Sliced;
            var text = HudLayout.NewText("Letter", cap.rectTransform, skin.NumberFont, new Vector2(size.x, size.y * 0.86f), HudLayout.Centre, TextAnchor.MiddleCenter);
            text.color = KeyLetterColour;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = letter;
            hud.RegisterPixelText(text, multiple);
            return cap.rectTransform;
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

        private static void SetIcon(Image icon, Sprite sprite)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        /// <summary>画面の枠1つ。カバンには数字キーの番号を、魔法には唱える物の印を添える。</summary>
        private sealed class Cell
        {
            private readonly InventoryScreen screen;
            private RectTransform mark;

            public Cell(InventoryScreen screen, InventoryCellKind kind, int slot, Image frame, Image icon)
            {
                this.screen = screen;
                Kind = kind;
                Slot = slot;
                Frame = frame;
                Root = frame.rectTransform;
                Icon = icon;
            }

            public Image Frame { get; }

            /// <summary>空の装備の枠の背景に薄く出す影の絵。装備の枠だけが持つ。</summary>
            public Image Placeholder { get; set; }

            public InventoryCellKind Kind { get; }
            public int Slot { get; }
            public RectTransform Root { get; }
            public Image Icon { get; }

            public void SetNumber(string number)
            {
                var key = screen.NewKey("Number", Root, number, new Vector2(28f, 28f), HudLayout.TopLeft, 2);
                key.anchoredPosition = new Vector2(-8f, 8f);
            }

            /// <summary>唱える魔法にEのキーを添える。nullで外す。</summary>
            public void SetMark(string letter)
            {
                if (letter == null)
                {
                    if (mark != null)
                    {
                        mark.gameObject.SetActive(false);
                    }

                    return;
                }

                if (mark == null)
                {
                    mark = screen.NewKey("Mark", Root, letter, new Vector2(28f, 28f), HudLayout.TopLeft, 2);
                    mark.anchoredPosition = new Vector2(-8f, 8f);
                }

                mark.gameObject.SetActive(true);
            }
        }
    }
}
