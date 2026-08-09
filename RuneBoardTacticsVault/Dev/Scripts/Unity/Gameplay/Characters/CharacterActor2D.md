# CharacterActor2D

対象: `Assets/Scripts/Gameplay/Characters/Unity/CharacterActor2D.cs`

状態: **本番Unity Actor実装 / Unity PlayMode検証済み**

## 役割

純C#の`CharacterHealth` / `CharacterMoveCommand`と、Unityの2D Physicsを接続する本番Character Actor。

- Spawn時に`CharacterHealth`と正の移動速度を注入する。
- `ICharacterMoveCommandTarget`として最新Commandを受け取る。
- Dynamic `Rigidbody2D.MovePosition`で移動し、MapCollision Layerとの衝突解決をUnity Physicsへ委ねる。
- 足元`CapsuleCollider2D`を使い、既存Previewで検証済みのSize 0.48 x 0.55、Offset Y 0.22を初期値とする。
- HP 0ではCommandを停止し、死亡後の移動を許可しない。

入力Action、CPU判断、Network、Camera、Sprite、Animation、Event種類は参照しない。正式Prefab / Sprite / Animationは後続相談でActorへ合成する。

