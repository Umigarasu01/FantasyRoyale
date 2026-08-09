# PreviewDebugMapEventPresentation

対象: `Assets/Scripts/Playtest/Runtime/PreviewDebugMapEventPresentation.cs`

状態: **Playtest専用実装 / Unity検証済み**

## 役割

- `PreviewDebugMapEventPresenter`: `MapEventExecutionResult`のMessage Keyを確認HUD用日本語へ変換し、最後のEffect / Audio Cueを保持する。

Preview専用HP実装は削除済みで、確認用Playerも`CharacterHealth`を使う。このScriptはLocalization、Effect Prefab、AudioSourceへ置き換えるまでの表示用`PreviewDebug`足場であり、本番Presenter APIとして使用しない。撃破中の通常回復拒否Message Keyも、確認HUD用日本語へ変換する。
