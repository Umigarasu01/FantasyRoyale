# MapAuthoringValidatorTests

状態: **v4.3 + Event Step 2契約 Test実装 / Unity EditMode検証済み**

## 役割

Map Authoring Kit v4.3の4 Tilemap構造、表示Root分離、自由配置Transform、Tilemap / CollisionBody判定分離、Socket幾何判定、Event定義CatalogをPreview Scene上で確認するEditMode Test。

- Script: `Assets/Scripts/MapAuthoringKit/Tests/Editor/MapAuthoringValidatorTests.cs`
- Test case定義: 40件。
- Editor Test AssemblyのC#ビルド: **0 warnings / 0 errors**。
- `MapAuthoringAssetContractTests`と合わせたEditMode Suite: **61 passed / 0 failed / 0 skipped**。

## v4.3主な確認内容

- 正しい4 TilemapとDecorations / ObstacleVisuals / Propsを持つHierarchyが構造Errorにならない。
- 必須TilemapとDecorationsの欠落を検出する。
- 旧DetailTilemap / ObstacleTilemapの再混入を検出する。
- Decorations配下のGrid、Tilemap、TilemapRendererを個別に検出する。
- Decorations / PropsとObstacleの表示Rootで、規定外Collider2D、Rigidbody2D、非Default Layerを検出する。正常なCollisionBodyは汎用表示違反にしない。
- 3表示Rootの各々でPositionの1 / 32 unit逸脱、Rotation 0逸脱、Scale 1逸脱を検出する。
- Decorations / ObstacleVisuals / Propsの各分類で、WorldObjectsまたはMapDetailの誤り、Order 0逸脱、Pivot以外のSort Pointを検出する。
- Decorations上のMapObstacleVisualMarkerを分類違反として検出する。
- TilemapFootprint方式の無効Size、Footprint / Collision Cell差分、CollisionBody混入を検出する。
- CollisionBody方式の正常系と、Body欠落・重複、Layer、Position、Rotation、Scale、Renderer、Rigidbody2D、Collider数・配置・有効性・Trigger、Shape、Size、Capsule Directionの不一致を検出する。
- ValidatorがMarkerからCollisionを生成・更新・修正しない。
- Socketの面積重複とGround範囲外を検出し、辺接触だけは重複扱いしない。
- Socket IDの欠落・重複、Event定義参照 / IDの欠落・不一致、接近半径不正、非EventへのEvent情報混入を検出する。
- Event中心がMapCollision上でも接近円内に足元Clearanceがあれば許可し、円全体が塞がれた場合だけ到達不能Errorにする。
- Socket Transformの非Cell位置と個別半径が独立し、上書きを0へ戻すと定義の標準半径へ戻る。
- EventDefinitionのScriptableObject設定、CatalogのList保存からID検索Dictionaryへの変換、重複IDを検出する。
- Sprite Postprocessorが単一Production Atlasだけを対象にする。
- 実Projectのv4.3 Catalog、205 Sub-Sprite、Rect、Pivot、Import設定にErrorがない。

Test用Objectは保存対象外のPreview Sceneへ作り、終了時に破棄するため、制作中Sceneを変更しない。

## 旧構造履歴

- v3 Testは6 Tilemap、191 Sub-Sprite、Props Gridを期待し、当時のSuiteは18 passedだった。
- v4 Testは5 TilemapとObstacleVisuals / Propsを期待し、当時の全Suiteは23 passedだった。
- v4.1 Testでは4 Tilemapと3表示Rootの自由配置契約へ置換した。旧件数は履歴としてのみ保持する。
- v4.2で地面差分、v4.3で道路端差分と205 Sprite契約を追加した。Validator Testの26件はScene構造を継続検証し、Asset Contract Test 17件と合わせて43件とする。
