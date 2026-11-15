using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 倒した敵が落とすゴールド。何枚も弾けて散らばり、跳ねて止まる。
    /// 騎士が近づくと加速しながら吸い込まれ、触れた1枚ごとに所持金が1増えて小さく光る。
    /// 落ちているコインはときどきキラッと光り、吸い込まれている間は頻繁に光る。
    /// </summary>
    public sealed partial class ForestMapPreviewDebug
    {
        /// <summary>
        /// 1体が落とすゴールドの枚数の下限と上限。1枚がコイン1枚分である。
        /// </summary>
        public const int GoldPerEnemyMin = 6;
        public const int GoldPerEnemyMax = 10;

        /// <summary>
        /// 落ちてから拾えるまでの秒数。散らばって跳ねるのを見せてから吸い込む。
        /// </summary>
        public const float GoldPickupDelay = 0.6f;

        /// <summary>
        /// 騎士の足元からこの距離に入ったゴールドは、騎士へ吸い込まれる。
        /// </summary>
        public const float GoldMagnetRadius = 2.6f;

        /// <summary>
        /// 騎士の足元からこの距離に入ったゴールドを拾う。
        /// </summary>
        public const float GoldPickupRadius = 0.3f;

        private const float GoldScatterSpeedMin = 2.2f;
        private const float GoldScatterSpeedMax = 4.6f;
        private const float GoldGroundFriction = 6f;
        private const float GoldLaunchHeightSpeedMin = 3.2f;
        private const float GoldLaunchHeightSpeedMax = 4.6f;
        private const float GoldGravity = 22f;
        private const float GoldBounce = 0.4f;
        private const float GoldMagnetStartSpeed = 2f;
        private const float GoldMagnetAcceleration = 40f;
        private const float GoldMagnetMaxSpeed = 16f;
        private const float GoldMagnetLift = 0.25f;
        private const float GoldSpinFramesPerSecond = 12f;
        private const float GoldPopSeconds = 0.18f;
        private const float GoldPopScale = 2.2f;
        private const float GoldBobHeight = 0.04f;
        private const float GoldBobPerSecond = 1.2f;
        private const float GoldSparkleFramesPerSecond = 16f;
        private const float GoldSparkleIntervalMin = 0.25f;
        private const float GoldSparkleIntervalMax = 1.0f;
        private const float GoldSparklePullInterval = 0.12f;

        private readonly List<GoldDrop> goldDrops = new List<GoldDrop>();
        private readonly List<GoldPop> goldPops = new List<GoldPop>();
        private System.Random goldRandom;
        [SerializeField] private Sprite[] goldFrames = new Sprite[0];
        [SerializeField] private Sprite goldShadow;
        [SerializeField] private Sprite[] goldSparkleFrames = new Sprite[0];

        /// <summary>
        /// 落ちていてまだ拾っていないゴールドの数。
        /// </summary>
        public int GoldDropCount => goldDrops.Count;

        /// <summary>
        /// 落ちているゴールドの足元の位置。
        /// </summary>
        public Vector2 GetGoldDropPosition(int index)
        {
            return goldDrops[index].Position;
        }

        /// <summary>
        /// 落ちているゴールドが跳ねたり散らばったりせず、止まっているか。
        /// </summary>
        public bool IsGoldDropResting(int index)
        {
            return goldDrops[index].IsResting;
        }

        /// <summary>
        /// Editor Builderから、回るゴールドのコマ、足元の影、キラッと光るコマを設定する。
        /// </summary>
        public void ConfigureGold(Sprite[] newFrames, Sprite newShadow, Sprite[] newSparkleFrames)
        {
            goldFrames = newFrames ?? new Sprite[0];
            goldShadow = newShadow;
            goldSparkleFrames = newSparkleFrames ?? new Sprite[0];
        }

        /// <summary>
        /// 指定の位置から、決まった範囲の枚数のゴールドを四方へ弾き出す。
        /// </summary>
        private void DropGold(Vector2 position)
        {
            goldRandom ??= new System.Random(Seed + 7);
            var count = goldRandom.Next(GoldPerEnemyMin, GoldPerEnemyMax + 1);
            var turn = (float)goldRandom.NextDouble();
            for (var index = 0; index < count; index++)
            {
                // 向きは均等に分けて少し揺らし、片寄らずに輪になって散らばるようにする。
                var angle = (index + turn + Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(GoldScatterSpeedMin, GoldScatterSpeedMax);
                goldDrops.Add(CreateGoldDrop(position, velocity, Range(GoldLaunchHeightSpeedMin, GoldLaunchHeightSpeedMax)));
            }
        }

        private float Range(float min, float max)
        {
            return min + (float)goldRandom.NextDouble() * (max - min);
        }

        private GoldDrop CreateGoldDrop(Vector2 position, Vector2 velocity, float heightSpeed)
        {
            var dropObject = new GameObject("GoldDropPreviewDebug");
            dropObject.transform.position = position;
            SpriteRenderer shadow = null;
            if (goldShadow != null)
            {
                shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
                shadow.transform.SetParent(dropObject.transform, false);
                shadow.sharedMaterial = playerRenderer.sharedMaterial;
                shadow.sprite = goldShadow;
            }

            var look = new GameObject("Look").AddComponent<SpriteRenderer>();
            look.transform.SetParent(dropObject.transform, false);
            look.sharedMaterial = playerRenderer.sharedMaterial;
            look.sprite = goldFrames.Length > 0 ? goldFrames[0] : hudSkin != null ? hudSkin.Coin : null;
            SpriteRenderer sparkle = null;
            if (goldSparkleFrames.Length > 0)
            {
                sparkle = new GameObject("Sparkle").AddComponent<SpriteRenderer>();
                sparkle.transform.SetParent(look.transform, false);
                sparkle.sharedMaterial = playerRenderer.sharedMaterial;
                sparkle.enabled = false;
            }

            var drop = new GoldDrop(dropObject, look, shadow, sparkle, Time.time, velocity, heightSpeed, (float)goldRandom.NextDouble());
            drop.NextSparkleAt = Time.time + Range(0f, GoldSparkleIntervalMax);
            return drop;
        }

        /// <summary>
        /// 散らばる、跳ねる、回る、吸い込む、拾うを進める。倒れている間は吸い込まない。
        /// </summary>
        private void UpdateGoldDrops()
        {
            var deltaTime = Time.deltaTime;
            var player = PlayerActor.Body.position;
            for (var index = goldDrops.Count - 1; index >= 0; index--)
            {
                var drop = goldDrops[index];
                var age = Time.time - drop.DroppedAt;
                if (drop.IsMagnetized || (age >= GoldPickupDelay && PlayerHealth.IsAlive
                                          && Vector2.Distance(drop.Position, player) <= GoldMagnetRadius))
                {
                    if (PullGold(drop, player, deltaTime))
                    {
                        CollectGold(index);
                        continue;
                    }
                }
                else
                {
                    ScatterGold(drop, deltaTime);
                }

                Spin(drop, age);
                Twinkle(drop);
                var bob = drop.IsResting ? GoldBobHeight * (1f + Mathf.Sin(age * GoldBobPerSecond * 2f * Mathf.PI)) : 0f;
                drop.Look.transform.localPosition = new Vector3(0f, drop.Height + bob, 0f);
            }

            UpdateGoldPops(deltaTime);
        }

        /// <summary>
        /// 地面を滑りながら減速し、高さは放物線で落ちて一度跳ねる。
        /// </summary>
        private static void ScatterGold(GoldDrop drop, float deltaTime)
        {
            drop.Position += drop.Velocity * deltaTime;
            drop.Velocity = Vector2.MoveTowards(drop.Velocity, Vector2.zero, GoldGroundFriction * deltaTime);
            drop.HeightSpeed -= GoldGravity * deltaTime;
            drop.Height += drop.HeightSpeed * deltaTime;
            if (drop.Height > 0f)
            {
                return;
            }

            drop.Height = 0f;
            drop.HeightSpeed = drop.HeightSpeed < -1f ? -drop.HeightSpeed * GoldBounce : 0f;
        }

        /// <summary>
        /// 騎士へ加速しながら寄せ、少し浮かせる。届いたらtrueを返す。一度吸い込み始めたら離れても追い続ける。
        /// </summary>
        private static bool PullGold(GoldDrop drop, Vector2 player, float deltaTime)
        {
            if (!drop.IsMagnetized)
            {
                drop.IsMagnetized = true;
                drop.PullSpeed = GoldMagnetStartSpeed;
            }

            drop.PullSpeed = Mathf.Min(GoldMagnetMaxSpeed, drop.PullSpeed + GoldMagnetAcceleration * deltaTime);
            drop.Position = Vector2.MoveTowards(drop.Position, player, drop.PullSpeed * deltaTime);
            drop.Height = Mathf.MoveTowards(drop.Height, GoldMagnetLift, deltaTime * 2f);
            return Vector2.Distance(drop.Position, player) <= GoldPickupRadius;
        }

        /// <summary>
        /// 1枚拾って所持金を1増やし、騎士の胸のあたりで大きくキラッと光らせる。
        /// </summary>
        private void CollectGold(int index)
        {
            var drop = goldDrops[index];
            Coins += 1;
            LastMessage = "コインを拾った";
            var pop = drop.Look;
            if (drop.Sparkle != null)
            {
                Destroy(drop.Sparkle.gameObject);
            }

            pop.transform.SetParent(null, true);
            pop.transform.position = (Vector3)(PlayerActor.Body.position + Vector2.up * 0.7f);
            if (goldSparkleFrames.Length > 0)
            {
                // 一番大きく広がったコマの光りを、拾った印にする。続けて拾うと少しずつずれて弾ける。
                pop.sprite = goldSparkleFrames[Mathf.Min(2, goldSparkleFrames.Length - 1)];
                pop.transform.position += (Vector3)new Vector2(Range(-0.2f, 0.2f), Range(-0.15f, 0.15f));
            }
            pop.sortingOrder = PlayerAnimator != null ? FeetOrder(PlayerActor.Body.position.y) + 50 : pop.sortingOrder;
            goldPops.Add(new GoldPop(pop, Time.time));
            Destroy(drop.Object);
            goldDrops.RemoveAt(index);
        }

        /// <summary>
        /// 拾った光りを、大きくしながら薄くして消す。
        /// </summary>
        private void UpdateGoldPops(float deltaTime)
        {
            for (var index = goldPops.Count - 1; index >= 0; index--)
            {
                var pop = goldPops[index];
                var t = (Time.time - pop.StartedAt) / GoldPopSeconds;
                if (t >= 1f || pop.Renderer == null)
                {
                    if (pop.Renderer != null)
                    {
                        Destroy(pop.Renderer.gameObject);
                    }

                    goldPops.RemoveAt(index);
                    continue;
                }

                pop.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(1f, GoldPopScale, t);
                pop.Renderer.transform.position += Vector3.up * (deltaTime * 1.5f);
                pop.Renderer.color = new Color(1f, 1f, 0.85f, 1f - t);
            }
        }

        /// <summary>
        /// 決まった間隔でコインの上の方にキラッとした光りを1回瞬かせる。吸い込まれている間は間隔を短くする。
        /// </summary>
        private void Twinkle(GoldDrop drop)
        {
            if (drop.Sparkle == null)
            {
                return;
            }

            if (!drop.Sparkle.enabled)
            {
                if (Time.time < drop.NextSparkleAt)
                {
                    return;
                }

                drop.SparkleStartedAt = Time.time;
                drop.Sparkle.enabled = true;
                drop.Sparkle.transform.localPosition = new Vector3(Range(-0.12f, 0.12f), Range(0.12f, 0.32f), 0f);
            }

            var frame = Mathf.FloorToInt((Time.time - drop.SparkleStartedAt) * GoldSparkleFramesPerSecond);
            if (frame >= goldSparkleFrames.Length)
            {
                drop.Sparkle.enabled = false;
                drop.NextSparkleAt = Time.time + (drop.IsMagnetized
                    ? GoldSparklePullInterval
                    : Range(GoldSparkleIntervalMin, GoldSparkleIntervalMax));
                return;
            }

            drop.Sparkle.sprite = goldSparkleFrames[frame];
            drop.Sparkle.sortingOrder = drop.Look.sortingOrder + 1;
        }

        private void Spin(GoldDrop drop, float age)
        {
            if (goldFrames.Length == 0)
            {
                return;
            }

            var frame = Mathf.FloorToInt((age + drop.SpinPhase) * GoldSpinFramesPerSecond) % goldFrames.Length;
            drop.Look.sprite = goldFrames[frame];
        }

        private void SortGoldDrops()
        {
            for (var index = 0; index < goldDrops.Count; index++)
            {
                var drop = goldDrops[index];
                SortByFeet(drop.Look, drop.Position.y);
                if (drop.Shadow != null)
                {
                    drop.Shadow.sortingOrder = drop.Look.sortingOrder - 1;
                }
            }
        }

        private void DestroyGoldDrops()
        {
            foreach (var drop in goldDrops)
            {
                if (drop.Object != null)
                {
                    Destroy(drop.Object);
                }
            }

            foreach (var pop in goldPops)
            {
                if (pop.Renderer != null)
                {
                    Destroy(pop.Renderer.gameObject);
                }
            }

            goldDrops.Clear();
            goldPops.Clear();
        }

        /// <summary>
        /// 落ちている一枚のゴールド。位置は足元で、見た目は子のSpriteを高さの分だけ持ち上げる。
        /// </summary>
        private sealed class GoldDrop
        {
            public GoldDrop(
                GameObject dropObject,
                SpriteRenderer look,
                SpriteRenderer shadow,
                SpriteRenderer sparkle,
                float droppedAt,
                Vector2 velocity,
                float heightSpeed,
                float spinPhase)
            {
                Object = dropObject;
                Look = look;
                Shadow = shadow;
                Sparkle = sparkle;
                DroppedAt = droppedAt;
                Velocity = velocity;
                HeightSpeed = heightSpeed;
                SpinPhase = spinPhase;
            }

            public GameObject Object { get; }
            public SpriteRenderer Look { get; }
            public SpriteRenderer Shadow { get; }
            public SpriteRenderer Sparkle { get; }
            public float NextSparkleAt { get; set; }
            public float SparkleStartedAt { get; set; }
            public float DroppedAt { get; }
            public float SpinPhase { get; }
            public Vector2 Velocity { get; set; }
            public float Height { get; set; }
            public float HeightSpeed { get; set; }
            public bool IsMagnetized { get; set; }
            public float PullSpeed { get; set; }
            public bool IsResting => !IsMagnetized && Height <= 0f && HeightSpeed <= 0f && Velocity == Vector2.zero;

            public Vector2 Position
            {
                get => Object.transform.position;
                set => Object.transform.position = value;
            }
        }

        private readonly struct GoldPop
        {
            public GoldPop(SpriteRenderer renderer, float startedAt)
            {
                Renderer = renderer;
                StartedAt = startedAt;
            }

            public SpriteRenderer Renderer { get; }
            public float StartedAt { get; }
        }
    }
}
