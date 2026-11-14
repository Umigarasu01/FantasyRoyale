using UnityEngine;
using UnityEngine.Serialization;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>
    /// HUDの絵をまとめる定義Asset。絵は`Tools/UiAuthoring/build_hud_sprites.py`が作る。
    /// 枠は9分割で伸ばすので、取り込み時に境界を付けておく。
    /// </summary>
    [CreateAssetMenu(menuName = "FantasyRoyale/HUD Skin", fileName = "GameHudSkin")]
    public sealed class HudSkin : ScriptableObject
    {
        [SerializeField] private Sprite panel;
        [SerializeField] private Sprite heartFull;
        [SerializeField] private Sprite heartHalf;
        [SerializeField] private Sprite heartEmpty;
        [SerializeField] private Sprite coin;
        [SerializeField, FormerlySerializedAs("keyE")] private Sprite interactKey;
        [SerializeField] private Sprite pointer;
        [SerializeField] private Font numberFont;
        [SerializeField, Min(1)] private int numberFontCell = 12;
        [SerializeField] private Font textFont;
        [SerializeField] private Sprite select;
        [SerializeField] private Sprite arrow;
        [SerializeField] private Sprite keyCap;
        [SerializeField] private Sprite cursor;
        [SerializeField] private Sprite slotRound;
        [SerializeField] private Sprite selectRound;
        [SerializeField] private Sprite manaFull;
        [SerializeField] private Sprite manaEmpty;

        /// <summary>黒漆に細い金の縁の枠。9分割で伸ばす。</summary>
        public Sprite Panel => panel;
        public Sprite HeartFull => heartFull;
        public Sprite HeartHalf => heartHalf;
        public Sprite HeartEmpty => heartEmpty;
        public Sprite Coin => coin;
        /// <summary>調べるキーの文字を描き込んだキー。2026-11-13からFである。</summary>
        public Sprite InteractKey => interactKey;
        /// <summary>吹き出しの下の三角。上の帯が枠の下の金の縁を隠してつながる。</summary>
        public Sprite Pointer => pointer;
        /// <summary>数字のピクセルフォント。ないときはUnityに組み込みのフォントを太字で使う。</summary>
        public Font NumberFont => numberFont;
        /// <summary>数字のフォントの元の格子の大きさ（pixel）。画面上ではこの整数倍で描く。</summary>
        public int NumberFontCell => numberFontCell;
        /// <summary>名前や説明の文のピクセルフォント。格子は数字のフォントと同じとする。ないときは数字のフォントを使う。</summary>
        public Font TextFont => textFont != null ? textFont : numberFont;
        /// <summary>選んだ行を囲む金の枠。枠と同じ境界で9分割して伸ばす。</summary>
        public Sprite Select => select;
        /// <summary>選んだ行から説明を指す金の矢印。</summary>
        public Sprite Arrow => arrow;
        /// <summary>文字のない無地のキー。文字はピクセルフォントで乗せる。</summary>
        public Sprite KeyCap => keyCap;
        /// <summary>マウスのカーソル。金の矢印で、先端で指す。</summary>
        public Sprite Cursor => cursor;
        /// <summary>魔法の枠。黒漆に細い金の縁の丸で、四角の武器と技の枠と形で分ける。</summary>
        public Sprite SlotRound => slotRound;
        /// <summary>選んでいる魔法を囲む金の丸。</summary>
        public Sprite SelectRound => selectRound;
        /// <summary>MPの玉。満ちた青い玉と空の暗い玉。</summary>
        public Sprite ManaFull => manaFull;
        public Sprite ManaEmpty => manaEmpty;

        public Sprite GetHeart(HeartFill fill)
        {
            return fill == HeartFill.Full ? heartFull : fill == HeartFill.Half ? heartHalf : heartEmpty;
        }

        /// <summary>Editor BuilderとTestから絵を設定する。</summary>
        public void Configure(
            Sprite newPanel,
            Sprite newHeartFull,
            Sprite newHeartHalf,
            Sprite newHeartEmpty,
            Sprite newCoin,
            Sprite newInteractKey,
            Sprite newPointer,
            Font newNumberFont,
            int newNumberFontCell)
        {
            numberFont = newNumberFont;
            numberFontCell = Mathf.Max(1, newNumberFontCell);
            panel = newPanel;
            heartFull = newHeartFull;
            heartHalf = newHeartHalf;
            heartEmpty = newHeartEmpty;
            coin = newCoin;
            interactKey = newInteractKey;
            pointer = newPointer;
        }

        /// <summary>Editor BuilderとTestから、下の装備の枠とMPの絵を設定する。</summary>
        public void ConfigureLoadout(Sprite newSlotRound, Sprite newSelectRound, Sprite newManaFull, Sprite newManaEmpty)
        {
            slotRound = newSlotRound;
            selectRound = newSelectRound;
            manaFull = newManaFull;
            manaEmpty = newManaEmpty;
        }

        /// <summary>Editor BuilderとTestから店の画面の絵とフォントを設定する。</summary>
        public void ConfigureShop(Font newTextFont, Sprite newSelect, Sprite newArrow, Sprite newKeyCap, Sprite newCursor)
        {
            cursor = newCursor;
            textFont = newTextFont;
            select = newSelect;
            arrow = newArrow;
            keyCap = newKeyCap;
        }
    }
}
