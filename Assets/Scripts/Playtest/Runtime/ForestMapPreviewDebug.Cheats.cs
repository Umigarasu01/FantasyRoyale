using FantasyRoyale.Gameplay.Characters;
using UnityEngine;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 遊びながら確かめるためのデバッグ機能。所持金の増減、HPの全回復、敵を全部倒す、無敵、ワンパンを、
    /// 左下の「デバッグを開く」の画面から使う。
    /// </summary>
    public sealed partial class ForestMapPreviewDebug
    {
        private const float CheatPanelWidth = 300f;
        private const float CheatPanelHeight = 124f;
        private static readonly int[] CoinSteps = { -100, -10, 10, 100 };

        /// <summary>
        /// デバッグの画面を開いているか。
        /// </summary>
        public bool IsCheatPanelOpen { get; set; }

        /// <summary>
        /// 騎士がダメージを受けないか。今の騎士へのダメージは敵の接触だけなので、接触を止めて表す。
        /// </summary>
        public bool IsInvincible { get; set; }

        /// <summary>
        /// 騎士の攻撃が当たった敵を、武器の威力によらず一撃で倒すか。
        /// </summary>
        public bool IsOneHitKill { get; set; }

        /// <summary>
        /// 「デバッグを開く」のボタン。「全装備を開く」の右に並べる。
        /// </summary>
        private static Rect CheatToggleRect
        {
            get
            {
                var equipment = EquipmentToggleRect;
                return new Rect(equipment.xMax + DebugGap, equipment.y, equipment.width, equipment.height);
            }
        }

        /// <summary>
        /// デバッグの画面の枠。確認用の文字の右に、下端をボタンに揃えて置く。
        /// </summary>
        private static Rect CheatPanelRect => new Rect(
            DebugMargin + DebugAreaWidth + DebugGap,
            Screen.height - DebugMargin - CheatPanelHeight,
            CheatPanelWidth,
            CheatPanelHeight);

        /// <summary>
        /// 確認用の文字の1行目に足す、今効いているデバッグ機能の名前。
        /// </summary>
        private string CheatStatusText => (IsInvincible ? "   無敵" : string.Empty) + (IsOneHitKill ? "   ワンパン" : string.Empty);

        /// <summary>
        /// コインを増やすか、負の値で減らす。0枚より少なくはならない。
        /// </summary>
        public void AddCoins(int amount)
        {
            Coins = Mathf.Max(0, Coins + amount);
        }

        /// <summary>
        /// 騎士のHPを最大まで戻す。倒れている間は戻さず、いつもどおり村で復活する。
        /// </summary>
        public bool RestorePlayerHealth()
        {
            var result = PlayerHealth.RestoreHealth(PlayerHealth.MaximumHealth);
            LastMessage = result.IsApplied ? "HPを全回復した" : PlayerHealth.IsAlive ? "HPは減っていない" : "倒れている間は回復しない";
            return result.IsApplied;
        }

        /// <summary>
        /// 出ている敵を全て倒し、倒した数を返す。剣で倒したときと同じく、1体ごとにコインが増え、時間が経つと持ち場へ戻る。
        /// </summary>
        public int DefeatAllEnemies()
        {
            var count = 0;
            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }

                enemy.TakeAllHealth();
                DefeatEnemy(enemy);
                count++;
            }

            LastMessage = count > 0 ? $"敵を {count} 体倒した" : "倒せる敵がいない";
            return count;
        }

        /// <summary>
        /// 「デバッグを開く」のボタンと、開いていればデバッグの画面を描く。
        /// </summary>
        private void DrawCheatGui()
        {
            if (GUI.Button(CheatToggleRect, IsCheatPanelOpen ? "デバッグを閉じる" : "デバッグを開く"))
            {
                IsCheatPanelOpen = !IsCheatPanelOpen;
            }

            if (!IsCheatPanelOpen)
            {
                return;
            }

            GUILayout.BeginArea(CheatPanelRect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"所持金 {Coins}", GUILayout.Width(88f));
            foreach (var step in CoinSteps)
            {
                if (GUILayout.Button(step > 0 ? $"+{step}" : step.ToString()))
                {
                    AddCoins(step);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HPを全回復"))
            {
                RestorePlayerHealth();
            }

            if (GUILayout.Button("敵を全部倒す"))
            {
                DefeatAllEnemies();
            }

            GUILayout.EndHorizontal();
            IsInvincible = GUILayout.Toggle(IsInvincible, " 無敵（敵に触れてもHPが減らない）");
            IsOneHitKill = GUILayout.Toggle(IsOneHitKill, " ワンパン（攻撃が当たった敵を一撃で倒す）");
            GUILayout.EndArea();
        }

        /// <summary>
        /// 敵のHurtboxへ渡すHP。ワンパンの間は、受けたダメージを残りのHPまで引き上げて一撃で倒す。
        /// HPそのものは敵の<see cref="CharacterHealth"/>なので、被弾の赤、倒した判定、復活はそのまま動く。
        /// </summary>
        private sealed class OneHitHealth : ICharacterHealthTarget
        {
            private readonly CharacterHealth health;
            private readonly ForestMapPreviewDebug owner;

            public OneHitHealth(CharacterHealth health, ForestMapPreviewDebug owner)
            {
                this.health = health;
                this.owner = owner;
            }

            public int CurrentHealth => health.CurrentHealth;
            public int MaximumHealth => health.MaximumHealth;
            public bool IsAlive => health.IsAlive;

            public CharacterHealthChangeResult ApplyDamage(int requestedAmount)
            {
                return health.ApplyDamage(owner.IsOneHitKill && requestedAmount > 0
                    ? Mathf.Max(requestedAmount, health.CurrentHealth)
                    : requestedAmount);
            }

            public CharacterHealthChangeResult RestoreHealth(int requestedAmount)
            {
                return health.RestoreHealth(requestedAmount);
            }
        }
    }
}
