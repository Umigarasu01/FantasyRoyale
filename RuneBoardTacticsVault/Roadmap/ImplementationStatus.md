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
| マップ | 仮実装 | 5-6人規模のバトロワを想定した広域マップへ拡張。草原、雪原、火山地帯、溶岩、中央街道を仮配置。 |
| 戦闘 | 仮実装 | 前方範囲攻撃、敵の追跡、接触ダメージを実装。斬撃の見た目に合わせて根元と先端を判定する。 |
| アイテム | 仮実装 | 宝箱抽選で通常、レア、伝説級アイテムを取得。 |
| UI | 仮実装 | `PrototypeHud` でHP、コイン、攻撃力、直近アイテム、ログを表示。 |
| 仮ビジュアル | 仮実装 | Editor生成のピクセル風スプライトと `PrototypeSpriteAnimator` による簡易アニメーションを追加。 |
| 攻撃エフェクト | 仮実装 | `PrototypeAttackEffect` で攻撃時に斬撃スプライトを短時間表示。既存Sceneでも1発撃破になるよう、敵起動時にプロトタイプHPを上書きする。 |
| 商人 | 仮実装 | `PrototypeMerchant` で10コイン購入、コイン不足表示、ランダム商品入手を実装。生成Sceneでは設置物を商人のみにして複数配置。 |
| カメラ | 仮実装 | `PrototypeCameraFollow` で広域マップ探索用のプレイヤー追従を実装。 |

## 確認状況

| 項目 | 状況 | メモ |
| --- | --- | --- |
| C# コンパイル | 確認済み | `dotnet build Assembly-CSharp.csproj --no-restore` と `dotnet build Assembly-CSharp-Editor.csproj --no-restore` が警告0、エラー0で成功。 |
| Scene生成 | 未確認 | Unityがプロジェクトを開いていたためbatchmode生成は未実行。Unity Editor上で `FantasyRoyale/Build Solo Prototype Scene` を実行する。 |
| 実プレイ | 未確認 | Unity上で再生確認が必要。 |
## 2026-05-24 追記

- 地形当たり判定: 仮実装。木、岩、溶岩、外周壁に `BoxCollider2D` を付け、広域マップで障害物を避けて移動する確認ができる。
- 仮素材管理: コード生成PNGを `Assets/Art/Prototype/Generated/` に隔離。外部素材候補は `RuneBoardTacticsVault/Dev/Assets/ExternalAssets.md` に記録。
- 仮グラフィック刷新: 参考画像の方向性に合わせ、オリジナルのトップダウン・ピクセル素材を36点作成。キャラ、敵、商人、建物、複数ロケーション用タイルと装飾を含む。
- 複雑マップ: `PrototypeSceneBuilder` に森林集落、雪原拠点、火山キャンプ、遺跡、水辺、石畳道の配置を追加。Unityが起動中のためScene再生成はUnityメニュー実行待ち。
- 仮素材細密化: 36点の仮PNGを32px基準へ更新。UnityインポートPPUも32へ変更し、表示サイズを維持しながらドット密度を上げた。
- ビジュアル方向調整: 参考画像の世界観と見下ろし角度を反映し、キャラの頭身、接地影、建物の屋根/前面、樹冠、地面装飾を再調整した。
