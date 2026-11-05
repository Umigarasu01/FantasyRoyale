using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>Scene生成前の読取り診断。Planが成立しても画質・実歩行の検証完了を意味しない。</summary>
    public sealed class OverheadStudyAPlanReadiness
    {
        public OverheadStudyAReadiness Assets;
        public OverheadStudyAPlan Plan;
        public string SettingsJson;
        public string LayoutHash;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool CanProceedToSceneImplementation => Errors.Count == 0 && Assets != null
            && Assets.IsReady && Assets.VisuallyReviewed && Plan != null;
        public bool SceneGenerated => false;

        public override string ToString()
        {
            var lines = new List<string>
            {
                "OverheadStudyA preflight: ready=" + CanProceedToSceneImplementation + ", sceneGenerated=false",
                "24x16 world / provisional 64px per world unit / viewport 1536x1024",
                "Input fingerprint: " + (Assets?.Fingerprint ?? "unavailable"),
                "Layout hash: " + (LayoutHash ?? "not generated")
            };
            lines.AddRange(Errors.Select(message => "ERROR: " + message));
            lines.AddRange(Warnings.Select(message => "WARNING: " + message));
            if (Plan != null)
            {
                lines.Add($"seed={Plan.Seed}, soil={Plan.SoilCells.Count}, road={Plan.RoadCells.Count}, water={Plan.WaterCells.Count}, bridge={Plan.BridgeCells.Count}");
                lines.AddRange(Plan.Placements.GroupBy(item => item.Asset.Role).OrderBy(group => group.Key)
                    .Select(group => group.Key + " placements=" + group.Count()));
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Aの独立したPlan入力準備だけを行う。Scene・派生Asset・QAの保存処理は未実装。
    /// CheckReadinessを最初に実行し、原本/Derivedの明示レビューが成立するまでPlanにも進まない。
    /// 原本import、旧Builder呼出し、Scene切替、Project設定変更はこの入口で一切行わない。
    /// </summary>
    public static class OverheadStudyABuilder
    {
        public const string SceneRoot = "Assets/Scenes/MapAuthoring/OverheadStudyA";
        public const int ViewportWidth = 1536, ViewportHeight = 1024;
        public static readonly Vector3 CameraPosition = new Vector3(12f, 8f, -10f);
        public const float CameraOrthographicSize = 8f;

        [MenuItem("FantasyRoyale/Map Authoring/Overhead Study A/Check Plan Readiness (Read Only)")]
        public static void CheckPlanReadinessMenu()
        {
            var report = PreparePlan();
            if (report.CanProceedToSceneImplementation) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <summary>Missing/unapproved/unreviewed入力に対して、ファイルとSceneを変更せず診断を返す。</summary>
        public static OverheadStudyAPlanReadiness PreparePlan(OverheadStudyASettings settings = null)
        {
            settings = settings ?? new OverheadStudyASettings();
            // Guardは出力先作成・Scene操作・Spriteロード・Plan処理より先。これはread-only API。
            var assets = OverheadStudyAAssets.CheckReadiness(settings);
            var report = new OverheadStudyAPlanReadiness { Assets = assets, SettingsJson = JsonUtility.ToJson(settings, true) };
            report.Errors.AddRange(assets.Errors);
            report.Warnings.AddRange(assets.Warnings);
            if (!assets.IsReady) return report;
            if (!assets.VisuallyReviewed)
            {
                report.Errors.Add("原本/Derivedのvisual reviewが未成立です。Scene/派生Asset/QAは保存しません。");
                return report;
            }
            if (settings.WorldWidth != OverheadStudyAPlan.Width || settings.WorldHeight != OverheadStudyAPlan.Height)
                report.Errors.Add("初回Aの一画面検証Profileは24x16です。別サイズは別Profileとして設計してください。");
            if (settings.PixelsPerUnit != 64f)
                report.Errors.Add("初回Aの粒度比較ProfileはPPU64です。既存PPU32設定を変更しません。");
            if (report.Errors.Count != 0) return report;
            try
            {
                var definitions = assets.AssetInfos.Select(info => new OverheadStudyAPlan.Art
                {
                    Key = info.Definition.name,
                    Role = info.Definition.role,
                    WorldSize = info.WorldSize,
                    Pivot = info.Pivot,
                    Footprint = info.LocalFootprint,
                    Collider = info.LocalCollider,
                    WalkFloor = info.LocalWalkFloor
                });
                report.Plan = OverheadStudyAPlan.Generate(settings.Seed, definitions);
                report.Errors.AddRange(report.Plan.Validate());
                report.LayoutHash = report.Plan.StableHash();
                report.Warnings.Add("この入口はPlan準備までです。独立Scene/Tilemap/Collision/QAの保存処理は未実装です。");
                report.Warnings.Add("Heroは静止比較の予定です。Actorの実移動/Physics2D/遮蔽/画質はまだ検証していません。");
                report.Warnings.Add("1Cell主道と橋の検査は足元Boundsの標本点とCell到達性です。連続移動の検査は別途必要です。");
            }
            catch (Exception exception) { report.Errors.Add("A Plan準備失敗: " + exception.Message); }
            return report;
        }
    }
}
