using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 武器の魔法の枠にはめる魔法1つの定義。唱えるAction、使うMPを持つ。
    /// 手に入れた魔法はカバンや武器の枠に入る持ち物で、idでこの定義へつなぐ。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterMagic",
        menuName = "FantasyRoyale/Gameplay/Character Magic")]
    public sealed class CharacterMagicDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField, Min(0)] private int manaCost = 10;
        [SerializeField] private CharacterActionDefinition action;

        public string Id => id;
        public string DisplayName => displayName;
        public int ManaCost => manaCost;
        public CharacterActionDefinition Action => action;

        /// <summary>
        /// この定義から、手に入れた1つ分の魔法を作る。
        /// </summary>
        public CharacterItem CreateItem()
        {
            return new CharacterItem(id, CharacterItemKind.Magic);
        }

        /// <summary>
        /// 唱えられる定義かを調べ、問題の一覧を返す。空なら使える。
        /// </summary>
        public List<string> FindProblems()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(id))
            {
                problems.Add($"{name}: idがありません。");
            }

            if (action == null)
            {
                problems.Add($"{name}: 唱えるActionがありません。");
            }

            return problems;
        }

        /// <summary>
        /// Editor Builderから値をまとめて設定する。
        /// </summary>
        public void Configure(string newId, string newDisplayName, int newManaCost, CharacterActionDefinition newAction)
        {
            id = newId;
            displayName = newDisplayName;
            manaCost = Mathf.Max(0, newManaCost);
            action = newAction;
        }
    }
}
