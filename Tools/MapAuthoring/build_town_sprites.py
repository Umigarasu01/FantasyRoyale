"""Cut the central town's shops into map sprites.

Usage:
    python build_town_sprites.py

Reads from Assets/Art/Generated/ForestLook:
    prop-shop-03.png       knight at half size | market stall | junk dealer's rug
    prop-shophouse-01.png  weapon shop | armour shop | magic shop | item shop

Writes to Assets/Art/Generated/ForestLook/Town:
    town-stall.png, town-rug.png, weapon-shop.png, armour-shop.png, magic-shop.png, item-shop.png

Every person in the town is as big as the knight: a head about as wide as the knight's helmet, 28 pixels.
The stall and the rug are drawn beside the knight, so they are shrunk by the knight's own scale; the knight
is drawn at half size there, so the stall fits on the sheet. A shop can reach into the knight's column, so
each is cut as one connected blob, and the knight is measured on its own blob. The shop houses were drawn
without the knight; they are shrunk so their shopkeepers' heads come out as wide as the knight's. Their sheet has a purple roof, which the forest's magenta key removes, so it is keyed
only where a pixel is nearly pure magenta. These are first sprites for placing the town: the drawings will be
refined later. forest_objects.py lists them with their feet and blocking areas. Output is deterministic.
"""
import os

from PIL import Image, ImageFilter

from build_hero_frames import shrink
from forest_objects import key_magenta, main_item

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOREST = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
OUT = os.path.join(FOREST, "Town")

# The knight of hero-idle-01.png is 530 pixels tall and is shrunk by 0.135.
KNIGHT_HEIGHT = 530
KNIGHT_SCALE = 0.135
# The shopkeepers' heads come out about 26 pixels wide, as wide as the knight's helmet. The houses are then
# 2.7 units wide and 3.5 units tall, about three times the knight's height.
HOUSE_SCALE = 0.5
HOUSES = ("weapon-shop", "armour-shop", "magic-shop", "item-shop")
# Shops beside the knight: sheet, and the file of each blob left to right after the knight.
BESIDE_KNIGHT = (
    ("prop-shop-03.png", ("town-stall", "town-rug")),
)
BLOB_STEP = 4  # blobs are found on the alpha shrunk by this much
PINK = 25  # red and blue both this far above green: a pixel mixed with the magenta background
PURE_MAGENTA = 150  # distance from pure magenta below which a pixel is background


def clear_pink(image):
    image = image.copy()
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a and r - g > PINK and b - g > PINK:
                pixels[x, y] = (0, 0, 0, 0)
    return image


def key_pure_magenta(image):
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, _ = pixels[x, y]
            if (255 - r) + g + (255 - b) < PURE_MAGENTA:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def blobs(keyed):
    """Connected areas of the sheet, largest first, as (size, box, cells) on a grid of BLOB_STEP pixels."""
    step = BLOB_STEP
    alpha = keyed.getchannel("A").resize((keyed.width // step, keyed.height // step), Image.BOX)
    alpha = alpha.point(lambda v: 255 if v > 40 else 0)
    width, height = alpha.size
    px = alpha.load()
    seen, out = set(), []
    for y in range(height):
        for x in range(width):
            if not px[x, y] or (x, y) in seen:
                continue
            stack, cells = [(x, y)], []
            seen.add((x, y))
            while stack:
                cx, cy = stack.pop()
                cells.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        n = (cx + dx, cy + dy)
                        if 0 <= n[0] < width and 0 <= n[1] < height and px[n] and n not in seen:
                            seen.add(n)
                            stack.append(n)
            xs = [c[0] for c in cells]
            ys = [c[1] for c in cells]
            box = (min(xs) * step, min(ys) * step, (max(xs) + 1) * step, (max(ys) + 1) * step)
            out.append((len(cells), box, cells))
    return sorted(out, key=lambda b: -b[0])


def isolate(keyed, box, cells):
    """The blob's pixels only, so a neighbour reaching into its box is left out."""
    crop = keyed.crop(box)
    mask = Image.new("L", crop.size, 0)
    mp = mask.load()
    for cx, cy in cells:
        for dx in range(BLOB_STEP):
            for dy in range(BLOB_STEP):
                x, y = cx * BLOB_STEP + dx - box[0], cy * BLOB_STEP + dy - box[1]
                if 0 <= x < crop.width and 0 <= y < crop.height:
                    mp[x, y] = 255
    mask = mask.filter(ImageFilter.MaxFilter(2 * BLOB_STEP + 1))
    crop.putalpha(Image.composite(crop.getchannel("A"), Image.new("L", crop.size, 0), mask))
    return crop


def trimmed(image):
    return image.crop(image.getchannel("A").getbbox())


def beside_knight(sheet, names):
    keyed = clear_pink(key_magenta(Image.open(os.path.join(FOREST, sheet))))
    knight, *shops = sorted(blobs(keyed)[:1 + len(names)], key=lambda b: b[1][0])
    knight_box = isolate(keyed, knight[1], knight[2]).getchannel("A").getbbox()
    scale = KNIGHT_SCALE * KNIGHT_HEIGHT / (knight_box[3] - knight_box[1])
    return {name: trimmed(shrink(isolate(keyed, box, cells), scale)) for name, (_, box, cells) in zip(names, shops) if name}


def houses():
    keyed = key_pure_magenta(Image.open(os.path.join(FOREST, "prop-shophouse-01.png")))
    step = keyed.width / len(HOUSES)
    out = {}
    for index, name in enumerate(HOUSES):
        column = keyed.crop((round(index * step), 0, round((index + 1) * step), keyed.height))
        out[name] = trimmed(shrink(main_item(column), HOUSE_SCALE))
    return out


def main():
    sprites = houses()
    for sheet, names in BESIDE_KNIGHT:
        sprites.update(beside_knight(sheet, names))
    os.makedirs(OUT, exist_ok=True)
    for name, image in sorted(sprites.items()):
        image.save(os.path.join(OUT, name + ".png"))
        print(name, image.size)


if __name__ == "__main__":
    main()
