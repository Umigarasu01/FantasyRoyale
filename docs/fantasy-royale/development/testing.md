# Build・Test手順

## 前提

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

## 現在の基準

2026-08-08時点の最後のGameplay変更では、C# 12 Projectが0 warning / 0 error、EditMode 83 passed、PlayMode 4 passed。これは履歴上の基準であり、将来の変更を未検証のまま成功扱いする根拠には使わない。

