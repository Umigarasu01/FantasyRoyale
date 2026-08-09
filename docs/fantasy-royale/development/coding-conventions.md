# Coding規約

## C# Comment

- 新しい主要型には、存在理由と責務が分かる短い日本語Commentを付ける。
- 関数Commentは宣言直前に置き、何を受け取り、何を判断・生成・更新するかを書く。
- 逐語的な処理説明は避ける。
- 状態更新順、無効値除外、Fallback、Testの狙いなど、理由が読み取りにくい箇所へ本文Commentを付ける。
- 単純なProperty、Operator、明らかな処理へ形式的なCommentを増やさない。

## Naming

- 確認用SceneやPlay確認専用`MonoBehaviour`には原則`PreviewDebug`を含める。
- Test専用補助型には`Test`、`Fake`、`Stub`、`Mock`など用途が分かる名前を使う。
- Productionへ昇格した型からは一時用途名を外し、参照・Test・Knowledgeも同時に更新する。
- DocumentationのFile名とDirectory名は原則英語`kebab-case`とする。

## Documentation境界

- 新しいC# Fileごとの解説Document作成は必須ではない。
- Public Behaviorは`product-specs/`、責務や依存方向は`design-docs/`へ記録する。
- `development/code-reference/`は既存実装の入口と移行前資料を保つ場所とし、コードを読めば明らかな説明を増やさない。
- 確定仕様、推奨案、未確定、Legacyを明記し、異なる確定度の内容を混ぜない。
- DocumentationとCode / Testの矛盾を見つけた場合は、根拠なく片方を正とせず差分を調査する。
