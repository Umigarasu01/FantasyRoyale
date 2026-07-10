using FantasyRoyale.Core.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// Milestone 1探索Sceneの固定Tilemap描画とスロット抽選配置を担当する。
    /// Scene上の見た目生成だけを扱い、抽選判断はCoreのFR_MapPlacementResolverへ委譲する。
    /// </summary>
    public sealed class ExplorationMapBuilder : MonoBehaviour
    {
        [SerializeField] private MapDefinitionAsset mapDefinition;
        [SerializeField] private MapTilePaletteAsset tilePalette;
        [SerializeField] private MapObjectSpawnTableAsset spawnTable;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap detailTilemap;
        [SerializeField] private Tilemap collisionTilemap;
        [SerializeField] private Transform objectRoot;
        [SerializeField] private int placementSeed = 1001;

        public Vector2Int MapSize => mapDefinition != null ? mapDefinition.GridSize : Vector2Int.zero;

        public void Configure(
            MapDefinitionAsset newMapDefinition,
            MapTilePaletteAsset newTilePalette,
            MapObjectSpawnTableAsset newSpawnTable,
            Tilemap newGroundTilemap,
            Tilemap newDetailTilemap,
            Tilemap newCollisionTilemap,
            Transform newObjectRoot,
            int newPlacementSeed)
        {
            mapDefinition = newMapDefinition;
            tilePalette = newTilePalette;
            spawnTable = newSpawnTable;
            groundTilemap = newGroundTilemap;
            detailTilemap = newDetailTilemap;
            collisionTilemap = newCollisionTilemap;
            objectRoot = newObjectRoot;
            placementSeed = newPlacementSeed;
        }

        public Vector3 GridToWorldCenter(Vector2Int gridPosition, Vector2Int footprintSize)
        {
            if (mapDefinition == null)
            {
                return Vector3.zero;
            }

            var origin = new Vector2Int(-mapDefinition.GridSize.x / 2, -mapDefinition.GridSize.y / 2);
            return new Vector3(
                origin.x + gridPosition.x + footprintSize.x * 0.5f,
                origin.y + gridPosition.y + footprintSize.y * 0.5f,
                0f);
        }

        public void Build()
        {
            if (mapDefinition == null || tilePalette == null || spawnTable == null)
            {
                Debug.LogWarning("ExplorationMapBuilder requires mapDefinition, tilePalette, and spawnTable.");
                return;
            }

            ClearGeneratedObjects();
            PaintTilemaps();
            SpawnObjects();
        }

        private void PaintTilemaps()
        {
            groundTilemap.ClearAllTiles();
            detailTilemap.ClearAllTiles();
            collisionTilemap.ClearAllTiles();

            var grass = tilePalette.Find(FR_MapTileKind.Grass);
            var origin = new Vector3Int(-mapDefinition.GridSize.x / 2, -mapDefinition.GridSize.y / 2, 0);

            for (var y = 0; y < mapDefinition.GridSize.y; y++)
            {
                for (var x = 0; x < mapDefinition.GridSize.x; x++)
                {
                    groundTilemap.SetTile(new Vector3Int(origin.x + x, origin.y + y, 0), grass);
                }
            }

            var areas = mapDefinition.TileAreas;
            for (var i = 0; i < areas.Length; i++)
            {
                PaintArea(areas[i], origin);
            }
        }

        private void PaintArea(MapTileArea area, Vector3Int origin)
        {
            var tile = tilePalette.Find(area.tileKind);
            if (tile == null)
            {
                return;
            }

            for (var y = area.rect.yMin; y < area.rect.yMax; y++)
            {
                for (var x = area.rect.xMin; x < area.rect.xMax; x++)
                {
                    var cell = new Vector3Int(origin.x + x, origin.y + y, 0);
                    if (area.blocksMovement)
                    {
                        collisionTilemap.SetTile(cell, tile);
                    }
                    else if (area.tileKind == FR_MapTileKind.TallGrass)
                    {
                        // 草むらは通行可能な演出なので、Detail層へ置く。
                        detailTilemap.SetTile(cell, tile);
                    }
                    else
                    {
                        groundTilemap.SetTile(cell, tile);
                    }
                }
            }
        }

        private void SpawnObjects()
        {
            var resolver = new FR_MapPlacementResolver();
            var placements = resolver.Resolve(mapDefinition.ToCoreDefinition(), spawnTable.ToCoreDefinitions(), placementSeed);

            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var prefab = spawnTable.FindPrefab(placement.ObjectId);
                if (prefab == null)
                {
                    continue;
                }

                var gridPosition = new Vector2Int(placement.GridPosition.X, placement.GridPosition.Y);
                var footprint = new Vector2Int(placement.FootprintSize.X, placement.FootprintSize.Y);
                var instance = Instantiate(prefab, GridToWorldCenter(gridPosition, footprint), Quaternion.identity, objectRoot);
                instance.name = $"{placement.ObjectId}_{placement.SlotId}";
            }
        }

        private void ClearGeneratedObjects()
        {
            if (objectRoot == null)
            {
                return;
            }

            for (var i = objectRoot.childCount - 1; i >= 0; i--)
            {
                var child = objectRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
