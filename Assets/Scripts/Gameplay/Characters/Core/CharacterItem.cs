using System;
using System.Collections.Generic;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 持ち物の種類。着けられる物は、埋める枠で武器と防具を分ける。
    /// </summary>
    public enum CharacterItemKind
    {
        Consumable = 0,
        Equipment = 1,
        Magic = 2,
        Other = 3
    }

    /// <summary>
    /// 装備を着ける枠。一体の装備は複数の枠を1つで埋める。
    /// </summary>
    public enum CharacterEquipSlot
    {
        MainHand = 0,
        OffHand = 1,
        Head = 2,
        Torso = 3,
        Legs = 4
    }

    /// <summary>
    /// 手に入れた持ち物1つ。定義でなく1つごとの状態なので、同じ剣が2本あれば、入っている魔法は別々になる。
    /// 武器は定義が決めた数の魔法の枠を持ち、0の武器もある。
    /// </summary>
    public sealed class CharacterItem
    {
        private static readonly CharacterEquipSlot[] NoSlots = new CharacterEquipSlot[0];

        private readonly CharacterEquipSlot[] slots;
        private readonly CharacterItem[] magicSlots;
        private int selectedMagicIndex;

        /// <summary>
        /// 定義のid、種類、着けたときに埋める枠、魔法の枠の数から持ち物を作る。
        /// 着けられるのは埋める枠を持つ装備だけで、魔法の枠はメイン武器の枠を埋める物だけが持てる。
        /// </summary>
        public CharacterItem(
            string id,
            CharacterItemKind kind,
            IReadOnlyList<CharacterEquipSlot> occupiedSlots = null,
            int magicSlotCount = 0)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("idが空です。", nameof(id));
            }

            if (magicSlotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magicSlotCount), "魔法の枠の数は0以上です。");
            }

            slots = occupiedSlots == null ? NoSlots : CopyDistinct(occupiedSlots);
            if (kind == CharacterItemKind.Equipment && slots.Length == 0)
            {
                throw new ArgumentException("装備は埋める枠が要ります。", nameof(occupiedSlots));
            }

            if (kind != CharacterItemKind.Equipment && slots.Length > 0)
            {
                throw new ArgumentException("装備でない物は枠を埋めません。", nameof(occupiedSlots));
            }

            if (magicSlotCount > 0 && Array.IndexOf(slots, CharacterEquipSlot.MainHand) < 0)
            {
                throw new ArgumentException("魔法の枠はメイン武器だけが持てます。", nameof(magicSlotCount));
            }

            Id = id;
            Kind = kind;
            magicSlots = new CharacterItem[magicSlotCount];
        }

        public string Id { get; }
        public CharacterItemKind Kind { get; }

        /// <summary>
        /// 着けたときに埋める枠。両手の武器はメイン武器とオフハンド、ローブは胴と足を埋める。
        /// </summary>
        public IReadOnlyList<CharacterEquipSlot> Slots => slots;

        public bool IsWeapon => Occupies(CharacterEquipSlot.MainHand);
        public int MagicSlotCount => magicSlots.Length;

        /// <summary>
        /// 選んでいる魔法の枠の番号。枠のない武器では-1である。武器が覚えるので、持ち替えて戻っても変わらない。
        /// </summary>
        public int SelectedMagicIndex => magicSlots.Length == 0 ? -1 : selectedMagicIndex;

        /// <summary>
        /// 選んでいる枠に入っている魔法。空の枠や枠のない武器ではnullである。
        /// </summary>
        public CharacterItem SelectedMagic => magicSlots.Length == 0 ? null : magicSlots[selectedMagicIndex];

        public bool Occupies(CharacterEquipSlot slot)
        {
            return Array.IndexOf(slots, slot) >= 0;
        }

        public CharacterItem GetMagic(int index)
        {
            return magicSlots[index];
        }

        /// <summary>
        /// 魔法を前から順に空いた枠へ入れ、入れた枠の番号を返す。空きがないか魔法でなければ-1を返す。
        /// 選んでいる枠が空だったときは、入れた魔法を選ぶ。
        /// </summary>
        public int TryInsertMagic(CharacterItem magic)
        {
            if (magic == null || magic.Kind != CharacterItemKind.Magic)
            {
                return -1;
            }

            for (var index = 0; index < magicSlots.Length; index++)
            {
                if (magicSlots[index] != null)
                {
                    continue;
                }

                magicSlots[index] = magic;
                if (magicSlots[selectedMagicIndex] == null)
                {
                    selectedMagicIndex = index;
                }

                return index;
            }

            return -1;
        }

        /// <summary>
        /// 番号の空いた魔法の枠へ魔法を入れる。魔法でないか、枠が埋まっていればfalseを返す。
        /// 選んでいる枠が空だったときは、入れた魔法を選ぶ。
        /// </summary>
        public bool TryPutMagic(CharacterItem magic, int index)
        {
            if (magic == null || magic.Kind != CharacterItemKind.Magic || index < 0 || index >= magicSlots.Length || magicSlots[index] != null)
            {
                return false;
            }

            magicSlots[index] = magic;
            if (magicSlots[selectedMagicIndex] == null)
            {
                selectedMagicIndex = index;
            }

            return true;
        }

        /// <summary>
        /// 2つの魔法の枠の中身を入れ替える。唱える魔法は、入れ替えた後も同じ魔法を選ぶ。
        /// </summary>
        public void SwapMagic(int a, int b)
        {
            (magicSlots[a], magicSlots[b]) = (magicSlots[b], magicSlots[a]);
            if (selectedMagicIndex == a)
            {
                selectedMagicIndex = b;
            }
            else if (selectedMagicIndex == b)
            {
                selectedMagicIndex = a;
            }
        }

        /// <summary>
        /// 指定の枠の魔法を外して返す。空ならnullを返す。
        /// </summary>
        public CharacterItem RemoveMagic(int index)
        {
            var magic = magicSlots[index];
            magicSlots[index] = null;
            return magic;
        }

        /// <summary>
        /// 次の魔法の入った枠へ切り替え、選んだ枠の番号を返す。空の枠は飛ばす。
        /// 魔法が1つも入っていなければ選びを変えず、今の番号を返す。
        /// </summary>
        public int SelectNextMagic()
        {
            for (var step = 1; step <= magicSlots.Length; step++)
            {
                var index = (selectedMagicIndex + step) % magicSlots.Length;
                if (magicSlots[index] != null)
                {
                    selectedMagicIndex = index;
                    break;
                }
            }

            return SelectedMagicIndex;
        }

        private static CharacterEquipSlot[] CopyDistinct(IReadOnlyList<CharacterEquipSlot> source)
        {
            var result = new List<CharacterEquipSlot>();
            foreach (var slot in source)
            {
                if (!result.Contains(slot))
                {
                    result.Add(slot);
                }
            }

            return result.ToArray();
        }
    }
}
