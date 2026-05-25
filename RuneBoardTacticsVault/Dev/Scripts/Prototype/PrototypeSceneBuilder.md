# PrototypeSceneBuilder

## 2026-05-24 追記: 仮素材の隔離と地形当たり判定

- コード生成の仮PNGは `Assets/Art/Prototype/Generated/` 配下に隔離する。
- 外部素材候補は `Assets/Art/External/AnokolisaPixelCrawler/` 配下へ取り込む想定とし、仮素材と混ぜない。
- 木、岩、溶岩は地形障害物として `BoxCollider2D` を付ける。ゲーム上の設置物は商人のみのままにする。
- 追加で生成する仮スプライト: `Tree.png`, `PineTree.png`, `Rock.png`, `SnowRock.png`, `VolcanoRock.png`。

## 2026-05-24 追記: 参考画像方向の仮グラフィック刷新

- 参考画像は直接コピーせず、密度の高いトップダウン・ピクセルファンタジー風のオリジナル仮素材として作成する。
- `Assets/Art/Prototype/Generated/` に、プレイヤー、スライム、商人、攻撃エフェクト、草原、雪原、火山、水辺、石畳、木、草むら、花、低木、建物、廃墟、テント、橋、看板を配置する。
- `PrototypeSceneBuilder` は既存PNGがある場合は上書きせず、足りない素材だけを生成する。手作業または外部素材で差し替えた仮素材がScene生成で戻らないようにするため。
- 生成Sceneには森林集落、雪原拠点、火山キャンプ、中央/南部の遺跡、池や湖、石畳道を追加する。

## 2026-05-24 追記: 32px基準の細密化

- 参考画像のドット密度へ近づけるため、仮PNGを32px基準で再作成した。
- Unity上の見た目の大きさを保つため、`PrototypeSceneBuilder` の基準PPUと仮PNGの `.meta` を32へ変更した。
- タイルは細かい色ノイズ、石目、水流、溶岩流を増やし、キャラと建物は輪郭、陰影、服や屋根のディテールを増やした。

## 2026-05-24 追記: 世界観と見下ろし角度の寄せ

- 単純なピクセル数合わせではなく、明るい森系ファンタジー、太めの輪郭、3/4見下ろし気味のキャラ、上面と前面が読める建物、丸い樹冠、地面に埋まる草や石畳を基準に再調整した。
- キャラと小物には接地影を入れ、床素材との一体感を出す。
- 建物は屋根上面、側面、正面壁の色差を付け、見下ろし角度を明確にする。

## 対応スクリプト

`Assets/Editor/PrototypeSceneBuilder.cs`

## 役割

一人用プロトタイプSceneをEditor上で生成する。
仮ピクセルスプライト、固定小マップ、プレイヤー、敵、宝箱、HUD、Build Settings登録をまとめて行う。

## 実行方法

Unity Editorで以下を実行する。

`FantasyRoyale > Build Solo Prototype Scene`

## 生成物

- `Assets/Scenes/PrototypeSoloScene.unity`
- `Assets/Art/Prototype/PlayerIdle0.png` などのプレイヤー仮スプライト
- `Assets/Art/Prototype/SlimeIdle0.png` などの敵仮スプライト
- `Assets/Art/Prototype/ChestClosed.png`
- `Assets/Art/Prototype/ChestOpen.png`
- `Assets/Art/Prototype/Merchant.png`
- `Assets/Art/Prototype/SlashEffect.png`
- `Assets/Art/Prototype/GrassTile.png`
- `Assets/Art/Prototype/SnowTile.png`
- `Assets/Art/Prototype/VolcanoTile.png`
- `Assets/Art/Prototype/LavaTile.png`
- `Assets/Art/Prototype/DirtTile.png`
- `Assets/Art/Prototype/StoneTile.png`

## 関連仕様

- 仮素材で最小プレイ確認を行うための足場。
- プレイヤーと敵には `PrototypeSpriteAnimator` を付け、移動時だけ簡易アニメーションする。
- プレイヤーには `PrototypeAttackEffect` のテンプレートを設定し、攻撃時に斬撃を表示する。
- 初期配置のスライムHPは、攻撃エフェクトが当たった手応えを確認しやすいように1発撃破の値にする。
- 商人を開始地点付近、草原側、雪原側、火山側に配置し、10コインでランダム商品を買えるようにする。
- 生成Sceneでは宝箱を置かず、設置物は商人のみにする。
- 広域マップ用に `PrototypeCameraFollow` をMain Cameraへ付ける。
- 本番Sceneや正式素材へ移行する前提のため、`Prototype` 名を付けている。
