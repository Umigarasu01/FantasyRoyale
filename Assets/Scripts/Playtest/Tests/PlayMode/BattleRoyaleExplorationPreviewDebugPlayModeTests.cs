using System.Collections;
using System.Collections.Generic;
using FantasyRoyale.MapAuthoringKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>
    /// 実Playtest Sceneを通して、Reference Map読込、Spawn、移動、Collision、Camera追従を一括確認する。
    /// </summary>
    public sealed class BattleRoyaleExplorationPreviewDebugPlayModeTests : InputTestFixture
    {
        private const string PlaytestScenePath =
            "Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity";

        /// <summary>
        /// 実行経路全体を通し、仮Playerが歩けてMapCollisionを貫通せずCameraが追従することを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator ExplorationPreview_LoadsMovesCollidesAndFollows()
        {
            var loadOperation = SceneManager.LoadSceneAsync(PlaytestScenePath, LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            var bootstrap = Object.FindAnyObjectByType<BattleRoyaleExplorationPreviewDebug>();
            Assert.That(bootstrap, Is.Not.Null);

            var timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!bootstrap.IsReady
                   && string.IsNullOrEmpty(bootstrap.FailureMessage)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(bootstrap.FailureMessage, Is.Empty);
            Assert.That(bootstrap.IsReady, Is.True);
            Assert.That(bootstrap.LoadedMapScene.isLoaded, Is.True);
            Assert.That(bootstrap.PlayerObject, Is.Not.Null);
            Assert.That(bootstrap.PlayerBody.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(bootstrap.PlayerBody.gravityScale, Is.EqualTo(0f));
            Assert.That(bootstrap.PlayerCollider, Is.Not.Null);
            Assert.That(bootstrap.GameplayCamera, Is.Not.Null);
            Assert.That(bootstrap.GameplayCamera.orthographic, Is.True);
            Assert.That(bootstrap.GameplayCamera.orthographicSize,
                Is.EqualTo(bootstrap.CameraOrthographicSize).Within(0.001f));
            Assert.That(bootstrap.CollisionTilemap.GetComponent<TilemapCollider2D>(), Is.Not.Null);
            Assert.That(bootstrap.CollisionTilemap.GetComponent<CompositeCollider2D>(), Is.Not.Null);

            var playerStarts = FindComponentsInScene<MapSocketMarker>(bootstrap.LoadedMapScene)
                .FindAll(marker => marker.SocketKind == MapSocketKind.PlayerStart);
            Assert.That(playerStarts, Has.Count.EqualTo(1));
            Assert.That(
                Vector2.Distance(bootstrap.PlayerBody.position, playerStarts[0].transform.position),
                Is.LessThan(0.01f));

            var clearDirection = FindClearMoveDirection(
                bootstrap.CollisionTilemap,
                bootstrap.PlayerBody.position,
                2);
            Assert.That(clearDirection, Is.Not.EqualTo(Vector2.zero));
            var runtimeMoveAction = bootstrap.RuntimeInputActions.FindAction(
                $"{BattleRoyaleExplorationPreviewDebug.PlayerActionMapName}/"
                + BattleRoyaleExplorationPreviewDebug.MoveActionName,
                true);
            Assert.That(runtimeMoveAction.enabled, Is.True);
            Assert.That(
                HasBinding(runtimeMoveAction, "<Keyboard>/w"),
                Is.True,
                "PlaytestのMove ActionにWASD入力がありません。");

            var movementStart = bootstrap.PlayerBody.position;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            runtimeMoveAction.actionMap.Disable();
            runtimeMoveAction.actionMap.devices = new InputDevice[] { keyboard };
            runtimeMoveAction.actionMap.Enable();
            var moveKey = ToKeyboardKey(clearDirection);
            Press(keyboard[moveKey]);
            yield return null;
            Assert.That(keyboard[moveKey].isPressed, Is.True);
            Assert.That(
                Vector2.Dot(runtimeMoveAction.ReadValue<Vector2>(), clearDirection),
                Is.GreaterThan(0.5f),
                "仮想Keyboard入力をMove Actionが受け取っていません。");
            Assert.That(
                Vector2.Dot(bootstrap.LatestMoveInput, clearDirection),
                Is.GreaterThan(0.5f),
                "Updateから物理移動用の入力値へ反映されていません。");
            for (var frame = 0; frame < 12; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            // 仮想Keyboardは移動経路の確認までに使い、以後は明示的な入力上書きで停止させる。
            // PlayModeのフレーム進行後にTestFixture側のDeviceが再構成されても、解除処理へ依存しないため。
            bootstrap.SetMoveInputOverrideForTests(Vector2.zero);
            yield return new WaitForFixedUpdate();
            var movementDelta = bootstrap.PlayerBody.position - movementStart;
            Assert.That(Vector2.Dot(movementDelta, clearDirection), Is.GreaterThan(0.35f));
            Assert.That(
                Mathf.Abs(Vector2.Dot(movementDelta, new Vector2(-clearDirection.y, clearDirection.x))),
                Is.LessThan(0.12f));

            Assert.That(
                TryFindCollisionApproach(
                    bootstrap.CollisionTilemap,
                    bootstrap.PlayerCollider,
                    out var freePosition,
                    out var blockedPosition,
                    out var towardCollision),
                Is.True);
            bootstrap.PlayerBody.position = freePosition;
            bootstrap.PlayerBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            var collisionStart = bootstrap.PlayerBody.position;
            bootstrap.SetMoveInputOverrideForTests(towardCollision);
            for (var frame = 0; frame < 25; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            bootstrap.SetMoveInputOverrideForTests(Vector2.zero);
            yield return new WaitForFixedUpdate();
            var progressTowardWall = Vector2.Dot(
                bootstrap.PlayerBody.position - collisionStart,
                towardCollision);
            Assert.That(progressTowardWall, Is.GreaterThan(0.02f));
            Assert.That(progressTowardWall, Is.LessThan(0.48f));
            Assert.That(
                Vector2.Dot(blockedPosition - bootstrap.PlayerBody.position, towardCollision),
                Is.GreaterThan(0.45f));

            var compositeCollider = bootstrap.CollisionTilemap.GetComponent<CompositeCollider2D>();
            var colliderDistance = Physics2D.Distance(bootstrap.PlayerCollider, compositeCollider);
            Assert.That(
                colliderDistance.distance,
                Is.GreaterThanOrEqualTo(-0.03f),
                $"Physics接触許容を超えてCollisionへ侵入しました: {colliderDistance.distance:0.0000}");

            bootstrap.PlayerBody.position = new Vector2(0f, 0f);
            bootstrap.PlayerBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            var cameraBefore = bootstrap.GameplayCamera.transform.position;
            bootstrap.PlayerBody.position = new Vector2(3f, 2f);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            var cameraAfter = bootstrap.GameplayCamera.transform.position;
            Assert.That(cameraAfter.x, Is.GreaterThan(cameraBefore.x + 2.5f));
            Assert.That(cameraAfter.y, Is.GreaterThan(cameraBefore.y + 1.5f));
            Assert.That(cameraAfter.z, Is.EqualTo(cameraBefore.z).Within(0.001f));
            Assert.That(
                Mathf.Abs(cameraAfter.x * 32f - Mathf.Round(cameraAfter.x * 32f)),
                Is.LessThan(0.001f));
            Assert.That(
                Mathf.Abs(cameraAfter.y * 32f - Mathf.Round(cameraAfter.y * 32f)),
                Is.LessThan(0.001f));
        }

        /// <summary>
        /// 実Reference Mapと仮Playerが同じ足元Yソート契約を使い、カテゴリ固定順へ戻らないことを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator ExplorationPreview_ConfiguresFootBasedWorldYSorting()
        {
            var loadOperation = SceneManager.LoadSceneAsync(PlaytestScenePath, LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            var bootstrap = Object.FindAnyObjectByType<BattleRoyaleExplorationPreviewDebug>();
            Assert.That(bootstrap, Is.Not.Null);
            var timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!bootstrap.IsReady
                   && string.IsNullOrEmpty(bootstrap.FailureMessage)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(bootstrap.FailureMessage, Is.Empty);
            Assert.That(bootstrap.IsReady, Is.True);
            Assert.That(
                bootstrap.GameplayCamera.transparencySortMode,
                Is.EqualTo(TransparencySortMode.CustomAxis));
            Assert.That(bootstrap.GameplayCamera.transparencySortAxis, Is.EqualTo(Vector3.up));

            var playerRenderer = bootstrap.PlayerObject.GetComponent<SpriteRenderer>();
            AssertWorldYRenderer(playerRenderer, "Preview Player");

            var mapRenderers = FindComponentsInScene<SpriteRenderer>(bootstrap.LoadedMapScene);
            var checkedWorldObjects = 0;
            var checkedGroundDetails = 0;
            for (var index = 0; index < mapRenderers.Count; index++)
            {
                var renderer = mapRenderers[index];
                if (HasAncestorNamed(renderer.transform, "ObstacleVisuals")
                    || HasAncestorNamed(renderer.transform, "Props"))
                {
                    AssertWorldYRenderer(renderer, renderer.name);
                    checkedWorldObjects++;
                    continue;
                }

                if (!HasAncestorNamed(renderer.transform, "Decorations"))
                {
                    continue;
                }

                var isGroundDetail = renderer.sprite != null
                                     && (renderer.sprite.name == "detail_flower_patch"
                                         || renderer.sprite.name == "detail_wildflowers");
                if (isGroundDetail)
                {
                    Assert.That(renderer.sortingLayerName, Is.EqualTo("MapDetail"), renderer.name);
                    Assert.That(renderer.sortingOrder, Is.EqualTo(0), renderer.name);
                    Assert.That(renderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot), renderer.name);
                    checkedGroundDetails++;
                }
                else
                {
                    AssertWorldYRenderer(renderer, renderer.name);
                    checkedWorldObjects++;
                }
            }

            Assert.That(checkedWorldObjects, Is.GreaterThan(0));
            Assert.That(checkedGroundDetails, Is.GreaterThan(0));
        }

        /// <summary>
        /// 実Reference Mapの木が樹冠全体ではなく幹の接地点だけをMapCollisionとして持つことを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator ExplorationPreview_TreeCollisionFollowsTrunkInsteadOfCanopy()
        {
            var loadOperation = SceneManager.LoadSceneAsync(PlaytestScenePath, LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            var bootstrap = Object.FindAnyObjectByType<BattleRoyaleExplorationPreviewDebug>();
            Assert.That(bootstrap, Is.Not.Null);
            var timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!bootstrap.IsReady
                   && string.IsNullOrEmpty(bootstrap.FailureMessage)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(bootstrap.FailureMessage, Is.Empty);
            Assert.That(bootstrap.IsReady, Is.True);

            var markers = FindComponentsInScene<MapObstacleVisualMarker>(bootstrap.LoadedMapScene);
            var preciseBodyCount = 0;
            MapObstacleVisualMarker isolatedTree = null;
            for (var index = 0; index < markers.Count; index++)
            {
                var marker = markers[index];
                if (marker.CollisionMode != MapObstacleCollisionMode.CollisionBody)
                {
                    continue;
                }

                preciseBodyCount++;
                var body = marker.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
                Assert.That(body, Is.Not.Null, marker.name);
                Assert.That(body.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("MapCollision")), marker.name);
                Assert.That(body.GetComponents<Collider2D>(), Has.Length.EqualTo(1), marker.name);
                Assert.That(body.GetComponent<Renderer>(), Is.Null, marker.name);
                Assert.That(body.GetComponent<Rigidbody2D>(), Is.Null, marker.name);

                var renderer = marker.GetComponent<SpriteRenderer>();
                if (isolatedTree != null
                    || renderer == null
                    || renderer.sprite == null
                    || !renderer.sprite.name.StartsWith("prop_tree_medium_"))
                {
                    continue;
                }

                var anchorCell = bootstrap.CollisionTilemap.WorldToCell(marker.transform.position);
                if (!bootstrap.CollisionTilemap.HasTile(anchorCell))
                {
                    isolatedTree = marker;
                }
            }

            Assert.That(preciseBodyCount, Is.GreaterThan(0));
            Assert.That(isolatedTree, Is.Not.Null, "CollisionTilemapに重ならない単木Instanceが必要です。");

            Physics2D.SyncTransforms();
            var treeBody = isolatedTree.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            var treeCollider = treeBody.GetComponent<Collider2D>();
            var treeRenderer = isolatedTree.GetComponent<SpriteRenderer>();
            var trunkPoint = (Vector2)treeCollider.bounds.center;
            Assert.That(
                Physics2D.GetIgnoreLayerCollision(
                    bootstrap.PlayerObject.layer,
                    LayerMask.NameToLayer("MapCollision")),
                Is.False,
                "Player LayerとMapCollision LayerはPhysics衝突する必要があります。");
            Assert.That(treeCollider.OverlapPoint(trunkPoint), Is.True, "幹中央には当たり判定が必要です。");
            Assert.That(
                Physics2D.OverlapPoint(trunkPoint, 1 << LayerMask.NameToLayer("MapCollision")),
                Is.Not.Null,
                "幹判定はMapCollision LayerのPhysics Queryから取得できる必要があります。");

            Assert.That(
                treeRenderer.bounds.max.y - treeCollider.bounds.max.y,
                Is.GreaterThan(0.25f),
                "樹冠と幹判定の間に歩行可能な表示領域が必要です。");
            var canopyPoint = new Vector2(
                treeCollider.bounds.center.x,
                Mathf.Lerp(treeCollider.bounds.max.y, treeRenderer.bounds.max.y, 0.5f));
            Assert.That(
                treeRenderer.bounds.Contains(new Vector3(canopyPoint.x, canopyPoint.y, treeRenderer.bounds.center.z)),
                Is.True);
            Assert.That(treeCollider.OverlapPoint(canopyPoint), Is.False, "樹冠側へ判定を広げないでください。");

            // 実Player Colliderの中心を同じ2点へ置き、幹では重なり、樹冠では離れることまで確認する。
            bootstrap.PlayerBody.position = trunkPoint - bootstrap.PlayerCollider.offset;
            bootstrap.PlayerBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            Assert.That(
                Physics2D.Distance(bootstrap.PlayerCollider, treeCollider).isOverlapped,
                Is.True,
                "実Player Colliderは幹判定と衝突する必要があります。");

            bootstrap.PlayerBody.position = canopyPoint - bootstrap.PlayerCollider.offset;
            Physics2D.SyncTransforms();
            Assert.That(
                Physics2D.Distance(bootstrap.PlayerCollider, treeCollider).isOverlapped,
                Is.False,
                "実Player Colliderを樹冠側で止めないでください。");
        }

        /// <summary>
        /// 実Reference MapのEventSocketへ接近し、泉の回復成功後だけSocket ID単位でOneShotになることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator ExplorationPreview_HealingFountainUsesAdjustedSocketAndRuntimeState()
        {
            var loadOperation = SceneManager.LoadSceneAsync(PlaytestScenePath, LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            var bootstrap = Object.FindAnyObjectByType<BattleRoyaleExplorationPreviewDebug>();
            Assert.That(bootstrap, Is.Not.Null);
            var timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!bootstrap.IsReady
                   && string.IsNullOrEmpty(bootstrap.FailureMessage)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(bootstrap.FailureMessage, Is.Empty);
            Assert.That(bootstrap.IsReady, Is.True);
            Assert.That(bootstrap.EventCatalog, Is.Not.Null);

            var interactAction = bootstrap.RuntimeInputActions.FindAction(
                $"{BattleRoyaleExplorationPreviewDebug.PlayerActionMapName}/"
                + BattleRoyaleExplorationPreviewDebug.InteractActionName,
                true);
            Assert.That(HasBinding(interactAction, "<Keyboard>/e"), Is.True);

            var eventSockets = FindComponentsInScene<MapSocketMarker>(bootstrap.LoadedMapScene)
                .FindAll(marker => marker.SocketKind == MapSocketKind.Event);
            Assert.That(eventSockets, Has.Count.EqualTo(6));
            var definition = eventSockets[0].EventDefinition as HealingFountainEventDefinition;
            Assert.That(definition, Is.Not.Null);
            for (var index = 0; index < eventSockets.Count; index++)
            {
                Assert.That(eventSockets[index].EventDefinition, Is.SameAs(definition));
                Assert.That(eventSockets[index].ResolveInteractionRadius(bootstrap.EventCatalog), Is.GreaterThan(0f));
            }

            var targetSocket = eventSockets[0];
            bootstrap.SetMoveInputOverrideForTests(Vector2.zero);
            bootstrap.PlayerBody.position = targetSocket.transform.position;
            bootstrap.PlayerBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(bootstrap.CurrentEventSocket, Is.SameAs(targetSocket));
            Assert.That(bootstrap.CurrentEventPrompt, Is.EqualTo(definition.PromptText));
            Assert.That(bootstrap.EventRuntimeState.IsUsed(targetSocket.SocketId), Is.False);
            var healthBefore = bootstrap.CurrentHealth;

            Assert.That(bootstrap.TryInteractWithCurrentEventForTests(), Is.True);
            Assert.That(
                bootstrap.CurrentHealth,
                Is.EqualTo(Mathf.Min(bootstrap.MaximumHealth, healthBefore + definition.HealAmount)));
            Assert.That(bootstrap.EventRuntimeState.IsUsed(targetSocket.SocketId), Is.True);
            Assert.That(bootstrap.LastEventFeedback, Does.Contain($"+{bootstrap.CurrentHealth - healthBefore}"));

            yield return null;
            Assert.That(bootstrap.CurrentEventSocket, Is.Not.SameAs(targetSocket));
            Assert.That(bootstrap.EventRuntimeState.TryMarkUsed(targetSocket.SocketId), Is.False);

            var fullHealthSocket = eventSockets[1];
            bootstrap.SetPreviewHealthForTests(bootstrap.MaximumHealth);
            bootstrap.PlayerBody.position = fullHealthSocket.transform.position;
            bootstrap.PlayerBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(bootstrap.CurrentEventSocket, Is.SameAs(fullHealthSocket));
            Assert.That(bootstrap.TryInteractWithCurrentEventForTests(), Is.False);
            Assert.That(bootstrap.EventRuntimeState.IsUsed(fullHealthSocket.SocketId), Is.False);
            Assert.That(bootstrap.LastEventFeedback, Does.Contain("満タン"));
        }

        /// <summary>
        /// Player、障害物、立体小物が共通WorldObjects層、Order 0、足元Pivotを使うことを確認する。
        /// </summary>
        private static void AssertWorldYRenderer(SpriteRenderer renderer, string context)
        {
            Assert.That(renderer, Is.Not.Null, context);
            Assert.That(
                renderer.sortingLayerName,
                Is.EqualTo(BattleRoyaleExplorationPreviewDebug.WorldObjectSortingLayerName),
                context);
            Assert.That(renderer.sortingOrder, Is.EqualTo(0), context);
            Assert.That(renderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot), context);
        }

        /// <summary>
        /// Prefab Instance内のRendererから用途別Root名まで親を辿り、Sceneのカテゴリを判定する。
        /// </summary>
        private static bool HasAncestorNamed(Transform transform, string expectedName)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.name == expectedName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 指定Actionが期待する実Control Pathを持つことを確認し、Test注入口だけで成功する状態を防ぐ。
        /// </summary>
        private static bool HasBinding(InputAction action, string expectedPath)
        {
            for (var bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
            {
                if (string.Equals(
                        action.bindings[bindingIndex].path,
                        expectedPath,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Cardinal移動方向を仮想KeyboardのWASD Keyへ変換する。
        /// </summary>
        private static Key ToKeyboardKey(Vector2 direction)
        {
            if (direction.x > 0.5f)
            {
                return Key.D;
            }

            if (direction.x < -0.5f)
            {
                return Key.A;
            }

            return direction.y > 0f ? Key.W : Key.S;
        }

        /// <summary>
        /// PlayerStartから指定Cell数先までCollisionがないCardinal方向を選び、固定座標依存を避ける。
        /// </summary>
        private static Vector2 FindClearMoveDirection(
            Tilemap collisionTilemap,
            Vector2 worldPosition,
            int requiredCells)
        {
            var startCell = collisionTilemap.WorldToCell(worldPosition);
            var directions = new[]
            {
                new Vector3Int(1, 0, 0),
                new Vector3Int(0, 1, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, -1, 0)
            };

            for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                var direction = directions[directionIndex];
                var clear = true;
                for (var distance = 1; distance <= requiredCells; distance++)
                {
                    if (collisionTilemap.HasTile(startCell + direction * distance))
                    {
                        clear = false;
                        break;
                    }
                }

                if (clear)
                {
                    return new Vector2(direction.x, direction.y);
                }
            }

            return Vector2.zero;
        }

        /// <summary>
        /// Collision CellにCardinal隣接する空Cellを探し、実Tilemap壁へ接近するTest条件を作る。
        /// </summary>
        private static bool TryFindCollisionApproach(
            Tilemap collisionTilemap,
            Collider2D playerCollider,
            out Vector2 freePosition,
            out Vector2 blockedPosition,
            out Vector2 towardCollision)
        {
            var directions = new[]
            {
                new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 1, 0),
                new Vector3Int(0, -1, 0)
            };
            var bounds = collisionTilemap.cellBounds;
            foreach (var blockedCell in bounds.allPositionsWithin)
            {
                if (!collisionTilemap.HasTile(blockedCell))
                {
                    continue;
                }

                for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                {
                    var freeCell = blockedCell - directions[directionIndex];
                    if (!bounds.Contains(freeCell) || collisionTilemap.HasTile(freeCell))
                    {
                        continue;
                    }

                    var candidateFree = (Vector2)collisionTilemap.GetCellCenterWorld(freeCell);
                    var candidateBlocked = (Vector2)collisionTilemap.GetCellCenterWorld(blockedCell);
                    playerCollider.transform.position = candidateFree;
                    Physics2D.SyncTransforms();
                    // Tilemapの空Cellでも木の足元判定があり得るため、MapCollision層全体で接近開始点を選ぶ。
                    var mapCollisionLayer = LayerMask.NameToLayer("MapCollision");
                    var contactFilter = new ContactFilter2D();
                    contactFilter.SetLayerMask(1 << mapCollisionLayer);
                    contactFilter.useTriggers = false;
                    var overlaps = new List<Collider2D>();
                    if (playerCollider.Overlap(contactFilter, overlaps) > 0)
                    {
                        continue;
                    }

                    freePosition = candidateFree;
                    blockedPosition = candidateBlocked;
                    towardCollision = new Vector2(
                        directions[directionIndex].x,
                        directions[directionIndex].y);
                    return true;
                }
            }

            freePosition = Vector2.zero;
            blockedPosition = Vector2.zero;
            towardCollision = Vector2.zero;
            return false;
        }

        /// <summary>
        /// Global検索を避け、指定Scene内だけからComponentを列挙する。
        /// </summary>
        private static List<T> FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            var result = new List<T>();
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                result.AddRange(roots[rootIndex].GetComponentsInChildren<T>(true));
            }

            return result;
        }
    }
}
