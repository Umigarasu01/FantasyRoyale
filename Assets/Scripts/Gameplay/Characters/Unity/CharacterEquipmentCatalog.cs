using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// idを持つ全ての防具と武器の一覧。中身はEditorが定義Assetから集めるので、装備を増やしても手で足さない。
    /// </summary>
    public sealed class CharacterEquipmentCatalog : ScriptableObject
    {
        [SerializeField] private CharacterSpriteSetDefinition[] armour = new CharacterSpriteSetDefinition[0];
        [SerializeField] private CharacterEquipmentDefinition[] weapons = new CharacterEquipmentDefinition[0];

        public IReadOnlyList<CharacterSpriteSetDefinition> Armour => armour;
        public IReadOnlyList<CharacterEquipmentDefinition> Weapons => weapons;

        /// <summary>
        /// 指定の部位の絵を持つ防具を返す。
        /// </summary>
        public List<CharacterSpriteSetDefinition> GetArmour(CharacterArmourSlot slot)
        {
            var parts = new List<CharacterSpriteSetDefinition>();
            foreach (var part in armour)
            {
                if (part.Covers(slot))
                {
                    parts.Add(part);
                }
            }

            return parts;
        }

        /// <summary>
        /// 一覧の武器にある武器種を、動きの型の順に返す。武器種は武器のデータから決まるので、ここへ足さない。
        /// </summary>
        public List<CharacterActionMotion> GetWeaponKinds()
        {
            var kinds = new List<CharacterActionMotion>();
            foreach (var weapon in weapons)
            {
                if (!kinds.Contains(weapon.WeaponKind))
                {
                    kinds.Add(weapon.WeaponKind);
                }
            }

            kinds.Sort();
            return kinds;
        }

        /// <summary>
        /// 指定の武器種の武器を、一覧の順に返す。
        /// </summary>
        public List<CharacterEquipmentDefinition> GetWeapons(CharacterActionMotion kind)
        {
            var found = new List<CharacterEquipmentDefinition>();
            foreach (var weapon in weapons)
            {
                if (weapon.WeaponKind == kind)
                {
                    found.Add(weapon);
                }
            }

            return found;
        }

        /// <summary>
        /// idの防具を返す。なければnullである。
        /// </summary>
        public CharacterSpriteSetDefinition FindArmour(string id)
        {
            foreach (var part in armour)
            {
                if (part.Id == id)
                {
                    return part;
                }
            }

            return null;
        }

        /// <summary>
        /// idの武器を返す。なければnullである。
        /// </summary>
        public CharacterEquipmentDefinition FindWeapon(string id)
        {
            foreach (var weapon in weapons)
            {
                if (weapon.Id == id)
                {
                    return weapon;
                }
            }

            return null;
        }

        /// <summary>
        /// 同じidの装備を調べ、問題の一覧を返す。空ならidで一つに決まる。
        /// </summary>
        public List<string> FindIdProblems()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (var part in armour)
            {
                if (!seen.Add(part.Id))
                {
                    problems.Add($"{part.name}: id「{part.Id}」は他の装備と同じです。");
                }
            }

            foreach (var weapon in weapons)
            {
                if (!seen.Add(weapon.Id))
                {
                    problems.Add($"{weapon.name}: id「{weapon.Id}」は他の装備と同じです。");
                }
            }

            return problems;
        }

        /// <summary>
        /// Editorから、集めた定義を設定する。
        /// </summary>
        public void Configure(CharacterSpriteSetDefinition[] newArmour, CharacterEquipmentDefinition[] newWeapons)
        {
            armour = newArmour;
            weapons = newWeapons;
        }
    }
}
