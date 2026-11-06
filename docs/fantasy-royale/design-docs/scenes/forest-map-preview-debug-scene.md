# Forest Map Preview Debug Scene

状態: **森マップを走る感覚の確認用 / 本番Gameplay Sceneではない**

承認済みの森マップを、本番の移動基盤と走りアニメーションで走り回るためのPlaytest足場である。攻撃の確認は[Exploration Preview Debug Scene](exploration-preview-debug-scene.md)で行う。

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
| `CharacterSpriteSetDefinition` | 5方向の立ち姿と走りのコマ、再生速度を持つ定義Asset |
| `ForestMapPreviewDebug` | 騎士の生成、追従Camera、足元の描画順、確認HUD。Playtest専用 |

`CharacterSpriteAnimator2D`はActorを読むだけなので、CPUやNetworkのCharacterにも同じComponentを付けられる。

## 表示の規則

- 8方向を、正面、斜め前、横、斜め後ろ、背面の5方向の絵で表す。左寄りの向きは右寄りの反転で表す。
- 斜めの2つのキーは同時には離れないため、斜めから隣の向きへ変わっても表示は0.08秒待つ。その間に止まれば斜めの立ち姿になる。表示だけの処理で、攻撃の向きは`CharacterActor2D.Facing`のまま変わらない。
- 走りは4コマで、止まると向きに合う立ち姿へ戻る。立ち姿は走りのコマとは別に、足を揃えた絵を5方向分持つ。
- 動けない間と倒れた後は走りのコマを使わない。減速中は足の動きも同じ倍率で遅くなる。
- コマと立ち姿は幅96、高さ80の同じCellに揃えてあり、足元の位置が全コマで共通である。Pivotは`(0.5, 0.05)`。兜の幅を走りのコマに合わせたため、立ち姿は脚が伸びる分だけ走りより少し背が高い。

## Camera

- 縦14.0 Unity単位を使う。Sceneの値が優先されるので、`ForestMapPreviewDebug`の`cameraHeight`で変える。Play中の変更もすぐ反映される。
- 体の中心を追い、進行方向へ少し先を見せる。追従の遅れと先読みの量は`ForestMapPreviewDebug`のInspectorで調整できる。
- Mapの外を映さないよう制限し、64 PPUの画素単位へ丸める。

## Map

- 地面、物、判定は`objects.json`と`collision.json`から作り、承認した検証用Sceneと同じ描画順を使う。
- 判定はMapCollision Layerへ置き、Map端にも壁を足す。
- 開始位置は村の広場`(60, 51)`。

## 関連

- [森マップの画作り基準](../../exec-plans/completed/forest-look-lock.md)
- [生成Asset台帳](../../development/assets/generated-assets.md)
