# AGENTS.md

## Purpose

このファイルは、Codexがこのリポジトリで作業するための恒久的なルールとKnowledgeへの入口を定義する。

このファイルを百科事典にしない。
詳細な仕様・設計・判断・手順は適切なリポジトリ内ドキュメントへ配置する。

---

## Repository Knowledge

`docs/fantasy-royale/` をこのプロジェクトのKnowledge BaseおよびSystem of Recordとして扱う。

作業開始時は、タスクに必要な範囲だけ関連Knowledgeを確認すること。

主なKnowledge:

- `docs/fantasy-royale/index.md`
  - Knowledge Baseの入口
  - 主要ドキュメントへのナビゲーション

- `docs/fantasy-royale/product-specs/`
  - ゲーム仕様
  - 機能仕様
  - ユーザーから見える振る舞い

- `docs/fantasy-royale/design-docs/`
  - 技術設計
  - Architecture
  - システム間の責務
  - 重要な設計判断とその理由

- `docs/fantasy-royale/exec-plans/`
  - 複雑な実装の計画・進捗・判断履歴

- `docs/fantasy-royale/references/`
  - 外部仕様や技術資料などの参考情報

- `docs/fantasy-royale/development/`
  - Build、Test、開発環境などの開発手順

Knowledgeを無差別に読み込まない。
`docs/fantasy-royale/index.md`および関連ドキュメントのリンクから、必要な情報へ段階的に辿ること。

---

## Source of Truth

プロジェクト固有の仕様・設計については、repo-localなKnowledgeを優先する。

コード・テストとDocumentationが矛盾する場合は、どちらかを無条件に正しいと仮定しない。

以下を確認すること:

1. 要求されている仕様
2. 関連Documentation
3. 現在の実装
4. テスト
5. 必要に応じて変更履歴

意図された仕様と実装を一致させ、古くなったDocumentationも同じ変更内で修正する。

外部Knowledgeをプロジェクト固有の要求として扱わない。

---

## Default Workflow

基本的な作業サイクル:

1. Understand
2. Plan
3. Implement
4. Verify
5. Update Knowledge
6. Review

### Understand

実装前に:

- タスクのGoalを明確にする
- 関連コードを調査する
- 関連Documentationを読む
- 類似した既存実装を検索する
- Architecture上の境界を確認する
- 完了条件と検証方法を把握する

推測よりリポジトリ内のEvidenceを優先する。

### Plan

小さく明確な変更では軽量なPlanでよい。

以下に該当する変更ではExecPlanを使用する:

- 複数システムにまたがる変更
- 大きな新機能
- Architecture変更
- 大規模Refactoring
- 長時間または複数段階にわたる作業
- 実装途中の判断を保存する必要がある作業

ExecPlanの規約は `docs/fantasy-royale/PLANS.md` に従う。

Active Plan:
`docs/fantasy-royale/exec-plans/active/`

Completed Plan:
`docs/fantasy-royale/exec-plans/completed/`

ExecPlanは実装中も更新するLiving Documentとして扱う。

### Implement

実装時は:

- 既存のArchitectureとPatternを優先する
- 類似実装を再利用する
- 必要以上の変更を行わない
- unrelatedなRefactoringを混ぜない
- 新しいDependencyの追加は必要性を確認する
- 公開Behaviorを意図せず変更しない
- 新しいArchitecture Patternを無断で導入しない

巧妙さより、可読性・保守性・Agent Legibilityを優先する。

### Verify

実装しただけで完了としない。

変更に応じて可能な範囲で:

- Test
- Build
- Lint
- Formatting
- Static Analysis
- 実際のBehavior確認

を実行する。

結果を確認し、変更によって発生したFailureを修正する。

実行していない検証を、実行済みとして報告してはならない。

安定したBuild/Testコマンドは `docs/fantasy-royale/development/` に記録する。
コマンドを推測して実行しない。

### Update Knowledge

Documentationは実装の一部として扱う。

Behaviorが変わった:
→ `docs/fantasy-royale/product-specs/`

Architectureや責務が変わった:
→ `docs/fantasy-royale/design-docs/`

重要な設計判断が発生した:
→ 関連するDesign Document

複雑な作業の進捗・判断:
→ ExecPlan

開発手順が変わった:
→ `docs/fantasy-royale/development/`

一時的な実装詳細やコードを読めば明らかな内容までKnowledge化しない。

既存Documentを更新できる場合は、重複Documentを新規作成しない。

主要Documentを追加した場合は `docs/fantasy-royale/index.md` から到達可能にする。

### Review

完了前に最終Diffを確認する。

確認項目:

- 要求を満たしているか
- unintendedな変更がないか
- Regressionの可能性がないか
- Architectureに反していないか
- Test結果は妥当か
- Documentationと実装が一致しているか

---

## Definition of Done

タスクは以下を満たした時点で完了とする:

- 要求されたBehaviorが実装されている
- 関連する検証が実行されている
- 変更によって発生したFailureが解消されている
- 最終Diffが確認されている
- Durable Knowledgeの変更がDocumentationへ反映されている
- コードとDocumentationに明らかな矛盾がない

---

## Documentation Language

Documentation本文は原則として日本語で記述する。

ただし以下は実際の名称をそのまま使用する:

- Class名
- Method名
- Property名
- Namespace
- File path
- Package名
- API名
- Command
- その他Code Identifier

File名とDirectory名は原則として英語の`kebab-case`を使用する。

例:

`docs/fantasy-royale/product-specs/battle-system.md`

Document titleは日本語でよい。

---

## Obsidian Compatibility

`docs/fantasy-royale/` はObsidian Vaultとして直接閲覧できるKnowledge Baseとして設計する。

ただしDocumentation自体はObsidianに依存させない。

内部リンクには標準Markdown Linkを使用する。

推奨:

`[バトルシステム](../product-specs/battle-system.md)`

原則として正式Documentationでは以下を使用しない:

- `[[WikiLinks]]`
- Obsidian固有Embed
- Block Reference
- Dataview固有Syntax
- Community Plugin固有Syntax

Obsidianの以下の機能は利用してよい:

- Backlinks
- Graph View
- Search
- Tags
- Properties
- Bookmarks

新規Documentには、必要に応じて関連する仕様・設計Documentへのリンクを追加する。

リンク数を増やすこと自体を目的にしない。
意味のあるKnowledge Relationshipだけを追加する。

---

## Knowledge Improvement Loop

Codexの失敗やReview Feedbackを単発修正だけで終わらせない。

原因を以下に分類する:

Implementation Bug
→ Codeを修正し、必要ならTestを追加する

不足していたProject Knowledge
→ `docs/fantasy-royale/`を更新する

同種のCodex Mistakeが繰り返される
→ 最小限のRuleを最も近い`AGENTS.md`へ追加する

繰り返される作業Procedure
→ `.agents/skills/`へのSkill化を検討する

機械的に検出できるInvariant
→ Test / Lint / Static Analysis / CIで強制する

一度だけ発生したMistakeを理由にAGENTS.mdを肥大化させない。

---

## Skills

再利用可能なWorkflowはAGENTS.mdへ詳細を書かず、Skillとして分離する。

Repository-specific Skills:

`.agents/skills/`

Skill化に適した例:

- Feature implementation workflow
- Documentation maintenance
- Code review
- Migration
- Release
- Validation
- Knowledge gardening

AGENTS.mdはSkillへのRoutingと恒久Ruleに集中する。

---

## Unity Project Rules

Unity Projectでは以下を守る:

- `Assets/`
- `Packages/`
- `ProjectSettings/`

を主要なProject Sourceとして扱う。

`Library/`, `Temp/`, `Logs/`など生成可能なUnity OutputをSource of Truthとして扱わない。

既存のUnity Version、Package構成、Assembly構成を確認してから変更する。

`.meta`とAsset GUIDを不用意に破壊しない。

Project SettingsやPackage Dependencyを、タスクと無関係に変更しない。

Unity固有の設計判断も、Durableなものは適切な`docs/fantasy-royale/design-docs/`へ記録する。

---

## Handling Ambiguity

可逆的で小さな曖昧さは:

1. Repository Evidence
2. Existing Pattern
3. Documentation
4. 最小変更

を基準に解決する。

Product Requirementを勝手に発明しない。

大きなProduct Decision、破壊的変更、Security-sensitiveな変更、
互換性を壊すArchitecture変更では、根拠のない仮定を置かない。

---

## Final Report

作業完了時は簡潔に報告する:

- 何を変更したか
- 重要な設計判断
- 実行した検証
- 更新したDocumentation
- 残っている制約または未解決事項

詳細な作業ログを最終報告へそのまま貼り付けない。
