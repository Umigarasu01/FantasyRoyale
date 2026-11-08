"""Cut the hero's armour parts and gear into native-scale sprites.

Usage:
    python build_hero_frames.py [--preview <png-path>]

The hero is drawn in three layers: legs, torso and head. Each layer can be swapped on its own, so parts
that were never drawn together still fit. Gear is drawn apart and held at anchors, so a new item needs
only its own small sprites and never a new animation. Long weapons need one drawing: this tool rotates
it into every angle, and the body says which angle each pose holds it at. The fist hides the grip, so the
sword is cut into two pieces: the guard with the blade, which starts at the blade-side end of the torso's
own fist, and the pommel, which starts at its other end. The tool measures both ends on every frame.

Each weapon kind may have its own run: the sword type's run holds the sword arm down with a closed fist. Its
body and torso sheets are the common run redrawn, so its frames sit where the common run's frames sit and the
common legs fit.

Reads from Assets/Art/Generated/ForestLook:
    the whole hero: hero-body-* and the same poses with the sword in hand, hero-run-*, hero-idle-* and
    hero-upper-swing-*. These are never shown. They say where the hands, the helmet and the blade are.
    the parts: hero-head, hero-torso-*, hero-legs-* and hero-set-<id>-head, hero-set-<id>-torso-*,
    hero-set-<id>-legs-*.
    the sword type's run: hero-body-run-sword-*, hero-run-sword-* and the torso parts hero-torso-run-sword-*,
    hero-set-<id>-torso-run-sword-*.
    hero-gear-01.png.

Writes, for the hero in Hero/ and for every other part in Sets/<id>/:
    hero-head-<view>.png, hero-torso-idle-<view>.png, hero-torso-<view>-<index>.png,
    hero-torso-sword-<view>-<index>.png, hero-torso-swing-<view>-<index>.png, hero-legs-idle-<view>.png,
    hero-legs-<view>-<index>.png, hero-legs-lunge-<view>-<index>.png
and hero-part-template-01.png, Hero/anchors.json, Hand/<frame>-main.png, Hand/<frame>-off.png,
Smear/hero-upper-swing-<view>-<index>.png, Gear/<item>-<view>.png, Gear/<item>-rot-<index>.png,
Gear/sword-pommel-rot-<index>.png, Gear/gear.json

A generated sheet of a part can sit a little away from the whole hero, so every torso and leg frame is
moved to where it matches the whole frame best. Every head is moved so its chin sits at the knight's:
a chin that sinks into the collar makes the neck look short. The back diagonal run turns its drawn head
aside, so its head is put on the torso's neck, the collar's opening, instead of on the drawn helmet. All parts share the hero's anchors and
fists, so every weapon fits every combination.

An attack keeps the legs apart from the torso. The torso of a swing sits on its own canvas with the
helmet at a fixed place. While running, the game puts that place on the helmet of the running frame.
A swing from a standing start lunges: the legs step forward with the swing's frames, and the helmet's
place on each of those frames carries the torso forward with them. The swing is a half circle around
the way the hero faces, from the shield's side out to the sword hand's, and the tool stops if a blade
leaves it or the shield moves. The lunge keeps its own order: it steps forward whichever way the blade goes.

Hand sprites are the fists alone. They are drawn over a held item, so its handle passes through the
fist instead of lying on top of it. The main fist comes from the sheets with the sword in hand.

Every frame on the body cell shares one size, so the sprite pivot stays at the same foot position.
Output is deterministic.
"""
import json
import math
import os
import re
import statistics
import sys
from collections import deque

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOREST = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
HERO = os.path.join(FOREST, "Hero")
GEAR = os.path.join(FOREST, "Gear")
HAND = os.path.join(FOREST, "Hand")
SMEAR = os.path.join(FOREST, "Smear")
SETS = os.path.join(FOREST, "Sets")

# Each sheet draws the hero at its own size. The scales match every helmet to about 28 pixels.
# A run sheet also says how many frames one cycle has, and in which of them both feet leave the ground.
# The run is six frames for two steps: landing, the weight over the planted foot, and a leap with both feet
# off the ground (columns 2 and 5).
RUN_SHEETS = (
    ("hero-body-run-01.png", ("down", "side", "up"), 0.254, 6, (2, 5)),
    ("hero-body-run-diagonal-01.png", ("downside", "upside"), 0.262, 6, (2, 5)),
)
# The sword type's run, one sheet for each run sheet: the same run redrawn with the sword arm hanging down and its
# fist closed. Its frames are named hero-sword-<view>-<index>. The sheet with the sword is the same name without
# "body-", and the torso parts are hero-torso-run-sword-* and hero-set-<id>-torso-run-sword-*.
SWORD_RUN_SHEETS = {
    "hero-body-run-01.png": "hero-body-run-sword-01.png",
    "hero-body-run-diagonal-01.png": "hero-body-run-sword-diagonal-01.png",
}
# How far a frame with both feet off the ground is lifted, in map pixels. Every frame is cut with its feet
# on the line, so a leap would otherwise land; a fixed height also keeps the leaps alike between rows.
FLIGHT_RISE = 3
# How many pixels a leap drawn too high for the cell may be lowered.
MAX_LEAP_TRIM = 3
# Seen from the front or the back the legs are alike, and the drawn run kept the same leg ahead in both steps.
# There the legs of the second step are the first step's, mirrored, so the feet take turns. Only the legs are
# mirrored: the arms hold the sword and the shield on their own sides.
MIRRORED_STEP_VIEWS = ("down", "up")
# Islands smaller than this, in sheet pixels, are left over from the background and are cleared.
MAX_SPECK = 1000
IDLE_SHEETS = (
    ("hero-body-idle-01.png", ("down", "side", "up"), 0.135),
    ("hero-body-idle-diagonal-01.png", ("downside", "upside"), 0.135),
)
# The sword slash, as an upper body only: front, side and back, then the diagonals.
SWING_SHEETS = (
    ("hero-body-upper-swing-01.png", ("down", "side", "up"), 0.22),
    ("hero-body-upper-swing-diagonal-01.png", ("downside", "upside"), 0.207),
)
SWING_FRAMES = 4
# The sheets draw the swing from the sword hand's side across to the shield's. The game plays it the
# other way, from the shield's side out to the sword hand's: left to right as the hero sees it. The
# drawn follow-through, arm across the body, reads as the wind-up. Each frame of the game takes this
# column of the sheet.
SWING_ORDER = (3, 2, 1, 0)
# The way each view attacks, in degrees counter-clockwise from the east, and where the blade points
# in each frame of the game around it. Together they sweep a half circle from the hero's left to the right.
FACING = {"down": 270, "downside": 315, "side": 0, "upside": 45, "up": 90}
SWING_ANGLES = (90, 30, -30, -90)
MAX_BLADE_ERROR = 30
MAX_SHIELD_MOVE = 8
# The legs of a swing from a standing start, one column for each frame of the swing.
LUNGE_SHEETS = (
    ("hero-legs-lunge-01.png", ("down", "side", "up"), 0.22),
    ("hero-legs-lunge-diagonal-01.png", ("downside", "upside"), 0.207),
)
# For each frame, in map pixels: how far the hips move the way the hero faces, how much farther the
# helmet leans past them, and how high the helmet stands above the belt.
LUNGE_REACH = (-2, 4, 8, 5)
LUNGE_LEAN = (-2, 2, 4, 2)
LUNGE_RISE = (27, 25, 22, 25)
# The ground is seen at an angle, so a step towards or away from the viewer looks this much shorter.
GROUND_DEPTH = 0.7
# The lunge's canvas, wider than a body cell for the wide stance, with the foot at a fixed place.
LUNGE_WIDTH = 160
LUNGE_HEIGHT = 128
LUNGE_FOOT = (80, 100)
# How many rows from the top of the legs count as the belt, in map pixels.
BELT_ROWS = 4
# A long blade can cross into the next cell, so the search for it looks this far past its own, in sheet pixels.
BLADE_SEARCH_X = 110
BLADE_SEARCH_Y = 40
# A blade is a long thin shape, which the cross on a shield is not: at least this long in sheet
# pixels, and this many times more spread out along its length than across it.
MIN_BLADE_LENGTH = 60
MIN_BLADE_THINNESS = 8
VIEWS = ("down", "downside", "side", "upside", "up")
CELL_WIDTH = 96
# Tall enough above the head for a leap drawn in a diagonal run. The feet stay FOOT_MARGIN above the bottom.
CELL_HEIGHT = 96
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
# How far around a drawn grip the torso's glove is looked for, in map pixels.
GLOVE_SNAP = 6
# The fist's two ends along the weapon are measured on the knight's torso: its glove and dark outline pixels
# within FIST_CORE of the fist's middle. Each sword piece reaches FIST_OVERLAP into the fist, so no gap shows.
FIST_CORE = 4.5
FIST_OVERLAP = 1.0
# In the run and the idle of these views the glove runs into the sleeve, so the fist's middle is measured on the
# glove below the found middle, without the forearm above it.
FIST_ONLY_VIEWS = ("side",)
# How far the sword slides out of the fist along the blade in the run and the idle, in map pixels. Seen from the
# back the blade lies under the torso, and the guard would otherwise hide under the fist.
SWORD_PUSH = {"up": 3.5}
# The fist hides the grip, so the sword is drawn as two pieces, cut at rows of its drawing (tip down): the
# pommel, and the guard with the blade.
SWORD_POMMEL_ROWS = (0, 28)
SWORD_GUARD_ROW = 60
# Pixels that count as the blade: nearly grey, bright, and clearly unlike the empty-handed sheet.
BLADE_MAX_TINT = 44
BLADE_MIN_BRIGHTNESS = 120
BLADE_MIN_CHANGE = 150
# Pixels that count as the shield: clearly blue, and unlike the empty-handed sheet.
SHIELD_MIN_BLUE = 40
# A raised arm is grey too, but only the helmet is this wide, in map pixels.
HELMET_MIN_WIDTH = 20
HELMET_MAX_TINT = 50

# The upper body's canvas, with the helmet point at a fixed place.
UPPER_WIDTH = 208
UPPER_HEIGHT = 192
UPPER_HEAD = (104, 88)

# The smear follows the blade between two frames, from this far along it to its tip, in map pixels.
SMEAR_START = 9
SMEAR_TIP = 28
SMEAR_STEPS = 8
SMEAR_COLOUR = (235, 245, 255)
SMEAR_MAX_ALPHA = 190

# The sheets of the parts besides the hero's own. The name in them is the part's id: its folder in Sets/
# and its entry in the game's list of equipment, so a new part only needs its sheets.
PART_SHEET = re.compile(r"hero-set-(.+?)-(head|torso|legs)(-.+)?-01\.png")
# Every head is on one sheet: the top row faces the viewer, right and away, the bottom row the diagonals.
HEAD_CELLS = {"down": (0, 0), "side": (0, 1), "up": (0, 2), "downside": (1, 0), "upside": (1, 1)}
HEAD_SCALE = 0.135
# The head's canvas, with the helmet point at a fixed place.
HEAD_SIZE = (96, 96)
HEAD_POINT = (48, 40)
# Runs whose head is seated on the torso's neck instead of on the drawn helmet, and the run whose heads
# show how a head sits on its neck. The back diagonal run turns its drawn head aside, so its helmet is
# not above the neck: the head is put on the collar's opening the way the back run's head sits on its own.
NECK_SEATED_RUNS = {"upside": "up"}
# Runs that hold the weapon at their own idle's angle. The back diagonal run was drawn with the sword
# raised, while every other run and its idle hold it pointing down.
RUNS_HELD_AS_IDLE = ("upside",)
# The collar's opening: dark pixels in the top part of the torso.
COLLAR_SHARE = 0.4
COLLAR_DARKNESS = 60
# The template for new parts: the rows of the bare head and of the standing torso and legs, and where
# each goes. Keeping them apart lets one generation redraw all three.
TEMPLATE = "hero-part-template-01.png"
TEMPLATE_ROWS = (
    ("hero-set-bare-head-01.png", 100, 350, 20),
    ("hero-torso-idle-01.png", 385, 675, 300),
    ("hero-legs-idle-01.png", 535, 785, 620),
)
# How far a part's frame is searched for around where its sheet says it is, in map pixels.
FIT_SEARCH = 14
# How much of a torso must lie over the legs, and of another torso over the hero's, in map pixels and as a share.
MIN_WAIST_OVERLAP = 6
MIN_OUTLINE_OVERLAP = 0.7
# Legs are thin, so a boot of another shape changes much of their outline.
MIN_LEGS_OVERLAP = 0.6
# How much lower than the hero's another part's lunge legs may start, in map pixels.
MAX_LUNGE_DROP = 2

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
# item: slot, row and column of the drawing, and its whole height on the map. The drawing's tip points down. It
# is cut into the pommel (SWORD_POMMEL_ROWS) and the guard with the blade (from SWORD_GUARD_ROW); the grip
# between them stays in the fist and is not drawn. Each piece turns about the point where it meets the fist.
ROTATED_ITEMS = {
    "sword": ("MainHand", 0, 0, 34),
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


def key_part(image):
    """Only pixels close to the pure background go, so a part may be purple."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, _ = pixels[x, y]
            if (255 - r) + g + (255 - b) < 150:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def helmet_centre(item):
    """Horizontal centre of the top of the sprite, which does not move with the legs."""
    alpha = item.getchannel("A")
    top = alpha.crop((0, 0, item.width, max(1, item.height // 5)))
    left, _, right, _ = top.getbbox()
    return (left + right) / 2


def helmet_point(item, scale):
    """A fixed place on the helmet: the middle of its first wide row, even with an arm raised beside it."""
    pixels = item.load()
    width = HELMET_MIN_WIDTH / scale
    for y in range(item.height):
        start = None
        for x in range(item.width + 1):
            r, g, b, a = pixels[x, y] if x < item.width else (0, 0, 0, 0)
            grey = a > 0 and max(r, g, b) - min(r, g, b) <= HELMET_MAX_TINT
            if grey and start is None:
                start = x
            elif not grey and start is not None:
                if x - start >= width:
                    return (start + x) / 2, y
                start = None
    raise ValueError("no helmet found")


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


def drop_specks(cell):
    """Clear the small islands a background key leaves, so they do not stretch the sprite's box."""
    alpha = cell.getchannel("A").load()
    pixels = cell.load()
    width, height = cell.size
    seen = set()
    for y in range(height):
        for x in range(width):
            if not alpha[x, y] or (x, y) in seen:
                continue
            group = [(x, y)]
            seen.add((x, y))
            queue = deque(group)
            while queue:
                cx, cy = queue.popleft()
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < width and 0 <= ny < height and alpha[nx, ny] and (nx, ny) not in seen:
                        seen.add((nx, ny))
                        group.append((nx, ny))
                        queue.append((nx, ny))
            if len(group) < MAX_SPECK:
                for gx, gy in group:
                    pixels[gx, gy] = (0, 0, 0, 0)
    return cell


def cut(keyed, rows, columns, row, column, clean=True):
    """The sprite in one grid cell, trimmed to its visible pixels, and where it sits on the sheet.

    Bodies are cleaned of specks. Gear is not: a bow string is thin and stands apart from the bow.
    """
    box = cell_box(keyed, rows, columns, row, column)
    cell = keyed.crop(box)
    if clean:
        drop_specks(cell)
    inner = cell.getchannel("A").getbbox()
    return cell.crop(inner), (box[0] + inner[0], box[1] + inner[1])


def place(item, scale, name, rise=0):
    """Shrink a sprite onto the shared cell, with its helmet centred and its feet on the baseline, or rise above it."""
    centre = helmet_centre(item) * scale
    small = shrink(item, scale)
    canvas = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT), (0, 0, 0, 0))
    left = round(CELL_WIDTH / 2 - centre)
    top = CELL_HEIGHT - FOOT_MARGIN - small.height - rise
    if top + small.height > CELL_HEIGHT:
        raise ValueError(f"{name} reaches {top + small.height - CELL_HEIGHT} pixels below the cell")
    # A drawn leap may reach a little above the cell; it is lowered to fit, by at most MAX_LEAP_TRIM pixels.
    if -MAX_LEAP_TRIM <= top < 0:
        top = 0
    if left < 0 or top < 0 or left + small.width > CELL_WIDTH:
        raise ValueError(f"{name} does not fit: {small.size} at {left},{top}")
    canvas.alpha_composite(small, (left, top))
    return canvas, (left, top)


def same_place(sheet, box, origin, offset, scale, size=(CELL_WIDTH, CELL_HEIGHT)):
    """One cell of another sheet of the same poses, sitting exactly where the body frame sits."""
    small = shrink(sheet.crop(box), scale)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    left = round(offset[0] - (origin[0] - box[0]) * scale)
    top = round(offset[1] - (origin[1] - box[1]) * scale)
    canvas.paste(small, (left, top), small)
    return canvas


def extent(cells):
    """How far a shape stretches in sheet pixels."""
    xs = [cell[0] for cell in cells]
    ys = [cell[1] for cell in cells]
    return math.hypot(max(xs) - min(xs), max(ys) - min(ys))


def thinness(cells):
    """How many times more a shape spreads along its long side than across it."""
    count = len(cells)
    mean_x = sum(cell[0] for cell in cells) / count
    mean_y = sum(cell[1] for cell in cells) / count
    xx = sum((cell[0] - mean_x) ** 2 for cell in cells) / count
    yy = sum((cell[1] - mean_y) ** 2 for cell in cells) / count
    xy = sum((cell[0] - mean_x) * (cell[1] - mean_y) for cell in cells) / count
    middle = (xx + yy) / 2
    spread = math.hypot((xx - yy) / 2, xy)
    return (middle + spread) / max(middle - spread, 1)


def added_shapes(held, body, belongs):
    """Every shape of chosen pixels that the sheet with the sword has and the empty-handed one lacks."""
    held_pixels, body_pixels = held.load(), body.load()
    mask = set()
    for y in range(held.height):
        for x in range(held.width):
            r, g, b, a = held_pixels[x, y]
            if a == 0 or not belongs(r, g, b):
                continue
            r2, g2, b2, a2 = body_pixels[x, y]
            if a2 == 0 or abs(r - r2) + abs(g - g2) + abs(b - b2) > BLADE_MIN_CHANGE:
                mask.add((x, y))
    shapes = []
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
        shapes.append(cells)
    return shapes


def added_cells(held, body, belongs):
    """The largest of those shapes."""
    return max(added_shapes(held, body, belongs), key=len, default=[])


def own_blade(shapes, own):
    """The blade of this cell: of the long thin shapes, the one with the most pixels inside it."""
    def inside(cells):
        return sum(1 for x, y in cells if own[0] <= x < own[2] and own[1] <= y < own[3])

    blades = [cells for cells in shapes if extent(cells) >= MIN_BLADE_LENGTH and thinness(cells) >= MIN_BLADE_THINNESS]
    return max(blades, key=inside, default=[])


def is_blade(r, g, b):
    return max(r, g, b) - min(r, g, b) <= BLADE_MAX_TINT and max(r, g, b) >= BLADE_MIN_BRIGHTNESS


def is_shield(r, g, b):
    return b >= r + SHIELD_MIN_BLUE and b >= g + SHIELD_MIN_BLUE


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


def read_held(body, held, rows, columns, row, column, scale, name, rise=0):
    """Cut one body frame and read from the sheet with the sword how its hand holds a weapon."""
    item, origin = cut(body, rows, columns, row, column)
    frame, offset = place(item, scale, name, rise)
    box = cell_box(body, rows, columns, row, column)
    chest = (origin[0] - box[0] + item.width / 2, origin[1] - box[1] + item.height * 0.45)
    cells = added_cells(held.crop(box), body.crop(box), is_blade)
    if not cells:
        raise ValueError(f"{name}: no blade found")

    def on_frame(point):
        return (offset[0] + (point[0] + box[0] - origin[0]) * scale, offset[1] + (point[1] + box[1] - origin[1]) * scale)

    near, direction = blade_line(cells, chest)
    start = on_frame(near)
    head = helmet_point(item, scale)
    return {
        "frame": frame,
        "placement": (box, origin, offset, scale),
        "held": same_place(held, box, origin, offset, scale),
        "grip": (start[0] - direction[0] * GRIP_TO_BLADE, start[1] - direction[1] * GRIP_TO_BLADE),
        "angle": math.degrees(math.atan2(-direction[1], direction[0])) % 360,
        "head": (offset[0] + head[0] * scale, offset[1] + head[1] * scale),
    }


def read_upper(body, held, rows, columns, row, column, scale, name):
    """Cut one upper body onto its canvas and read the sword and the shield from the sheet that holds them."""
    box = cell_box(body, rows, columns, row, column)
    cell, held_cell = body.crop(box), held.crop(box)
    inner = cell.getchannel("A").getbbox()
    item = cell.crop(inner)
    head = helmet_point(item, scale)
    origin = (box[0] + inner[0], box[1] + inner[1])
    offset = (UPPER_HEAD[0] - head[0] * scale, UPPER_HEAD[1] - head[1] * scale)
    window = (
        max(0, box[0] - BLADE_SEARCH_X), max(0, box[1] - BLADE_SEARCH_Y),
        min(body.width, box[2] + BLADE_SEARCH_X), min(body.height, box[3] + BLADE_SEARCH_Y),
    )
    # The blade is found in the wider window, so its points are moved back to the cell afterwards.
    shift = (box[0] - window[0], box[1] - window[1])
    own = (shift[0], shift[1], shift[0] + box[2] - box[0], shift[1] + box[3] - box[1])
    blade = own_blade(added_shapes(held.crop(window), body.crop(window), is_blade), own)
    shield = added_cells(held_cell, cell, is_shield)
    if not blade or not shield:
        raise ValueError(f"{name}: no blade or no shield found")

    def on_canvas(point):
        return (offset[0] + (point[0] - inner[0]) * scale, offset[1] + (point[1] - inner[1]) * scale)

    chest = (inner[0] + head[0] + shift[0], inner[1] + head[1] + item.height * 0.4 + shift[1])
    near, direction = blade_line(blade, chest)
    start = on_canvas((near[0] - shift[0], near[1] - shift[1]))
    size = (UPPER_WIDTH, UPPER_HEIGHT)
    return {
        "frame": same_place(body, box, origin, offset, scale, size),
        "placement": (box, origin, offset, scale),
        "held": same_place(held, box, origin, offset, scale, size),
        "grip": (start[0] - direction[0] * GRIP_TO_BLADE, start[1] - direction[1] * GRIP_TO_BLADE),
        "angle": math.degrees(math.atan2(-direction[1], direction[0])) % 360,
        "shield": on_canvas((
            (min(cell[0] for cell in shield) + max(cell[0] for cell in shield)) / 2,
            (min(cell[1] for cell in shield) + max(cell[1] for cell in shield)) / 2,
        )),
    }


def best_fit(small, target, start, colour):
    """Where a part matches a frame of the same pose best, near the place its sheet says.

    With colour the pixels must also look alike, which is for a part of the same outfit as the frame.
    """
    part, whole = small.load(), target.load()
    width, height = target.size
    cells = [(x, y) for y in range(small.height) for x in range(small.width) if part[x, y][3]]
    best = None
    for shift_y in range(-FIT_SEARCH, FIT_SEARCH + 1):
        for shift_x in range(-FIT_SEARCH, FIT_SEARCH + 1):
            left, top = round(start[0]) + shift_x, round(start[1]) + shift_y
            score = 0
            for x, y in cells:
                cell_x, cell_y = x + left, y + top
                if not (0 <= cell_x < width and 0 <= cell_y < height) or whole[cell_x, cell_y][3] == 0:
                    continue
                if not colour:
                    score += 1
                    continue
                other, mine = whole[cell_x, cell_y], part[x, y]
                score += 2 if abs(other[0] - mine[0]) + abs(other[1] - mine[1]) + abs(other[2] - mine[2]) < 120 else -1
            key = (score, -abs(shift_x) - abs(shift_y))
            if best is None or key > best[0]:
                best = (key, left, top)
    return best[1], best[2]


def place_part(sheet, rows, columns, row, column, placement, target, colour, on_feet=False):
    """One cell of a part's sheet on the canvas of a frame of the same pose, where it fits that frame.

    Legs stand on the feet of the frame, because their sheet can draw them a little longer than the body.
    """
    box, origin, offset, scale = placement
    item, part_origin = cut(sheet, rows, columns, row, column)
    small = shrink(item, scale)
    start = (offset[0] + (part_origin[0] - origin[0]) * scale, offset[1] + (part_origin[1] - origin[1]) * scale)
    left, top = best_fit(small, target, start, colour)
    if on_feet:
        top = target.getchannel("A").getbbox()[3] - small.height
    canvas = Image.new("RGBA", target.size, (0, 0, 0, 0))
    canvas.paste(small, (left, top), small)
    return canvas


def read_lunge(keyed, rows, row, column, scale, view, name):
    """Put one frame of the lunge's legs on its canvas. Returns it and the helmet's place, from the foot."""
    small = shrink(cut(keyed, rows, SWING_FRAMES, row, column)[0], scale)
    belt = small.crop((0, 0, small.width, BELT_ROWS)).getchannel("A").getbbox()
    radians = math.radians(FACING[view])
    step_x, step_y = math.cos(radians), -math.sin(radians) * GROUND_DEPTH
    hips_x = LUNGE_FOOT[0] + step_x * LUNGE_REACH[column]
    left = round(hips_x - (belt[0] + belt[2]) / 2)
    top = round(LUNGE_FOOT[1] + step_y * LUNGE_REACH[column] - small.height)
    if left < 0 or top < 0 or left + small.width > LUNGE_WIDTH or top + small.height > LUNGE_HEIGHT:
        raise ValueError(f"{name} does not fit: {small.size} at {left},{top}")
    canvas = Image.new("RGBA", (LUNGE_WIDTH, LUNGE_HEIGHT), (0, 0, 0, 0))
    canvas.alpha_composite(small, (left, top))
    head_x = hips_x + step_x * LUNGE_LEAN[column]
    head_y = top - LUNGE_RISE[column] + step_y * LUNGE_LEAN[column]
    return canvas, {"x": round(head_x - LUNGE_FOOT[0], 1), "y": round(LUNGE_FOOT[1] - head_y, 1)}


def fit_to(frame, target):
    """Move a part's frame to where it matches the hero's frame of the same pose best, on the same feet."""
    box = frame.getchannel("A").getbbox()
    small = frame.crop(box)
    left, _ = best_fit(small, target, box[:2], False)
    top = target.getchannel("A").getbbox()[3] - small.height
    canvas = Image.new("RGBA", target.size, (0, 0, 0, 0))
    canvas.paste(small, (left, top), small)
    return canvas


def build_lunge(part="hero", folder=HERO):
    """Write the legs of the standing swing. Returns the helmet's place on each frame.

    Another part's legs stand where the hero's do, so the hero's helmet places carry any torso over them.
    The tool stops if they start lower than the hero's, where the torso would no longer cover the waist.
    """
    stances = []
    problems = []
    for file, views, scale in LUNGE_SHEETS:
        sheet = file if part == "hero" else file.replace("hero-legs-", f"hero-set-{part}-legs-")
        keyed = key_part(Image.open(os.path.join(FOREST, sheet)))
        for row, view in enumerate(views):
            for column in range(SWING_FRAMES):
                name = f"hero-legs-lunge-{view}-{column}"
                frame, head = read_lunge(keyed, len(views), row, column, scale, view, name)
                if part != "hero":
                    knight = Image.open(os.path.join(HERO, name + ".png")).convert("RGBA")
                    frame = fit_to(frame, knight)
                    drop = frame.getchannel("A").getbbox()[1] - knight.getchannel("A").getbbox()[1]
                    if drop > MAX_LUNGE_DROP:
                        problems.append(f"{name}: starts {drop} pixels lower than the hero's")
                frame.save(os.path.join(folder, name + ".png"))
                stances.append({"name": name, "head": head})
    if problems:
        raise ValueError(f"the lunge legs of {part} do not reach the torso: " + "; ".join(problems))
    return stances


def smear_sprite(before, after):
    """The band the blade sweeps between two frames, fading towards the older end."""
    canvas = Image.new("RGBA", (UPPER_WIDTH, UPPER_HEIGHT), (0, 0, 0, 0))
    pen = ImageDraw.Draw(canvas)
    turn = (after["angle"] - before["angle"] + 180) % 360 - 180

    def blade_point(share, reach):
        radians = math.radians(before["angle"] + turn * share)
        x = before["grip"][0] + (after["grip"][0] - before["grip"][0]) * share
        y = before["grip"][1] + (after["grip"][1] - before["grip"][1]) * share
        return x + math.cos(radians) * reach, y - math.sin(radians) * reach

    for index in range(SMEAR_STEPS):
        first, last = index / SMEAR_STEPS, (index + 1) / SMEAR_STEPS
        strength = 0.35 + 0.65 * last
        pen.polygon(
            [blade_point(first, SMEAR_TIP), blade_point(last, SMEAR_TIP), blade_point(last, SMEAR_START), blade_point(first, SMEAR_START)],
            fill=(*SMEAR_COLOUR, int(SMEAR_MAX_ALPHA * strength)))
    return canvas


def load_with_held(file):
    held_file = file.replace("hero-body-", "hero-")
    body = key_magenta(Image.open(os.path.join(FOREST, file)))
    held = key_magenta(Image.open(os.path.join(FOREST, held_file)))
    if held.size != body.size:
        raise ValueError(f"{held_file} is not the size of {file}")
    return body, held


def load_part(file, kind, part="hero"):
    """The sheet of a part for the pose of a hero-body sheet."""
    pose = file.replace("hero-body-upper-swing", "swing").replace("hero-body-", "")
    name = f"hero-{kind}-{pose}" if part == "hero" else f"hero-set-{part}-{kind}-{pose}"
    return key_part(Image.open(os.path.join(FOREST, name)))


def upper_point(point):
    """A place on the upper body's canvas, in pixels from the helmet point, with y going up."""
    return {"x": round(point[0] - UPPER_HEAD[0], 1), "y": round(UPPER_HEAD[1] - point[1], 1)}


def read_like(body, held, placement, name):
    """Cut a frame of a redrawn run where the common run's frame sits, and read how its hand holds the sword."""
    box, origin, offset, scale = placement
    cell = body.crop(box)
    inner = cell.getchannel("A").getbbox()
    chest = ((inner[0] + inner[2]) / 2, inner[1] + (inner[3] - inner[1]) * 0.45)
    cells = added_cells(held.crop(box), cell, is_blade)
    if not cells:
        raise ValueError(f"{name}: no blade found")
    near, direction = blade_line(cells, chest)
    start = (offset[0] + (near[0] + box[0] - origin[0]) * scale, offset[1] + (near[1] + box[1] - origin[1]) * scale)
    return {
        "frame": same_place(body, box, origin, offset, scale),
        "placement": placement,
        "grip": (start[0] - direction[0] * GRIP_TO_BLADE, start[1] - direction[1] * GRIP_TO_BLADE),
        "angle": math.degrees(math.atan2(-direction[1], direction[0])) % 360,
    }


def build_body():
    """Measure the whole hero and write the hands. Returns its frames, anchors, where each sits on its sheet, and
    for every frame where the sheet with the sword holds the grip, the blade's angle, the view and the kind of pose."""
    os.makedirs(HERO, exist_ok=True)
    os.makedirs(HAND, exist_ok=True)
    os.makedirs(SMEAR, exist_ok=True)
    frames = {}
    placements = {}
    idles = {}
    grips = {}
    data = {"frames": [], "stances": build_lunge()}

    def add(name, read, view, hands, angle, kind):
        frame = read["frame"]
        frames[name] = frame
        placements[name] = read["placement"]
        grips[name] = (read["grip"], angle, view, kind)
        main_hand, off_hand = hands
        fist_sprite(frame, main_hand).save(os.path.join(HAND, name + "-main.png"))
        fist_sprite(frame, off_hand).save(os.path.join(HAND, name + "-off.png"))
        anchors = anchor_data(frame, view, main_hand, off_hand, angle)
        foot_x, foot_y = CELL_WIDTH / 2, CELL_HEIGHT - FOOT_MARGIN
        anchors["head"] = {"x": round(read["head"][0] - foot_x, 1), "y": round(foot_y - read["head"][1], 1)}
        data["frames"].append({"name": name, **anchors})

    for file, views, scale, columns, flights in RUN_SHEETS:
        body, held = load_with_held(file)
        sword_body, sword_held = load_with_held(SWORD_RUN_SHEETS[file])
        for row, view in enumerate(views):
            names = [f"hero-{view}-{column}" for column in range(columns)]
            # Every frame is cut with its feet on the line, so a leap is lifted to leave the ground.
            rises = [FLIGHT_RISE if column in flights else 0 for column in range(columns)]
            read = [
                read_held(body, held, len(views), columns, row, column, scale, names[column], rises[column])
                for column in range(columns)]
            # One angle for the whole run: the weapon moves with the hand and never swings on its own.
            angle = round(statistics.median(item["angle"] for item in read), 1)
            for name, item in zip(names, read):
                add(name, item, view, find_hands(item["frame"], view, name), angle, "run")
            # The sword type's run sits where the common run sits, and keeps its head.
            sword = [read_like(sword_body, sword_held, item["placement"], name) for name, item in zip(names, read)]
            sword_angle = round(statistics.median(item["angle"] for item in sword), 1)
            for column, (item, common) in enumerate(zip(sword, read)):
                name = f"hero-sword-{view}-{column}"
                item["head"] = common["head"]
                add(name, item, view, find_hands(item["frame"], view, name), sword_angle, "run")
    for file, views, scale in IDLE_SHEETS:
        body, held = load_with_held(file)
        for column, view in enumerate(views):
            name = f"hero-idle-{view}"
            item = read_held(body, held, 1, len(views), 0, column, scale, name)
            add(name, item, view, find_hands(item["frame"], view, name), round(item["angle"], 1), "idle")
            idles[view] = item
    for view in RUNS_HELD_AS_IDLE:
        angle = round(idles[view]["angle"], 1)
        for entry in data["frames"]:
            name = entry["name"]
            if re.fullmatch(rf"hero-{view}-\d+", name):
                entry["mainHandAngle"] = angle

    entries = {entry["name"]: entry for entry in data["frames"]}
    problems = []
    for file, views, scale in SWING_SHEETS:
        body, held = load_with_held(file)
        for row, view in enumerate(views):
            names = [f"hero-upper-swing-{view}-{index}" for index in range(SWING_FRAMES)]
            read = [read_upper(body, held, len(views), SWING_FRAMES, row, column, scale, name) for name, column in zip(names, SWING_ORDER)]
            problems += swing_problems(view, read)
            # The shield hand stays at one place for the whole swing.
            off_hand = (statistics.mean(item["shield"][0] for item in read), statistics.mean(item["shield"][1] for item in read))
            idle = entries[f"hero-idle-{view}"]
            back = {"x": round(idle["back"]["x"] - idle["head"]["x"], 1), "y": round(idle["back"]["y"] - idle["head"]["y"], 1)}
            for column, (name, item) in enumerate(zip(names, read)):
                frames[name] = item["frame"]
                placements[name] = item["placement"]
                grips[name] = (item["grip"], round(item["angle"], 1), view, "swing")
                main_hand = glove_near(item["frame"], item["grip"])
                fist_sprite(item["frame"], main_hand).save(os.path.join(HAND, name + "-main.png"))
                fist_sprite(item["frame"], off_hand).save(os.path.join(HAND, name + "-off.png"))
                if column > 0:
                    smear_sprite(read[column - 1], item).save(os.path.join(SMEAR, name + ".png"))
                data["frames"].append({
                    "name": name,
                    "mainHand": upper_point(main_hand),
                    "offHand": upper_point(off_hand),
                    "back": back,
                    "mainHandAngle": round(item["angle"], 1),
                    "head": {"x": 0, "y": 0},
                })
    if problems:
        raise ValueError("the swing does not fit: " + "; ".join(problems))

    with open(os.path.join(HERO, "anchors.json"), "w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, indent=1)
        file.write("\n")
    print("whole frames", len(frames))
    return frames, {entry["name"]: entry for entry in data["frames"]}, placements, grips


def swing_problems(view, read):
    """Check one view of the swing: each blade on the half circle, and a shield that stays put."""
    problems = []
    angles = [round(item["angle"]) for item in read]
    wanted = [(FACING[view] + offset) % 360 for offset in SWING_ANGLES]
    shields = [item["shield"] for item in read]
    moved = max(math.dist(a, b) for a in shields for b in shields)
    print(f"swing {view}: blade {angles}, wanted {wanted}, shield moved {moved:.1f}")
    for column, (angle, goal) in enumerate(zip(angles, wanted)):
        if abs((angle - goal + 180) % 360 - 180) > MAX_BLADE_ERROR:
            problems.append(f"{view} frame {column}: blade at {angle}, wanted {goal}")
    if moved > MAX_SHIELD_MOVE:
        problems.append(f"{view}: shield moved {moved:.1f}")
    return problems


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


def is_skin(pixel):
    r, g, b, a = pixel
    return a > 0 and r > 200 and 140 < g < 225 and 70 < b < 170 and r > g > b


def chin_row(frame):
    """The lowest row of skin, which is the chin of a head seen from the front."""
    pixels = frame.load()
    return max(y for y in range(frame.height) for x in range(frame.width) if is_skin(pixels[x, y]))


def head_sheet_name(part):
    return "hero-head-01.png" if part == "hero" else f"hero-set-{part}-head-01.png"


def build_heads(part, folder, knight):
    """Write the heads of a part around the helmet point. Returns the knight's placing for the next parts."""
    sheet = key_part(Image.open(os.path.join(FOREST, head_sheet_name(part))))
    if knight is None:
        knight = {}
        for view, (row, column) in HEAD_CELLS.items():
            box = cell_box(sheet, 2, 3, row, column)
            item, origin = cut(sheet, 2, 3, row, column)
            point = helmet_point(item, HEAD_SCALE)
            offset = (HEAD_POINT[0] - point[0] * HEAD_SCALE, HEAD_POINT[1] - point[1] * HEAD_SCALE)
            knight[view] = (box, origin, offset)
    placed = {view: same_place(sheet, *knight[view], HEAD_SCALE, HEAD_SIZE) for view in HEAD_CELLS}
    if part == "hero":
        knight["chin"] = chin_row(placed["down"])
    drop = knight["chin"] - chin_row(placed["down"])
    print(f"head {part}: chin moved {drop}")
    for view, frame in placed.items():
        moved = Image.new("RGBA", HEAD_SIZE, (0, 0, 0, 0))
        moved.paste(frame, (0, drop))
        moved.save(os.path.join(folder, f"hero-head-{view}.png"))
    return knight


def each_pose():
    """Every frame of the body cell: its sheet, grid, name in the whole hero and name of the part's frame."""
    for file, views, scale, columns, flights in RUN_SHEETS:
        for row, view in enumerate(views):
            for column in range(columns):
                yield file, len(views), columns, row, column, f"hero-{view}-{column}", f"{view}-{column}"
    for file, views, scale in IDLE_SHEETS:
        for column, view in enumerate(views):
            yield file, 1, len(views), 0, column, f"hero-idle-{view}", f"idle-{view}"


def each_swing():
    """Every frame of the swing in the order the game plays it, with the column of the sheet it comes from."""
    for file, views, scale in SWING_SHEETS:
        for row, view in enumerate(views):
            for index, column in enumerate(SWING_ORDER):
                yield file, len(views), SWING_FRAMES, row, column, f"hero-upper-swing-{view}-{index}", f"swing-{view}-{index}"


def build_torso(part, folder, frames, placements, knight):
    """Write the torso frames of a part. The hero's fit the whole hero, the others fit the hero's torso."""
    sheets = {}
    placed = {}
    lowest = 1
    for file, rows, columns, row, column, whole, name in [*each_pose(), *each_swing()]:
        if file not in sheets:
            sheets[file] = load_part(file, "torso", part)
        target = frames[whole] if knight is None else knight[whole]
        frame = place_part(sheets[file], rows, columns, row, column, placements[whole], target, knight is None)
        placed[whole] = frame
        frame.save(os.path.join(folder, f"hero-torso-{name}.png"))
        if knight is not None:
            lowest = min(lowest, outline_overlap(knight[whole], frame))
    if knight is not None:
        print(f"torso {part}: overlap {lowest:.2f} or more")
        if lowest < MIN_OUTLINE_OVERLAP:
            raise ValueError(f"{part} does not fit the hero: overlap {lowest:.2f}")
    return placed


def build_legs(part, folder, frames, placements, torso, knight):
    """Write the legs of a part, and stop if a torso would not lie over them.

    The hero's fit the whole hero. Every other part fits the hero's legs and stands on the same feet,
    and brings the legs of the standing swing too. The hero's are written with the hero's anchors.
    """
    sheets = {}
    placed = {}
    names = {}
    problems = []
    lowest = 1
    for file, rows, columns, row, column, whole, name in each_pose():
        if file not in sheets:
            sheets[file] = load_part(file, "legs", part)
        target = frames[whole] if knight is None else knight[whole]
        placed[whole] = place_part(sheets[file], rows, columns, row, column, placements[whole], target, knight is None, True)
        names[whole] = name
    mirror_second_step(placed)
    for whole, frame in placed.items():
        frame.save(os.path.join(folder, f"hero-legs-{names[whole]}.png"))
        over = torso[whole].getchannel("A").getbbox()[3] - frame.getchannel("A").getbbox()[1]
        if over < MIN_WAIST_OVERLAP:
            problems.append(f"{whole}: the torso lies {over} pixels over the legs")
        if knight is not None:
            lowest = min(lowest, outline_overlap(knight[whole], frame))
    if problems:
        raise ValueError(f"the legs of {part} do not reach the torso: " + "; ".join(problems))
    if knight is not None:
        print(f"legs {part}: overlap {lowest:.2f} or more")
        if lowest < MIN_LEGS_OVERLAP:
            raise ValueError(f"the legs of {part} do not fit the hero: overlap {lowest:.2f}")
        build_lunge(part, folder)
    return placed


def mirror_second_step(placed):
    """In the front and back runs, give the second step the first step's legs, mirrored on the hips."""
    for view in MIRRORED_STEP_VIEWS:
        count = len([name for name in placed if re.fullmatch(f"hero-{view}-[0-9]+", name)])
        step = count // 2
        for column in range(step, count):
            first = placed[f"hero-{view}-{column - step}"]
            placed[f"hero-{view}-{column}"] = first.transpose(Image.Transpose.FLIP_LEFT_RIGHT)


def write_template():
    """The head, the torso and the legs apart on one sheet: the shape every new part must keep."""
    sheet = Image.new("RGB", (1536, 1024), (255, 0, 255))
    for file, top, bottom, target in TEMPLATE_ROWS:
        rows = Image.open(os.path.join(FOREST, file)).convert("RGB").crop((0, top, 1536, bottom))
        sheet.paste(rows, (0, target))
    sheet.save(os.path.join(FOREST, TEMPLATE))


def find_parts():
    """Every part on a sheet, with the layers it has."""
    parts = {}
    for file in sorted(os.listdir(FOREST)):
        match = PART_SHEET.fullmatch(file)
        if match:
            parts.setdefault(match.group(1), set()).add(match.group(2))
    return parts


def collar_opening(torso):
    """The middle of the collar's opening: the biggest dark patch in the top part of the torso."""
    pixels = torso.load()
    box = torso.getchannel("A").getbbox()
    bottom = int(box[1] + (box[3] - box[1]) * COLLAR_SHARE)
    dark = {(x, y) for y in range(box[1], bottom) for x in range(box[0], box[2])
            if pixels[x, y][3] and max(pixels[x, y][:3]) < COLLAR_DARKNESS}
    # The outline is one pixel wide; the opening has dark pixels on both sides in a row or a column.
    inner = {(x, y) for x, y in dark
             if {(x - 1, y), (x + 1, y)} <= dark or {(x, y - 1), (x, y + 1)} <= dark}
    best, seen = [], set()
    for start in sorted(inner):
        if start in seen:
            continue
        group, queue = [], deque([start])
        seen.add(start)
        while queue:
            x, y = queue.popleft()
            group.append((x, y))
            for near in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if near in inner and near not in seen:
                    seen.add(near)
                    queue.append(near)
        if len(group) > len(best):
            best = group
    if not best:
        raise ValueError("no collar opening found")
    xs = [x for x, _ in best]
    ys = [y for _, y in best]
    return (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2


def head_neck(head):
    """Where the neck leaves the head: the middle of the head's lowest three rows, on its own canvas."""
    alpha = head.getchannel("A")
    bottom = alpha.getbbox()[3]
    middles = []
    for y in range(bottom - 3, bottom):
        xs = [x for x in range(head.width) if alpha.getpixel((x, y))]
        middles.append((min(xs) + max(xs)) / 2)
    return statistics.mean(middles), bottom


def seat_heads(torso):
    """Put the head of each run in NECK_SEATED_RUNS on the collar's opening of the knight's torso, and
    rewrite those head points in anchors.json. The head sits on the opening as it does in the model run."""
    path = os.path.join(HERO, "anchors.json")
    with open(path, encoding="utf-8") as file:
        data = json.load(file)
    entries = {entry["name"]: entry for entry in data["frames"]}
    foot_x, foot_y = CELL_WIDTH / 2, CELL_HEIGHT - FOOT_MARGIN

    def neck_on_canvas(view, name):
        neck = head_neck(Image.open(os.path.join(HERO, f"hero-head-{view}.png")).convert("RGBA"))
        head = entries[name]["head"]
        return (foot_x + head["x"] - HEAD_POINT[0] + neck[0], foot_y - head["y"] - HEAD_POINT[1] + neck[1])

    for view, model in NECK_SEATED_RUNS.items():
        model_names = [name for name in entries if re.fullmatch(rf"hero-{model}-\d+", name)]
        gaps = []
        for name in model_names:
            neck, opening = neck_on_canvas(model, name), collar_opening(torso[name])
            gaps.append((neck[0] - opening[0], neck[1] - opening[1]))
        gap = (statistics.mean(x for x, _ in gaps), statistics.mean(y for _, y in gaps))
        head = Image.open(os.path.join(HERO, f"hero-head-{view}.png")).convert("RGBA")
        neck = head_neck(head)
        for name in [name for name in entries if re.fullmatch(rf"hero-{view}-\d+", name)]:
            opening = collar_opening(torso[name])
            point = (opening[0] + gap[0] - neck[0] + HEAD_POINT[0], opening[1] + gap[1] - neck[1] + HEAD_POINT[1])
            entries[name]["head"] = {"x": round(point[0] - foot_x, 1), "y": round(foot_y - point[1], 1)}
        print(f"head {view}: seated on the collar like {model}, gap ({gap[0]:.1f}, {gap[1]:.1f})")
    with open(path, "w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, indent=1)
        file.write("\n")


def glove_near(frame, point):
    """The middle of the empty-handed body's glove around a drawn grip: the fist at the end of the arm."""
    pixels = frame.load()
    glove = [(x, y) for y in range(frame.height) for x in range(frame.width)
             if math.hypot(x - point[0], y - point[1]) <= GLOVE_SNAP and is_glove(pixels[x, y])]
    if not glove:
        return point
    return statistics.mean(x for x, _ in glove) + 0.5, statistics.mean(y for _, y in glove) + 0.5


def fist_only(torso, fist):
    """The middle of the fist alone: the glove near the found middle, from its row down (the forearm is above)."""
    pixels = torso.load()
    glove = [(x + 0.5, y + 0.5) for y in range(torso.height) for x in range(torso.width)
             if math.hypot(x + 0.5 - fist[0], y + 0.5 - fist[1]) <= GLOVE_SNAP and y + 1 >= fist[1] and is_glove(pixels[x, y])]
    if not glove:
        return fist
    return statistics.mean(x for x, _ in glove), statistics.mean(y for _, y in glove)


def fist_ends(torso, fist, direction):
    """How far the fist reaches from its middle along the weapon: towards the tip, and towards the pommel."""
    pixels = torso.load()
    along = [(x + 0.5 - fist[0]) * direction[0] + (y + 0.5 - fist[1]) * direction[1]
             for y in range(torso.height) for x in range(torso.width)
             if math.hypot(x + 0.5 - fist[0], y + 0.5 - fist[1]) <= FIST_CORE and pixels[x, y][3]
             and (is_glove(pixels[x, y]) or max(pixels[x, y][:3]) < 100)]
    if not along:
        return 0.0, 0.0
    return max(along) - FIST_OVERLAP, min(along) + FIST_OVERLAP


def build_sword_torso(part, folder, frames, placements, knight):
    """Write the sword type's run torso of a part that has its sheets, where it fits the sword type's frame.

    The hero's fit the hero's sword frames, every other part's fit the hero's sword torso. Returns the placed
    frames, or nothing when the part has no sword type's run."""
    placed = {}
    lowest = 1
    for file, views, scale, columns, flights in RUN_SHEETS:
        pose = SWORD_RUN_SHEETS[file].replace("hero-body-", "")
        name = f"hero-torso-{pose}" if part == "hero" else f"hero-set-{part}-torso-{pose}"
        if not os.path.exists(os.path.join(FOREST, name)):
            return None
        sheet = load_part(SWORD_RUN_SHEETS[file], "torso", part)
        for row, view in enumerate(views):
            for column in range(columns):
                whole = f"hero-sword-{view}-{column}"
                target = frames[whole] if knight is None else knight[whole]
                frame = place_part(sheet, len(views), columns, row, column, placements[whole], target, knight is None)
                placed[whole] = frame
                frame.save(os.path.join(folder, f"hero-torso-sword-{view}-{column}.png"))
                if knight is not None:
                    lowest = min(lowest, outline_overlap(knight[whole], frame))
    if knight is not None:
        print(f"sword torso {part}: overlap {lowest:.2f} or more")
        if lowest < MIN_OUTLINE_OVERLAP:
            raise ValueError(f"the sword torso of {part} does not fit the hero: overlap {lowest:.2f}")
    return placed


def build_parts(frames, placements):
    """Write every part. Returns the knight's torso frames, the common and the sword type's, by frame name."""
    knight_heads = build_heads("hero", HERO, None)
    knight_torso = build_torso("hero", HERO, frames, placements, None)
    knight_sword = build_sword_torso("hero", HERO, frames, placements, None)
    if knight_sword is None:
        raise ValueError("the knight has no sword type's run torso")
    seat_heads(knight_torso)
    knight_legs = build_legs("hero", HERO, frames, placements, knight_torso, None)
    for part, layers in find_parts().items():
        folder = os.path.join(SETS, part)
        os.makedirs(folder, exist_ok=True)
        if "head" in layers:
            build_heads(part, folder, knight_heads)
        if "torso" in layers:
            build_torso(part, folder, frames, placements, knight_torso)
            build_sword_torso(part, folder, frames, placements, knight_sword)
        if "legs" in layers:
            build_legs(part, folder, frames, placements, knight_torso, knight_legs)
    return {**knight_torso, **knight_sword}


def write_grip_ends(grips, torsos):
    """Measure the fist of every frame on the knight's torso, and write where the sword's two pieces meet it.

    The main hand becomes the middle of the torso's glove near the grip the sheet with the sword draws. The
    fist's two ends are measured along the weapon at the step it is drawn at: mainHandFront towards the tip,
    mainHandBack towards the pommel, in pixels. A fist the torso hides keeps the drawn grip, with both ends
    there. The sword type's frames keep the head of the common run, as seated."""
    path = os.path.join(HERO, "anchors.json")
    with open(path, encoding="utf-8") as file:
        data = json.load(file)
    entries = {entry["name"]: entry for entry in data["frames"]}
    foot_x, foot_y = CELL_WIDTH / 2, CELL_HEIGHT - FOOT_MARGIN
    step = 360 / ROTATION_STEPS
    hidden = []
    for name, (grip, angle, view, kind) in grips.items():
        torso = torsos[name]
        fist = glove_near(torso, grip)
        if fist is grip:
            hidden.append(name)
            front = back = 0.0
        else:
            if kind != "swing" and view in FIST_ONLY_VIEWS:
                fist = fist_only(torso, fist)
            radians = math.radians(round(angle / step) * step)
            front, back = fist_ends(torso, fist, (math.cos(radians), -math.sin(radians)))
            if kind != "swing":
                front, back = front + SWORD_PUSH.get(view, 0.0), back + SWORD_PUSH.get(view, 0.0)
        entry = entries[name]
        entry["mainHand"] = upper_point(fist) if kind == "swing" else {"x": round(fist[0] - foot_x, 1), "y": round(foot_y - fist[1], 1)}
        entry["mainHandFront"] = round(front, 1)
        entry["mainHandBack"] = round(back, 1)
        if name.startswith("hero-sword-"):
            entry["head"] = entries[name.replace("hero-sword-", "hero-")]["head"]
    with open(path, "w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, indent=1)
        file.write("\n")
    print(f"grip ends {len(grips)} frames, fist hidden in {hidden}")


def rotate_item(item, part, height, grip, folder=GEAR):
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
        shrink(rotated, height / part.height).save(os.path.join(folder, name + ".png"))
        names.append(name)
    return names


def build_gear():
    os.makedirs(GEAR, exist_ok=True)
    keyed = key_magenta(Image.open(os.path.join(FOREST, GEAR_SHEET)))
    rows = max(item[1] for item in GEAR_ITEMS.values()) + 1
    data = {"items": []}
    count = 0
    for item, (slot, row, column, height) in ROTATED_ITEMS.items():
        drawing = cut(keyed, rows, len(VIEWS), row, column, clean=False)[0]
        blade = drawing.crop((0, SWORD_GUARD_ROW, drawing.width, drawing.height))
        pommel = drawing.crop((0, SWORD_POMMEL_ROWS[0], drawing.width, SWORD_POMMEL_ROWS[1]))
        # Both pieces keep the whole drawing's scale, and turn about where they meet the fist.
        names = rotate_item(item, blade, height * blade.height / drawing.height, (0.5, 0.0))
        back_names = rotate_item(f"{item}-pommel", pommel, height * pommel.height / drawing.height, (0.5, 1.0))
        count += len(names) + len(back_names)
        data["items"].append({
            "name": item, "slot": slot, "views": [], "rotations": names, "backRotations": back_names, "heldInFist": True})
    for item, (slot, row, height, grips, held_in_fist) in GEAR_ITEMS.items():
        views = []
        for column, view in enumerate(VIEWS):
            part = cut(keyed, rows, len(VIEWS), row, column, clean=False)[0]
            name = f"{item}-{view}"
            shrink(part, height / part.height).save(os.path.join(GEAR, name + ".png"))
            count += 1
            pivot_x, pivot_from_top = grips[view]
            views.append({"view": view, "file": name, "pivotX": pivot_x, "pivotY": round(1 - pivot_from_top, 3)})
        data["items"].append({
            "name": item, "slot": slot, "views": views, "rotations": [], "backRotations": [], "heldInFist": held_in_fist})
    with open(os.path.join(GEAR, "gear.json"), "w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, indent=1)
        file.write("\n")
    print("gear sprites", count)


def preview(path, frames, points):
    """A sheet of every view with its anchors marked: red main hand, blue off hand, yellow back."""
    zoom = 3
    width, height = CELL_WIDTH * zoom, CELL_HEIGHT * zoom
    columns = max(sheet[3] for sheet in RUN_SHEETS)
    sheet = Image.new("RGB", (width * (columns + 1), height * len(VIEWS)), (106, 148, 62))
    draw = ImageDraw.Draw(sheet)
    for row, view in enumerate(VIEWS):
        count = len([name for name in frames if re.fullmatch(f"hero-{view}-[0-9]+", name)])
        names = [f"hero-{view}-{column}" for column in range(count)] + [f"hero-idle-{view}"]
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
    frames, points, placements, grips = build_body()
    torsos = build_parts(frames, placements)
    write_grip_ends(grips, torsos)
    write_template()
    build_gear()
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1], frames, points)


if __name__ == "__main__":
    main()
