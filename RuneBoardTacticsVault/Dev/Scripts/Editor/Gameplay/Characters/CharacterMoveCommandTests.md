# CharacterMoveCommandTests

対象: `Assets/Scripts/Gameplay/Characters/Tests/Editor/CharacterMoveCommandTests.cs`

状態: **4件実装 / Unity EditMode検証済み**

## 確認内容

- 単位円内のアナログ入力を保持する。
- 1,1の斜め入力を長さ1へClampする。
- `None`が停止Commandになる。
- NaN / Infinityを拒否する。

Unity SceneやInput Systemを使わず、全入力元が共有する移動値の境界を検査する。全EditMode Suiteは83 passed / 0 failed / 0 skipped。

