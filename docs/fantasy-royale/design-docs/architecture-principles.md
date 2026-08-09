# Architecture原則

## 目的

FantasyRoyaleのGameplay実装で維持する、CoreとUnityの責務境界を定義する。

## Core

- Rule、状態、Command、結果は可能な限り純C#で表現する。
- Coreは`GameObject`、`Transform`、`MonoBehaviour`、Sceneへ依存しない。
- HP、装備、攻撃範囲、Match状態、収縮状態など、同期やTestが必要な情報をCore側へ置く。
- 複数状態を更新する処理は、必要条件を先に検証してから一括更新する。途中失敗で部分更新を残さない。
- 入力元は人間、CPU、Networkで分けても、Gameplayへ渡すCommand形式を共有する。

## Unity

- Unity側はInput System、表示、Animation、Audio、Effect、Camera、Scene遷移、Physics接続を担当する。
- Unity側はCoreの結果をPresentationへ変換し、表示処理からCore状態を直接書き換えない。
- `CharacterActor2D`のようなAdapterはCore状態とUnity Componentを接続するが、入力機器やMatch進行を抱え込まない。
- Map配置のSource of TruthはUnity Scene、移動PhysicsのSource of Truthは`MapCollision` Layerとする。
- GraphicとCollisionを分離し、Spriteの見た目からRuntimeで判定を復元しない。

## 現行Assembly境界

- `FantasyRoyale.Gameplay.Characters.Core`: `CharacterHealth`、`CharacterMoveCommand`などの純C#状態と契約。
- `FantasyRoyale.Gameplay.Characters.Unity`: Character CoreとInput System / 2D Physicsの接続。
- `FantasyRoyale.Gameplay.Events.Runtime`: Event Handler、共通実行結果、OneShot状態更新、Event Pool抽選。
- `FantasyRoyale.MapAuthoringKit.Runtime`: Map Sceneへ保存する制作契約とScriptableObject定義。
- `FantasyRoyale.Playtest.Runtime`: Production経路を実Sceneで確認する`PreviewDebug`足場。

詳細なScene依存関係は[SceneとClass構成](scene-and-class-structure.md)を参照する。

