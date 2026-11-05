"""Build the fixed forest base map from the layout, the design image and generated parts.

Usage:
    python build_forest_map.py <output-directory> [--area x0 x1 y0 y1]

Writes ground tiles, objects.json, collision.json, routes.json, Parts/ and preview images.
With --area, only that part of the map is built, for quick checks.
Inputs are read only. Output is deterministic for SEED.
"""
import json
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

import forest_layout as L
import forest_texture as T

Image.MAX_IMAGE_PIXELS = None

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOREST = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
PARTS = os.path.join(FOREST, "Parts")
NATIVE = os.path.join(ROOT, "Assets", "NemoBlocks", "NativeTextures")
LIBRARY = os.path.join(ROOT, "Assets", "NemoBlocks", "NativeLibrary", "Library.asset")
INPUTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Inputs")

SEED = 20261105
PPU = 64
TILE = 2304
SMALL = 8
RIVER_WIDTH = 184
FOREST_FLOOR = (14, 56, 52)
TREE_SPACING = 84
FEATURES = {
    "natural-spring": "feature-spring", "herb-bed": "feature-herbs", "monster-nest": "feature-nest",
    "cliff-face-small": "feature-cliff",
}
FEATURE_FOOT = {"natural-spring": 14, "cliff-face-small": 12}

if "--area" in sys.argv:
    index = sys.argv.index("--area")
    X0, X1, Y0, Y1 = (float(v) for v in sys.argv[index + 1:index + 5])
else:
    X0, X1, Y0, Y1 = 0.0, L.MAP_WIDTH, 0.0, L.MAP_HEIGHT
W, H = int((X1 - X0) * PPU), int((Y1 - Y0) * PPU)
AREA_SCALE = W * H / (L.MAP_WIDTH * L.MAP_HEIGHT * PPU * PPU)

rng = random.Random(SEED)


def to_px(x, y):
    return (x - X0) * PPU, (Y1 - y) * PPU


def inside(x, y, margin=0):
    return margin <= x < W - margin and margin <= y < H - margin


def noise(period, seed, size=(W, H)):
    local = random.Random(seed)
    w, h = size[0] // period + 3, size[1] // period + 3
    small = Image.frombytes("L", (w, h), local.randbytes(w * h))
    return small.resize(((w - 1) * period, (h - 1) * period), Image.BICUBIC).crop((0, 0) + size)


class Field:
    """A mask kept at a fraction of the map size."""

    def __init__(self, image, scale):
        self.pixels = image.load()
        self.width, self.height = image.size
        self.scale = scale

    def __getitem__(self, point):
        x, y = int(point[0]) // self.scale, int(point[1]) // self.scale
        return self.pixels[min(self.width - 1, max(0, x)), min(self.height - 1, max(0, y))]


def is_water(r, g, b):
    return b > g * 1.05 and b > r * 1.35


def stroke(points, width):
    mask = Image.new("L", (W, H))
    draw = ImageDraw.Draw(mask)
    pixels = [to_px(x, y) for x, y in points]
    draw.line(pixels, fill=255, width=int(width))
    radius = width / 2
    for x, y in pixels:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=255)
    return mask


def ragged(mask, blur, amount, seed, periods=(38, 11)):
    """Bend a mask's outline with noise and return it as a hard mask."""
    field = mask.filter(ImageFilter.GaussianBlur(blur))
    edge = Image.blend(noise(periods[0], seed), noise(periods[1], seed + 1), 0.4)
    bent = ImageChops.add(field, edge.point(lambda v: int((v - 128) * amount + 128)), 1, -128)
    return bent.point(lambda v: 255 if v > 127 else 0)


def quilt(source, size, patch, seed, valid):
    """Cover size with soft-edged patches taken from the valid corners of source."""
    local = random.Random(seed)
    canvas = Image.new("RGB", size)
    masks = []
    for index in range(6):
        field = Image.new("L", (patch, patch))
        ImageDraw.Draw(field).ellipse((4, 4, patch - 4, patch - 4), fill=255)
        field = field.filter(ImageFilter.GaussianBlur(patch / 10))
        grain = noise(max(4, patch // 12), seed + 100 + index, (patch, patch))
        hard = ImageChops.add(field, grain.point(lambda v: int((v - 128) * 1.2 + 128)), 1, -128).point(lambda v: 255 if v > 118 else 0)
        masks.append(hard.filter(ImageFilter.GaussianBlur(3)))
    for y in range(0, size[1], patch):
        for x in range(0, size[0], patch):
            sx, sy = local.choice(valid)
            canvas.paste(source.crop((sx, sy, sx + patch, sy + patch)), (x, y))
    step = patch * 3 // 4
    for y in range(-patch // 2, size[1], step):
        for x in range(-patch // 2, size[0], step):
            sx, sy = local.choice(valid)
            canvas.paste(source.crop((sx, sy, sx + patch, sy + patch)), (x, y), local.choice(masks))
    return canvas


def water_source():
    """Open water from the design's river panel, and the corners that stay inside it."""
    design = Image.open(T.DESIGN).convert("RGB").crop((1010, 80, 1320, 520))
    size = (round(design.width * T.DESIGN_SCALE), round(design.height * T.DESIGN_SCALE))
    image = design.resize(size, Image.LANCZOS)
    pixels = image.load()
    mask = Image.new("L", size)
    mask_px = mask.load()
    for y in range(size[1]):
        for x in range(size[0]):
            if is_water(*pixels[x, y]):
                mask_px[x, y] = 255
    solid = mask.filter(ImageFilter.MedianFilter(7))
    patch = 64
    for _ in range(patch // 8 + 2):
        solid = solid.filter(ImageFilter.MinFilter(9))
    solid_px = solid.load()
    half = patch // 2
    valid = [
        (x - half, y - half)
        for y in range(half, size[1] - half, 3)
        for x in range(half, size[0] - half, 3)
        if solid_px[x, y]
    ]
    print("water corners", len(valid))
    return image, valid, patch


# --- Banks ---------------------------------------------------------------------------------
BANK_WATER = 22
BANK_LAND = 66


def bank_strips():
    """Straighten the design's two river banks into strips.

    Rows run along the river. Column BANK_WATER is the shoreline, and larger columns go inland.
    The shoreline is smoothed before straightening, so the bank keeps its own lobes.
    """
    design = Image.open(T.DESIGN).convert("RGB").crop(T.PANELS[1])
    size = (round(design.width * T.DESIGN_SCALE), round(design.height * T.DESIGN_SCALE))
    panel = design.resize(size, Image.LANCZOS)
    pixels = panel.load()
    mask = Image.new("L", size)
    mask_px = mask.load()
    for y in range(size[1]):
        for x in range(size[0]):
            if is_water(*pixels[x, y]):
                mask_px[x, y] = 255
    mask_px = mask.filter(ImageFilter.MedianFilter(5)).load()
    lefts, rights = [], []
    for y in range(size[1]):
        best = (0, 0, 0)
        run = None
        for x in range(size[0] + 1):
            on = x < size[0] and mask_px[x, y]
            if on and run is None:
                run = x
            elif not on and run is not None:
                if x - run > best[0]:
                    best = (x - run, run, x - 1)
                run = None
        lefts.append(best[1])
        rights.append(best[2])

    def smooth(values):
        return [sum(values[max(0, i - 26):i + 27]) / len(values[max(0, i - 26):i + 27]) for i in range(len(values))]

    grain = random.Random(SEED + 80)
    strips = []
    for line, direction in ((smooth(lefts), -1), (smooth(rights), 1)):
        strip = Image.new("RGBA", (BANK_WATER + BANK_LAND, size[1]), (0, 0, 0, 0))
        strip_px = strip.load()
        for y in range(size[1]):
            for column in range(strip.width):
                t = column - BANK_WATER
                x = int(line[y] + direction * t)
                if not 0 <= x < size[0]:
                    continue
                r, g, b = pixels[x, y]
                bright = max(r, g, b) > 196
                if is_water(r, g, b) and not bright:
                    continue
                grass = g > r * 1.03 and g > b * 1.2
                if grass and t > 26 + grain.randrange(26):
                    continue
                # Boulders are cut by the strip's edge, so stones come from the object layer.
                if t > 8 and max(r, g, b) - min(r, g, b) < 44 and max(r, g, b) > 70:
                    continue
                # Reed roots standing in the water would stretch into brown spikes.
                if t < -4 and r > b * 1.3:
                    continue
                strip_px[column, y] = (r, g, b, 255)
        strips.append(strip)
        strips.append(strip.transpose(Image.FLIP_TOP_BOTTOM))
    print("bank strips", len(strips), strips[0].size)
    return strips


def resample(points, step):
    """Return points along a polyline at an even spacing, with their unit normals."""
    result = []
    carry = 0.0
    for a, b in zip(points, points[1:]):
        length = math.dist(a, b)
        distance = carry
        while distance < length:
            t = distance / length
            result.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            distance += step
        carry = distance - length
    normals = []
    reach = int(14 / step)
    for index in range(len(result)):
        ax, ay = result[max(0, index - reach)]
        bx, by = result[min(len(result) - 1, index + reach)]
        length = math.hypot(bx - ax, by - ay) or 1.0
        normals.append((-(by - ay) / length, (bx - ax) / length))
    return result, normals


def draw_banks(ground, line, half_width, strips):
    """Bend random stretches of the strips along both sides of the river."""
    pixels = ground.load()
    step = 0.6
    centre, normals = resample([to_px(x, y) for x, y in line], step)
    data = [strip.load() for strip in strips]
    height = strips[0].height
    width = strips[0].width
    for side in (1, -1):
        index = 0
        while index < len(centre):
            length = int(rng.randint(150, 240) / step)
            which = rng.randrange(len(strips))
            offset = rng.randrange(0, max(1, height - int(length * step) - 4))
            source = data[which]
            ends = []
            end = rng.uniform(0, 18)
            for _ in range(width):
                end = min(18, max(0, end + rng.uniform(-3, 3)))
                ends.append(end)
            for k in range(min(length + int(18 / step), len(centre) - index)):
                i = index + k
                row = min(height - 1, offset + int(k * step))
                late = k * step - length * step
                cx, cy = centre[i]
                nx, ny = normals[i]
                bx, by = cx + nx * side * half_width, cy + ny * side * half_width
                for column in range(width):
                    if late > ends[column]:
                        continue
                    colour = source[column, row]
                    if colour[3]:
                        t = column - BANK_WATER
                        x, y = int(bx + nx * side * t), int(by + ny * side * t)
                        if 0 <= x < W and 0 <= y < H:
                            pixels[x, y] = colour
            index += length


# --- The design's bridge --------------------------------------------------------------------
BRIDGE_BOX = (176, 606, 590, 850)
BRIDGE_CENTRE = (382.5, 726.5)
BRIDGE_INNER = (
    (196, 664, 570, 790), (188, 620, 224, 676), (541, 620, 578, 676), (188, 770, 224, 836), (541, 770, 578, 836),
    (228, 644, 538, 666), (228, 790, 538, 814),
)


def design_bridge():
    """Cut the bridge from the design. Returns the whole bridge and its front rail, native size."""
    design = Image.open(T.DESIGN).convert("RGB").crop(BRIDGE_BOX)
    left, top = BRIDGE_BOX[0], BRIDGE_BOX[1]
    inner = Image.new("L", design.size)
    draw = ImageDraw.Draw(inner)
    for box in BRIDGE_INNER:
        draw.rectangle((box[0] - left, box[1] - top, box[2] - left, box[3] - top), fill=255)
    outline = design.convert("L").point(lambda v: 255 if v < 62 else 0)
    around = inner.filter(ImageFilter.MaxFilter(9))
    alpha = ImageChops.lighter(inner, ImageChops.multiply(outline, around))
    alpha = alpha.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))
    front = Image.new("L", design.size)
    draw = ImageDraw.Draw(front)
    draw.rectangle((0, 786 - top, design.width, design.height), fill=255)
    draw.rectangle((0, 766 - top, 228 - left, design.height), fill=255)
    draw.rectangle((538 - left, 766 - top, design.width, design.height), fill=255)
    size = (round(design.width * T.DESIGN_SCALE), round(design.height * T.DESIGN_SCALE))

    def finish(mask):
        image = design.resize(size, Image.LANCZOS)
        image.putalpha(mask.resize(size, Image.LANCZOS).point(lambda v: 255 if v > 127 else 0))
        return image

    pivot = (
        round((BRIDGE_CENTRE[0] - left) * T.DESIGN_SCALE),
        round((BRIDGE_CENTRE[1] - top) * T.DESIGN_SCALE),
    )
    return finish(alpha), finish(ImageChops.multiply(alpha, front)), pivot


# --- Facility art ---------------------------------------------------------------------------
def read_library():
    """Read prototype visuals, bounds and collision bodies from the Unity asset text."""
    prototypes = {}
    current = visual = section = list_name = None
    with open(LIBRARY, encoding="utf-8") as file:
        for raw in file:
            line = raw.rstrip()
            if line.startswith("  - id: "):
                current = {"id": line[8:].strip(), "visuals": [], "bounds": [], "bodies": []}
                prototypes[current["id"]] = current
                section = None
            elif current is None:
                continue
            elif line == "    visuals:":
                section = "visuals"
            elif line == "    bounds:":
                section = "bounds"
            elif line == "    bodies:":
                section = "bodies"
            elif section == "visuals":
                stripped = line.strip()
                if stripped.startswith("- path: "):
                    visual = {"path": stripped[8:], "rect": [], "color": []}
                    current["visuals"].append(visual)
                    list_name = None
                elif stripped in ("rect:", "color:"):
                    list_name = stripped[:-1]
                elif stripped.startswith("- ") and list_name:
                    visual[list_name].append(float(stripped[2:]))
                else:
                    key, _, value = stripped.partition(": ")
                    list_name = None
                    if key in ("x", "y", "pivotX", "pivotY"):
                        visual[key] = float(value)
                    elif key in ("flipX", "flipY", "order"):
                        visual[key] = int(value)
            elif section == "bounds" and line.strip().startswith("- "):
                current["bounds"].append(float(line.strip()[2:]))
            elif section == "bodies":
                stripped = line.strip()
                if stripped.startswith("- x: "):
                    current["bodies"].append({"x": float(stripped[5:])})
                elif ": " in stripped and current["bodies"]:
                    key, _, value = stripped.partition(": ")
                    if key in ("y", "width", "height"):
                        current["bodies"][-1][key] = float(value)
    return prototypes


def compose_prototype(prototype):
    """Flatten a prototype's visuals into one sprite, returning it with its foot pivot."""
    pieces = []
    for visual in sorted(prototype["visuals"], key=lambda item: item.get("order", 0)):
        path = os.path.join(ROOT, visual["path"].replace("/", os.sep))
        if not os.path.exists(path):
            continue
        with Image.open(path) as source:
            image = source.convert("RGBA")
        x, y, width, height = (int(v) for v in visual["rect"])
        piece = image.crop((x, image.height - y - height, x + width, image.height - y))
        if visual.get("flipX"):
            piece = piece.transpose(Image.FLIP_LEFT_RIGHT)
        if visual.get("flipY"):
            piece = piece.transpose(Image.FLIP_TOP_BOTTOM)
        colour = visual["color"]
        if len(colour) == 4 and colour != [1, 1, 1, 1]:
            bands = piece.split()
            piece = Image.merge("RGBA", tuple(band.point(lambda v, c=c: int(v * c)) for band, c in zip(bands, colour)))
        pieces.append((piece, visual["x"] * PPU - visual["pivotX"], visual["y"] * PPU - visual["pivotY"]))
    if not pieces:
        return None, None
    min_x = min(p[1] for p in pieces)
    max_x = max(p[1] + p[0].width for p in pieces)
    min_y = min(p[2] for p in pieces)
    max_y = max(p[2] + p[0].height for p in pieces)
    canvas = Image.new("RGBA", (int(max_x - min_x) + 1, int(max_y - min_y) + 1), (0, 0, 0, 0))
    for piece, left, bottom in pieces:
        canvas.alpha_composite(piece, (int(left - min_x), int(canvas.height - (bottom - min_y) - piece.height)))
    return canvas, (int(-min_x), int(-min_y))


def facility_overrides():
    """The six rebuilt facilities use their approved originals at native size."""
    result = {}
    for entry in json.load(open(os.path.join(INPUTS, "facilities.json"), encoding="utf-8")):
        with Image.open(os.path.join(INPUTS, entry["file"])) as source:
            image = source.convert("RGBA").crop(tuple(entry["frame"]))
        result[entry["id"]] = (image.resize(tuple(entry["size"]), Image.BOX), tuple(entry["pivot"]))
    return result


def grade(sprite, saturation, value):
    """Scale a sprite's saturation and brightness, keeping its alpha."""
    alpha = sprite.getchannel("A")
    h, s, v = sprite.convert("RGB").convert("HSV").split()
    s = s.point(lambda x: int(x * saturation))
    v = v.point(lambda x: int(x * value))
    result = Image.merge("HSV", (h, s, v)).convert("RGB")
    result.putalpha(alpha)
    return result


def load_parts():
    manifest = json.load(open(os.path.join(PARTS, "parts.json"), encoding="utf-8"))
    parts = {name: Image.open(os.path.join(PARTS, data["file"])).convert("RGBA") for name, data in manifest.items()}
    for name, sprite in parts.items():
        if name.startswith("tree"):
            parts[name] = grade(sprite, 0.96, 0.87)
        elif name.startswith(("bush", "fern", "broadleaf")):
            parts[name] = grade(sprite, 0.96, 0.92)
    return parts


class Spacing:
    def __init__(self, cell):
        self.cell, self.grid = cell, {}

    def free(self, x, y, distance):
        cx, cy = int(x // self.cell), int(y // self.cell)
        reach = int(distance // self.cell) + 1
        for i in range(-reach, reach + 1):
            for j in range(-reach, reach + 1):
                for a, b in self.grid.get((cx + i, cy + j), ()):
                    if (x - a) ** 2 + (y - b) ** 2 < distance ** 2:
                        return False
        return True

    def add(self, x, y):
        self.grid.setdefault((int(x // self.cell), int(y // self.cell)), []).append((x, y))


# --- Forest shape ---------------------------------------------------------------------------
def forest_shape(facilities, curves, river):
    """Decide the open ground on the whole map at low resolution. Returns the forest mask."""
    scale = PPU / SMALL
    width, height = int(L.MAP_WIDTH * scale), int(L.MAP_HEIGHT * scale)
    open_ground = Image.new("L", (width, height))
    draw = ImageDraw.Draw(open_ground)

    def px(point):
        return point[0] * scale, (L.MAP_HEIGHT - point[1]) * scale

    for cx, cy, rx, ry in L.CLEARINGS:
        a, b = px((cx - rx, cy + ry))
        c, d = px((cx + rx, cy - ry))
        draw.ellipse((a, b, c, d), fill=255)
    for curve in curves.values():
        draw.line([px(p) for p in curve], fill=255, width=int(7.0 * scale), joint="curve")
    draw.line([px(p) for p in river], fill=255, width=int(9.5 * scale), joint="curve")
    for left, bottom, right, top in facilities:
        a, b = px((left - 2.0, top + 1.0))
        c, d = px((right + 2.0, bottom - 2.5))
        draw.rectangle((a, b, c, d), fill=255)
    field = open_ground.filter(ImageFilter.GaussianBlur(scale * 1.6))
    wobble = Image.blend(noise(int(scale * 7), SEED + 50, (width, height)), noise(int(scale * 2.4), SEED + 51, (width, height)), 0.35)
    bent = ImageChops.add(field, wobble.point(lambda v: int((v - 128) * 1.0 + 128)), 1, -128)
    forest = bent.point(lambda v: 255 if v < 118 else 0).filter(ImageFilter.MedianFilter(7))
    left, top = int(X0 * scale), int((L.MAP_HEIGHT - Y1) * scale)
    crop = forest.crop((left, top, left + W // SMALL, top + H // SMALL))
    smooth = crop.resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(5))
    return crop, smooth.point(lambda v: 255 if v > 127 else 0)


def build_collision(blocked, bridges, solids):
    """Merge the blocked mask into rectangles, leaving the bridge decks open."""
    cell = 16
    columns, rows = W // cell, H // cell
    small = blocked.resize((columns, rows), Image.NEAREST).load()
    decks = [(bx - 150, by - 62, bx + 150, by + 66) for bx, by in bridges]

    def closed(column, row):
        x, y = column * cell + cell / 2, row * cell + cell / 2
        return small[column, row] > 127 and not any(d[0] <= x <= d[2] and d[1] <= y <= d[3] for d in decks)

    spans = {}
    rects = []
    for row in range(rows):
        current = {}
        column = 0
        while column < columns:
            if not closed(column, row):
                column += 1
                continue
            start = column
            while column < columns and closed(column, row):
                column += 1
            key = (start, column)
            current[key] = spans.pop(key, row)
        for (start, end), top in spans.items():
            rects.append([start * cell, top * cell, (end - start) * cell, (row - top) * cell])
        spans = current
    for (start, end), top in spans.items():
        rects.append([start * cell, top * cell, (end - start) * cell, (rows - top) * cell])
    rails = []
    for bx, by in bridges:
        rails += [[int(bx - 136), int(by - 62), 272, 14], [int(bx - 136), int(by + 52), 272, 14]]
    print("merged rectangles", len(rects), "solids", len(solids))
    return {"rects": [{"x": r[0], "y": r[1], "w": r[2], "h": r[3]} for r in rects + rails + solids]}


def check_walkable(rects, curves, fronts):
    """Report road points that touch something solid, and facilities that cannot be reached."""
    cell, grow = 16, 19
    columns, rows = W // cell, H // cell
    blocked = bytearray(columns * rows)
    for rect in rects:
        x0, x1 = max(0, int((rect["x"] - grow) // cell)), min(columns - 1, int((rect["x"] + rect["w"] + grow) // cell))
        y0, y1 = max(0, int((rect["y"] - grow) // cell)), min(rows - 1, int((rect["y"] + rect["h"] + grow) // cell))
        if x1 < x0:
            continue
        for row in range(y0, y1 + 1):
            blocked[row * columns + x0:row * columns + x1 + 1] = b"\x01" * (x1 - x0 + 1)

    def to_cell(point):
        x, y = to_px(*point)
        return min(columns - 1, max(0, int(x // cell))), min(rows - 1, max(0, int(y // cell)))

    conflicts = []
    for name, curve in curves.items():
        points = [p for p in curve if inside(*to_px(*p))]
        hits = [p for p in points if blocked[to_cell(p)[1] * columns + to_cell(p)[0]]]
        if hits:
            conflicts.append({"name": name, "points": len(hits), "total": len(points), "first": [round(v, 1) for v in hits[0]]})
    start = to_cell(next(p for curve in curves.values() for p in curve if inside(*to_px(*p))))
    seen = bytearray(columns * rows)
    stack = [start]
    seen[start[1] * columns + start[0]] = 1
    while stack:
        c, r = stack.pop()
        for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nc, nr = c + dc, r + dr
            if 0 <= nc < columns and 0 <= nr < rows:
                index = nr * columns + nc
                if not seen[index] and not blocked[index]:
                    seen[index] = 1
                    stack.append((nc, nr))
    unreachable = []
    for name, point in fronts:
        if not inside(*to_px(*point)):
            continue
        c, r = to_cell(point)
        near = any(
            seen[(r + j) * columns + c + i]
            for i in range(-6, 7) for j in range(-6, 7)
            if 0 <= c + i < columns and 0 <= r + j < rows
        )
        if not near:
            unreachable.append(name)

    def is_free(point):
        if not inside(*to_px(*point), 8):
            return False
        c, r = to_cell(point)
        return bool(seen[r * columns + c])

    return conflicts, unreachable, is_free


def blocked_tests(curves, river, core_field, trees, is_free):
    """Pairs of points for Unity: the player must not get from one to the other."""
    tests = []
    for index in range(20, len(river) - 20, 26):
        x, y = river[index]
        ax, ay = river[index - 6]
        bx, by = river[index + 6]
        length = math.hypot(bx - ax, by - ay) or 1.0
        nx, ny = -(by - ay) / length, (bx - ax) / length
        start, goal = (x + nx * 5.0, y + ny * 5.0), (x - nx * 5.0, y - ny * 5.0)
        if min(math.dist((x, y), bridge) for bridge in L.BRIDGES) > 7 and is_free(start) and is_free(goal):
            tests.append({"name": f"river-{index}", "start": list(start), "goal": list(goal), "seconds": 4.0})
    forest = 0
    for name, curve in curves.items():
        for index in range(12, len(curve) - 12, 70):
            x, y = curve[index]
            ax, ay = curve[index - 4]
            bx, by = curve[index + 4]
            length = math.hypot(bx - ax, by - ay) or 1.0
            nx, ny = -(by - ay) / length, (bx - ax) / length
            for side in (1, -1):
                for reach in range(4, 15):
                    point = (x + nx * side * reach, y + ny * side * reach)
                    if not inside(*to_px(*point), 8):
                        break
                    if core_field[to_px(*point)]:
                        goal = (point[0] + nx * side * 3, point[1] + ny * side * 3)
                        if forest < 14 and is_free((x, y)):
                            tests.append({"name": f"forest-{name}-{index}", "start": [x, y], "goal": list(goal), "seconds": 5.0})
                            forest += 1
                        break
    picked = 0
    for x, y in trees:
        world = (X0 + x / PPU, Y1 - y / PPU)
        start = (world[0], world[1] - 1.4)
        if picked < 6 and is_free(start):
            tests.append({"name": f"trunk-{picked}", "start": list(start), "goal": [world[0], world[1] + 0.12], "seconds": 2.0})
            picked += 1
    return tests


def draw_sprite(canvas, sprite, foot_x, foot_y, pivot=None):
    """Draw a sprite by its foot position, clipped to the canvas."""
    pivot_x, pivot_y = pivot if pivot else (sprite.width // 2, 0)
    left, top = foot_x - pivot_x, foot_y - sprite.height + pivot_y
    x0, y0 = max(0, left), max(0, top)
    x1, y1 = min(canvas.width, left + sprite.width), min(canvas.height, top + sprite.height)
    if x0 < x1 and y0 < y1:
        canvas.alpha_composite(sprite.crop((x0 - left, y0 - top, x1 - left, y1 - top)), (x0, y0))


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    print("area", X0, X1, Y0, Y1, "size", W, H)
    parts = load_parts()
    library = read_library()
    overrides = facility_overrides()
    curves = {name: L.catmull_rom(points) for name, points in L.ROADS.items()}
    river = L.catmull_rom(L.RIVER)
    bridges = [to_px(*bridge) for bridge in L.BRIDGES]

    # Facility sprites and footprints.
    sprite_pivots = {}
    bounds = []
    solids = []
    fronts = []
    for proto, fx, fy in L.FACILITIES:
        prototype = library[proto]
        name = "proto-" + proto
        if name not in parts:
            sprite, pivot = overrides.get(proto, (None, None))
            feature = FEATURES.get(proto)
            if feature:
                sprite = parts[feature]
                pivot = (sprite.width // 2, FEATURE_FOOT.get(proto, 8))
            if sprite is None:
                sprite, pivot = compose_prototype(prototype)
            if sprite is None:
                raise ValueError("no art for " + proto)
            parts[name] = sprite
            sprite_pivots[name] = list(pivot)
        left, bottom, width, height = prototype["bounds"]
        bounds.append((fx + left, fy + bottom, fx + left + width, fy + bottom + height))
        fronts.append((proto, (fx, fy - 1.0)))
        for body in prototype["bodies"]:
            a, b = to_px(fx + body["x"], fy + body["y"] + body["height"])
            solids.append([int(a), int(b), int(body["width"] * PPU), int(body["height"] * PPU)])

    # Masks.
    forest_small, forest = forest_shape(bounds, curves, river)
    water = stroke(river, RIVER_WIDTH + 14)
    roads = Image.new("L", (W, H))
    for curve in curves.values():
        roads = ImageChops.lighter(roads, stroke(curve, L.ROAD_WIDTH * PPU))
    draw = ImageDraw.Draw(roads)
    for cx, cy, rx, ry in L.PLAZAS:
        a, b = to_px(cx - rx, cy + ry)
        c, d = to_px(cx + rx, cy - ry)
        draw.ellipse((a, b, c, d), fill=255)
    for bx, by in bridges:
        for cx in (bx - 215, bx + 215):
            draw.ellipse((cx - 110, by - 80, cx + 110, by + 80), fill=255)
    spots = ImageChops.multiply(
        noise(20, SEED + 70).point(lambda v: 255 if v > 200 else 0),
        noise(150, SEED + 71).point(lambda v: 255 if v > 146 else 0),
    )
    roads = ImageChops.lighter(roads, ImageChops.subtract(spots, forest))
    soil = ragged(roads, 11, 1.05, SEED + 3, (54, 17))
    islands = noise(22, SEED + 72).point(lambda v: 255 if v > 214 else 0)
    soil = ImageChops.subtract(ImageChops.subtract(soil, islands), water)

    small_size = (W // SMALL, H // SMALL)
    core_small = forest_small.filter(ImageFilter.MinFilter(11))
    core = core_small.resize((W, H), Image.NEAREST)
    deep = forest_small.filter(ImageFilter.MinFilter(25)).resize((W, H), Image.NEAREST)
    water_small = water.resize(small_size, Image.NEAREST)
    water_inner = water_small.filter(ImageFilter.MinFilter(15)).resize((W, H), Image.NEAREST)
    skip = ImageChops.lighter(deep, water_inner)

    # Objects.
    road_keep = Field(soil.resize((W // 4, H // 4), Image.BOX).point(lambda v: 255 if v > 20 else 0).filter(ImageFilter.MaxFilter(5)), 4)
    water_keep_small = water_small.filter(ImageFilter.MaxFilter(13))
    water_keep = Field(water_keep_small, SMALL)
    open_keep = Image.new("L", small_size)
    draw = ImageDraw.Draw(open_keep)
    for left, bottom, right, top in bounds:
        a, b = to_px(left - 0.4, top + 0.2)
        c, d = to_px(right + 0.4, bottom - 0.6)
        draw.rectangle((a / SMALL, b / SMALL, c / SMALL, d / SMALL), fill=255)
    for bx, by in bridges:
        draw.rectangle(((bx - 200) / SMALL, (by - 130) / SMALL, (bx + 200) / SMALL, (by + 130) / SMALL), fill=255)
    facility_keep = Field(open_keep, SMALL)
    forest_field = Field(forest_small, SMALL)
    core_field = Field(core_small, SMALL)

    objects = []

    def add(name, x, y, kind, sort=None):
        objects.append({"part": name, "x": int(x), "y": int(y), "sort": int(y if sort is None else sort), "kind": kind})

    for proto, fx, fy in L.FACILITIES:
        x, y = to_px(fx, fy)
        if -500 < x < W + 500 and -100 < y < H + 600:
            add("proto-" + proto, x, y, "facility")

    # Trees: a jittered grid fills the forest, and groves share a tone.
    grove = noise(280, SEED + 60).load()
    dark_trees = ("tree-dense", "tree-wide", "tree-dense", "tree-split")
    light_trees = ("tree-oak", "tree-upright", "tree-split", "tree-oak")
    mixed_trees = ("tree-oak", "tree-dense", "tree-upright", "tree-wide", "tree-split", "tree-young")
    row = 0
    planted = 0
    y = -TREE_SPACING
    while y < H + 200:
        x = -TREE_SPACING + (TREE_SPACING // 2 if row % 2 else 0)
        while x < W + TREE_SPACING:
            fx, fy = x + rng.randint(-24, 24), y + rng.randint(-20, 20)
            if forest_field[fx, fy] and forest_field[fx, fy - 118] and not facility_keep[fx, fy] and not water_keep[fx, fy] and rng.random() > 0.05:
                value = grove[min(W - 1, max(0, fx)), min(H - 1, max(0, fy))]
                table = dark_trees if value < 112 else light_trees if value > 146 else mixed_trees
                add(rng.choice(table), fx, fy, "tree")
                if not core_field[fx, fy]:
                    solids.append([fx - 16, fy - 18, 32, 19])
                planted += 1
            x += TREE_SPACING
        y += int(TREE_SPACING * 0.86)
        row += 1
    print("trees", planted)

    # Ground: the design's grass and soil, woven along the road shapes.
    tone = Image.blend(noise(170, SEED + 5), noise(70, SEED + 6), 0.4).point(lambda v: min(255, max(0, int((v - 128) * 2.0 + 128))))
    groups = noise(240, SEED + 7).point(lambda v: 255 if v > 136 else 0)
    detail = groups.point(lambda v: 46 if v else 14)
    near_forest = forest.resize(small_size, Image.BOX).filter(ImageFilter.MaxFilter(21)).filter(ImageFilter.GaussianBlur(4)).resize((W, H), Image.BILINEAR)
    detail = ImageChops.lighter(detail, near_forest.point(lambda v: int(v * 0.3)))
    atlas = T.Atlas()
    buckets = T.index_atlas(atlas)
    print("patches available", {k[0] if len(k) == 1 else k: len(v) for k, v in buckets.items() if k[0] != "edge"})
    ground = T.synthesise(atlas, buckets, soil, tone, detail, skip, SEED + 30)

    # Forest floor and the shade around it.
    soft = forest.resize(small_size, Image.BOX).filter(ImageFilter.GaussianBlur(6)).resize((W, H), Image.BICUBIC)
    light = soft.point(lambda v: 255 - int(min(v, 235) * 0.42))
    ground = ImageChops.multiply(ground, Image.merge("RGB", (light.point(lambda v: int(v * 0.96)), light, light.point(lambda v: min(255, int(v * 1.02))))))
    floor_tone = noise(9, SEED + 8).point(lambda v: int(205 + v * 0.2))
    floor = ImageChops.multiply(Image.new("RGB", (W, H), FOREST_FLOOR), Image.merge("RGB", (floor_tone, floor_tone, floor_tone)))
    crowns = Image.new("L", (W, H))
    draw = ImageDraw.Draw(crowns)
    for item in objects:
        if item["kind"] == "tree":
            draw.ellipse((item["x"] - 68, item["y"] - 126, item["x"] + 68, item["y"] - 8), fill=255)
    floor_mask = ImageChops.lighter(ragged(crowns, 4, 0.8, SEED + 9, (16, 5)), deep)
    ground.paste(floor, (0, 0), floor_mask)

    # Water, banks and bridges.
    under = water
    for _ in range(2):
        under = under.filter(ImageFilter.MaxFilter(7))
    ground.paste(Image.new("RGB", (W, H), (92, 58, 32)), (0, 0), under)
    source, valid, patch = water_source()
    ground.paste(quilt(source, (W, H), patch, SEED + 31, valid), (0, 0), water)
    ground = ground.convert("RGBA")
    draw_banks(ground, river, RIVER_WIDTH / 2, bank_strips())

    bridge, bridge_front, pivot = design_bridge()
    parts["bridge-front"] = bridge_front
    sprite_pivots["bridge-front"] = [pivot[0], bridge_front.height - pivot[1]]
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)
    for bx, by in bridges:
        shadow_draw.rectangle((bx - 132, by + 58, bx + 132, by + 86), fill=(6, 28, 58, 150))
    ground.alpha_composite(Image.composite(shadow, Image.new("RGBA", (W, H), (0, 0, 0, 0)), water))
    for bx, by in bridges:
        ground.alpha_composite(bridge, (int(bx) - pivot[0], int(by) - pivot[1])) if inside(bx - pivot[0], by - pivot[1]) and inside(bx + 170, by + 110) else None

    # Forest edge: dense low growth along the outside of the tree line.
    band_small = ImageChops.subtract(forest_small.filter(ImageFilter.MaxFilter(13)), forest_small.filter(ImageFilter.MinFilter(5)))
    band = band_small.tobytes()
    band_width = band_small.size[0]
    edge_points = [((i % band_width) * SMALL + 4, (i // band_width) * SMALL + 4) for i in range(len(band)) if band[i]]
    rng.shuffle(edge_points)
    plants = Spacing(32)
    gate = noise(100, SEED + 21).load()
    edge_table = (
        ["fern-a", "fern-b", "broadleaf-a", "broadleaf-b"] * 4
        + ["bush-a", "bush-b", "bush-c", "bush-d", "bush-e", "bush-f", "bush-g", "bush-h"] * 3
        + ["flower-white-a", "flower-yellow-a", "flower-pink-a", "flower-white-b", "flower-yellow-b", "flower-pink-b"]
        + ["tallgrass-a", "tallgrass-c", "boulder-a", "boulder-b", "boulder-c"]
    )
    placed = 0
    for x, y in edge_points:
        x, y = x + rng.randint(-3, 3), y + rng.randint(-3, 3)
        inside_map = min(W - 1, max(0, x)), min(H - 1, max(0, y))
        if gate[inside_map] < 112 or road_keep[x, y] or water_keep[x, y] or facility_keep[x, y] or not plants.free(x, y, 42):
            continue
        add(rng.choice(edge_table), x, y, "plant")
        plants.add(x, y)
        placed += 1
    print("edge plants", placed)

    # River bank: reeds and stones close to the water.
    reeds = next(os.path.join(NATIVE, n) for n in sorted(os.listdir(NATIVE)) if n.endswith("shore_reeds.png"))
    parts["shore-reeds"] = Image.open(reeds).convert("RGBA")
    bank_small = ImageChops.subtract(water_keep_small, water_small.filter(ImageFilter.MaxFilter(5)))
    bank = bank_small.tobytes()
    bank_width = bank_small.size[0]
    bank_points = [((i % bank_width) * SMALL + 4, (i // bank_width) * SMALL + 4) for i in range(len(bank)) if bank[i]]
    rng.shuffle(bank_points)
    bank_table = ["shore-reeds", "shore-reeds", "tallgrass-a", "tallgrass-c", "fern-b", "boulder-b", "boulder-a", "stones-a", "flower-white-a", "flower-yellow-a"]
    placed = 0
    for x, y in bank_points:
        if road_keep[x, y] or facility_keep[x, y] or not plants.free(x, y, 96):
            continue
        add(rng.choice(bank_table), x, y, "plant")
        plants.add(x, y)
        placed += 1
    print("bank plants", placed)

    # Open ground: a few small flowers and stones, in loose groups.
    cluster = noise(110, SEED + 20).load()
    open_table = ["flower-white-a", "flower-yellow-a", "flower-pink-a", "flower-white-b", "pebbles-a", "pebbles-b", "stones-a", "tuft-b", "tuft-c"]
    placed = 0
    for _ in range(int(17000 * AREA_SCALE * 9)):
        x, y = rng.randrange(0, W), rng.randrange(0, H)
        if cluster[x, y] < 154 or forest_field[x, y] or water_keep[x, y] or road_keep[x, y] or facility_keep[x, y] or not plants.free(x, y, 72):
            continue
        add(rng.choice(open_table), x, y, "plant")
        plants.add(x, y)
        placed += 1
    print("open plants", placed)

    for bx, by in bridges:
        if inside(bx, by):
            add("bridge-front", bx, by, "bridge-front", by + 58)

    # Solid objects.
    for item in objects:
        if item["part"].startswith("boulder"):
            width = int(parts[item["part"]].width * 0.8)
            solids.append([item["x"] - width // 2, item["y"] - 26, width, 26])

    # Save the fixed layers.
    ground = ground.convert("RGB")
    tiles = []
    for top in range(0, H, TILE):
        for left in range(0, W, TILE):
            name = f"ground-{top // TILE}-{left // TILE}.png"
            ground.crop((left, top, min(left + TILE, W), min(top + TILE, H))).save(os.path.join(out, name))
            tiles.append({"file": name, "x": left, "y": top})
    objects.sort(key=lambda item: (item["sort"], item["x"]))
    parts_dir = os.path.join(out, "Parts")
    os.makedirs(parts_dir, exist_ok=True)
    parts["hero_idle"] = Image.open(next(os.path.join(NATIVE, n) for n in sorted(os.listdir(NATIVE)) if n.endswith("hero_idle.png"))).convert("RGBA")
    sprites = []
    for name in sorted({item["part"] for item in objects} | {"hero_idle"}):
        sprite = parts[name]
        sprite.save(os.path.join(parts_dir, name + ".png"))
        pivot_px = sprite_pivots.get(name, [sprite.width // 2, 0])
        sprites.append({"name": name, "pivotX": pivot_px[0] / sprite.width, "pivotY": pivot_px[1] / sprite.height})
    blocked = ImageChops.lighter(water, core)
    collision = build_collision(blocked, bridges, solids)
    conflicts, unreachable, is_free = check_walkable(collision["rects"], curves, fronts)
    edge_trees = [(i["x"], i["y"]) for i in objects if i["kind"] == "tree" and not core_field[i["x"], i["y"]] and inside(i["x"], i["y"], 40)]
    rng.shuffle(edge_trees)
    tests = blocked_tests(curves, river, core_field, edge_trees, is_free)
    print("blocked tests", len(tests))
    print("road conflicts", conflicts)
    print("unreachable facilities", unreachable)
    routes = []
    for name, curve in curves.items():
        points = [p for p in curve if inside(*to_px(*p), 4)]
        if len(points) > 3:
            flat = []
            for point in points[::3] + [points[-1]]:
                flat += [round(point[0], 3), round(point[1], 3)]
            routes.append({"name": name, "flat": flat})
    with open(os.path.join(out, "collision.json"), "w", encoding="utf-8") as file:
        json.dump(collision, file, ensure_ascii=False)
    with open(os.path.join(out, "routes.json"), "w", encoding="utf-8") as file:
        json.dump({"routes": routes, "blocked": tests, "conflicts": conflicts, "unreachable": unreachable}, file, ensure_ascii=False)
    with open(os.path.join(out, "objects.json"), "w", encoding="utf-8") as file:
        json.dump({
            "size": [W, H], "origin": [X0, Y0], "ppu": PPU, "tiles": tiles, "sprites": sprites,
            "bridges": [{"x": bx, "y": by} for bx, by in bridges if inside(bx, by)],
            "views": [{"name": name, "x": x, "y": y} for name, x, y in L.VIEWS if inside(*to_px(x, y))],
            "depthTree": [X0 + edge_trees[0][0] / PPU, Y1 - edge_trees[0][1] / PPU],
            "objects": objects,
        }, file, ensure_ascii=False)

    preview = ground.convert("RGBA")
    for item in objects:
        draw_sprite(preview, parts[item["part"]], item["x"], item["y"], sprite_pivots.get(item["part"]))
    preview = preview.convert("RGB")
    preview.save(os.path.join(out, "preview.png"))
    preview.resize((W // 4, H // 4), Image.BOX).save(os.path.join(out, "overview.png"))
    print("objects", len(objects), "saved", out)


if __name__ == "__main__":
    main()
