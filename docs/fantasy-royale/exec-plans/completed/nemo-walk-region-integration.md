# Nemo 歩行領域と元プロジェクト統合

## 目的
道・庭・施設周辺を歩けるランダムマップと、独立したG/H表示を提供する。
## 背景と課題
所有者が元repoへの直接統合を明示承認。四角い区画を物理壁にする案を廃止し、実際の歩行輪郭を森で囲む。
## 対象範囲
専用Scene、再利用ブロック、森と水の制限、橋、保存、表示、既存入力とURPへの対応、日本語手順。
## 対象外
元ゲームの起動Scene・Build Settings・Packages・ProjectSettingsの変更、素材購入、OS/IDE設定変更。
## 実装計画
別候補で実装・多seed検証後、既存ソースを退避してAssets/NemoBlocksに統合。元repoの入力・描画と既存テストを確認する。
## 進捗
完了。5 seed Sceneを元repoへ配置。101 seed論理・頻度検証、実接続・衝突・橋・保存・画面・実Playを確認。保全hash、既存テスト前後比較、日本語手順、Libraryレビュー画像の正式保存を完了。
## 判断記録
区画割り当てと通行領域を分離。道路・作業場を連結し、森の輪郭をColliderに一致させる。元PNGの画素は保ち、重複Sprite IDは専用コピーで解消。既存URPとInput Systemは設定を変えず対応。主人公の描画深度を床面より手前へ修正。
## 検証
1,042接続/実物理/保存検査、436,767画像alpha点、756森境界/出口移動が通過。G/Hの実描画・復元一致が5seedで通過。仮想Keyboard→Player入力→実Rigidbody移動0.70、Playエラー0。EditMode258/258。既存PlayModeは前後同じ4/9で、5件のSetUp要求失敗は既存。詳細は [設計と操作手順](../../design-docs/scenes/nemo-modular-random-map.md)。
## 完了条件
要求したSceneと実装・検証を納品済み。物理キーボード／GUIの手操作とPlayerビルドは未検証。既存専用Playテスト5件の実行条件は本件の統合で変更せず、前後同一失敗として報告。Library画像は同じIDのversion3へ更新、Windowsのxattr保存のみ非対応。
