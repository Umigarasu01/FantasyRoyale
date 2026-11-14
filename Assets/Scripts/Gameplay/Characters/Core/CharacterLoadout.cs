namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 現在の装備が持つ通常攻撃、特殊Action（固有の技）、選んでいる魔法を保持する純C#状態。
    /// 装備を替えるとActionも替わる仕様の正本で、空のSlotはnullで表す。
    /// </summary>
    public sealed class CharacterLoadout
    {
        private CharacterActionSpec primary;
        private CharacterActionSpec special;
        private CharacterActionSpec magic;

        public CharacterLoadout(CharacterActionSpec primary, CharacterActionSpec special)
        {
            Replace(primary, special);
        }

        /// <summary>
        /// 武器が決める二つのSlotをまとめて差し替える。魔法は武器にはめた物で決まるので変えない。
        /// </summary>
        public void Replace(CharacterActionSpec newPrimary, CharacterActionSpec newSpecial)
        {
            primary = newPrimary;
            special = newSpecial;
        }

        /// <summary>
        /// 唱える魔法のActionを差し替える。選んでいる魔法がなければnullを渡す。
        /// </summary>
        public void SetMagic(CharacterActionSpec newMagic)
        {
            magic = newMagic;
        }

        /// <summary>
        /// 指定Slotの現在のActionを返す。
        /// </summary>
        public CharacterActionSpec Get(CharacterActionSlot slot)
        {
            switch (slot)
            {
                case CharacterActionSlot.Primary:
                    return primary;
                case CharacterActionSlot.Special:
                    return special;
                default:
                    return magic;
            }
        }
    }
}
