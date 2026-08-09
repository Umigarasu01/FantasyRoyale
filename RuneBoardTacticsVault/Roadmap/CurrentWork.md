# Current Work

## 2026-08-08 本番Character・HP・戦闘基盤 Step 2

状態: **実装・Unity再生成・EditMode / PlayMode検証完了 / Step 3設計相談待ち**

今回の設計:

- 人間、CPU、将来のNetwork入力は、入力機器やUnity型を含まない同じ`CharacterMoveCommand`を生成する。
- `CharacterActor2D`は入力元を知らず、最新Commandと本番`CharacterHealth`だけを受けて`Rigidbody2D.MovePosition`へ接続する。
- 人間操作は`HumanCharacterMoveInputAdapter`がUnity Input Systemの`Move` Actionを純C# Commandへ変換する。Preview BootstrapはPhysicsを直接更新しない。
- 既存Previewで検証済みのDynamic Rigidbody2D、足元Capsule、MapCollision、移動速度、足元Y描画順をそのまま本番Actor経路へ移す。
- HP 0ではActorが移動Commandを破棄して停止し、死亡後は観戦のみという既存方針と矛盾させない。

実装済み:

- 純C# `CharacterMoveCommand`と`ICharacterMoveCommandTarget`。アナログ量を保持し、単位円外だけをClampする。
- `FantasyRoyale.Gameplay.Characters.Unity` Assembly、`CharacterActor2D`、`HumanCharacterMoveInputAdapter`。
- Exploration Previewを「Input System → Human Adapter → Move Command → Character Actor → Rigidbody2D」へ移行。
- Event Context、Camera追従、Socket距離、Test APIもActorが持つBody / Healthを参照するよう統一。
- Command単体Test 4件と、実Scene上のActor構成、人間入力変換、Collision移動、撃破時停止のPlayMode回帰。

確認済み:

- 生成済みC# 12 Project: **0 warning / 0 error**。
- Exploration Preview Debug SceneのUnity再生成: 成功。
- Unity EditMode Test: **83 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。

次の相談地点:

- 攻撃方向を4方向、8方向、自由照準のどれにするか。
- 通常攻撃Commandが持つ情報と、人間 / CPU / Network入力で共有する単位。
- 攻撃範囲のCore表現と、Unity Physicsによる実命中判定の境界。
- 発生、持続、硬直、移動可否、割込みなどの最小アクション時間モデル。
- 装備が通常攻撃 / 特殊アクションを提供するDefinitionとRuntime装備状態の形。

## 2026-08-08 本番Character・HP・戦闘基盤 Step 1

状態: **実装・Unity再生成・EditMode / PlayMode検証完了**

今回の設計:

- CharacterのHP、生死、ダメージ、通常回復を`GameObject`や`Transform`へ依存しない純C# Coreへ置く。
- HP変更は要求量だけでなく、変更種別、適用・拒否・不正、実適用量、変更前後、撃破遷移を不変の結果として返す。
- HP 0を撃破状態とし、通常回復は撃破状態を解除しない。将来の復活は通常回復と分けた明示的な処理として追加する。
- Event RuntimeはPreview専用HP契約を持たず、本番`ICharacterHealthTarget`だけへ依存する。Unity側は結果を表示へ変換する。

実装済み:

- `FantasyRoyale.Gameplay.Characters.Core` Assemblyと`CharacterHealth`、`ICharacterHealthTarget`、HP変更結果。
- 初期値検証、過剰ダメージ / 回復のClamp、撃破遷移、撃破後の追加ダメージ / 通常回復拒否。
- 回復の泉HandlerとEvent実行Contextを本番HP契約へ移行。撃破中は専用Message Keyで拒否し、OneShotを消費しない。
- Exploration PreviewのPreview専用HP実装を削除し、確認用Playerも本番`CharacterHealth`を利用。
- Scene非依存のCharacter Health Test 5件、撃破中の泉拒否EditMode回帰、実Sceneでの非復活PlayMode回帰。

確認済み:

- 生成済みC# 11 Project: **0 warning / 0 error**。
- Exploration Preview Debug SceneのUnity再生成: 成功。
- Unity EditMode Test: **79 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。

今回含めないもの:

- 本番Character GameObject / Prefab、移動Command、通常攻撃、装備、武器範囲、被弾演出、敵、復活処理。

## 2026-08-07 Event Pool抽選 Step 4

状態: **実装・Unity再生成・EditMode / PlayMode検証完了**

今回の設計:

- EventSocketの配置方式を`PoolCandidate`と`FixedDefinition`へ明示分離する。通常候補は位置、Socket ID、個別接近半径だけを持ち、固定配置だけがEvent定義を直接参照する。
- `MapEventPoolDefinition`はInspector編集用ListへEvent定義、整数重み、有効Socket数を保存する。Seedと抽選結果は対戦状態なのでAssetへ保存しない。
- `MapEventPlacementSelector`はSocket ID、Event Definition ID、重み、有効数、Seedだけを受け取る純C#処理とする。入力順をID Sortで正規化し、Xorshift32で再現可能な配置計画を返す。
- 抽選は一部成功を許さない。ID空値・重複、非正重み、有効数超過を先に検証してから、Socketを重複なしで選び、Event種類を重み付きで割り当てる。

実装済み:

- `MapEventPlacementMode`、Pool候補Socket契約、方式別Validator / Scene View表示。
- `MapEventPoolDefinition`とRuntime ID Dictionary、`MapEventPlacementSelector`と不変な配置計画。
- `BattleRoyaleReferenceEventPool.asset`。初期値は泉1種・重み1・有効3地点。
- Reference Mapの`Event_01`〜`Event_06`をすべて`PoolCandidate`へ移行。直接Event定義参照は削除し、調整済みTransform位置と接近半径は維持。
- Preview開始時にSeed `20260807`で6候補から3地点を選び、Poolの定義を割り当てる。非選択地点は接近候補へ入れない。
- 同Seed・入力順非依存、候補数超過、Pool List / Dictionary、Scene契約、実Sceneでの3/6選択と泉操作を検証する回帰Test。

確認済み:

- dotnet 8 Project: **0 warning / 0 error**。
- Reference MapとExploration Preview Debug SceneのUnity再生成: 成功。
- Unity EditMode Test: **73 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。

今回含めないもの:

- 2種類目のEvent、配置間隔制約、地域Tag別Pool、正式Match Seed供給、Cooldown、Save連携。

## 2026-08-07 Event Runtime共通化 Step 3

状態: **実装・Unity再生成・EditMode / PlayMode検証完了**

今回の設計:

- `MapEventHandlerRegistry`は`MapEventDefinition`の実型をKeyとするRuntime DictionaryでHandlerを検索し、Playtest側へイベント種類ごとの条件分岐を増やさない。
- Handlerはルール対象だけを受け取り、成功・拒否・未対応・不正、適用値、Message / Effect / Audio Cue IDを`MapEventExecutionResult`として返す。GameObject、Transform、UI、Prefab生成、SE再生へ依存しない。
- `MapEventExecutionService`が既使用検査、Handler実行、成功後だけのOneShot更新を共通順序として保証する。
- Preview用HPとPresenterは`PreviewDebug`責務に残し、Handlerが返した意味IDを日本語HUDへ変換する。正式Effect / Audio再生は後続Stepとする。

実装済み:

- `FantasyRoyale.Gameplay.Events.Runtime` Assemblyと、実行契約、Registry、ExecutionService。
- `HealingFountainEventHandler`。HP契約へ回復量を適用し、成功時だけ回復演出Cueを返す。
- `PreviewDebugMapEventHealthTarget`と`PreviewDebugMapEventPresenter`。
- `BattleRoyaleExplorationPreviewDebug`をRegistry経由へ移行し、泉型判定、直接HP更新、Handler内UI文言生成を削除。
- Handler重複、回復成功、満タン拒否、未対応Event、使用済み拒否、非OneShotを検証するEditMode Test 6件。

確認済み:

- dotnet 8 Project: **0 warning / 0 error**。
- Exploration Preview Debug SceneのUnity再生成: 成功。
- Unity EditMode Test: **67 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。実Scene上でRegistry、Handler、Presentation Cue、HUD、成功後OneShotを確認。

今回含めないもの:

- 本番Character HP、正式なEffect / Audio Presenter、Event Pool抽選、2種類目のEvent、Cooldown、Save連携。

## 2026-08-06 EventSocket操作と回復の泉 Step 2

状態: **実装・Unity再生成・EditMode / PlayMode検証完了**

今回の設計:

- EventSocket中心はScene上のGameObject Transformを正本とし、Cell中心へ固定しない。定義の標準半径とSocket単位の上書き半径を位置とは別に扱う。
- Scene ViewでSocket ID、Event名、実効半径を表示し、Transform移動とRadius Handleを別々に操作する。接近円はCollider / Triggerではない。
- Event中心が水や障害物上でも、接近円内にPlayerが立てるGroundがあれば有効とする。
- Reference Mapの6 EventSocketは`Event_01`〜`Event_06`を配置固有IDとして維持し、同じ`healing-fountain-basic`定義を共有する。
- PlaytestはPlayer足元とSocket Transformの距離で最寄り候補を選び、Eで回復する。成功後だけSocket ID単位のRuntime DictionaryへOneShot状態を保存し、HP満タンでは消費しない。

実装済み:

- `HealingFountainEventDefinition`と回復量30の`HealingFountainBasic.asset`。
- `MapSocketMarker`のCatalog解決、実効半径解決、半径更新API、Event接近円Gizmo。
- `MapSocketMarkerEditor`のScene Label、Radius Handle、標準半径へ戻す操作。
- ValidatorのEvent接近範囲内Ground / MapCollision足元Clearance検査。
- Reference Builderの共有泉定義と、再生成前後でSocket IDにより手動Event位置・半径を維持する処理。
- Exploration PreviewのCatalog解決、仮HP 50 / 100、E操作、回復、使用済みDictionary、HUD。
- EditMode 3件、PlayMode 1件の回帰Test追加。

確認済み:

- dotnet Runtime / Editor / Editor.Tests / Playtest.Runtime / PlayModeTests / Assembly-CSharp-Editor: **0 warning / 0 error**。
- 96x72 Reference MapとExploration Preview Debug SceneのUnity再生成: 成功。
- Unity EditMode Test: **61 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **4 passed / 0 failed / 0 skipped**。実Scene上で移動、Collision、足元Y描画順、幹判定、E操作、回復、OneShot状態を確認。

## 2026-08-05 Event定義とSocket契約 Step 1

状態: **実装・Unity再生成・EditMode検証完了 / 近接判定・E操作は未着手**

今回の設計:

- `EventSocket`は泉、祠、罠、会話地点などの汎用配置ポイントとする。Event処理本体やRuntime状態は持たせない。
- Map SceneのEvent配置は、配置固有の`SocketId`、EventDefinition参照または`EventDefinitionId`、必要ならSocket単位の接近半径上書きを持つ。
- Eventの不変設定は`MapEventDefinition` ScriptableObjectへ保存し、対戦中の使用済み状態はSocket ID単位のRuntimeデータへ分離する。
- `MapEventCatalog`はInspector編集用の`List<MapEventDefinition>`を保存の正本とし、実行時だけ`Dictionary<string, MapEventDefinition>`を検索索引として構築する。ID重複、空ID、未設定、半径不正はErrorとする。

実装済み:

- `MapEventDefinition`、`MapEventCatalog`、`MapSocketMarker`のEvent項目、BuilderのEvent ID / 初期半径書き込み。
- `MapAuthoringValidator`のSocket ID一意性、Event定義、参照ID、接近半径、非Event混入の検査。
- Catalog検索、重複ID、EventSocket最小契約、定義欠落のEditMode回帰Test。

今回のStepではScriptableObject Assetの具体的な泉処理、近接UI、`E`操作、イベント発火は実装しない。次Stepで泉の定義AssetとRuntime接近処理を接続する。

確認結果:

- dotnet Runtime / Editor / Editor.Tests / Assembly-CSharp-Editor: **0 warning / 0 error**。
- Complete Kit再構築と96x72 Reference Map再生成: 成功。Event Socket 6点へSocket ID、`event-01`〜`event-06`、接近半径1.25を保存。
- Unity EditMode Test: **58 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **3 passed / 0 failed / 0 skipped**。既存の探索移動、足元Y描画順、幹Collisionを再確認。

## 2026-08-04 幹・接地点へ寄せた障害物判定

状態: **実装・Unity再構築・EditMode / PlayMode検証完了**

今回整理した原因:

- 従来は木の足元Pivotを整数座標へ置き、同じ整数を左下とする1x1 Collision Cellを塗っていた。このため判定中心が見た目より右上へ0.5 unitずれ、幹だけでなく樹冠側まで塞いでいた。
- 自動配置で必要な「木同士や道路を離す範囲」と、Playerを止める実際のPhysics形状を同じFootprintで扱っていた。

今回確定・実装した判断:

- 表示Rootと判定を分離したまま、Obstacle Prefab直下へ非表示`CollisionBody`を置く。表示RootはDefault Layer、Colliderなし。CollisionBodyはMapCollision Layer、Renderer / Rigidbody2D / Triggerなし、Collider一つ。
- 森3種は前縁の細いBox、Tree 3種・BlockingBush・MossyRock・Stumpは幹または接地点の水平Capsule、FallenLogは接地軸へ沿う回転Capsuleとする。
- 崖2種、水、外周はCell地形なのでCollisionTilemapを維持する。
- 論理Footprintは道路、Socket、Decoration、他Obstacleとの間隔確保と到達性計画へ残し、Physics Collisionとは分ける。
- PlaytestのSpawn確認とTestの空き地点探索は、CollisionTilemap一つではなくMapCollision Layer全体を調べる。

完成したもの:

- 10 Obstacle Prefabの素材別CollisionBody profileと、崖2種のTilemapFootprint mode。
- Mode、Shape、Size、Offset、Rotation、Capsule Directionを保持する`MapObstacleVisualMarker`契約。
- Mode別にScene保存判定を検査するValidatorと回帰Test。
- 30x20 Sampleと96x72 Reference Mapの再生成。Reference MapのCollisionTilemapは1,927 Cellから684 Cellへ減り、木・森・岩などはPrefab内CollisionBodyへ移行した。
- 実Reference Mapで「幹中央は衝突、樹冠側は同Collider外」を確認するPlayMode Test。

確認結果:

- dotnet Runtime / Editor / EditModeTests / PlayModeTests: **0 warning / 0 error**。
- Rebuild Complete Kitと96x72 Battle Royale Reference Map再生成: 成功。
- Unity EditMode Test: **53 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **3 passed / 0 failed / 0 skipped**。

この判定修正Stepは完了。Event Socket近接とE操作は未着手のまま次Stepへ残す。

## 2026-08-04 足元Yによる描画順整理

状態: **実装・Unity再構築・EditMode / PlayMode検証完了**

今回確定・実装した判断:

- 下側にあるものを手前とし、同じ描画層ではSprite Pivotのworld Yが低いものほど前へ描画する。
- URP 2Dの正本を`Assets/Settings/Renderer2D.asset`とし、Transparency Sort ModeをCustom Axis、軸を`Vector3.up`へ設定する。
- 旧`Characters` Sorting Layerはunique IDを維持したまま`WorldObjects`へ改名する。
- Player、ObstacleVisuals、Props、TallGrass、Reedsは`WorldObjects` / Order 0 / `SpriteSortPoint.Pivot`へ統一する。
- FlowerPatch / Wildflowersは平面装飾として`MapDetail`固定背面、Ground / Terrainは`MapGround`固定背面、Foregroundは`MapForeground`固定前面とする。

完成したもの:

- Kit再構築時にRenderer2D Dataと19表示Prefabの描画契約を揃える処理。
- Scene ValidatorによるRenderer2D設定、Sorting Layer、Order、Sort Pointの検査。
- EditModeのAsset Contract 2件、Validator 3件、PlayMode 1件の回帰検査追加。
- 仮PlayerとReference Map内の立体表示を同じ足元Yソートへ接続。

確認結果:

- dotnet Runtime / Editor / EditModeTests / PlayModeTests: **0 warning / 0 error**。
- Rebuild Complete Kitと96x72 Battle Royale Reference Map再生成: 成功。
- Unity EditMode Test: **48 passed / 0 failed / 0 skipped**。
- Unity PlayMode Test: **2 passed / 0 failed / 0 skipped**。

描画順Stepは完了。次はEvent Socket近接とE操作の確認Stepへ進められる。

## 2026-08-04 Exploration Preview Debug Step 1

状態: **移動・地形判定・Camera追従を実装、PlayMode検証完了**

今回の範囲:

- 96x72 Reference Mapを変更せず、別Playtest SceneからAdditive読込する。
- 単一`PlayerStart`へ確認専用Playerを生成し、WASD / Arrow Keysで移動する。
- MapCollision Layer上のCollisionTilemapとCollisionBodyで、崖、水際、木の幹などの不可侵領域で停止する。初回StepではCollisionTilemapだけだったが、後続の接地点判定Stepで更新した。
- Reference Sceneの単一Cameraを再利用し、Player追従、Map端Clamp、1 / 32 unit Snapを行う。

完成したもの:

- `Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity`。
- `BattleRoyaleExplorationPreviewDebug`と再生成用Scene Builder。
- 実Scene、実Input Action、実CollisionTilemapを通すPlayMode Test。
- Reference MapとPlaytest SceneのBuild Settings登録。

確認結果:

- PlayMode Test: **1 passed / 0 failed / 0 skipped**。
- 既存EditMode Test: **43 passed / 0 failed / 0 skipped**。
- dotnet Runtime / Editor / PlayModeTests: **0 warning / 0 error**。

このPlayer表示と直接移動は`PreviewDebug`の一時足場。本番Character、戦闘、HP、CPU、試合進行、エリア収縮は未実装であり、完成扱いにしない。

次の候補Step:

- Event Socketへの接近を検出し、範囲内表示とE操作で一度だけ確認イベントを発火する。
- Socket選定やイベント内容は未確定。次のStep開始前に範囲を確認する。

## 2026-08-03 96x72 Battle Royale Reference Map

状態: **基準Scene実装・Unity生成・Validator・撮影・目視QA・回帰テスト完了**

既存資料に合わせた作業条件:

- 森・草原を完成基準とし、バトロワ探索検証サイズは96x72 Cell、参加者はPlayer 1 + CPU 5の計6開始地点とする。
- Map配置の正本はUnity Scene、移動Physicsの正本はMapCollision Layer上の不可視`CollisionTilemap`と`CollisionBody`とする。
- 地形、主要ルート、ロケーションを固定し、Enemy / Loot / Merchant / Eventは60 Socketの候補からゲーム側が抽選できる構造にする。
- 中央石畳、南の森壁、西の草むら回廊、東の池、北の木陰、南広場、東水辺広場の役割を維持する。

完成したもの:

- `Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity`。
- 96x72 Ground 6,912 Cell、単一Componentの3 Cell幅Road 1,479 Cell、Blob-47 Water 352 Cell、CollisionTilemap 684 Cell。森・木・岩などはPrefab内CollisionBody。
- 6参加者開始地点、7 Landmark、16 Enemy、22 Loot、3 Merchant、6 Eventの計60 Socket。
- Obstacle Visual 232 Instance、通行可能Decoration 115 Instance、Props 16 Instance。全て既存v4.3 Production Atlas由来のPrefabだけを使用する。
- 全景、中央広場、東水辺のUnity Camera QA画像3点。

確認結果:

- 生成前検査: Map範囲、道路と水の非重複、道路の4近傍連結、SocketのCollision非重複、開始地点間隔18 Cell以上、全開始地点・Landmark・Merchantへの歩行到達性を確認済み。
- `MapAuthoringValidator`: Errorなし。
- `Assembly-CSharp-Editor`: 0 warning / 0 error。
- Unity EditMode Test: **43 passed / 0 failed / 0 skipped**。
- 目視QA: 初版の長い矩形Loopと等間隔の森林壁を廃止し、短い段差を連ねた道路、密度差のある森林島、不規則な東の湖へ修正済み。

次に行う場合:

- 実ゲームの移動速度、Camera、収縮進行を接続し、約20分の試合尺として横断時間・初接敵時間・終盤収束をPlayModeで計測する。
- 基準Mapのプレイ結果が良好なら、固定配置計画から道路Graph、水域Row Span、POI保護領域、森林密度、Socket制約を抽出してSeed生成へ移す。

## 2026-08-03 Map Authoring Kit v4.3

状態: **実装・自動テスト・Unity再構築・撮影・目視QA完了**

今回整理した原因:

- v4.2のDirt / Stone境界Maskは一つの輪郭を反復し、Tile内に焼き込まれた旧GrassがGroundの地面差分を上書きしていたため、道端へ32px単位の格子感が残っていた。
- 接続辺を32px全幅で塗ると直線に周期的な出っ張りが残る一方、中央Socketだけにすると太い道路の内角へGroundの芝が穴として露出する。接続と角埋めを別の条件にする必要があった。
- 標準RuleTileのPerlin差分は局所的に偏り、同じ輪郭が長く連続する可能性を契約上排除できなかった。

今回確定・実装した判断:

- Map配置の正本はUnity Scene、移動Physicsの正本はMapCollision Layer上の不可視`CollisionTilemap`と`CollisionBody`とする。表示と判定の分離、4 Tilemap、自由配置のDecorations / ObstacleVisuals / Propsは継続する。
- Dirt / StoneのRoad Edge Sourceを各4差分作成し、Cardinal-16の全16 Maskを4 Spriteずつへ展開する。
- 非全面Maskの路面外は透明にして`GroundTilemap`を見せる。各接続辺の中央`[6,26)`を20px固定Socketとし、両端6pxはその辺と直交する2方向のbitが両方とも接続する場合に限り埋める。直線Mask `05` / `0a`は20px Socketだけとし、周期的なタブを付けない。
- 太い道路の内角は直交bitのAND条件でのみ埋め、接続部の芝穴と直線辺の格子感を同時に防ぐ。露出辺にだけ1〜3pxの不規則輪郭を残す。
- `RoadConnectionRuleTile`は横4 Cell内で4差分を一度ずつ使い、座標・Rule ID・SeedのHashで24順列から選ぶ。同一輪郭の横連続は区間境界を含め最大2 Cellとする。
- Dirt / Stoneは共通`road`接続Group、道路形状はRadius 1の3 Cell幅Rasterizerを継続し、Water非重複と4近傍単一Componentを一括検証してから配置する。

完成したもの:

- `fantasyroyale.map-authoring-atlas.v4.3` Catalogと205 named Sub-Spriteの単一Production Atlas。
- Grass 8、Dirt 64、Stone 64、Water 50、Detail 4、Props 10、Obstacle Visual 5。
- Road Edge Source 2点、Surface Source 4点、19表示Prefab、10 CollisionBody、4 TilemapのScene Templateと30x20 Sample。
- v4.3 Importer、Builder、Validator、Road Rasterizer、Road Connection RuleTile、Asset Contract Test、Validator Test。

確認結果:

- Unity Rebuild / Sample再構築 / Camera Capture / 目視QA: 成功。
- EditMode Test: **43 passed / 0 failed / 0 skipped**。
- Dirt / Stoneの全16 Maskが各4差分を参照し、負Xを含む各4 Cell区間で4差分を一度ずつ使用、同一差分の横連続最大2 Cellを自動検査済み。
- 道路内部のGrass穴がなく、Dirt / Stone材質境界を含む全道路が単一Componentとして接続することを確認済み。
- Production Atlas SHA-256: `412C63A03CD84472A9928DA5BF687903DB47AD3FF75DAECD246222EDD50D5FF4`。
- Sprite Catalog SHA-256: `EA6A6D111FF41DE5081D7537C5A20D0F74F2854D1C492D747FF94F9070B68600`。
- Production Atlas QA v4.3 SHA-256: `34923AEC3E2721EA1D2491D18A8CE15C6AAF65E5FF2799001367080838B848F1`。
- Unity Sample QA SHA-256: `DD256066AC7DBD5C5A4BC798F750908A8C44D54A8DFE1483A01D9E3DE1E4275E`。

現行設計:

- [[../Architecture/MapAuthoringKit|Map Authoring Kit]]
- [[../Architecture/ForestProductionAtlasContract|Forest Production Atlas Contract]]
- [[../Architecture/Scenes/GbaForestMapAuthoringScene|GBA Forest Map Authoring Scene]]

次に行う場合:

- Scene Templateを複製し、実ゲーム用レイアウトをUnity標準機能で制作する。
- 実制作で繰り返し負荷が確認された操作だけを、小さなEditor補助として検討する。

## 履歴

- v4.2は115 Sprite、GroundVariationTile、全面Maskだけの4差分を導入した。道端の焼き込みGrassと境界Maskの輪郭反復が残ったため、v4.3へ置換済み。
- v4.1は102 Sprite、4 Tilemap、自由配置Decoration / Obstacle Visual、3 Cell幅の道路を導入した。一方でGrass装飾混入、16 Cell完全周期、Dirt / Water全面Maskの単一Sprite反復が残ったため、v4.2へ置換済み。
- v4は5 Tilemap、Detail Tile / Palette、15表示Prefab、手書き1 Cell道路を使っていた。v4.1を経て置換済み。
- v3は191 Sprite、ForestWall / Cliff Blob-47、6 Tilemap、Props Grid、GameObject Brush、Prefab Colliderを使っていた。v4を経て置換済み。
- v2は366 Loose PNGとRandom差分、v1は71 Loose PNGを使用していた。いずれも置換済み。
- 旧独自ツール案と旧Map Data Formatは廃案・保留。旧Biome Atlas、FR_Map、Exploration実装は削除済み。
