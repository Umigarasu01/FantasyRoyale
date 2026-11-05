using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>One opt-in art pass on generated clones. Native inputs and collider recipes remain unchanged.</summary>
    public static class StudyAArtFinishLayout
    {
        private sealed class Move
        {
            public SpriteRenderer Renderer;
            public StudyAStarterProp Prop;
            public Vector3 Before, After;
            public bool Facility;
            public Rect BeforeVisual;
            public Vector2 Delta => (Vector2)(After - Before);
            public Rect Foot => Shift(Prop.Metrics.LocalFootprint, After);
            public Rect Visual => Shift(BeforeVisual, Delta);
        }

        public static string[] Apply(StudyABlockAssembly assembly, Transform obstacles,
            Dictionary<string, StudyAStarterProp> props, Material material, StudyABridgeRiverFinePlan fine,
            StudyASoilMask roadMask = null)
        {
            if (assembly == null || obstacles == null || props == null || material == null || fine == null || roadMask == null ||
                EditorUtility.IsPersistent(obstacles) || !obstacles.gameObject.scene.IsValid() || !string.IsNullOrEmpty(obstacles.gameObject.scene.path))
                throw new ArgumentException("Art finish requires a new scene clone, approved props, fine water and the actual soil mask.");
            if (assembly.Blocks.Count(b => b.Definition.Kind == StudyABlockKind.Plaza) != 1 ||
                assembly.Blocks.Count(b => b.Definition.Kind == StudyABlockKind.ForestPath) != 1)
                throw new ArgumentException("Art finish requires the reviewed Plaza and ForestPath clones.");
            var moves = new List<Move>();
            Add(StudyABlockKind.Plaza, "fountain", new Vector2(5.75f, 7.75f), new Vector2(5.25f, 7.5f), true);
            Add(StudyABlockKind.Plaza, "shop", new Vector2(9f, 8.125f), new Vector2(9f, 8.375f), true);
            Add(StudyABlockKind.Plaza, "treeMedium", new Vector2(11.25f, 10.875f), new Vector2(11.25f, 11.25f));
            // Keep the north protrusions at x13/x18; pull one crown back to make a recess between them.
            Add(StudyABlockKind.ForestPath, "treeLarge", new Vector2(4.125f, 8f), new Vector2(4.875f, 8.875f));
            // Southern crowns retain their original Y and road clearance, with a small unequal horizontal clump.
            Add(StudyABlockKind.ForestPath, "treeMedium", new Vector2(2.5f, .0625f), new Vector2(2.875f, .0625f));
            Add(StudyABlockKind.ForestPath, "treeLarge", new Vector2(3.625f, .125f), new Vector2(3.875f, .125f));
            Add(StudyABlockKind.ForestPath, "treeMedium", new Vector2(6.25f, .25f), new Vector2(5.5f, .25f));
            Add(StudyABlockKind.ForestPath, "treeLarge", new Vector2(7.25f, .125f), new Vector2(7.625f, .125f));

            Physics2D.SyncTransforms();
            var bodies = obstacles.GetComponentsInChildren<BoxCollider2D>(true).Where(c => c.enabled && !c.isTrigger)
                .Select(c => Tuple.Create(c, BoundsRect(c.bounds))).ToArray();
            Rect FutureBody(Tuple<BoxCollider2D, Rect> body)
            {
                var move = moves.SingleOrDefault(m => body.Item1.transform.IsChildOf(m.Renderer.transform));
                return move == null ? body.Item2 : Shift(body.Item2, move.Delta);
            }
            var futureBodies = bodies.Select(b => Tuple.Create(b.Item1, FutureBody(b))).ToArray();
            foreach (var move in moves)
            {
                if (fine.WaterIntersectsRect(move.Foot) || fine.DeckBounds.Any(d => Overlaps(d, move.Foot)))
                    throw new InvalidDataException("Art foot overlaps water/deck: " + move.Renderer.name);
                if (move.Facility && (!roadMask.ContainsRect(move.Foot) || !fine.IsFreeRect(move.Foot)))
                    throw new InvalidDataException("Moved facility foot leaves actual soil/free ground: " + move.Renderer.name);
                if (assembly.RoadCells.Any(c => Overlaps(move.Facility ? move.Foot : move.Visual, new Rect((Vector2)c, Vector2.one))))
                    throw new InvalidDataException("Art pass obscures the protected three-unit lane: " + move.Renderer.name);
                foreach (var body in futureBodies.Where(b => b.Item1.transform.IsChildOf(move.Renderer.transform)))
                {
                    if ((move.Facility && !fine.IsFreeRect(body.Item2)) || fine.CollisionIntersectsRect(body.Item2) || fine.DeckBounds.Any(d => Overlaps(d, body.Item2)) ||
                        assembly.RoadCells.Any(c => Overlaps(body.Item2, new Rect((Vector2)c, Vector2.one))) ||
                        Overlaps(body.Item2, ActorRect(assembly.Spawn)))
                        throw new InvalidDataException("Moved body blocks protected ground: " + move.Renderer.name);
                    if (futureBodies.Any(other => !other.Item1.transform.IsChildOf(move.Renderer.transform) && Overlaps(body.Item2, other.Item2)))
                        throw new InvalidDataException("Moved body intersects another native body: " + move.Renderer.name);
                    foreach (var route in assembly.MainRoutes)
                        for (var i = 1; i < route.Count; i++)
                            if (HitsActorSweep(body.Item2, route[i - 1], route[i]))
                                throw new InvalidDataException("Moved body blocks the actual Actor route: " + move.Renderer.name);
                }
            }
            var allTrees = obstacles.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.sprite == props["treeLarge"].Sprite || r.sprite == props["treeMedium"].Sprite).ToArray();
            foreach (var facility in moves.Where(m => m.Facility))
                foreach (var tree in allTrees)
                {
                    var changed = moves.SingleOrDefault(m => m.Renderer == tree);
                    var prop = tree.sprite == props["treeLarge"].Sprite ? props["treeLarge"] : props["treeMedium"];
                    var visual = changed == null ? Shift(LocalVisual(prop), tree.transform.position) : changed.Visual;
                    if (Overlaps(facility.Visual, visual)) throw new InvalidDataException("A tree obscures the moved facility: " + tree.name);
                }
            // All refusal checks precede mutation. Moving the Sprite root moves its existing CollisionBody with it.
            foreach (var move in moves) move.Renderer.transform.position = move.After;
            Physics2D.SyncTransforms();
            foreach (var body in futureBodies)
                if (!Near(BoundsRect(body.Item1.bounds), body.Item2)) throw new InvalidDataException("Art pass changed an existing collider shape/pose.");
            return moves.Select(m => string.Format(CultureInfo.InvariantCulture,
                "{0}: ({1:R},{2:R})->({3:R},{4:R}); existingBodyCount={5}; colliderRecipeUnchanged=true; nativeReservationShapeUnchanged=true",
                PathOf(m.Renderer.transform, obstacles), m.Before.x, m.Before.y, m.After.x, m.After.y,
                bodies.Count(b => b.Item1.transform.IsChildOf(m.Renderer.transform)))).ToArray();

            void Add(StudyABlockKind kind, string role, Vector2 source, Vector2 target, bool facility = false)
            {
                if (!props.TryGetValue(role, out var prop) || prop.Sprite == null || prop.Metrics == null)
                    throw new ArgumentException("Approved art role is missing: " + role);
                var block = assembly.Blocks.Single(b => b.Definition.Kind == kind);
                var root = obstacles.Find(block.Definition.Id + "_FixedScenery");
                var from = source + (Vector2)block.Origin;
                var candidates = root == null ? Array.Empty<SpriteRenderer>() : root.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r => r.sprite == prop.Sprite && Vector2.Distance(r.transform.position, from) < .0001f).ToArray();
                if (candidates.Length != 1) throw new ArgumentException("Authored art source differs from the reviewed slot: " + kind + "/" + role + "/" + source);
                var renderer = candidates[0];
                if (!renderer.enabled || renderer.drawMode != SpriteDrawMode.Simple || renderer.flipX || renderer.flipY ||
                    renderer.maskInteraction != SpriteMaskInteraction.None || renderer.spriteSortPoint != SpriteSortPoint.Pivot ||
                    renderer.sortingLayerName != "WorldObjects" || renderer.sortingOrder != 0)
                    throw new ArgumentException("Art finish preserves unsupported renderer authoring by refusing it: " + renderer.name);
                for (var t = renderer.transform; t != null; t = t.parent)
                    if (t.localScale != Vector3.one || t.localRotation != Quaternion.identity)
                        throw new ArgumentException("Art moves require unit unrotated source roots.");
                if (renderer.GetComponentsInChildren<Collider2D>(true).Any(c => !(c is BoxCollider2D)))
                    throw new ArgumentException("Art finish supports the unchanged measured Box recipes only.");
                moves.Add(new Move { Renderer = renderer, Prop = prop, Before = renderer.transform.position,
                    After = new Vector3(target.x + block.Origin.x, target.y + block.Origin.y, renderer.transform.position.z),
                    Facility = facility, BeforeVisual = Shift(LocalVisual(prop), renderer.transform.position) });
            }
        }

        private static Rect BoundsRect(Bounds bounds) => new Rect(bounds.min, bounds.size);
        private static Rect LocalVisual(StudyAStarterProp prop)
        {
            // Approved AlphaBounds uses native bottom-origin pixels; transparent FullRect padding is not visible.
            var m = prop.Metrics; var alpha = m.AlphaBounds; var ppu = prop.Sprite.pixelsPerUnit;
            if (ppu != StudyAStarterAssets.PixelsPerUnit || alpha.width <= 0 || alpha.height <= 0 ||
                alpha.xMin < 0 || alpha.yMin < 0 || alpha.xMax > m.NativeSize.x || alpha.yMax > m.NativeSize.y)
                throw new ArgumentException("Art finish requires the approved native opaque bounds.");
            return new Rect(((Vector2)alpha.position - m.PixelPivot) / ppu, (Vector2)alpha.size / ppu);
        }
        private static Rect Shift(Rect rect, Vector2 delta) { rect.position += delta; return rect; }
        private static bool Overlaps(Rect a, Rect b) => a.xMin < b.xMax - .00001f && a.xMax > b.xMin + .00001f && a.yMin < b.yMax - .00001f && a.yMax > b.yMin + .00001f;
        private static Rect ActorRect(Vector2 foot) => new Rect(foot + new Vector2(-.24f, -.055f), new Vector2(.48f, .55f));
        private static bool Near(Rect a, Rect b) => Vector2.Distance(a.position, b.position) < .0001f && Vector2.Distance(a.size, b.size) < .0001f;
        private static string PathOf(Transform child, Transform root)
        { var parts = new List<string>(); for (var t = child; t != null && t != root; t = t.parent) parts.Add(t.name); parts.Reverse(); return string.Join("/", parts); }
        private static bool HitsActorSweep(Rect body, Vector2 a, Vector2 b)
        {
            // Minkowski expansion of the unchanged .48 x .55 / offset .22 Actor; continuous, not point samples.
            var min = body.min - new Vector2(.24f, .495f); var max = body.max + new Vector2(.24f, .055f);
            var delta = b - a; var lo = 0f; var hi = 1f;
            for (var axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .000001f) { if (a[axis] <= min[axis] || a[axis] >= max[axis]) return false; continue; }
                var x = (min[axis] - a[axis]) / delta[axis]; var y = (max[axis] - a[axis]) / delta[axis];
                lo = Mathf.Max(lo, Mathf.Min(x, y)); hi = Mathf.Min(hi, Mathf.Max(x, y)); if (lo >= hi) return false;
            }
            return lo < hi;
        }
    }
}
