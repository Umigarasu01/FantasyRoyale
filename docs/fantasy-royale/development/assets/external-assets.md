# 外部素材

## 現在のMap Authoring Kit

外部配布素材は使用していない。

- 森Themeの候補AtlasはCodex imagegenで生成。
- Production PNGは候補Atlasからローカル変換。
- Tile、RuleTile、Palette、Prefab、SceneはUnity Editor APIで生成。

したがって、現在のMap Authoring Kitに第三者Asset Packのライセンス依存はない。

## 旧候補

Anokolisaの Free Pixel Art Asset Pack は過去の差し替え候補だったが、配布ZIP内のライセンスを確認できておらず、現行実装には取り込んでいない。旧候補フォルダも2026-07-10に削除した。

今後、外部素材を導入する場合は、素材名、用途、提供元、利用条件を事前に確認し、Assets/Art/External配下へ提供元単位で隔離する。

## 2026-11-09 ピクセルフォントの候補

状態: **使用中**。所有者が両方を試し、PixelMplus12 BoldをHUDの文字に選んだ（2026-11-09）。HUDの数字はこのフォントで描く。日本語の長い文に使えるよう、PixelMplus12 Regularも残した。DotGothic16とPixelMplus10は使わないので取り除いた。

所有者がHUDの文字にピクセルフォントを試したいとした。HUDは今は数字だけだが、後で日本語も出すので、日本語の漢字まで入っている物を選んだ。2026-11-09に配布元のページとライセンスの本文を確かめた。

| 候補 | 提供元 | ライセンス | 配布ファイル | 中身 |
| --- | --- | --- | --- | --- |
| DotGothic16 | Fontworks（Google Fonts、`github.com/google/fonts`の`ofl/dotgothic16`） | SIL Open Font License 1.1。ゲームに同梱して販売してよい。フォント単体の販売は不可。著作権表示とライセンス文を同梱する | `DotGothic16-Regular.ttf`（2,069,236 bytes）と`OFL.txt` | 16 pixelの格子のドット風ゴシック。太さは1つ。日本語、ラテン文字、キリル文字 |
| PixelMplus | itouhiro（`github.com/itouhiro/PixelMplus`、M+のビットマップフォントが元） | M+ FONT LICENSE。商用、非商用を問わず、改変の有無を問わず、使用、複製、配布が無制限に許される。無保証 | Release v1.0.0の`PixelMplus-20130602.zip`（1,428,228 bytes） | 10 pixelと12 pixelの2系統、それぞれRegularとBold。JIS第1・第2水準の漢字とLatin-1 |

ピクセルフォントは、元の格子の整数倍の大きさで描かないと、ドットの太さが揃わない。HUDは画面の高さに合わせて拡大縮小するので、組み込むときは1080pと900pの両方で数字の見え方を確かめる。

残している物は次のとおりである。配布元から直接ダウンロードし、ファイルの大きさとライセンス文が事前に確かめた内容と同じことを確かめた。PixelMplusはzipから、フォントと説明とライセンス文だけを入れた。

| ファイル | SHA-256 |
| --- | --- |
| `Assets/Art/External/itouhiro/PixelMplus/PixelMplus12-Bold.ttf` | `7276753dc80861e2f45257b7b1a54c967c160065f5d40e10fb48aabd5648a2a6` |
| `Assets/Art/External/itouhiro/PixelMplus/PixelMplus12-Regular.ttf` | `02f19467ea7cc235cc06c570b7f6c3b0a12a6f682bc8d74c43f2d323d97bcd12` |

同じフォルダに、PixelMplusの`README.txt`、`LICENSE_E`、`LICENSE_J`（zipの`mplus_bitmap_fonts/`から）も置いた。zip（`PixelMplus-20130602.zip`）のSHA-256は`ca4b5eecc9cf869e85d480000084ecc0fb24c16fc8b37b4046145ecd642fae11`である。

取り込み設定: PixelMplusはRendering ModeをHinted Rasterにして、ドットをぼかさない。比べたときのDotGothic16（`DotGothic16-Regular.ttf`、SHA-256 `3ad9af88…`）はHinted Rasterだと線が細く崩れ、Smoothにした。取り除いたPixelMplus10の2つ（Bold `3875ea41…`、Regular `01b5e4ae…`）とDotGothic16は、上の配布元から同じ物を取り直せる。
