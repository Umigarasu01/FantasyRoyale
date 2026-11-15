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
- 攻撃で押し戻す（`TryKnockBack`）。押し戻しとアーマーは純C#の`CharacterKnockback`が持ち、押されている間は移動Commandを使わない。押す距離は攻撃のKnockbackを重さ（`knockbackWeight` / `SetKnockbackWeight`、1が標準で小さいほど軽い）で割り、アーマーを引いた値である。アーマーはInspectorの`knockbackArmor`か`SetKnockbackArmor`で決め、`float.PositiveInfinity`で決して押されない。押されると`KnockedBack`を発生させる。

入力Action、CPU判断、Network、Camera、Sprite、Animation、Event種類は参照しない。正式Prefab / Sprite / Animationは後続相談でActorへ合成する。

