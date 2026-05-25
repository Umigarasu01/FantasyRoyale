# 進行中作業

## 現在の方針

FantasyRoyale の初期企画をもとに、一人で遊べる最小プロトタイプを作る。

## 現在の対象

- Step 3: 5-6人規模のバトロワを想定した広めの探索マップにする。
- 草原、雪原、火山地帯、溶岩地形、中央街道を仮配置する。
- カメラはプレイヤー追従にする。
- 設置物は商人のみ。宝箱は生成Sceneから外す。
- 商人は開始地点近くと各ロケーションに配置する。

## 次に進める候補

- Unity上で `FantasyRoyale/Build Solo Prototype Scene` を実行し、`PrototypeSoloScene` を生成する。
- 生成Sceneで、広域マップ探索、カメラ追従、商人の視認性、各ロケーションの見分けやすさを確認する。
- 商人購入と敵撃破によるコイン獲得を確認する。
- Step 3 の触り心地を見て、次の Step を決める。
## 2026-05-24 追記

- 仮素材は `Assets/Art/Prototype/Generated/` に隔離し、外部素材候補とは混ぜない。
- Anokolisa の Pixel Crawler Free Pack は候補として扱い、正式取り込み前に同梱ライセンスを確認する。
- 木、岩、溶岩は地形障害物として当たり判定を付ける。遊び上の設置物は商人のみのままにする。
- 参考画像方向の仮グラフィックを36点作成し、複雑マップ配置を `PrototypeSceneBuilder` に追加した。
- 次の確認: Unity上で `FantasyRoyale > Build Solo Prototype Scene` を実行し、生成された複雑マップの密度、視認性、当たり判定を確認する。
