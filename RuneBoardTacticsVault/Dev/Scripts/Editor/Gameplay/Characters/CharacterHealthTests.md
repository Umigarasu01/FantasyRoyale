# CharacterHealthTests

対象: `Assets/Scripts/Gameplay/Characters/Tests/Editor/CharacterHealthTests.cs`

状態: **5件実装 / Unity EditMode検証済み**

## 確認内容

- 最大HP 0以下、負の初期HP、最大超過の初期HPを生成時に拒否する。
- 過剰ダメージをHP 0へClampし、一度の撃破遷移を返す。
- 撃破後の追加ダメージと通常回復を拒否し、HP 0を維持する。
- 過剰回復を最大HPへClampし、実際に増えた量を返す。
- 0以下のダメージ・回復要求を`Invalid`として返し、状態を変更しない。

Unity Sceneや`GameObject`を使わず、本番HP Coreの境界値と生死契約を検査する。Move Command / Event回帰を含む全EditMode Suiteは83 passed / 0 failed / 0 skipped。
