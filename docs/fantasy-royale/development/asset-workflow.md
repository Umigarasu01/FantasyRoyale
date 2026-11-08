# Asset導入・生成Workflow

## 外部Asset

- Unity Asset Storeなどの外部AssetをProjectへ取り込む前に、Asset名、用途、提供元、License上の注意点をユーザーへ共有して確認を取る。
- 採用後は用途に応じて`Assets/Art/`、`Assets/Audio/`、`Assets/Prefabs/`などへ整理する。
- Licenseと提供元は[外部Asset候補・導入記録](assets/external-assets.md)へ残す。

## AI生成Asset

- 原則として`Assets/Art/Generated/`または用途別の`Assets/Art/`配下へ保存する。
- 初期導入ではPlaceholderとして扱い、正式素材へ差し替え可能な参照構造を保つ。
- 採用済みAssetを保護し、候補生成とQAは別Fileで行う。採用判断前に既存AssetやScriptableObject参照を置き換えない。
- Unityから参照する生成AssetはGit管理対象に含める。
- 用途、保存先、生成日、生成方法、Prompt概要、Hashは[生成Asset台帳](assets/generated-assets.md)へ記録する。

## 森マップの生成手順

`Tools/MapAuthoring/`の3つのScriptを順に使用する。Python 3.10とPillowを使い、追加Packageは不要。

1. 画像生成: `python generate_forest_asset.py <asset-name> Assets/Art/Generated/ForestLook`。実行のたびに課金され、同じ画素は再生成できない。既存Fileへの上書きは拒否する。APIキーはユーザー環境変数`OPENAI_IMAGE_API_KEY`から読み、表示や保存はしない。
2. 部品化: `python build_forest_parts.py`。生成シートを切り出し、`Parts/`へ部品と`parts.json`を書き出す。
3. 地図生成: `Tools/MapAuthoring/`で`python build_forest_map.py <出力先> [--area x0 x1 y0 y1]`を実行する。省略時は全域、`--area`で一部だけを生成する。全域は2〜4分かかる。道、川、施設の位置は`forest_layout.py`、デザイン画の画素から地面を合成する処理は`forest_texture.py`にある。

デザイン案は`Tools/MapAuthoring/Inputs/`にある。同じ場所の施設の原画は旧素材で、地図生成では使わない。生成した地図の出力は再生成できるのでGitへ入れない。

次はディスクにだけ残し、Gitへ入れない: `Assets/NemoBlocks/Scenes/`、`Assets/NemoBlocks/Generated/`、OverheadStudyAの素材とScene、`NemoValidation*/`、`Assets/_Recovery/`。新しいCloneにはないため、OverheadStudyAの素材を読むTestは失敗する。

Unity確認は、Repositoryではなく検証用Project`C:/Users/umiga/Documents/Codex/2026-10-12/task/nemo-visual-implementation/UnityVisualValidation/`で行う。出力を同Projectの`Assets/ForestLookSlice/`へ置き、`Tools/MapAuthoring/Unity/`の`.cs.txt`を拡張子`.cs`で配置する。Runnerは`Editor/`、`ForestLookWalker`は同Directoryの直下に置く。環境変数`FOREST_LOOK_OUT`へ結果の出力先を指定し、`-batchmode -projectPath <検証用Project> -executeMethod ForestLookSliceRunner.Run`で実行する。結果は`report.json`と撮影画像である。Sceneは`Assets/ForestLookSlice/ForestLookMap.unity`に保存され、Playすると矢印キーまたはWASDで移動できる。

## 森マップを走るSceneの作り方

1. 騎士のコマ: `Tools/MapAuthoring/`で`python build_hero_frames.py [--preview <gif>]`を実行する。生成シートから走り30コマと立ち姿5枚を書き出す。走りのシートは1周6コマで、両足が浮くコマはツールが決まった高さだけ持ち上げる。出力は決まった結果になる。
2. 地図の配置: 地図出力の地面画像、`objects.json`、`collision.json`、`Parts/`を`Assets/ForestMapPreview/`へ置く。
3. Scene生成: Unityで`FantasyRoyale/Playtest/Build Forest Map Preview Debug Scene`を実行する。

`Assets/ForestMapPreview/`はSceneを含めてGitへ入れない。新しいCloneでは上の手順で作り直す。

## 森マップの設置物を足す

宝箱と敵はUnityが置く絵なので、`Tools/MapAuthoring/`で`python forest_objects.py`を実行して書き出す。

設置物の絵は、草地の切り抜きと既存のシートを参照にして、3行3列のシートで生成する。`Tools/MapAuthoring/forest_objects.py`の一覧へ、シートの位置、地図での幅、足元、判定を足す。大きさは高さ72pxの騎士を基準にする。配置は`forest_layout.py`の`FACILITIES`で持つ。

## 装備の見た目を足す

体のアニメーションは防具の部位ごとに素手の1組だけで、武器は部品として手と背中へ重ねる。体は`Hero/anchors.json`にコマごとの手と背中の位置、武器の角度を持ち、部品はその位置へ毎Frame付いて動く。新しい装備に走りや攻撃のコマは要らない。

部品は形で2種類に分ける。

- どの向きからも形が同じ物（剣など）: 絵を1枚描く。ツールが24方向へ回転させ、全ての姿勢で使える。
- 向きで見える面が変わる物（盾、矢筒など）: 5方向の絵を描く。

1. `hero-gear-01.png`を参照にして、部品シートへ行を足すか、同じ形のシートを生成する。列は正面、斜め前、横、斜め後ろ、背面の順である。
2. `Tools/MapAuthoring/build_hero_frames.py`の`ROTATED_ITEMS`か`GEAR_ITEMS`へ足して実行する。`ROTATED_ITEMS`には付ける位置、行と列、高さを書く。絵は切っ先を下に描き、武器の絵に手は描かない。剣の絵はツールが柄頭（`SWORD_POMMEL_ROWS`）と鍔から先（`SWORD_GUARD_ROW`から下）に分け、間の握りは拳に隠れるので捨てる。2つは拳に接する点（柄頭の下端の中央、鍔の上端の中央）を中心に回し、`sword-pommel-rot-<index>`と`sword-rot-<index>`として書き出す。`GEAR_ITEMS`には付ける位置、行、高さ、向きごとの持つ点を書く。回転は大きな絵のまま行ってから縮小するので、線が荒れにくい。
3. Scene Builderを実行すると部品の定義Assetができる。`CharacterEquipmentDefinition`の`Gear`へ設定する。
4. `CharacterEquipmentDefinition`の`Id`へ、他と重ならない名前を入れる。全装備の一覧へ自動で載るので、一覧や画面へ足す作業はない。`Id`が空の武器は載らず、Consoleへ警告が出る。

利き手の角度は、ツールが剣入りの絵と素手の絵の差から測る。剣は胴の絵が描く拳の両端へ付く（`write_grip_ends`）。利き手の位置（`mainHand`）は、剣入りの絵の握りの点の近くにある、騎士の胴の部位の手袋の中心である。横の走りと立ち姿は手袋が袖へ続くので、見つけた中心から下の手袋だけで拳の中心を測る（`FIST_ONLY_VIEWS`）。ツールはその中心から、剣の向き（15度刻み）に沿って拳の手袋と暗い輪郭がある所の両端を測り、刃の側を`mainHandFront`、柄頭の側を`mainHandBack`として`anchors.json`へ書く。どちらも1 pixelだけ拳へ食い込ませ、隙間が出ないようにする（`FIST_OVERLAP`）。背面の走りと立ち姿は鍔から先が胴の下に回るので、両端を刃の向きへ3.5 pixel出して鍔を見せる（`SWORD_PUSH`）。拳が体の陰で見つからないコマ（背面の攻撃の2コマ目）は、剣入りの絵の握りの点に両端を置く。拳は胴の部位の絵そのものなので、手袋の色は胴に合い、拳は腕と一緒に動く。緑の拳を剣と同じ角度に回して重ねる方法（`hero-fist-01.png`）は、握る向きが合わず、剣と拳を一体で描いた部品は胴の拳とコマごとにずれて揺れたので、使わない。走りの角度は向きごとに1つへ固定し、武器は手の動きだけについていく。

武器を持つ腕の形は武器種で違うので、走りの胴は武器種ごとに持てる。片手剣の型は`hero-body-run-sword-*`（剣と盾を除いた体）、`hero-run-sword-*`（剣入りの測定用）、`hero-torso-run-sword-*`と`hero-set-<id>-torso-run-sword-*`（胴）から作る。体のシートは共通の走りを描き直した物なので、ツールは共通の走りのコマの置き方をそのまま使い、共通の脚と頭がそのまま合う。胴は`hero-torso-sword-<view>-<index>.png`、手の位置は`hero-sword-<view>-<index>`として書き出す。頭の位置は共通の走りの値を使う。片手剣の型のシートを持たない胴の部位は、共通の走りで剣を持つ。斜め後ろの走りはGIFで剣を立てて描かれたが、他の走りと斜め後ろの立ち姿は剣を下へ向けている。ツールは斜め後ろの走りに、斜め後ろの立ち姿の角度を使う（`RUNS_HELD_AS_IDLE`）。攻撃はコマごとの角度を使う。反対の手は、走りと立ち姿では素手の絵から測り、攻撃では盾の位置の平均で向きごとに1つへ固定する。

拳で握る部品は、拳の絵を上へ重ねる。柄が拳の中を通って見え、持っているように見える。利き手の拳は剣入りの絵の握った手から、反対の手の拳は体の絵から切り出す。拳の絵はツールが`Hand/`へ全コマ分を書き出す。盾は拳を隠すので重ねない。

動きの絵は武器ごとに作らず、`CharacterActionDefinition`の`Motion`で型を選ぶ。今の型は`Swing`（向いた方向へ半円に横へ振る）と`Shoot`（弓で射る）である。武器の差は範囲や速さの数値で出す。`Shoot`の動きの絵はまだないので、弓は走りと立ち姿のまま撃つ。

騎士は足、胴、頭の3層で表示する。手法の調査は[2Dスプライトの装備差分の手法](../references/sprite-equipment-layering.md)にある。

次のシートは表示しない測定用で、手の位置、兜の位置、剣の角度を読むために残す。

- `hero-body-run-*`、`hero-body-idle-*`、`hero-body-upper-swing-*`: 素手の体全体の絵
- `hero-run-*`、`hero-idle-*`、`hero-upper-swing-*`: 同じ姿勢で剣と盾を持った絵。振りは1振り4コマで、シートは走りと同じ行の並びとする

表示する絵は次の部位のシートから作る。

1. `hero-head-01.png`: 頭。上の行に正面、横、背面、下の行に斜め前、斜め後ろを並べた1枚である。
2. `hero-torso-run-*`、`hero-torso-idle-*`、`hero-torso-swing-*`: 胴。頭と脚を消し、腰当ての裾までを残した絵である。
3. `hero-legs-run-*`、`hero-legs-idle-*`: 足。腰から腿の上部は暗いズボンで、膝と靴を残した絵である。
4. `hero-legs-lunge-*`: 止まって振るときの足。列は振りの4コマに対応し、構え、踏み出し、最も深い踏み込み、戻りの順である。

振りの絵を描くときは次を守る。

- 行の中で動くのは利き腕と武器だけである。頭、胴、盾の腕は全コマ同じにする。
- 盾を別の側へ描かない。
- 剣は体の右から前を通って左へ、攻撃方向を中心に半円を描く。ツールの`SWING_ANGLES`がコマごとの角度である。

生成した部位のシートは、体全体のシートと位置が少しずれることがある。ツールは胴と足のコマを、体全体のコマに一番合う位置へ動かす。足はさらに足元を体全体のコマへ揃える。頭は全ての部位で、正面のあごの高さを騎士へ揃える。斜め後ろの走りはGIFの頭が横を向いていて、描かれた兜が首の上にない。ツールは斜め後ろの走りだけ、頭を描かれた兜でなく首の付け根に付ける（`NECK_SEATED_RUNS`）。首の付け根は騎士の胴のコマの襟の穴（上部の暗い部分）で、頭の首（頭の絵のいちばん下の3行の中央）が、背面の走りで襟の穴に対してある位置と同じ所へ来るよう置く。胴と足の重なりが足りなければ止まる。

踏み込みの足は、腰を`LUNGE_REACH`の分だけ向いた方向へ動かして置く。胴の付く位置は`LUNGE_LEAN`と`LUNGE_RISE`から決め、`anchors.json`の`stances`へ書き出す。踏み込みの大きさはこの3つの値で調整する。

ツールは攻撃の胴を兜の位置で揃えて書き出し、残像を`Smear/`へ書き出す。刃が半円から30度を超えて外れるか、盾が8 pixelを超えて動くと止まる。刃は隣のマスへはみ出すので、自分のマスより広く探し、細長い形だけを刃と見なす。盾の十字を拾わないためである。

部品が体へ重ねられることは機械的に確認する。全ての絵、体と同じPPU、全コマの手の位置が揃っていなければScene Builderが止まり、EditMode Testの`CharacterSpriteContractTests`が失敗する。`FindEquipmentProblems`は胴と装備の組み合わせを調べ、振る動きのコマがない胴と、型に合わない部品も止める。

## 防具の部位を足す

防具は足、胴、頭の部位で増やす。守る決まりは[防具の部位デザイン規約](../product-specs/art/armor-part-design.md)にある。手の位置、武器の角度、拳は騎士の値を共有するので、どの部位を組み合わせてもどの武器もそのまま持てる。

枚数は頭1枚、胴は4枚と武器種ごとに4枚（攻撃の上半身2枚と、武器種の走りを持つなら走りの胴2枚）、足は4枚と動きの型ごとに2枚である。片手剣の型の走りの胴は、騎士の片手剣の型の胴を参照画像にし、同じ部位の共通の走りの胴を見本にして着替えさせる。斜めの走りは奥の脚が体の陰に隠れやすい。元絵と、そこから作る体と足のシートでは、両脚を描くよう指示する。斜めの走りは、`gpt-image-2.6`系の画像編集でGIFとして描かせる（出力形式をgifにし、コマ数とコマの長さを指定する）。1本の動画として描かれるので、コマ同士がつながる。見本には近い向きの良い絵（斜め前なら正面、斜め後ろなら背面の走り）の騎士を1体だけ、見た目の見本として渡し、ポーズは写させない。動きは文章で説明し、他の向きと同じ拍子（どのコマで脚がいちばん開き、どのコマで両足が浮くか）を指定する。ツールは決まったコマを浮くコマとして持ち上げるので、拍子がずれると不自然になる。描けたGIFは、脚の開きと足の高さをコマごとに測って拍子を確かめる。今の斜めの絵を見本に加えるとその絵に引きずられ、横向きのコマを並べて渡すと横向きのポーズを写して不自然になった。脚の重なりやつま先の向きまで細かく指示すると、ポーズが崩れた。返るGIFは背景が透明なので、コマを縮めて走りのシートの並びへ置き、そこから体と部品を前と同じ編集で作る。シートを1枚ずつ描かせて、脚の左右や重なりを指示文、色分け、下書きで指定する方法は、どれも所有者の求める質にならなかった。腰から下だけの絵は安全チェックで断られることがあるので、指示文では鎧の脚の部品として書く。どの向きも各コマの足元を揃え、浮くコマを3 pixel持ち上げる。正面と背面では生成した2歩目が1歩目と同じ脚を出したので、ツールが2歩目の足を1歩目の足の左右反転で作る。体のコマの枠は96 pixelの高さで、足元は下から4 pixelである。横向きの走りでは、剣の腕を剣入りの絵と同じく斜め後ろ下へ下ろした素手の体にしている。持ち物を除く編集で肘を曲げた腕に描かれ、剣を斜め下へ向けると腕が上がって見えたので、横の行だけ腕を描き直し、胴、革の全身、革の胴の横の行もそこから作り直した。素手や弓の時も、横向きの走りはこの下ろした腕になる。新しい型を足すときは、全ての胴と足へ2枚ずつ足す。要件は[装備と動きの要件](../product-specs/equipment-and-motion.md)にある。

1. 見た目を試すときは、`hero-part-template-01.png`を参照画像にしてOpenAI Image APIのeditで描く。頭、胴、足を離して並べた基準の絵である。Promptは同名のJSONにある。
2. 採用する部位は、騎士の同じ姿勢のシートを参照画像にしてeditで着替えさせ、`hero-set-<id>-head-01.png`、`hero-set-<id>-torso-*.png`として保存する。足は騎士の足のシート`hero-legs-*.png`を参照画像にして膝から下だけを描き替え、`hero-set-<id>-legs-*.png`として保存する。`<id>`は部位の名前で、他の装備と重ならないようにする。シートの名前が部位の登録になる。
3. `Tools/MapAuthoring/build_hero_frames.py`を実行する。ツールがシートの名前から部位を見つけ、騎士のコマへ合わせて`Sets/<id>/`へ書き出す。胴の輪郭が騎士と70%未満しか重ならなければ止まる。足は騎士の足元へ揃え、輪郭が騎士と60%未満しか重ならないか、胴の裾が腰に届かなければ止まる。止まって振るときの足も騎士と同じ場所へ置く。
4. Scene Builderを実行する。`Sets/<id>/`ごとに定義Asset`Assets/Data/Gameplay/Characters/Sets/<id>.asset`ができ、全装備の一覧へ載る。コードと一覧へ足す作業はない。
5. 画面の名前は定義Assetの`Display Name`へ入れる。入れるまではidを見せる。入れた名前は作り直しても残る。

剣は着ている胴の部位の拳の両端へ付くので、手袋の色は胴に合う。弓のように拳を重ねる部品は騎士の体から切った拳（`Hand/`）を重ねるので、手袋の色を変えるセットで弓を持つと拳の色が合わない（未対応）。

## GraphicとCollision

- 見た目と移動判定を分離する。
- Map Objectは素材ごとの幹・接地点へ寄せたCollisionを使用し、表示Bounds全体を判定にしない。
- 詳細は[Map Authoring Kit](../design-docs/map-authoring/map-authoring-kit.md)を参照する。

