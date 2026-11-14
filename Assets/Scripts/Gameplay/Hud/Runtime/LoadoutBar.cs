using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>
    /// 下の枠に出す値。ゲームの状態は持たず、呼ぶ側が所持品から作って渡す。
    /// 魔法とカバンの枠の数は決まっておらず、渡された数だけ並べる。空の枠はnullの絵で表す。
    /// </summary>
    public sealed class LoadoutBarModel
    {
        public LoadoutBarModel(
            Sprite skill,
            IReadOnlyList<Sprite> magics,
            int selectedMagic,
            IReadOnlyList<Sprite> items,
            int selectedItem)
        {
            Skill = skill;
            Magics = magics ?? new Sprite[0];
            SelectedMagic = selectedMagic;
            Items = items ?? new Sprite[0];
            SelectedItem = selectedItem;
        }

        public Sprite Skill { get; }

        /// <summary>メイン武器の魔法の枠。武器の持つ数だけ並び、0もある。</summary>
        public IReadOnlyList<Sprite> Magics { get; }

        /// <summary>選んでいる魔法の枠の番号。枠がなければ-1。</summary>
        public int SelectedMagic { get; }

        /// <summary>使用枠。数字キーで使う物が並ぶ。</summary>
        public IReadOnlyList<Sprite> Items { get; }

        public int SelectedItem { get; }
    }

    /// <summary>
    /// 画面の下の真ん中の枠。下の段に使用枠のアイテムを全て大きく並べ、その上の段に固有の技（Q）と魔法（E）を置く
    /// （2026-11-13、所有者）。アイテムは数字キーで使うので、段には番号だけを添える。
    /// 魔法は選んでいる物をEの丸い枠に出し、武器の魔法の枠を全てその右に小さく並べる。切り替えはRである。
    /// 使用枠の5つ目までと技と魔法の枠の位置はいつも同じで、使用枠が増えた分と魔法の小さな列は右へ伸びる。
    /// 並べ方は`docs/fantasy-royale/product-specs/art/ui-design.md`の「所持品の見せ方」にある。
    /// 寸法は1920×1080の画面での値である。
    /// </summary>
    public sealed class LoadoutBar
    {
        // 2026-11-13に所有者が決めたキー。技はQ、魔法はE、魔法の切り替えはR、アイテムは数字キーで使う。
        public const string SkillKey = "Q";
        public const string MagicKey = "E";
        public const string NextMagicKey = "R";

        private const float BottomMargin = 20f;

        // 下の段のアイテム。初めの5つが画面の真ん中に揃う。
        private const float ItemSize = 72f;
        private const float ItemStep = 82f;
        private const int CentredItems = 5;

        // 上の段の技と魔法。
        private const float RowGap = 18f;
        private const float SkillSize = 80f;
        private const float MagicSize = 72f;
        private const float SkillX = -104f;
        private const float MagicX = -10f;
        private const float SmallSize = 30f;
        private const float SmallStep = 36f;
        private const float SmallGap = 24f;

        private const float KeySize = 28f;
        private const float SmallKeySize = 22f;
        private const float IconScale = 0.72f;

        private static readonly Color SlotColour = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color DimColour = new Color(0f, 0f, 0f, 0.62f);
        // 技の待ち時間の覆いは黒漆の地の上でも見えるよう濃くし、待っている間は絵も灰色にする。
        private static readonly Color CooldownColour = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color CoolingIconColour = new Color(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color KeyLetterColour = new Color(0.07f, 0.07f, 0.08f, 1f);

        private readonly GameHud hud;
        private readonly HudSkin skin;
        private readonly RectTransform root;
        private readonly Image skillIcon;
        private readonly Image skillDim;
        private readonly RectTransform magicSlot;
        private readonly Image magicIcon;
        private readonly Image magicDim;
        private readonly RectTransform magicStrip;
        private readonly RectTransform itemRow;
        private readonly List<SmallSlot> magicSlots = new List<SmallSlot>();
        private readonly List<ItemSlot> itemSlots = new List<ItemSlot>();

        internal LoadoutBar(GameHud hud, RectTransform parent, HudSkin skin)
        {
            this.hud = hud;
            this.skin = skin;
            root = HudLayout.NewRect("Loadout", parent, new Vector2(ItemSize, ItemSize), new Vector2(0.5f, 0f));
            root.anchoredPosition = new Vector2(0f, BottomMargin);

            itemRow = HudLayout.NewRect("Items", root, Vector2.zero, new Vector2(0.5f, 0f));
            itemRow.anchoredPosition = new Vector2(-(CentredItems - 1) * ItemStep * 0.5f, ItemSize * 0.5f);

            var upperY = ItemSize + RowGap + SkillSize * 0.5f;
            skillIcon = NewBigSlot("Skill", skin.Panel, true, SkillSize, new Vector2(SkillX, upperY), SkillKey, out skillDim);

            // 待ち時間は、時計回りに減っていく暗い覆いで見せる。数字は出さない。
            skillDim.type = Image.Type.Filled;
            skillDim.fillMethod = Image.FillMethod.Radial360;
            skillDim.fillOrigin = (int)Image.Origin360.Top;
            skillDim.fillClockwise = false;
            skillDim.color = CooldownColour;

            magicIcon = NewBigSlot("Magic", skin.SlotRound, false, MagicSize, new Vector2(MagicX, upperY), MagicKey, out magicDim);
            magicSlot = (RectTransform)magicIcon.rectTransform.parent;
            var nextMagic = NewKey("NextMagicKey", magicSlot, NextMagicKey, SmallKeySize, HudLayout.TopRight, 1);
            nextMagic.rectTransform.anchoredPosition = new Vector2(SmallKeySize * 0.3f, SmallKeySize * 0.3f);

            magicStrip = HudLayout.NewRect("MagicStrip", root, Vector2.zero, new Vector2(0.5f, 0f));
            magicStrip.anchoredPosition = new Vector2(MagicX + MagicSize * 0.5f + SmallGap, upperY);
        }

        /// <summary>下の枠を出しているか。店の画面のように、下まで広がる画面を開いている間は隠す。</summary>
        public bool IsVisible => root.gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            if (root.gameObject.activeSelf != visible)
            {
                root.gameObject.SetActive(visible);
            }
        }

        /// <summary>魔法の大きな枠を出しているか。魔法の枠のない武器では隠す。</summary>
        public bool IsMagicVisible => magicSlot.gameObject.activeSelf;

        public int MagicSlotCount { get; private set; }
        public int ItemSlotCount { get; private set; }
        public int SelectedMagic { get; private set; } = -1;
        public int SelectedItem { get; private set; } = -1;
        public Sprite MagicIcon => magicIcon.enabled ? magicIcon.sprite : null;

        /// <summary>ゲームパッドで使う、選んでいるアイテムの絵。キーボードでは数字キーで直接使うので、画面では囲まない。</summary>
        public Sprite ItemIcon => SelectedItem >= 0 ? itemSlots[SelectedItem].Icon : null;

        public bool IsMagicDimmed => magicDim.enabled;

        /// <summary>技の待ち時間の覆いの残り（1で全て暗く、0で覆いなし）。</summary>
        public float SkillCooldownFill => skillDim.enabled ? skillDim.fillAmount : 0f;

        /// <summary>
        /// 画面上の枠の中心（pixel）。0が技、1が魔法、2が使用枠の1つ目。Testが位置の動かないことを確かめるのに使う。
        /// </summary>
        public Vector2 GetBigSlotScreenCentre(int index)
        {
            var rect = index == 0 ? skillIcon.rectTransform : index == 1 ? magicIcon.rectTransform : itemSlots[0].Root;
            return RectTransformUtility.WorldToScreenPoint(hud.EventCamera, rect.position);
        }

        public Sprite GetMagicIcon(int index) => magicSlots[index].Icon;
        public Sprite GetItemIcon(int index) => itemSlots[index].Icon;

        /// <summary>枠の中身と選んでいる物を表示する。枠の数が変われば、段と列を作り直す。</summary>
        public void Show(LoadoutBarModel model)
        {
            SetIcon(skillIcon, model.Skill);
            ResizeMagics(model.Magics.Count);
            ResizeItems(model.Items.Count);
            MagicSlotCount = model.Magics.Count;
            ItemSlotCount = model.Items.Count;
            SelectedMagic = model.Magics.Count == 0 ? -1 : Mathf.Clamp(model.SelectedMagic, 0, model.Magics.Count - 1);
            SelectedItem = model.Items.Count == 0 ? -1 : Mathf.Clamp(model.SelectedItem, 0, model.Items.Count - 1);

            for (var index = 0; index < magicSlots.Count; index++)
            {
                magicSlots[index].Set(model.Magics[index], index == SelectedMagic);
            }

            for (var index = 0; index < itemSlots.Count; index++)
            {
                itemSlots[index].Set(model.Items[index]);
            }

            magicSlot.gameObject.SetActive(model.Magics.Count > 0);
            SetIcon(magicIcon, SelectedMagic >= 0 ? model.Magics[SelectedMagic] : null);

        }

        /// <summary>
        /// 技の待ち時間の残りの割合（1から0）を渡す。覆いは時計回りに減り、0で消える。毎フレーム呼んでよい。
        /// </summary>
        public void SetSkillCooldown(float remainingFraction)
        {
            var fill = Mathf.Clamp01(remainingFraction);
            skillDim.enabled = fill > 0f;
            skillDim.fillAmount = fill;
            skillIcon.color = fill > 0f ? CoolingIconColour : Color.white;
        }

        /// <summary>
        /// MPが足りないなど、今唱えられない魔法の枠を暗くする。毎フレーム呼んでよい。
        /// </summary>
        public void SetMagicReady(bool magicReady)
        {
            if (magicDim.enabled == magicReady)
            {
                magicDim.enabled = !magicReady;
            }

            if (SelectedMagic >= 0 && SelectedMagic < magicSlots.Count)
            {
                magicSlots[SelectedMagic].SetDim(!magicReady);
            }
        }

        private Image NewBigSlot(string name, Sprite frame, bool sliced, float size, Vector2 centre, string key, out Image dim)
        {
            var slot = sliced
                ? HudLayout.NewPanel(name, root, frame, new Vector2(size, size), new Vector2(0.5f, 0f))
                : HudLayout.NewImage(name, root, frame, new Vector2(size, size), new Vector2(0.5f, 0f));
            slot.rectTransform.pivot = HudLayout.Centre;
            slot.rectTransform.anchoredPosition = centre;
            var icon = HudLayout.NewImage("Icon", slot.rectTransform, null, Vector2.one * size * IconScale, HudLayout.Centre);
            icon.preserveAspect = true;
            icon.enabled = false;

            // 暗くする覆いは枠と同じ形にする。
            dim = sliced
                ? HudLayout.NewPanel("Dim", slot.rectTransform, frame, new Vector2(size, size), HudLayout.Centre)
                : HudLayout.NewImage("Dim", slot.rectTransform, frame, new Vector2(size, size), HudLayout.Centre);
            dim.color = DimColour;
            dim.enabled = false;

            var cap = NewKey("Key", slot.rectTransform, key, KeySize, HudLayout.TopLeft, 2);
            cap.rectTransform.anchoredPosition = new Vector2(-KeySize * 0.3f, KeySize * 0.3f);
            return icon;
        }

        private Image NewKey(string name, RectTransform parent, string letter, float size, Vector2 anchor, int fontMultiple)
        {
            var cap = HudLayout.NewImage(name, parent, skin.KeyCap, new Vector2(size, size), anchor);
            var text = HudLayout.NewText("Letter", cap.rectTransform, skin.NumberFont, new Vector2(size, size * 0.86f), HudLayout.Centre, TextAnchor.MiddleCenter);
            text.color = KeyLetterColour;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = letter;
            hud.RegisterPixelText(text, fontMultiple);
            return cap;
        }

        // 魔法の小さな列は、Lの枠の右に左から並ぶ。
        private void ResizeMagics(int count)
        {
            while (magicSlots.Count < count)
            {
                magicSlots.Add(new SmallSlot(magicStrip, skin.SlotRound, skin.SelectRound));
            }

            while (magicSlots.Count > count)
            {
                Object.Destroy(magicSlots[magicSlots.Count - 1].Root.gameObject);
                magicSlots.RemoveAt(magicSlots.Count - 1);
            }

            for (var index = 0; index < magicSlots.Count; index++)
            {
                magicSlots[index].Root.anchoredPosition = new Vector2(SmallSize * 0.5f + SmallStep * index, 0f);
            }
        }

        // アイテムの段は、初めの5つを真ん中に揃え、増えた分は右へ並ぶ。
        private void ResizeItems(int count)
        {
            while (itemSlots.Count < count)
            {
                var slot = new ItemSlot(this, itemRow, itemSlots.Count + 1);
                slot.Root.anchoredPosition = new Vector2(ItemStep * itemSlots.Count, 0f);
                itemSlots.Add(slot);
            }

            while (itemSlots.Count > count)
            {
                Object.Destroy(itemSlots[itemSlots.Count - 1].Root.gameObject);
                itemSlots.RemoveAt(itemSlots.Count - 1);
            }
        }

        private static void SetIcon(Image icon, Sprite sprite)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        /// <summary>アイテムの段の枠1つ。使う数字キーの番号を左上に添える。</summary>
        private sealed class ItemSlot
        {
            private readonly Image icon;

            public ItemSlot(LoadoutBar bar, RectTransform row, int number)
            {
                var size = new Vector2(ItemSize, ItemSize);
                var slot = HudLayout.NewPanel($"Item{number}", row, bar.skin.Panel, size, HudLayout.Centre);
                slot.color = SlotColour;
                Root = slot.rectTransform;
                icon = HudLayout.NewImage("Icon", Root, null, size * IconScale, HudLayout.Centre);
                icon.preserveAspect = true;
                icon.enabled = false;
                var key = bar.NewKey("Number", Root, number.ToString(), KeySize, HudLayout.TopLeft, 2);
                key.rectTransform.anchoredPosition = new Vector2(-KeySize * 0.3f, KeySize * 0.3f);
            }

            public RectTransform Root { get; }
            public Sprite Icon => icon.enabled ? icon.sprite : null;

            public void Set(Sprite sprite)
            {
                SetIcon(icon, sprite);
            }
        }

        /// <summary>魔法の小さな列の枠1つ。選んでいる物は金の太い縁で囲む。</summary>
        private sealed class SmallSlot
        {
            private readonly Image icon;
            private readonly Image select;
            private readonly Image dim;

            public SmallSlot(RectTransform strip, Sprite frame, Sprite selectFrame)
            {
                var size = new Vector2(SmallSize, SmallSize);
                var slot = HudLayout.NewImage("Small", strip, frame, size, HudLayout.Centre);
                slot.color = SlotColour;
                Root = slot.rectTransform;
                icon = HudLayout.NewImage("Icon", Root, null, size * IconScale, HudLayout.Centre);
                icon.preserveAspect = true;
                dim = HudLayout.NewImage("Dim", Root, frame, size, HudLayout.Centre);
                dim.color = DimColour;
                dim.enabled = false;
                select = HudLayout.NewImage("Select", Root, selectFrame, size + Vector2.one * 6f, HudLayout.Centre);
                select.enabled = false;
            }

            public RectTransform Root { get; }
            public Sprite Icon => icon.enabled ? icon.sprite : null;

            public void Set(Sprite sprite, bool selected)
            {
                SetIcon(icon, sprite);
                select.enabled = selected;
                dim.enabled = false;
            }

            public void SetDim(bool dimmed)
            {
                dim.enabled = dimmed;
            }
        }
    }
}
