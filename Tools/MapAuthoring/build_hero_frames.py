"""Cut the generated hero sheets into native-scale frames.

Usage:
    python build_hero_frames.py [--preview <gif-path>]

Reads the hero sheets in Assets/Art/Generated/ForestLook and writes
Hero/hero-<view>-<index>.png and Hero/hero-idle-<view>.png.
Every frame shares one cell size, so the sprite pivot stays at the same foot position.
Frames are aligned by the helmet horizontally and by the feet vertically, which keeps the
upper body steady and leaves the natural bounce of the run. Output is deterministic.
"""
import os
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOREST = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
TARGET = os.path.join(FOREST, "Hero")

# Each sheet draws the hero at its own size. The scales match every helmet to about 28 pixels.
RUN_SHEETS = (
    ("hero-run-01.png", ("down", "side", "up"), 0.22),
    ("hero-run-diagonal-01.png", ("downside", "upside"), 0.207),
)
IDLE_SHEETS = (
    ("hero-idle-01.png", ("down", "side", "up"), 0.135),
    ("hero-idle-diagonal-01.png", ("downside", "upside"), 0.135),
)
VIEWS = ("down", "downside", "side", "upside", "up")
COLUMNS = 4
CELL_WIDTH = 96
CELL_HEIGHT = 80
FOOT_MARGIN = 4


def key_magenta(image):
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, _ = pixels[x, y]
            if r > 150 and b > 130 and g < min(r, b) * 0.62:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def helmet_centre(item):
    """Horizontal centre of the top of the sprite, which does not move with the legs."""
    alpha = item.getchannel("A")
    top = alpha.crop((0, 0, item.width, max(1, item.height // 5)))
    left, _, right, _ = top.getbbox()
    return (left + right) / 2


def shrink(item, scale):
    size = (max(1, round(item.width * scale)), max(1, round(item.height * scale)))
    colour = item.convert("RGB").resize(size, Image.BOX)
    colour.putalpha(item.getchannel("A").resize(size, Image.BOX).point(lambda v: 255 if v >= 150 else 0))
    return colour


def cut(keyed, rows, columns, row, column):
    """The sprite in one grid cell, trimmed to its visible pixels."""
    row_height = keyed.height / rows
    column_width = keyed.width / columns
    box = (
        round(column * column_width), round(row * row_height),
        round((column + 1) * column_width), round((row + 1) * row_height),
    )
    cell = keyed.crop(box)
    return cell.crop(cell.getchannel("A").getbbox())


def place(item, scale, name):
    """Shrink a sprite onto the shared cell, with its helmet centred and its feet on the baseline."""
    centre = helmet_centre(item) * scale
    small = shrink(item, scale)
    canvas = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT), (0, 0, 0, 0))
    left = round(CELL_WIDTH / 2 - centre)
    top = CELL_HEIGHT - FOOT_MARGIN - small.height
    if left < 0 or top < 0 or left + small.width > CELL_WIDTH:
        raise ValueError(f"{name} does not fit: {small.size} at {left},{top}")
    canvas.alpha_composite(small, (left, top))
    canvas.save(os.path.join(TARGET, f"{name}.png"))
    return canvas


def main():
    os.makedirs(TARGET, exist_ok=True)
    frames = {}
    for file, views, scale in RUN_SHEETS:
        keyed = key_magenta(Image.open(os.path.join(FOREST, file)))
        for row, view in enumerate(views):
            for column in range(COLUMNS):
                item = cut(keyed, len(views), COLUMNS, row, column)
                frames[(view, column)] = place(item, scale, f"hero-{view}-{column}")

    idles = {}
    for file, views, scale in IDLE_SHEETS:
        keyed = key_magenta(Image.open(os.path.join(FOREST, file)))
        for column, view in enumerate(views):
            item = cut(keyed, 1, len(views), 0, column)
            idles[view] = place(item, scale, f"hero-idle-{view}")
    print("frames", len(frames), "idles", len(idles), "cell", CELL_WIDTH, CELL_HEIGHT)

    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        zoom = 3
        pages = []
        for column in range(COLUMNS):
            page = Image.new("RGB", (CELL_WIDTH * zoom * len(VIEWS), CELL_HEIGHT * zoom), (106, 148, 62))
            for index, view in enumerate(VIEWS):
                big = frames[(view, column)].resize((CELL_WIDTH * zoom, CELL_HEIGHT * zoom), Image.NEAREST)
                page.paste(big, (index * CELL_WIDTH * zoom, 0), big)
            pages.append(page)
        pages[0].save(path, save_all=True, append_images=pages[1:], duration=90, loop=0)
        sheet = Image.new("RGB", (CELL_WIDTH * 2 * (COLUMNS + 1), CELL_HEIGHT * 2 * len(VIEWS)), (106, 148, 62))
        for (view, column), frame in frames.items():
            big = frame.resize((CELL_WIDTH * 2, CELL_HEIGHT * 2), Image.NEAREST)
            sheet.paste(big, (column * CELL_WIDTH * 2, VIEWS.index(view) * CELL_HEIGHT * 2), big)
        for view, frame in idles.items():
            big = frame.resize((CELL_WIDTH * 2, CELL_HEIGHT * 2), Image.NEAREST)
            sheet.paste(big, (COLUMNS * CELL_WIDTH * 2, VIEWS.index(view) * CELL_HEIGHT * 2), big)
        sheet.save(path.replace(".gif", "-sheet.png"))


if __name__ == "__main__":
    main()
