# 2Dスプライトの装備差分の手法

2026-11-07に、一般的な装備差分の手法を調べた結果である。プロジェクトの要求ではなく、設計の参考情報として扱う。

## 手法

### 同じコマ割りの重ね合わせ

基準になる体を決め、体、髪、服、武器の各層を全コマ同じ配置で描いて重ねる。層を重ねるだけで揃うので、実行時の位置合わせが要らない。

- Stardew Valleyの農夫は、体、腕、ズボンを同じ格子で持ち、服や髪などを層として上へ重ねる。
- Liberated Pixel Cupの素材は、共通の格子のシートを層ごとに持ち、層ごとに描く順を決める。
- 代償は絵の量である。服1つにも、体の全コマに対応するコマが要る。体の形が違うと服が合わないので、基準の体を標準化する。

### 付け位置

体の絵に付け位置を持たせ、頭、武器、盾、頭装備をその位置へ付ける。

- Ragnarok OnlineのACT Fileは、ある絵を別の絵に対して相対配置するAnchorを持つ。体のAnchorが頭、武器、盾、頭装備を揃える。
- 付ける側のコマを体の今のコマへ合わせて描くと、表示の乱れを防げる。

### 描く順と隠す処理

部位ごとに描く順を決める。Daggerfallは部品ごとの値で順を決め、マントは内側、体、外側の3段階で重ねる。兜やフードは、下の髪をマスクで隠す。

### 上半身と下半身の分離

上半身と下半身を別の層にして、脚は走りを続けたまま上半身で攻撃する。

- Contraは上下2つの絵に分かれていると言われる。市販の素材にも、部位別の絵で上下を別々に動かす物や、走り攻撃の上半身を走りの脚へ合わせた物がある。
- 主な失敗は、脚と上半身が合わないことである。走りのコマの時間を固定し、上半身をそれに合わせて描く。腰の継ぎ目を確認する。

### 8方向の攻撃の絵

SLYNYRDの解説は、見下ろし型の攻撃を次のように作る。

- 1振りは6コマで、剣は合計400 ms（100、50、50、50、100、50）である。構え、残像、振り切り、戻りの順に進む。
- 武器は8方向すべてで同じ手に持つ。姿勢と振りの経路も向きの間で揃える。
- 残像は動きの速いコマにだけ入れる。構えと戻りには入れない。
- 重い武器は構えと振り切りを長く、短い武器は打撃を速くする。
- 全ての攻撃は、同じ素体と8方向のテンプレートの上で作る。

### 武器の握り

2026-11-08に、手と武器の向きをどう揃えるかを追加で調べた。どの手法も、手の向きと武器の向きを同じ情報から決めている。

- コマごとに武器を描く。MapleStoryは体の絵に手の位置などの付け位置を持たせ、武器も体の動きのコマごとに描いて合わせる。重なりは`weaponOverHand`、`weaponBelowArm`、`handBelowWeapon`のような層の名前で決め、武器を腕と手の間に挟める。Ragnarok Onlineも、体のAnchorへ武器の絵を合わせ、体の今のコマに揃えて描く。描いた通りに握るが、武器ごとに全コマの絵が要る。
- 武器の角度に合わせて腕を回す。Terrariaの合成腕は、前腕の回転と伸びを武器の角度から決め（`SetCompositeArmFront`）、手の位置（`GetFrontHandPosition`）へ武器を置く。例では、振る武器の回転から腕の回転と手の位置を同じ角度で決める。Starboundも、持ち物の腕の角度（`setArmAngle`）と腕の絵（`setFrontArmFrame`）を持ち物側が決め、手の外側に描くかを選べる。腕と武器が同じ角度で動くので、ずれない。
- 手を武器の側に持たせる。銃を回す見下ろし型では、手を武器の絵の上の決まった点に描く例がある。改造用の資料には、武器ごとに両手の座標を持たせる例がある。手は武器と一緒に回る。
- 実践者の議論では、体の動きを共通にして武器を別の層にする方法が多く、ドット絵を計算で回すと形が崩れるので、回した絵を描き直すことも多い。

## このプロジェクトへの示唆

- 全ての絵は1つの基準の体から作り、シートごとに持ち手や盾の側を決め直さない。
- 武器と盾は体の絵に描かず、付け位置へ付ける。
- 盾の手は攻撃中も同じ側に留める。動きすぎたコマは機械的に止める。
- 拳の握る向きと剣の向きは、同じ情報から決める。別々の絵（素手の体の拳と回した剣）を重ねると、向きが合わないコマが出る。
- このプロジェクトでは、剣を握った形の拳を胴の絵に描き（武器種ごとの走りの胴）、剣は握りを捨てて「鍔から先」と「柄頭」に分け、拳の両端へ付けた（2026-11-08）。拳は胴と一緒に動くので揺れず、手袋の色も胴に合う。

## 出典

- [Daggerfallの装備の表示](https://www.dfworkshop.net/?p=2358)
- [Ragnarok OnlineのACT File](https://ragnarokresearchlab.github.io/file-formats/act)
- [RagnarokFileFormats](https://github.com/rdw-archive/RagnarokFileFormats/blob/master/README.MD)
- [Stardew Valleyの農夫の絵](https://stardewvalleywiki.com/Modding:Farmer_sprite)
- [Universal LPC Spritesheet Character Generator](https://github.com/Orval1/Universal-LPC-Spritesheet-Character-Generator)
- [paper dollの議論（gamedev.net）](https://gamedev.net/forums/topic/622607-2d-action-game-sprites-paper-doll-equipment-or-custom-characters-with-unchanging-appearance/)
- [Pixelblog 56: Top Down Character Attack Animation](https://www.slynyrd.com/blog/2025/5/23/pixelblog-56-top-down-character-attack-animation)
- [上下を分けた素材の例（Pixel Prototype Player Sprites）](https://deadrevolver.itch.io/pixel-prototype-player-sprites)
- [走り攻撃の素材の例（Zegley）](https://zegley.itch.io/2d-platformermetroidvania-asset-pack/devlog/656465/coming-soon-air-attacks-and-attack-while-running)
- [歩きながら撃つ絵の議論（Construct）](https://construct.net/en/forum/construct-2/how-do-i-18/shooting-walking-66970)
- [MapleStoryのWZ編集の解説（RageZone）](https://forum.ragezone.com/threads/tutorial-wz-editing-concepts-and-tutorial.838526/)
- [MapleStoryをブラウザで描く例（RageZone）](https://forum.ragezone.com/threads/maplestory-in-a-web-browser.1122866/)
- [tModLoaderのPlayer Class Reference](https://docs.tmodloader.net/docs/stable/class_player.html)
- [tModLoaderの振る武器の例（ExampleCustomSwingProjectile）](https://github.com/tModLoader/tModLoader/blob/1.4.4/ExampleMod/Content/Projectiles/ExampleCustomSwingProjectile.cs)
- [StarboundのactiveItemの資料](https://starbounder.org/Modding:Lua/Tables/Activeitem)
- [武器に手を付ける改造の例（Nuclear Throne Together）](https://splattie.itch.io/hands-mod)
- [装備を付け替えるキャラクターの作り方（GameFromScratch）](https://gamefromscratch.com/creating-dynamically-equipped-characters-in-2d-and-3d-games/)
- [武器と防具を絵に付ける議論（gamedev.net）](https://gamedev.net/forums/topic/642038-graphic-design-for-dynamic-weaponarmor-on-sprite/)
- [装備できる絵の組み方の議論（Unity Discussions）](https://discussions.unity.com/t/how-to-structure-character-sprite-to-allow-for-equipping-weapons-armor/814631)
