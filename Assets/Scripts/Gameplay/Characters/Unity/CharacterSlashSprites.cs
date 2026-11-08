using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 近接攻撃の当たり判定を見せる斬撃の絵を、判定の距離と扇の幅から作る。
    /// 外側の縁が判定の届く距離そのものなので、同じ武器種でも武器の距離が違えば斬撃の大きさも変わる。
    /// 絵は東向きの扇で、軸は扇の中心にある。振り始めを上、振り終わりを下に描き、振り終わりの側ほど濃い。
    /// 向きは回転で変え、振る向きが逆の向きはflipYで上下を入れ替える。
    /// </summary>
    public static class CharacterSlashSprites
    {
        /// <summary>
        /// 体と同じドットの細かさ。
        /// </summary>
        public const float PixelsPerUnit = 64f;

        // 扇の中央の帯の太さは、判定の距離に対する割合で決める。両端は縁の太さまで細くする。
        private const float BandShare = 0.4f;
        private const float RimPixels = 2f;
        private const int BandLevels = 3;
        private const float BandAlpha = 170f;
        private const float StartStrength = 0.45f;
        private static readonly Color32 RimColour = new Color32(250, 253, 255, 235);
        private static readonly Color32 BandColour = new Color32(190, 225, 255, 0);
        private static readonly Dictionary<(float Range, float Arc), Sprite> Sprites = new Dictionary<(float Range, float Arc), Sprite>();

        /// <summary>
        /// 判定の距離と扇の幅に合う斬撃を返す。同じ組み合わせなら作り直さない。
        /// </summary>
        public static Sprite Get(float range, float arcDegrees)
        {
            if (Sprites.TryGetValue((range, arcDegrees), out var sprite) && sprite != null)
            {
                return sprite;
            }

            var texture = CreateTexture(range, arcDegrees);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            Sprites[(range, arcDegrees)] = sprite;
            return sprite;
        }

        /// <summary>
        /// 東向きの扇の外周に、明るい縁と内へ薄れる帯を描く。帯の濃さは数段に分けてドット絵に揃える。
        /// </summary>
        private static Texture2D CreateTexture(float range, float arcDegrees)
        {
            var outer = range * PixelsPerUnit;
            var half = Mathf.CeilToInt(outer) + 1;
            var size = half * 2;
            var halfArc = arcDegrees * 0.5f;
            var band = Mathf.Max(RimPixels, outer * BandShare);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var offset = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    var angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                    var share = Mathf.Abs(angle) / halfArc;
                    var radius = offset.magnitude;
                    var thickness = RimPixels + (band - RimPixels) * (1f - share * share);
                    if (share > 1f || radius > outer || radius < outer - thickness)
                    {
                        continue;
                    }

                    if (radius >= outer - RimPixels)
                    {
                        pixels[y * size + x] = RimColour;
                        continue;
                    }

                    // 縁から内へ薄れ、振り始めの上から振り終わりの下へ濃くなる。
                    var depth = (outer - RimPixels - radius) / (thickness - RimPixels);
                    var progress = (halfArc - angle) / arcDegrees;
                    var strength = (1f - depth) * (StartStrength + (1f - StartStrength) * progress);
                    var level = Mathf.Ceil(strength * BandLevels) / BandLevels;
                    var colour = BandColour;
                    colour.a = (byte)Mathf.RoundToInt(BandAlpha * level);
                    pixels[y * size + x] = colour;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"CharacterSlash-{range:0.###}-{arcDegrees:0.#}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
