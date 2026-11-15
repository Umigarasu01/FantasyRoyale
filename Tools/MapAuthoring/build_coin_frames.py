"""倒した敵が落とすゴールドの絵を、ドット絵で書き出す。

引きの画面でもコインと分かるよう、太い縁取り、明るい金、白い光り、回る動きで見せる。
粗いドットに見せるため、半分の解像度で描いてから2倍に拡大する（1ドット = 2 px）。
コインの上で瞬くキラッとした光りの4コマも書き出す。
画素は計算で決めるので、何度実行しても同じ絵になる。

使い方: python build_coin_frames.py [--preview <gif>]
出力: Assets/Art/Generated/ForestLook/Game/coin-0.png 〜 coin-7.png、coin-shadow.png、
      coin-sparkle-0.png 〜 coin-sparkle-3.png
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Art/Generated/ForestLook/Game"

FRAMES = 8
DOT = 2  # 1ドットの大きさ（px）。森の絵より粗く見せる。
FACE = 11  # 面の直径（ドット）。拡大後22 px、64 px = 1 Unity単位なので約0.34単位。騎士の高さ72 pxの3分の1ほど。
SIZE = FACE + 3  # 縁取りと厚みの余白を含めた絵の幅と高さ（ドット）。拡大後28 px。
THICKNESS = 2  # 横を向いたときに見える厚み（ドット）。

OUTLINE = (58, 30, 8, 255)
SIDE = (176, 104, 18, 255)
SIDE_DARK = (130, 72, 12, 255)
RIM = (214, 140, 26, 255)
FACE_LIGHT = (255, 230, 92, 255)
FACE_MID = (250, 196, 46, 255)
FACE_DARK = (226, 154, 30, 255)
MARK = (196, 120, 20, 255)
SHINE = (255, 252, 222, 255)
SHADOW = (20, 28, 12, 110)
SPARKLE_CORE = (255, 255, 255, 255)
SPARKLE_ARM = (255, 246, 170, 255)


def inside(x: float, y: float, cx: float, cy: float, rx: float, ry: float) -> float:
    """楕円の中なら0から1の正規化距離、外なら1より大きい値を返す。"""
    if rx <= 0:
        return 9.0
    return math.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)


def draw_frame(index: int) -> Image.Image:
    """回転の角度から、面の幅と見える厚みを決めて1コマ描く。"""
    angle = index / FRAMES * math.pi
    turn = abs(math.cos(angle))
    radius = FACE / 2
    rx = max(radius * turn, 0.6)
    side = round(THICKNESS * math.sin(angle))
    image = Image.new("RGBA", (SIZE, SIZE))
    pixels = image.load()
    cx = SIZE / 2 - side / 2
    cy = SIZE / 2

    for y in range(SIZE):
        for x in range(SIZE):
            px, py = x + 0.5, y + 0.5
            face = inside(px, py, cx, cy, rx, radius)
            # 厚み: 面を右へずらした楕円を、面の後ろに暗い金で塗る。
            edge = min(inside(px, py, cx + offset, cy, rx, radius) for offset in range(1, side + 1)) if side > 0 else 9.0
            if face <= 1.0:
                pixels[x, y] = face_color(px - cx, py - cy, face, turn, radius)
            elif edge <= 1.0:
                pixels[x, y] = SIDE_DARK if py > cy + radius * 0.35 else SIDE

    outline(image)
    return upscale(image)


def upscale(image: Image.Image) -> Image.Image:
    return image.resize((image.width * DOT, image.height * DOT), Image.NEAREST)


def face_color(dx: float, dy: float, distance: float, turn: float, radius: float):
    """面の色。外周は縁、中は左上ほど明るく、右下ほど暗い。真ん中に刻印の輪と光りを置く。"""
    sx, sy = dx / max(radius * turn, 1e-3), dy / radius
    if distance > 0.74:
        # 縁は右下だけ暗くして、厚みのある硬貨に見せる。
        return MARK if sx + sy > 0.5 else RIM
    # 左上の光り。粗いドットでも見えるよう、2ドットの太さで斜めに置く。
    if turn > 0.3 and -0.75 < sx < -0.05 and -0.75 < sy < -0.05 and -1.0 < sx + sy < -0.55:
        return SHINE
    light = -(sx + sy) * 0.5
    if light > 0.1:
        return FACE_LIGHT
    if light < -0.2:
        return FACE_DARK
    return FACE_MID


def outline(image: Image.Image) -> None:
    """不透明な画素に4方向で接する透明な画素を、焦げ茶の縁取りにする。"""
    pixels = image.load()
    width, height = image.size
    solid = {(x, y) for y in range(height) for x in range(width) if pixels[x, y][3] > 0}
    for y in range(height):
        for x in range(width):
            if (x, y) in solid:
                continue
            if any((x + dx, y + dy) in solid for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                pixels[x, y] = OUTLINE


def draw_shadow() -> Image.Image:
    """足元の影。草の上でもコインが地面から浮いて見えるよう、少し暗い楕円を置く。"""
    width, height = 10, 4
    image = Image.new("RGBA", (width, height))
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            if inside(x + 0.5, y + 0.5, width / 2, height / 2, width / 2, height / 2) <= 1.0:
                pixels[x, y] = SHADOW
    return upscale(image)


def draw_sparkle(size: int) -> Image.Image:
    """十字に伸びるキラッとした光り。sizeは腕の長さ（ドット）で、0は芯だけ。"""
    span = 7
    image = Image.new("RGBA", (span, span))
    pixels = image.load()
    center = span // 2
    pixels[center, center] = SPARKLE_CORE
    for step in range(1, size + 1):
        color = SPARKLE_CORE if step == 1 and size >= 2 else SPARKLE_ARM
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            pixels[center + dx * step, center + dy * step] = color
    if size >= 3:
        for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            pixels[center + dx, center + dy] = SPARKLE_ARM
    return upscale(image)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preview", type=Path, help="草の色の上で回る様子を確かめるGIF")
    args = parser.parse_args()

    OUT.mkdir(parents=True, exist_ok=True)
    frames = [draw_frame(index) for index in range(FRAMES)]
    for index, frame in enumerate(frames):
        frame.save(OUT / f"coin-{index}.png")
    shadow = draw_shadow()
    shadow.save(OUT / "coin-shadow.png")
    # 芯から広がって縮む、瞬き1回分の4コマ。
    sparkles = [draw_sparkle(size) for size in (1, 2, 3, 1)]
    for index, sparkle in enumerate(sparkles):
        sparkle.save(OUT / f"coin-sparkle-{index}.png")
    print(f"wrote {FRAMES} frames, a shadow and {len(sparkles)} sparkles to {OUT}")

    if args.preview:
        scale = 4
        size = SIZE * DOT
        sheet = []
        for index, frame in enumerate(frames):
            canvas = Image.new("RGBA", (size * 3, size + 16), (86, 140, 70, 255))
            canvas.alpha_composite(shadow, (size + (size - shadow.width) // 2, size + 4 - shadow.height // 2))
            canvas.alpha_composite(frame, (size, 0))
            if index < len(sparkles):
                canvas.alpha_composite(sparkles[index], (size + 2, 0))
            sheet.append(canvas.resize((canvas.width * scale, canvas.height * scale), Image.NEAREST).convert("P"))
        sheet[0].save(args.preview, save_all=True, append_images=sheet[1:], duration=80, loop=0)
        print(f"preview: {args.preview}")


if __name__ == "__main__":
    main()
