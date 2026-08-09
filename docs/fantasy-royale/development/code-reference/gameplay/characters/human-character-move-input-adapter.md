# HumanCharacterMoveInputAdapter

対象: `Assets/Scripts/Gameplay/Characters/Unity/HumanCharacterMoveInputAdapter.cs`

状態: **人間移動入力Adapter実装 / Unity PlayMode検証済み**

## 役割

Unity Input Systemの`Move` Actionを読み、純C# `CharacterMoveCommand`へ変換する。

- InputAction AssetやActorを所有せず、渡されたMove Actionの有効化、読取、無効化だけを担当する。
- Unityの`Vector2`をCoreへ漏らさず、水平・垂直値としてCommandへ渡す。
- 無効中は停止Commandを返す。

PreviewはRuntime複製したInput ActionをこのAdapterへ渡す。CPU / NetworkはこのClassを使わず、同じ`CharacterMoveCommand`を別の入力元から生成する。

