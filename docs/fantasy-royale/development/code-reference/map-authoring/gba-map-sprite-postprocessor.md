# GbaMapSpritePostprocessor

状態: **v4.3実装・Import / Validator / EditMode Test検証済み**

## 役割

単一Production AtlasへPixel Art向けImport設定とCatalog由来のnamed Sub-Sprite分割を適用する`AssetPostprocessor`。

画像Pixelは変更せず、Unityへ作画責務を持ち込まない。

## 対象

- Script: `Assets/Scripts/MapAuthoringKit/Editor/GbaMapSpritePostprocessor.cs`
- Atlas: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`
- Catalog: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`
- v4.3 Required schema: `fantasyroyale.map-authoring-atlas.v4.3`
- v4.3 Sub-Sprite: 205点

Design Master、生成Source画像、Concept画像、旧Loose PNGへは適用しない。

## 強制する設定

- Texture Type: `Sprite`
- Sprite Mode: `Multiple`
- Sprite Mesh: `Full Rect`
- Pixels Per Unit: `32`
- Filter Mode: `Point`
- Compression: `None / Uncompressed`
- Mip Maps: `Off`
- Wrap Mode: `Clamp`
- Alpha Is Transparency: `On`
- NPOT Scale: `None`
- Ground / Terrain Tile Pivot: 中央
- Detail / Prop / Obstacle Pivot: 足元中央

## v4.3 Catalog処理

- schema、Atlas path、Canvas、Tile size、Sprite名重複、Rect範囲、Pivot、Family、Source出典をImport前に検査する。
- Family内訳はGrass 8、Dirt 64、Stone 64、Water 50、Detail 4、Props 10、Obstacle Visual 5の計205点。
- Grassは`ground_grass_01`〜`08`。Dirt / Stoneは全16 Maskについて基準名と`_v02`〜`_v04`の4差分、Waterは47 Maskに全面`ff`の追加差分3点を持つ。
- Detail family名と`detail_` Sprite名は参照互換のため維持するが、Unity上の用途はDetail TileではなくDecoration Prefabとする。
- Detail 4点は透過Spriteかつ足元中央Pivotとして登録し、TallGrass、FlowerPatch、Wildflowers、Reeds Prefabから参照する。
- `ForestWall`と`Cliff` Familyをv4.3 Catalogとして受理しない。
- Unity Sprite Data Providerを使い、CatalogのRectとPivotをMultiple Spriteへ登録する。
- 既存の同名Sprite IDを優先して再利用し、Tile、RuleTile、Prefab参照を維持する。
- 新規SpriteのIDはschemaとSprite名から決定し、端末や再Import順序に依存させない。

Atlas build処理はGrass、Dirt、Stone、Waterの4 Surface Source Sheetに加え、Dirt / Stoneの2 Road Edge Profile Sheetを使う。各Catalog Entryには元SheetのpathとRectをSource出典として保持し、Postprocessorはその出典が欠落または不正なEntryをImport前に拒否する。

- Grass Source: 4 x 2から8 Sprite。
- Dirt / Stone / Water Source: 各2 x 2から全面質感4 Sprite。
- Dirt / Stone Edge Profile Source: 各2 x 2から道端輪郭4差分。
- Dirt / Stoneは接続Mask用Source、全面質感、道端輪郭を合成して全16 Mask x 4差分を作る。Water岸は従来どおり接続Mask用Sourceを使う。
- Dirt / Stoneの完成Spriteは各辺の中央`[6, 26)`を20px Socketとし、角はその辺と直交辺のCardinal bitが両方開く場合だけ不透明にする。PostprocessorはPixelを変更せず、この実Pixel契約は`MapAuthoringAssetContractTests`がchecked-in Atlas PNGを直接読んで検査する。

Canvas Size、Pixel内容、半透明Pixelは自動変更しない。Import後のAtlas、Catalog、Sub-Sprite一致確認は`MapAuthoringValidator`が担当する。

v4.3 + Event Socket操作の`MapAuthoringAssetContractTests`と`MapAuthoringValidatorTests`を合わせたMap Authoring EditMode Suiteは**63 passed / 0 failed / 0 skipped**。

## 旧方式の履歴

- v3はschema v3 / 191 Spriteを検査していた。
- v4はschema v4 / 102 Spriteで、Detail 4点を中央PivotのTile用途として扱っていた。
- v4.1はschema v4.1 / 102 SpriteとDetail family名を維持し、Detail 4点を足元中央PivotのPrefab用途へ変更した。
- v4.2はschema v4.2 / 115 Spriteへ更新し、4 Surface Source Sheet由来のGrass 8差分と全面地形差分を追加した。v4.1以前は履歴としてのみ扱う。
- v4.3はschema v4.3 / 205 Spriteへ更新し、2 Road Edge Profile Sheet由来のDirt / Stone全Mask差分を追加した。v4.2以前のSprite契約は履歴としてのみ扱う。
