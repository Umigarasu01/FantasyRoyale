"""Cut the generated forest sprite sheets into native-scale parts.

Usage:
    python build_forest_parts.py

Reads Assets/Art/Generated/ForestLook/*.png and writes Parts/*.png with parts.json.
The sheets are read only. Scaling and keying are deterministic.
"""
import hashlib
import json
import os

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
TARGET = os.path.join(SOURCE, "Parts")

TREE_SCALE = 0.375
# Sizes follow the design panels, where ferns and flowers are smaller than bushes.
UNDERSTORY_SCALES = (
    ("fern", 0.38), ("broadleaf", 0.38), ("bush", 0.46), ("flower", 0.3), ("boulder", 0.42),
    ("stones", 0.34), ("pebbles", 0.34), ("tallgrass", 0.36), ("tuft", 0.34),
)


FEATURE_WIDTHS = {"feature-spring": 320, "feature-herbs": 256, "feature-nest": 256, "feature-cliff": 384}


def part_scale(name, default):
    return next((scale for prefix, scale in UNDERSTORY_SCALES if name.startswith(prefix)), default)

SHEETS = {
    "forest-trees-02.png": {
        "columns": 3,
        "rows": 2,
        "scale": TREE_SCALE,
        "names": ["tree-oak", "tree-upright", "tree-split", "tree-young", "tree-wide", "tree-dense"],
    },
    "forest-features-01.png": {
        "columns": 2,
        "rows": 2,
        "scale": 0.5,
        "names": ["feature-spring", "feature-herbs", "feature-nest", "feature-cliff"],
    },
    "forest-understory-01.png": {
        "columns": 6,
        "rows": 5,
        "scale": 0.5,
        "names": [
            "fern-a", "broadleaf-a", "fern-b", "broadleaf-b", "bush-a", "bush-b",
            "bush-c", "bush-d", "bush-e", "bush-f", "bush-g", "bush-h",
            "flower-white-a", "flower-yellow-a", "flower-pink-a", "flower-white-b", "flower-yellow-b", "flower-pink-b",
            "boulder-a", "boulder-b", "boulder-c", "stones-a", "pebbles-a", "pebbles-b",
            "tallgrass-a", "tallgrass-b", "tallgrass-c", "tuft-a", "tuft-b", "tuft-c",
        ],
    },
}


def key_magenta(image):
    """Remove the flat background and any fringe that still leans toward it."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, _ = pixels[x, y]
            if r > 150 and b > 130 and g < min(r, b) * 0.62:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def bands(values, gap):
    """Return (start, end) runs of truthy values, joining runs closer than gap."""
    runs = []
    start = last = None
    for index, value in enumerate(values):
        if value:
            if start is None:
                start = index
            elif index - last > gap:
                runs.append((start, last + 1))
                start = index
            last = index
    if start is not None:
        runs.append((start, last + 1))
    return runs


def find_items(keyed):
    """Find items row by row from the empty background gaps, in reading order."""
    alpha = keyed.getchannel("A")
    width, height = keyed.size
    data = alpha.tobytes()
    rows = [any(data[y * width:(y + 1) * width]) for y in range(height)]
    boxes = []
    for top, bottom in bands(rows, 6):
        if bottom - top < 12:
            continue
        strip = alpha.crop((0, top, width, bottom)).tobytes()
        strip_height = bottom - top
        columns = [any(strip[y * width + x] for y in range(strip_height)) for x in range(width)]
        for left, right in bands(columns, 34):
            box = alpha.crop((left, top, right, bottom)).getbbox()
            boxes.append((left + box[0], top + box[1], left + box[2], top + box[3]))
    return boxes


def shrink(part, scale):
    width, height = max(1, round(part.width * scale)), max(1, round(part.height * scale))
    colour = part.convert("RGB").resize((width, height), Image.BOX)
    alpha = part.getchannel("A").resize((width, height), Image.BOX).point(lambda v: 255 if v >= 150 else 0)
    colour.putalpha(alpha)
    return colour


def main():
    os.makedirs(TARGET, exist_ok=True)
    manifest = {}
    for sheet_name, sheet in SHEETS.items():
        path = os.path.join(SOURCE, sheet_name)
        keyed = key_magenta(Image.open(path))
        boxes = find_items(keyed)
        if len(boxes) != len(sheet["names"]):
            raise ValueError(f"{sheet_name}: found {len(boxes)} items, expected {len(sheet['names'])}")
        for name, box in zip(sheet["names"], boxes):
            cell = keyed
            scale = part_scale(name, sheet["scale"])
            if name in FEATURE_WIDTHS:
                scale = FEATURE_WIDTHS[name] / (box[2] - box[0])
            part = shrink(cell.crop(box), scale)
            target = os.path.join(TARGET, name + ".png")
            part.save(target)
            manifest[name] = {
                "file": name + ".png",
                "size": list(part.size),
                "pivot": [part.width // 2, 0],
                "source": sheet_name,
                "sha256": hashlib.sha256(open(target, "rb").read()).hexdigest(),
            }
    with open(os.path.join(TARGET, "parts.json"), "w", encoding="utf-8") as file:
        json.dump(manifest, file, ensure_ascii=False, indent=2)
    print("parts", len(manifest))


if __name__ == "__main__":
    main()
