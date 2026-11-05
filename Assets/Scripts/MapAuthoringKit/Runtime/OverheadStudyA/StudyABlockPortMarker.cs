using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    /// <summary>Prefabの接続帯。Transformは帯の最小Cell中心。ゲームEvent/Spawn候補ではない。</summary>
    [DisallowMultipleComponent]
    public sealed class StudyABlockPortMarker : MonoBehaviour
    {
        public string Id;
        public StudyAPortDirection Direction;
        public int Width = 3;
        public StudyATerrain Terrain = StudyATerrain.Soil;
        public int Height;

        public StudyABlockPort ReadPort()
        {
            var position = (Vector2)transform.localPosition - Vector2.one * 0.5f;
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y) || transform.localPosition.z != 0f)
                throw new System.ArgumentException("Port座標は有限XY/Z0にしてください: " + name);
            if (Vector2.Distance(position, Vector2Int.RoundToInt(position)) > 0.00001f)
                throw new System.ArgumentException("PortはCell中心に配置してください: " + name);
            return new StudyABlockPort { Id = Id, Position = Vector2Int.RoundToInt(position), Direction = Direction, Width = Width, Terrain = Terrain, Height = Height };
        }

        private void OnDrawGizmosSelected()
        {
            var along = Direction == StudyAPortDirection.West || Direction == StudyAPortDirection.East ? Vector3.up : Vector3.right;
            var center = along * (Width - 1) * 0.5f;
            var previous = Gizmos.matrix; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Terrain == StudyATerrain.Water ? Color.cyan : Color.yellow;
            Gizmos.DrawWireCube(center, along == Vector3.up ? new Vector3(1f, Width, 0f) : new Vector3(Width, 1f, 0f));
            Gizmos.matrix = previous;
        }
    }
}
