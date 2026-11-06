# Forest Map Preview Debug Scene

状態: **森マップを遊びながら調整するための確認用 / 本番Gameplay Sceneではない**

森マップを、本番の移動、攻撃、敵の判断で遊ぶためのPlaytest足場である。数値と試合の仮ルールだけをこの足場が持つ。

## 対象

- Scene: `Assets/ForestMapPreview/ForestMapPreviewDebug.unity`（Gitへ入れない、Build Settingsにも入れない）
- 足場: `Assets/Scripts/Playtest/Runtime/ForestMapPreviewDebug.cs`
- Builder: `Assets/Editor/Playtest/ForestMapPreviewDebugSceneBuilder.cs`
- 騎士の定義: `Assets/Data/Gameplay/Characters/HeroSprites.asset`

`Assets/ForestMapPreview/`は地図出力の置き場であり、Sceneを含めて全体を再生成できるのでGitへ入れない。作り方は[Asset導入・生成Workflow](../../development/asset-workflow.md)に従う。

## 責務

| 担当 | 役割 |
| --- | --- |
| `CharacterActor2D` | 移動、足元判定。本番と同じ |
| `HumanCharacterMoveInputAdapter` | `Player/Move`の入力。本番と同じ |
| `CharacterSpriteAnimator2D` | 向きと移動状態から表示Spriteを選ぶ本番Component。移動や判定には影響しない |
| `CharacterSpriteSetDefinition` | 5方向の立ち姿と走りのコマ、コマごとの手と背中の位置、再生速度を持つ定義Asset |
| `CharacterGearDefinition` | 装備の部品1つ。付ける位置と5方向の絵だけを持ち、走りのコマは持たない |
| `CharacterCombat2D` | 攻撃。本番と同じ |
| `CharacterChaseBrain` | 敵が追うか持ち場へ戻るかの判断。純C#の本番規則 |
| `CharacterChaseController2D` | Brainの結果をActorへ渡し、触れたときのダメージを与える本番Component |
| `ForestMapPreviewDebug` | 騎士、敵、宝箱の生成、追従Camera、足元の描画順、HUD、仮ルール。Playtest専用 |

`CharacterSpriteAnimator2D`はActorを読むだけなので、CPUやNetworkのCharacterにも同じComponentを付けられる。

## 表示の規則

- 8方向を、正面、斜め前、横、斜め後ろ、背面の5方向の絵で表す。左寄りの向きは右寄りの反転で表す。
- 斜めの2つのキーは同時には離れないため、斜めから隣の向きへ変わっても表示は0.08秒待つ。その間に止まれば斜めの立ち姿になる。表示だけの処理で、攻撃の向きは`CharacterActor2D.Facing`のまま変わらない。
- 走りは4コマで、止まると向きに合う立ち姿へ戻る。立ち姿は走りのコマとは別に、足を揃えた絵を5方向分持つ。
- 動けない間と倒れた後は走りのコマを使わない。減速中は足の動きも同じ倍率で遅くなる。
- コマと立ち姿は幅96、高さ80の同じCellに揃えてあり、足元の位置が全コマで共通である。Pivotは`(0.5, 0.05)`。兜の幅を走りのコマに合わせたため、立ち姿は脚が伸びる分だけ走りより少し背が高い。

## 仮ルール

数値は`ForestMapPreviewDebug`の定数で、遊びながら調整する。

- 操作は移動WASD、通常攻撃J、特殊K、装備切り替え1と2、宝箱を開けるE。
- 敵は6以内に近づくと追い、持ち場から14を超えた相手は諦める。速さは3で騎士の5より遅い。
- 敵のHPは40で、触れると1秒に一度10ダメージを与える。倒すとコインが1枚増え、30秒後に持ち場へ戻る。
- 開始時の装備は剣だけ。宝箱の中身はコイン10枚で、弓はショップで買う。
- 屋台の前でEを押すとショップが開く。回復薬は5コインでHPを30回復し、弓は20コインで買うとその場で持ち替える。
- 装備を替えると攻撃と一緒に手と背中の部品も替わる。
- 騎士のHPは100。倒れると1.5秒後に村へ戻って全回復し、2秒間は敵に触れてもダメージを受けない。育成中は脱落しない方針に合わせている。
- 村の噴水と祠の泉の前でEを押すと、既存の回復の泉のEventでHPが30回復する。それぞれ一度しか使えない。

地形は固定で、敵は候補20か所から12か所、宝箱は候補9か所から4か所を、Event配置と同じ`MapEventPlacementSelector`で試合ごとに選ぶ。候補は`ForestMapPreviewDebugSceneBuilder`の一覧で持つ。HUDにSeedを表示し、`ForestMapPreviewDebug`の`matchSeed`へ入れると同じ配置を再現できる。

## 装備の見た目

体のアニメーションは素手の1組だけで、部品を重ねて装備を表す。

- 部品の位置は`MainHand`、`OffHand`、`Back`の3つである。剣は`MainHand`、盾と弓は`OffHand`、矢筒は`Back`に付く。
- `CharacterSpriteAnimator2D`が今のコマの手と背中の位置へ部品を毎Frame置く。左寄りの向きは体と一緒に反転する。
- 部品を体の前後どちらに描くかは、位置と向きから`CharacterSpriteAnimator2D`が決める。背中の物は背を向けたときだけ体の前に出る。
- 体の描画順は`SetSortingOrder`で変える。部品の前後が崩れないよう、体と同時に更新する。

足し方と確認の仕組みは[Asset導入・生成Workflow](../../development/asset-workflow.md)に従う。

## Camera

- 縦14.0 Unity単位を使う。Sceneの値が優先されるので、`ForestMapPreviewDebug`の`cameraHeight`で変える。Play中の変更もすぐ反映される。
- 体の中心へ遅れなく追従する。追従の遅れと進行方向の先読みは違和感があったため入れない。
- Mapの外を映さないよう制限し、64 PPUの画素単位へ丸める。

## Map

- 地面、物、判定は`objects.json`と`collision.json`から作り、承認した検証用Sceneと同じ描画順を使う。
- 判定はMapCollision Layerへ置き、Map端にも壁を足す。
- 開始位置は村の広場`(60, 51)`。

## 関連

- [森マップの画作り基準](../../exec-plans/completed/forest-look-lock.md)
- [生成Asset台帳](../../development/assets/generated-assets.md)
