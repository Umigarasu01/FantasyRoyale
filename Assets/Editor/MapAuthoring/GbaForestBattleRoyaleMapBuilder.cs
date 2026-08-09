using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasyRoyale.MapAuthoringKit;
using FantasyRoyale.MapAuthoringKit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.Editor.MapAuthoring
{
    /// <summary>
    /// 既存資料の96x72・5〜6人想定を、完成基準となる固定Sceneへ変換する。
    /// 生成計画を先に検証してからSceneへ一括反映し、表示とCollisionの正本を混同しない。
    /// </summary>
    public static class GbaForestBattleRoyaleMapBuilder
    {
        public const string ReferenceScenePath =
            "Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity";
        public const string OverviewPreviewPath =
            "Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-overview.png";
        public const string CentralPreviewPath =
            "Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-central.png";
        public const string WaterfrontPreviewPath =
            "Assets/Art/Generated/MapAuthoring/QA/gba-forest-battle-royale-reference-waterfront.png";

        private const int XMin = -48;
        private const int XMax = 47;
        private const int YMin = -36;
        private const int YMax = 35;
        private const int ExpectedParticipantCount = 6;
        private const int LayoutSeed = 20260803;

        private static readonly Vector3Int[] CardinalDirections =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };

        /// <summary>
        /// Sceneへ書き込む既存TileとPrefabを一度だけ解決し、計画と出力で同じ参照を使う。
        /// </summary>
        private sealed class ReferenceAssets
        {
            public GroundVariationTile GrassTile;
            public RoadConnectionRuleTile DirtRuleTile;
            public RoadConnectionRuleTile StoneRuleTile;
            public RuleTile WaterRuleTile;
            public Tile CollisionTile;
            public readonly Dictionary<string, GameObject> VisualPrefabs =
                new Dictionary<string, GameObject>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 再生成前にSceneで手調整されたEventSocketのWorld位置と個別半径だけを退避する。
        /// Event種類はCatalog契約へ収束させるためBuilder側の定義を正本とする。
        /// </summary>
        private readonly struct EventSocketAuthoringOverride
        {
            public EventSocketAuthoringOverride(Vector3 position, float interactionRadiusOverride)
            {
                Position = position;
                InteractionRadiusOverride = interactionRadiusOverride;
            }

            public Vector3 Position { get; }
            public float InteractionRadiusOverride { get; }
        }

        /// <summary>
        /// Unity Sceneを変更する前に確定する、地形・判定・表示・Socketの固定生成計画。
        /// 将来のSeed生成ではこの計画を作る部分だけを差し替えられるようにする。
        /// </summary>
        private sealed class ReferenceMapPlan
        {
            public readonly HashSet<Vector3Int> GroundCells = new HashSet<Vector3Int>();
            public readonly HashSet<Vector3Int> WaterCells = new HashSet<Vector3Int>();
            public readonly HashSet<Vector3Int> RoadCells = new HashSet<Vector3Int>();
            public readonly HashSet<Vector3Int> StoneCells = new HashSet<Vector3Int>();
            public readonly HashSet<Vector3Int> CollisionCells = new HashSet<Vector3Int>();
            public readonly HashSet<Vector3Int> ProtectedCells = new HashSet<Vector3Int>();
            public readonly HashSet<Vector3Int> ObstacleFootprintCells = new HashSet<Vector3Int>();
            public readonly List<VisualPlacement> Decorations = new List<VisualPlacement>();
            public readonly List<VisualPlacement> Obstacles = new List<VisualPlacement>();
            public readonly List<VisualPlacement> Props = new List<VisualPlacement>();
            public readonly List<SocketPlacement> Sockets = new List<SocketPlacement>();
        }

        /// <summary>
        /// 既存Prefabをどの自由配置Rootへ、どの1px単位座標で置くかを表す。
        /// </summary>
        private readonly struct VisualPlacement
        {
            public VisualPlacement(string prefabKey, string name, Vector3 position)
            {
                PrefabKey = prefabKey;
                Name = name;
                Position = position;
            }

            public string PrefabKey { get; }
            public string Name { get; }
            public Vector3 Position { get; }
        }

        /// <summary>
        /// ゲーム側が後で抽選に使う候補点を、用途と向きを含めて保持する。
        /// </summary>
        private readonly struct SocketPlacement
        {
            public SocketPlacement(
                string name,
                Vector3 position,
                MapSocketKind kind,
                MapSocketFacing facing,
                string tag)
            {
                Name = name;
                Position = position;
                Kind = kind;
                Facing = facing;
                Tag = tag;
            }

            public string Name { get; }
            public Vector3 Position { get; }
            public MapSocketKind Kind { get; }
            public MapSocketFacing Facing { get; }
            public string Tag { get; }
        }

        /// <summary>
        /// 1枚のQA画像で確認するCamera位置、表示範囲、画像寸法、保存先をまとめる。
        /// </summary>
        private readonly struct CaptureFrame
        {
            public CaptureFrame(string path, Vector2 center, float orthographicSize, int width, int height)
            {
                Path = path;
                Center = center;
                OrthographicSize = orthographicSize;
                Width = width;
                Height = height;
            }

            public string Path { get; }
            public Vector2 Center { get; }
            public float OrthographicSize { get; }
            public int Width { get; }
            public int Height { get; }
        }

        /// <summary>
        /// 既存Production Assetだけを使い、96x72の基準バトロワSceneを再生成する。
        /// </summary>
        [MenuItem("FantasyRoyale/Map Authoring/Build Battle Royale Reference Map")]
        public static void BuildReferenceMap()
        {
            EnsureRequiredFolders();
            EnsureTemplateExists();

            var assets = LoadReferenceAssets();
            var plan = CreateReferencePlan(assets);
            ValidateReferencePlan(plan);
            WriteReferenceScene(assets, plan);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"Battle Royale reference map built: {ReferenceScenePath} / "
                + $"ground={plan.GroundCells.Count}, road={plan.RoadCells.Count}, "
                + $"water={plan.WaterCells.Count}, collision={plan.CollisionCells.Count}, "
                + $"obstacles={plan.Obstacles.Count}, decorations={plan.Decorations.Count}, "
                + $"sockets={plan.Sockets.Count}");
        }

        /// <summary>
        /// 保存済み基準Sceneを全景・中央・水辺の3構図で描画し、比較用PNGを更新する。
        /// </summary>
        [MenuItem("FantasyRoyale/Map Authoring/Capture Battle Royale Reference Map")]
        public static void CaptureReferencePreviews()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                throw new InvalidOperationException(
                    "Reference previewにはGraphics Deviceが必要です。-nographicsを外してください。");
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ReferenceScenePath);
            if (sceneAsset == null)
            {
                throw new FileNotFoundException("基準Map Sceneがありません。先にBuildReferenceMapを実行してください。", ReferenceScenePath);
            }

            var wasLoaded = TryGetLoadedScene(ReferenceScenePath, out var scene);
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Additive);
            }

            var previousActiveScene = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = FindRoot(scene, "MapRoot");
                var camera = root.GetComponentInChildren<Camera>(true);
                if (camera == null)
                {
                    throw new InvalidOperationException("基準Map SceneにCameraがありません。");
                }

                var frames = new[]
                {
                    new CaptureFrame(OverviewPreviewPath, Vector2.zero, 37.5f, 1600, 1200),
                    new CaptureFrame(CentralPreviewPath, new Vector2(0f, 0f), 15f, 1280, 960),
                    new CaptureFrame(WaterfrontPreviewPath, new Vector2(25f, 7f), 15f, 1280, 960)
                };

                var originalPosition = camera.transform.position;
                var originalRotation = camera.transform.rotation;
                var originalSize = camera.orthographicSize;
                try
                {
                    for (var frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                    {
                        CaptureFrameToPng(root.transform, camera, frames[frameIndex]);
                    }
                }
                finally
                {
                    camera.targetTexture = null;
                    camera.transform.position = originalPosition;
                    camera.transform.rotation = originalRotation;
                    camera.orthographicSize = originalSize;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Battle Royale reference previews captured.");
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// 地面、地形形状、配置物、Socketを決定し、Scene書き込み前の完全な計画を返す。
        /// </summary>
        private static ReferenceMapPlan CreateReferencePlan(ReferenceAssets assets)
        {
            var plan = new ReferenceMapPlan();
            FillGround(plan);
            BuildEastWaterway(plan);
            BuildRoadNetwork(plan);
            AddSocketPlan(plan);
            AddBoundaryCollision(plan);
            BuildProtectedCells(plan);
            AddObstaclePlan(assets, plan);
            AddDecorationPlan(plan);
            AddPropPlan(plan);
            return plan;
        }

        /// <summary>
        /// 96x72全CellをGroundとして登録し、地形の下地を欠落させない。
        /// </summary>
        private static void FillGround(ReferenceMapPlan plan)
        {
            for (var y = YMin; y <= YMax; y++)
            {
                for (var x = XMin; x <= XMax; x++)
                {
                    plan.GroundCells.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        /// <summary>
        /// 東端に開いた不規則な湖と南へ細く続く水路をCell集合として作る。
        /// 水際のSprite選択はWater RuleTileへ委ね、ここでは輪郭だけを決める。
        /// </summary>
        private static void BuildEastWaterway(ReferenceMapPlan plan)
        {
            var coastRows = new[]
            {
                (Y: -10, MinX: 40, MaxX: 42), (Y: -9, MinX: 40, MaxX: 43),
                (Y: -8, MinX: 39, MaxX: 43), (Y: -7, MinX: 39, MaxX: 43),
                (Y: -6, MinX: 38, MaxX: 42), (Y: -5, MinX: 38, MaxX: 42),
                (Y: -4, MinX: 37, MaxX: 41), (Y: -3, MinX: 36, MaxX: 41),
                (Y: -2, MinX: 36, MaxX: 40), (Y: -1, MinX: 35, MaxX: 39),
                (Y: 0, MinX: 34, MaxX: 39), (Y: 1, MinX: 35, MaxX: 40),
                (Y: 2, MinX: 34, MaxX: 40), (Y: 3, MinX: 34, MaxX: 41),
                (Y: 4, MinX: 33, MaxX: 41), (Y: 5, MinX: 33, MaxX: 42),
                (Y: 6, MinX: 32, MaxX: 42), (Y: 7, MinX: 31, MaxX: 41),
                (Y: 8, MinX: 30, MaxX: 41), (Y: 9, MinX: 29, MaxX: 40),
                (Y: 10, MinX: 28, MaxX: 39), (Y: 11, MinX: 29, MaxX: 38),
                (Y: 12, MinX: 28, MaxX: 38), (Y: 13, MinX: 28, MaxX: 39),
                (Y: 14, MinX: 29, MaxX: 40), (Y: 15, MinX: 28, MaxX: 40),
                (Y: 16, MinX: 28, MaxX: 41), (Y: 17, MinX: 28, MaxX: 42),
                (Y: 18, MinX: 29, MaxX: 42), (Y: 19, MinX: 29, MaxX: 43),
                (Y: 20, MinX: 30, MaxX: 43), (Y: 21, MinX: 30, MaxX: 44),
                (Y: 22, MinX: 31, MaxX: 44), (Y: 23, MinX: 31, MaxX: 43),
                (Y: 24, MinX: 32, MaxX: 43), (Y: 25, MinX: 33, MaxX: 42),
                (Y: 26, MinX: 34, MaxX: 41), (Y: 27, MinX: 36, MaxX: 39)
            };

            for (var rowIndex = 0; rowIndex < coastRows.Length; rowIndex++)
            {
                var row = coastRows[rowIndex];
                for (var x = row.MinX; x <= row.MaxX; x++)
                {
                    plan.WaterCells.Add(new Vector3Int(x, row.Y, 0));
                }
            }

            // 水面を一枚塗りにせず、小さな島を抜いて内角も使える岸線にする。
            var islandCells = new[]
            {
                Cell(36, 19), Cell(37, 19), Cell(35, 18),
                Cell(36, 18), Cell(37, 18), Cell(36, 17)
            };
            for (var islandIndex = 0; islandIndex < islandCells.Length; islandIndex++)
            {
                plan.WaterCells.Remove(islandCells[islandIndex]);
            }

            plan.CollisionCells.UnionWith(plan.WaterCells);
        }

        /// <summary>
        /// 6方向の開始地点と7つの役割エリアを、中央で交差する複数Loopの3Cell幅道路で結ぶ。
        /// </summary>
        private static void BuildRoadNetwork(ReferenceMapPlan plan)
        {
            var paths = new IEnumerable<Vector3Int>[]
            {
                new[]
                {
                    Cell(-43, 3), Cell(-40, 3), Cell(-40, 5), Cell(-35, 5),
                    Cell(-35, 7), Cell(-29, 7), Cell(-29, 5), Cell(-24, 5),
                    Cell(-24, 3), Cell(-18, 3), Cell(-18, 1), Cell(-12, 1),
                    Cell(-12, -1), Cell(-6, -1), Cell(-6, 0), Cell(0, 0)
                },
                new[]
                {
                    Cell(0, 0), Cell(5, 0), Cell(5, 2), Cell(10, 2),
                    Cell(10, 4), Cell(15, 4), Cell(15, 6), Cell(20, 6),
                    Cell(20, 8), Cell(25, 8)
                },
                new[]
                {
                    Cell(0, -32), Cell(0, -26), Cell(1, -26), Cell(1, -20),
                    Cell(-2, -20), Cell(-2, -15), Cell(0, -15), Cell(0, -10),
                    Cell(2, -10), Cell(2, -5), Cell(0, -5), Cell(0, 0)
                },
                new[]
                {
                    Cell(-6, 32), Cell(-6, 25), Cell(-8, 25), Cell(-8, 21),
                    Cell(-6, 21), Cell(-6, 16), Cell(-3, 16), Cell(-3, 11),
                    Cell(-1, 11), Cell(-1, 6), Cell(0, 6), Cell(0, 0)
                },
                new[]
                {
                    Cell(-42, 26), Cell(-37, 26), Cell(-37, 24), Cell(-32, 24),
                    Cell(-32, 22), Cell(-27, 22), Cell(-27, 24), Cell(-21, 24),
                    Cell(-21, 26), Cell(-14, 26), Cell(-14, 25), Cell(-6, 25)
                },
                new[]
                {
                    Cell(34, 28), Cell(29, 28), Cell(29, 26), Cell(24, 26),
                    Cell(24, 24), Cell(18, 24), Cell(18, 22), Cell(12, 22),
                    Cell(12, 20), Cell(6, 20), Cell(6, 18), Cell(0, 18),
                    Cell(0, 15), Cell(-3, 15)
                },
                new[]
                {
                    Cell(39, -28), Cell(34, -28), Cell(34, -26), Cell(29, -26),
                    Cell(29, -23), Cell(24, -23), Cell(24, -21), Cell(19, -21),
                    Cell(19, -18), Cell(14, -18), Cell(14, -15), Cell(8, -15),
                    Cell(8, -11), Cell(2, -11)
                },
                new[]
                {
                    Cell(-39, -28), Cell(-35, -28), Cell(-35, -26), Cell(-30, -26),
                    Cell(-30, -24), Cell(-24, -24), Cell(-24, -22), Cell(-18, -22),
                    Cell(-18, -20), Cell(-12, -20), Cell(-12, -18), Cell(-7, -18),
                    Cell(-7, -16), Cell(-2, -16)
                },
                new[]
                {
                    Cell(25, 8), Cell(25, 3), Cell(23, 3), Cell(23, -2),
                    Cell(25, -2), Cell(25, -7), Cell(22, -7), Cell(22, -12),
                    Cell(19, -12), Cell(19, -17), Cell(14, -17)
                },
                new[]
                {
                    Cell(18, 24), Cell(18, 18), Cell(20, 18), Cell(20, 14),
                    Cell(23, 14), Cell(23, 10), Cell(25, 10)
                },
                new[]
                {
                    Cell(-29, 7), Cell(-29, 12), Cell(-27, 12), Cell(-27, 16),
                    Cell(-24, 16), Cell(-24, 20), Cell(-21, 20), Cell(-21, 24)
                },
                new[]
                {
                    Cell(-18, 1), Cell(-18, -5), Cell(-20, -5), Cell(-20, -10),
                    Cell(-18, -10), Cell(-18, -15), Cell(-12, -15), Cell(-12, -18)
                }
            };

            plan.RoadCells.UnionWith(MapRoadPathRasterizer.RasterizePaths(paths, squareBrushRadius: 1));

            AddIrregularPlaza(plan.StoneCells, new Vector2Int(0, 0), new[] { 3, 4, 5, 6, 6, 5, 3 });
            AddIrregularPlaza(plan.StoneCells, new Vector2Int(-6, 25), new[] { 2, 3, 4, 5, 4, 2 });
            AddIrregularPlaza(plan.StoneCells, new Vector2Int(24, 8), new[] { 1, 2, 3, 3, 2 });
            AddIrregularPlaza(plan.StoneCells, new Vector2Int(0, -26), new[] { 2, 3, 5, 5, 4, 2 });
            plan.RoadCells.UnionWith(plan.StoneCells);
        }

        /// <summary>
        /// 行ごとの半幅から角の揃わない広場を作り、矩形塗りによるグリッド感を避ける。
        /// </summary>
        private static void AddIrregularPlaza(
            ISet<Vector3Int> destination,
            Vector2Int center,
            IReadOnlyList<int> halfWidths)
        {
            var startY = center.y - halfWidths.Count / 2;
            for (var rowIndex = 0; rowIndex < halfWidths.Count; rowIndex++)
            {
                var halfWidth = halfWidths[rowIndex];
                var rowOffset = rowIndex % 3 == 1 ? 1 : 0;
                for (var x = center.x - halfWidth + rowOffset; x <= center.x + halfWidth; x++)
                {
                    destination.Add(Cell(x, startY + rowIndex));
                }
            }
        }

        /// <summary>
        /// 1人分のPlayerStart、5人分のCPU開始候補と、探索・戦闘・報酬候補を固定配置する。
        /// </summary>
        private static void AddSocketPlan(ReferenceMapPlan plan)
        {
            AddSocket(plan, "PlayerStart_SouthWest", -39, -28, MapSocketKind.PlayerStart, MapSocketFacing.North, "participant-south-west");
            AddSocket(plan, "CpuStart_West", -43, 2, MapSocketKind.CpuStart, MapSocketFacing.East, "participant-west");
            AddSocket(plan, "CpuStart_NorthWest", -42, 26, MapSocketKind.CpuStart, MapSocketFacing.East, "participant-north-west");
            AddSocket(plan, "CpuStart_North", -6, 32, MapSocketKind.CpuStart, MapSocketFacing.South, "participant-north");
            AddSocket(plan, "CpuStart_NorthEast", 34, 28, MapSocketKind.CpuStart, MapSocketFacing.West, "participant-north-east");
            AddSocket(plan, "CpuStart_SouthEast", 39, -28, MapSocketKind.CpuStart, MapSocketFacing.West, "participant-south-east");

            AddSocket(plan, "Landmark_CentralCrossroads", 0, 0, MapSocketKind.Landmark, MapSocketFacing.Any, "central-crossroads");
            AddSocket(plan, "Landmark_NorthShade", -6, 25, MapSocketKind.Landmark, MapSocketFacing.Any, "north-shaded-plaza");
            AddSocket(plan, "Landmark_WestMeadow", -29, 6, MapSocketKind.Landmark, MapSocketFacing.Any, "west-grass-corridor");
            AddSocket(plan, "Landmark_Waterfront", 26, 8, MapSocketKind.Landmark, MapSocketFacing.Any, "east-waterfront");
            AddSocket(plan, "Landmark_SouthGrove", 1, -25, MapSocketKind.Landmark, MapSocketFacing.Any, "south-grove");
            AddSocket(plan, "Landmark_SouthEastGate", 27, -23, MapSocketKind.Landmark, MapSocketFacing.Any, "south-east-cliff-gate");
            AddSocket(plan, "Landmark_NorthEastLookout", 18, 24, MapSocketKind.Landmark, MapSocketFacing.Any, "north-east-lookout");

            var enemies = new[]
            {
                (-34, -13), (-26, -18), (-15, -15), (-11, -7), (-13, 13), (-28, 18),
                (-35, 12), (7, 8), (14, 17), (22, 10), (21, -9), (11, -19),
                (31, -24), (26, 21), (8, 29), (-18, 28)
            };
            for (var i = 0; i < enemies.Length; i++)
            {
                AddSocket(plan, $"Enemy_{i + 1:00}", enemies[i].Item1, enemies[i].Item2, MapSocketKind.Enemy, MapSocketFacing.Any, $"enemy-zone-{i + 1:00}");
            }

            var loot = new[]
            {
                (-36, -24), (-27, -23), (-20, -17), (-10, -18), (-4, -4), (5, 3),
                (-25, 8), (-35, 17), (-25, 23), (-12, 22), (-2, 29), (7, 23),
                (16, 27), (25, 24), (26, 12), (27, 3), (20, -4), (25, -16),
                (34, -25), (9, -27), (-7, -26), (-40, 8)
            };
            for (var i = 0; i < loot.Length; i++)
            {
                AddSocket(plan, $"Loot_{i + 1:00}", loot[i].Item1, loot[i].Item2, MapSocketKind.Loot, MapSocketFacing.Any, $"loot-{i + 1:00}");
            }

            AddSocket(plan, "Merchant_West", -27, 5, MapSocketKind.Merchant, MapSocketFacing.Any, "merchant-west");
            AddSocket(plan, "Merchant_Waterfront", 24, 7, MapSocketKind.Merchant, MapSocketFacing.Any, "merchant-waterfront");
            AddSocket(plan, "Merchant_South", 3, -24, MapSocketKind.Merchant, MapSocketFacing.Any, "merchant-south");

            var events = new[]
            {
                (-30, -9), (-18, 10), (5, 15), (12, 20), (14, -8), (20, -18)
            };
            for (var i = 0; i < events.Length; i++)
            {
                AddSocket(plan, $"Event_{i + 1:00}", events[i].Item1, events[i].Item2, MapSocketKind.Event, MapSocketFacing.Any, $"event-{i + 1:00}");
            }
        }

        /// <summary>
        /// Socket定義を追加し、位置と用途の記述を一か所へ揃える。
        /// </summary>
        private static void AddSocket(
            ReferenceMapPlan plan,
            string name,
            int x,
            int y,
            MapSocketKind kind,
            MapSocketFacing facing,
            string tag)
        {
            plan.Sockets.Add(new SocketPlacement(name, new Vector3(x, y, 0f), kind, facing, tag));
        }

        /// <summary>
        /// 外周を不可視Collisionで閉じ、表示素材の隙間が移動可能領域にならないようにする。
        /// </summary>
        private static void AddBoundaryCollision(ReferenceMapPlan plan)
        {
            for (var x = XMin; x <= XMax; x++)
            {
                plan.CollisionCells.Add(Cell(x, YMin));
                plan.CollisionCells.Add(Cell(x, YMax));
            }

            for (var y = YMin; y <= YMax; y++)
            {
                plan.CollisionCells.Add(Cell(XMin, y));
                plan.CollisionCells.Add(Cell(XMax, y));
            }
        }

        /// <summary>
        /// 道路・広場・水面・Socket周辺を障害物候補から除外し、読める移動空間を守る。
        /// </summary>
        private static void BuildProtectedCells(ReferenceMapPlan plan)
        {
            plan.ProtectedCells.UnionWith(plan.WaterCells);
            AddExpandedCells(plan.ProtectedCells, plan.RoadCells, 1);

            for (var socketIndex = 0; socketIndex < plan.Sockets.Count; socketIndex++)
            {
                var socket = plan.Sockets[socketIndex];
                var radius = socket.Kind == MapSocketKind.PlayerStart || socket.Kind == MapSocketKind.CpuStart
                    ? 2
                    : 1;
                AddSquare(plan.ProtectedCells, RoundToCell(socket.Position), radius);
            }

            // 西の草原は路面で埋めず、戦える芝生の余白として明示的に保護する。
            AddIrregularPlaza(plan.ProtectedCells, new Vector2Int(-30, 5), new[] { 4, 6, 8, 8, 7, 5, 3 });
        }

        /// <summary>
        /// 大型森林Stamp、崖、単木を組み合わせ、等間隔の壁ではない連続した外周と内部林を作る。
        /// </summary>
        private static void AddObstaclePlan(ReferenceAssets assets, ReferenceMapPlan plan)
        {
            var random = new System.Random(LayoutSeed);
            var counter = 0;

            for (var x = -44; x <= 44; x += random.Next(4, 8))
            {
                var key = random.Next(4) == 0 ? "forest_mass_deep" : "forest_mass_wide";
                TryAddObstacle(assets, plan, key, $"ForestNorth_{counter:00}", Snapped(x + Jitter(random, 5), 33f + Jitter(random, 3)), true);
                counter++;
            }

            counter = 0;
            for (var x = -44; x <= 44; x += random.Next(4, 8))
            {
                var key = random.Next(4) == 1 ? "forest_mass_deep" : "forest_mass_wide";
                TryAddObstacle(assets, plan, key, $"ForestSouth_{counter:00}", Snapped(x + Jitter(random, 5), -35f + Jitter(random, 2)), true);
                counter++;
            }

            counter = 0;
            for (var y = -29; y <= 29; y += random.Next(5, 9))
            {
                var key = random.Next(3) == 0 ? "forest_mass_deep" : "forest_mass_wide";
                TryAddObstacle(assets, plan, key, $"ForestWest_{counter:00}", Snapped(-45f + Jitter(random, 3), y + Jitter(random, 5)), true);
                counter++;
            }

            counter = 0;
            for (var y = -29; y <= 29; y += random.Next(5, 9))
            {
                var key = random.Next(3) == 1 ? "forest_mass_deep" : "forest_mass_wide";
                TryAddObstacle(assets, plan, key, $"ForestEast_{counter:00}", Snapped(45f + Jitter(random, 3), y + Jitter(random, 5)), true);
                counter++;
            }

            var fixedGroves = new[]
            {
                new VisualPlacement("forest_mass_wide", "GroveNorthWest01", Snapped(-27.125f, 30.0625f)),
                new VisualPlacement("forest_mass_deep", "GroveNorthWest02", Snapped(-20.03125f, 31.0f)),
                new VisualPlacement("forest_front_strip", "GroveNorthWestFront", Snapped(-24.0f, 28.96875f)),
                new VisualPlacement("forest_mass_wide", "GroveNorthEast01", Snapped(8.125f, 31.0f)),
                new VisualPlacement("forest_mass_deep", "GroveNorthEast02", Snapped(15.03125f, 30.96875f)),
                new VisualPlacement("forest_mass_wide", "GroveWestLower01", Snapped(-41.0625f, -13.0f)),
                new VisualPlacement("forest_mass_deep", "GroveWestLower02", Snapped(-35.96875f, -11.96875f)),
                new VisualPlacement("forest_mass_deep", "GroveSouthWest", Snapped(-23.0625f, -33.0f)),
                new VisualPlacement("forest_mass_wide", "GroveSouthCenter", Snapped(11.09375f, -34.0f)),
                new VisualPlacement("forest_mass_deep", "GroveSouthEast", Snapped(16.03125f, -32.96875f)),
                new VisualPlacement("forest_mass_deep", "GroveCenterWest", Snapped(-12.0625f, 13.0f)),
                new VisualPlacement("forest_mass_wide", "GroveCenterNorth", Snapped(15.09375f, 18.0f)),
                new VisualPlacement("forest_front_strip", "GroveCenterNorthFront", Snapped(15.0f, 17.03125f))
            };
            for (var i = 0; i < fixedGroves.Length; i++)
            {
                var placement = fixedGroves[i];
                TryAddObstacle(assets, plan, placement.PrefabKey, placement.Name, placement.Position, true);
            }

            // 大型Stampを道路間の空白へ疎に置き、長い直線や四角い空地を森林の輪郭で分割する。
            var massRegions = new[]
            {
                new RectInt(-44, -20, 20, 15),
                new RectInt(-31, -12, 19, 20),
                new RectInt(-19, 7, 17, 15),
                new RectInt(4, 8, 21, 12),
                new RectInt(5, -10, 17, 18),
                new RectInt(-27, -34, 52, 12),
                new RectInt(26, -34, 18, 18),
                new RectInt(-41, 14, 22, 17),
                new RectInt(4, 20, 25, 13)
            };
            var massTargets = new[] { 7, 8, 7, 7, 6, 10, 7, 8, 8 };
            var massKeys = new[]
            {
                "forest_mass_wide", "forest_mass_wide", "forest_mass_deep", "forest_front_strip"
            };
            var massAnchors = plan.Obstacles.Select(placement => (Vector2)placement.Position).ToList();
            var massIndex = 0;
            for (var regionIndex = 0; regionIndex < massRegions.Length; regionIndex++)
            {
                var placed = 0;
                var attempts = massTargets[regionIndex] * 30;
                for (var attempt = 0; attempt < attempts && placed < massTargets[regionIndex]; attempt++)
                {
                    var region = massRegions[regionIndex];
                    var candidate = new Vector2(
                        random.Next(region.xMin, region.xMax) + Jitter(random, 5),
                        random.Next(region.yMin, region.yMax) + Jitter(random, 4));
                    var tooClose = false;
                    for (var anchorIndex = 0; anchorIndex < massAnchors.Count; anchorIndex++)
                    {
                        if ((massAnchors[anchorIndex] - candidate).sqrMagnitude < 16f)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose)
                    {
                        continue;
                    }

                    var key = massKeys[random.Next(massKeys.Length)];
                    var position = Snapped(candidate.x, candidate.y);
                    if (!TryAddObstacle(
                            assets,
                            plan,
                            key,
                            $"ForestMassScatter_{massIndex:000}",
                            position,
                            true))
                    {
                        continue;
                    }

                    massAnchors.Add(candidate);
                    massIndex++;
                    placed++;
                }
            }

            // 崖素材は回転せず、南東の高低差だけに限定して森林とは異なる輪郭を作る。
            TryAddObstacle(assets, plan, "cliff_straight_wide", "CliffSouthEast01", Snapped(35f, -19f), false);
            TryAddObstacle(assets, plan, "cliff_straight_wide", "CliffSouthEast02", Snapped(41.03125f, -19.0f), false);
            TryAddObstacle(assets, plan, "cliff_outer_corner", "CliffSouthEastCorner", Snapped(45f, -21f), false);
            TryAddObstacle(assets, plan, "cliff_straight_wide", "CliffWestShelf01", Snapped(-36f, 13f), false);
            TryAddObstacle(assets, plan, "cliff_outer_corner", "CliffWestShelfCorner", Snapped(-31f, 11f), false);

            var regions = new[]
            {
                new RectInt(-46, -31, 13, 19),
                new RectInt(-29, -34, 20, 13),
                new RectInt(8, -34, 18, 14),
                new RectInt(-46, 11, 15, 20),
                new RectInt(-26, 24, 17, 10),
                new RectInt(6, 18, 24, 15),
                new RectInt(-16, 7, 12, 12),
                new RectInt(11, -11, 12, 12)
            };
            var regionTargets = new[] { 18, 20, 18, 18, 12, 22, 12, 10 };
            var obstacleKeys = new[]
            {
                "tree_medium_01", "tree_medium_02", "tree_medium_03",
                "tree_medium_01", "tree_medium_02", "blocking_bush",
                "mossy_rock", "stump", "fallen_log"
            };

            var scatterIndex = 0;
            for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
            {
                var placed = 0;
                var attempts = regionTargets[regionIndex] * 18;
                for (var attempt = 0; attempt < attempts && placed < regionTargets[regionIndex]; attempt++)
                {
                    var region = regions[regionIndex];
                    var x = random.Next(region.xMin, region.xMax);
                    var y = random.Next(region.yMin, region.yMax);
                    var key = obstacleKeys[random.Next(obstacleKeys.Length)];
                    var position = Snapped(x + Jitter(random, 6), y + Jitter(random, 5));
                    if (!TryAddObstacle(
                            assets,
                            plan,
                            key,
                            $"ForestScatter_{scatterIndex:000}",
                            position,
                            false))
                    {
                        continue;
                    }

                    scatterIndex++;
                    placed++;
                }
            }
        }

        /// <summary>
        /// PrefabのMarkerから計画上の占有範囲を求め、表示と物理判定の方式を分けて追加する。
        /// </summary>
        private static bool TryAddObstacle(
            ReferenceAssets assets,
            ReferenceMapPlan plan,
            string prefabKey,
            string name,
            Vector3 position,
            bool allowObstacleOverlap)
        {
            if (!assets.VisualPrefabs.TryGetValue(prefabKey, out var prefab) || prefab == null)
            {
                throw new InvalidOperationException($"障害物Prefabがありません: {prefabKey}");
            }

            var marker = prefab.GetComponent<MapObstacleVisualMarker>();
            if (marker == null)
            {
                throw new InvalidOperationException($"障害物PrefabにFootprint Markerがありません: {prefabKey}");
            }

            var anchorCell = RoundToCell(position);
            var footprint = marker.GetFootprintCells(anchorCell).ToArray();
            for (var cellIndex = 0; cellIndex < footprint.Length; cellIndex++)
            {
                var cell = footprint[cellIndex];
                if (!IsInsideMap(cell)
                    || plan.ProtectedCells.Contains(cell)
                    || (!allowObstacleOverlap && plan.ObstacleFootprintCells.Contains(cell)))
                {
                    return false;
                }
            }

            plan.Obstacles.Add(new VisualPlacement(prefabKey, name, position));
            for (var cellIndex = 0; cellIndex < footprint.Length; cellIndex++)
            {
                plan.ObstacleFootprintCells.Add(footprint[cellIndex]);
                if (marker.CollisionMode == MapObstacleCollisionMode.TilemapFootprint)
                {
                    // CollisionBody方式はPrefab内の小さな接地判定を使うため、全CellをTilemapへ書き込まない。
                    plan.CollisionCells.Add(footprint[cellIndex]);
                }
            }

            return true;
        }

        /// <summary>
        /// 水際の葦と、最低間隔を持つ草花を自由配置し、Tile周期と一致しない地表密度を作る。
        /// </summary>
        private static void AddDecorationPlan(ReferenceMapPlan plan)
        {
            var decorationIndex = 0;
            for (var y = -17; y <= 20; y++)
            {
                if ((y + 17) % 3 != 0)
                {
                    continue;
                }

                var shoreCell = FindWestShoreCell(plan.WaterCells, y);
                if (!shoreCell.HasValue)
                {
                    continue;
                }

                var groundCell = shoreCell.Value + Vector3Int.left;
                if (plan.RoadCells.Contains(groundCell) || IsPlanningBlocked(plan, groundCell))
                {
                    continue;
                }

                plan.Decorations.Add(new VisualPlacement(
                    "reeds",
                    $"WaterReeds_{decorationIndex:00}",
                    Snapped(shoreCell.Value.x - 0.84375f, y + ((decorationIndex % 2 == 0) ? 0.0625f : -0.09375f))));
                decorationIndex++;
            }

            var random = new System.Random(LayoutSeed + 101);
            var acceptedPositions = new List<Vector2>();
            var keys = new[] { "tall_grass", "flower_patch", "wildflowers", "tall_grass", "tall_grass" };
            var attempts = 1800;
            while (attempts-- > 0 && acceptedPositions.Count < 105)
            {
                var cell = Cell(random.Next(XMin + 3, XMax - 2), random.Next(YMin + 3, YMax - 2));
                if (plan.WaterCells.Contains(cell)
                    || IsPlanningBlocked(plan, cell)
                    || plan.RoadCells.Contains(cell)
                    || HasNeighborIn(plan.RoadCells, cell, 1))
                {
                    continue;
                }

                var candidate = new Vector2(
                    cell.x + Jitter(random, 11),
                    cell.y + Jitter(random, 11));
                var isTooClose = false;
                for (var positionIndex = 0; positionIndex < acceptedPositions.Count; positionIndex++)
                {
                    if ((acceptedPositions[positionIndex] - candidate).sqrMagnitude < 5.75f)
                    {
                        isTooClose = true;
                        break;
                    }
                }

                if (isTooClose)
                {
                    continue;
                }

                acceptedPositions.Add(candidate);
                var key = keys[random.Next(keys.Length)];
                plan.Decorations.Add(new VisualPlacement(
                    key,
                    $"GroundDetail_{acceptedPositions.Count:000}",
                    Snapped(candidate.x, candidate.y)));
            }
        }

        /// <summary>
        /// 湖の西岸となる最小XのWater Cellを指定行から取得する。
        /// </summary>
        private static Vector3Int? FindWestShoreCell(ISet<Vector3Int> waterCells, int y)
        {
            for (var x = XMin; x <= XMax; x++)
            {
                var cell = Cell(x, y);
                if (waterCells.Contains(cell))
                {
                    return cell;
                }
            }

            return null;
        }

        /// <summary>
        /// 主要広場の報酬表示と、分岐を読むための看板、森際のキノコを配置する。
        /// </summary>
        private static void AddPropPlan(ReferenceMapPlan plan)
        {
            var props = new[]
            {
                new VisualPlacement("chest", "ChestCentralWest", Snapped(-4f, 3f)),
                new VisualPlacement("chest", "ChestCentralEast", Snapped(5f, -3f)),
                new VisualPlacement("chest", "ChestNorthShade", Snapped(-11f, 24f)),
                new VisualPlacement("chest", "ChestWaterfront", Snapped(26f, 12f)),
                new VisualPlacement("chest", "ChestSouthGrove", Snapped(-7f, -26f)),
                new VisualPlacement("chest", "ChestSouthEast", Snapped(34f, -25f)),
                new VisualPlacement("signpost", "SignWestEntry", Snapped(-37.125f, 4.90625f)),
                new VisualPlacement("signpost", "SignWestBranch", Snapped(-20.15625f, 4.875f)),
                new VisualPlacement("signpost", "SignCentralNorth", Snapped(2.125f, 10.9375f)),
                new VisualPlacement("signpost", "SignEastLoop", Snapped(22.875f, 12.90625f)),
                new VisualPlacement("signpost", "SignSouthBranch", Snapped(5.125f, -13.09375f)),
                new VisualPlacement("signpost", "SignSouthEast", Snapped(18.875f, -22.90625f)),
                new VisualPlacement("mushroom_patch", "MushroomsWest", Snapped(-33.15625f, -10.90625f)),
                new VisualPlacement("mushroom_patch", "MushroomsNorth", Snapped(-14.125f, 19.09375f)),
                new VisualPlacement("mushroom_patch", "MushroomsEast", Snapped(15.15625f, 16.0625f)),
                new VisualPlacement("mushroom_patch", "MushroomsSouth", Snapped(-23.09375f, -16.90625f))
            };

            plan.Props.AddRange(props);
        }

        /// <summary>
        /// Sceneへ触れる前に、地形連結、範囲、Spawn間隔、主要地点到達性を一括検証する。
        /// </summary>
        private static void ValidateReferencePlan(ReferenceMapPlan plan)
        {
            var expectedGroundCount = (XMax - XMin + 1) * (YMax - YMin + 1);
            if (plan.GroundCells.Count != expectedGroundCount)
            {
                throw new InvalidOperationException(
                    $"Ground Cell数が96x72ではありません: {plan.GroundCells.Count}");
            }

            if (!MapRoadPathRasterizer.IsSingleCardinalComponent(plan.RoadCells))
            {
                throw new InvalidOperationException("基準Mapの道路が4近傍で分断されています。");
            }

            if (!MapRoadPathRasterizer.IsSingleCardinalComponent(plan.WaterCells))
            {
                throw new InvalidOperationException("東の湖と水路が4近傍で分断されています。");
            }

            foreach (var roadCell in plan.RoadCells)
            {
                if (!plan.GroundCells.Contains(roadCell))
                {
                    throw new InvalidOperationException($"道路がMap範囲外です: {roadCell}");
                }

                if (plan.WaterCells.Contains(roadCell))
                {
                    throw new InvalidOperationException($"道路と水域が重複しています: {roadCell}");
                }

                if (IsPlanningBlocked(plan, roadCell))
                {
                    throw new InvalidOperationException($"道路にCollisionがあります: {roadCell}");
                }
            }

            foreach (var stoneCell in plan.StoneCells)
            {
                if (!plan.RoadCells.Contains(stoneCell))
                {
                    throw new InvalidOperationException($"石畳が道路形状から外れています: {stoneCell}");
                }
            }

            foreach (var collisionCell in plan.CollisionCells)
            {
                if (!plan.GroundCells.Contains(collisionCell))
                {
                    throw new InvalidOperationException($"CollisionがMap範囲外です: {collisionCell}");
                }
            }

            foreach (var footprintCell in plan.ObstacleFootprintCells)
            {
                if (!plan.GroundCells.Contains(footprintCell))
                {
                    throw new InvalidOperationException($"障害物占有範囲がMap範囲外です: {footprintCell}");
                }
            }

            ValidateSocketPlan(plan);
            ValidateKeyLocationReachability(plan);
            ValidateUniqueNames(plan);
        }

        /// <summary>
        /// 参加者数、開始地点間隔、Socketの範囲・非衝突を検証する。
        /// </summary>
        private static void ValidateSocketPlan(ReferenceMapPlan plan)
        {
            var participants = plan.Sockets
                .Where(socket => socket.Kind == MapSocketKind.PlayerStart || socket.Kind == MapSocketKind.CpuStart)
                .ToArray();
            if (participants.Length != ExpectedParticipantCount)
            {
                throw new InvalidOperationException(
                    $"参加者開始Socketは{ExpectedParticipantCount}個必要です: {participants.Length}");
            }

            for (var socketIndex = 0; socketIndex < plan.Sockets.Count; socketIndex++)
            {
                var socket = plan.Sockets[socketIndex];
                var cell = RoundToCell(socket.Position);
                if (!plan.GroundCells.Contains(cell))
                {
                    throw new InvalidOperationException($"SocketがMap範囲外です: {socket.Name} / {cell}");
                }

                if (IsPlanningBlocked(plan, cell))
                {
                    throw new InvalidOperationException($"SocketがCollision上にあります: {socket.Name} / {cell}");
                }
            }

            for (var firstIndex = 0; firstIndex < participants.Length; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < participants.Length; secondIndex++)
                {
                    var delta = participants[firstIndex].Position - participants[secondIndex].Position;
                    if (delta.sqrMagnitude < 18f * 18f)
                    {
                        throw new InvalidOperationException(
                            $"開始地点間隔が18Cell未満です: {participants[firstIndex].Name} / {participants[secondIndex].Name}");
                    }
                }
            }
        }

        /// <summary>
        /// 最初の開始地点から全参加者・Landmark・Merchantへ、Collisionを避けて到達できることを確認する。
        /// </summary>
        private static void ValidateKeyLocationReachability(ReferenceMapPlan plan)
        {
            var start = RoundToCell(plan.Sockets.First(socket => socket.Kind == MapSocketKind.PlayerStart).Position);
            var visited = FloodWalkableCells(plan, start);

            for (var socketIndex = 0; socketIndex < plan.Sockets.Count; socketIndex++)
            {
                var socket = plan.Sockets[socketIndex];
                if (socket.Kind != MapSocketKind.PlayerStart
                    && socket.Kind != MapSocketKind.CpuStart
                    && socket.Kind != MapSocketKind.Landmark
                    && socket.Kind != MapSocketKind.Merchant)
                {
                    continue;
                }

                var cell = RoundToCell(socket.Position);
                if (!visited.Contains(cell))
                {
                    throw new InvalidOperationException($"主要Socketへ到達できません: {socket.Name} / {cell}");
                }
            }
        }

        /// <summary>
        /// GroundからTilemap Collisionと障害物占有範囲を除いた4近傍を走査し、歩行可能領域を返す。
        /// </summary>
        private static HashSet<Vector3Int> FloodWalkableCells(ReferenceMapPlan plan, Vector3Int start)
        {
            var visited = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current)
                    || !plan.GroundCells.Contains(current)
                    || IsPlanningBlocked(plan, current))
                {
                    continue;
                }

                visited.Add(current);
                for (var directionIndex = 0; directionIndex < CardinalDirections.Length; directionIndex++)
                {
                    queue.Enqueue(current + CardinalDirections[directionIndex]);
                }
            }

            return visited;
        }

        /// <summary>
        /// Scene Hierarchyで曖昧にならないよう、全自由配置ObjectとSocketの名前重複を検出する。
        /// </summary>
        private static void ValidateUniqueNames(ReferenceMapPlan plan)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in plan.Decorations.Concat(plan.Obstacles).Concat(plan.Props))
            {
                if (!names.Add(placement.Name))
                {
                    throw new InvalidOperationException($"表示Instance名が重複しています: {placement.Name}");
                }
            }

            for (var socketIndex = 0; socketIndex < plan.Sockets.Count; socketIndex++)
            {
                if (!names.Add(plan.Sockets[socketIndex].Name))
                {
                    throw new InvalidOperationException($"Socket名が重複しています: {plan.Sockets[socketIndex].Name}");
                }
            }
        }

        /// <summary>
        /// 検証済み計画をTemplate複製へ一括反映し、Validator成功後だけ基準Sceneとして保存する。
        /// </summary>
        private static void WriteReferenceScene(ReferenceAssets assets, ReferenceMapPlan plan)
        {
            var previousActiveScene = SceneManager.GetActiveScene();
            var referenceAssetExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ReferenceScenePath) != null;
            var referenceWasLoaded = TryGetLoadedScene(ReferenceScenePath, out var referenceScene);
            var reusedUntitledScene = false;
            if (!referenceWasLoaded)
            {
                if (referenceAssetExists)
                {
                    referenceScene = EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Additive);
                }
                else if (previousActiveScene.IsValid()
                         && previousActiveScene.isLoaded
                         && string.IsNullOrEmpty(previousActiveScene.path)
                         && SceneManager.sceneCount == 1
                         && (Application.isBatchMode || !previousActiveScene.isDirty))
                {
                    // Batchmode起動直後の空Sceneを先に保存し、未保存Sceneがある状態でのAdditive作成失敗を避ける。
                    referenceScene = previousActiveScene;
                    reusedUntitledScene = true;
                    if (!EditorSceneManager.SaveScene(referenceScene, ReferenceScenePath))
                    {
                        throw new InvalidOperationException(
                            $"空Sceneを基準Mapの保存先へ初期化できません: {ReferenceScenePath}");
                    }
                }
                else
                {
                    referenceScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                }
            }

            var templateWasLoaded = TryGetLoadedScene(GbaForestMapAuthoringSceneBuilder.ScenePath, out var templateScene);
            if (!templateWasLoaded)
            {
                templateScene = EditorSceneManager.OpenScene(
                    GbaForestMapAuthoringSceneBuilder.ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                var eventSocketOverrides = CaptureEventSocketAuthoringOverrides(referenceScene);
                ClearScene(referenceScene);
                var templateRoot = FindRoot(templateScene, "MapRoot");
                var mapRoot = Object.Instantiate(templateRoot);
                mapRoot.name = "MapRoot";
                SceneManager.MoveGameObjectToScene(mapRoot, referenceScene);
                SceneManager.SetActiveScene(referenceScene);

                ApplyPlanToScene(
                    mapRoot.transform,
                    referenceScene,
                    assets,
                    plan,
                    eventSocketOverrides);
                var report = MapAuthoringValidator.ValidateScene(referenceScene, true);
                if (report.HasErrors)
                {
                    var codes = report.Issues
                        .Where(issue => issue.Severity == MapAuthoringIssueSeverity.Error)
                        .Select(issue => issue.Code)
                        .ToArray();
                    throw new InvalidOperationException(
                        $"基準MapのMap Authoring検証に失敗しました: {string.Join(", ", codes)}");
                }

                EditorSceneManager.MarkSceneDirty(referenceScene);
                if (!EditorSceneManager.SaveScene(referenceScene, ReferenceScenePath))
                {
                    throw new InvalidOperationException($"基準Map Sceneを保存できません: {ReferenceScenePath}");
                }

                AssetDatabase.ImportAsset(ReferenceScenePath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (!referenceWasLoaded
                    && !reusedUntitledScene
                    && referenceScene.IsValid()
                    && referenceScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(referenceScene, true);
                }

                if (!templateWasLoaded && templateScene.IsValid() && templateScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(templateScene, true);
                }
            }
        }

        /// <summary>
        /// 4 Tilemap、3つの自由配置Root、Sockets、Cameraへ計画内容を反映する。
        /// </summary>
        private static void ApplyPlanToScene(
            Transform mapRoot,
            Scene scene,
            ReferenceAssets assets,
            ReferenceMapPlan plan,
            IReadOnlyDictionary<string, EventSocketAuthoringOverride> eventSocketOverrides)
        {
            var ground = RequireTilemap(mapRoot, "Grid/GroundTilemap");
            var terrain = RequireTilemap(mapRoot, "Grid/TerrainTilemap");
            var collision = RequireTilemap(mapRoot, "Grid/CollisionTilemap");
            var foreground = RequireTilemap(mapRoot, "Grid/ForegroundTilemap");
            var decorationsRoot = RequireChild(mapRoot, "Decorations");
            var obstacleVisualsRoot = RequireChild(mapRoot, "ObstacleVisuals");
            var propsRoot = RequireChild(mapRoot, "Props");
            var socketsRoot = RequireChild(mapRoot, "Sockets");

            ground.ClearAllTiles();
            terrain.ClearAllTiles();
            collision.ClearAllTiles();
            foreground.ClearAllTiles();
            RemoveAllChildren(decorationsRoot);
            RemoveAllChildren(obstacleVisualsRoot);
            RemoveAllChildren(propsRoot);
            RemoveAllChildren(socketsRoot);

            SetTiles(ground, plan.GroundCells, assets.GrassTile);
            SetTiles(terrain, plan.RoadCells, assets.DirtRuleTile);
            SetTiles(terrain, plan.StoneCells, assets.StoneRuleTile);
            SetTiles(terrain, plan.WaterCells, assets.WaterRuleTile);
            SetTiles(collision, plan.CollisionCells, assets.CollisionTile);

            for (var index = 0; index < plan.Decorations.Count; index++)
            {
                PlaceVisual(assets, plan.Decorations[index], decorationsRoot, scene);
            }

            for (var index = 0; index < plan.Obstacles.Count; index++)
            {
                PlaceVisual(assets, plan.Obstacles[index], obstacleVisualsRoot, scene);
            }

            for (var index = 0; index < plan.Props.Count; index++)
            {
                PlaceVisual(assets, plan.Props[index], propsRoot, scene);
            }

            for (var index = 0; index < plan.Sockets.Count; index++)
            {
                CreateSocket(
                    socketsRoot,
                    plan.Sockets[index],
                    eventSocketOverrides);
            }

            var camera = mapRoot.GetComponentInChildren<Camera>(true);
            if (camera == null)
            {
                throw new InvalidOperationException("TemplateにCameraがありません。");
            }

            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 37.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(20, 32, 28, 255);
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.up;

            ground.CompressBounds();
            terrain.CompressBounds();
            collision.CompressBounds();
            foreground.CompressBounds();
        }

        /// <summary>
        /// 指定Cell集合を座標順に並べ、同じTile参照として一括設定する。
        /// </summary>
        private static void SetTiles(Tilemap tilemap, IEnumerable<Vector3Int> sourceCells, TileBase tile)
        {
            var cells = sourceCells.OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToArray();
            var tiles = new TileBase[cells.Length];
            for (var index = 0; index < tiles.Length; index++)
            {
                tiles[index] = tile;
            }

            tilemap.SetTiles(cells, tiles);
        }

        /// <summary>
        /// Prefab接続を保ったInstanceを生成し、自由配置契約のRotation 0・Scale 1へ揃える。
        /// </summary>
        private static void PlaceVisual(
            ReferenceAssets assets,
            VisualPlacement placement,
            Transform parent,
            Scene scene)
        {
            if (!assets.VisualPrefabs.TryGetValue(placement.PrefabKey, out var prefab) || prefab == null)
            {
                throw new InvalidOperationException($"表示Prefabがありません: {placement.PrefabKey}");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"表示Prefabを配置できません: {placement.PrefabKey}");
            }

            instance.name = placement.Name;
            instance.transform.SetParent(parent, true);
            instance.transform.position = placement.Position;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Runtime処理を持たない制作MarkerをSockets配下へ生成する。
        /// </summary>
        private static void CreateSocket(
            Transform parent,
            SocketPlacement placement,
            IReadOnlyDictionary<string, EventSocketAuthoringOverride> eventSocketOverrides)
        {
            var socketObject = new GameObject(placement.Name);
            socketObject.transform.SetParent(parent, false);
            var authoringOverride = default(EventSocketAuthoringOverride);
            var hasAuthoringOverride = placement.Kind == MapSocketKind.Event
                && eventSocketOverrides != null
                && eventSocketOverrides.TryGetValue(placement.Name, out authoringOverride);
            socketObject.transform.position = hasAuthoringOverride
                ? authoringOverride.Position
                : placement.Position;
            socketObject.transform.rotation = Quaternion.identity;
            socketObject.transform.localScale = Vector3.one;
            socketObject.layer = ResolveLayer("MapInteraction");
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(
                placement.Kind,
                new Vector2(0.8f, 0.8f),
                placement.Facing,
                placement.Tag,
                placement.Name,
                null,
                null,
                placement.Kind == MapSocketKind.Event
                    ? hasAuthoringOverride
                        ? authoringOverride.InteractionRadiusOverride > 0f
                            ? authoringOverride.InteractionRadiusOverride
                            : MapEventDefinition.DefaultInteractionRadiusValue
                        : MapEventDefinition.DefaultInteractionRadiusValue
                    : 0f,
                placement.Kind == MapSocketKind.Event
                    ? MapEventPlacementMode.PoolCandidate
                    : MapEventPlacementMode.FixedDefinition);
        }

        /// <summary>
        /// 保存済みReference SceneのEventSocketをSocket IDで収集し、再生成後も手動位置・半径を維持する。
        /// </summary>
        private static Dictionary<string, EventSocketAuthoringOverride> CaptureEventSocketAuthoringOverrides(
            Scene referenceScene)
        {
            var result = new Dictionary<string, EventSocketAuthoringOverride>(StringComparer.Ordinal);
            if (!referenceScene.IsValid() || !referenceScene.isLoaded)
            {
                return result;
            }

            var roots = referenceScene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var markers = roots[rootIndex].GetComponentsInChildren<MapSocketMarker>(true);
                for (var markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                {
                    var marker = markers[markerIndex];
                    if (marker.SocketKind != MapSocketKind.Event
                        || string.IsNullOrWhiteSpace(marker.SocketId)
                        || !IsFinite(marker.transform.position.x)
                        || !IsFinite(marker.transform.position.y)
                        || !IsFinite(marker.transform.position.z))
                    {
                        continue;
                    }

                    result[marker.SocketId.Trim()] = new EventSocketAuthoringOverride(
                        marker.transform.position,
                        marker.InteractionRadiusOverride);
                }
            }

            return result;
        }

        /// <summary>
        /// Sceneから退避するWorld座標がNaNやInfinityを含まないか確認する。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 指定構図をPoint FilterのRenderTextureで二度描画し、Unity Import後のPNGとして保存する。
        /// </summary>
        private static void CaptureFrameToPng(Transform root, Camera camera, CaptureFrame frame)
        {
            RenderTexture renderTexture = null;
            Texture2D capture = null;
            var previousActive = RenderTexture.active;
            try
            {
                renderTexture = new RenderTexture(frame.Width, frame.Height, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point
                };
                renderTexture.Create();
                if (!renderTexture.IsCreated())
                {
                    throw new InvalidOperationException($"RenderTextureを作成できません: {frame.Path}");
                }

                var tilemaps = root.GetComponentsInChildren<Tilemap>(true);
                for (var index = 0; index < tilemaps.Length; index++)
                {
                    tilemaps[index].RefreshAllTiles();
                }

                camera.transform.position = new Vector3(frame.Center.x, frame.Center.y, -10f);
                camera.transform.rotation = Quaternion.identity;
                camera.orthographic = true;
                camera.orthographicSize = frame.OrthographicSize;
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.Render();

                RenderTexture.active = renderTexture;
                capture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, frame.Width, frame.Height), 0, 0);
                capture.Apply(false, false);

                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                {
                    throw new InvalidOperationException("Project Rootを取得できません。");
                }

                var absolutePath = Path.Combine(projectRoot, frame.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? projectRoot);
                File.WriteAllBytes(absolutePath, capture.EncodeToPNG());
                AssetDatabase.ImportAsset(
                    frame.Path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                if (capture != null)
                {
                    Object.DestroyImmediate(capture);
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }
            }
        }

        /// <summary>
        /// 基準Mapが消費する既存TileとPrefabを型付きで読み込み、欠落時は生成を止める。
        /// </summary>
        private static ReferenceAssets LoadReferenceAssets()
        {
            var assets = new ReferenceAssets
            {
                GrassTile = LoadRequired<GroundVariationTile>(
                    $"{MapAuthoringKitBuilder.TileAssetRoot}/Ground/GrassVariation.asset"),
                DirtRuleTile = LoadRequired<RoadConnectionRuleTile>(
                    $"{MapAuthoringKitBuilder.TileAssetRoot}/Rules/DirtRuleTile.asset"),
                StoneRuleTile = LoadRequired<RoadConnectionRuleTile>(
                    $"{MapAuthoringKitBuilder.TileAssetRoot}/Rules/StoneRuleTile.asset"),
                WaterRuleTile = LoadRequired<RuleTile>(
                    $"{MapAuthoringKitBuilder.TileAssetRoot}/Rules/WaterRuleTile.asset"),
                CollisionTile = LoadRequired<Tile>(
                    $"{MapAuthoringKitBuilder.TileAssetRoot}/Utility/CollisionTile.asset")
            };

            AddPrefab(assets, "tall_grass", "TallGrass");
            AddPrefab(assets, "flower_patch", "FlowerPatch");
            AddPrefab(assets, "wildflowers", "Wildflowers");
            AddPrefab(assets, "reeds", "Reeds");
            AddPrefab(assets, "forest_mass_wide", "ForestMassWide");
            AddPrefab(assets, "forest_mass_deep", "ForestMassDeep");
            AddPrefab(assets, "forest_front_strip", "ForestFrontStrip");
            AddPrefab(assets, "cliff_straight_wide", "CliffStraightWide");
            AddPrefab(assets, "cliff_outer_corner", "CliffOuterCorner");
            AddPrefab(assets, "tree_medium_01", "TreeMedium01");
            AddPrefab(assets, "tree_medium_02", "TreeMedium02");
            AddPrefab(assets, "tree_medium_03", "TreeMedium03");
            AddPrefab(assets, "blocking_bush", "BlockingBush");
            AddPrefab(assets, "mossy_rock", "MossyRock");
            AddPrefab(assets, "stump", "Stump");
            AddPrefab(assets, "fallen_log", "FallenLog");
            AddPrefab(assets, "mushroom_patch", "MushroomPatch");
            AddPrefab(assets, "chest", "Chest");
            AddPrefab(assets, "signpost", "Signpost");
            return assets;
        }

        /// <summary>
        /// Prefabファイル名を論理Keyへ対応付け、計画側をAsset pathから独立させる。
        /// </summary>
        private static void AddPrefab(ReferenceAssets assets, string key, string fileName)
        {
            assets.VisualPrefabs.Add(
                key,
                LoadRequired<GameObject>($"{MapAuthoringKitBuilder.PrefabRoot}/{fileName}.prefab"));
        }

        /// <summary>
        /// Assetを必須参照として読み込み、型違いや欠落を早い段階で報告する。
        /// </summary>
        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException($"必要なMap Authoring Assetがありません: {path}", path);
            }

            return asset;
        }

        /// <summary>
        /// Templateが未作成の場合だけ標準Builderを呼び、既存Sceneを不用意に更新しない。
        /// </summary>
        private static void EnsureTemplateExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GbaForestMapAuthoringSceneBuilder.ScenePath) != null)
            {
                return;
            }

            GbaForestMapAuthoringSceneBuilder.BuildOrRefreshTemplate();
        }

        /// <summary>
        /// SceneとQA画像の保存先だけを再帰的に作成する。
        /// </summary>
        private static void EnsureRequiredFolders()
        {
            EnsureAssetFolder("Assets/Scenes/MapAuthoring");
            EnsureAssetFolder("Assets/Art/Generated/MapAuthoring/QA");
        }

        /// <summary>
        /// AssetDatabase管理下のFolderを親から順に作る。
        /// </summary>
        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var separatorIndex = path.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                throw new ArgumentException($"Invalid asset folder path: {path}", nameof(path));
            }

            var parent = path.Substring(0, separatorIndex);
            var name = path.Substring(separatorIndex + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>
        /// Sceneが既にLoad済みかをpathで判定する。
        /// </summary>
        private static bool TryGetLoadedScene(string path, out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>
        /// 再生成対象Sceneの旧Rootをすべて除去し、Instanceが積み重ならないようにする。
        /// </summary>
        private static void ClearScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var index = roots.Length - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(roots[index]);
            }
        }

        /// <summary>
        /// Scene直下の必須Rootを一意に取得する。
        /// </summary>
        private static GameObject FindRoot(Scene scene, string name)
        {
            var matches = scene.GetRootGameObjects().Where(root => root.name == name).ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Scene Root {name} は1個必要です: scene={scene.path}, count={matches.Length}");
            }

            return matches[0];
        }

        /// <summary>
        /// MapRootから指定pathのTilemapを必須取得する。
        /// </summary>
        private static Tilemap RequireTilemap(Transform root, string path)
        {
            var child = root.Find(path);
            var tilemap = child != null ? child.GetComponent<Tilemap>() : null;
            if (tilemap == null)
            {
                throw new InvalidOperationException($"TemplateにTilemapがありません: {path}");
            }

            return tilemap;
        }

        /// <summary>
        /// MapRootから指定pathの配置Rootを必須取得する。
        /// </summary>
        private static Transform RequireChild(Transform root, string path)
        {
            var child = root.Find(path);
            if (child == null)
            {
                throw new InvalidOperationException($"TemplateにRootがありません: {path}");
            }

            return child;
        }

        /// <summary>
        /// 専用配置Root内の旧Instanceをすべて除去する。
        /// </summary>
        private static void RemoveAllChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(parent.GetChild(index).gameObject);
            }
        }

        /// <summary>
        /// Layer未定義時はDefaultへ戻し、制作環境の差で生成を止めない。
        /// </summary>
        private static int ResolveLayer(string requestedLayer)
        {
            var layer = LayerMask.NameToLayer(requestedLayer);
            return layer >= 0 ? layer : LayerMask.NameToLayer("Default");
        }

        /// <summary>
        /// 中心Cellの周囲へ正方形範囲を追加する。
        /// </summary>
        private static void AddSquare(ISet<Vector3Int> destination, Vector3Int center, int radius)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    destination.Add(center + new Vector3Int(x, y, 0));
                }
            }
        }

        /// <summary>
        /// 元Cell集合を指定半径だけ膨張して追加する。
        /// </summary>
        private static void AddExpandedCells(
            ISet<Vector3Int> destination,
            IEnumerable<Vector3Int> source,
            int radius)
        {
            foreach (var cell in source)
            {
                AddSquare(destination, cell, radius);
            }
        }

        /// <summary>
        /// 指定Cellの周辺に対象集合があるかを判定する。
        /// </summary>
        private static bool HasNeighborIn(ISet<Vector3Int> cells, Vector3Int center, int radius)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (cells.Contains(center + new Vector3Int(x, y, 0)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Cellが実際のTilemap判定またはCollisionBodyを持つ障害物の計画上占有範囲かを判定する。
        /// </summary>
        private static bool IsPlanningBlocked(ReferenceMapPlan plan, Vector3Int cell)
        {
            return plan.CollisionCells.Contains(cell) || plan.ObstacleFootprintCells.Contains(cell);
        }

        /// <summary>
        /// Cellが96x72のGround範囲内かを判定する。
        /// </summary>
        private static bool IsInsideMap(Vector3Int cell)
        {
            return cell.x >= XMin && cell.x <= XMax && cell.y >= YMin && cell.y <= YMax;
        }

        /// <summary>
        /// 2D整数座標をz=0のCellへ変換する。
        /// </summary>
        private static Vector3Int Cell(int x, int y)
        {
            return new Vector3Int(x, y, 0);
        }

        /// <summary>
        /// 自由配置座標を1/32 unitへ丸める。
        /// </summary>
        private static Vector3 Snapped(float x, float y)
        {
            return new Vector3(
                Mathf.Round(x * 32f) / 32f,
                Mathf.Round(y * 32f) / 32f,
                0f);
        }

        /// <summary>
        /// 自由配置座標に短周期でない1px単位の小さな揺らぎを返す。
        /// </summary>
        private static float Jitter(System.Random random, int maxPixels)
        {
            return random.Next(-maxPixels, maxPixels + 1) / 32f;
        }

        /// <summary>
        /// 自由配置Anchorを最寄りの論理Cellへ対応付ける。
        /// </summary>
        private static Vector3Int RoundToCell(Vector3 position)
        {
            return Cell(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        }
    }
}
