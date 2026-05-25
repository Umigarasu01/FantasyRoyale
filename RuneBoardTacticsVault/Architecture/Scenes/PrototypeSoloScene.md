# PrototypeSoloScene

## 2026-05-24 追記: 素材配置と当たり判定

- 仮スプライトは `Assets/Art/Prototype/Generated/` 配下に生成・参照する。
- 外部素材候補は `Assets/Art/External/AnokolisaPixelCrawler/` 配下に隔離して取り込む想定。
- 木、岩、溶岩、外周壁には `BoxCollider2D` があり、プレイヤーや敵の移動を遮る。
- 遊び上の設置物は商人のみ。宝箱は生成Sceneから外したまま。
- 2026-05-24時点の追加配置: 森林集落、雪原拠点、火山キャンプ、遺跡、池、湖、橋、草むら、花、低木、きのこ、看板。

## 目的

一人で「歩く、攻撃する、敵を倒す、宝箱を開ける、低確率で伝説級アイテムを得る」流れを確認するための仮Scene。

## 生成方法

Unity Editorで以下を実行する。

`FantasyRoyale > Build Solo Prototype Scene`

生成されるScene:

`Assets/Scenes/PrototypeSoloScene.unity`

同時に仮ピクセルスプライトとして以下のような画像を生成する。

- `Assets/Art/Prototype/PlayerIdle0.png`
- `Assets/Art/Prototype/PlayerIdle1.png`
- `Assets/Art/Prototype/PlayerWalk0.png`
- `Assets/Art/Prototype/PlayerWalk1.png`
- `Assets/Art/Prototype/SlimeIdle0.png`
- `Assets/Art/Prototype/SlimeIdle1.png`
- `Assets/Art/Prototype/SlimeWalk0.png`
- `Assets/Art/Prototype/SlimeWalk1.png`
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

## 操作

- `WASD` / 矢印キー: 移動
- `Space`: 向いている方向へ近接攻撃
- `E`: 近くの宝箱を開ける

## 配置

- プレイヤー1体。
- 雑魚敵12体。
- 商人4人。
- 草原、雪原、火山地帯、溶岩地形、中央街道を持つ広域固定マップ。
- 外周壁。
- 画面左上のHUD。
- プレイヤーと敵の簡易2フレームアニメーション。
- 攻撃時に表示する斬撃エフェクトテンプレート。

## 確認したい体験

- 敵を倒すとコインが増える。
- 宝箱を開けるとアイテムが手に入る。
- アイテムで攻撃力、回復、コインが増える。
- 低確率で伝説級アイテムが出て、ログに `LEGEND!` と表示される。
- HPが0になると `Game Over` 表示になり、入力が止まる。
- プレイヤーと敵が移動中に歩行/揺れアニメーションへ切り替わる。
- `Space` 攻撃時に、向いている方向へ短い斬撃エフェクトが出る。
- 斬撃が触れているように見える範囲で外れにくいよう、攻撃判定は根元と先端の2つの円で判定する。
- 初期スライムは攻撃1発で倒れる。既存Sceneに古いHP値が残っていても、起動時に敵側のプロトタイプHPで上書きする。
- 商人に近づいて `E` を押すと、10コインでランダム商品を買える。
- コイン不足時はHUDに不足メッセージが出る。
- 設置物は商人のみ。宝箱は生成Sceneから外す。

## 未確認

Unityがプロジェクトを開いていたため、batchmodeによるScene生成とコンパイル確認は未実行。
