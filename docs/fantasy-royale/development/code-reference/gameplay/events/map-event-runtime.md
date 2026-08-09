# MapEventRuntime

対象: `Assets/Scripts/Gameplay/Events/Runtime/MapEventRuntime.cs`

状態: **Event Runtime共通契約実装 / Unity EditMode・PlayMode検証済み**

## 役割

イベント種類ごとの効果と、Socket選択、OneShot更新、Unity表示を分離する共通Runtime契約。

- `MapEventExecutionOutcome`: 成功、拒否、未対応、不正を区別する。
- `MapEventPresentationRequest`: Message / Effect / Audioの意味IDだけを保持する。
- `MapEventExecutionResult`: Outcome、実適用値、Presentation要求を一括して返す。
- `MapEventExecutionContext`: HandlerへUnity Scene参照を渡さず、本番`ICharacterHealthTarget`などのルール対象だけを束ねる。
- `IMapEventHandler`: 一種類のDefinitionに対するルール処理。
- `MapEventHandlerRegistry`: Definition実型をKeyとするRuntime Dictionary。
- `MapEventExecutionService`: 既使用検査、Handler実行、成功後OneShot更新を統括する。

## 実行順

1. 呼び出し側がSocket ID、Definition、実行Context、Runtime Stateを渡す。
2. OneShotが使用済みならHandler実行前に拒否する。
3. RegistryがDefinitionの実型へ完全一致するHandlerを検索する。
4. Handlerがルール対象を更新し、結果とPresentation要求を返す。
5. 成功したOneShotだけをSocket ID単位で使用済みにする。
6. PresenterがMessage Keyを文言へ変換し、Effect / Audio CueをUnity表示へ接続する。

Handlerと共通実行サービスは`GameObject`、`Transform`、UI、Effect Prefab、AudioSourceを参照しない。現在のContextは`FantasyRoyale.Gameplay.Characters.Core`の本番HP対象だけを持つ。Inventoryなど別のルール対象は必要になった時点で追加する。

## 検証

`MapEventRuntimeTests`の実行系7件でHandler重複、回復成功、満タン非消費、撃破中の通常回復拒否と非消費、未対応Event、使用済み拒否、非OneShotを検証する。同じTest ScriptのPool系4件とCharacter / Map Authoring系を合わせた全EditMode Suiteは83 passed / 0 failed / 0 skipped。
