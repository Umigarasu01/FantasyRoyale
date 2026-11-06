# Character・戦闘基盤

## 目的

トップダウンAction戦闘を成立させ、装備が通常攻撃と特殊Actionを差し替えられるProduction基盤を作る。

## 背景と根拠

- [Prototype方針](../../product-specs/prototype-direction.md)では、人間、CPU、Network入力を共通Commandへ変換する。
- HP、装備、攻撃範囲はCore、Input、Physics、表示、Animation、EffectはUnity側へ分ける。
- Step 1で`CharacterHealth`、Step 2で`CharacterMoveCommand`、`CharacterActor2D`、人間入力Adapterを実装済み。
- Exploration PreviewはProduction HP / 移動経路へ移行済み。

## 対象範囲

- Character HP、Move Command、Unity Actor。
- 通常攻撃Command、攻撃範囲、Damage適用。
- 装備DefinitionとRuntime装備状態の最小構造。
- 人間入力から通常攻撃までの最初のVertical Slice。

## 対象外

- 正式Character Art / Animationの全面制作。
- CPU / Network入力Adapterの実装。
- Match開始終了、観戦、復活、Area収縮。
- 多数の武器・Item Balance。

## 実装計画

1. Character Health Coreを実装する。
2. 共通Move Command、Character Actor、人間入力Adapterを実装する。
3. 通常攻撃の方向、Command、命中境界、時間Model、装備Definitionを設計する。
4. 最小の攻撃Vertical Sliceを実装してPreviewへ接続する。
5. 被弾、撃破、Presentation要求を検証する。

## 進捗

- [x] Step 1: 純C# `CharacterHealth`、Damage / Healing結果、通常回復非復活。
- [x] Step 2: `CharacterMoveCommand`、`CharacterActor2D`、`HumanCharacterMoveInputAdapter`、撃破時停止。
- [x] Step 3: 通常攻撃・装備Action設計。
- [x] Step 4: 攻撃Vertical Slice実装。探索Previewで剣と弓を差し替えて的を攻撃できる。
- [ ] Step 5: 被弾・撃破・Presentation検証。的への被弾、撃破、仮表示は確認済み。Player同士の被弾は育成中のHP 0の扱いが決まるまで保留する。

## 判断記録

- CoreはUnity Sceneへ依存しない。
- 人間、CPU、Networkは同じMove Commandを生成する。
- HP 0では通常回復せず、Actorは移動Commandを適用しない。
- Preview BootstrapはRigidbody2Dを直接移動せず、Production Actorを使う。
- 装備状態はCoreの`CharacterLoadout`が持ち、ScriptableObjectの定義は`CharacterActionSpec`へ変換して渡す。
- 持続0の飛び道具も発動を逃さないよう、`CharacterActionState`は一度の時間更新で通過した段階を全て報告する。
- 矢はRigidbodyを持たず、毎回の移動区間をCircleCastで先に調べる。
- 近接攻撃は壁を貫通する。壁で止めるかは、戦闘を触ってから判断する。

## Decision Gate

Step 3へ進む前に次を決める。

- 攻撃方向: 4方向、8方向、自由照準。
- Attack Command: 方向、対象、押下 / 保持などの入力情報。
- 命中境界: Coreの範囲表現とUnity Physicsによる候補取得。
- Action時間: 発生、持続、硬直、移動可否、割込み。
- 装備: 通常攻撃 / 特殊Action SlotとScriptableObject Definition。
- 検証武器: 近接一種、または近接と遠隔の二種。

2026-11-05に所有者が下記の設計を承認した。

### Step 3 設計

| 判断 | 案 | 理由 |
| --- | --- | --- |
| 攻撃方向 | 8方向。停止中は最後の移動方向 | 移動と同じ入力で済み、CPUとNetworkも同じCommandを使える |
| Command | 使うSlotと方向を、押した瞬間に1回だけ渡す | 溜めや長押しは後からSlotの定義へ足せる |
| 命中 | Unity Physicsが候補を集め、Coreが同じ攻撃の多重命中を防いでDamageを適用する | CoreをSceneへ依存させず、既存の`ICharacterHealthTarget`を使える |
| 時間 | 発生、持続、硬直の三段階をCoreで進める。発生と持続は移動不可、硬直は半速、割込みなし | 手応えが出やすく、最初の状態数を抑えられる |
| 装備 | 通常攻撃と特殊Actionの2 Slot。ScriptableObjectの定義を差し替える | 装備でActionが変わる仕様を最初から確かめられる |
| 検証武器 | 近接の剣と遠隔の弓 | 範囲判定と飛び道具の両方で差し替えを実証できる |
| 被弾先 | 動かない的 | 育成中のHP 0の扱いが未確定なので、Player同士の被弾はStep 4へ含めない |

Assemblyは既存の`FantasyRoyale.Gameplay.Characters.Core`と`FantasyRoyale.Gameplay.Characters.Unity`へ追加し、新設しない。

初期値はHP 100と通常Camera縦8.0を基準にした仮の値で、定義Assetで調整する。特殊Actionの検証のため、剣の装備は通常攻撃が剣で特殊Actionが弓、弓の装備はその逆とする。

| 項目 | 剣 | 弓 |
| --- | --- | --- |
| Damage | 20 | 12 |
| 発生 | 0.10秒 | 0.25秒 |
| 持続 | 0.10秒 | 矢を1本放つ |
| 硬直 | 0.20秒 | 0.30秒 |
| 範囲 | 前方1.4単位、120度 | 射程6単位、弾速12単位/秒 |

## 検証

- Step 1完了時: C# 11 Project、EditMode 79、PlayMode 4。
- Step 2完了時: C# 12 Projectが0 warning / 0 error、EditMode 83 passed、PlayMode 4 passed。
- Step 4完了時: EditMode 279 / 279成功。PlayMode 9 / 14成功で、失敗5件は[Build・Test手順](../../development/testing.md)に記録した既存のStudyAのSetUp失敗。
- PlayModeの攻撃Test5件は、仮想KeyboardからInput Action、Adapter、Combat、Physics、HPまで本番の経路を通す。
- 斬撃、矢、倒れた的の表示は一時的な撮影Testの画像で確認した。2026-11-06に所有者がRepositoryのProjectで実際に操作し、攻撃が動くことを確認した。武器の数値と手触りの調整は未了。

## 完了結果

進行中。Step 5のPlayer同士の被弾は、育成中のHP 0の扱いが決まるまで保留する。

