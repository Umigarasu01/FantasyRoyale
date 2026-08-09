# MapEventRuntimeTests

対象: `Assets/Scripts/Gameplay/Events/Tests/Editor/MapEventRuntimeTests.cs`

状態: **実行系7件・Pool系4件実装 / Unity EditMode検証済み**

## 確認内容

- 同じDefinition型へHandlerを重複登録できない。
- 回復成功時にHP、実回復量、Presentation Cue、OneShot状態が更新される。
- HP満タンでは拒否となり、OneShotとEffect / Audio Cueを更新しない。
- 撃破中の本番Character Healthでは通常回復を拒否し、OneShotを消費しない。
- 未対応Definitionを既存Handlerへ誤配送しない。
- 使用済みOneShotをHandler実行前に拒否する。
- 非OneShotは成功しても使用済み状態を書き込まない。

Unity Sceneを必要とせず、本番`CharacterHealth`とFake Runtime Stateで共通実行順を検査する。加えて次を検査する。

- 同Seedかつ同じID集合なら、Socket / Event入力順が違っても同じ配置Planになる。
- 有効数が候補Socket数を超える場合は空Planで失敗する。
- Event Pool ScriptableObjectのListから抽選候補とDefinition Dictionaryを構築できる。
- checked-in初期Pool Assetが泉1種・重み1・有効数3である。

全EditMode Suiteは83 passed / 0 failed / 0 skipped。
