# GroundVariationTile

状態: **v4.3継続使用・Unity検証済み**

## 役割

地面の同一Sprite反復と短周期の格子模様を抑えるため、8枚のSpriteからCell座標とSeedだけで表示差分を選ぶRuntime用`TileBase`。

- Script: `Assets/Scripts/MapAuthoringKit/Runtime/GroundVariationTile.cs`
- Namespace: `FantasyRoyale.MapAuthoringKit`
- Collider: なし
- 色: White固定
- Transform: Identity固定
- TileFlags: LockColor / LockTransform

Scene生成順、Tilemapの走査順、`UnityEngine.Random`、端末時刻には依存しない。同じXY座標とSeedには常に同じvariant indexを返す。Z座標は2D地面の選択へ使用しない。

## 公開API

### `VariantCount` / `Variants`

- `VariantCount`は保存中のSprite参照数を返す。
- `Variants`は要素を差し替えられない`IReadOnlyList<Sprite>`として参照一覧を返す。
- Unity再読込後も、保存済み配列から読み取り専用Viewを遅延生成する。

### `Configure(Sprite[] sprites, int variationSeed)`

8枚のSpriteとSeedを設定する。

- Sprite配列が`null`の場合は`ArgumentNullException`。
- 枚数が8以外、または要素に`null`を含む場合は`ArgumentException`。
- 呼び出し元による配列差し替えの影響を受けないよう、配列を複製して保持する。

### `GetVariantIndex(Vector3Int position)`

XY座標と保存済みSeedを32bit 2D hashへ混合し、0以上8未満のindexを返す。8枚固定のため、avalanche後の下位3bitを使用する。

### `GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)`

選択したSpriteと固定描画設定を`TileData`へ返す。Sprite配列が未設定または破損している場合は表示Spriteを`null`にするが、ColliderやGameObjectは生成しない。

## Hash契約

単純な`x + y`、小さい係数、短い剰余周期は使わない。

- X、Y、Seedを異なる32bit奇数定数で混合する。
- 循環Shiftで軸ごとのbit位置を分散する。
- 二段の乗算とXOR Shiftによる最終avalancheを行う。
- 符号付き座標は`uint`へbit列として変換し、負座標でも同じ演算規約を使う。
- オーバーフローは意図したmod 2^32演算とする。

この型は各Cellのmicro variationを決定的に選択する責務だけを持つ。非矩形macro patch、Overlay配置、道や水との除外処理はScene Builder側の別責務とする。
