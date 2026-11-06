"""Catalog of the objects placed on the forest map.

Each sheet is a generated 3 x 3 grid on magenta. Running this file writes the sprites Unity places itself. An entry says where an object is on its sheet,
how wide it is on the map, and how much of it blocks the player. Objects are sized against the
hero, who is 72 pixels tall: a door matches the hero, and a crate reaches about the hero's waist.
"""
import os

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
PPU = 64
COLUMNS = 3
ROWS = 3
GAP = 6

VILLAGE = "forest-village-01.png"
RURAL = "forest-rural-01.png"
LANDMARKS = "forest-landmarks-01.png"
EXTRAS = "forest-extras-01.png"
ENEMY = "forest-slime-01.png"
ENEMY_FRAMES = 4
ENEMY_SCALE = 0.24
ENEMY_CANVAS = (72, 76)
ENEMY_FOOT = 4
CHEST_WIDTH = 74

# id: sheet, row, column, width on the map in pixels, foot in pixels above the sprite's bottom,
# depth of the blocking area in world units, and the share of the width that blocks.
# A depth of zero means the player can walk over it.
OBJECTS = {
    "inn": (VILLAGE, 0, 0, 520, 10, 2.4, 0.94),
    "timber-hut": (VILLAGE, 0, 1, 300, 10, 1.9, 0.92),
    "market-stall": (VILLAGE, 0, 2, 200, 8, 0.9, 0.92),
    "fountain": (VILLAGE, 1, 0, 180, 8, 1.1, 0.86),
    "crate": (VILLAGE, 1, 1, 62, 4, 0.55, 0.9),
    "barrel": (VILLAGE, 1, 2, 44, 4, 0.5, 0.9),
    "signpost": (VILLAGE, 2, 2, 72, 6, 0.25, 0.3),
    "tent": (RURAL, 0, 0, 300, 8, 1.6, 0.8),
    "fire": (RURAL, 0, 1, 130, 8, 0.8, 0.85),
    "watermill": (RURAL, 0, 2, 430, 8, 2.2, 0.92),
    "watchtower": (RURAL, 1, 0, 210, 8, 1.2, 0.8),
    "logging-shed": (RURAL, 1, 1, 380, 8, 1.4, 0.9),
    "log-stack": (RURAL, 1, 2, 300, 8, 1.1, 0.92),
    "stump": (RURAL, 2, 0, 80, 6, 0.5, 0.7),
    "crop-row": (RURAL, 2, 1, 340, 6, 0, 0),
    "fence": (RURAL, 2, 2, 300, 6, 0.4, 1.0),
    "cave-mouth": (LANDMARKS, 0, 0, 290, 8, 1.0, 0.98),
    "mine-mouth": (LANDMARKS, 0, 1, 270, 8, 1.0, 0.98),
    "cliff-face-small": (LANDMARKS, 0, 2, 384, 10, 2.0, 0.96),
    "ruined-arch": (LANDMARKS, 1, 0, 420, 8, 1.0, 0.9),
    "ruined-column": (LANDMARKS, 1, 1, 150, 8, 0.9, 0.7),
    "wayside-shrine": (LANDMARKS, 1, 2, 130, 6, 0.9, 0.7),
    "natural-spring": (LANDMARKS, 2, 0, 300, 10, 1.6, 0.9),
    "monster-nest": (LANDMARKS, 2, 1, 250, 8, 1.2, 0.85),
    "loaded-cart": (LANDMARKS, 2, 2, 190, 6, 0.9, 0.9),
    "well": (EXTRAS, 0, 0, 120, 6, 0.8, 0.85),
    "broken-house": (EXTRAS, 0, 1, 320, 10, 1.9, 0.92),
    "herb-bed": (EXTRAS, 0, 2, 220, 6, 0, 0),
    "tool-bench": (EXTRAS, 1, 0, 130, 6, 0.6, 0.9),
    "crate-stack": (EXTRAS, 1, 1, 110, 6, 0.7, 0.9),
    "rock-cluster": (EXTRAS, 1, 2, 130, 6, 0.7, 0.85),
    "fallen-log": (EXTRAS, 2, 0, 170, 6, 0.5, 0.9),
    "notice-board": (EXTRAS, 2, 1, 110, 6, 0.3, 0.8),
    "rubble": (EXTRAS, 2, 2, 150, 6, 0.6, 0.85),
}


def key_magenta(image):
    """Remove the background, including soft pixels that still lean toward it."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, _ = pixels[x, y]
            if r > 110 and b > 90 and g < min(r, b) * 0.72:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def largest_band(values):
    """The longest run of truthy values, joining runs closer than GAP."""
    best = (0, 0)
    start = last = None
    for index, value in enumerate(list(values) + [False] * (GAP + 2)):
        if value:
            start = index if start is None else start
            last = index
        elif start is not None and index - last > GAP:
            if last + 1 - start > best[1] - best[0]:
                best = (start, last + 1)
            start = None
    return best


def main_item(cell):
    """Crop to the object, leaving out stray pixels from a neighbouring cell."""
    alpha = cell.getchannel("A")
    data = alpha.tobytes()
    width, height = cell.size
    top, bottom = largest_band(any(data[y * width:(y + 1) * width]) for y in range(height))
    strip = alpha.crop((0, top, width, bottom)).tobytes()
    left, right = largest_band(any(strip[y * width + x] for y in range(bottom - top)) for x in range(width))
    return cell.crop((left, top, right, bottom))


def fit(item, width):
    """Resize a sprite without transparent pixels bleeding into its colours."""
    height = round(item.height * width / item.width)
    alpha = item.getchannel("A")
    filled = Image.new("RGB", item.size)
    filled.paste(item.convert("RGB"), mask=alpha)
    method = Image.BOX if width < item.width else Image.LANCZOS
    colour = filled.resize((width, height), method)
    weight = alpha.resize((width, height), method)
    colour_px, weight_px = colour.load(), weight.load()
    for y in range(height):
        for x in range(width):
            w = weight_px[x, y]
            if w:
                r, g, b = colour_px[x, y]
                colour_px[x, y] = (min(255, r * 255 // w), min(255, g * 255 // w), min(255, b * 255 // w))
    colour.putalpha(weight.point(lambda v: 255 if v >= 128 else 0))
    return colour


def load_sprites():
    """Cut every object from its sheet at its size on the map."""
    sheets = {}
    sprites = {}
    for name, (file, row, column, width, *_rest) in OBJECTS.items():
        if file not in sheets:
            sheets[file] = key_magenta(Image.open(os.path.join(SOURCE, file)))
        sheet = sheets[file]
        cell_width, cell_height = sheet.width / COLUMNS, sheet.height / ROWS
        cell = sheet.crop((
            round(column * cell_width), round(row * cell_height),
            round((column + 1) * cell_width), round((row + 1) * cell_height),
        ))
        sprites[name] = fit(main_item(cell), width)
    return sprites


def foot(name):
    return OBJECTS[name][4]


def bodies(name, sprite):
    """Blocking rectangles as x, y, width, height in world units from the foot, y going up."""
    depth, share = OBJECTS[name][5], OBJECTS[name][6]
    if depth <= 0:
        return []
    width = sprite.width / PPU
    if name == "ruined-arch":
        # The gap in the middle stays open, so the player can walk through the arch.
        side = width * 0.3
        return [(-width * 0.47, 0, side, depth), (width * 0.17, 0, side, depth)]
    return [(-width * share / 2, -0.1, width * share, depth)]


def write_game_sprites():
    """Write the sprites that Unity places itself: chests and the enemy's hop."""
    target = os.path.join(SOURCE, "Game")
    os.makedirs(target, exist_ok=True)
    sheet = key_magenta(Image.open(os.path.join(SOURCE, VILLAGE)))
    cell_width, cell_height = sheet.width / COLUMNS, sheet.height / ROWS
    for name, column in (("chest-closed", 0), ("chest-open", 1)):
        cell = sheet.crop((
            round(column * cell_width), round(2 * cell_height),
            round((column + 1) * cell_width), round(3 * cell_height),
        ))
        fit(main_item(cell), CHEST_WIDTH).save(os.path.join(target, name + ".png"))

    sheet = key_magenta(Image.open(os.path.join(SOURCE, ENEMY)))
    cell_width = sheet.width / ENEMY_FRAMES
    for frame in range(ENEMY_FRAMES):
        cell = sheet.crop((round(frame * cell_width), 0, round((frame + 1) * cell_width), sheet.height))
        item = main_item(cell)
        small = fit(item, round(item.width * ENEMY_SCALE))
        # Every frame shares one canvas, so the feet stay still through the hop.
        canvas = Image.new("RGBA", ENEMY_CANVAS, (0, 0, 0, 0))
        canvas.paste(small, ((ENEMY_CANVAS[0] - small.width) // 2, ENEMY_CANVAS[1] - ENEMY_FOOT - small.height))
        canvas.save(os.path.join(target, f"slime-{frame}.png"))
    print("game sprites written")


if __name__ == "__main__":
    write_game_sprites()
