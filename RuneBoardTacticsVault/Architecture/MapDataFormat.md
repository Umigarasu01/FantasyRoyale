# Map Data Format

> [!warning] 保留
> Mapの正本をUnity Scene / Tilemapとする方針へ変更したため、`.frmap` v1は実装しない。
> 外部データ交換が必要になった場合の旧検討案として残す。

## 位置づけ

このノートは、独立したマップ作成ツールと FantasyRoyale が共有する唯一の境界を定義する。

ツール内部の編集データ、Unity EditorWindow、使用素材、プレビューSceneの構造はゲーム側の仕様に含めない。ゲーム側は、公開済みの `.frmap` を読み取り、記録されたタイル、オブジェクト、当たり判定、配置ソケットを再現できればよい。

状態: **旧ドラフト / 実装保留**

## 基本方針

- 拡張子は `.frmap`、内容は UTF-8 JSON とする。
- Unity の GUID、Asset path、`Vector2Int` などの Unity 型を含めない。
- 座標原点は左下、X は右、Y は上へ増える。
- 1セルは 32x32 pixel を標準とするが、ゲーム内の World Scale はConsumer側が決める。
- 配列上のセルIndexは `x + y * width` とする。
- マップ作成ツールがオートタイル、地面差分、静的装飾を解決し、出力には最終的な `assetId` を記録する。
- ゲーム側で同じ乱数生成やオートタイル解決を再実装しない。
- `assetId` は安定した文字列IDとし、画像ファイル名や保存場所に依存させない。

## v1 構造

```json
{
  "format": "GbaMap",
  "schemaVersion": 1,
  "mapId": "forest_field_01",
  "revision": 1,
  "toolVersion": "0.1.0",
  "width": 2,
  "height": 2,
  "tileSizePixels": 32,
  "assetCatalog": {
    "catalogId": "gba_forest_v1",
    "version": 1,
    "contentHash": "sha256:..."
  },
  "assetTable": [
    "",
    "terrain.forest.grass.base.01",
    "terrain.forest.path.mask_255.01"
  ],
  "tileLayers": [
    {
      "layerId": "ground",
      "renderOrder": 0,
      "cells": [1, 1, 1, 2]
    }
  ],
  "collisionFlags": [0, 0, 1, 1],
  "objects": [],
  "sockets": [],
  "metadata": {
    "displayName": "Forest Field 01",
    "tags": ["forest", "prototype"]
  },
  "contentHash": "sha256:..."
}
```

実データでは、各 `cells` と `collisionFlags` の要素数は必ず `width * height` と一致させる。

## Asset Table

- Index `0` は空セル専用とする。
- `tileLayers[].cells` は `assetTable` のIndexを記録する。
- `assetCatalog.contentHash` が一致しないCatalogをConsumerが使用した場合はErrorとする。
- Catalog内で廃止されたIDは削除せず、`deprecated` として互換期間を持たせる。

## Tile Layers

v1 の標準レイヤーは以下とする。

| layerId | 役割 |
| --- | --- |
| `ground` | 基本地面 |
| `terrain` | 道、水辺、崖面などの地形 |
| `detail` | 通行可能な草、花、泡など |
| `obstacle` | 壁、崖、通行不能地形の見た目 |
| `foreground` | プレイヤーより前へ描く樹冠など |

Consumerは `renderOrder` を正として描画順を決める。未知の `layerId` は、対応する描画機能がなければ明示的なWarningを残す。

## Collision Flags

`collisionFlags` はセル単位のbit flagとする。

| Bit | 名前 | 意味 |
| --- | --- | --- |
| `1 << 0` | `BlockMovement` | キャラクター移動を遮る |
| `1 << 1` | `BlockProjectile` | 飛び道具を遮る |
| `1 << 2` | `BlockSight` | 視線判定を遮る |
| `1 << 3` | `Hazard` | 危険地形として扱う候補 |
| `1 << 4` | `Slow` | 移動低下地形として扱う候補 |

`Hazard` と `Slow` の具体的なゲーム効果はConsumer側が決める。ツールは意味を示すだけで、ダメージ量や速度倍率を持たない。

## Objects

```json
{
  "instanceId": "tree_0123",
  "assetId": "prop.forest.tree.medium.02",
  "cell": [12, 18],
  "offsetPixels": [0, 0],
  "sortOffset": 0,
  "tags": ["static", "blocker"]
}
```

- `instanceId` はマップ内で一意にする。
- Pivot、足元Anchor、占有セル、Collider形状はAsset Catalog側の契約を正とする。
- `offsetPixels` は整数のみ許可し、サブピクセル配置は禁止する。
- GBA素材では回転と任意角度の変形を禁止する。向き差分が必要な場合は別 `assetId` を使用する。
- 静的オブジェクトによる移動不可は `collisionFlags` へコンパイル済みにする。

## Sockets

```json
{
  "socketId": "player_start_01",
  "socketType": "player_start",
  "cell": [8, 6],
  "sizeCells": [2, 2],
  "facing": "north",
  "required": true,
  "tags": ["safe_start"]
}
```

Socketはゲーム固有オブジェクトそのものではなく、Consumerが後から解釈する配置候補地とする。

- `socketType` と `tags` は安定した文字列IDとする。
- v1 の基本候補は `player_start`、`cpu_start`、`enemy`、`loot`、`merchant`、`event`、`landmark`。
- Toolは未知のSocket種別も保持できる。
- `required` Socketが移動不可セルと重なる場合はPublish Errorとする。

## Hashと決定性

- `contentHash` 自身を除外した正規化JSONへ SHA-256 を適用する。
- Key順、数値表現、改行コードを固定し、同一入力から同一Hashを得る。
- 同じ編集データ、Asset Catalog、Tool Version、Seedからは同じ `.frmap` を生成する。
- 地面差分と静的装飾の選択は、座標と安定IDを含む局所Hashから決める。素材追加によって無関係な全セルが再抽選されないようにする。

## Consumer側の必須検証

- `format` が `GbaMap` ではない。
- 対応していない `schemaVersion`。
- `contentHash` 不一致。
- Asset Catalog ID、version、hashの不一致。
- 未知の `assetId`。
- LayerまたはCollision配列長の不一致。
- Object、Socket IDの重複。
- 座標やFootprintのマップ外参照。

いずれかを検出した場合、部分的に読み込まずマップ全体をRejectする。

## 互換性ルール

- 必須Fieldや意味を変更する場合は `schemaVersion` を上げる。
- `metadata` への任意Field追加は同じSchema Versionで許可する。
- Consumerは未知のMetadataを無視してよい。
- v1の公開後は、既存Fieldの意味を後から変更しない。
