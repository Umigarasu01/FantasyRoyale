# Implementation Status

> **Legacy:** 2026-08-10以前の実装状況Ledger。現行状態はProduct Spec、Design Doc、Active ExecPlan、Code / Testを照合する。

## 2026-08-08 本番Character・HP・戦闘基盤 Step 2

状態: **実装済み / Unity Scene再生成・EditMode・PlayMode検証済み / Step 3設計相談待ち**

### 現行契約

- `CharacterMoveCommand`は純C#の水平・垂直入力値。単位円内のアナログ量を維持し、斜め過速だけを防ぐ。
- 人間、CPU、Networkは同じCommandを`ICharacterMoveCommandTarget`へ渡す。入力元ごとにActorの移動処理を複製しない。
- `CharacterActor2D`は本番`CharacterHealth`、Dynamic Rigidbody2D、足元Capsule、移動速度を接続し、入力機器やCameraを参照しない。
- HP 0では移動Commandを停止する。ColliderによるMapCollision解決はUnity Physics、HPとCommandはGameplay Charactersが担当する。
- `HumanCharacterMoveInputAdapter`はRuntime複製されたInput Actionを読み、Move Commandへ変換する。

### 実装

- Core: `CharacterMoveCommand`、`ICharacterMoveCommandTarget`、EditMode Test 4件。
- Unity: `CharacterActor2D`、`HumanCharacterMoveInputAdapter`、専用Assembly。
- Preview: 直接`FixedUpdate`移動を削除し、本番Actor経路へ移行。
- PlayMode: Actor / Body / Foot Collider / Health接続、人間入力Command、Collision、Camera、撃破時停止を実Sceneで検証。

### 検証

- C# 12 Project: **0 warning / 0 error**。
- Exploration Preview Debug Scene再生成: 成功。
- Unity EditMode Test: **83 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。

### 未実装・要相談

- 攻撃 / 特殊アクションCommand、向き・照準、命中判定、アクション時間、装備Definition / Runtime装備状態。
- 正式Character Prefab / Sprite / Animation、CPU / Network入力Adapter、撃破後の観戦Controller。

## 2026-08-08 本番Character・HP・戦闘基盤 Step 1

状態: **実装済み / Unity Scene再生成・EditMode・PlayMode検証済み**

### 現行契約

- `CharacterHealth`は純C#で最大HP、現在HP、生死を管理し、Unity Scene参照を持たない。
- ダメージと通常回復は`CharacterHealthChangeResult`へ変更種別、結果、要求量、実適用量、変更前後、撃破遷移を返す。
- HP 0は撃破状態。通常回復は生存中だけ適用し、撃破状態からの復活は行わない。
- Event RuntimeはGameplay Charactersの`ICharacterHealthTarget`を利用し、Preview固有HPへ依存しない。

### 実装

- `FantasyRoyale.Gameplay.Characters.Core`と`CharacterHealth`一式。
- `CharacterHealthTests` 5件と専用Editor Test Assembly。
- Event Context / 回復の泉Handler / Event EditMode Testを本番HPへ移行。
- Exploration PreviewのHPを本番`CharacterHealth`へ置換し、撃破時の泉拒否HUDとPlayMode回帰を追加。

### 検証

- C# 11 Project: **0 warning / 0 error**。
- Exploration Preview Debug Scene再生成: 成功。
- Unity EditMode Test: **79 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。

### 未実装

- 本番Character Actor / Prefab、移動Command、攻撃、装備、被弾表示、撃破後の観戦接続、復活専用処理。

## 2026-08-07 Event Pool抽選 Step 4

状態: **実装済み / Unity Scene再生成・EditMode・PlayMode検証済み**

### 現行契約

- EventSocketは`PoolCandidate`または`FixedDefinition`を明示する。Pool候補は固定定義を持たず、配置固有ID、Transform位置、正の個別接近半径を保存する。
- Event Pool ScriptableObjectはEvent定義List、整数重み、有効Socket数を保存する。Seedと配置結果はRuntime状態でありAssetへ書き戻さない。
- 抽選器はUnity型に依存せず、安定IDで入力順を正規化してからSeedで有効Socketを重複なし選択し、Eventを重み付き割当する。
- 固定配置はPool抽選を迂回し、従来どおり直接参照またはCatalog ID解決を使う。

### 実装

- `MapEventPlacementMode`、方式別Socket解決・半径・Validator契約。
- `MapEventPoolDefinition`、`MapEventPoolEntry`、Runtime Definition Dictionary。
- `MapEventPlacementSelector`、Candidate / Assignment / Plan、安定Xorshift32。
- 泉1種・重み1・有効数3の`BattleRoyaleReferenceEventPool.asset`。
- Reference Map 6候補のPoolCandidate化とPreview Seed `20260807`接続。
- EditMode 6件追加・更新、PlayModeの3/6有効化回帰。

### 検証

- C# 8 Project: **0 warning / 0 error**。
- Reference Map / Exploration Preview Debug Scene再生成: 成功。
- Unity EditMode Test: **73 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。

### 未実装

- 2種類目のEvent、地域・Tag・距離制約を含むPool、正式Match Seed、Cooldown、Save連携。

## 2026-08-07 Event Runtime共通化 Step 3

状態: **実装済み / Unity Scene再生成・EditMode・PlayMode検証済み**

### 現行契約

- Event実行経路は`MapEventExecutionService` → `MapEventHandlerRegistry` → Event固有Handler → `MapEventExecutionResult` → Presenterとする。
- Handlerは純C#のルール対象だけを扱い、UnityのGameObject、Transform、UI、Effect、Audioを直接操作しない。
- HandlerはMessage / Effect / Audioの意味IDをPresentation要求として返す。実際の文言生成と演出再生はPresenterが担当する。
- OneShot状態は効果成功後だけ共通実行サービスが更新する。拒否、未対応、不正では消費しない。

### 実装

- Gameplay Event Runtime Assembly、実行Outcome / Result / Context / Presentation Request。
- Definition型を一意KeyにするHandler Registryと、OneShot更新を統括するExecution Service。
- 回復の泉Handlerと最小HP対象契約。
- Preview専用HP対象、HUD Presenter、既存BootstrapのRegistry経由化。
- Scene非依存のEditMode Test 6件と、実Scene PlayMode TestのPresentation検査。

### 検証

- C# 8 Project: **0 warning / 0 error**。
- Exploration Preview Debug SceneのUnity再生成: 成功。
- Unity EditMode Test: **67 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。

### 未実装

- 本番Character HP API、正式なEffect / Audio Presenter、Event Pool抽選、2種類目のEvent、Cooldown、Save連携。

## 2026-08-06 EventSocket操作と回復の泉 Step 2

状態: **実装済み / Unity Scene再生成・EditMode・PlayMode検証済み**

### 現行契約

- EventSocketのTransform位置、個別接近半径、表示、MapCollisionを別々の情報として扱う。
- 接近判定はPlayer足元とSocket Transformの距離で行い、SocketへCollider / Triggerを追加しない。
- Event中心自体はMapCollision上でもよい。接近範囲内に足元Clearanceを満たすGroundが最低一つ必要。
- 同じEvent定義を複数Socketで共有し、使用済み状態はSocket ID単位のRuntime Dictionaryへ保存する。
- OneShotは効果成功後だけ消費する。

### 実装

- 泉派生定義、回復量、6 EventSocketの共有参照。
- SocketのScene View位置・半径編集と再生成時の手動値維持。
- 到達可能性ValidatorとEditMode Test。
- Exploration PreviewのE操作、仮HP、回復、OneShot状態、HUDとPlayMode Test。

### 検証

- C# 6 Project: **0 warning / 0 error**。
- 96x72 Reference MapとExploration Preview Debug SceneのUnity再生成: 成功。
- Unity EditMode Test: **61 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。E操作、回復成功後のOneShot消費、HP満タン時の非消費を実Scene経路で確認。

### 未実装

- 本番CharacterのHP API、Event Handler Registry、Event Pool抽選、Cooldown、Save連携。
- 泉専用の正式表示Prefabと演出。

## 2026-08-05 Event定義とSocket契約 Step 1

状態: **実装済み / Unity再生成・EditMode検証済み / 近接判定・E操作は未実装**

### 現行契約

- `EventSocket`は汎用イベントの配置情報だけを持ち、Socket ID、EventDefinition参照または定義ID、必要に応じた接近半径上書きを保存する。
- `MapEventDefinition`は不変設定をScriptableObjectとして保存する。OneShotを含む設定は共有Assetへ置き、使用済み状態はRuntimeへ分離する。
- `MapEventCatalog`はInspector編集用Listを正本とし、実行時にID検索用Dictionaryを構築する。IDはstring、重複・空ID・未設定・半径不正はErrorとする。

### 実装

- `MapEventDefinition`と`MapEventCatalog`をRuntime Assemblyへ追加。
- `MapSocketMarker`へSocket ID、Event定義参照 / ID、接近半径上書きと解決プロパティを追加。
- Reference / Sample BuilderがSocket IDを明示し、Reference Eventへ定義IDと初期半径を書き込む。
- `MapAuthoringValidator`へSocket ID一意性とEvent契約検証を追加。
- Catalog検索、ID重複、EventSocket契約、定義欠落のEditMode Testを追加。

### 検証

- dotnet Runtime / Editor / Editor.Tests / Assembly-CSharp-Editor: **全て0 warning / 0 error**。
- Complete Kit再構築・96x72 Reference Map再生成: 成功。Event 6点をSocket ID、定義ID、初期接近半径付きで保存。
- Unity EditMode Test: **58 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **3 passed / 0 failed / 0 skipped**。既存の探索移動、足元Y描画順、幹Collisionを再確認。

### 未実装

- 泉の具体的なScriptableObject Assetと回復処理。
- 近接表示、`E`操作、イベント発火、使用済みRuntime状態。

## 2026-08-04 Obstacle接地点Collision

状態: **実装済み / Unity再構築・EditMode・PlayMode検証済み**

### 現行契約

- Map配置の正本はUnity Scene。移動Physicsの正本はMapCollision Layer上のCollisionTilemapとCollisionBody。
- 水、外周、崖はCollisionTilemap。森、木、低木、岩、切株、倒木はPrefab直下のCollisionBody。
- 表示RootはDefault Layer、Collider / Rigidbody2Dなし。CollisionBodyはMapCollision Layer、Renderer / Rigidbody2D / Triggerなし、Collider一つ。
- 自動生成のObstacleFootprintCellsは配置間隔・経路計画用で、Playerを止める物理形状ではない。

### 実装

- `MapObstacleVisualMarker`へCollision Mode / Shape / Size / Offset / Rotation / Capsule Directionを追加。
- 10 Prefabへ素材別の幹・接地点Colliderを設定し、崖2種はTilemapFootprintを維持。
- Sample Builderと96x72 BuilderはTilemapFootprintだけをCollisionTilemapへ書き、論理占有範囲は別集合で維持する。
- ValidatorはMode別にTilemap CellまたはCollisionBodyのLayer、階層、Component、形状、Transformを検査する。
- PlaytestはSpawn時にMapCollision Layer全体との重複を確認する。

### 生成結果

- 30x20 Sampleと96x72 Reference Mapを再生成。
- Reference Map: Ground 6,912、Road 1,479、Water 352、CollisionTilemap 684 Cell、Obstacle 232、Decoration 115、Socket 60。
- 旧CollisionTilemap 1,927 Cellのうち、森・木・岩などの広い論理Footprintを物理Tileから除外し、Prefab内CollisionBodyへ置換。

### 検証

- dotnet `FantasyRoyale.MapAuthoringKit.Runtime`、`FantasyRoyale.MapAuthoringKit.Editor`、`FantasyRoyale.MapAuthoringKit.Editor.Tests`、`FantasyRoyale.Playtest.Runtime`、`FantasyRoyale.Playtest.PlayModeTests`、`Assembly-CSharp-Editor`: **全て0 warning / 0 error**。
- Unity Rebuild Complete Kit / Reference Map Build: 成功。
- Unity EditMode Test: **53 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **3 passed / 0 failed / 0 skipped**。実Reference Mapで幹中央が衝突し、樹冠側が同Collider外であることを確認。

## 2026-08-04 World Y描画順

状態: **実装済み / Unity再構築・EditMode・PlayMode検証済み**

### 現行契約

- Renderer2D Data: Transparency Sort Mode Custom Axis、軸`Vector3.up`。
- Sorting Layer: MapGround、MapDetail、WorldObjects、MapForeground、Effects、UI。`WorldObjects`は旧`Characters`のunique IDを維持する。
- Player、ObstacleVisuals、Props、TallGrass、Reeds: WorldObjects / Order 0 / Pivot。world Yが低い足元ほど手前。
- FlowerPatch / Wildflowers: MapDetail / Order 0 / Pivotの固定背面。
- Ground / Terrain: MapGround固定背面。Foreground: MapForeground固定前面。

### 実装

- `MapAuthoringKitBuilder`がRenderer2D Dataと表示Prefabを上記契約へ収束させる。
- `MapAuthoringValidator`がRenderer2D Data、Sorting Layer、Order、SpriteSortPointを検査する。
- `BattleRoyaleExplorationPreviewDebug`が仮PlayerをWorldObjects / Order 0 / Pivotへ設定し、Camera側にもCustom AxisをFallback設定する。
- Asset Contract Test 2件、Validator Test Case 3件、PlayMode Test 1件を追加した。

### 検証

- dotnet `FantasyRoyale.Playtest.Runtime`、`FantasyRoyale.MapAuthoringKit.Editor`、`FantasyRoyale.MapAuthoringKit.Editor.Tests`、`Assembly-CSharp-Editor`、`FantasyRoyale.Playtest.PlayModeTests`: **全て0 warning / 0 error**。
- Rebuild Complete Kitと`GbaForestBattleRoyaleReference`再生成: 成功。
- Unity EditMode Test: **48 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **2 passed / 0 failed / 0 skipped**。

## 2026-08-04 Exploration Preview Debug Step 1

状態: **実装済み / Unity Scene生成・PlayMode・既存回帰テスト済み**

### 実装

- Playtest Scene: `Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`。
- Runtime: `BattleRoyaleExplorationPreviewDebug`。Reference MapのAdditive読込、PlayerStart Spawn、Move Action、Dynamic Rigidbody2D、足元Collider、Camera追従、確認HUDを担当する。
- Editor: `BattleRoyaleExplorationPreviewDebugSceneBuilder`。Playtest Sceneの再生成、入力設定、Build Settings登録、保存後検査を担当する。
- Player表示はRuntime生成の確認Marker。正式Character素材は追加していない。
- Map配置の正本はReference Scene、移動判定の正本は`CollisionTilemap`のまま変更していない。

### 検証

- PlayMode: Reference Map読込、PlayerStart Spawn、仮想WASD入力、空き地移動、実Collision Cellでの停止、Camera追従・Z維持・1 / 32 unit Snapを一括確認。
- Unity PlayMode Test: **1 passed / 0 failed / 0 skipped**。
- Unity EditMode Test: **43 passed / 0 failed / 0 skipped**。
- dotnet `FantasyRoyale.Playtest.Runtime`、`Assembly-CSharp-Editor`、`FantasyRoyale.Playtest.PlayModeTests`: **全て0 warning / 0 error**。

### 未実装

- Event / Loot / Enemy / Merchant SocketのRuntime選定・生成。
- 近接UI、Interact、イベント発火。
- 本番Character / 入力Command、戦闘、CPU、試合進行、エリア収縮。

次の実装候補はEvent Socket近接とE操作の一度限りのDebug発火。仕様確定や実装完了としては扱わない。

## 2026-08-03 96x72 Battle Royale Reference Map

状態: **実装済み / Unity生成・Validator・Camera Capture・目視QA・EditMode回帰テスト済み**

### 実装

- Scene: `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`。
- Builder: `GbaForestBattleRoyaleMapBuilder`。30x20の回帰Sampleは変更せず、96x72専用Sceneを別出力する。
- Ground 6,912 Cell、Road 1,479 Cell、Water 352 Cell、CollisionTilemap 684 Cell。森・木・岩などはPrefab内CollisionBodyへ移行。
- 参加者開始地点はPlayer 1 + CPU 5。7 Landmark、16 Enemy、22 Loot、3 Merchant、6 Eventを含めてSocketは計60点。
- Obstacle Visual 232 Instance、Decoration 115 Instance、Props 16 Instance。表示RootはCollider / Rigidbodyを持たず、規定のObstacleだけが直下CollisionBodyを持つ。
- 道路は短い直交区間を段差状につないだ複数LoopをRadius 1で3 Cell幅化し、全経路を単一の4近傍Componentにする。Stoneは中央・北・東水辺・南の小広場へ限定する。
- 東の池は行ごとに幅を変えた不規則なBlob-47水域と小島を持ち、森林は大型Stamp、単木、低木、岩、倒木を1 / 32 unit配置で混在させる。

### 検証

- Builder内の事前検査: 範囲、道路連結、道路 / Water / Collision非重複、Socket範囲・開始地点間隔、主要地点への歩行到達性、Instance名一意性が成功。
- `MapAuthoringValidator`: Errorなし。
- dotnet `Assembly-CSharp-Editor`: 0 warning / 0 error。
- Unity EditMode Test: **43 passed / 0 failed / 0 skipped**。
- QA画像: `gba-forest-battle-royale-reference-overview.png`、`-central.png`、`-waterfront.png`。

試合時間はScene寸法だけでは確定しない。約20分の目標尺は、移動速度、Camera、エリア収縮を接続したPlayMode計測で引き続き検証する。

## 2026-08-03 Map Authoring Kit v4.3

状態: **実装済み / 自動テスト・Unity再構築・Camera Capture・目視QA済み**

### 現行契約

- Map配置の正本はUnity Scene、移動Physicsの正本はMapCollision Layer上の不可視`CollisionTilemap`と`CollisionBody`。
- Grid配下はGround / Terrain / Collision / Foregroundの4 Tilemap。Decorations / ObstacleVisuals / PropsはGrid外のplain Transformへ1 / 32 unit単位で自由配置する。
- Unity表示素材は`fantasyroyale.map-authoring-atlas.v4.3`の単一Production Atlas、205 named Spriteだけを参照する。
- Family内訳はGrass 8、Dirt 64、Stone 64、Water 50、Detail 4、Props 10、Obstacle Visual 5。
- Dirt / StoneはCardinal-16の全16 Maskに各4差分を持つ。非全面Maskの路面外は透明なGround underlayとし、各接続辺は中央`[6,26)`の20px固定Socketを使う。
- 接続辺の残り両端6pxは、その辺と直交する2方向のbitが両方とも接続する場合に限り埋める。直線Mask `05` / `0a`は20px Socketだけで、周期的なタブを付けない。太い道路の内角はAND条件で埋め、芝穴を残さない。
- Dirt / Stoneは共通`road` Groupで材質境界を接続する。表示差分は`RoadConnectionRuleTile`が横4 Cell内で4種を各1回使い、座標・Rule ID・SeedのHashで24順列から決定する。同一差分の横連続は最大2 Cell。
- WaterはBlob-47、全面Mask `ff`だけ4 Surface差分。Grassは装飾なし8差分の`GroundVariationTile`。表示差分にShader、Runtime Pixel合成、回転、反転を使わない。
- Sample道路はWater Cell確定後にWaypointをRadius 1で3 Cell幅化し、Stone広場を統合して範囲・Water非重複・4近傍連結を一括検証してからDirt / Stoneを配置する。

### v4.3成果物

- Production Atlas: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`。
- Catalog: `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`。
- Road Edge Source: `Assets/Art/Generated/MapAuthoring/Source/Edges/`のDirt / Stone各1 Sheet、各4輪郭差分。
- QA Atlas: `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v4-3.png`。
- Unity Sample: `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`。
- Scene Templateと30x20 Sample、Ground / Collision Palette、19表示Prefab、10 CollisionBody、不可視Collision Tile。

### v4.3検証

- Atlas生成、Unity Rebuild、Validator、Camera Capture、目視QA: 成功。
- Unity EditMode Test: **53 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **3 passed / 0 failed / 0 skipped**。
- Production Atlas SHA-256: `412C63A03CD84472A9928DA5BF687903DB47AD3FF75DAECD246222EDD50D5FF4`。
- Catalog SHA-256: `EA6A6D111FF41DE5081D7537C5A20D0F74F2854D1C492D747FF94F9070B68600`。
- QA Atlas SHA-256: `34923AEC3E2721EA1D2491D18A8CE15C6AAF65E5FF2799001367080838B848F1`。
- Unity Sample SHA-256: `DD256066AC7DBD5C5A4BC798F750908A8C44D54A8DFE1483A01D9E3DE1E4275E`。
- 詳細契約は[Forest Production Atlas Contract](../../design-docs/map-authoring/forest-production-atlas-contract.md)、Scene構造は[GBA Forest Map Authoring Scene](../../design-docs/scenes/gba-forest-map-authoring-scene.md)、生成記録は[Generated Assets](../../development/assets/generated-assets.md)を参照する。

## 履歴: 2026-08-03 Map Authoring Kit v4.2

状態: **当時実装済み / v4.3により置換済み**

### 採用方式

- Map配置の正本: Unity Scene。
- 移動・占有判定の唯一の正本: 不可視`CollisionTilemap`。
- 画像デザインの正本: `Assets/Art/Map/Source/Forest/forest-gba-design-master.png`。
- Unity表示素材の正本: 単一Production Atlasとv4.2 Catalog、115 named Sprite。
- Tile編集: Grid / 4 Tilemap / Tile Palette / `GroundVariationTile` / Dirt・Stone・Water RuleTile。
- 自由配置表示: plain TransformのDecorations / ObstacleVisuals / Propsとrenderer-only Prefab。
- 道路生成: Water Cell確定 → WaypointをRadius 1で3 Cell幅化 → Stone広場統合 → 範囲・Water重複・4近傍連結の一括検証 → Dirt / Stone材質割当。
- 表示差分: Atlas SpriteとTile設定だけで行う。Shader、Runtime Pixel合成、独自EditorWindow、`.frmap`、JSON Map Exportは採用しない。

### v4.1からの修正理由

- Grass Surfaceへ花や草束に見える装飾が混入し、地面装飾を`Decorations`へ分離した設計と矛盾していた。
- Scene BuilderのGrass差分割当がmod 16で完全に繰り返し、広い地面へ周期的な模様を作っていた。
- Sample内で`dirt_0f` 46 Cell、`water_ff` 10 Cellがそれぞれ同一Spriteを反復し、広い道路内部と水面に人工的な繰り返しが見えていた。
- 道路とWaterを描画順で上書きできる余地をなくし、論理Cell集合を検証してから描画する必要があった。

### SourceとAsset

| 項目 | 実装 |
| --- | --- |
| Design Master | 1点。指定画像を変更不可の正本として継続使用 |
| Surface Source | AI生成4点。Grass 8差分、Dirt / Stone / Water各4差分。装飾を含めずMagenta gutterで分離 |
| Obstacle Visual Source | 5点。正本参照、Magenta Key除去済み |
| Decoration Source | 4点。正本参照、Magenta Key除去済み、足元中央Pivot |
| Production Atlas | 1024x1024 PNG 1点 |
| Catalog | `fantasyroyale.map-authoring-atlas.v4.2`、115 named Sprite |
| Ground Variation | 8 Spriteを座標とSeedの32bit 2D hashで選ぶ`GroundVariationTile` |
| RuleTile | 共通`road` GroupのDirt / Stone Cardinal-16、Water Blob-47の3点 |
| 頻出全面Mask | Dirt `0f`、Stone `0f`、Water `ff`だけ各4 SpriteのRandom出力。他MaskはSingle出力 |
| Transform | 全RuleでFixed。Random出力でも回転・反転しない |
| Standard Tile | Stone Variant、不可視Collision Tile |
| Tile Palette | Ground / Collisionの2点 |
| 表示Prefab | 19点。全てDefault Layer、Collider2D / Rigidbody2Dなし |
| Obstacle Marker | 12 Prefabへ`MapObstacleVisualMarker`を設定 |
| Shader | 追加なし |

### v4.2 Atlas

| Family | Sprite数 |
| --- | ---: |
| Grass | 8 |
| Dirt | 19 |
| Stone | 19 |
| Water | 50 |
| Detail | 4 |
| Props | 10 |
| Obstacle Visual | 5 |
| **合計** | **115** |

Dirt / StoneはCardinal-16の16基本Spriteへ全面Mask差分3点を追加し、`0f`を計4差分とする。WaterはBlob-47の47基本Spriteへ全面Mask差分3点を追加し、`ff`を計4差分とする。境界MaskのTopologyは増やさず、広い面で頻出する全面Maskだけを変化させる。AI Surface Sheetは質感Sourceとして使い、接続形状とSprite名はPackerが決定する。

Detail 4点はCatalog Family名を互換のため維持するが、Tile Assetではなく`Decorations`配下のPrefabとして使う。TallGrass、FlowerPatch、Wildflowers、ReedsをGrass Surfaceへ焼き込まない。

### Ground Variation

- `Assets/Data/MapAuthoring/Tiles/Forest/Ground/GrassVariation.asset`へ8枚の低コントラストGrass SpriteとSeed 0を保存する。
- `GroundVariationTile`は符号付きXY座標とSeedを32bit avalanche hashへ直接混合する。Z座標を選択へ含めず、再構築後も同じXY Cellへ同じ差分を返す。
- Collider、任意Transform、GameObjectを持たず、Shaderや実行時Texture生成へ依存しない。
- Sample 600 Cellで8差分を全て使用し、出現数は`72 / 74 / 75 / 78 / 82 / 70 / 80 / 69`。
- Cardinal同一差分率は`138 / 1150 = 12.00%`。同一差分の最大連続長は横3、縦4 Cell。
- 固定Offset一致率はOffset 4が横`71 / 520 = 13.65%`・縦`51 / 480 = 10.63%`、Offset 8が横`61 / 440 = 13.86%`・縦`43 / 360 = 11.94%`、Offset 16が横`33 / 280 = 11.79%`・縦`15 / 120 = 12.50%`。旧mod 16完全周期はない。

### Sceneと道路

- MapRoot。
- Grid配下のGround、Terrain、Collision、Foregroundの4 Tilemap。
- MapRoot直下のDecorations、ObstacleVisuals、Props、Sockets、Main Camera。
- Decorations / ObstacleVisuals / PropsはGrid Componentを持たず、1 / 32 unit単位で自由配置する。
- 表示PrefabはDefault Layer、Collider2Dなし、Rigidbody2Dなし。CollisionTilemapだけがMapCollision LayerとColliderを持つ。
- `GbaForestMapAuthoringTemplate`、Scene Template、30x20の`GbaForestMapSample`をv4.2 Atlasで再構築済み。
- Sample生成はWaterの論理Cell集合を先に確定する。道路を3 Cell幅の単一集合として作り、Stone広場を統合し、Map範囲内、Water非重複、4近傍単一Componentを検証してからTerrainTilemapへ一括配置する。
- DirtとStoneは同じ`road`接続Groupを使い、材質境界でも道路を閉じない。

### 補助コード

- GroundVariationTile。
- MapSocketMarker。
- MapObstacleVisualMarker。
- GbaMapSpritePostprocessor。
- MapAuthoringValidator。
- GbaForestMapAuthoringSceneBuilder。
- MapAuthoringKitBuilder。
- MapRoadPathRasterizer。
- RoadConnectionRuleTile。
- MapAuthoringAssetContractTests。
- MapAuthoringValidatorTests。

### Validatorと自動テスト

- v4.2 Catalog、115 Sub-Sprite、単一Atlas参照。
- Grass 8差分と`GroundVariationTile`型、旧Grass Tileの再混入禁止。
- Dirt / Stone各16 Rule、Water 47 Rule。全面Maskだけ4 Sprite / Random、他Maskは1 Sprite / Single、全TransformはFixed。
- 4 Tilemapと3つのplain表示Transform Root。
- 表示PrefabのDefault Layer、Collider2D / Rigidbody2D禁止、1 / 32 unit Snap、Rotation identity、Scale one。
- MapObstacleVisualMarkerのSize / FootprintとCollisionTilemap Cellの一致。
- 道路のMap範囲、Water非重複、4近傍単一Component。

Validatorは報告だけを行い、Scene、Prefab、CollisionTilemapを自動修正しない。

### 検証

- Unity Rebuild: 成功。
- Unity Camera Capture: 成功。
- EditMode Test: 41 passed / 0 failed / 0 skipped。
- Grass Surface SHA-256: `E0EE4BC62090E3457B6E4EF36892DA4BA09CDE74D128B5C3FC13DC5D2A980EC0`。
- Dirt Surface SHA-256: `5957838B73439D3ADDCA10D2CF81159FF6ED00A6AEE7435CDCB6FB2BFC649690`。
- Stone Surface SHA-256: `E8DEC1D7E9E8B94B1DBCB35DA808EDDDE55E6B1C4EF9876E33DA03AB3DA8BBAE`。
- Water Surface SHA-256: `15C883CE055DD9DAF44FDC7B3993F4098BDAEC7BDB60BD91DA8F27F0B69C8DDD`。
- Production Atlas SHA-256: `13A2D2C6AD63A377CE6080F8AE4BD03093B8B1BA9161BBA84F3A2154951C96E9`。
- Sprite Catalog SHA-256: `DFEB397B73C7784D6C6EE7203B841AA0213E230F729EA529AD769898AB5648DE`。
- Production Atlas QA v4.2 SHA-256: `598216621F672A61BD7DA430C6B44BA5CF71807093410C06605553874BEBC213`。
- Unity Sample QA SHA-256: `A49438073B9AD04434A44D62F23B42A8097AF3C712813E36A17FD73850B93218`。

### QA成果物

- `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v4-2.png`
- `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`

## 履歴: 2026-08-02 Map Authoring Kit v4.1

状態: **当時実装済み / 2026-08-03のv4.2により置換済み**

### 採用方式

- Map配置の正本: Unity Scene。
- 移動・占有判定の唯一の正本: 不可視`CollisionTilemap`。
- 画像デザインの正本: `Assets/Art/Map/Source/Forest/forest-gba-design-master.png`。
- Unity表示素材の正本: 単一Production Atlasとv4.1 Catalog、102 named Sprite。
- Tile編集: Grid / 4 Tilemap / Tile Palette / Dirt・Stone・Water RuleTile。
- 自由配置表示: plain TransformのDecorations / ObstacleVisuals / Propsとrenderer-only Prefab。
- 道路生成: Waypoint → Radius 1の3 Cell幅化 → 曲がり・分岐統合 → 4近傍連結検証 → Dirt / Stone材質割当。
- 独自EditorWindow、`.frmap`、JSON Map Export、Runtime Pixel合成: 採用しない。

### SourceとAsset

| 項目 | 実装 |
| --- | --- |
| Design Master | 1点。指定画像を変更不可の正本として継続使用 |
| Obstacle Visual Source | 5点。正本参照、Magenta Key除去済み |
| Decoration Source | 4点。正本参照、Magenta Key除去済み、足元中央Pivot |
| Production Atlas | 1024x1024 PNG 1点 |
| Catalog | `fantasyroyale.map-authoring-atlas.v4.1`、102 named Sprite |
| RuleTile | 共通`road` GroupのDirt / Stone Cardinal-16、Water Blob-47の3点 |
| Standard Tile | Grass 4、Stone Variant、不可視Collision Tile |
| Tile Palette | Ground / Collisionの2点 |
| 表示Prefab | 19点。全てDefault Layer、Collider2D / Rigidbody2Dなし |
| Obstacle Marker | 12 PrefabへMapObstacleVisualMarkerを設定 |
| GameObject Brush | 不使用。`Assets/Data/MapAuthoring/Brushes` Folder全体を削除 |
| v3地形Asset | ForestWall / Cliff RuleTileとObstacle Paletteを削除 |

### v4.1 Atlas

| Family | Sprite数 |
| --- | ---: |
| Grass | 4 |
| Dirt | 16 |
| Stone | 16 |
| Water | 47 |
| Detail | 4 |
| Props | 10 |
| Obstacle Visual | 5 |
| **合計** | **102** |

Detail 4点はCatalog Family名を互換のため維持するが、Tile AssetではなくDecoration Prefabとして使う。Dirt / Stoneの16 MaskはAI Sourceを質感DonorとしてPackerが形状を決定し、開いた角を全面素材で埋める。`0f`は全面路面で、太い道路内部へ草穴を残さない。

Obstacle Visual名:

- `obstacle_forest_mass_wide`
- `obstacle_forest_mass_deep`
- `obstacle_forest_front_strip`
- `obstacle_cliff_straight_wide`
- `obstacle_cliff_outer_corner`

### Scene

- MapRoot。
- Grid配下のGround、Terrain、Collision、Foregroundの4 Tilemap。
- MapRoot直下のDecorations、ObstacleVisuals、Props、Sockets、Main Camera。
- Decorations / ObstacleVisuals / PropsはGrid Componentを持たない。
- 表示PrefabはDefault Layer、Collider2Dなし、Rigidbody2Dなし。
- Position 1 / 32 unit、Rotation 0、Scale 1。
- CollisionTilemapだけがMapCollision LayerとColliderを持つ。

`GbaForestMapAuthoringTemplate`、Scene Template、30x20の`GbaForestMapSample`をv4.1構造で再構築済み。Decorationsへ16装飾Instance、ObstacleVisualsへ12障害物表示、PropsへMushroomPatch / Chest / Signpostを配置し、Marker footprintに対応する不可視Collision Cellを保存する。Sample道路は3 Cell幅でDirtからStone広場まで単一Componentとする。

### 補助コード

- MapSocketMarker。
- MapObstacleVisualMarker。
- GbaMapSpritePostprocessor。
- MapAuthoringValidator。
- GbaForestMapAuthoringSceneBuilder。
- MapAuthoringKitBuilder。
- MapRoadPathRasterizer。
- RoadConnectionRuleTile。
- MapAuthoringAssetContractTests。
- MapAuthoringValidatorTests。

### Validator

- 4 Tilemapと3つのplain表示Transform Root。
- 表示PrefabのDefault Layer。
- Collider2D / Rigidbody2D禁止。
- Position 1 / 32 unit Snap、Rotation identity、Scale one。
- MapObstacleVisualMarkerのSizeと推奨Footprint。
- 推奨FootprintとCollisionTilemap Cellの一致。
- 旧DetailTilemap / ObstacleTilemap、表示Root配下のGrid / Tilemap再混入禁止。
- v4.1 Catalog、102 Sub-Sprite、単一Atlas参照。

検査は報告だけを行い、Scene、Prefab、CollisionTilemapを自動修正しない。

### 検証

- Unity Rebuild: 成功。
- EditMode Test: 39 passed / 0 failed / 0 skipped。
- dotnet `Assembly-CSharp-Editor`: 0 warning / 0 error。
- dotnet `Editor.Tests`: 0 warning / 0 error。
- Production Atlas SHA-256: `6E4CCC929B386BEBE9E3A3AAAC5B1F1373A9E3C4E5C36B3A0AB13ED95B6030C6`。
- Sprite Catalog SHA-256: `405F14B1A0D2B9081170A9FBC4981967C5B9BACC5F1C650B270620756E9B6F2E`。
- Production Atlas QA v4.1 SHA-256: `8F18CE22C6D31B917BF0E6C49901FBB995BD0EFE43CE4899A1BBEAB03F9821D1`。
- Unity Sample QA SHA-256: `EC6672D49B9D3F6B3E5135E1122F355C312E3B07F429500B43AC3DAC091BC0F2`。
- 目視QA: 装飾の矩形地面、道路内の草穴、Dirt / Stone境界の閉じた草縁、暗いCell格子がないことを確認済み。

### QA成果物

- `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v4-1.png`
- `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`

## 履歴: 2026-08-02 Map Authoring Kit v4

v4は102 Spriteの単一Atlasと判定分離を導入したが、TallGrass / FlowerPatch / Wildflowers / ReedsをDetail Tileとして扱い、Grid配下にDetailTilemapを持っていた。道路は1 Cell幅の手書きCell列で、Dirt / Stoneが互いを非接続扱いし、太い道路用Cardinal Spriteもなかった。v4.1で4 Decoration Prefab、4 Tilemap、共通`road` Group、3 Cell幅Rasterizer、面用Cardinal合成へ置換した。

## 履歴: 2026-08-02 Map Authoring Kit v3

状態: **実装済み / 自動テスト・Unity表示確認済み**

### 採用方式

- Mapの正本: Unity Scene。
- 画像デザインの正本: `Assets/Art/Map/Source/Forest/forest-gba-design-master.png`。
- Unity表示素材の正本: 1024x1024の単一Production Atlasとv3 Catalog。
- Tile編集: Grid / Tilemap / Tile Palette / RuleTile。
- Prop編集: Props Grid / Prefab / 1Cell GameObject Brush。
- テンプレート: Unity Scene Template。
- 独自EditorWindow、`.frmap`、JSON Map Export、Runtime Pixel合成: 未実装。

### SourceとAsset

| 項目 | 実装 |
| --- | --- |
| Design Master | 1点。指定画像を変更不可の正本として保存 |
| Extension Source Sheet | 6点。Dirt / Stone / Water / ForestWall / Cliff / Tree Variants |
| Production Atlas | 1024x1024 PNG 1点 |
| Catalog | `fantasyroyale.map-authoring-atlas.v3`、191 named Sprite |
| Grass | 4 Sprite |
| Dirt RuleTile | Cardinal-16、各Mask 1 Sprite、Single出力 |
| Stone RuleTile | Cardinal-16、各Mask 1 Sprite、Single出力 |
| Water RuleTile | Blob-47、各Mask 1 Sprite、Single出力 |
| ForestWall RuleTile | Blob-47、各Mask 1 Sprite、Single出力 |
| Cliff RuleTile | Blob-47、各Mask 1 Sprite、Single出力 |
| Detail Tile | 4 Sprite |
| Collision Paint Tile | 表示Spriteなし |
| Prop Prefab | 10点。Tree 3差分とその他7種 |
| GameObject Brush | 10点。各Prefabを持つ1Cell Brush |
| Tile Palette | Ground / Detail / Obstacleの3点 |

191 Spriteの内訳:

| Family | Sprite数 |
| --- | ---: |
| Grass | 4 |
| Dirt | 16 |
| Stone | 16 |
| Water | 47 |
| ForestWall | 47 |
| Cliff | 47 |
| Detail | 4 |
| Props | 10 |

保存先:

- `Assets/Art/Map/Source/Forest`
- `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png`
- `Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json`
- `Assets/Data/MapAuthoring/Tiles/Forest`
- `Assets/Data/MapAuthoring/Palettes/Forest`
- `Assets/Data/MapAuthoring/Brushes/Forest`
- `Assets/Prefabs/MapAuthoring/Forest`

旧366 Loose PNG、旧 `asset-manifest.json`、旧Candidate Atlas、旧Program作画Scriptは削除済み。Tile、RuleTile、Prefabの表示Spriteは単一Production AtlasのSub-Spriteへ移行した。

### Scene

- `GbaForestMapAuthoringTemplate.unity`: 空の標準階層とProps Grid。
- `GbaForestMapAuthoringTemplate.scenetemplate`: New Sceneへ登録する正式Template。
- `GbaForestMapSample.unity`: 全Layer、Rule Family、Prefab、Socketの確認Scene。

標準階層:

- MapRoot。
- Grid配下の6 Tilemap。
- Grid Componentを持つProps。
- Sockets。
- 制作用Orthographic Camera。

Sampleはv3新素材だけで構成した30x20 Cell。北西の段状ForestWall、南西から中央と池へ続くS字Dirt、5x3 Stone祠広場、島と南へ伸びる水路を持つWater、南東のCliff段丘を含む。Tree 3種を計8本と全Prop種、Water Collision、手置きDetailを配置する。TreeWest01とTreeEast01は同じTreeMedium01 Prefabを参照し、同一Prefabを別Instanceとして反復配置できることを確認する。CameraはPosition `(-0.5, -0.25)`、Orthographic Size `10.6`。

### 補助コード

- MapSocketMarker。
- GbaMapSpritePostprocessor。
- MapAuthoringValidator。
- GbaForestMapAuthoringSceneBuilder。
- MapAuthoringKitBuilder。
- MapAuthoringAssetContractTests。
- MapAuthoringValidatorTests。

### Layer

- Sorting: MapGround、MapDetail、Characters、MapForeground、Effects、UI。
- Physics: MapCollision、MapInteraction。
- CollisionTilemapはRendererを無効化し、CollisionTile自体も表示Spriteを持たない。

### 検証

- Unity version: 6000.4.3f1。
- Rebuild Complete Kit: exit 0。
- 生成SampleのValidator: exit 0 / Error 0。
- Camera Capture: exit 0。
- EditMode Test: 18 passed / 0 failed / 0 skipped。
- dotnet Runtime / Editor / Editor.Tests / Assembly-CSharp-Editor: すべて0 warning / 0 error。
- Production Atlas SHA-256: `75D5560B1E68CD970C9F5B7895C4C5B54CA57C7826B4631D10EA8CE9F639BE2A`。
- Sprite Catalog SHA-256: `21DD3E7D0E042DD3E76B71F59C34E8579EF89A4851160D6C70B5224DE1657D42`。
- Production Atlas QA SHA-256: `53834EFA45C55C2FFC50312D13BA05947E58E690F831682AFA083EF4F8762CD2`。
- Atlas Catalog 191 Sprite、Single Rule出力、不可視Collisionを自動確認済み。
- PackerはWater 47 Spriteが全32x32 / Alpha 255であることをfail-fastし、広義MagentaをCrop境界検出だけに限定して元RGBを保持する。
- ForestWallの旧140x140 Cell全体縮小はMagenta / Alpha insetを残し、反復時に横3px、縦2pxの背景露出を発生させていた。旧 `ff` 外周4辺はOpaque 0、接続136辺中の境界到達は6辺だった。
- 全47 Maskへ正方形の共通Socket Crop `(x=20, y=16, width=110, height=110)` を適用し、縦横比を維持してNearest 32px化した。修正後は接続136 / 136辺が境界到達、非接続52 / 52辺が境界非到達、`ff` は1024 / 1024 Opaqueかつ4辺各32 / 32 Opaque。接続辺のOpaque幅は最小17px、平均26.07px。向かい合う全SocketのOpaque重なりは12px以上（実測最小は東西13px、南北15px）。PackerへMask / Edge / Socket fail-fastを追加済み。
- 197表示参照はすべて単一Atlasを参照し、旧素材参照は0件。
- Production Atlas QA画像とUnity Camera Previewを目視確認済み。黒い地面格子と紫の水フリンジは最終Captureで消失した。
- Unity Camera Preview SHA-256: `9258F7F79DDD0B4966782FDDE37EA3389DB5AB3C69062E64D00C0EC4A343572C`。

### QA成果物

- `Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v3.png`
- `Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png`

## 履歴: 2026-08-01 Map Authoring Kit v2

状態: **当時実装済み / 2026-08-02のv3により置換済み**

- 366 Loose PNGを使用した。
- Dirt Cardinal-16は各Mask 3 Random差分、Stone Cardinal-16は各Mask 1差分であった。
- Water / ForestWall / Cliff Blob-47は各Mask 2 Random差分であった。
- 旧Candidate Atlas v1 / v2を併用し、Programで接続Spriteと差分を描画・正規化していた。
- Tree 3 Prefab、全10 PrefabのGameObject Brush、Scene TemplateというUnity制作方式はv3へ継承した。
- 当時の最終確認はUnity 6000.4.3f1、EditMode Test 15 passed、Sample Validator Error 0であった。
- v3ではRandom差分とProgram作画を廃止し、正本準拠のSource Sheet、単一Atlas、Catalog選択へ置換した。

## 履歴: 2026-07-10 Map Authoring Kit v1

状態: **当時実装済み / v2を経てv3により置換済み**

- Production 71 Loose PNG。
- Dirt / Stone / WaterはCardinal-16。
- 森壁、幹、崖は単一Tile。
- Prop Prefab 8点、GameObject Brushなし。
- 当時の最終確認はUnity 6000.4.3f1、EditMode Test 10 passed、Sample Validator Error 0であった。

### v1で削除した旧方式

- BiomeAtlasMapSceneBuilderと生成Asset。
- FR_Map系Core。
- Exploration系MonoBehaviourとScriptableObject。
- Milestone1OrganicMapScene。
- 未使用のAnokolisa候補フォルダ。
- 対応する旧Script解説とScene構造ノート。

削除前状態はGit snapshot ce2d1d3で参照できる。
