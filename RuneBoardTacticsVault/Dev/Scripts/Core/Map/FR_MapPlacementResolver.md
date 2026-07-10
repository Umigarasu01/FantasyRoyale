# FR_MapPlacementResolver

Seed、マップスロット、配置候補から `FR_PlacementResult` を決めるCoreロジック。Unity非依存で、同じSeedなら同じ配置になる。候補のカテゴリ、Biome、タグ、サイズ、Footprintを検証してから重み付き抽選する。
