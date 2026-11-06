# Build・Test手順

## Batchmodeの前提

- Repository RootでPowerShellから実行する。
- Unity Editorで同じProjectを開いていない状態にする。
- Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe`
- Unity Test Runnerは結果File書き出し完了前の終了を避けるため、`-runTests`時に`-quit`を付けない。

## Knowledge検証

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Knowledge\validate-knowledge.ps1
```

標準Markdown Link、WikiLink禁止、`kebab-case`、必須入口Fileを検査する。

## C# Project Build

Unityが生成した全`.csproj`を順番にBuildする。並列実行は共有DLL Lockを起こし得るため使用しない。

```powershell
$projects = rg --files -g '*.csproj' | Sort-Object
foreach ($project in $projects) {
    dotnet build $project --no-restore --nologo --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

## Unity EditMode Test

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' `
  -batchmode -nographics -automated `
  -projectPath 'C:\Users\umiga\Repository\FantasyRoyale' `
  -runTests -testPlatform EditMode `
  -testResults 'C:\Users\umiga\Repository\FantasyRoyale\Logs\EditModeResults.xml' `
  -logFile 'C:\Users\umiga\Repository\FantasyRoyale\Logs\EditMode.log'
```

## Unity PlayMode Test

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe' `
  -batchmode -nographics -automated `
  -projectPath 'C:\Users\umiga\Repository\FantasyRoyale' `
  -runTests -testPlatform PlayMode `
  -testResults 'C:\Users\umiga\Repository\FantasyRoyale\Logs\PlayModeResults.xml' `
  -logFile 'C:\Users\umiga\Repository\FantasyRoyale\Logs\PlayMode.log'
```

## 起動中EditorのUnity CLI Test

2026-10-03にPipeline経由で実行確認した手順。Editorを開いたまま行う場合はBatchmodeとの同時実行を避ける。CLIと対象Projectを絶対Pathで固定する。

```powershell
$unityCli = 'C:\Users\umiga\AppData\Local\Unity\bin\unity.exe'
$projectPath = 'C:\Users\umiga\Repository\FantasyRoyale'
& $unityCli command run_tests --project-path $projectPath --mode editor --async_tests true --format json
& $unityCli command test_status --project-path $projectPath --format json
```

`run_tests`が返す`running`は成功結果ではない。`test_status`の`data.result`をJSONとして読み、`status=completed`と`summary`を確認する。現行CLIではこの`result`はJSON文字列である。

```powershell
$response = & $unityCli command test_status --project-path $projectPath --format json | ConvertFrom-Json
$testResult = $response.data.result | ConvertFrom-Json
$testResult.summary
$testResult.results | Where-Object Status -ne Passed
```

EditMode終了後、`--mode playmode`で同じ手順を実行する。結果を取得してから次のTestを始める。

## 仮想Keyboardの入力が届かないとき

`InputTestFixture`の`Press`がPlayMode Testで効かず、`InputSystem.onEvent`にも現れない場合は、Input Systemの時刻による破棄を疑う。Input SystemはEditを抜けてからPlayへ入るまでの時刻を持つ入力を捨てる。この時刻はEditor起動からの秒数で、Test用の時計も0付近から同じ範囲を通るため、実行のたびに一部のTestだけが失敗する。`Time.realtimeSinceStartup`もPlay開始付近から数えるので、同じ範囲を通る。

`PreviewDebugCombatPlayModeTests`は仮想Keyboardを追加した直後に`currentTime`を十分先の値へ進めて避ける。`Press`を使うTestを新しく書くときは同じようにする。

## 所有者のEditorが開いている間の検証

Editorが同じProjectを開いているとBatchmodeは使えない。編集中の状態を変えずに検証するときは、`Assets/`、`Packages/`、`ProjectSettings/`を一時Folderへ複製し、そのProjectへBatchmodeを実行する。`Library/`は複製しない。初回は再Importで数分かかる。

Folderの絶対Pathが長いと、一部のPackageで`DirectoryNotFoundException`が出る。短いPathを選ぶ。

複製先で作ったSceneを本体へ戻すときは、参照が切れないよう新しいAssetと`.meta`を一緒に戻す。本体のEditorは新しいFileを先にImportして別のGUIDを付けることがあるので、戻す前に`.meta`のGUIDを比べ、参照されているFileは複製先の`.meta`で揃える。

## Forest Compositionの再現

今回の比較生成では、保存済みSceneを開き、Playを停止してから`Assets/Art/Map/ForestRestart/Composition/ForestCompositionSettings.asset`を選ぶ。Inspectorの`seed`を20261003、`focusOrthographicSize`を8.5、`includeLandmarkCandidates`をtrueにして保存し、`FantasyRoyale/Map Authoring/Generate Configured Forest Composition`を実行する。

この入口は既存76Spriteを再制作せず、追加Composition Assetと`Assets/Scenes/MapAuthoring/ForestRestart/ForestCompositionSeed20261003.unity`を生成する。生成後はそのSceneを開いてCamera表示を確認する。同じ設定からの再生成は比較Sceneを組み直す操作であり、Scene上の手編集を保つ操作ではない。無名Sceneや対象Sceneの未保存変更がある場合、Builderは実行を拒否する。

起動中EditorをCLIから同じ設定で生成する場合は、上記の`$unityCli`と`$projectPath`を使用する。

```powershell
$code = @'
var settings = FantasyRoyale.MapAuthoringKit.Editor.ForestCompositionVisuals.GetSettings();
settings.seed = 20261003;
settings.focusOrthographicSize = 8.5f;
settings.includeLandmarkCandidates = true;
UnityEditor.EditorUtility.SetDirty(settings);
UnityEditor.AssetDatabase.SaveAssets();
FantasyRoyale.MapAuthoringKit.Editor.ForestRestartAssetBuilder.BuildConfiguredComposition();
return "Forest Composition generated";
'@
& $unityCli command eval --project-path $projectPath --timeout 120 --format json --code $code
```

Seedだけを指定する場合は`ForestRestartAssetBuilder.BuildRepresentativeSeed(20261003)`を呼べる。保存SceneのCamera倍率とランドマーク候補有無は設定Assetを参照する。`includeLandmarkCandidates=false`では店と泉の構図候補を生成しない。店の売買や泉の回復はこの比較Scene生成には実装されていない。

結果は`Assets/Art/Map/ForestRestart/QA/composition-seed-{seed}-validation.json`へ記録する。`logicalValid`に加えて`sceneValidated=true`と`sceneValid=true`、ErrorとScene Issueの内容を確認する。実画像は同Rootの次の5点で、描画後に全景の空間構成、道と森縁の密度、ゲーム縮尺のCharacter、前後関係、橋の歩行幅を目視する。

| 画像suffix | サイズ | Camera Orthographic Size | 用途 |
| --- | --- | ---: | --- |
| `-overview.png` | 1536x1024 | 16 | 全体の道路、広場、水路、段丘、森林 |
| `-focused.png` | 1536x1024 | 9 | 中央付近の構図と密度 |
| `-game-scale.png` | 1536x1024 | 6 | Characterを大きく表示したゲーム縮尺の比較 |
| `-detail.png` | 768x768 | 6 | Spawn付近の足元、小物、前後関係 |
| `-bridge-standing.png` | 1024x768 | 4 | 橋上Characterと表示Deck・歩行域 |

`focusOrthographicSize`は保存SceneのCamera設定で、上記のQA撮影倍率とは別である。QAのJSONやCompile成功だけでは見た目の確認を終えた扱いにしない。今回の指定Library画像はHomePCで取得を完了できていないため、その実ピクセルとの最終一致判定は保留する。

### 探索Previewへの接続

今回の修正済みSeed20261003は`Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`へ反映済みで、ReferenceのPath/GUIDを維持した。`Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`を開いてPlayすると、このReferenceをAdditiveで読み、新しい地形上で従来のCharacter/Event経路を確認できる。

Runtime確認では既存の青緑Debug Markerを操作Playerとして使用する。Map内の静止`ConceptScaleHero`は比較表示で、操作Playerではない。今回の実画面はQA Directoryの`composition-runtime-start.png`と`composition-runtime-after-move.png`で、HUDを含むscreen Captureを保存した。移動後に操作Playerが森の樹冠の背面へ入る足元Yソートも確認する。

比較Sceneの再生成だけでは探索Previewの参照先を切り替えない。別SeedをReferenceへ反映するときは対象Sceneの手編集を保存・退避し、同じ設定Assetの候補有無を確認してから、CLI evalで次を呼ぶ。Referenceの配置を組み直す保守操作であり、保存済みの手配置を保持する操作ではない。

```csharp
FantasyRoyale.MapAuthoringKit.Editor.ForestRestartAssetBuilder.BuildScene(
    20261003,
    FantasyRoyale.MapAuthoringKit.Editor.ForestRestartAssetBuilder.ReferenceScenePath);
```

## Forest Restartの全体保守再生成

未保存Sceneを保存するか保存済みSceneを開き、Playを停止してから実行する。対象Sceneに未保存変更がある場合もBuilderは上書きを拒否する。

この広い保守入口は既存のGrass / Dirt / Prefabを含むAssetを組み直し、複数SeedのSceneと共通QAを更新する。ひとつの構成比較を再現する通常手順には上記の`BuildConfiguredComposition`を使用する。

```powershell
& $unityCli command eval --project-path $projectPath --timeout 120 --format json `
  --code 'FantasyRoyale.MapAuthoringKit.Editor.ForestRestartAssetBuilder.BuildCompleteKit(); return "Forest kit generated";'
```

40 Seedの論理Planと代表4 Sceneを現行の構成規則で生成する。結果は`Assets/Art/Map/ForestRestart/QA/generation-validation.json`。実Scene未検証の36 Seedは`sceneValidated=false`で区別する。QAは同じDirectoryの`composition-seed-{seed}-*.png`へ保存される。以前の40Plan / 4Sceneの成功数を、再実行せず今回の変更後の結果として扱わない。

## 現在の基準

2026-11-06、森マップへ攻撃、雑魚敵、宝箱、ショップを追加した後、一時複製のProjectでのBatchmode実行: EditMode 291 / 291成功、PlayMode 22 / 27成功。失敗した5件は従来と同じStudyAのSetUp失敗で、証跡Folderの指定がないため実行されていない。Player Buildと`dotnet build`は未実行。攻撃の5件は時刻による入力の破棄で不安定だったため修正し、単独でも4回連続で全件成功した。

2026-11-05、攻撃のVertical Slice実装後のBatchmode実行: EditMode 279 / 279成功、Compile Error 0。PlayMode 9 / 14成功で、Exploration Previewの4件と攻撃の5件は成功した。失敗した5件は`StudyANativeLayoutsPlayPreviewDebugTests`と`StudyANativeFlexibleConnectionsPlayPreviewDebugTests`で、検証の保存先の指定がなくSetUpで止まる。以前の記録と同じ結果で、機能の不具合を示すものではない。C# Project BuildとPlayer Buildは実行していない。以下はそれ以前の記録である。

今回の構成改善は代表Seed20261003〜20261006の論理検査と実Scene Validatorがすべて成功し、実Unity Cameraで各5画像、計20画像を保存した。橋Deckと崖Frontを修正し、各SeedのPhysics検査で橋を除く水域90Cell、橋21Cell、崖、全歩行域、木の接地点を確認してError0。Reference移行後の最終Test RunnerはEditMode113 / 113成功（10.42秒）、PlayMode4 / 4成功（2.2秒）、Failed / Skipped / Inconclusiveは0。平面装飾の回帰Testはbridge / cobblesも対象に加え、`MapDetail` / Order0 / Pivotの検査を維持した。

実Gameplay PreviewはReady、失敗Message空でReferenceを読み、位置(24.5,13.5)から(37.1,13.5)への移動とCamera追従を確認した。Runtime2画面を追加してQAは計22PNG。終了後はPlay停止、元の保存済みTemplate Scene、実行設定へ復元し、Console対象区間のErrorは0。スタンドアロン27論理Test Case、UnityのScene / Physics / Test Runner、実画面確認を区別する。

永続的な検証集計は[composition-verification.json](../../../Assets/Art/Map/ForestRestart/QA/composition-verification.json)。4Seed、Physics、Test結果、22PNGのHashを含む。Test Runnerの完全応答は`Logs/MapComposition/20261003-editmode-tests.json`と`20261003-playmode-tests.json`で、生成OutputのためGit対象外の証跡である。[構成改善ExecPlan](../exec-plans/active/forest-composition-improvement.md)に実装と動作検証の結果、残る見た目の課題を記録する。Knowledgeの正式Validatorは資料をcheckoutへ反映した後に実行する。

2026-10-03 ForestRestart再制作・旧素材135 Asset File撤去後: Unity CLI Test RunnerでEditMode 98 passed、PlayMode 4 passed、Failed / Skipped 0。証跡は`Logs/ArtReset/editmode-results.json`と`playmode-results.json`。40 Seed論理Plan、代表4 SceneのValidator、8撮影画像は`Assets/Art/Map/ForestRestart/QA/`へ保存した。C# Project BuildとPlayer Buildは今回実行していない。

2026-08-08時点の最後のGameplay変更では、C# 12 Projectが0 warning / 0 error、EditMode 83 passed、PlayMode 4 passed。これは履歴上の基準であり、将来の変更を未検証のまま成功扱いする根拠には使わない。
