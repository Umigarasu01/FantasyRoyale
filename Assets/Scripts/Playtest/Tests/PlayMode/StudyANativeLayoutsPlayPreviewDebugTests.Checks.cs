using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using FantasyRoyale.Playtest.OverheadStudyA;

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    // Additional actual-Play checks. Place is fixture setup; Drive is the parent's
    // explicitly classified virtual InputSystem -> existing Adapter -> Actor loop.
    public sealed partial class StudyANativeLayoutsPlayPreviewDebugTests
    {
        [Serializable] sealed class FullGeometry { public RectInt[] FloorsPx; }
        sealed class FullPassage
        {
            public string Name;
            public Vector2 From, To;
            public FullPassage(string name, Vector2 from, Vector2 to) { Name=name; From=from; To=to; }
        }

        IEnumerator RunFullChecks(StudyABlockMovementDiagnostic d, StudyAIndependentStableDepthSort depth, string recipe)
        {
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(recipe == "E" || recipe == "S0", Is.True);
            var map = d.Actor.gameObject.scene;
            var roots = map.GetRootGameObjects();
            var nav = roots.Single(root => root.name == "MapRoot").GetComponent<StudyAIndependentNavigation>();
            Assert.That(nav, Is.Not.Null);
            Assert.That(nav.Owners.Length, Is.EqualTo(43));
            Assert.That(nav.ActiveOwnerRectangleCount, Is.GreaterThan(0));
            Assert.That(nav.Failure, Is.Empty);
            var bridge = nav.Owners.Single(owner => owner.ObjectId == "bridge_crossing");
            var tree = nav.Owners.Single(owner => owner.ObjectId == "c_tree_northwest_anchor01");
            var animations = roots.SelectMany(root => root.GetComponentsInChildren<StudyAIndependentWaterAnimation>(true))
                .Where(a => a.isActiveAndEnabled).ToArray();
            Assert.That(animations.Length, Is.EqualTo(11), "Original inactive WaterHighlights is excluded.");
            var originalPaused = animations.Select(a => a.Paused).ToArray();
            var originalOwnerStates = nav.Owners.ToDictionary(o => o.ObjectId, o => o.Present);
            var originalFootTransforms = roots.SelectMany(root => root.GetComponentsInChildren<SortingGroup>(true))
                .Where(g => g.transform != d.Actor.transform).ToDictionary(g => g, g => FullLocalTransform(g.transform));
            Assert.That(bridge.Present && tree.Present, Is.True);
            Assert.That(d.UseHumanInput && !d.AutoTraverse && d.Running, Is.True);
            report.Facts.Add("Fixture placement changes only the actual Actor body between probes. Each reported traversal uses Drive and production FixedUpdate/real Physics; no manual Physics simulation.");
            var y = recipe == "E" ? 8.640625f : 4.640625f;
            try
            {
                var passages = recipe == "E"
                    ? new[] { new FullPassage("A-B",new Vector2(11,y),new Vector2(13,y)),
                        new FullPassage("B-D",new Vector2(17,y),new Vector2(19,y)),
                        new FullPassage("D-C",new Vector2(24,7),new Vector2(24,5)) }
                    : new[] { new FullPassage("A-B",new Vector2(11,y),new Vector2(13,y)),
                        new FullPassage("B-C",new Vector2(17,y),new Vector2(19,y)),
                        new FullPassage("C-D",new Vector2(24,7),new Vector2(24,9)) };
                foreach (var passage in passages) yield return FullCrossBothWays(d, passage);
                var grass = recipe == "E"
                    ? new FullPassage("B-C-shared-grass",new Vector2(17,5),new Vector2(19,5))
                    : new FullPassage("B-D-shared-grass",new Vector2(17,9),new Vector2(19,9));
                yield return FullCrossBothWays(d, grass);
                report.Checks.Add("All three accepted location connections crossed both directions with the real Actor; broad additional grass edge remained open.");

                var crossing = new FullPassage("bridge-present",new Vector2(4.8f,y),new Vector2(10,y));
                yield return FullCrossBothWays(d, crossing);
                yield return Place(d, new Vector2(7.53125f,y));
                yield return null;
                var hero = depth.Observations.Single(o => o.Key == "actor:hero");
                var railBack = depth.Observations.Single(o => o.OwnerId == "bridge_crossing" && o.PartPath.EndsWith("BridgeBackFoot",StringComparison.Ordinal));
                var railFront = depth.Observations.Single(o => o.OwnerId == "bridge_crossing" && o.PartPath.EndsWith("BridgeFrontFoot",StringComparison.Ordinal));
                Assert.That(railBack.AssignedOrder, Is.LessThan(hero.AssignedOrder));
                Assert.That(railFront.AssignedOrder, Is.GreaterThan(hero.AssignedOrder));
                report.Facts.Add($"Bridge rails at Actor {d.Position}: backY={railBack.FootWorldY:F6}/order{railBack.AssignedOrder}, HeroY={hero.FootWorldY:F6}/order{hero.AssignedOrder}, frontY={railFront.FootWorldY:F6}/order{railFront.AssignedOrder}.");
                Capture(d,depth,"bridge-present-actor");
                yield return Place(d, FullSafeSegment(d,new Vector2(4.8f,y),Vector2.right,.6f));
                var bridgeOthers = FullOtherOwners(nav, bridge);
                var presentBodies = FullActiveBodies(bridge).Length;
                Assert.That(bridge.CurrentNavigationRectangles().Length, Is.EqualTo(4));
                bridge.SetPresent(false); Physics2D.SyncTransforms(); yield return null;
                Assert.That(bridge.Failure, Is.Empty);
                Assert.That(bridge.StateChanges, Is.GreaterThan(0));
                Assert.That(bridge.PresentObjects.All(o => !o.activeInHierarchy), Is.True);
                Assert.That(bridge.AbsentObjects.All(o => o.activeInHierarchy), Is.True);
                Assert.That(bridge.CurrentNavigationRectangles().Length, Is.EqualTo(40));
                Assert.That(FullActiveBodies(bridge).Length, Is.EqualTo(40));
                Assert.That(FullOtherOwners(nav,bridge), Is.EqualTo(bridgeOthers));
                var gapBodies = FullActiveBodies(bridge);
                yield return FullBlocked(d,Vector2.right,.65f,gapBodies,"bridge-removed-west-gap");
                Assert.That(nav.IsBlockedPoint(new Vector2(7.5f,y+.15f)), Is.True);
                Capture(d,depth,"bridge-removed-blocked");
                bridge.SetPresent(true); Physics2D.SyncTransforms(); yield return null;
                Assert.That(bridge.Failure, Is.Empty);
                Assert.That(bridge.CurrentNavigationRectangles().Length, Is.EqualTo(4));
                Assert.That(FullActiveBodies(bridge).Length, Is.EqualTo(presentBodies));
                Assert.That(FullOtherOwners(nav,bridge), Is.EqualTo(bridgeOthers));
                yield return FullCrossBothWays(d,new FullPassage("bridge-restored",new Vector2(4.8f,y),new Vector2(10,y)));
                report.Checks.Add("Bridge present/restored crossed both directions; absent bridge activates 40 water-gap bodies/Nav rectangles and blocks the real Actor, with unrelated owner states/geometry retained.");

                var riverBodies = roots.SelectMany(root => root.GetComponentsInChildren<Collider2D>(false))
                    .Where(c => FullIsBody(c) && c.name.StartsWith("StaticWater_",StringComparison.Ordinal)).ToArray();
                Assert.That(riverBodies.Length, Is.EqualTo(283));
                var riverY = recipe == "E" ? 6f : 2f;
                foreach (var side in new[] { -1, 1 })
                {
                    var direction = side < 0 ? Vector2.right : Vector2.left;
                    var candidate = new Vector2(side < 0 ? 4.5f : 10.5f,riverY);
                    var start = FullSafeApproach(d,candidate,direction,riverBodies,2.5f,.4f);
                    yield return Place(d,start);
                    yield return FullBlocked(d,direction,.65f,riverBodies,side < 0 ? "west-river-bank" : "east-river-bank");
                }
                report.Checks.Add("Both river shores stopped the real capsule against the inherited water bodies without penetration.");

                var geometryPath = Path.GetFullPath(Path.Combine(Application.dataPath,"..",Path.GetDirectoryName(d.MapScenePath),"geometry-recipe.json"));
                var geometry = JsonUtility.FromJson<FullGeometry>(File.ReadAllText(geometryPath));
                Assert.That(geometry.FloorsPx.Length, Is.EqualTo(4));
                var walls = roots.SelectMany(root => root.GetComponentsInChildren<Collider2D>(false))
                    .Where(c => FullIsBody(c) && c.name == "NativePerimeter").ToArray();
                Assert.That(walls.Length, Is.EqualTo(recipe == "E" ? 8 : 10));
                foreach (var wall in walls)
                {
                    Vector2 start, direction;
                    Assert.That(FullWallApproach(d,wall,geometry.FloorsPx,out start,out direction), Is.True,
                        "Each saved perimeter slab needs an unobstructed real-capsule approach: "+wall.bounds);
                    yield return Place(d,start);
                    yield return FullBlocked(d,direction,.5f,new[] {wall},"perimeter-"+wall.transform.GetSiblingIndex());
                }
                if (recipe == "E")
                {
                    var capStart = FullSafeApproach(d,new Vector2(18.8f,2.640625f),Vector2.left,walls,2f,.2f);
                    yield return Place(d,capStart);
                    yield return FullBlocked(d,Vector2.left,.5f,walls,"E-C-unused-west-mouth-cap");
                    Assert.That(d.Position.x, Is.GreaterThan(18f));
                    Capture(d,depth,"E-C-west-cap-stops-actor");
                }
                report.Checks.Add("Every saved perimeter slab stopped an actual-input-driven capsule. E also stopped at the unused C west road cap.");

                yield return FullTreeState(d,depth,nav,tree);
                yield return FullDepth(d,depth,tree,recipe);
                yield return FullWater(d,depth,animations,y);

                Assert.That(depth.Failure, Is.Empty);
                Assert.That(depth.Applied, Is.True);
                Assert.That(depth.BoundUnitCount, Is.EqualTo(36));
                foreach (var pair in originalFootTransforms)
                    Assert.That(FullLocalTransform(pair.Key.transform), Is.EqualTo(pair.Value), "Original foot moved: "+pair.Key.name);
                Assert.That(nav.Owners.All(o => o.Present == originalOwnerStates[o.ObjectId]), Is.True);
                report.Facts.Add("Authored 35 map foot transforms retained. Object-owned visibility/body/Nav removal/restoration exercised; autonomous independent-owner movement is not implemented and is not certified by these checks.");
                report.Facts.Add($"End: frame={d.Frame}, fixedFrame={d.FixedFrame}, depthRefresh={depth.RefreshCount}, actualActor={d.Position}, ortho={d.GameplayCamera.orthographicSize}, camera={d.GameplayCamera.transform.position}.");
            }
            finally
            {
                foreach (var owner in nav.Owners) owner.SetPresent(originalOwnerStates[owner.ObjectId]);
                for (var i=0;i<animations.Length;i++) if (animations[i] != null) animations[i].Paused=originalPaused[i];
                Physics2D.SyncTransforms();
            }
        }

        IEnumerator FullCrossBothWays(StudyABlockMovementDiagnostic d, FullPassage passage)
        {
            var direction = (passage.To-passage.From).normalized;
            var length = Vector2.Distance(passage.From,passage.To);
            var start = FullSafeSegment(d,passage.From,direction,length);
            var end = start + direction*length;
            yield return Place(d,start);
            yield return null;
            var before=d.Position;
            var cameraBefore=d.GameplayCamera.transform.position;
            yield return Drive(d,direction,length/d.Actor.MovementSpeed);
            Assert.That(Vector2.Dot(d.Position-before,direction), Is.GreaterThan(length-.25f), passage.Name+" forward blocked");
            Assert.That(Mathf.Abs(Vector2.Dot(d.Position-before,new Vector2(-direction.y,direction.x))), Is.LessThan(.15f));
            report.Facts.Add($"Cross {passage.Name}: from={before}, expected={end}, actual={d.Position}, forward={Vector2.Dot(d.Position-before,direction):F5}.");
            if(passage.Name=="A-B") FullCameraFollow(d,cameraBefore,true);
            before=d.Position;
            cameraBefore=d.GameplayCamera.transform.position;
            yield return Drive(d,-direction,length/d.Actor.MovementSpeed);
            Assert.That(Vector2.Dot(d.Position-before,-direction), Is.GreaterThan(length-.25f), passage.Name+" reverse blocked");
            report.Facts.Add($"Cross {passage.Name} reverse: from={before}, actual={d.Position}, progress={Vector2.Dot(d.Position-before,-direction):F5}.");
            if(passage.Name=="A-B") FullCameraFollow(d,cameraBefore,false);
        }

        void FullCameraFollow(StudyABlockMovementDiagnostic d, Vector3 before, bool forward)
        {
            var camera=d.GameplayCamera;
            var after=camera.transform.position;
            Assert.That(camera.orthographicSize, Is.EqualTo(8f));
            Assert.That(after.z, Is.EqualTo(before.z).Within(.0001f));
            Assert.That((after.x-before.x)*(forward ? 1f : -1f), Is.GreaterThan(1.5f),"Normal A-B camera did not follow input-driven Actor");
            Assert.That(Mathf.Abs(after.x*32f-Mathf.Round(after.x*32f)), Is.LessThan(.001f));
            Assert.That(Mathf.Abs(after.y*32f-Mathf.Round(after.y*32f)), Is.LessThan(.001f));
            report.Facts.Add($"Normal camera A-B {(forward ? "forward" : "reverse")}: actualActor={d.Position}, before={before}, after={after}, deltaX={after.x-before.x:F6}, aspect={camera.aspect:F6}, ortho={camera.orthographicSize}, snap1/32 confirmed.");
            report.Checks.Add("Normal ortho8 camera followed real A-B input movement with unchanged Z and1/32-unit position snap.");
        }

        IEnumerator FullBlocked(StudyABlockMovementDiagnostic d, Vector2 direction, float seconds, Collider2D[] expected, string label)
        {
            var start=d.Position;
            var hit=FullFirstHit(d,start,direction,d.Actor.MovementSpeed*seconds+.3f);
            Assert.That(hit.collider, Is.Not.Null,label+" needs a real expected-body cast");
            Assert.That(expected.Contains(hit.collider), Is.True,label+" would hit a different obstacle first: "+hit.collider.name);
            yield return Drive(d,direction,seconds);
            var progress=Vector2.Dot(d.Position-start,direction);
            Assert.That(progress, Is.GreaterThan(.02f),label+" started already stuck");
            Assert.That(progress, Is.LessThan(d.Actor.MovementSpeed*seconds-.15f),label+" did not block");
            var distances=expected.Where(FullIsBody).Select(c => new { Collider=c, Distance=Physics2D.Distance(d.Actor.FootCollider,c) })
                .Where(item => item.Distance.isValid).OrderBy(item => Mathf.Abs(item.Distance.distance)).ToArray();
            Assert.That(distances.Length, Is.GreaterThan(0));
            var contacted=distances[0]; var contact=contacted.Distance;
            Assert.That(contact.isValid, Is.True);
            Assert.That(contact.distance, Is.InRange(-.03f,.06f),label+" real contact distance");
            Assert.That(FullOverlaps(d,d.Position).All(c => Physics2D.Distance(d.Actor.FootCollider,c).distance >= -.03f), Is.True,label+" penetrated map");
            report.Facts.Add($"Block {label}: start={start}, dir={direction}, end={d.Position}, progress={progress:F6}/{d.Actor.MovementSpeed*seconds:F6} unobstructed, first={hit.collider.name}, castDistance={hit.distance:F6}, finalContact={contacted.Collider.name}, actualCapsuleDistance={contact.distance:F6}, fixedFrame={d.FixedFrame}.");
        }

        IEnumerator FullTreeState(StudyABlockMovementDiagnostic d, StudyAIndependentStableDepthSort depth, StudyAIndependentNavigation nav, StudyAIndependentObjectState tree)
        {
            Assert.That(tree.AbsentObjects.Length, Is.EqualTo(0));
            var others=FullOtherOwners(nav,tree);
            var bodies=FullActiveBodies(tree); Assert.That(bodies.Length, Is.GreaterThan(0));
            var treeNav=tree.CurrentNavigationRectangles(); Assert.That(treeNav.Length, Is.EqualTo(1));
            var navCount=nav.ActiveOwnerRectangleCount;
            var point=treeNav[0].center;
            Assert.That(nav.IsBlockedPoint(point), Is.True);
            var start=FullSafeApproach(d,(Vector2)tree.transform.position-Vector2.up,Vector2.up,bodies,2f,.25f);
            yield return Place(d,start);
            yield return FullBlocked(d,Vector2.up,.4f,bodies,"tree-present-trunk");
            Capture(d,depth,"tree-present-collision");
            yield return Place(d,start);
            tree.SetPresent(false); Physics2D.SyncTransforms(); yield return null;
            Assert.That(tree.Failure, Is.Empty);
            Assert.That(FullActiveBodies(tree).Length, Is.EqualTo(0));
            Assert.That(tree.CurrentNavigationRectangles().Length, Is.EqualTo(0));
            Assert.That(nav.ActiveOwnerRectangleCount, Is.EqualTo(navCount-1));
            Assert.That(nav.IsBlockedPoint(point), Is.False);
            Assert.That(tree.PresentObjects.All(o => !o.activeInHierarchy), Is.True);
            Assert.That(FullOtherOwners(nav,tree), Is.EqualTo(others));
            var before=d.Position;
            yield return Drive(d,Vector2.up,.4f);
            Assert.That(d.Position.y-before.y, Is.GreaterThan(1.75f),"Removed trunk still blocked real actor");
            report.Facts.Add($"Tree {tree.ObjectId} absent: actual travel={d.Position.y-before.y:F6}, Nav={navCount}->{nav.ActiveOwnerRectangleCount}, activeBodies={FullActiveBodies(tree).Length}, unrelated42 unchanged.");
            Capture(d,depth,"tree-removed-actor-through-trunk");
            yield return Place(d,start);
            tree.SetPresent(true); Physics2D.SyncTransforms(); yield return null;
            Assert.That(FullActiveBodies(tree).Length, Is.EqualTo(bodies.Length));
            Assert.That(tree.CurrentNavigationRectangles().SequenceEqual(treeNav), Is.True);
            Assert.That(nav.IsBlockedPoint(point), Is.True);
            Assert.That(FullOtherOwners(nav,tree), Is.EqualTo(others));
            yield return FullBlocked(d,Vector2.up,.4f,bodies,"tree-restored-trunk");
            report.Checks.Add("Independent C tree removal/restoration changes its visuals, real trunk collision and live Nav together; unrelated 42 owners retain state/body/Nav configuration.");
        }

        IEnumerator FullDepth(StudyABlockMovementDiagnostic d, StudyAIndependentStableDepthSort depth, StudyAIndependentObjectState tree, string recipe)
        {
            var foot=(Vector2)tree.transform.Find("PresentOwned/VisualsFoot").position;
            var from=new Vector2(20.1f,foot.y-.6f);
            var start=FullSafeSegment(d,from,Vector2.up,1.2f,.2f);
            yield return Place(d,start); yield return null;
            var frontHero=depth.Observations.Single(o => o.Key=="actor:hero");
            var frontTree=depth.Observations.Single(o => o.OwnerId==tree.ObjectId);
            Assert.That(frontHero.FootWorldY, Is.LessThan(frontTree.FootWorldY));
            Assert.That(frontHero.AssignedOrder, Is.GreaterThan(frontTree.AssignedOrder));
            var frontPixels=FullHeroControl(d,depth,"hero-front-tree");
            yield return Drive(d,Vector2.up,1.2f/d.Actor.MovementSpeed); yield return null;
            var backHero=depth.Observations.Single(o => o.Key=="actor:hero");
            var backTree=depth.Observations.Single(o => o.OwnerId==tree.ObjectId);
            Assert.That(backHero.FootWorldY, Is.GreaterThan(backTree.FootWorldY));
            Assert.That(backHero.AssignedOrder, Is.LessThan(backTree.AssignedOrder));
            var backPixels=FullHeroControl(d,depth,"hero-back-tree");
            Assert.That(frontPixels, Is.GreaterThan(0),"Front Hero was not drawn by real camera");
            Assert.That(backPixels, Is.LessThan(frontPixels),"Back tree failed to occlude more of actual Hero than front tree frame");
            report.Facts.Add($"Depth C tree: front HeroY/order={frontHero.FootWorldY:F6}/{frontHero.AssignedOrder}, tree={frontTree.FootWorldY:F6}/{frontTree.AssignedOrder}; back Hero={backHero.FootWorldY:F6}/{backHero.AssignedOrder}, tree={backTree.FootWorldY:F6}/{backTree.AssignedOrder}; same-frame Hero-on/off changedPixels front={frontPixels}, back={backPixels}. GPU PNG comparison is separate from sort-number assertions.");
            report.Checks.Add("Hero crossed the C tree foot Y through real movement, changed front/back ranks, and showed greater tree occlusion in paired same-frame ordinary-camera Hero-on/off GPU captures.");
        }

        int FullHeroControl(StudyABlockMovementDiagnostic d, StudyAIndependentStableDepthSort depth, string label)
        {
            var renderers=d.Actor.GetComponentsInChildren<SpriteRenderer>(true);
            var enabled=renderers.Select(r => r.enabled).ToArray();
            var frame=d.Frame; var fixedFrame=d.FixedFrame;
            Capture(d,depth,label);
            try { foreach(var renderer in renderers) renderer.enabled=false; Capture(d,depth,label+"-hero-hidden-control"); }
            finally { for(var i=0;i<renderers.Length;i++) renderers[i].enabled=enabled[i]; }
            Assert.That(d.Frame, Is.EqualTo(frame)); Assert.That(d.FixedFrame, Is.EqualTo(fixedFrame));
            return FullChangedPixels(label,label+"-hero-hidden-control");
        }

        IEnumerator FullWater(StudyABlockMovementDiagnostic d, StudyAIndependentStableDepthSort depth, StudyAIndependentWaterAnimation[] animations, float y)
        {
            yield return Place(d,new Vector2(7.53125f,y)); yield return null;
            Assert.That(animations.All(a => !a.Paused && a.Failure.Length==0), Is.True);
            var before=animations.Select(a => a.FrameChanges).ToArray();
            var loops=animations.Select(a => a.CompletedLoops).ToArray();
            Capture(d,depth,"water-phase-before");
            yield return new WaitForSeconds(1.67f);
            Capture(d,depth,"water-phase-after");
            var changed=FullChangedPixels("water-phase-before","water-phase-after");
            Assert.That(changed, Is.GreaterThan(0),"Active water animation did not change actual GPU pixels");
            for(var i=0;i<animations.Length;i++)
            {
                var a=animations[i];
                Assert.That(a.FrameChanges, Is.GreaterThan(before[i]),a.name);
                Assert.That(a.CompletedLoops, Is.GreaterThan(loops[i]),a.name+" needs >=1 real time loop");
                Assert.That(a.Surface.sprite, Is.SameAs(a.Frames[a.FrameIndex]));
                report.Facts.Add($"Animation {a.name}: fps={a.FramesPerSecond}, frames={a.Frames.Length}, FrameChanges={before[i]}->{a.FrameChanges}, loops={loops[i]}->{a.CompletedLoops}, index={a.FrameIndex}, sprite={a.Surface.sprite.name}.");
            }
            foreach(var paused in animations)
            {
                var changes=paused.FrameChanges; var index=paused.FrameIndex; var sprite=paused.Surface.sprite;
                var others=animations.Where(a => a!=paused).ToArray(); var otherBefore=others.Select(a => a.FrameChanges).ToArray();
                paused.Paused=true;
                try
                {
                    yield return new WaitForSeconds(.37f);
                    Assert.That(paused.FrameChanges, Is.EqualTo(changes),paused.name+" did not pause independently");
                    Assert.That(paused.FrameIndex, Is.EqualTo(index)); Assert.That(paused.Surface.sprite, Is.SameAs(sprite));
                    Assert.That(others.Where((a,i) => a.FrameChanges>otherBefore[i]).Count(), Is.EqualTo(others.Length));
                }
                finally { paused.Paused=false; }
                yield return new WaitForSeconds(.35f);
                Assert.That(paused.FrameChanges, Is.GreaterThan(changes),paused.name+" did not resume");
                report.Facts.Add($"Independent pause/resume {paused.name}: paused index={index}, changes={changes}; resumeChanges={paused.FrameChanges}; other10 advanced during pause.");
            }
            report.Facts.Add($"Water GPU phase changedPixels={changed}; original inactive 64-frame WaterHighlights retained and not included among 11 active animation successes.");
            report.Checks.Add("All 11 active river surface/foam/plant animations advanced and looped in real Play time; each paused/resumed independently while the other ten continued; ordinary-camera GPU phases differ.");
        }

        static bool FullIsBody(Collider2D collider) => collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy && collider.gameObject.layer==LayerMask.NameToLayer("MapCollision");
        static Collider2D[] FullActiveBodies(StudyAIndependentObjectState owner) => owner.GetComponentsInChildren<Collider2D>(false).Where(FullIsBody).ToArray();
        static string FullLocalTransform(Transform t) => t.localPosition.ToString("F8")+"|"+t.localRotation.ToString("F8")+"|"+t.localScale.ToString("F8");
        static string[] FullOtherOwners(StudyAIndependentNavigation nav, StudyAIndependentObjectState excluded)
            => nav.Owners.Where(o => o!=excluded).OrderBy(o => o.ObjectId,StringComparer.Ordinal).Select(o =>
                o.ObjectId+"|"+o.Present+"|"+string.Join(";",o.CurrentNavigationRectangles().Select(r => r.ToString("F8")))+"|"+
                string.Join(";",o.GetComponentsInChildren<Collider2D>(true).Select(c => c.name+":"+c.enabled+":"+c.gameObject.activeInHierarchy+":"+c.bounds.ToString("F8")))+"|"+
                string.Join(";",o.GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.name+":"+r.enabled+":"+r.gameObject.activeInHierarchy))).ToArray();

        static Vector2 FullCapsuleCentre(StudyABlockMovementDiagnostic d, Vector2 body) => body+d.Actor.FootCollider.offset;
        static Collider2D[] FullOverlaps(StudyABlockMovementDiagnostic d, Vector2 body)
            => Physics2D.OverlapCapsuleAll(FullCapsuleCentre(d,body),d.Actor.FootCollider.size,d.Actor.FootCollider.direction,0f,1<<LayerMask.NameToLayer("MapCollision")).Where(FullIsBody).ToArray();
        static RaycastHit2D FullFirstHit(StudyABlockMovementDiagnostic d, Vector2 body, Vector2 direction, float distance)
            => Physics2D.CapsuleCastAll(FullCapsuleCentre(d,body),d.Actor.FootCollider.size,d.Actor.FootCollider.direction,0f,direction,distance,1<<LayerMask.NameToLayer("MapCollision"))
                .Where(h => FullIsBody(h.collider)).OrderBy(h => h.distance).FirstOrDefault();
        static Vector2 FullSafeSegment(StudyABlockMovementDiagnostic d, Vector2 candidate, Vector2 direction, float distance, float lateral=.3f)
        {
            var tangent=new Vector2(-direction.y,direction.x);
            foreach(var shift in new[]{0f,.1f,-.1f,.2f,-.2f,.3f,-.3f})
            {
                if(Mathf.Abs(shift)>lateral+.001f) continue;
                var start=candidate+tangent*shift;
                if(FullOverlaps(d,start).Length==0 && FullOverlaps(d,start+direction*distance).Length==0 && FullFirstHit(d,start,direction,distance).collider==null) return start;
            }
            Assert.Fail("No actual-capsule clear segment near "+candidate+" toward "+direction+" length "+distance);
            return candidate;
        }
        static Vector2 FullSafeApproach(StudyABlockMovementDiagnostic d, Vector2 candidate, Vector2 direction, Collider2D[] targets, float distance, float lateral)
        {
            var tangent=new Vector2(-direction.y,direction.x);
            foreach(var shift in new[]{0f,.1f,-.1f,.2f,-.2f,.3f,-.3f,.4f,-.4f})
            {
                if(Mathf.Abs(shift)>lateral+.001f) continue;
                var start=candidate+tangent*shift;
                if(FullOverlaps(d,start).Length!=0) continue;
                var hit=FullFirstHit(d,start,direction,distance);
                if(hit.collider!=null && targets.Contains(hit.collider) && hit.distance>.04f) return start;
            }
            Assert.Fail("No free real-capsule approach to required target near "+candidate);
            return candidate;
        }
        static bool FullWallApproach(StudyABlockMovementDiagnostic d, Collider2D wall, RectInt[] floors, out Vector2 start, out Vector2 direction)
        {
            var b=wall.bounds;
            foreach(var dir in new[]{Vector2.up,Vector2.down,Vector2.right,Vector2.left})
            {
                for(var i=1;i<20;i++)
                {
                    var t=i/20f;
                    var candidate=dir==Vector2.up ? new Vector2(Mathf.Lerp(b.min.x,b.max.x,t),b.min.y-.8f)
                        : dir==Vector2.down ? new Vector2(Mathf.Lerp(b.min.x,b.max.x,t),b.max.y+.8f)
                        : dir==Vector2.right ? new Vector2(b.min.x-.8f,Mathf.Lerp(b.min.y,b.max.y,t))
                        : new Vector2(b.max.x+.8f,Mathf.Lerp(b.min.y,b.max.y,t));
                    var centre=FullCapsuleCentre(d,candidate); var half=d.Actor.FootCollider.size*.5f;
                    var points=new[]{centre,centre+half,centre-half,centre+new Vector2(half.x,-half.y),centre+new Vector2(-half.x,half.y)};
                    if(!points.All(point => floors.Any(f => new Rect(f.x/64f,f.y/64f,f.width/64f,f.height/64f).Contains(point)))) continue;
                    if(FullOverlaps(d,candidate).Length!=0) continue;
                    var hit=FullFirstHit(d,candidate,dir,2.8f);
                    if(hit.collider==wall && hit.distance>.04f) {start=candidate;direction=dir;return true;}
                }
            }
            start=direction=Vector2.zero; return false;
        }
        int FullChangedPixels(string labelA, string labelB)
        {
            var a=new Texture2D(2,2,TextureFormat.RGBA32,false); var b=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                Assert.That(a.LoadImage(File.ReadAllBytes(Path.Combine(evidencePath,report.Recipe+"-"+labelA+"-ordinary-camera.png"))), Is.True);
                Assert.That(b.LoadImage(File.ReadAllBytes(Path.Combine(evidencePath,report.Recipe+"-"+labelB+"-ordinary-camera.png"))), Is.True);
                Assert.That(a.width, Is.EqualTo(b.width)); Assert.That(a.height, Is.EqualTo(b.height));
                var pa=a.GetPixels32(); var pb=b.GetPixels32(); var count=0;
                for(var i=0;i<pa.Length;i++) if(!pa[i].Equals(pb[i])) count++;
                return count;
            }
            finally { UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b); }
        }
    }
}
