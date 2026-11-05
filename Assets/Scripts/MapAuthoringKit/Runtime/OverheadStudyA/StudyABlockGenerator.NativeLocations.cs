using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    [Serializable]
    public sealed class StudyANativeLocationCatalog
    {
        public string Id = "native-locations-v1";
        public int Version = 1;
        public int PixelsPerUnit = 64;
        public StudyANativeLocationDefinition[] Locations = Array.Empty<StudyANativeLocationDefinition>();
        public StudyANativeSharedEdgeMatch[] SharedEdgeMatches = Array.Empty<StudyANativeSharedEdgeMatch>();
    }

    [Serializable]
    public sealed class StudyANativeLocationDefinition
    {
        public string Id = string.Empty;
        public string PrefabPath = string.Empty;
        public string GroundPath = string.Empty;
        public Vector2Int ExtentPx;
        public Vector2 SpawnPx;
        public string PrefabSHA256 = string.Empty;
        public string GroundSHA256 = string.Empty;
        public StudyANativeLocationPort[] Ports = Array.Empty<StudyANativeLocationPort>();
        public string[] SourceOwnerIds = Array.Empty<string>();
        public StudyANativeFootUnit[] FootUnits = Array.Empty<StudyANativeFootUnit>();
    }

    [Serializable]
    public sealed class StudyANativeFootUnit
    {
        public string OwnerId = string.Empty;
        public string PartPath = string.Empty;
        public int TieOrder;
    }

    [Serializable]
    public sealed class StudyANativeLocationPort
    {
        public string Id = string.Empty;
        public StudyAPortDirection Direction;
        // Native boundary point, not the legacy interior cell centre. No +one-cell offset.
        public Vector2Int AnchorPx;
        public int AlongMinOffsetPx;
        // Art/entry reservation; it does not narrow the open shared floor into a physics gate.
        public int MouthWidthPx;
        public StudyATerrain Terrain = StudyATerrain.Soil;
        public string CompatibilityKey = string.Empty;
        public int Height;
    }

    [Serializable]
    public sealed class StudyANativeSharedEdgeMatch
    {
        public string FromLocationId = string.Empty;
        public string ToLocationId = string.Empty;
        public StudyAPortDirection FromDirection;
        public int FromAlongMinPx;
        public int ToAlongMinPx;
        public int LengthPx;
    }

    [Serializable]
    public sealed class StudyANativePlacedLocation
    {
        public string InstanceId = string.Empty;
        public string LocationId = string.Empty;
        public Vector2Int OriginPx;
        public Vector2Int ExtentPx;
    }

    [Serializable]
    public sealed class StudyANativeLocationConnection
    {
        public int FromBlock;
        public string FromPortId = string.Empty;
        public int ToBlock;
        public string ToPortId = string.Empty;
    }

    [Serializable]
    public sealed class StudyANativeLocationAssembly
    {
        public int Seed;
        public int CatalogVersion;
        public List<StudyANativePlacedLocation> Blocks = new List<StudyANativePlacedLocation>();
        public List<StudyANativeLocationConnection> Connections = new List<StudyANativeLocationConnection>();
        public RectInt NormalizedBoundsPx;
        // Canonical normalized geometry/source guards: excludes seed, search order and instance IDs.
        public string ContentHash = string.Empty;
        public string FixedFullCatalogLayoutQualification = string.Empty;
    }

    public static partial class StudyABlockGenerator
    {
        public static StudyANativeLocationAssembly GenerateNativeAssembly(StudyANativeLocationCatalog catalog,
            int seed, int minBlocks = 2, int maxBlocks = 4, int requestedBlockCount = 0)
        {
            var errors = NativeCatalogErrors(catalog);
            if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors), nameof(catalog));
            if (minBlocks < 2 || maxBlocks > 4 || minBlocks > maxBlocks)
                throw new ArgumentException("Native block count range must be between2 and4.");
            if (requestedBlockCount != 0 && (requestedBlockCount < minBlocks || requestedBlockCount > maxBlocks))
                throw new ArgumentException("Requested native block count is outside the selected range.");
            var rng = new NativeRandom(catalog.Id, catalog.Version, seed);
            var target = requestedBlockCount == 0 ? minBlocks + rng.Next(maxBlocks - minBlocks + 1) : requestedBlockCount;
            if (catalog.Locations.Length < target)
                throw new ArgumentException("Catalog cannot supply the requested unique native block count.");
            var starts = catalog.Locations.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            NativeShuffle(starts, rng);
            var budget = 4096;
            foreach (var start in starts)
            {
                var initial = new StudyANativeLocationAssembly { Seed = seed, CatalogVersion = catalog.Version };
                initial.Blocks.Add(new StudyANativePlacedLocation { InstanceId = start.Id, LocationId = start.Id,
                    OriginPx = Vector2Int.zero, ExtentPx = start.ExtentPx });
                StudyANativeLocationAssembly result;
                if (!NativeGrow(catalog, initial, target, rng, ref budget, out result)) continue;
                var bounds = NativeBounds(result.Blocks);
                foreach (var block in result.Blocks)
                    block.OriginPx = new Vector2Int(checked(block.OriginPx.x - bounds.xMin), checked(block.OriginPx.y - bounds.yMin));
                result.NormalizedBoundsPx = new RectInt(0, 0, bounds.width, bounds.height);
                var finalErrors = ValidateNativeAssembly(catalog, result);
                if (finalErrors.Count != 0) throw new ArgumentException("Generated native assembly is invalid: " + string.Join("; ", finalErrors));
                result.ContentHash = NativeContentHash(catalog, result);
                if (result.Blocks.Count == catalog.Locations.Length)
                    result.FixedFullCatalogLayoutQualification = "All catalog locations selected. For the certified A-B-C-D chain the normalized full layout is fixed; seed/search order is not proof of random geometry. Compare canonical ContentHash across outputs.";
                return result;
            }
            throw new ArgumentException("No compatible native assembly reaches requested block count " + target +
                (budget <= 0 ? " within the bounded search." : "."));
        }

        public static bool TryConnectNative(StudyANativeLocationCatalog catalog, StudyANativeLocationAssembly existing,
            int existingBlockIndex, string existingPortId, string candidateLocationId, string candidatePortId,
            out StudyANativePlacedLocation placement, out string reason)
        {
            placement = null;
            var errors = ValidateNativeAssembly(catalog, existing);
            if (errors.Count != 0) { reason = string.Join("; ", errors); return false; }
            if (existingBlockIndex < 0 || existingBlockIndex >= existing.Blocks.Count)
            { reason = "Existing native block index is invalid."; return false; }
            var definitions = catalog.Locations.ToDictionary(x => x.Id, StringComparer.Ordinal);
            StudyANativeLocationDefinition candidate;
            if (!definitions.TryGetValue(candidateLocationId ?? string.Empty, out candidate))
            { reason = "Candidate native location ID is not in catalog."; return false; }
            if (existing.Blocks.Any(x => x.LocationId == candidateLocationId))
            { reason = "Duplicate native location instances are not supported; source owner IDs would duplicate."; return false; }
            var source = existing.Blocks[existingBlockIndex];
            var a = definitions[source.LocationId].Ports.FirstOrDefault(x => x.Id == existingPortId);
            var b = candidate.Ports.FirstOrDefault(x => x.Id == candidatePortId);
            if (a == null || b == null) { reason = "Native port ID is missing."; return false; }
            if (!NativePortsCompatible(a, b, out reason)) return false;
            if (NativePortUsed(existing, existingBlockIndex, existingPortId))
            { reason = "Existing native port is already used."; return false; }
            try
            {
                var origin = new Vector2Int(checked(source.OriginPx.x + a.AnchorPx.x - b.AnchorPx.x),
                    checked(source.OriginPx.y + a.AnchorPx.y - b.AnchorPx.y));
                var proposed = new StudyANativePlacedLocation { InstanceId = candidate.Id, LocationId = candidate.Id,
                    OriginPx = origin, ExtentPx = candidate.ExtentPx };
                var probe = NativeCopy(existing);
                probe.Blocks.Add(proposed);
                probe.Connections.Add(new StudyANativeLocationConnection { FromBlock = existingBlockIndex,
                    FromPortId = existingPortId, ToBlock = probe.Blocks.Count - 1, ToPortId = candidatePortId });
                errors = ValidateNativeAssembly(catalog, probe);
                if (errors.Count != 0) { reason = string.Join("; ", errors); return false; }
                placement = proposed; reason = string.Empty; return true;
            }
            catch (OverflowException) { reason = "Native coordinate overflow."; return false; }
        }

        // Derived report hash/bounds are not prerequisites for validating intermediate/manual placements.
        public static List<string> ValidateNativeAssembly(StudyANativeLocationCatalog catalog, StudyANativeLocationAssembly assembly)
        {
            var errors = NativeCatalogErrors(catalog);
            if (errors.Count != 0) return errors;
            if (assembly == null || assembly.Blocks == null || assembly.Connections == null || assembly.Blocks.Count == 0 || assembly.Blocks.Count > 4)
                return new List<string> { "Native assembly needs one tofour actual blocks and explicit connections." };
            var definitions = catalog.Locations.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var instances = new HashSet<string>(StringComparer.Ordinal);
            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var block in assembly.Blocks)
            {
                StudyANativeLocationDefinition definition;
                if (block == null || !NativeIdentity(block.LocationId) || !NativeIdentity(block.InstanceId) ||
                    !definitions.TryGetValue(block.LocationId, out definition))
                { errors.Add("Native placed location/instance identity is missing."); continue; }
                if (!ids.Add(block.LocationId) || !instances.Add(block.InstanceId)) errors.Add("Duplicate native location/instance identity.");
                if (block.ExtentPx != definition.ExtentPx) errors.Add("Native placement extent differs from catalog.");
                foreach (var owner in definition.SourceOwnerIds ?? Array.Empty<string>())
                    if (!NativeIdentity(owner) || !owners.Add(owner)) errors.Add("Duplicate/invalid source owner ID in selected native locations.");
            }
            if (errors.Count != 0) return errors.Distinct().ToList();
            try { NativeBounds(assembly.Blocks); }
            catch (OverflowException) { return new List<string> { "Native bounds coordinate overflow." }; }
            errors.AddRange(NativeContactErrors(catalog, assembly.Blocks));
            var used = new HashSet<string>(StringComparer.Ordinal);
            var adjacency = new List<int>[assembly.Blocks.Count];
            for (var i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            foreach (var connection in assembly.Connections)
            {
                if (connection == null || connection.FromBlock < 0 || connection.ToBlock < 0 ||
                    connection.FromBlock >= assembly.Blocks.Count || connection.ToBlock >= assembly.Blocks.Count || connection.FromBlock == connection.ToBlock)
                { errors.Add("Native connection block index is invalid."); continue; }
                var from = assembly.Blocks[connection.FromBlock]; var to = assembly.Blocks[connection.ToBlock];
                var a = definitions[from.LocationId].Ports.FirstOrDefault(x => x.Id == connection.FromPortId);
                var b = definitions[to.LocationId].Ports.FirstOrDefault(x => x.Id == connection.ToPortId);
                if (a == null || b == null) { errors.Add("Native connection port is missing."); continue; }
                string why;
                if (!NativePortsCompatible(a, b, out why)) { errors.Add(why); continue; }
                if (!used.Add(connection.FromBlock + ":" + a.Id) || !used.Add(connection.ToBlock + ":" + b.Id)) errors.Add("Native port is used more than once.");
                if ((long)from.OriginPx.x + a.AnchorPx.x != (long)to.OriginPx.x + b.AnchorPx.x ||
                    (long)from.OriginPx.y + a.AnchorPx.y != (long)to.OriginPx.y + b.AnchorPx.y)
                { errors.Add("Native edge anchors do not align exactly."); continue; }
                NativeContact contact;
                if (!NativeTouch(from, to, out contact) || contact.Direction != a.Direction)
                { errors.Add("Native declared ports do not connect facing floor edges."); continue; }
                var start = NativeAlongOrigin(from, a.Direction) + NativeAnchorAlong(a) + a.AlongMinOffsetPx;
                if (start < contact.Min || start + a.MouthWidthPx > (long)contact.Min + contact.Length)
                { errors.Add("Native mouth width is outside the actual shared floor edge."); continue; }
                adjacency[connection.FromBlock].Add(connection.ToBlock); adjacency[connection.ToBlock].Add(connection.FromBlock);
            }
            var reached = new HashSet<int> { 0 }; var pending = new Queue<int>(); pending.Enqueue(0);
            while (pending.Count != 0)
                foreach (var next in adjacency[pending.Dequeue()]) if (reached.Add(next)) pending.Enqueue(next);
            if (reached.Count != assembly.Blocks.Count) errors.Add("Native declared connection graph is disconnected.");
            return errors.Distinct().ToList();
        }

        private static List<string> NativeCatalogErrors(StudyANativeLocationCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null || !NativeIdentity(catalog.Id) || catalog.Version <= 0 || catalog.PixelsPerUnit != 64 ||
                catalog.Locations == null || catalog.Locations.Length == 0 || catalog.SharedEdgeMatches == null)
                return new List<string> { "Native catalog requires stable ID/version, PPU64 and explicit locations/shared matches." };
            var definitions = new Dictionary<string, StudyANativeLocationDefinition>(StringComparer.Ordinal);
            var allSourceOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in catalog.Locations)
            {
                if (d == null || !NativeIdentity(d.Id) || definitions.ContainsKey(d.Id))
                { errors.Add("Native catalog location ID is missing/duplicate."); continue; }
                definitions.Add(d.Id, d);
                var localOwners = new HashSet<string>(StringComparer.Ordinal);
                if (d.SourceOwnerIds == null) errors.Add("Native SourceOwnerIds array is null: " + d.Id);
                foreach (var ownerId in d.SourceOwnerIds ?? Array.Empty<string>())
                {
                    if (!NativeIdentity(ownerId)) errors.Add("Native source owner ID is null/empty/invalid: " + d.Id);
                    else if (!localOwners.Add(ownerId) || !allSourceOwners.Add(ownerId))
                        errors.Add("Native source owner ID is duplicated across the catalog: " + ownerId);
                }
                if (d.FootUnits == null) errors.Add("Native FootUnits array is null: " + d.Id);
                var footKeys = new HashSet<Tuple<string, string>>();
                foreach (var foot in d.FootUnits ?? Array.Empty<StudyANativeFootUnit>())
                {
                    if (foot == null) { errors.Add("Native foot entry is null: " + d.Id); continue; }
                    if (!NativeIdentity(foot.OwnerId) || !localOwners.Contains(foot.OwnerId))
                        errors.Add("Native foot owner is not in its location SourceOwnerIds: " + d.Id);
                    if (!NativeFootPath(foot.PartPath)) errors.Add("Native foot path is null/empty/invalid: " + d.Id);
                    if (!footKeys.Add(Tuple.Create(foot.OwnerId, foot.PartPath)))
                        errors.Add("Native owner/foot path pair is duplicated: " + d.Id);
                }
                if (d.ExtentPx.x <= 0 || d.ExtentPx.y <= 0 || d.ExtentPx.x % 64 != 0 || d.ExtentPx.y % 64 != 0)
                    errors.Add("Native extent must be positive PPU64 multiples.");
                if (float.IsNaN(d.SpawnPx.x) || float.IsInfinity(d.SpawnPx.x) || float.IsNaN(d.SpawnPx.y) || float.IsInfinity(d.SpawnPx.y) ||
                    d.SpawnPx.x < 0 || d.SpawnPx.y < 0 || d.SpawnPx.x >= d.ExtentPx.x || d.SpawnPx.y >= d.ExtentPx.y)
                    errors.Add("Native SpawnPx must be inside its extent; physics safety is separately authored.");
                if (d.Ports == null) { errors.Add("Native ports are null."); continue; }
                var ports = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in d.Ports)
                {
                    if (p == null || !NativeIdentity(p.Id) || !ports.Add(p.Id) || !NativeIdentity(p.CompatibilityKey) ||
                        !Enum.IsDefined(typeof(StudyAPortDirection), p.Direction) || !Enum.IsDefined(typeof(StudyATerrain), p.Terrain))
                    { errors.Add("Native port identity/direction/terrain/key is invalid."); continue; }
                    var horizontal = p.Direction == StudyAPortDirection.North || p.Direction == StudyAPortDirection.South;
                    var along = NativeAnchorAlong(p); var limit = horizontal ? d.ExtentPx.x : d.ExtentPx.y;
                    var onEdge = p.Direction == StudyAPortDirection.North ? p.AnchorPx.y == d.ExtentPx.y :
                        p.Direction == StudyAPortDirection.South ? p.AnchorPx.y == 0 :
                        p.Direction == StudyAPortDirection.East ? p.AnchorPx.x == d.ExtentPx.x : p.AnchorPx.x == 0;
                    if (!onEdge || along < 0 || along > limit) errors.Add("Native anchor must be exactly on its declared extent boundary.");
                    var start = (long)along + p.AlongMinOffsetPx;
                    if (p.MouthWidthPx <= 0 || start < 0 || start + p.MouthWidthPx > limit) errors.Add("Native mouth width/band lies outside its edge.");
                }
            }
            if (errors.Count != 0) return errors.Distinct().ToList();
            foreach (var rule in catalog.SharedEdgeMatches)
            {
                StudyANativeLocationDefinition from, to;
                if (rule == null || !definitions.TryGetValue(rule.FromLocationId ?? string.Empty, out from) ||
                    !definitions.TryGetValue(rule.ToLocationId ?? string.Empty, out to) || rule.FromLocationId == rule.ToLocationId ||
                    !Enum.IsDefined(typeof(StudyAPortDirection), rule.FromDirection))
                { errors.Add("Native shared edge rule has invalid locations/direction."); continue; }
                var horizontal = rule.FromDirection == StudyAPortDirection.North || rule.FromDirection == StudyAPortDirection.South;
                var fromLimit = horizontal ? from.ExtentPx.x : from.ExtentPx.y; var toLimit = horizontal ? to.ExtentPx.x : to.ExtentPx.y;
                if (rule.LengthPx <= 0 || rule.FromAlongMinPx < 0 || rule.ToAlongMinPx < 0 ||
                    (long)rule.FromAlongMinPx + rule.LengthPx > fromLimit || (long)rule.ToAlongMinPx + rule.LengthPx > toLimit)
                    errors.Add("Native shared edge match window exceeds location extent.");
            }
            return errors.Distinct().ToList();
        }

        private static bool NativePortsCompatible(StudyANativeLocationPort a, StudyANativeLocationPort b, out string reason)
        {
            if (((int)a.Direction + 2) % 4 != (int)b.Direction) { reason = "Native port direction mismatch."; return false; }
            if (!string.Equals(a.CompatibilityKey, b.CompatibilityKey, StringComparison.Ordinal)) { reason = "Native port compatibility key mismatch."; return false; }
            if (a.Terrain != b.Terrain) { reason = "Native port terrain mismatch."; return false; }
            if (a.MouthWidthPx != b.MouthWidthPx || a.AlongMinOffsetPx != b.AlongMinOffsetPx) { reason = "Native port mouth width/along band mismatch."; return false; }
            if (a.Height != b.Height) { reason = "Native port height mismatch."; return false; }
            reason = string.Empty; return true;
        }

        private sealed class NativeContact { public StudyAPortDirection Direction; public int Min, Length; }
        private static bool NativeTouch(StudyANativePlacedLocation a, StudyANativePlacedLocation b, out NativeContact contact)
        {
            contact = null; long start, end; StudyAPortDirection direction;
            if ((long)a.OriginPx.x + a.ExtentPx.x == b.OriginPx.x || (long)b.OriginPx.x + b.ExtentPx.x == a.OriginPx.x)
            {
                start = Math.Max(a.OriginPx.y, b.OriginPx.y); end = Math.Min((long)a.OriginPx.y + a.ExtentPx.y, (long)b.OriginPx.y + b.ExtentPx.y);
                direction = (long)a.OriginPx.x + a.ExtentPx.x == b.OriginPx.x ? StudyAPortDirection.East : StudyAPortDirection.West;
            }
            else if ((long)a.OriginPx.y + a.ExtentPx.y == b.OriginPx.y || (long)b.OriginPx.y + b.ExtentPx.y == a.OriginPx.y)
            {
                start = Math.Max(a.OriginPx.x, b.OriginPx.x); end = Math.Min((long)a.OriginPx.x + a.ExtentPx.x, (long)b.OriginPx.x + b.ExtentPx.x);
                direction = (long)a.OriginPx.y + a.ExtentPx.y == b.OriginPx.y ? StudyAPortDirection.North : StudyAPortDirection.South;
            }
            else return false;
            if (end <= start) return false;
            contact = new NativeContact { Direction = direction, Min = checked((int)start), Length = checked((int)(end - start)) }; return true;
        }

        private static IEnumerable<string> NativeContactErrors(StudyANativeLocationCatalog catalog, List<StudyANativePlacedLocation> blocks)
        {
            for (var i = 0; i < blocks.Count; i++) for (var j = i + 1; j < blocks.Count; j++)
            {
                var a = blocks[i]; var b = blocks[j];
                if (Math.Min((long)a.OriginPx.x + a.ExtentPx.x, (long)b.OriginPx.x + b.ExtentPx.x) > Math.Max(a.OriginPx.x, b.OriginPx.x) &&
                    Math.Min((long)a.OriginPx.y + a.ExtentPx.y, (long)b.OriginPx.y + b.ExtentPx.y) > Math.Max(a.OriginPx.y, b.OriginPx.y))
                { yield return "Native floor extents overlap: " + a.LocationId + "/" + b.LocationId; continue; }
                NativeContact contact; if (!NativeTouch(a, b, out contact)) continue;
                var windows = new List<Tuple<long, long>>();
                foreach (var rule in catalog.SharedEdgeMatches)
                {
                    var forward = rule.FromLocationId == a.LocationId && rule.ToLocationId == b.LocationId && rule.FromDirection == contact.Direction;
                    var reverse = rule.FromLocationId == b.LocationId && rule.ToLocationId == a.LocationId && ((int)rule.FromDirection + 2) % 4 == (int)contact.Direction;
                    if (!forward && !reverse) continue;
                    var startA = NativeAlongOrigin(a, contact.Direction) + (forward ? rule.FromAlongMinPx : rule.ToAlongMinPx);
                    var startB = NativeAlongOrigin(b, contact.Direction) + (forward ? rule.ToAlongMinPx : rule.FromAlongMinPx);
                    if (startA != startB) continue;
                    var lo = Math.Max(startA, contact.Min); var hi = Math.Min(startA + rule.LengthPx, (long)contact.Min + contact.Length);
                    if (hi > lo) windows.Add(Tuple.Create(lo, hi));
                }
                var cursor = (long)contact.Min;
                foreach (var window in windows.OrderBy(x => x.Item1).ThenBy(x => x.Item2))
                { if (window.Item1 > cursor) break; cursor = Math.Max(cursor, window.Item2); }
                if (cursor < (long)contact.Min + contact.Length)
                    yield return "Native shared edge is not fully covered by a declared match: " + a.LocationId + "/" + b.LocationId;
            }
        }

        private static RectInt NativeBounds(List<StudyANativePlacedLocation> blocks)
        {
            long x = blocks.Min(b => (long)b.OriginPx.x), y = blocks.Min(b => (long)b.OriginPx.y);
            long right = blocks.Max(b => (long)b.OriginPx.x + b.ExtentPx.x), top = blocks.Max(b => (long)b.OriginPx.y + b.ExtentPx.y);
            if (right < int.MinValue || right > int.MaxValue || top < int.MinValue || top > int.MaxValue)
                throw new OverflowException("Native extent endpoint exceeds integer coordinates.");
            return new RectInt(checked((int)x), checked((int)y), checked((int)(right - x)), checked((int)(top - y)));
        }
        private static long NativeAlongOrigin(StudyANativePlacedLocation block, StudyAPortDirection direction) =>
            direction == StudyAPortDirection.East || direction == StudyAPortDirection.West ? (long)block.OriginPx.y : block.OriginPx.x;
        private static int NativeAnchorAlong(StudyANativeLocationPort p) => p.Direction == StudyAPortDirection.East || p.Direction == StudyAPortDirection.West ? p.AnchorPx.y : p.AnchorPx.x;
        private static bool NativeIdentity(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
        private static bool NativeFootPath(string value) => NativeIdentity(value) && !value.StartsWith("/", StringComparison.Ordinal) &&
            !value.EndsWith("/", StringComparison.Ordinal) && value.Split('/').All(part => NativeIdentity(part) && part != "." && part != "..");
        private static bool NativePortUsed(StudyANativeLocationAssembly a, int index, string port) => a.Connections.Any(c =>
            c.FromBlock == index && c.FromPortId == port || c.ToBlock == index && c.ToPortId == port);
        private static StudyANativeLocationAssembly NativeCopy(StudyANativeLocationAssembly a) => new StudyANativeLocationAssembly
        {
            Seed = a.Seed, CatalogVersion = a.CatalogVersion,
            Blocks = a.Blocks.Select(b => new StudyANativePlacedLocation { InstanceId = b.InstanceId, LocationId = b.LocationId, OriginPx = b.OriginPx, ExtentPx = b.ExtentPx }).ToList(),
            Connections = a.Connections.Select(c => new StudyANativeLocationConnection { FromBlock = c.FromBlock, FromPortId = c.FromPortId, ToBlock = c.ToBlock, ToPortId = c.ToPortId }).ToList()
        };
        private sealed class NativeCandidate { public int Index; public string FromPort, ToLocation, ToPort, Key; }
        private static bool NativeGrow(StudyANativeLocationCatalog catalog, StudyANativeLocationAssembly assembly, int target,
            NativeRandom rng, ref int budget, out StudyANativeLocationAssembly result)
        {
            result = null; if (--budget < 0) return false;
            if (assembly.Blocks.Count == target) { result = assembly; return true; }
            var choices = new List<NativeCandidate>(); var defs = catalog.Locations.ToDictionary(d => d.Id, StringComparer.Ordinal);
            for (var i = 0; i < assembly.Blocks.Count; i++)
                foreach (var from in defs[assembly.Blocks[i].LocationId].Ports)
                {
                    if (NativePortUsed(assembly, i, from.Id)) continue;
                    foreach (var to in catalog.Locations.Where(d => !assembly.Blocks.Any(b => b.LocationId == d.Id)))
                        foreach (var port in to.Ports)
                        {
                            string why; if (!NativePortsCompatible(from, port, out why)) continue;
                            choices.Add(new NativeCandidate { Index = i, FromPort = from.Id, ToLocation = to.Id, ToPort = port.Id,
                                Key = assembly.Blocks[i].LocationId + "/" + from.Id + "/" + to.Id + "/" + port.Id });
                        }
                }
            choices = choices.OrderBy(c => c.Key, StringComparer.Ordinal).ToList(); NativeShuffle(choices, rng);
            foreach (var choice in choices)
            {
                StudyANativePlacedLocation placed; string why;
                if (!TryConnectNative(catalog, assembly, choice.Index, choice.FromPort, choice.ToLocation, choice.ToPort, out placed, out why)) continue;
                var next = NativeCopy(assembly); next.Blocks.Add(placed);
                next.Connections.Add(new StudyANativeLocationConnection { FromBlock = choice.Index, FromPortId = choice.FromPort,
                    ToBlock = next.Blocks.Count - 1, ToPortId = choice.ToPort });
                if (NativeGrow(catalog, next, target, rng, ref budget, out result)) return true;
            }
            return false;
        }

        private sealed class NativeRandom
        {
            private uint state;
            public NativeRandom(string id, int version, int seed)
            {
                state = 2166136261u;
                foreach (var b in Encoding.UTF8.GetBytes(id + "|" + version.ToString(CultureInfo.InvariantCulture) + "|" + seed.ToString(CultureInfo.InvariantCulture)))
                    state = unchecked((state ^ b) * 16777619u);
                if (state == 0) state = 0x6d2b79f5u;
            }
            private uint NextUInt() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
            public int Next(int count)
            {
                if (count <= 0) throw new ArgumentException("PRNG bound must be positive.");
                var bound = (uint)count; var threshold = unchecked(0u - bound) % bound; uint n;
                do { n = NextUInt(); } while (n < threshold);
                return (int)(n % bound);
            }
        }
        private static void NativeShuffle<T>(List<T> values, NativeRandom rng)
        { for (var i = values.Count - 1; i > 0; i--) { var j = rng.Next(i + 1); var value = values[i]; values[i] = values[j]; values[j] = value; } }
        private static string NativeContentHash(StudyANativeLocationCatalog catalog, StudyANativeLocationAssembly assembly)
        {
            var text = new StringBuilder();
            foreach (var block in assembly.Blocks.OrderBy(b => b.LocationId, StringComparer.Ordinal))
            {
                NativeHashField(text, block.LocationId);
                var definition = catalog.Locations.First(d => d.Id == block.LocationId);
                NativeHashField(text, definition.PrefabSHA256 ?? string.Empty);
                NativeHashField(text, definition.GroundSHA256 ?? string.Empty);
                text.Append(block.OriginPx.x.ToString(CultureInfo.InvariantCulture)).Append(',').Append(block.OriginPx.y.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(block.ExtentPx.x.ToString(CultureInfo.InvariantCulture)).Append(',').Append(block.ExtentPx.y.ToString(CultureInfo.InvariantCulture)).Append('|');
            }
            var links = new List<string>();
            foreach (var c in assembly.Connections)
            {
                var a = new StringBuilder(); NativeHashField(a, assembly.Blocks[c.FromBlock].LocationId); NativeHashField(a, c.FromPortId);
                var b = new StringBuilder(); NativeHashField(b, assembly.Blocks[c.ToBlock].LocationId); NativeHashField(b, c.ToPortId);
                links.Add(StringComparer.Ordinal.Compare(a.ToString(), b.ToString()) < 0 ? a + ">" + b : b + ">" + a);
            }
            foreach (var link in links.OrderBy(x => x, StringComparer.Ordinal)) NativeHashField(text, link);
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static void NativeHashField(StringBuilder text, string value) => text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
    }
}
