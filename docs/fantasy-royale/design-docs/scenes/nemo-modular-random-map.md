# Nemo ランダムマップ v8 — ビジュアル仕上げ

Unity 6000.4.3f1、元プロジェクト `C:/Users/umiga/Repository/FantasyRoyale`。試遊Sceneは `Assets/NemoBlocks/Scenes/NemoRandomMap_WalkRegions.unity`。保存済みseed17・42・99・20261011は `Assets/NemoBlocks/Scenes/RiverSeeds/`。

## 操作
WASD／矢印で移動、Cで通常／全景、Gで青・黄の割り当て区画、Hで緑の通行輪郭。両表示は独立・既定非表示。Bは近い橋の除去復帰、Bridge viewは橋へ移動。Nは次seed、入力欄とGenerateは指定生成、Rは中央、Tabは次の場所、K／LまたはSave／Loadは保存再読込。Fキーは不要。保存先は `UserRuns/NemoBlocks/map-save.json`。

Standard、144×108、中央大エリア1＋外側3、64PPU・Scale1、通常カメラ従来比1.5倍を保持。別カードの内部ビジュアル変更は今回行っていない。

## 外周とデザインの順序
既存ブロックの施設・道・庭・森を保持する。`NemoExteriorForest` が同じ223px樹木を使い、32マスの奥行きで外の森を延長する。規則的な小木のフェンスにはしない。道路端と入口にはプレイヤーのカプセル余白を残す。既存の川と両岸は画面外32マスまでの延長を保持する。

内部と外側のデザインを固定した後、原画像alpha・実際の道路Mesh・水面から `NemoWalkRegion` を作り、`NemoBoundary` が最後にColliderを作る。v6で水面近くの頂点を省いて隣接する長い線まで欠けていた処理を廃止。描画済み地形から得た輪郭を完全に閉じる。水面Colliderと輪郭の重複は橋の通行チェックで確認する。旧保存データのActor座標が地面の範囲外なら、Load時に中央へ戻す。

`NemoBlockDesignFreeze` は外側の追加前後で既存のSprite・Mesh digestを照合し、Collider後のデザイン不変も検査する。Sprite原画像・importer、内部施設と道は変更しない。通常の起動SceneとBuild Settingsも変えない。

## 検証
5seedで外向き966歩行の脱出0、既存道路・施設入口・橋の534チェック成功。四辺・四隅・川出口付近、橋2本の除去復帰、runtime保存再読込とScene再読込を確認。接近不能な深い森の開始点424件はskipとして記録。内部digestと中央の通常カメラPNGは全seedで変更前と完全一致。

5seedの四辺四隅の通常・32:9カメラ、実際の2河川出口、全景のGPU画像を保存・目視。川の両端が同じ側になるseedもあるため、画像は上下の固定名称ではなく2つの実出口を確認する。表示外側の原画像alphaによる森または水の被覆は最小76.3%。実Playの新Input System仮想Keyboard→実FixedUpdate→Rigidbody移動1.80とカメラ・G/H・橋保存復帰は成功。

旧v6では526回中215回の脱出を再現。初回v7の北側幹接触、r2の道路端の輪郭接触と検証開始点不足を修正した。すべての失敗と通信タイムアウトは履歴に残す。物理キーボード・手クリック・Player buildは未検証。既存プロジェクトの以前の全テスト結果は今回の物理検証と区別する。

詳細は `C:/Users/umiga/Documents/Codex/2026-10-11/task/nemo-block-map-v7-outer/README-ja.md`、`delivery.json`、`evidence/outer-final.json`。原本保全は通過。元Editorの素材インポート完了ログは確認したが、MainThread操作が応答待ちのためv7コンパイル・Scene読み込みの最終確認は保留。所有者の現在の画面状態を確認し、未保存作業を破棄せず再読み込みする必要がある。最終状態は納品記録に記載。

[外周修正の計画](../../exec-plans/completed/nemo-outer-forest-and-boundaries.md) / [Knowledge Base](../../index.md)

実装時は Unity 6.4 の [Edge Collider 2D](https://docs.unity3d.com/6000.4/Documentation/Manual/2d-physics/collider/edge-collider-2d-reference.html) と [Physics2D.Simulate](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Physics2D.Simulate.html) を確認した。


## 2026-10-13 ビジュアル仕上げ v8

所有者が承認した4面のデザイン案に基づき、既存の生成ブロックに表示だけの仕上げを加えた。`NemoVisualFinish` は草土の不規則な遷移、土色の川岸と水際の陰影、橋端の接地影、既存素材による林縁・岸の小物群を生成する。承認画像そのものを背景として貼り付けない。原画像・既存Sprite importer・施設・道と川の接続・通行領域・外周32マスの木・通常カメラを維持する。新しい表示にColliderを加えない。橋端の影は橋の除去・復帰と連動する。

生成の既定はStandard、64PPU・Scale1。5seed（0/17/42/99/20261011）の元保存済みv7と最終v8で、Collider・歩行マスク・配置・外周の森の一致を確認した。実Rigidbody2Dの外向き966試行は脱出0、道路・入口・橋の534確認は成功。接近不能な深い森の開始点424件はskipとして保持する。runtime保存再読込、保存Sceneの再読込、両橋の除去復帰を確認した。新Input System仮想Keyboardから実FixedUpdate/Rigidbodyで1.80移動し、通常カメラ・追従・G/H・橋状態の保存復帰を実Playで確認した。実Playの捕捉エラー0。物理キーボード・GUIの手クリック・Player buildはこの結果へ含めない。

追加調整ではStudyAのGroundR2とForestDetailsR1の手描き床・草・花を使い、林床の接地影を加えた。土床は従来の物理用Material識別を維持しながら表示だけ置き換え、Scene再読込時に復元する。最終5seedでも元v7との一致と回帰を再確認した。

元Editorで保存Sceneを読み込み、Standard・seed0・通常カメラ・外周4988本、未保存変更なし、土床表示の復元を確認した。通常GPU画像を保存した。元EditorでPlayは開始していない。

承認画像と同等の最終画質には未到達。川岸の自然な凹凸と厚み、草や岩の群れ・空白の配置、床面の広い変化、反復の抑制が残課題。物理回帰合格を画質合格とは扱わず、川橋1ブロックの通常表示で所有者と詰める。

検証資料は `C:/Users/umiga/Documents/Codex/2026-10-12/task/nemo-visual-implementation/evidence/`。[画質の残課題を含む進行中の計画](../../exec-plans/completed/nemo-visual-finish.md)。
