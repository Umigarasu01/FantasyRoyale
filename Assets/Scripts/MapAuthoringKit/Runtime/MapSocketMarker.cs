using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit
{
    /// <summary>
    /// マップ上に置く候補地点の用途を表す。ゲーム処理ではなく、制作時の配置意図だけを分類する。
    /// </summary>
    public enum MapSocketKind
    {
        PlayerStart,
        CpuStart,
        Enemy,
        Loot,
        Merchant,
        Event,
        Landmark
    }

    /// <summary>
    /// Socketが想定する進入方向や正面を表す。Anyは向きを限定しない候補地点に使う。
    /// </summary>
    public enum MapSocketFacing
    {
        Any,
        North,
        East,
        South,
        West
    }

    /// <summary>
    /// Map Sceneに候補地点の種別、占有範囲、向き、任意Tagを残すための制作マーカー。
    /// Spawnや抽選を実行せず、ゲーム側が必要なときに読み取れる静的な配置情報だけを持つ。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Map Authoring/Map Socket Marker")]
    [DisallowMultipleComponent]
    public sealed class MapSocketMarker : MonoBehaviour
    {
        [SerializeField] private string socketId = string.Empty;
        [SerializeField] private MapSocketKind socketKind = MapSocketKind.Enemy;
        [SerializeField] private Vector2 size = Vector2.one;
        [SerializeField] private MapSocketFacing facing = MapSocketFacing.Any;
        [SerializeField] private string socketTag = string.Empty;
        [SerializeField] private MapEventDefinition eventDefinition;
        [SerializeField] private string eventDefinitionId = string.Empty;
        [SerializeField] [Min(0f)] private float interactionRadiusOverride;

        public string SocketId => socketId;
        public MapSocketKind SocketKind => socketKind;
        public Vector2 Size => size;
        public MapSocketFacing Facing => facing;
        public string SocketTag => socketTag;
        public MapEventDefinition EventDefinition => eventDefinition;
        public string SerializedEventDefinitionId => eventDefinitionId;
        public string EventDefinitionId => eventDefinition != null
            ? eventDefinition.EventDefinitionId
            : eventDefinitionId;
        public float InteractionRadiusOverride => interactionRadiusOverride;
        public float InteractionRadius => interactionRadiusOverride > 0f
            ? interactionRadiusOverride
            : eventDefinition != null ? eventDefinition.DefaultInteractionRadius : 0f;

        /// <summary>
        /// 直接参照を優先し、参照がない生成配置ではCatalogのID索引からEvent定義を解決する。
        /// </summary>
        public bool TryResolveEventDefinition(
            MapEventCatalog catalog,
            out MapEventDefinition resolvedDefinition)
        {
            if (eventDefinition != null)
            {
                resolvedDefinition = eventDefinition;
                return true;
            }

            if (catalog != null && catalog.TryGet(eventDefinitionId, out resolvedDefinition))
            {
                return true;
            }

            resolvedDefinition = null;
            return false;
        }

        /// <summary>
        /// Socket固有値を優先し、未指定なら解決済みEvent定義の標準接近半径を返す。
        /// </summary>
        public float ResolveInteractionRadius(MapEventCatalog catalog)
        {
            if (interactionRadiusOverride > 0f)
            {
                return interactionRadiusOverride;
            }

            return TryResolveEventDefinition(catalog, out var resolvedDefinition)
                ? resolvedDefinition.DefaultInteractionRadius
                : 0f;
        }

        /// <summary>
        /// Validatorやゲーム側が扱えるよう、回転とScaleを反映したSocket範囲のWorld AABBを返す。
        /// </summary>
        public Bounds WorldBounds
        {
            get
            {
                var corners = GetWorldCorners();
                var bounds = new Bounds(corners[0], Vector3.zero);
                for (var i = 1; i < corners.Length; i++)
                {
                    bounds.Encapsulate(corners[i]);
                }

                return bounds;
            }
        }

        /// <summary>
        /// Editor生成やテストから、Socketの制作情報をまとめて設定する。
        /// </summary>
        public void Configure(
            MapSocketKind newSocketKind,
            Vector2 newSize,
            MapSocketFacing newFacing,
            string newSocketTag,
            string newSocketId = null,
            MapEventDefinition newEventDefinition = null,
            string newEventDefinitionId = null,
            float newInteractionRadiusOverride = 0f)
        {
            socketId = string.IsNullOrWhiteSpace(newSocketId)
                ? gameObject.name
                : newSocketId.Trim();
            socketKind = newSocketKind;
            size = newSize;
            facing = newFacing;
            socketTag = newSocketTag ?? string.Empty;
            eventDefinition = newSocketKind == MapSocketKind.Event ? newEventDefinition : null;
            eventDefinitionId = newSocketKind == MapSocketKind.Event && newEventDefinition == null
                ? NormalizeId(newEventDefinitionId)
                : string.Empty;
            interactionRadiusOverride = newSocketKind == MapSocketKind.Event
                ? NormalizeRadiusOverride(newInteractionRadiusOverride)
                : 0f;
        }

        /// <summary>
        /// Scene Viewの半径HandleやEditor処理から、配置固有の接近半径だけを更新する。
        /// 0以下は定義の標準値を使う意味へ正規化する。
        /// </summary>
        public void SetInteractionRadiusOverride(float newInteractionRadiusOverride)
        {
            interactionRadiusOverride = NormalizeRadiusOverride(newInteractionRadiusOverride);
        }

        /// <summary>
        /// ID入力の前後空白だけを除き、Scene名やAsset名変更に依存しない値へ揃える。
        /// </summary>
        private static string NormalizeId(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        /// <summary>
        /// Socket固有半径は未指定を0で表し、InfinityやNaNは未指定へ戻す。
        /// </summary>
        private static float NormalizeRadiusOverride(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 0f : value;
        }

        /// <summary>
        /// 回転とScaleを含むTransformを反映し、左下から時計回りのSocket四隅をWorld座標で返す。
        /// </summary>
        public Vector3[] GetWorldCorners()
        {
            var halfSize = size * 0.5f;
            return new[]
            {
                transform.TransformPoint(new Vector3(-halfSize.x, -halfSize.y, 0f)),
                transform.TransformPoint(new Vector3(-halfSize.x, halfSize.y, 0f)),
                transform.TransformPoint(new Vector3(halfSize.x, halfSize.y, 0f)),
                transform.TransformPoint(new Vector3(halfSize.x, -halfSize.y, 0f))
            };
        }

        /// <summary>
        /// Scene Viewで未選択時にもSocketの範囲と向きを確認できるよう、用途別の輪郭を描く。
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawGizmo(false);
        }

        /// <summary>
        /// 選択中のSocketは半透明面も描き、重なりや占有範囲を判別しやすくする。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            DrawGizmo(true);
        }

        /// <summary>
        /// TransformとSocket Sizeを反映した矩形、およびFacingを示す矢印を描画する。
        /// </summary>
        private void DrawGizmo(bool drawFill)
        {
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            var color = GetSocketColor();
            Gizmos.matrix = transform.localToWorldMatrix;

            if (drawFill)
            {
                Gizmos.color = new Color(color.r, color.g, color.b, 0.14f);
                Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
            }

            Gizmos.color = color;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
            DrawFacingArrow();

            Gizmos.matrix = previousMatrix;
            if (socketKind == MapSocketKind.Event && InteractionRadius > 0f)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = new Color(color.r, color.g, color.b, drawFill ? 0.95f : 0.65f);
                DrawInteractionRadiusCircle(InteractionRadius);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        /// <summary>
        /// Eventの論理接近範囲をWorld座標の円として描き、物理Colliderと混同せず確認できるようにする。
        /// </summary>
        private void DrawInteractionRadiusCircle(float radius)
        {
            const int segmentCount = 48;
            var center = transform.position;
            var previousPoint = center + Vector3.right * radius;
            for (var segment = 1; segment <= segmentCount; segment++)
            {
                var angle = segment * Mathf.PI * 2f / segmentCount;
                var point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }

        /// <summary>
        /// Facingが指定されている場合だけ、Socket中心から正面へ向く短い矢印を描く。
        /// </summary>
        private void DrawFacingArrow()
        {
            var direction = GetFacingDirection();
            if (direction == Vector3.zero)
            {
                return;
            }

            var arrowLength = Mathf.Max(0.2f, Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.35f);
            var tip = direction * arrowLength;
            var side = new Vector3(-direction.y, direction.x, 0f) * arrowLength * 0.25f;
            var arrowBase = tip - direction * arrowLength * 0.3f;

            Gizmos.DrawLine(Vector3.zero, tip);
            Gizmos.DrawLine(tip, arrowBase + side);
            Gizmos.DrawLine(tip, arrowBase - side);
        }

        /// <summary>
        /// Facing列挙値をGizmo描画で使うローカル方向へ変換する。
        /// </summary>
        private Vector3 GetFacingDirection()
        {
            switch (facing)
            {
                case MapSocketFacing.North:
                    return Vector3.up;
                case MapSocketFacing.East:
                    return Vector3.right;
                case MapSocketFacing.South:
                    return Vector3.down;
                case MapSocketFacing.West:
                    return Vector3.left;
                default:
                    return Vector3.zero;
            }
        }

        /// <summary>
        /// Socket種別ごとに一貫したGizmo色を返し、Scene View上で用途を見分けやすくする。
        /// </summary>
        private Color GetSocketColor()
        {
            switch (socketKind)
            {
                case MapSocketKind.PlayerStart:
                    return new Color(0.15f, 0.9f, 1f, 0.95f);
                case MapSocketKind.CpuStart:
                    return new Color(0.2f, 0.55f, 1f, 0.95f);
                case MapSocketKind.Enemy:
                    return new Color(1f, 0.25f, 0.2f, 0.95f);
                case MapSocketKind.Loot:
                    return new Color(1f, 0.85f, 0.15f, 0.95f);
                case MapSocketKind.Merchant:
                    return new Color(0.65f, 0.3f, 1f, 0.95f);
                case MapSocketKind.Event:
                    return new Color(1f, 0.45f, 0.8f, 0.95f);
                case MapSocketKind.Landmark:
                    return new Color(0.35f, 1f, 0.4f, 0.95f);
                default:
                    return Color.white;
            }
        }
    }
}
