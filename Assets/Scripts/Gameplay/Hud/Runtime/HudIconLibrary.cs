using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>
    /// 品物のアイコンや店主の顔を、名前から引く定義Asset。名前は絵のFile名（拡張子なし）である。
    /// 今の絵は`Tools/UiAuthoring/build_shop_art.py`がゲームの絵から切り出した仮の物である。
    /// </summary>
    [CreateAssetMenu(menuName = "FantasyRoyale/HUD Icon Library", fileName = "HudIconLibrary")]
    public sealed class HudIconLibrary : ScriptableObject
    {
        [SerializeField] private Sprite[] sprites = new Sprite[0];

        private Dictionary<string, Sprite> byName;

        public int Count => sprites.Length;

        /// <summary>名前の絵を返す。ないときはnullを返す。</summary>
        public Sprite Find(string name)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var sprite in sprites)
                {
                    if (sprite != null)
                    {
                        byName[sprite.name] = sprite;
                    }
                }
            }

            return name != null && byName.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>Editor BuilderとTestから絵を設定する。</summary>
        public void Configure(Sprite[] newSprites)
        {
            sprites = newSprites ?? new Sprite[0];
            byName = null;
        }
    }
}
