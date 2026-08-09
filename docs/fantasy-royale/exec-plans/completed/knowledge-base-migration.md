# FantasyRoyale Knowledge Base移行

## 目的

旧`RuneBoardTacticsVault/`と`AGENTS_old.md`に分散しているProject Knowledgeを、新しいAGENTS.mdの分類へ合わせて`docs/fantasy-royale/`へ再編する。内容を失わず、標準Markdown Link、英語`kebab-case`、明確なSource of Truthへ移行する。

## 背景と根拠

- 新AGENTS.mdはKnowledge Base、Product Spec、Design Doc、ExecPlan、Development Guideの責務を分離する方針へ変わった。
- テンプレート名`docs/robo-days/`は本Project名と一致しないため、`docs/fantasy-royale/`へ置換する。
- 旧VaultはObsidian WikiLink、PascalCase File名、Script単位解説、履歴Ledgerが混在している。
- `AGENTS_old.md`には現在も有効なCore / Unity境界、Preview命名、AI / 外部素材管理、FantasyRoyale専用Unity確認手順が残る。

## 対象範囲

- AGENTS.mdのKnowledge pathと例をFantasyRoyale向けへ変更する。
- 旧Vaultの全Markdownを新カテゴリへ移動し、File名を`kebab-case`へ変更する。
- WikiLinkと旧Path参照を標準Markdown Linkへ変換する。
- 旧AGENTSの有効ルールをDesign Doc / Development Guideへ移す。
- 新しいKnowledge入口、ExecPlan規約、Build / Test手順を作る。
- 移行後のリンク、旧Path参照、File命名、Git Diffを検証する。

## 対象外

- Unity Runtime Behavior、Asset GUID、Scene、C#実装の変更。
- Product仕様の追加確定。
- 過去の履歴内容そのものの書き換え。
- AGENTS.mdへ詳細規則を戻して肥大化させること。

## 実装計画

1. 新Knowledge Directoryと本規約・ExecPlanを作る。
2. 旧VaultをProduct Spec、Design Doc、Development、Reference、ExecPlanへ分類して移動する。
3. 旧AGENTSの有効ルールを専用Documentへ抽出する。
4. 新`index.md`から主要Documentへ到達可能にする。
5. WikiLink、旧Vault path、`docs/robo-days`参照を除去する。
6. Markdown Link、命名、空Directory、Diffを検証する。
7. 結果を記録し、本Planを`completed/`へ移す。

## 進捗

- [x] 新AGENTS、旧AGENTS、旧Vault構造を確認した。
- [x] `docs/fantasy-royale/`のカテゴリDirectoryを作成した。
- [x] ExecPlan運用規約とActive Planを作成した。
- [x] 旧Vaultを移動する。
- [x] Linkと旧Path参照を変換する。
- [x] 旧AGENTSの有効ルールをKnowledgeへ抽出する。
- [x] 新IndexとDevelopment Guideを完成させる。
- [x] 検証と最終Reviewを完了する。

## 判断記録

- Knowledge Base名はテンプレート由来の`robo-days`ではなくRepository名に合わせて`fantasy-royale`とする。
- 旧Script解説は即時削除せず、`development/code-reference/`へ移して履歴と実装入口を保つ。今後はコードを読めば明らかな内容だけのDocumentを増やさない。
- 旧Current Work / Implementation Statusは現在のSource of Truthにせず、`references/legacy/`へ履歴として保存する。
- 競合する旧ルールのうち「全C# Scriptに解説md必須」は移管しない。DurableなBehavior / ArchitectureだけをKnowledgeへ記録する新方針を優先する。

## 検証

- `Tools/Knowledge/validate-knowledge.ps1`: 62 Markdown File、標準Link、WikiLink禁止、`kebab-case`、必須入口Fileを検証して成功。
- 現行DocumentとSourceから`RuneBoardTacticsVault`、`docs/robo-days`、`AGENTS_old.md`参照が除去され、残る記述は本Planと`references/legacy/`の履歴説明だけであることを確認。
- `git diff --check`: Errorなし。AGENTS.mdの既存Line Ending Warningだけを確認。
- Unity Runtime / Asset / C#は変更していないため、Unity Build / Testは実行対象外。最新のGameplay基準83 EditMode / 4 PlayModeは履歴として移管したが、今回の検証結果として再利用しない。

## 完了結果

旧Vault 53 Fileと旧AGENTSを`docs/fantasy-royale/`へ移行した。現行Knowledge、Development Guide、Active Combat Plan、Legacy履歴を分離し、旧Directoryを削除した。Knowledge検証Scriptを追加し、今後の壊れたLinkと命名違反を機械的に検出できる状態にした。
