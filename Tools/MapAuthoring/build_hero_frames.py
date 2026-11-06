"""Cut the hero's body frames and gear parts into native-scale sprites.

Usage:
    python build_hero_frames.py [--preview <png-path>]

The hero has one empty-handed body animation. Gear is drawn apart and held at anchors, so a new
item needs only its own small sprites and never a new animation. Long weapons need one drawing:
this tool rotates it into every angle, and the body says which angle each pose holds it at.

Reads the hero-body sheets, the sheets of the same poses with the sword in hand, the hero-set sheets
and hero-gear-01.png in Assets/Art/Generated/ForestLook and writes:
    Hero/hero-<view>-<index>.png, Hero/hero-idle-<view>.png, Hero/hero-attack-<view>-<index>.png,
    Hero/anchors.json, Hand/<frame>-main.png, Hand/<frame>-off.png,
    Sets/<set>/<frame>.png,
    Gear/<item>-<view>.png, Gear/<item>-rot-<index>.png, Gear/gear.json

Where the hand holds a weapon, and at what angle, is read from the sheets with the sword in hand, so
the weapon stays rigid with the arm as it was drawn.

Hand sprites are the fists alone. They are drawn over a held item, so its handle passes through the
fist instead of lying on top of it. The main fist comes from the sheets with the sword in hand.

An armour set is the same body in other clothes: every sheet is a hero-body sheet redrawn with the
same poses. A set shares the hero's hands, angles and fists, so every weapon fits every set. Its
frames must stay on the hero's outline, and the tool stops when one does not.

Every body frame shares one cell size, so the sprite pivot stays at the same foot position.
Frames are aligned by the helmet horizontally and by the feet vertically, which keeps the
upper body steady and leaves the natural bounce of the run. Output is deterministic.
"""
import json
import math
import os
import statistics
import sys
from collections import deque

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOREST = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
HERO = os.path.join(FOREST, "Hero")
GEAR = os.path.join(FOREST, "Gear")
HAND = os.path.join(FOREST, "Hand")
SETS = os.path.join(FOREST, "Sets")

# Each sheet draws the hero at its own size. The scales match every helmet to about 28 pixels.
RUN_SHEETS = (
    ("hero-body-run-01.png", ("down", "side", "up"), 0.22),
    ("hero-body-run-diagonal-01.png", ("downside", "upside"), 0.207),
)
IDLE_SHEETS = (
    ("hero-body-idle-01.png", ("down", "side", "up"), 0.135),
    ("hero-body-idle-diagonal-01.png", ("downside", "upside"), 0.135),
)
ATTACK_SHEETS = (
    ("hero-body-attack-01.png", ("down", "side", "up"), 0.22),
    ("hero-body-attack-diagonal-01.png", ("downside", "upside"), 0.207),
)
# The same poses as the hero-body sheet with the sword in hand. There are none for the attack.
HELD_SHEETS = {
    "hero-body-run-01.png": "hero-run-01.png",
    "hero-body-run-diagonal-01.png": "hero-run-diagonal-01.png",
    "hero-body-idle-01.png": "hero-idle-01.png",
    "hero-body-idle-diagonal-01.png": "hero-idle-diagonal-01.png",
}
ATTACK_FRAMES = 3
# Gloves in the attack sheets overlap the body, so they are read by eye, as sheet pixels:
# for each frame the main hand, then the off hand.
ATTACK_HANDS = {
    "down": (((178, 128), (325, 220)), ((700, 170), (830, 208)), ((1310, 232), (1340, 198))),
    "side": (((185, 430), (305, 532)), ((855, 505), (800, 555)), ((1355, 548), (1330, 575))),
    "up": (((168, 790), (325, 855)), ((885, 803), (680, 858)), ((1200, 865), (1340, 850))),
    "downside": (((135, 208), (322, 290)), ((895, 255), (680, 250)), ((1310, 305), (1345, 272))),
    "upside": (((140, 655), (315, 740)), ((885, 685), (670, 715)), ((1375, 760), (1235, 740))),
}
VIEWS = ("down", "downside", "side", "upside", "up")
COLUMNS = 4
CELL_WIDTH = 96
CELL_HEIGHT = 80
FOOT_MARGIN = 4

# Which glove is the main hand, by its place on screen. The hero faces right in the side views.
# Seen from the front the right hand is on the left of the screen, and from behind on the right.
MAIN_HAND_IS_LEFT = {"down": True, "downside": True, "side": True, "upside": False, "up": False}

MIN_HAND_GAP = 14
# The fist is the last few pixels of the brown arm, counted from the end farthest from the chest.
FIST_LENGTH = 9
FIST_RADIUS = 8

# From the middle of the grip to where the blade starts, in map pixels.
GRIP_TO_BLADE = 7.5
# Pixels that count as the blade: nearly grey, bright, and clearly unlike the empty-handed sheet.
BLADE_MAX_TINT = 44
BLADE_MIN_BRIGHTNESS = 120
BLADE_MIN_CHANGE = 150

# Armour sets. Each one has a hero-set-<name> sheet for every hero-body sheet.
ARMOUR_SETS = ("leather",)
# How far a set may leave the hero's outline: the share of pixels they have in common, and how many
# pixels the outline around a hand may move.
MIN_OUTLINE_OVERLAP = 0.85
MAX_HAND_SHIFT = 2
HAND_WINDOW = 9
HAND_SEARCH = 6
# A hand with less empty space than this around it lies on the body, so its outline says nothing.
MIN_HAND_EDGE = 0.15

# The back anchor sits on the spine, as a share of the sprite's height from its top.
BACK_HEIGHT = 0.42
BACK_OFFSET_X = {"down": 0, "downside": -4, "side": -8, "upside": -3, "up": 0}

# Gear sheet: one row per item, one column per view, in VIEWS order.
GEAR_SHEET = "hero-gear-01.png"
# item: slot, row, height on the map in pixels, the grip as a share of the sprite for each view,
# and whether the fist closes around it. A shield hides the fist, so it does not.
GEAR_ITEMS = {
    "shield": ("OffHand", 1, 30, {view: (0.5, 0.5) for view in VIEWS}, False),
    "bow": ("OffHand", 2, 46, {view: (0.5, 0.5) for view in VIEWS}, True),
    "quiver": ("Back", 3, 34, {view: (0.5, 0.52) for view in VIEWS}, False),
}


# Weapons that look the same from every side need one drawing, rotated by this tool.
# item: slot, row and column of the drawing, height on the map, and the grip as a share of the
# sprite from its top left. The drawing's tip points down.
ROTATED_ITEMS = {
    "sword": ("MainHand", 0, 0, 34, (0.5, 0.18)),
}
ROTATION_STEPS = 24


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


def cell_box(keyed, rows, columns, row, column):
    row_height = keyed.height / rows
    column_width = keyed.width / columns
    return (
        round(column * column_width), round(row * row_height),
        round((column + 1) * column_width), round((row + 1) * row_height),
    )


def cut(keyed, rows, columns, row, column):
    """The sprite in one grid cell, trimmed to its visible pixels, and where it sits on the sheet."""
    box = cell_box(keyed, rows, columns, row, column)
    cell = keyed.crop(box)
    inner = cell.getchannel("A").getbbox()
    return cell.crop(inner), (box[0] + inner[0], box[1] + inner[1])


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
    return canvas, (left, top)


def same_place(sheet, box, origin, offset, scale):
    """One cell of another sheet of the same poses, sitting exactly where the body frame sits."""
    small = shrink(sheet.crop(box), scale)
    canvas = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT), (0, 0, 0, 0))
    left = round(offset[0] - (origin[0] - box[0]) * scale)
    top = round(offset[1] - (origin[1] - box[1]) * scale)
    canvas.paste(small, (left, top), small)
    return canvas


def blade_cells(held, body):
    """The pixels of the blade: the largest grey shape that the empty-handed cell does not have."""
    held_pixels, body_pixels = held.load(), body.load()
    mask = set()
    for y in range(held.height):
        for x in range(held.width):
            r, g, b, a = held_pixels[x, y]
            if a == 0 or max(r, g, b) - min(r, g, b) > BLADE_MAX_TINT or max(r, g, b) < BLADE_MIN_BRIGHTNESS:
                continue
            r2, g2, b2, a2 = body_pixels[x, y]
            if a2 == 0 or abs(r - r2) + abs(g - g2) + abs(b - b2) > BLADE_MIN_CHANGE:
                mask.add((x, y))
    largest = []
    seen = set()
    for start in sorted(mask):
        if start in seen:
            continue
        seen.add(start)
        queue = deque([start])
        cells = []
        while queue:
            x, y = queue.popleft()
            cells.append((x, y))
            for nx in range(x - 2, x + 3):
                for ny in range(y - 2, y + 3):
                    if (nx, ny) in mask and (nx, ny) not in seen:
                        seen.add((nx, ny))
                        queue.append((nx, ny))
        if len(cells) > len(largest):
            largest = cells
    return largest


def blade_line(cells, chest):
    """Where the blade starts, and the way it points, from the end nearer the chest to the tip."""
    count = len(cells)
    mean_x = sum(cell[0] for cell in cells) / count
    mean_y = sum(cell[1] for cell in cells) / count
    xx = sum((cell[0] - mean_x) ** 2 for cell in cells)
    yy = sum((cell[1] - mean_y) ** 2 for cell in cells)
    xy = sum((cell[0] - mean_x) * (cell[1] - mean_y) for cell in cells)
    tilt = 0.5 * math.atan2(2 * xy, xx - yy)
    step_x, step_y = math.cos(tilt), math.sin(tilt)
    along = [(cell[0] - mean_x) * step_x + (cell[1] - mean_y) * step_y for cell in cells]
    near = (mean_x + step_x * min(along), mean_y + step_y * min(along))
    far = (mean_x + step_x * max(along), mean_y + step_y * max(along))
    if math.dist(near, chest) > math.dist(far, chest):
        near, far = far, near
    length = math.dist(near, far)
    return near, ((far[0] - near[0]) / length, (far[1] - near[1]) / length)


def is_glove(pixel):
    r, g, b, a = pixel
    return a > 0 and 70 <= r <= 190 and r > g + 22 and g > b + 8 and b < 90


def fist_centre(cells, top, height):
    """The middle of the fist: the part of the brown arm farthest from the chest."""
    chest_x, chest_y = CELL_WIDTH / 2, top + height * 0.45

    def reach(cell):
        return math.hypot(cell[0] - chest_x, cell[1] - chest_y)

    farthest = max(reach(cell) for cell in cells)
    end = [cell for cell in cells if reach(cell) >= farthest - FIST_LENGTH]
    return sum(cell[0] for cell in end) / len(end), sum(cell[1] for cell in end) / len(end)


def fist_sprite(frame, centre):
    """The fist alone on an empty cell: glove pixels near the centre and the dark edge around them."""
    pixels = frame.load()
    result = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    out = result.load()
    glove = set()
    for y in range(frame.height):
        for x in range(frame.width):
            if math.hypot(x - centre[0], y - centre[1]) <= FIST_RADIUS and is_glove(pixels[x, y]):
                glove.add((x, y))
                out[x, y] = pixels[x, y]
    for x, y in glove:
        for nx in range(x - 1, x + 2):
            for ny in range(y - 1, y + 2):
                r, g, b, a = pixels[nx, ny]
                if a and (nx, ny) not in glove and max(r, g, b) < 100:
                    out[nx, ny] = pixels[nx, ny]
    return result


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
                clusters.append((len(cells), *fist_centre(cells, top, height)))
    return sorted(clusters, reverse=True)


def anchor_data(frame, view, main, off, angle):
    """The hands and the spine of one frame, in pixels from the foot, with y going up."""
    _, top, _, bottom = frame.getchannel("A").getbbox()
    foot_x, foot_y = CELL_WIDTH / 2, CELL_HEIGHT - FOOT_MARGIN
    spine_y = top + (bottom - top) * BACK_HEIGHT

    def point(x, y):
        return {"x": round(x - foot_x, 1), "y": round(foot_y - y, 1)}

    return {
        "mainHand": point(*main),
        "offHand": point(*off),
        "back": point(foot_x + BACK_OFFSET_X[view], spine_y),
        "mainHandAngle": angle,
    }


def find_hands(frame, view, name):
    """Find the two fists of one frame, returning the main hand and then the off hand."""
    found = gloves(frame)
    # A forearm can break into two clusters, so the second hand must be clearly apart from the first.
    apart = [item for item in found[1:] if abs(item[1] - found[0][1]) >= MIN_HAND_GAP] if found else []
    if not apart:
        raise ValueError(f"{name}: expected two gloves, found {found}")
    first, second = sorted((found[0], apart[0]), key=lambda item: item[1])
    main, off = (first, second) if MAIN_HAND_IS_LEFT[view] else (second, first)
    return main[1:], off[1:]


def read_held(body, held, rows, columns, row, column, scale, name):
    """Cut one body frame and read from the sheet with the sword how its hand holds a weapon.

    Returns the frame, how it sits on its sheet, the same pose with the sword, the grip and the angle.
    """
    item, origin = cut(body, rows, columns, row, column)
    frame, offset = place(item, scale, name)
    box = cell_box(body, rows, columns, row, column)
    chest = (origin[0] - box[0] + item.width / 2, origin[1] - box[1] + item.height * 0.45)
    cells = blade_cells(held.crop(box), body.crop(box))
    if not cells:
        raise ValueError(f"{name}: no blade found")
    near, direction = blade_line(cells, chest)
    start_x = offset[0] + (near[0] + box[0] - origin[0]) * scale
    start_y = offset[1] + (near[1] + box[1] - origin[1]) * scale
    grip = (start_x - direction[0] * GRIP_TO_BLADE, start_y - direction[1] * GRIP_TO_BLADE)
    angle = math.degrees(math.atan2(-direction[1], direction[0])) % 360
    return frame, (box, origin, offset, scale), same_place(held, box, origin, offset, scale), grip, angle


def load_with_held(file):
    body = key_magenta(Image.open(os.path.join(FOREST, file)))
    held = key_magenta(Image.open(os.path.join(FOREST, HELD_SHEETS[file])))
    if held.size != body.size:
        raise ValueError(f"{HELD_SHEETS[file]} is not the size of {file}")
    return body, held


def build_body():
    """Write the hero's frames. Returns them with their anchors and where each sits on its sheet."""
    os.makedirs(HERO, exist_ok=True)
    frames = {}
    placements = {}
    held_angle = {}
    data = {"frames": []}

    os.makedirs(HAND, exist_ok=True)

    def add(name, frame, view, hands, angle, placement, fist_frame):
        frames[name] = frame
        placements[name] = placement
        frame.save(os.path.join(HERO, name + ".png"))
        fist_sprite(fist_frame, hands[0]).save(os.path.join(HAND, name + "-main.png"))
        fist_sprite(frame, hands[1]).save(os.path.join(HAND, name + "-off.png"))
        data["frames"].append({"name": name, **anchor_data(frame, view, hands[0], hands[1], angle)})

    for file, views, scale in RUN_SHEETS:
        body, held = load_with_held(file)
        for row, view in enumerate(views):
            names = [f"hero-{view}-{column}" for column in range(COLUMNS)]
            read = [read_held(body, held, len(views), COLUMNS, row, column, scale, names[column]) for column in range(COLUMNS)]
            # One angle for the whole run: the weapon moves with the hand and never swings on its own.
            held_angle[view] = round(statistics.median(item[4] for item in read), 1)
            for name, (frame, placement, held_frame, grip, _) in zip(names, read):
                hands = (grip, find_hands(frame, view, name)[1])
                add(name, frame, view, hands, held_angle[view], (file, *placement), held_frame)
    for file, views, scale in IDLE_SHEETS:
        body, held = load_with_held(file)
        for column, view in enumerate(views):
            name = f"hero-idle-{view}"
            frame, placement, held_frame, grip, angle = read_held(body, held, 1, len(views), 0, column, scale, name)
            hands = (grip, find_hands(frame, view, name)[1])
            add(name, frame, view, hands, round(angle, 1), (file, *placement), held_frame)
    for file, views, scale in ATTACK_SHEETS:
        keyed = key_magenta(Image.open(os.path.join(FOREST, file)))
        for row, view in enumerate(views):
            for column in range(ATTACK_FRAMES):
                name = f"hero-attack-{view}-{column}"
                item, origin = cut(keyed, len(views), ATTACK_FRAMES, row, column)
                frame, offset = place(item, scale, name)
                box = cell_box(keyed, len(views), ATTACK_FRAMES, row, column)

                def on_frame(point):
                    return (offset[0] + (point[0] - origin[0]) * scale, offset[1] + (point[1] - origin[1]) * scale)

                main, off = ATTACK_HANDS[view][column]
                add(name, frame, view, (on_frame(main), on_frame(off)), held_angle[view], (file, box, origin, offset, scale), frame)

    with open(os.path.join(HERO, "anchors.json"), "w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, indent=1)
        file.write("\n")
    print("body frames", len(frames))
    return frames, {entry["name"]: entry for entry in data["frames"]}, placements


def outline_overlap(first, second):
    """The share of pixels two frames have in common, out of those either of them has."""
    a = first.getchannel("A").load()
    b = second.getchannel("A").load()
    both = either = 0
    for y in range(first.height):
        for x in range(first.width):
            both += 1 if a[x, y] and b[x, y] else 0
            either += 1 if a[x, y] or b[x, y] else 0
    return both / either


def hand_shift(body, dressed, centre):
    """How far the outline around a hand moved, in pixels. None when the hand lies on the body."""
    a = body.getchannel("A").load()
    b = dressed.getchannel("A").load()
    centre_x, centre_y = round(centre[0]), round(centre[1])
    cells = [
        (x, y)
        for x in range(max(0, centre_x - HAND_WINDOW), min(body.width, centre_x + HAND_WINDOW + 1))
        for y in range(max(0, centre_y - HAND_WINDOW), min(body.height, centre_y + HAND_WINDOW + 1))
        if math.hypot(x - centre_x, y - centre_y) <= HAND_WINDOW
    ]
    if sum(1 for x, y in cells if not a[x, y]) < len(cells) * MIN_HAND_EDGE:
        return None
    best = None
    for shift_x in range(-HAND_SEARCH, HAND_SEARCH + 1):
        for shift_y in range(-HAND_SEARCH, HAND_SEARCH + 1):
            same = 0
            for x, y in cells:
                moved_x, moved_y = x + shift_x, y + shift_y
                inside = 0 <= moved_x < body.width and 0 <= moved_y < body.height and b[moved_x, moved_y] > 0
                same += (a[x, y] > 0) == inside
            score = (same, -math.hypot(shift_x, shift_y))
            if best is None or score > best:
                best = score
    return -best[1]


def build_sets(frames, points, placements):
    """Write every armour set's frames on the hero's cells, and stop if one leaves the hero's outline."""
    foot_x, foot_y = CELL_WIDTH / 2, CELL_HEIGHT - FOOT_MARGIN
    for armour in ARMOUR_SETS:
        folder = os.path.join(SETS, armour)
        os.makedirs(folder, exist_ok=True)
        sheets = {}
        problems = []
        lowest_overlap = 1
        largest_shift = 0
        hidden = 0
        for name, (file, box, origin, offset, scale) in placements.items():
            set_file = file.replace("hero-body", f"hero-set-{armour}")
            if set_file not in sheets:
                sheets[set_file] = key_magenta(Image.open(os.path.join(FOREST, set_file)))
            frame = same_place(sheets[set_file], box, origin, offset, scale)
            overlap = outline_overlap(frames[name], frame)
            lowest_overlap = min(lowest_overlap, overlap)
            if overlap < MIN_OUTLINE_OVERLAP:
                problems.append(f"{name}: outline overlap {overlap:.2f}")
            for hand in ("mainHand", "offHand"):
                point = points[name][hand]
                shift = hand_shift(frames[name], frame, (foot_x + point["x"], foot_y - point["y"]))
                if shift is None:
                    hidden += 1
                    continue
                largest_shift = max(largest_shift, shift)
                if shift > MAX_HAND_SHIFT:
                    problems.append(f"{name}: {hand} moved {shift:.1f}")
            frame.save(os.path.join(folder, name + ".png"))
        print(f"set {armour}: overlap {lowest_overlap:.2f} or more, hands moved {largest_shift:.1f} at most, {hidden} hands on the body")
        if problems:
            raise ValueError(f"{armour} does not fit the hero: " + "; ".join(problems))


def rotate_item(item, part, height, grip):
    """Write the drawing at every angle, grip at the centre. Index 0 points east, going counter-clockwise.

    Rotating the large drawing before shrinking keeps the lines as clean as the other sprites.
    """
    grip_x, grip_y = part.width * grip[0], part.height * grip[1]
    radius = math.ceil(max(math.hypot(x - grip_x, y - grip_y) for x in (0, part.width) for y in (0, part.height)))
    canvas = Image.new("RGBA", (radius * 2, radius * 2), (0, 0, 0, 0))
    canvas.paste(part, (round(radius - grip_x), round(radius - grip_y)))
    names = []
    for index in range(ROTATION_STEPS):
        angle = index * 360 / ROTATION_STEPS
        rotated = canvas.rotate(angle - 270, resample=Image.NEAREST)
        name = f"{item}-rot-{index:02d}"
        shrink(rotated, height / part.height).save(os.path.join(GEAR, name + ".png"))
        names.append(name)
    return names


def build_gear():
    os.makedirs(GEAR, exist_ok=True)
    keyed = key_magenta(Image.open(os.path.join(FOREST, GEAR_SHEET)))
    rows = max(item[1] for item in GEAR_ITEMS.values()) + 1
    data = {"items": []}
    count = 0
    for item, (slot, row, column, height, grip) in ROTATED_ITEMS.items():
        names = rotate_item(item, cut(keyed, rows, len(VIEWS), row, column)[0], height, grip)
        count += len(names)
        data["items"].append({"name": item, "slot": slot, "views": [], "rotations": names, "heldInFist": True})
    for item, (slot, row, height, grips, held_in_fist) in GEAR_ITEMS.items():
        views = []
        for column, view in enumerate(VIEWS):
            part = cut(keyed, rows, len(VIEWS), row, column)[0]
            name = f"{item}-{view}"
            shrink(part, height / part.height).save(os.path.join(GEAR, name + ".png"))
            count += 1
            pivot_x, pivot_from_top = grips[view]
            views.append({"view": view, "file": name, "pivotX": pivot_x, "pivotY": round(1 - pivot_from_top, 3)})
        data["items"].append({"name": item, "slot": slot, "views": views, "rotations": [], "heldInFist": held_in_fist})
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
    frames, points, placements = build_body()
    build_sets(frames, points, placements)
    build_gear()
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1], frames, points)


if __name__ == "__main__":
    main()
