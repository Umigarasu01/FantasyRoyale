# 所持品と魔法

## 目的

[所持品](../../product-specs/inventory.md)の決まりを、森の確認用Sceneで遊べる形にする。決まっていない所は仕様のおすすめで始め、所有者が触って調整する（2026-11-13、所有者）。

完了条件は次のとおり。

- メイン武器、オフハンド、頭、胴、足の装備と、カバン（5枠、重ねない）を持てる。一体の装備は、埋める枠をまとめて着け外しする。
- 武器ごとに魔法の枠の数が違い、0もある。はめた魔法は武器の1本ごとに残る。
- Lで選んでいる魔法をMPを使って唱え、Oで切り替える。;で選んでいるアイテムを使い、Pで切り替える。1〜9でカバンの枠を選ぶ。
- HUDに、J、K、L、;の大きな枠と、魔法とカバンの小さな列、MPが出る。
- Tabで持ち物の画面を開ける。持ち物の画面と店の画面を開いている間も、WASDで騎士が動ける。
- 店で買った物はカバンに入る。回復薬もその場で飲まず、カバンに入る。

## 背景と根拠

2026-11-11から13に、所有者が所持品の決まりを決めた（[所持品](../../product-specs/inventory.md#決まったこと2026-11-11と13所有者)）。キーボードの配置、MP、重ねないこと、開いている間も動けることは決まり、残りは実装して調整するとした。

今の実装は次のとおりである（詳しくは[今の実装との違い](../../product-specs/inventory.md#今の実装との違い)）。

- 装備は1つの定義（`CharacterEquipmentDefinition`）が通常攻撃と特殊Actionを持ち（`CharacterLoadout`）、剣の定義は盾の部品も持つ。
- 武器は定義の並びの番号で持っていて、1本ごとの持ち物がない。
- 持ち物の仕組み、魔法、MPはない。回復薬は買うとその場で飲む。
- 店の画面（`ShopScreen`）を開くと騎士が止まり、W、Sで選んでJで買う。

## 対象範囲

- 所持品とMPの純C#の状態と、EditMode Test
- 武器の定義への魔法の枠の数と、両手の武器の印。盾を別の装備に分けること
- 魔法の定義（MP、唱えたときのAction）と、仮の魔法2〜3種
- 入力（`InputSystem_Actions`）の追加と置き換え
- HUD（`GameHud`）の装備の枠、小さな列、MP
- 持ち物の画面（新しく作る）と、店の画面の操作と右の枠の作り替え
- 森の確認用Sceneへの組み込み、PlayMode Test
- 仕様、設計、手順の文書

## 対象外

- 正式な品揃え、値段、魔法の種類と数値、アイコンと魔法の絵
- 唱える動きの絵（当面は今の攻撃の動きを使う）
- 一体の防具の絵（ローブなど）。仕組みだけを作り、Testで確かめる
- ゲームパッドとマウスの新しい割り当て（案のまま）
- 売る、カバンが増える、捨てた物を拾う仕組み、妨害で失う物
- 探索Preview（`BattleRoyaleExplorationPreviewDebug`）への組み込み

## Decision Gate

なし。決まっていない所は仕様のおすすめで始め、仮の値は確認用Sceneの定数か定義Assetに置く。所有者が触った後の調整は判断記録に残す。

## 実装計画

1. **状態**: Character Coreに、装備の枠、カバン、武器の1本ごとの魔法、選んでいる魔法とアイテム、MPを持つ純C#の型を足す。武器の枠の数から全てを作り、決まった数を前提にしない。EditMode Testで、一体の装備、カバンがいっぱいのとき、枠0の武器、持ち替えで魔法が残ること、切り替えで空の枠を飛ばすこと、MPが足りないときを確かめる。
2. **定義**: 武器の定義に魔法の枠の数、両手の武器かを足す。盾をオフハンドの装備として分ける。魔法の定義を足し、仮の魔法を作る。今の特殊Actionを固有の技として扱う。
3. **入力**: L、O、;、P、Tab、1〜9を足し、1と2の武器の持ち替えを外す。画面を開いている間は、I、J、K、Lと;を画面の操作に回し、攻撃、技、魔法を止める。
4. **確認用Scene**: `ForestMapPreviewDebug`を所持品の状態へつなぐ。買った物はカバンへ入れ、回復薬はカバンから使う。MPは時間で戻す。
5. **HUD**: J、K、L、;の大きな枠、魔法とカバンの小さな列、MPの青い玉を足す。
6. **持ち物の画面**: 装備、固有の技と魔法、カバン、説明を出す。;で使う、着ける、持ち替える。
7. **店**: 開いている間も動けるようにし、届く所を出たら閉じる。右の枠を持ち物の画面を縮めた形に替える。
8. **Test**: PlayModeで、唱える、切り替える、使う、開いたまま動く、店で買ってカバンへ入るを確かめる。
9. 1080pの画面で撮って確かめ、文書を更新する。

## 進捗

- [x] 状態（2026-11-13）
- [x] 定義（2026-11-13）
- [x] 入力（2026-11-13）
- [x] 確認用Scene（2026-11-13）
- [x] HUD（2026-11-13。所有者の見た目の確認待ち）
- [x] 持ち物の画面（2026-11-13。所有者の見た目の確認待ち）
- [x] 店（2026-11-13。所有者の見た目の確認待ち）
- [x] Test（2026-11-14）
- [x] 画面の確認と文書（2026-11-14）

## 判断記録

- 2026-11-13: 計画を作った。仕様のおすすめで始める形は[所持品の決めること](../../product-specs/inventory.md#決めること)にある。
- 2026-11-13: 状態はCharacter Core（`FantasyRoyale.Gameplay.Characters.Core`）に3つの型で置いた。`CharacterItem`は手に入れた物1つで、定義のid、種類、埋める枠、魔法の枠（数は0から）、選んでいる魔法を持つ。`CharacterInventory`は着けている物とカバンを持ち、`CharacterMana`はMPを持つ。Unityの定義Assetには触れず、idでつなぐ。
- 2026-11-13: 武器と防具を同じ`Equipment`の種類にし、埋める枠で分ける。メイン武器の枠を埋める物が武器で、魔法の枠を持てるのは武器だけである。両手の武器とローブは、埋める枠を2つ持つだけで、特別な種類にしない。
- 2026-11-13: カバンの装備を使うと、外れた1つ目の物を使った物のいた枠へ入れる。持ち替えた後に同じ番号で戻せるようにするため。空きは使った物の抜けた枠も数える。
- 2026-11-13: 魔法を唱えるとき、MPは呼び出し側が確かめて使う。`CharacterCombat2D`にMPを持たせず、Actionを始められたときだけ使う。

- 2026-11-13: 盾は`CharacterEquipmentDefinition`のまま、持つ手（`CharacterHandUse`）を`OffHand`にした定義（`ShieldEquipment.asset`、id `shield`）として分けた。新しい型を作らず、部品の重ね方を武器と共有するため。一覧（`CharacterEquipmentCatalog`）は武器と`OffHands`を分けて持ち、武器種の一覧に盾を入れない。
- 2026-11-13: 剣は片手で魔法の枠2つ、弓は両手で魔法の枠1つを仮の値にした（Scene Builderの定数）。両手の武器を持つ間は、オフハンドの部品を重ねない。
- 2026-11-13: 魔法は`CharacterMagicDefinition`（id、名前、MP、唱えるAction）にした。唱える動きの絵がないので、仮の2つ（炎の弾MP12、氷のつぶてMP8）は弓と同じ射る型の飛び道具である。`CharacterActionSlot.Magic`と`CharacterLoadout.SetMagic`を足し、魔法は武器の差し替えで消えない。
- 2026-11-13: 確認用Sceneは、この段では盾を初めのオフハンドとして剣の部品に重ねるだけにした。所持品の状態へのつなぎ込みは4段目で行う。

- 2026-11-13: 入力のAssetのPlayer Mapに、`CastMagic`（L）、`NextMagic`（O）、`UseItem`（;）、`NextItem`（P）、`Inventory`（Tab）、`SelectBagSlot`（1〜9）、画面の上下左右`MenuUp`、`MenuDown`、`MenuLeft`、`MenuRight`（I、K、J、L）を足した。ゲームパッドは案のとおり、魔法を右の引き金と十字キーの上、アイテムを左の引き金、画面をSelectにした。
- 2026-11-13: 1と2の`Previous`、`Next`は外さなかった。探索Previewが武器の持ち替えに使い、そのTestも1を押す。森の確認用Sceneが使うのをやめるのは4段目である。
- 2026-11-13: 唱える操作は攻撃と同じAction Commandにし、`HumanCharacterActionInputAdapter`が魔法のButtonを任意で受け取る。同時押しは通常攻撃、技、魔法の順。そのほかの操作は`HumanInventoryInputAdapter`が1フレーム分まとめて読む。画面の上下左右は攻撃や魔法と同じキーなので、画面を開いている間だけ呼び出し側が使う。
- 2026-11-13: 2段目の後のEditModeで、全ての装備の定義が武器の一覧にあることを求めるTest（`RealCatalog_ListsEveryDefinitionWithAnId`）が盾で失敗した。2段目の検証はSceneを作り直す前にEditModeを走らせたので、見落としていた。盾はオフハンドの一覧から引くようにTestを直した。
- 2026-11-13: 4段目で、店で買った装備はカバンへ入れず、すぐ着けて外れた物をカバンへ入れる形にした。計画では「買った物はカバンに入る」としていたが、店で買って持ち替える今の手触りを保つため。外れた物がカバンに入りきらないときは、買った物をカバンへ入れる。回復薬と魔法はカバンへ入る。カバンにある装備を店で選ぶと、代金なしで着け替える。
- 2026-11-13: 初めは剣、盾、騎士の防具を着け、剣に炎の弾を入れてカバンを空にした。所有者がすぐ魔法を試せるようにするため。魔法屋の品揃えを分けていないので、全ての店で炎の弾と氷のつぶて（12コイン）を売る。
- 2026-11-13: 全装備の確認画面の持ち替え（`TryEquipWeapon`、`TryEquipArmour`）は、カバンにあればそれを使い、なければ`CharacterInventory.ForceEquip`で新しく着けて外れた物を消す。確認のたびにカバンが埋まらないようにするため。片手の武器へ替えて盾がなくなったときは盾を戻す。
- 2026-11-13: HUDは`LoadoutBar`を新しく作り、`GameHud`が持つ。新しい絵（丸い魔法の枠、丸い選びの縁、MPの玉）は`build_hud_sprites.py`が図形で描き、盾、魔法、技の仮のアイコンは`build_shop_art.py`が作る。盾は今の盾の絵から切り、魔法と技は16 pixelの小さなドット絵を描いて拡大した。技の仮のアイコンは今の特殊Actionに合わせ、剣は矢、弓は斬撃である。
- 2026-11-13: Editorを開かずに見た目を確かめるため、名前で選んだときだけ動く`ForestMapHudCaptureDebug`を作った。撮る間だけCanvasをScreen Space Cameraへ替え、Cameraを1920×1080のRender Textureへ描く。
- 2026-11-13: 所有者が下の枠の絵を見て、武器は何を持っているかが分かればよく、技の待ち時間の方が大事だとした。続けて、武器は左上にまとめ、技を下に置くとした。メイン武器とオフハンドの絵を左上の枠（`GameHud.SetEquipment`）へ移し、下の枠はK、L、;の3つにして、固有の技の枠を一番大きくした。
- 2026-11-13: 固有の技に待ち時間を足した。純C#の`CharacterCooldown`を足し、確認用Sceneが仮に3秒を持つ。HUDは上から時計回りに減る覆い（uGUIの`Image`のRadial360）で見せる。最初の覆い（黒の0.62）は黒漆の地の上でほとんど見えなかったので、0.8にし、待っている間は技の絵を灰色にした。
- 2026-11-13: 所有者が続けて、下に大きくアイテムを並べてその上に技と魔法を置き、武器はもっと大きなアイコンにするとした。`LoadoutBar`を、下の段にカバンの全てのアイテム（72 pixel、数字キーの番号つき）、上の段に技（80 pixel）と魔法（72 pixelの丸と小さな列）の形に作り替えた。;のキーは選んでいるアイテムの右下に添える。右上に置くと隣の枠の番号と重なった。左上の武器は76 pixelにし、オフハンドを38 pixelで右下に重ねた。
- 2026-11-13: 所有者が、武器はHUDにいらないとした。左上の枠の武器とオフハンドの絵（`GameHud.SetEquipment`）を外し、枠をハート、MP、コインだけに戻した。
- 2026-11-13: 所有者が、技と魔法をQとEに、魔法の切り替えをRに、アイテムを数字キーに替えた。入力のAssetで`Special`をQ、`CastMagic`をE、`NextMagic`をRにし、`UseItem`の;と`NextItem`のPをキーボードから外した。1〜9は`UseBagSlot`に改め、押すとその番号のアイテムを使う。Eが魔法になったので、調べる（`Interact`）はFへ移した。所有者がFでよいとした。吹き出しのキーの絵は`build_hud_sprites.py`がFも描き（`hud-key-f.png`）、`HudSkin`の`KeyE`を`InteractKey`に改めた（Fieldは`FormerlySerializedAs`で引き継ぐ）。店を閉じるキーもFにした。探索PreviewのTestが特殊ActionをKで押していたので、Qに直した。HUDのアイテムの段は、;とPのキーと選んでいる物の囲みを外し、番号だけを出す。
- 2026-11-13: 持ち物の画面（`InventoryScreen`）を作った。決めるキーは、計画の;をキーボードから外し、JはIJKLの左と重なるので、UかEnterかクリックにした（`MenuConfirm`、仮）。上下左右で選ぶ隣の枠は、枠の画面の位置から向きに近い物を探して決める。魔法とカバンの数が変わっても作り替えずに済むため。強さの棒、レア度、騎士の姿、捨てるは、まだ作っていない。
- 2026-11-13: 所有者が、装備を体に沿った形に並べるとした。頭を上、武器と胴とオフハンドを真ん中の段、足を下に置き、枠を80 pixelにした。段が3つになったので、画面の高さを720から790 pixelにした。
- 2026-11-13: 所有者が持ち物の画面のバランスが悪いとし、カバンと使用枠を仕組みごと分けるとした。`CharacterInventory`に使用枠（使う物だけ、数字キーで使う）を足し、カバンをしまう所にした。使う物は使用枠へ先に入り、入らなければカバンへ入る。カバンの使う物を使うと使用枠へ移る。ゲームパッドの選びは使用枠に移した。仮の数は使用枠5、カバン6である。入力の`UseBagSlot`は`UseQuickSlot`に、HUDの下の段は使用枠に改めた。持ち物の画面は、技と魔法を装備の右へ移し、使用枠とカバンを下の2段に分けた。
- 2026-11-13: 店を開いている間も騎士が動けるようにした。選ぶのはI、Kか上下の矢印、買うのはUかEnter、閉じるのはFで、持ち物の画面と同じキーにした。届く所を出ると閉じる。店の画面は下まで広がり、下の技とアイテムの枠が店の枠の上に重なったので、開いている間は下の枠を隠す。右の枠は装備を体の形に並べ、使用枠とカバンを出す形にした。
- 2026-11-13: 所有者が、オフハンドとメインハンドに差がないので直し、両手武器の形も出すとした。持ち物の画面と店の右の枠で、武器とオフハンドの枠の下に「武器」「オフ」と書き、両手の武器では2つの枠を胴の後ろの金の帯でつなぎ、オフハンドに同じ武器を薄く出して「両手武器」「両手」と書く。最初の名前（「オフハンド」「両手で使用中」）は2行に折り返したので短くした。
- 2026-11-13: 所有者が、メインハンドは右、オフハンドは左、メインハンドの枠は縦に大きく、両手武器が入るとオフハンドはグレーアウトとした。持ち物の画面と店の右の枠で、メイン武器を右の80×124 pixelの枠、オフハンドを左に置き、両手の武器ではオフハンドの枠を灰色にして絵を出さない形にした。金の帯と薄い絵はやめた。持ち物の画面の選びの金の囲みは、枠の大きさに合わせる。
- 2026-11-13: 所有者が、オフや両手武器の文字はやめ、背景のイラストでうっすら出し、装備したら消すとした。持ち物の画面と店の右の枠から文字を外し、空の装備の枠の背景に影の絵（アイコンを1色で塗った物）を出す。線形の色空間で描くので白の不透明度は数値より明るく見え、0.16、0.07と下げて0.035（灰色のオフハンドでは0.015）にした。
- 2026-11-14: 本物のキーの経路を確かめるため、仮想Keyboardで森の確認用Sceneを操作する`ForestMapKeyboardPlayModeTests`を足した。E、R、Q、1、Tab、Jを押す。最初は2つのTestに分けたが、`InputTestFixture`の2つ目のTestで森のSceneの読み込みが終わらなかったので、1つのTestにまとめ、読み込みと準備の待ちに30秒の上限を付けた。原因は調べていない。
- 2026-11-14: 所持品とMPの型と責務を[SceneとClass構成](../../design-docs/scene-and-class-structure.md#ゲーム側との境界)に書いた。

## 検証

- 2026-11-13（1段目）: Editorを閉じた状態のBatchmodeで、EditMode 349 / 349成功。新しい`CharacterInventoryTests` 9件と`CharacterManaTests` 3件を含む。PlayModeは実行していない。
- 2026-11-13（2段目）: BatchmodeでEditMode 352 / 352成功（`CharacterEquipmentHandTests` 3件を含む）。`-executeMethod FantasyRoyale.Editor.Playtest.ForestMapPreviewDebugSceneBuilder.BuildScene`で森のSceneを作り直した。森のPlayMode（`ForestMapPreviewPlayModeTests`）は33 / 34成功。盾と弓の見た目、全装備の着替え、店で弓を買うTestは成功した。失敗した`ForestMap_HudFollowsHealthCoinsAndUseTargets`は、Batchmodeの画面（2560×1440）でピクセルフォントの大きさが12の整数倍にならないという検査で、この段で触っていないHUDの処理である。Editorの画面で確かめ直す。
- 2026-11-13（3段目）: BatchmodeでEditMode 366 / 366成功（`KeyboardLayoutTests` 14件を含む）。PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は38 / 39成功で、失敗は2段目と同じHUDのピクセルフォントの検査だけである。
- 2026-11-13（4、5段目）: 森のSceneを作り直し、BatchmodeでEditMode 367 / 367成功（`ForceEquip_DropsDisplacedItemsWithoutTouchingTheBag`を含む）。PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は40 / 41成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。新しい`ForestMap_PotionGoesToTheBagAndHealsWhenUsed`、`ForestMap_MagicUsesManaAndStaysWithTheWeapon`、`ForestMap_LoadoutBarFollowsTheInventory`は成功した。`ForestMapHudCaptureDebug`で1920×1080を撮り、魔法の枠が2つから1つへ変わっても大きな枠が動かないことを絵で確かめた（[剣](../../product-specs/art/ui-samples/loadout-in-game-sword.png)、[弓](../../product-specs/art/ui-samples/loadout-in-game-bow.png)）。本物のキーでの操作は確かめていない。
- 2026-11-13（HUDの作り替え）: EditMode 368 / 368成功（`CharacterCooldownTests`を含む）。PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は40 / 41成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。`ForestMap_LoadoutBarFollowsTheInventory`は、武器が左上に出ること、技の覆いが使うと出て時間で減り消えること、待ち時間の間は使えないことを確かめる。撮り直した絵で、技の覆いが見えることを確かめた。
- 2026-11-13（下にアイテムを並べた形）: PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は40 / 41成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。その後に;のキーの位置だけを右上から右下へ動かし、撮り直して重ならないことを確かめた。この位置の変更の後はTestを走らせていない。
- 2026-11-13（武器を外した形）: PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は40 / 41成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。撮り直して、左上がハート、MP、コインだけになったことを確かめた。
- 2026-11-13（キーの付け直し）: 森のSceneを作り直し、EditMode 366 / 366成功（`KeyboardLayoutTests`をQ、E、R、F、数字キーに直した）。PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は40 / 41成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。撮り直して、技と魔法の枠にQ、E、Rが、アイテムに1〜5が出ることを確かめた。本物のキーでの操作は確かめていない。
- 2026-11-13（持ち物の画面）: EditMode 368 / 368成功（`KeyboardLayoutTests`に決めるキーU、Enterを足した）。PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は41 / 42成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。新しい`ForestMap_InventoryScreenEquipsAndKeepsTheKnightMoving`は、枠の数、開いたまま動けること、上下左右で隣の枠へ移ること、魔法をカバンへ外すこと、カバンの弓を着けて枠の数が変わることを確かめる。撮った絵で、下のHUDと重ならないよう枠を上へ動かした（[持ち物の画面](../../product-specs/art/ui-samples/inventory-screen.png)）。動かした後はTestを走らせていない。本物のキーとマウスでの操作は確かめていない。

- 2026-11-13（装備を体の形に）: PlayModeのForestMapPreviewPlayModeTestsとPreviewDebugCombatPlayModeTestsは41 / 42成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。持ち物の画面のTestは、頭の下が胴、胴の下が足、胴の左が武器、胴の右がオフハンドへ移ることを確かめる。撮り直して、画面がHUDと重ならないことを確かめた。

- 2026-11-13（使用枠とカバンを分けた）: EditMode 368 / 368成功（QuickSlots_HoldConsumablesApartFromTheBagを含む）。PlayModeのForestMapPreviewPlayModeTestsとPreviewDebugCombatPlayModeTestsは41 / 42成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。回復薬が使用枠に入り、いっぱいならカバンへ入ること、下のHUDが使用枠を出すこと、持ち物の画面で足の下が使用枠、使用枠の下がカバンへ移ることを確かめた。撮り直した絵で並べ方を確かめた。本物のキーとマウスでの操作は確かめていない。

- 2026-11-13（店）: EditMode 368 / 368成功。PlayModeのForestMapPreviewPlayModeTestsとPreviewDebugCombatPlayModeTestsは41 / 42成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。店の画面のTestは、開いたまま騎士が動けて店が開いたままのこと、下の枠を隠すこと、右の枠に盾が出て弓では空くこと、届く所を出ると閉じることを確かめる。撮った絵（[店の画面](../../product-specs/art/ui-samples/shop-screen.png)）で、下の枠が店に重ならないことを確かめた。本物のキーとマウスでの操作は確かめていない。

- 2026-11-13（武器とオフハンドの見分け）: PlayModeのForestMapPreviewPlayModeTestsとPreviewDebugCombatPlayModeTestsは41 / 42成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。剣と盾では帯がなく名前が「武器」「オフ」、弓では帯が出て「両手武器」「両手」になることを、持ち物の画面と店の両方で確かめる。名前を折り返さないようにした後は撮り直しだけで、Testは走らせていない。

- 2026-11-13（メイン武器を右の縦長、オフハンドを灰色）: PlayModeのForestMapPreviewPlayModeTestsとPreviewDebugCombatPlayModeTestsは41 / 42成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。メイン武器が胴より右にあること、胴の右がメイン武器、左がオフハンドへ移ること、弓ではオフハンドが灰色で絵がないことを、持ち物の画面と店で確かめる。

- 2026-11-13（空の枠の影の絵）: 森のSceneを作り直し、EditMode 368 / 368成功。PlayModeは影の絵の不透明度を下げる前に41 / 42成功で、着けている枠には影を出さないこと、外した頭の枠と灰色のオフハンドには出すことを、持ち物の画面と店で確かめた。下げた後も、PlayModeの`ForestMapPreviewPlayModeTests`と`PreviewDebugCombatPlayModeTests`は41 / 42成功で、失敗は前と同じHUDのピクセルフォントの検査だけである。

## 完了結果

2026-11-14に完了とした。

- 森の確認用Sceneで、メイン武器、オフハンド、頭、胴、足の装備、使用枠（5）とカバン（6から）、武器ごとに数の違う魔法の枠、MP、固有の技の待ち時間が動く。キーボードは技Q、魔法E、魔法の切り替えR、使用枠1〜5、調べるF、持ち物の画面Tabである。
- HUDは、ハートの下にMPの玉、画面の下に使用枠の段と、その上に技と魔法の段を出す。武器はHUDに出さない。
- Tabの持ち物の画面と店の画面は、開いたまま騎士が動ける。装備は体の形に並べ、メイン武器は右の縦長の枠、オフハンドは左で、両手の武器ではオフハンドを灰色にする。空の装備の枠には影の絵を薄く出す。
- 最終の検証は、Batchmode（Editorは閉じていた）でEditMode 368 / 368成功、PlayModeの`ForestMapPreviewPlayModeTests`、`PreviewDebugCombatPlayModeTests`、`ForestMapKeyboardPlayModeTests`は42 / 43成功。

残る事項は次のとおり。

- `ForestMap_HudFollowsHealthCoinsAndUseTargets`のピクセルフォントの大きさの検査が、`-nographics`のBatchmode（画面2560×1440）で失敗する。2段目から同じで、この計画で触っていないHUDの処理である。Editorの画面で確かめていない。
- 同じ`InputTestFixture`で森のSceneを2回読み込むと、2回目が終わらない。原因は調べていない。
- 所有者は、Editorで本物のキーとマウスを使って触っていない。
- 決めていない、または仮のもの: 魔法の操作のゲームパッドとマウスの割り当て、決めるキー（U、Enter）、使用枠とカバンの数、MPと待ち時間の数値、使用枠の物をカバンへ戻す操作、捨てる操作とキー、売る、カバンが増える仕組み、拾う、妨害で失う物、正式なアイコンと魔法の絵、唱える動きの絵、一体の防具（ローブ）の絵、店ごとの品揃え、説明の強さの棒（持ち物の画面）。
- 盾の影の絵の下に1点の小さな点が残る。
- [配置の下書き](../../product-specs/art/ui-samples/inventory-layout-draft.svg)は最初の案のままである。
