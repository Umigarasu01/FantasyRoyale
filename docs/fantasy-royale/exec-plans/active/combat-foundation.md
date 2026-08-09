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
- [ ] Step 3: 通常攻撃・装備Action設計。
- [ ] Step 4: 攻撃Vertical Slice実装。
- [ ] Step 5: 被弾・撃破・Presentation検証。

## 判断記録

- CoreはUnity Sceneへ依存しない。
- 人間、CPU、Networkは同じMove Commandを生成する。
- HP 0では通常回復せず、Actorは移動Commandを適用しない。
- Preview BootstrapはRigidbody2Dを直接移動せず、Production Actorを使う。

## Decision Gate

Step 3へ進む前に次を決める。

- 攻撃方向: 4方向、8方向、自由照準。
- Attack Command: 方向、対象、押下 / 保持などの入力情報。
- 命中境界: Coreの範囲表現とUnity Physicsによる候補取得。
- Action時間: 発生、持続、硬直、移動可否、割込み。
- 装備: 通常攻撃 / 特殊Action SlotとScriptableObject Definition。
- 検証武器: 近接一種、または近接と遠隔の二種。

現時点の推奨案は8方向、停止中は最後の移動方向、Unity Physicsで候補取得後にCoreでDamage検証、発生・持続・硬直の三段階、近接と遠隔の二種で差し替えを実証する構成。未採用であり、ユーザー確認前に確定しない。

## 検証

- Step 1完了時: C# 11 Project、EditMode 79、PlayMode 4。
- Step 2完了時: C# 12 Projectが0 warning / 0 error、EditMode 83 passed、PlayMode 4 passed。

## 完了結果

進行中。Step 3 Decision Gateで停止中。

