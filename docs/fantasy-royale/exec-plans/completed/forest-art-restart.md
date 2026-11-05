# 森林素材の再制作と自動配置検証

## 目的

2026-10-03のユーザー依頼に従い、最新コンセプトv005を基準に森林・道・広場の素材を再制作する。同じ素材を複数Seedから配置し、手修正なしで接続・通行・描画が成立するか検証する。仕切り直しとして旧素材と旧生成物を参照監査後に整理・削除する。

## 背景と根拠

- 基準: `Assets/Art/Concept/style-comparison-2026-10-03/a-classic-adventure-texture-v005.png`。Character頭身はv004、表面質感は初版Aに沿う。
- ユーザーは自動生成を重視し、素材制作・複数パターン検証・既存素材削除を明示的に依頼した。
- 旧v4.3は96x72固定Mapと固定Seedの散布。新しい少量の素材による検証用生成と、正式な試合ごとの地形生成は区別する。

## 対象範囲

- 新しい地表・木・森林塊・岩と下草・表示比較用Character。
- 接続する道路・広場、配置Footprintと接地Collisionを分離したSeed付きEditor生成。
- Unity Scene、複数Seedの画像・検証結果、生成Prompt/Hash記録。
- 旧Art/Map、Generated/MapAuthoring、Forest Tile/Palette/Prefabと不要な旧Sceneの撤去。参照先となるReference Sceneのpath/GUIDとゲーム基盤は保護する。互換Builderと旧実行Toolは保持する。

## 対象外

- 攻撃・装備・CPU・試合進行の新仕様、正式な素材採用、全バイオーム制作。
- PackagesやUnity Versionの変更。ユーザーの既存未Commit変更。

## 実装計画

1. 旧素材・GUID・Scene/Code参照を列挙し、削除一覧と保護範囲を保存する。
2. imagegenでv005に沿った少量の素材を生成し、元画像・Prompt・Hashを保存する。
3. 論理Plan→検査→Unity Sceneの順に生成する。Map配置の正本はUnity Sceneを維持し、別のRuntime Map Serializerは作らない。
4. 旧素材を撤去し、Reference Sceneを新素材で置換。Event/Character基盤の接続を維持する。
5. 複数Seedを同じ手順で生成し、道路連結、Spawn/目的地到達、占有範囲、物理判定、再現性、描画を確認する。
6. EditMode/PlayMode、Knowledge、最終Diffを検証し、KBを更新する。

## 進捗

- 完了: 基準画像・Unity/Package確認、削除台帳と復元ZIPの作成・全Entry Hash照合、残存参照0の監査。
- 完了: 6元画像の制作、透明外周修正、Grass / Dirt再生成、76 Sprite / 3 Tile / 4 Prefab、Editor生成Plan / Builder。
- 完了: 40 Seed論理検査、4 Scene置換・描画、道路角の穴修正、Unity全テストと撤去後の再検証。
- 完了: 旧素材135 File（明示削除131＋Unity削除の付随.meta4）・空19 Directoryの撤去、KB更新、独立Review。互換コードと旧Python Tool / Contractは保管。

## 判断記録

- 削除の再確認は求めない。今回の明示指示が旧素材整理の承認である。対象をForest Artへ限定し、ゲームコード・Event設定・URP/Layersを保護する。
- 正式採用の判断は今回の自動検証と分け、生成物の状態は検証用素材とする。
- 検証用サイズ・個数はPrototype設定であり、固定Map + ランダム配置という本番方針を自動変更しない。
- 自動承認レビューが広い一括削除を拒否したため、参照の切替と検証を先行させ、削除範囲を実行コードを含まない素材Asset 131件へ縮小した。この範囲で承認・削除できた。旧Toolは現行生成から外して履歴用に保管する。
- 初回画像QAで草の細かすぎる粒と道端の矩形感を検出し、地表作画を再生成、露出辺に最大4pxの起伏を追加した。
- PNGの接続角Testで透明穴を検出し、二辺の角判定を修正した。生成前にCompile完了を確認して再生成する必要がある。
- PlayMode Camera追従Testは旧Map原点に依存していたため、新MapのClamp内・衝突のない2点を選ぶ検査へ変更した。実Gameplayの移動・HP・Event Codeは変更していない。
- 最終Diffで当初台帳に付随.meta4件の不足を検出した。Unityが自動削除したこれらを台帳へ追加し、Git HEAD Blobから別ZIPへ保管した。今後の台帳は単独Assetごとにも`.meta`の存在を明示確認する。

## 検証

- `Assets/Art/Map/ForestRestart/QA/generation-validation.json`: 40論理Plan PASS、40 distinct Hash、4実Scene Validator PASS。
- 最終画像QA: 4全景・拡大画像の素材透過、足元、通路の可読性を確認。4全景を等倍で並べた`four-seed-comparison.png`を保存。
- 旧素材撤去後Unity CLI Test Runner: EditMode 98 PASS、PlayMode 4 PASS、Failed / Skipped 0。`Logs/ArtReset/editmode-results.json` / `playmode-results.json`。
- Backup: 135 EntryのByte数 / SHA-256全一致。ZIP SHA-256 `83A677C0AB0A933328F33AB6B3474C031671668AE76FD56FC8047BE0754194BE`。削除前の残存GUID / Path参照0、削除後の台帳対象残存0。
- 付随.meta4件: Git HEAD Blobの別ZIP SHA-256 `61C0AA546181D427D4A56E9C0FC759C84F819F8523437D99202F0187AF3D8372`。これらの撤去後GUID参照も0。台帳は合計139行（削除135、保管Tool2、互換Builder2）。
- Source / Documentationの`git diff --check` PASS。Unityが保存したScene YAMLの空値行には通常の末尾空白があり、生成形式を手編集しない。
- Plan移動後のKnowledge検証は65 Markdown PASS。C# Project Build / Player Buildは今回未実行。

## 完了結果

少量のコンセプト由来素材を共通利用し、手配置修正なしに複数の道路・広場・森林Mapを組めることを確認した。旧素材は新Sceneへの参照切替と回帰確認の後に撤去した。今後の見た目の検討には[森林素材再制作Kit](../../design-docs/map-authoring/forest-restart-kit.md)と比較画像を使う。

残るDecision: 素材の正式採用、水・崖・橋・店などの拡張、直交的な道路骨格と反復の改善、本番Matchでの地形生成採用。今回はこれらを自動確定しない。
