# GeneratedAssets

## 2026-06-05 Biome Atlas Generated Map Preview

- 用途: `biome-tile-object-atlas.png` から構成した探索マップのプレビュー画像。
- 保存先: `Assets/Art/Generated/BiomeAtlas/PreviewMaps/biome-atlas-generated-map-preview.png`
- 生成方法: ローカル画像合成。仮アトラスのセルを切り出し、96x72相当の森/火山/雪マップとして配置。
- 扱い: Unity Scene生成前の構造確認用。最終的なTilemap Sceneは `BiomeAtlasMapSceneBuilder` で生成する。

## 2026-06-05 Biome Tile/Object Atlas

- 用途: 森・火山・雪バイオームを共通構造で差し替えるための、Tilemap/Prefab切り出し元になる仮アトラス。
- 保存先: `Assets/Art/Generated/BiomeAtlas/biome-tile-object-atlas.png`
- 生成方法: `$imagegen` built-in image generation
- 元画像保存先: `C:\Users\umiga\.codex\generated_images\019e5986-75a2-7ee2-8488-1ef4e6aa7bc4\ig_0aadb9c0c406fc03016a22cafe02308191990e0f89f0bc4e5b.png`
- 画像サイズ: 1254x1254
- プロンプト概要: 16bit/GBA風のトップダウン2Dピクセルアートで、森・火山・雪の地形タイルとオブジェクトを同一シートに整理。草地/灰地/雪原、道、石畳、水/溶岩/氷、崖、壁、木、切り株、倒木、岩、結晶などを含める。
- 扱い: 仮アトラス。完全な32x32グリッドではないため、Unity導入時は必要タイルを切り出して正規グリッド化する。

## 2026-06-05 Volcano/Snow Biome Sample Maps

- 状態: 火山版は生成・保存済み。雪版は `$imagegen` built-in image generation のレート制限により生成待ち。
- 目的: `Assets/Art/Concept/forest-biome-sample-map.png` の構図、通路、広場、障害物配置を維持したまま、火山バイオーム版と雪バイオーム版を生成する。
- 保存先:
  - `Assets/Art/Concept/volcano-biome-sample-map.png`
- 想定保存先:
  - `Assets/Art/Concept/snow-biome-sample-map.png`
- 火山元画像保存先: `C:\Users\umiga\.codex\generated_images\019e5986-75a2-7ee2-8488-1ef4e6aa7bc4\ig_0aadb9c0c406fc03016a22c8b6f210819181ffdb8e51369575.png`
- 火山プロンプト概要: 森サンプルの中央石畳、左の曲がる道、広場、崖、右の水辺構造を維持し、水辺を溶岩、草地を火山灰、木を焦げ木、茂みを火山岩/赤結晶へ差し替える。
- 雪プロンプト概要: 森サンプルの構造を維持し、水辺を凍結池、草地を雪原、木を雪針葉樹、茂みを雪だまり/凍結低木へ差し替える。
- 関連設計: `RuneBoardTacticsVault/Art/BiomeTilemapAtlasPlan.md`

## 2026-06-05 Forest Biome Sample Map

- 用途: 森バイオームのトップダウン2Dピクセルアート風サンプルマップ。今後のタイル/マップ制作の見た目基準。
- 保存先: `Assets/Art/Concept/forest-biome-sample-map.png`
- 生成方法: `$imagegen` built-in image generation
- 元画像保存先: `C:\Users\umiga\.codex\generated_images\019e5986-75a2-7ee2-8488-1ef4e6aa7bc4\ig_07bf56a25547ecae016a22c6b25e488191be64dc5c3c40e446.png`
- プロンプト概要: 16bit/GBA風、高密度なトップダウン2DアクションRPGの森バイオーム。草地、石畳、水辺、木、茂み、キノコ、切り株、小広場、自然に曲がる通路を含む。単色地面、巨大な四角い道、円や四角だけの木、フラット図形表現を避ける。
- 扱い: 仮コンセプト画像。既存作品のコピーではなく、今後のオリジナル素材制作の方向確認に使う。
