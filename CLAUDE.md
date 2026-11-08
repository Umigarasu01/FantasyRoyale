@AGENTS.md

# Claude向け補足

- ルールの正本は `AGENTS.md`。どのエージェントにも共通するルールはそちらに書き、ここにはClaude Code固有の差分だけを書く。
- 作業前に `git status` で未コミットの変更を確認し、自分が触っていない変更は戻さない。所有者の手作業や前のセッションの作業が残っていることがある。
- 下の階層だけに効くルールは、その階層の `CLAUDE.md` に書く。Claude Codeは下の階層の `AGENTS.md` を読み込まない。
- プランモードの計画と作業リストはそのセッション限りで消える。`AGENTS.md`のExecPlanの条件に当たる作業は、計画と進捗を `docs/fantasy-royale/exec-plans/active/` に書く。
- 自動メモリは所有者の好みや指摘の控えに使う。仕様、手順、設計判断は `docs/fantasy-royale/` に書き、メモリだけに残さない。
- Unity Editorを操作するMCPは `.mcp.json` の `unity-mcp-fantasyroyale` だけを使う。設定と確認方法は[Unity CLI MCP](docs/fantasy-royale/development/unity-cli-mcp.md)にある。
