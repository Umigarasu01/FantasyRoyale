using System;
using System.Collections.Generic;

namespace FantasyRoyale.Core.Map
{
    /// <summary>
    /// Seed、スロット、配置候補からマップ上の出現物を決定するCoreロジック。
    /// 将来のCPU/オンライン検証でも同じSeedで再現できるようUnityに依存させない。
    /// </summary>
    public sealed class FR_MapPlacementResolver
    {
        public IReadOnlyList<FR_PlacementResult> Resolve(FR_MapDefinition map, IReadOnlyList<FR_MapObjectDefinition> candidates, int seed)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            var random = new Random(seed);
            var results = new List<FR_PlacementResult>();

            foreach (var slot in map.Slots)
            {
                ResolveSlot(slot, candidates, random, results);
            }

            return results;
        }

        private static void ResolveSlot(
            FR_MapSlot slot,
            IReadOnlyList<FR_MapObjectDefinition> candidates,
            Random random,
            List<FR_PlacementResult> results)
        {
            var occupied = new List<FR_PlacementResult>();
            var attempts = Math.Max(4, slot.MaxSpawnCount * 8);

            for (var spawnIndex = 0; spawnIndex < slot.MaxSpawnCount && attempts > 0; attempts--)
            {
                var objectDefinition = PickCandidate(slot, candidates, random);
                if (objectDefinition == null)
                {
                    return;
                }

                if (spawnIndex > 0 && !objectDefinition.CanShareLargeSlot)
                {
                    continue;
                }

                if (!TryFindPlacement(slot, objectDefinition, occupied, out var localPosition))
                {
                    continue;
                }

                var gridPosition = new FR_IntVector2(
                    slot.GridPosition.X + localPosition.X,
                    slot.GridPosition.Y + localPosition.Y);
                var result = new FR_PlacementResult(slot.SlotId, objectDefinition.ObjectId, gridPosition, objectDefinition.FootprintSize);
                occupied.Add(result);
                results.Add(result);
                spawnIndex++;
            }
        }

        private static FR_MapObjectDefinition PickCandidate(FR_MapSlot slot, IReadOnlyList<FR_MapObjectDefinition> candidates, Random random)
        {
            var totalWeight = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (CanPlaceInSlot(slot, candidates[i]))
                {
                    totalWeight += candidates[i].Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            var roll = random.Next(totalWeight);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!CanPlaceInSlot(slot, candidate))
                {
                    continue;
                }

                roll -= candidate.Weight;
                if (roll < 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool CanPlaceInSlot(FR_MapSlot slot, FR_MapObjectDefinition candidate)
        {
            if (candidate.Weight <= 0 || candidate.Category != slot.Category)
            {
                return false;
            }

            if (candidate.Biome != FR_MapBiome.Any && slot.Biome != FR_MapBiome.Any && candidate.Biome != slot.Biome)
            {
                return false;
            }

            if (candidate.FootprintSize.X > slot.GridSize.X || candidate.FootprintSize.Y > slot.GridSize.Y)
            {
                return false;
            }

            if (candidate.SizeCategory > slot.SizeCategory)
            {
                return false;
            }

            // スロット側にタグ指定がある場合は、候補がその条件を満たすものだけ通す。
            return slot.Tags == FR_MapObjectTag.None || (candidate.Tags & slot.Tags) == slot.Tags;
        }

        private static bool TryFindPlacement(
            FR_MapSlot slot,
            FR_MapObjectDefinition objectDefinition,
            IReadOnlyList<FR_PlacementResult> occupied,
            out FR_IntVector2 localPosition)
        {
            for (var y = 0; y <= slot.GridSize.Y - objectDefinition.FootprintSize.Y; y++)
            {
                for (var x = 0; x <= slot.GridSize.X - objectDefinition.FootprintSize.X; x++)
                {
                    var candidate = new FR_IntVector2(x, y);
                    if (!Overlaps(candidate, objectDefinition.FootprintSize, occupied, slot.GridPosition))
                    {
                        localPosition = candidate;
                        return true;
                    }
                }
            }

            localPosition = default;
            return false;
        }

        private static bool Overlaps(
            FR_IntVector2 localPosition,
            FR_IntVector2 footprintSize,
            IReadOnlyList<FR_PlacementResult> occupied,
            FR_IntVector2 slotOrigin)
        {
            var minX = slotOrigin.X + localPosition.X;
            var minY = slotOrigin.Y + localPosition.Y;
            var maxX = minX + footprintSize.X;
            var maxY = minY + footprintSize.Y;

            for (var i = 0; i < occupied.Count; i++)
            {
                var other = occupied[i];
                var otherMinX = other.GridPosition.X;
                var otherMinY = other.GridPosition.Y;
                var otherMaxX = otherMinX + other.FootprintSize.X;
                var otherMaxY = otherMinY + other.FootprintSize.Y;

                if (minX < otherMaxX && maxX > otherMinX && minY < otherMaxY && maxY > otherMinY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
