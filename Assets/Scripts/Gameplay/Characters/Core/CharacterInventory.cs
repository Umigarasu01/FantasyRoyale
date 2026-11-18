using System;
using System.Collections.Generic;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 持ち物を使ったときの結果。
    /// </summary>
    public enum CharacterItemUseOutcome
    {
        None = 0,
        Consumed = 1,
        Equipped = 2,
        MagicInserted = 3,
        EmptySlot = 4,
        NoRoomInBag = 5,
        NoMagicSlot = 6,
        NotUsable = 7,
        MovedToQuickSlot = 8,
        NoRoomInQuickSlots = 9
    }

    /// <summary>
    /// 持ち物を使った結果と、効き目を出すために呼び出し側へ渡す物。
    /// </summary>
    public readonly struct CharacterItemUseResult
    {
        public CharacterItemUseResult(CharacterItemUseOutcome outcome, CharacterItem item)
        {
            Outcome = outcome;
            Item = item;
        }

        public CharacterItemUseOutcome Outcome { get; }

        /// <summary>
        /// 使った物。使う物なら所持品から消えているので、呼び出し側が効き目を出す。
        /// </summary>
        public CharacterItem Item { get; }

        public bool IsApplied =>
            Outcome == CharacterItemUseOutcome.Consumed
            || Outcome == CharacterItemUseOutcome.Equipped
            || Outcome == CharacterItemUseOutcome.MagicInserted
            || Outcome == CharacterItemUseOutcome.MovedToQuickSlot;
    }

    /// <summary>
    /// 着けている装備、使用枠、カバンをまとめて持つ純C#の所持品。
    /// 使用枠は回復薬のような使う物だけを入れ、数字キーですぐ使う。カバンはそれ以外の物と予備の使う物をしまう（2026-11-13、所有者）。
    /// どの置き場も、1つの物が1つの枠を使い、重ねない。
    /// </summary>
    public sealed class CharacterInventory
    {
        private static readonly int SlotCount = Enum.GetValues(typeof(CharacterEquipSlot)).Length;

        private readonly CharacterItem[] equipped = new CharacterItem[SlotCount];
        private readonly CharacterItem[] quick;
        private CharacterItem[] bag;
        private int selectedQuickIndex;

        /// <summary>
        /// 使用枠とカバンの枠の数を決めて、空の所持品を作る。
        /// </summary>
        public CharacterInventory(int bagCapacity, int quickCapacity = 0)
        {
            if (bagCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bagCapacity), "カバンの枠は1つ以上です。");
            }

            if (quickCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quickCapacity), "使用枠は0以上です。");
            }

            bag = new CharacterItem[bagCapacity];
            quick = new CharacterItem[quickCapacity];
        }

        /// <summary>
        /// 着けている物、使用枠、カバンが変わったときに呼ばれる。
        /// </summary>
        public event Action Changed;

        public int BagCapacity => bag.Length;
        public int QuickCapacity => quick.Length;

        /// <summary>ゲームパッドで使う、選んでいる使用枠の番号。</summary>
        public int SelectedQuickIndex => selectedQuickIndex;

        public CharacterItem SelectedQuickItem => quick.Length == 0 ? null : quick[selectedQuickIndex];
        public CharacterItem MainWeapon => equipped[(int)CharacterEquipSlot.MainHand];

        public CharacterItem GetEquipped(CharacterEquipSlot slot)
        {
            return equipped[(int)slot];
        }

        public CharacterItem GetBagItem(int index)
        {
            return bag[index];
        }

        public CharacterItem GetQuickItem(int index)
        {
            return quick[index];
        }

        public int CountFreeBagSlots()
        {
            return CountFree(bag);
        }

        public int CountFreeQuickSlots()
        {
            return CountFree(quick);
        }

        /// <summary>
        /// 持っている物（着けている物、使用枠、カバン）に、この物があるかを返す。
        /// </summary>
        public bool Contains(CharacterItem item)
        {
            return item != null
                && (Array.IndexOf(equipped, item) >= 0 || Array.IndexOf(bag, item) >= 0 || Array.IndexOf(quick, item) >= 0);
        }

        /// <summary>
        /// カバンの枠を増やす。増えた枠は空で、減らすことはしない。
        /// </summary>
        public void ExpandBag(int extraSlots)
        {
            if (extraSlots <= 0)
            {
                return;
            }

            Array.Resize(ref bag, bag.Length + extraSlots);
            Changed?.Invoke();
        }

        /// <summary>
        /// 手に入れた物をしまう。使う物は空いた使用枠へ先に入れ、入らなければカバンへ入れる。ほかの物はカバンへ入れる。
        /// しまえなければfalseを返す。
        /// </summary>
        public bool TryStore(CharacterItem item)
        {
            if (item != null && item.Kind == CharacterItemKind.Consumable && TryAddToQuick(item) >= 0)
            {
                return true;
            }

            return TryAddToBag(item) >= 0;
        }

        /// <summary>
        /// 前から順に空いた使用枠へ入れ、入れた番号を返す。使う物でないか、いっぱいなら-1を返す。
        /// </summary>
        public int TryAddToQuick(CharacterItem item)
        {
            if (item == null || item.Kind != CharacterItemKind.Consumable || Contains(item))
            {
                return -1;
            }

            var index = Array.IndexOf(quick, null);
            if (index < 0)
            {
                return -1;
            }

            quick[index] = item;
            if (quick[selectedQuickIndex] == null)
            {
                selectedQuickIndex = index;
            }

            Changed?.Invoke();
            return index;
        }

        /// <summary>
        /// 前から順に空いたカバンの枠へ入れ、入れた番号を返す。いっぱいなら-1を返す。
        /// </summary>
        public int TryAddToBag(CharacterItem item)
        {
            if (item == null || Contains(item))
            {
                return -1;
            }

            var index = Array.IndexOf(bag, null);
            if (index < 0)
            {
                return -1;
            }

            bag[index] = item;
            Changed?.Invoke();
            return index;
        }

        /// <summary>
        /// 番号の空いた使用枠へ入れる。使う物でないか、枠が空いていなければ何も変えずにfalseを返す。
        /// 店で品物を枠へ運んで買うときに使う。
        /// </summary>
        public bool TryPutInQuick(CharacterItem item, int index)
        {
            if (item == null || item.Kind != CharacterItemKind.Consumable || Contains(item)
                || index < 0 || index >= quick.Length || quick[index] != null)
            {
                return false;
            }

            quick[index] = item;
            if (quick[selectedQuickIndex] == null)
            {
                selectedQuickIndex = index;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 番号の空いたカバンの枠へ入れる。枠が空いていなければ何も変えずにfalseを返す。
        /// </summary>
        public bool TryPutInBag(CharacterItem item, int index)
        {
            if (item == null || Contains(item) || index < 0 || index >= bag.Length || bag[index] != null)
            {
                return false;
            }

            bag[index] = item;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// カバンか使用枠の物を、カバンか使用枠の別の枠へ動かす。埋まっていれば入れ替える。
        /// 使用枠に使う物でない物が入ることになるか、元が空なら何も変えずにfalseを返す。
        /// </summary>
        public bool TryMoveStored(bool fromQuick, int from, bool toQuick, int to)
        {
            var source = fromQuick ? quick : bag;
            var target = toQuick ? quick : bag;
            if (from < 0 || from >= source.Length || to < 0 || to >= target.Length || source[from] == null)
            {
                return false;
            }

            if (source == target && from == to)
            {
                return false;
            }

            var moving = source[from];
            var other = target[to];
            if ((toQuick && moving.Kind != CharacterItemKind.Consumable)
                || (fromQuick && other != null && other.Kind != CharacterItemKind.Consumable))
            {
                return false;
            }

            target[to] = moving;
            source[from] = other;
            SelectNextFilledIfEmpty();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 着けている装備を外して、番号の空いたカバンの枠へ入れる。枠が埋まっていれば何も変えずにfalseを返す。
        /// </summary>
        public bool TryUnequipTo(CharacterEquipSlot slot, int bagIndex)
        {
            var item = equipped[(int)slot];
            if (item == null || bagIndex < 0 || bagIndex >= bag.Length || bag[bagIndex] != null)
            {
                return false;
            }

            Release(item);
            bag[bagIndex] = item;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// カバンの枠から物を取り出して返す。捨てるときに使う。
        /// </summary>
        public CharacterItem RemoveFromBag(int index)
        {
            var item = bag[index];
            if (item == null)
            {
                return null;
            }

            bag[index] = null;
            Changed?.Invoke();
            return item;
        }

        /// <summary>
        /// カバンにない装備を直接着ける。初めの装備や、買ってすぐ着ける物に使う。
        /// 外れた物はカバンへ入れ、入りきらなければ何も変えずにfalseを返す。
        /// </summary>
        public bool TryEquipNew(CharacterItem item)
        {
            if (item == null || item.Kind != CharacterItemKind.Equipment || Contains(item))
            {
                return false;
            }

            var displaced = CollectOccupants(item);
            if (displaced.Count > CountFreeBagSlots())
            {
                return false;
            }

            PlaceEquipment(item, displaced);
            foreach (var old in displaced)
            {
                bag[Array.IndexOf(bag, null)] = old;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// カバンを通さずに装備を着け、外れた物を返す。外れた物はカバンへ入れず、所持品から消える。
        /// 確認用に全装備を代金なしで試すときだけ使う。
        /// </summary>
        public List<CharacterItem> ForceEquip(CharacterItem item)
        {
            if (item == null || item.Kind != CharacterItemKind.Equipment || Contains(item))
            {
                return new List<CharacterItem>();
            }

            var displaced = CollectOccupants(item);
            PlaceEquipment(item, displaced);
            Changed?.Invoke();
            return displaced;
        }

        /// <summary>
        /// idの物が入っているカバンの枠の番号を返す。なければ-1を返す。
        /// </summary>
        public int FindInBag(string id)
        {
            for (var index = 0; index < bag.Length; index++)
            {
                if (bag[index] != null && bag[index].Id == id)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// 着けている装備を外してカバンへ入れる。カバンがいっぱいなら外さずにfalseを返す。
        /// </summary>
        public bool TryUnequip(CharacterEquipSlot slot)
        {
            var item = equipped[(int)slot];
            if (item == null || CountFreeBagSlots() == 0)
            {
                return false;
            }

            Release(item);
            bag[Array.IndexOf(bag, null)] = item;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 使用枠の物を使う。使う物は消えて呼び出し側へ渡り、選んでいた物がなくなれば次の物を選ぶ。
        /// </summary>
        public CharacterItemUseResult UseQuickItem(int index)
        {
            var item = quick[index];
            if (item == null)
            {
                return new CharacterItemUseResult(CharacterItemUseOutcome.EmptySlot, null);
            }

            quick[index] = null;
            SelectNextFilledIfEmpty();
            Changed?.Invoke();
            return new CharacterItemUseResult(CharacterItemUseOutcome.Consumed, item);
        }

        /// <summary>
        /// 選んでいる使用枠の物を使う。ゲームパッドで使う。
        /// </summary>
        public CharacterItemUseResult UseSelected()
        {
            return quick.Length == 0
                ? new CharacterItemUseResult(CharacterItemUseOutcome.EmptySlot, null)
                : UseQuickItem(selectedQuickIndex);
        }

        /// <summary>
        /// カバンの物を使う。装備は着けている物と入れ替わり、魔法はメイン武器の空いた枠へ入り、
        /// 使う物は空いた使用枠へ移る。できないときは何も変えない。
        /// </summary>
        public CharacterItemUseResult UseBagItem(int index)
        {
            var item = bag[index];
            if (item == null)
            {
                return new CharacterItemUseResult(CharacterItemUseOutcome.EmptySlot, null);
            }

            switch (item.Kind)
            {
                case CharacterItemKind.Consumable:
                    var target = Array.IndexOf(quick, null);
                    if (target < 0)
                    {
                        return new CharacterItemUseResult(CharacterItemUseOutcome.NoRoomInQuickSlots, item);
                    }

                    bag[index] = null;
                    quick[target] = item;
                    SelectNextFilledIfEmpty();
                    Changed?.Invoke();
                    return new CharacterItemUseResult(CharacterItemUseOutcome.MovedToQuickSlot, item);
                case CharacterItemKind.Equipment:
                    return EquipFromBag(index, item);
                case CharacterItemKind.Magic:
                    var weapon = MainWeapon;
                    if (weapon == null || weapon.TryInsertMagic(item) < 0)
                    {
                        return new CharacterItemUseResult(CharacterItemUseOutcome.NoMagicSlot, item);
                    }

                    bag[index] = null;
                    Changed?.Invoke();
                    return new CharacterItemUseResult(CharacterItemUseOutcome.MagicInserted, item);
                default:
                    return new CharacterItemUseResult(CharacterItemUseOutcome.NotUsable, item);
            }
        }

        /// <summary>
        /// 使用枠の物をカバンへ戻す。カバンがいっぱいならfalseを返す。
        /// </summary>
        public bool TryMoveQuickToBag(int index)
        {
            var item = quick[index];
            var target = Array.IndexOf(bag, null);
            if (item == null || target < 0)
            {
                return false;
            }

            quick[index] = null;
            bag[target] = item;
            SelectNextFilledIfEmpty();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 番号で使用枠を選ぶ。空の枠も選べる。範囲の外なら何もしない。
        /// </summary>
        public void SelectQuickIndex(int index)
        {
            if (index < 0 || index >= quick.Length || index == selectedQuickIndex)
            {
                return;
            }

            selectedQuickIndex = index;
            Changed?.Invoke();
        }

        /// <summary>
        /// 次の物の入った使用枠へ切り替える。空の枠は飛ばし、何も入っていなければ変えない。
        /// </summary>
        public int SelectNextQuickItem()
        {
            for (var step = 1; step <= quick.Length; step++)
            {
                var index = (selectedQuickIndex + step) % quick.Length;
                if (quick[index] != null)
                {
                    if (index != selectedQuickIndex)
                    {
                        selectedQuickIndex = index;
                        Changed?.Invoke();
                    }

                    break;
                }
            }

            return selectedQuickIndex;
        }

        /// <summary>
        /// メイン武器の魔法を次へ切り替える。武器や魔法がなければ-1か今の番号を返す。
        /// </summary>
        public int SelectNextMagic()
        {
            var weapon = MainWeapon;
            if (weapon == null)
            {
                return -1;
            }

            var before = weapon.SelectedMagicIndex;
            var after = weapon.SelectNextMagic();
            if (after != before)
            {
                Changed?.Invoke();
            }

            return after;
        }

        /// <summary>
        /// 魔法をはめたり外したりなど、持ち物の中身を外から変えたときに知らせる。
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private CharacterItemUseResult EquipFromBag(int index, CharacterItem item)
        {
            var displaced = CollectOccupants(item);

            // 使った物の抜けた枠にも入れられるので、空きは1つ多く数える。
            if (displaced.Count > CountFreeBagSlots() + 1)
            {
                return new CharacterItemUseResult(CharacterItemUseOutcome.NoRoomInBag, item);
            }

            bag[index] = null;
            PlaceEquipment(item, displaced);
            for (var order = 0; order < displaced.Count; order++)
            {
                // 外れた物の1つ目は使った物のいた枠へ入れ、持ち替えの前後で同じ番号から戻せるようにする。
                var target = order == 0 ? index : Array.IndexOf(bag, null);
                bag[target] = displaced[order];
            }

            Changed?.Invoke();
            return new CharacterItemUseResult(CharacterItemUseOutcome.Equipped, item);
        }

        private List<CharacterItem> CollectOccupants(CharacterItem item)
        {
            var occupants = new List<CharacterItem>();
            foreach (var slot in item.Slots)
            {
                var current = equipped[(int)slot];
                if (current != null && !occupants.Contains(current))
                {
                    occupants.Add(current);
                }
            }

            return occupants;
        }

        private void PlaceEquipment(CharacterItem item, List<CharacterItem> displaced)
        {
            foreach (var old in displaced)
            {
                Release(old);
            }

            foreach (var slot in item.Slots)
            {
                equipped[(int)slot] = item;
            }
        }

        private void Release(CharacterItem item)
        {
            for (var index = 0; index < equipped.Length; index++)
            {
                if (equipped[index] == item)
                {
                    equipped[index] = null;
                }
            }
        }

        private static int CountFree(CharacterItem[] slots)
        {
            var count = 0;
            foreach (var item in slots)
            {
                if (item == null)
                {
                    count++;
                }
            }

            return count;
        }

        // 選んでいた使用枠の物を使い切ったら、次の物の入った枠を選ぶ。
        private void SelectNextFilledIfEmpty()
        {
            if (quick.Length == 0 || quick[selectedQuickIndex] != null)
            {
                return;
            }

            for (var step = 1; step < quick.Length; step++)
            {
                var index = (selectedQuickIndex + step) % quick.Length;
                if (quick[index] != null)
                {
                    selectedQuickIndex = index;
                    return;
                }
            }
        }
    }
}
