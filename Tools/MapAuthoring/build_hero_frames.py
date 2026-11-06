"""Cut the hero's body frames and gear parts into native-scale sprites.

Usage:
    python build_hero_frames.py [--preview <png-path>]

The hero has one empty-handed body animation. Gear is drawn apart and held at anchors, so a new
item needs only its own small sprites and never a new animation.

Reads the hero-body sheets and hero-gear-01.png in Assets/Art/Generated/ForestLook and writes:
    Hero/hero-<view>-<index>.png, Hero/hero-idle-<view>.png, Hero/anchors.json
    Gear/<item>-<view>.png, Gear/gear.json

Every body frame shares one cell size, so the sprite pivot stays at the same foot position.
Frames are aligned by the helmet horizontally and by the feet vertically, which keeps the
upper body steady and leaves the natural bounce of the run. Output is deterministic.
"""
import json
import os
import sys
from collections import deque

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOREST = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
HERO = os.path.join(FOREST, "Hero")
GEAR = os.path.join(FOREST, "Gear")

# Each sheet draws the hero at its own size. The scales match every helmet to about 28 pixels.
RUN_SHEETS = (
    ("hero-body-run-01.png", ("down", "side", "up"), 0.22),
    ("hero-body-run-diagonal-01.png", ("downside", "upside"), 0.207),
)
IDLE_SHEETS = (
    ("hero-body-idle-01.png", ("down", "side", "up"), 0.135),
    ("hero-body-idle-diagonal-01.png", ("downside", "upside"), 0.135),
)
VIEWS = ("down", "downside", "side", "upside", "up")
COLUMNS = 4
CELL_WIDTH = 96
CELL_HEIGHT = 80
FOOT_MARGIN = 4

# Which glove is the main hand, by its place on screen. The hero faces right in the side views.
# Seen from the front the right hand is on the left of the screen, and from behind on the right.
MAIN_HAND_IS_LEFT = {"down": True, "downside": True, "side": True, "upside": False, "up": False}

MIN_HAND_GAP = 14

# The back anchor sits on the spine, as a share of the sprite's height from its top.
BACK_HEIGHT = 0.42
BACK_OFFSET_X = {"down": 0, "downside": -4, "side": -8, "upside": -3, "up": 0}

# Gear sheet: one row per item, one column per view, in VIEWS order.
GEAR_SHEET = "hero-gear-01.png"
# item: slot, row, height on the map in pixels, and the grip as a share of the sprite for each view.
GEAR_ITEMS = {
    "sword": ("MainHand", 0, 34, {
        "down": (0.5, 0.74), "downside": (0.76, 0.74), "side": (0.5, 0.74), "upside": (0.24, 0.74), "up": (0.5, 0.74),
    }),
    "shield": ("OffHand", 1, 30, {view: (0.5, 0.5) for view in VIEWS}),
    "bow": ("OffHand", 2, 46, {view: (0.5, 0.5) for view in VIEWS}),
    "quiver": ("Back", 3, 34, {view: (0.5, 0.52) for view in VIEWS}),
}


# These parts are drawn leaning across the body, so they are mirrored to lean outward.
GEAR_MIRRORED = {("sword", "downside"), ("sword", "upside")}


def key_magenta(image):
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, _ = pixels[x, y]
            if r > 110 and b > 90 and g < min(r, b) * 0.72:
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
    return canvas


def is_glove(pixel):
    r, g, b, a = pixel
    return a > 0 and 70 <= r <= 190 and r > g + 22 and g > b + 8 and b < 90


def gloves(frame):
    """Brown clusters in the arm band, largest first, as (size, centre x, centre y)."""
    pixels = frame.load()
    _, top, _, bottom = frame.getchannel("A").getbbox()
    height = bottom - top
    first, last = top + int(height * 0.38), top + int(height * 0.74)
    seen = set()
    clusters = []
    for y in range(first, last):
        for x in range(frame.width):
            if (x, y) in seen or not is_glove(pixels[x, y]):
                continue
            cells = []
            queue = deque([(x, y)])
            seen.add((x, y))
            while queue:
                cx, cy = queue.popleft()
                cells.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if first <= ny < last and 0 <= nx < frame.width and (nx, ny) not in seen and is_glove(pixels[nx, ny]):
                        seen.add((nx, ny))
                        queue.append((nx, ny))
            if len(cells) >= 10:
                clusters.append((len(cells), sum(c[0] for c in cells) / len(cells), sum(c[1] for c in cells) / len(cells)))
    return sorted(clusters, reverse=True)


def anchors(frame, view, name):
    """The hands and the spine of one frame, in pixels from the foot, with y going up."""
    found = gloves(frame)
    # A forearm can break into two clusters, so the second hand must be clearly apart from the first.
    apart = [item for item in found[1:] if abs(item[1] - found[0][1]) >= MIN_HAND_GAP] if found else []
    if not apart:
        raise ValueError(f"{name}: expected two gloves, found {found}")
    first, second = sorted((found[0], apart[0]), key=lambda item: item[1])
    main, off = (first, second) if MAIN_HAND_IS_LEFT[view] else (second, first)
    _, top, _, bottom = frame.getchannel("A").getbbox()
    foot_x, foot_y = CELL_WIDTH / 2, CELL_HEIGHT - FOOT_MARGIN
    spine_y = top + (bottom - top) * BACK_HEIGHT

    def point(x, y):
        return {"x": round(x - foot_x, 1), "y": round(foot_y - y, 1)}

    return {
        "mainHand": point(main[1], main[2]),
        "offHand": point(off[1], off[2]),
        "back": point(foot_x + BACK_OFFSET_X[view], spine_y),
    }


def build_body():
    os.makedirs(HERO, exist_ok=True)
    frames = {}
    for file, views, scale in RUN_SHEETS:
        keyed = key_magenta(Image.open(os.path.join(FOREST, file)))
        for row, view in enumerate(views):
            for column in range(COLUMNS):
                name = f"hero-{view}-{column}"
                frames[name] = place(cut(keyed, len(views), COLUMNS, row, column), scale, name)
    for file, views, scale in IDLE_SHEETS:
        keyed = key_magenta(Image.open(os.path.join(FOREST, file)))
        for column, view in enumerate(views):
            name = f"hero-idle-{view}"
            frames[name] = place(cut(keyed, 1, len(views), 0, column), scale, name)

    data = {"frames": []}
    for name, frame in frames.items():
        frame.save(os.path.join(HERO, name + ".png"))
        view = name.split("-")[-1] if "idle" in name else name.split("-")[1]
        data["frames"].append({"name": name, **anchors(frame, view, name)})
    with open(os.path.join(HERO, "anchors.json"), "w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, indent=1)
        file.write("\n")
    print("body frames", len(frames))
    return frames, {entry["name"]: entry for entry in data["frames"]}


def build_gear():
    os.makedirs(GEAR, exist_ok=True)
    keyed = key_magenta(Image.open(os.path.join(FOREST, GEAR_SHEET)))
    rows = max(item[1] for item in GEAR_ITEMS.values()) + 1
    data = {"items": []}
    count = 0
    for item, (slot, row, height, grips) in GEAR_ITEMS.items():
        views = []
        for column, view in enumerate(VIEWS):
            part = cut(keyed, rows, len(VIEWS), row, column)
            name = f"{item}-{view}"
            if (item, view) in GEAR_MIRRORED:
                part = part.transpose(Image.FLIP_LEFT_RIGHT)
            shrink(part, height / part.height).save(os.path.join(GEAR, name + ".png"))
            count += 1
            pivot_x, pivot_from_top = grips[view]
            views.append({"view": view, "file": name, "pivotX": pivot_x, "pivotY": round(1 - pivot_from_top, 3)})
        data["items"].append({"name": item, "slot": slot, "views": views})
    with open(os.path.join(GEAR, "gear.json"), "w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, indent=1)
        file.write("\n")
    print("gear sprites", count)


def preview(path, frames, points):
    """A sheet of every view with its anchors marked: red main hand, blue off hand, yellow back."""
    zoom = 3
    width, height = CELL_WIDTH * zoom, CELL_HEIGHT * zoom
    sheet = Image.new("RGB", (width * (COLUMNS + 1), height * len(VIEWS)), (106, 148, 62))
    draw = ImageDraw.Draw(sheet)
    for row, view in enumerate(VIEWS):
        names = [f"hero-{view}-{column}" for column in range(COLUMNS)] + [f"hero-idle-{view}"]
        for column, name in enumerate(names):
            big = frames[name].resize((width, height), Image.NEAREST)
            left, top = column * width, row * height
            sheet.paste(big, (left, top), big)
            for key, colour in (("mainHand", (255, 40, 40)), ("offHand", (40, 120, 255)), ("back", (255, 240, 60))):
                x = left + (points[name][key]["x"] + CELL_WIDTH / 2) * zoom
                y = top + (CELL_HEIGHT - FOOT_MARGIN - points[name][key]["y"]) * zoom
                draw.ellipse((x - 5, y - 5, x + 5, y + 5), outline=colour, width=2)
    sheet.save(path)


def main():
    frames, points = build_body()
    build_gear()
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1], frames, points)


if __name__ == "__main__":
    main()
