# ExecPlan運用規約

ExecPlanは、複数システムにまたがる変更や、設計判断と進捗を作業中も保存する必要がある変更のLiving Documentである。

## 保存場所

- 進行中: `docs/fantasy-royale/exec-plans/active/`
- 完了済み: `docs/fantasy-royale/exec-plans/completed/`

File名は内容を表す英語の`kebab-case`とする。

## 必須項目

各ExecPlanには、最低限次を含める。

1. `目的`
2. `背景と根拠`
3. `対象範囲`
4. `対象外`
5. `実装計画`
6. `進捗`
7. `判断記録`
8. `検証`
9. `完了結果`

## 更新ルール

- 作業開始時に、第三者が読んでもGoalと完了条件を理解できる内容を書く。
- 実装中に判明した制約、変更した判断、検証結果をその都度追記する。
- Product Decisionが必要な箇所はDecision Gateとして明示し、根拠なく越えない。
- Progressは完了・進行中・未着手が判別できる形にする。
- 一時的な端末出力を貼り付けず、再利用できる結論とEvidenceだけを残す。
- 完了時は最終結果と未解決事項を記録し、Fileを`completed/`へ移す。
- 中止した場合も削除せず、中止理由を記録して`completed/`へ移す。

## 完了条件

ExecPlanを完了扱いにする前に次を確認する。

- 要求された変更が実装されている。
- 必要なBuild / Test / Link検証が実行されている。
- 変更に起因するFailureが解消されている。
- 関連するProduct Spec / Design Doc / Development Guideが更新されている。
- 最終Diffを確認している。
- 残るDecision Gateや制約が明記されている。

