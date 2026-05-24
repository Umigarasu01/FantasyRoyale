# 実装状況

## 概要

現時点では Unity プロジェクトの初期状態。
ゲーム仕様と実装方針は企画メモとして整理を開始した段階。

## Game Design

| 項目 | 状況 | メモ |
| --- | --- | --- |
| 初期コンセプト | 整理中 | [[../GameDesign/FantasyRoyaleConcept|FantasyRoyale 企画メモ]] を作成。 |
| 1試合の基本ループ | 仮実装中 | Step 1では固定小マップの探索、敵撃破、宝箱開封まで。 |
| 強化ルート | 仮実装中 | 敵撃破によるコイン、宝箱アイテムによる攻撃力/回復/コインを実装。 |
| 伝説級アイテム | 仮実装中 | 宝箱から低確率で伝説級アイテムが出る仮抽選を実装。 |

## Unity Implementation

| 項目 | 状況 | メモ |
| --- | --- | --- |
| Core ロジック | 仮実装 | `PrototypeItem` と `PrototypeHealth` を追加。将来のCore分離候補。 |
| プレイヤー操作 | 仮実装 | `PrototypePlayerController2D` でキーボード移動、近接攻撃、Eキー操作を実装。 |
| マップ | 仮実装 | Editor生成ツールで固定小マップを作成予定。 |
| 戦闘 | 仮実装 | 前方範囲攻撃、敵の追跡、接触ダメージを実装。斬撃の見た目に合わせて根元と先端を判定する。 |
| アイテム | 仮実装 | 宝箱抽選で通常、レア、伝説級アイテムを取得。 |
| UI | 仮実装 | `PrototypeHud` でHP、コイン、攻撃力、直近アイテム、ログを表示。 |
| 仮ビジュアル | 仮実装 | Editor生成のピクセル風スプライトと `PrototypeSpriteAnimator` による簡易アニメーションを追加。 |
| 攻撃エフェクト | 仮実装 | `PrototypeAttackEffect` で攻撃時に斬撃スプライトを短時間表示。既存Sceneでも1発撃破になるよう、敵起動時にプロトタイプHPを上書きする。 |

## 確認状況

| 項目 | 状況 | メモ |
| --- | --- | --- |
| C# コンパイル | 確認済み | `dotnet build Assembly-CSharp.csproj --no-restore` と `dotnet build Assembly-CSharp-Editor.csproj --no-restore` が警告0、エラー0で成功。 |
| Scene生成 | 未確認 | `FantasyRoyale/Build Solo Prototype Scene` メニュー、またはbatchmodeで生成予定。 |
| 実プレイ | 未確認 | Unity上で再生確認が必要。 |
