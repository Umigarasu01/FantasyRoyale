namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 現在の装備が持つ通常攻撃と特殊Actionを保持する純C#状態。
    /// 装備を替えるとActionも替わる仕様の正本で、空のSlotはnullで表す。
    /// </summary>
    public sealed class CharacterLoadout
    {
        private CharacterActionSpec primary;
        private CharacterActionSpec special;

        public CharacterLoadout(CharacterActionSpec primary, CharacterActionSpec special)
        {
            Replace(primary, special);
        }

        /// <summary>
        /// 二つのSlotをまとめて差し替える。
        /// </summary>
        public void Replace(CharacterActionSpec newPrimary, CharacterActionSpec newSpecial)
        {
            primary = newPrimary;
            special = newSpecial;
        }

        /// <summary>
        /// 指定Slotの現在のActionを返す。
        /// </summary>
        public CharacterActionSpec Get(CharacterActionSlot slot)
        {
            return slot == CharacterActionSlot.Primary ? primary : special;
        }
    }
}
