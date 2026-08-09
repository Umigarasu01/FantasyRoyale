# CharacterHealth

対象: `Assets/Scripts/Gameplay/Characters/Core/CharacterHealth.cs`

状態: **本番Character HP Core実装 / Unity EditMode・PlayMode検証済み**

## 役割

Unity Sceneへ依存せず、Characterの最大HP、現在HP、生死と、一回のダメージ・通常回復結果を管理する。

- `ICharacterHealthTarget`: Event、戦闘、エリア外ダメージが共有する最小HP操作契約。
- `CharacterHealth`: 最大HP、現在HP、生死を保持する本番用純C#状態。
- `CharacterHealthChangeKind`: ダメージか回復かを区別する。
- `CharacterHealthChangeOutcome`: 適用、条件拒否、不正入力を区別する。
- `CharacterHealthChangeResult`: 要求量、実適用量、変更前後、生死、撃破遷移を不変値として返す。

## 契約

- 最大HPは1以上、初期HPは0以上かつ最大HP以下。不正設定は生成時に拒否する。
- 正のダメージだけを生存中に適用し、HP 0で撃破状態へ遷移する。過剰分は現在HPまでにClampする。
- 正の通常回復だけを生存中に適用し、最大HPまでにClampする。
- 撃破後の追加ダメージと通常回復は状態を変えず`Rejected`を返す。
- 通常回復から復活させない。将来の復活は別の明示的な契約として追加する。

Coreは表示、Collider、Animation、Effect、Audioを操作しない。Unity側は`CharacterHealthChangeResult`を受けて表示や演出へ変換する。

## 利用箇所

- `MapEventExecutionContext`と`HealingFountainEventHandler`。
- `BattleRoyaleExplorationPreviewDebug`の確認用Player HP。
- 後続Stepで接続する戦闘ダメージ、被弾、撃破判定。

