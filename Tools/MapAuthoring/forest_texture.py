"""Label-guided texture synthesis from the approved design image.

The design's own pixels are woven into a new ground that follows a target soil mask.
Small patches are joined along minimum-error seams, so grass, soil and the broken edge
between them keep the design's drawing without repeating any large fragment.
Uses Pillow only. The result is deterministic for a seed.
"""
import colorsys
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageStat

DESIGN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Inputs", "design-reference.png")
# The hero is 78 px tall in the design and 61 px tall at native scale.
DESIGN_SCALE = 0.78
PANELS = ((14, 72, 762, 520), (778, 72, 1526, 520), (14, 534, 762, 1008), (778, 534, 1526, 1008))
TITLE_SIZE = (205, 62)
# Areas that must not be sampled, in design coordinates: heroes and the bridge.
EXCLUDED = ((640, 280, 760, 380), (1200, 790, 1300, 900), (175, 600, 600, 830))

PATCH = 40
OVERLAP = 12
STEP = PATCH - OVERLAP
GRID = 5


def kind(r, g, b):
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    h *= 360
    if b > g * 1.05 and b > r * 1.35:
        return 2
    if 55 < h < 150 and s > 0.3:
        return 0
    if 18 < h < 50 and s > 0.35 and v > 0.5:
        return 1
    return 3


class Atlas:
    """The four design panels and their mirror images, with per-pixel labels."""

    def __init__(self):
        design = Image.open(DESIGN).convert("RGB")
        panels = []
        for panel in PANELS:
            crop = design.crop(panel)
            size = (round(crop.width * DESIGN_SCALE), round(crop.height * DESIGN_SCALE))
            scaled = crop.resize(size, Image.LANCZOS)
            bad = Image.new("L", crop.size)
            draw = ImageDraw.Draw(bad)
            draw.rectangle((0, 0) + TITLE_SIZE, fill=255)
            for box in EXCLUDED:
                draw.rectangle((box[0] - panel[0], box[1] - panel[1], box[2] - panel[0], box[3] - panel[1]), fill=255)
            panels.append((scaled, bad.resize(size, Image.NEAREST)))
        width = max(p[0].width for p in panels)
        gap = PATCH
        height = sum(p[0].height + gap for p in panels)
        self.image = Image.new("RGB", (width * 2 + gap, height))
        excluded = Image.new("L", self.image.size, 255)
        y = 0
        for scaled, bad in panels:
            self.image.paste(scaled, (0, y))
            excluded.paste(bad, (0, y))
            self.image.paste(scaled.transpose(Image.FLIP_LEFT_RIGHT), (width + gap, y))
            excluded.paste(bad.transpose(Image.FLIP_LEFT_RIGHT), (width + gap, y))
            y += scaled.height + gap
        width, height = self.image.size
        pixels = self.image.load()
        soil = Image.new("L", self.image.size)
        blocked = Image.new("L", self.image.size)
        dark = Image.new("L", self.image.size)
        soil_px, blocked_px, dark_px = soil.load(), blocked.load(), dark.load()
        for y in range(height):
            for x in range(width):
                r, g, b = pixels[x, y]
                label = kind(r, g, b)
                if label == 1:
                    soil_px[x, y] = 255
                elif label != 0:
                    blocked_px[x, y] = 255
                if max(r, g, b) < 100:
                    dark_px[x, y] = 255
        self.soil = soil.filter(ImageFilter.MedianFilter(5))
        # Pebbles and flowers are small, so only larger blobs count as blocked.
        solid = blocked.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(9))
        self.blocked = ImageChops.lighter(solid, excluded)
        self.dark = dark
        self.stone = blocked
        self.luma = self.image.convert("L")


def label_vector(mask, x, y):
    cell = mask.crop((x, y, x + PATCH, y + PATCH)).resize((GRID, GRID), Image.BOX)
    return tuple(cell.tobytes())


def bucket_key(vector):
    mean = sum(vector) / (len(vector) * 255)
    if mean < 0.03:
        return ("grass",)
    if mean > 0.97:
        return ("soil",)
    gx = gy = 0.0
    centre = (GRID - 1) / 2
    for row in range(GRID):
        for column in range(GRID):
            value = vector[row * GRID + column]
            gx += value * (column - centre)
            gy += value * (row - centre)
    direction = round(math.atan2(gy, gx) / (math.pi / 4)) % 8
    return ("edge", min(3, int(mean * 4)), direction)


def index_atlas(atlas, stride=4):
    """Group usable patch positions by what they contain."""
    width, height = atlas.image.size
    small_blocked = atlas.blocked.resize((width // 4, height // 4), Image.BOX)
    small_dark = atlas.dark.resize((width // 4, height // 4), Image.BOX)
    small_stone = atlas.stone.resize((width // 4, height // 4), Image.BOX)
    buckets = {}
    tone_values = {"grass": [], "soil": []}
    span = PATCH // 4
    for y in range(0, height - PATCH, stride):
        for x in range(0, width - PATCH, stride):
            sx, sy = x // 4, y // 4
            blocked = ImageStat.Stat(small_blocked.crop((sx, sy, sx + span, sy + span))).mean[0]
            dark = ImageStat.Stat(small_dark.crop((sx, sy, sx + span, sy + span))).mean[0]
            vector = label_vector(atlas.soil, x, y)
            key = bucket_key(vector)
            tone = ImageStat.Stat(atlas.luma.crop((x, y, x + PATCH, y + PATCH))).mean[0]
            if key[0] == "grass" and 12 < dark < 90 and blocked < 120:
                # A small dark clump: the centre holds it and the rim stays clear.
                rim = ImageStat.Stat(small_dark.crop((sx, sy, sx + span, sy + 2))).mean[0]
                rim += ImageStat.Stat(small_dark.crop((sx, sy + span - 2, sx + span, sy + span))).mean[0]
                rim += ImageStat.Stat(small_dark.crop((sx, sy, sx + 2, sy + span))).mean[0]
                rim += ImageStat.Stat(small_dark.crop((sx + span - 2, sy, sx + span, sy + span))).mean[0]
                if rim < 70:
                    buckets.setdefault(("clump",), []).append((x, y, vector, tone))
                continue
            if blocked > 8 or (key[0] == "grass" and dark > 12):
                continue
            if key[0] == "soil" and ImageStat.Stat(small_stone.crop((sx, sy, sx + span, sy + span))).mean[0] > 3:
                buckets.setdefault(("stones",), []).append((x, y, vector, tone))
                continue
            buckets.setdefault(key, []).append((x, y, vector, tone))
            if key[0] in tone_values:
                tone_values[key[0]].append(tone)
    tiers = {}
    for name, values in tone_values.items():
        values.sort()
        count = len(values)
        # The darkest fifth of the grass lies in tree shade, which is added separately.
        floor = values[count // 5] if name == "grass" else values[0]
        limits = (values[count * 5 // 12], values[count * 3 // 4])
        tiers[name] = limits
        groups = ([], [], [])
        for item in buckets[(name,)]:
            if item[3] >= floor:
                groups[(item[3] > limits[0]) + (item[3] > limits[1])].append(item)
        for index, group in enumerate(groups):
            buckets[(name, index)] = group
    return buckets


def seam_mask(error, width, height, vertical):
    """Find the cheapest path through an overlap and return a mask of what stays new."""
    along, across = (height, width) if vertical else (width, height)

    def cost(a, c):
        return error[a * width + c] if vertical else error[c * width + a]

    table = [[0.0] * across for _ in range(along)]
    for c in range(across):
        table[0][c] = cost(0, c)
    for a in range(1, along):
        previous, row = table[a - 1], table[a]
        for c in range(across):
            best = previous[c]
            if c and previous[c - 1] < best:
                best = previous[c - 1]
            if c + 1 < across and previous[c + 1] < best:
                best = previous[c + 1]
            row[c] = cost(a, c) + best
    path = [0] * along
    last = table[along - 1]
    position = last.index(min(last))
    path[along - 1] = position
    for a in range(along - 2, -1, -1):
        row = table[a]
        low, high = max(0, position - 1), min(across - 1, position + 1)
        position = min(range(low, high + 1), key=row.__getitem__)
        path[a] = position
    return path


def synthesise(atlas, buckets, target, tone, detail, skip, seed):
    """Weave a new image that follows the target soil mask.

    target: L image, soil = 255. tone: L image that chooses dark or light patches.
    detail: L image, the chance of a small clump in the grass out of 255.
    skip: L image, cells where it is set are left empty.
    """
    rng = random.Random(seed)
    width, height = target.size
    canvas = Image.new("RGB", (width, height))
    source = atlas.image
    written = 0
    tone_px = tone.load()
    skip_px = skip.load()
    detail_px = detail.load()
    clumps = buckets.get(("clump",), [])
    stones = buckets.get(("stones",), [])
    neighbours = {0: (1, 7), 1: (0, 2), 2: (1, 3), 3: (2, 4), 4: (3, 5), 5: (4, 6), 6: (5, 7), 7: (6, 0)}

    def pool(vector, key, cx, cy):
        if key[0] == "grass" and clumps and rng.randrange(255) < detail_px[cx, cy]:
            return rng.sample(clumps, min(10, len(clumps)))
        if key[0] == "soil" and stones and rng.randrange(255) < 16:
            return rng.sample(stones, min(10, len(stones)))
        if key[0] in ("grass", "soil"):
            value = tone_px[cx, cy]
            tier = 0 if value < 110 else 2 if value > 146 else 1
            items = buckets.get((key[0], tier)) or buckets[(key[0],)]
            return rng.sample(items, min(14, len(items)))
        items = list(buckets.get(key, ()))
        for direction in neighbours[key[2]]:
            items += buckets.get(("edge", key[1], direction), ())
        if len(items) < 24:
            for level in range(4):
                for direction in (key[2],) + neighbours[key[2]]:
                    items += buckets.get(("edge", level, direction), ())
        if not items:
            items = buckets[("grass",)]
        chosen = rng.sample(items, min(70, len(items)))
        chosen.sort(key=lambda item: sum((a - b) ** 2 for a, b in zip(item[2], vector)))
        return chosen[:12]

    for y in range(0, height, STEP):
        for x in range(0, width, STEP):
            cx, cy = min(x + PATCH // 2, width - 1), min(y + PATCH // 2, height - 1)
            if skip_px[cx, cy]:
                continue
            vector = label_vector(target, x, y)
            key = bucket_key(vector)
            has_left = x > 0
            has_top = y > 0
            best, best_error = None, None
            for item in pool(vector, key, cx, cy):
                sx, sy = item[0], item[1]
                error = 0.0
                if has_left:
                    a = canvas.crop((x, y, x + OVERLAP, y + PATCH))
                    b = source.crop((sx, sy, sx + OVERLAP, sy + PATCH))
                    error += sum(ImageStat.Stat(ImageChops.difference(a, b)).sum)
                if has_top:
                    a = canvas.crop((x, y, x + PATCH, y + OVERLAP))
                    b = source.crop((sx, sy, sx + PATCH, sy + OVERLAP))
                    error += sum(ImageStat.Stat(ImageChops.difference(a, b)).sum)
                error *= 0.9 + rng.random() * 0.2
                if best_error is None or error < best_error:
                    best, best_error = item, error
            sx, sy = best[0], best[1]
            patch = source.crop((sx, sy, sx + PATCH, sy + PATCH))
            mask = Image.new("L", (PATCH, PATCH), 255)
            mask_px = mask.load()
            if has_left:
                a = canvas.crop((x, y, x + OVERLAP, y + PATCH)).convert("L")
                error = ImageChops.difference(a, patch.crop((0, 0, OVERLAP, PATCH)).convert("L")).tobytes()
                path = seam_mask(error, OVERLAP, PATCH, True)
                for row, column in enumerate(path):
                    for c in range(column):
                        mask_px[c, row] = 0
            if has_top:
                a = canvas.crop((x, y, x + PATCH, y + OVERLAP)).convert("L")
                error = ImageChops.difference(a, patch.crop((0, 0, PATCH, OVERLAP)).convert("L")).tobytes()
                path = seam_mask(error, PATCH, OVERLAP, False)
                for column, row in enumerate(path):
                    for r in range(row):
                        mask_px[column, r] = 0
            canvas.paste(patch, (x, y), mask.filter(ImageFilter.GaussianBlur(0.7)))
            written += 1
    print("patches", written)
    return canvas
