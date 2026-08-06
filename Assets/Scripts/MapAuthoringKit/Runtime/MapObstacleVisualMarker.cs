using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit
{
    /// <summary>
    /// 障害物の物理判定をTilemapへ焼くか、足元へ追従する独立Colliderで表現するかを示す。
    /// </summary>
    public enum MapObstacleCollisionMode
    {
        TilemapFootprint,
        CollisionBody
    }

    /// <summary>
    /// CollisionBodyへ生成する単純形状を示し、見た目の輪郭ではなく接地点へ判定を寄せる。
    /// </summary>
    public enum MapObstacleCollisionShape
    {
        Box,
        Capsule
    }

    /// <summary>
    /// 障害物表示Prefabに、生成計画用Footprintと足元へ置く物理判定の方式を保持する制作マーカー。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Map Authoring/Map Obstacle Visual Marker")]
    [DisallowMultipleComponent]
    public sealed class MapObstacleVisualMarker : MonoBehaviour
    {
        public const string CollisionBodyName = "CollisionBody";

        [SerializeField] private Vector2Int footprintSize = Vector2Int.one;
        [SerializeField] private Vector2Int footprintOffset = Vector2Int.zero;
        [SerializeField] private MapObstacleCollisionMode collisionMode =
            MapObstacleCollisionMode.TilemapFootprint;
        [SerializeField] private MapObstacleCollisionShape collisionShape =
            MapObstacleCollisionShape.Box;
        [SerializeField] private Vector2 collisionSize = Vector2.one;
        [SerializeField] private Vector2 collisionOffset = Vector2.zero;
        [SerializeField] private float collisionRotationDegrees;
        [SerializeField] private CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Horizontal;

        public Vector2Int FootprintSize => footprintSize;
        public Vector2Int FootprintOffset => footprintOffset;
        public MapObstacleCollisionMode CollisionMode => collisionMode;
        public MapObstacleCollisionShape CollisionShape => collisionShape;
        public Vector2 CollisionSize => collisionSize;
        public Vector2 CollisionOffset => collisionOffset;
        public float CollisionRotationDegrees => collisionRotationDegrees;
        public CapsuleDirection2D CapsuleDirection => capsuleDirection;

        /// <summary>
        /// 旧呼出しとの互換用に、Cell Footprintだけを使う障害物として設定する。
        /// </summary>
        public void Configure(Vector2Int size, Vector2Int offset)
        {
            Configure(
                size,
                offset,
                MapObstacleCollisionMode.TilemapFootprint,
                MapObstacleCollisionShape.Box,
                Vector2.zero,
                Vector2.zero);
        }

        /// <summary>
        /// 生成計画用Footprintと物理判定方式・形状をまとめて設定し、無効なFootprint Sizeを補正する。
        /// </summary>
        public void Configure(
            Vector2Int size,
            Vector2Int offset,
            MapObstacleCollisionMode mode,
            MapObstacleCollisionShape shape,
            Vector2 collisionSize,
            Vector2 collisionOffset,
            float rotationDegrees = 0f,
            CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Horizontal)
        {
            footprintSize = new Vector2Int(
                Mathf.Max(1, size.x),
                Mathf.Max(1, size.y));
            footprintOffset = offset;
            collisionMode = mode;
            collisionShape = shape;
            this.collisionSize = mode == MapObstacleCollisionMode.CollisionBody
                ? new Vector2(
                    Mathf.Max(1f / 32f, collisionSize.x),
                    Mathf.Max(1f / 32f, collisionSize.y))
                : Vector2.zero;
            this.collisionOffset = collisionOffset;
            collisionRotationDegrees = rotationDegrees;
            this.capsuleDirection = capsuleDirection;
        }

        /// <summary>
        /// 配置Anchor Cellを受け取り、生成時の占有・間隔確認に使うFootprint Cellを順に列挙する。
        /// </summary>
        public IEnumerable<Vector3Int> GetFootprintCells(Vector3Int anchorCell)
        {
            for (var y = 0; y < footprintSize.y; y++)
            {
                for (var x = 0; x < footprintSize.x; x++)
                {
                    yield return new Vector3Int(
                        anchorCell.x + footprintOffset.x + x,
                        anchorCell.y + footprintOffset.y + y,
                        anchorCell.z);
                }
            }
        }
    }
}
