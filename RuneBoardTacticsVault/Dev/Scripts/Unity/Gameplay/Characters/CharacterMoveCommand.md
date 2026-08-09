# CharacterMoveCommand

対象: `Assets/Scripts/Gameplay/Characters/Core/CharacterMoveCommand.cs`

状態: **本番移動Command Core実装 / Unity EditMode・PlayMode検証済み**

## 役割

人間、CPU、将来のNetwork入力を、入力機器やUnity型を含まない同じ移動形式へ変換する。

- `CharacterMoveCommand`: 水平・垂直の移動意思を保持する純C#不変値。
- `ICharacterMoveCommandTarget`: 入力元を知らずに最新Move Commandを受け取るCharacter側の最小契約。

## 契約

- 単位円内のアナログ入力は大きさを維持する。
- 単位円外の斜め入力だけを長さ1へClampし、Cardinal移動より速くしない。
- `None`は停止Commandを表す。
- NaN / Infinityは生成時に拒否し、Physics座標へ伝播させない。
- Commandは速度、`Rigidbody2D`、入力Action、Cameraを持たない。移動速度と物理適用はUnity Actorが担当する。

現時点では移動専用Commandのみを確定する。Attack / Special Commandを同じEnvelopeへまとめるか、用途別CommandにするかはStep 3の相談対象。

