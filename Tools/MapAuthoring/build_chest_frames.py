"""Cut the treasure chest and its light into native-scale sprites, with the light's loop in four colours.

Usage:
    python build_chest_frames.py [--preview <webp-path>]

The chest has three states: closed, open with something inside, and open and empty. Opening shows the
half-open chest once, with a burst of light. While something is inside, the open chest glows from inside
and loops FRAMES frames. The light's colour says how good the best thing inside is, and each colour after
white adds one more effect and keeps the ones before it: blue adds motes rising out of the box, purple adds
rays to the sides, and gold adds a pillar of light. Which colour stands for which rarity is decided with the
items' rarities.

Reads from Assets/Art/Generated/ForestLook (each sheet has the knight in its left column, except the last):
    prop-chest-01.png  knight | closed chest | open chest with coins
    prop-chest-03.png  knight | half-open chest | the same chest with light bursting out
    prop-chest-04.png  knight | empty open chest | the same chest glowing inside
    prop-chest-05.png  four glowing chests | + rising motes | + side rays | + pillar of light

Writes to Assets/Art/Generated/ForestLook/Chest:
    chest-closed.png, chest-half.png, chest-open.png
    chest-burst-<colour>.png              the light over the half-open chest
    chest-light-<colour>-<frame>.png      the light over the open chest, frames 0 to FRAMES - 1

Every sprite shares one canvas with the chest's body centred on its bottom row, so a state change swaps
the sprite and the light sits on top at the same position. Sprites are shrunk by the knight's own scale,
so their dots are as coarse as the knight's. Light is the difference between a chest with light and the
same chest without it, drawn on the same sheet. The loop moves the drawn shapes and never redraws them.
The rules behind these choices are in docs/fantasy-royale/product-specs/art/prop-design.md.
Output is deterministic.
"""
import colorsys
import json
import math
import os
import sys

from PIL import Image, ImageDraw

from build_hero_frames import shrink
from forest_objects import key_magenta, main_item

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOREST = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
OUT = os.path.join(FOREST, "Chest")

CLOSED_SHEET = "prop-chest-01.png"
HALF_SHEET = "prop-chest-03.png"
GLOW_SHEET = "prop-chest-04.png"
ADDON_SHEET = "prop-chest-05.png"

# The knight of hero-idle-01.png is 530 pixels tall and is shrunk by 0.135. A sheet that redraws the knight
# is shrunk so its knight comes out the same height.
KNIGHT_HEIGHT = 530
KNIGHT_SCALE = 0.135

CANVAS = (60, 96)  # tall enough for the pillar, wide enough for the side rays
BODY_ROWS = 6  # the chest's lowest rows, whose middle is the chest's centre
PINK = 25  # red and blue both this far above green: a pixel mixed with the magenta background
MIN_SPECK = 4  # light islands smaller than this are left over from the chest's gold and are cleared

COLOURS = ("white", "blue", "purple", "gold")
# Each colour keeps the effects of the colours before it and adds one more.
EFFECTS = {"white": (), "blue": ("motes",), "purple": ("motes", "rays"), "gold": ("motes", "rays", "pillar")}
# The burst and the effects keep their brightness and the share of their saturation, and take a new hue
# (None keeps the drawn hue) and a factor on the saturation. The white core has almost no saturation, so
# it stays white in every colour. White keeps little colour, so it does not look like gold.
HUES = {"white": (None, 0.4), "blue": (205, 1.15), "purple": (282, 1.3), "gold": (44, 3.0)}
# The inner glow keeps the wood and adds this colour by how much brighter the glowing chest is than the
# empty one. Recolouring its hue made the box look filled with coloured liquid.
TINTS = {"white": (255, 248, 225), "blue": (110, 195, 255), "purple": (205, 130, 255), "gold": (255, 205, 70)}
GLOW_GAIN = 25  # pixels at least this much brighter than the empty chest are lit
GLOW_FULL = 150  # brightening at which the pixel takes the full light colour
CORE_LUM = 225  # brighter than this is the white core
CORE_PULSE = 120  # how far the core's threshold rises as the glow dims, so the core shrinks
# The light sits in the box. The inside of the lid is only lit faintly from below: just above the box's
# back edge, and at the lid's top. Lit as brightly as the box, the light looked like two stacked panels.
LID_LIGHT = (0.45, 0.15)

FRAMES = 12  # one loop, at FRAME_MS a frame
FRAME_MS = 100
PULSE = (0.82, 1.0)  # the glow's weakest and strongest
MOTE_START = 75  # canvas row where a mote appears, inside the light in the box
MOTE_STEP = 3  # rows a mote rises each frame
RAY_SHORTEST = 0.55  # a ray's shortest length, as a share of its drawn length
PILLAR_DROP = 4  # rows the pillar's top falls at its lowest
SMALL = 6  # islands this size or smaller are dots: they blink instead of moving


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def column(keyed, index, count):
    step = keyed.width / count
    return keyed.crop((round(index * step), 0, round((index + 1) * step), keyed.height))


def knight_scale(keyed):
    return KNIGHT_SCALE * KNIGHT_HEIGHT / main_item(column(keyed, 0, 3)).height


def clear_pink(image):
    image = image.copy()
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a and r - g > PINK and b - g > PINK:
                pixels[x, y] = (0, 0, 0, 0)
    return image


def shrink_clean(item, scale):
    """Like shrink, but colours are averaged over opaque pixels only. Light has no outline, and the black
    of transparent pixels would muddy its edges."""
    size = (max(1, round(item.width * scale)), max(1, round(item.height * scale)))
    alpha = item.getchannel("A")
    colour = item.convert("RGB").resize(size, Image.BOX)
    weight = alpha.resize(size, Image.BOX)
    c, w = colour.load(), weight.load()
    for y in range(size[1]):
        for x in range(size[0]):
            if w[x, y]:
                r, g, b = c[x, y]
                c[x, y] = tuple(min(255, v * 255 // w[x, y]) for v in (r, g, b))
    colour.putalpha(weight.point(lambda v: 255 if v >= 150 else 0))
    return colour


def chest_frame(image, rows):
    """Left, right and bottom of the chest, from its lowest rows, where no light is drawn."""
    alpha = image.getchannel("A")
    bottom = alpha.getbbox()[3]
    band = alpha.crop((0, bottom - rows, image.width, bottom)).getbbox()
    return band[0], band[2], bottom


def window(image, frame, margin, width, height):
    """A crop that starts at the same point relative to its chest, so the shrunk grids line up."""
    left, _right, bottom = frame
    return image.crop((left - margin, bottom - height, left - margin + width, bottom))


def clear_specks(image):
    alpha = image.getchannel("A").load()
    pixels = image.load()
    seen = set()
    for y in range(image.height):
        for x in range(image.width):
            if alpha[x, y] == 0 or (x, y) in seen:
                continue
            island, stack = [], [(x, y)]
            seen.add((x, y))
            while stack:
                px, py = stack.pop()
                island.append((px, py))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = px + dx, py + dy
                        if 0 <= nx < image.width and 0 <= ny < image.height and alpha[nx, ny] and (nx, ny) not in seen:
                            seen.add((nx, ny))
                            stack.append((nx, ny))
            if len(island) < MIN_SPECK:
                for px, py in island:
                    pixels[px, py] = (0, 0, 0, 0)
    return image


def light_layer(plain, lit, min_lum=150, min_gain=40):
    """Opaque pixels of the lit chest that are bright and either outside the plain chest or much brighter."""
    out = Image.new("RGBA", lit.size)
    p, l, o = plain.load(), lit.load(), out.load()
    for y in range(lit.height):
        for x in range(lit.width):
            a = l[x, y]
            if a[3] == 0 or lum(a) < min_lum:
                continue
            b = p[x, y]
            if b[3] == 0 or lum(a) - lum(b) > min_gain:
                o[x, y] = a
    return out


def recolour(light, colour):
    hue, saturation = HUES[colour]
    out = light.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a:
                h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                h = h if hue is None else hue / 360
                r, g, b = colorsys.hsv_to_rgb(h, min(1.0, s * saturation), v)
                px[x, y] = (round(r * 255), round(g * 255), round(b * 255), 255)
    return out


def body_anchor(image):
    """Centre of the chest's lowest rows, and its bottom."""
    alpha = image.getchannel("A")
    box = alpha.getbbox()
    band = alpha.crop((0, box[3] - BODY_ROWS, image.width, box[3])).getbbox()
    return (band[0] + band[2]) / 2, box[3]


def place(image, anchor):
    """Put an image on the shared canvas so its anchor lands on the canvas's bottom centre."""
    canvas = Image.new("RGBA", CANVAS)
    canvas.alpha_composite(image, (round(CANVAS[0] / 2 - anchor[0]), CANVAS[1] - anchor[1]))
    return canvas


# ---------------------------------------------------------------- the chest and its still light

def closed_chest():
    keyed = key_magenta(Image.open(os.path.join(FOREST, CLOSED_SHEET)))
    closed = shrink(main_item(column(keyed, 1, 3)), knight_scale(keyed))
    return place(closed, body_anchor(closed))


def half_chest():
    """The half-open chest and its burst. The side rays reach down the box, so the chest is found from
    its lowest 70 rows."""
    keyed = key_magenta(Image.open(os.path.join(FOREST, HALF_SHEET)))
    scale = knight_scale(keyed)
    plain, lit = column(keyed, 1, 3), column(keyed, 2, 3)
    frames = [chest_frame(plain, 70), chest_frame(lit, 70)]
    width = max(f[1] - f[0] for f in frames) + 40
    chest = shrink(window(plain, frames[0], 20, width, 700), scale)
    lit = shrink_clean(clear_pink(window(lit, frames[1], 20, width, 700)), scale)
    burst = clear_specks(light_layer(chest, lit))
    anchor = body_anchor(chest)
    return place(chest, anchor), place(burst, anchor)


def back_edge(empty, lit):
    """The row of the box's back edge, between the inside of the lid and the inside of the box: the
    brightest row of the empty chest's middle, inside the area the glow lights. Also the glow's top row."""
    rows = [y for y in range(lit.height) if any(
        lit.getpixel((x, y))[3] and lum(lit.getpixel((x, y))) - lum(empty.getpixel((x, y))) >= GLOW_GAIN
        for x in range(lit.width))]
    top, bottom = rows[0], rows[-1]
    box = empty.getchannel("A").getbbox()
    middle = range(box[0] + (box[2] - box[0]) // 3, box[2] - (box[2] - box[0]) // 3)

    def brightness(y):
        return sum(lum(empty.getpixel((x, y))) for x in middle) / len(middle)

    return max(range(top + 3, bottom - 2), key=brightness), top


def tinted_glow(empty, lit, tint, pulse):
    """The glowing chest in a colour: the wood stays, lit by the colour, and the brightest pixels in the
    box turn almost white."""
    edge, top = back_edge(empty, lit)
    out = Image.new("RGBA", lit.size)
    e, l, o = empty.load(), lit.load(), out.load()
    for y in range(lit.height):
        if y < edge:
            t = (edge - 1 - y) / max(1, edge - 1 - top)
            strength = LID_LIGHT[0] + (LID_LIGHT[1] - LID_LIGHT[0]) * t
        else:
            strength = 1.0
        for x in range(lit.width):
            a, b = l[x, y], e[x, y]
            if not a[3]:
                continue
            gain = lum(a) - (lum(b) if b[3] else 0)
            if gain < GLOW_GAIN:
                continue
            if lum(a) > CORE_LUM + (1 - pulse) * CORE_PULSE and y >= edge:
                colour = tuple(round(255 * 0.7 + c * 0.3) for c in tint)
            else:
                g = min(1.0, gain / GLOW_FULL) * strength * pulse
                wood = b[:3] if b[3] else tint
                colour = tuple(round(w * (1 - g) + c * g) for w, c in zip(wood, tint))
            o[x, y] = colour + (255,)
    return out


def open_chest():
    """The empty and the glowing chest, their anchor, and the placed empty chest."""
    keyed = key_magenta(Image.open(os.path.join(FOREST, GLOW_SHEET)))
    scale = knight_scale(keyed)
    sources = []
    for index in (1, 2):
        col = clear_pink(column(keyed, index, 3))
        sources.append(window(col, chest_frame(col, 70), 60, 420, 720))
    empty, lit = shrink(sources[0], scale), shrink_clean(sources[1], scale)
    anchor = body_anchor(empty)
    return empty, lit, anchor, scale


def effects(empty, lit, anchor, scale):
    """The motes, the rays and the pillar, uncoloured, on the shared canvas."""
    glow_keyed = key_magenta(Image.open(os.path.join(FOREST, GLOW_SHEET)))
    keyed = key_magenta(Image.open(os.path.join(FOREST, ADDON_SHEET)))
    # The sheet draws the chest without the knight: match its width to the glowing chest's.
    a = chest_frame(column(glow_keyed, 2, 3), 70)
    b = chest_frame(column(keyed, 0, 4), 70)
    scale = scale * (a[1] - a[0]) / (b[1] - b[0])
    smalls = []
    for index in range(4):
        col = clear_pink(column(keyed, index, 4))
        smalls.append(shrink_clean(window(col, chest_frame(col, 70), 60, 384, 900), scale))
    base_anchor = body_anchor(smalls[0])
    out = {}
    for index, name in ((1, "motes"), (2, "rays"), (3, "pillar")):
        out[name] = place(clear_specks(light_layer(smalls[0], smalls[index])), base_anchor)
    # The pillar must rise out of the light in the box. The drawn pillar stops above the dim inside of the
    # lid, so its lowest row is repeated down to the box's back edge.
    edge, _top = back_edge(place(empty, anchor), place(lit, anchor))
    pillar = out["pillar"]
    alpha = pillar.getchannel("A")
    lowest = alpha.getbbox()[3] - 1
    for y in range(lowest + 1, edge + 1):
        for x in range(pillar.width):
            if alpha.getpixel((x, lowest)):
                pillar.putpixel((x, y), pillar.getpixel((x, lowest)))
    return out, edge


# ---------------------------------------------------------------- the loop

def wave(frame, phase=0.0):
    """0 to 1 and back once over the loop."""
    return 0.5 - 0.5 * math.cos(2 * math.pi * (frame / FRAMES + phase))


def islands(layer):
    """Connected groups of opaque pixels, each as {(x, y): rgba}."""
    alpha = layer.getchannel("A")
    px = layer.load()
    seen, out = set(), []
    for y in range(layer.height):
        for x in range(layer.width):
            if not alpha.getpixel((x, y)) or (x, y) in seen:
                continue
            group, stack = {}, [(x, y)]
            seen.add((x, y))
            while stack:
                cx, cy = stack.pop()
                group[(cx, cy)] = px[cx, cy]
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        n = (cx + dx, cy + dy)
                        if 0 <= n[0] < layer.width and 0 <= n[1] < layer.height and n not in seen and alpha.getpixel(n):
                            seen.add(n)
                            stack.append(n)
            out.append(group)
    return out


def mote_frames(layer):
    """Each mote rises out of the light in the box and vanishes above the lid, one after another. A mote
    is a single dot as it appears and as it vanishes."""
    groups = sorted(islands(layer), key=lambda g: min(x for x, _ in g))
    frames = []
    for f in range(FRAMES):
        image = Image.new("RGBA", layer.size)
        for index, group in enumerate(groups):
            age = (f + index * FRAMES // len(groups)) % FRAMES
            xs = [x for x, _ in group]
            ys = [y for _, y in group]
            cx, cy = (min(xs) + max(xs)) // 2, (min(ys) + max(ys)) // 2
            dy = MOTE_START - age * MOTE_STEP - cy
            pixels = [max(group, key=lambda p: sum(group[p][:3]))] if age in (0, FRAMES - 1) else group
            for (x, y) in pixels:
                if 0 <= y + dy < image.height:
                    image.putpixel((x, y + dy), group[(x, y)])
        frames.append(image)
    return frames


def ray_frames(layer, centre):
    """Each ray grows and shrinks from where it leaves the box, out of step with the others. Dots blink."""
    groups = islands(layer)
    frames = []
    for f in range(FRAMES):
        image = Image.new("RGBA", layer.size)
        for index, group in enumerate(groups):
            phase = index / len(groups)
            if len(group) <= SMALL:
                if wave(f, phase) > 0.4:
                    for p, c in group.items():
                        image.putpixel(p, c)
                continue
            origin = min(group, key=lambda p: (p[0] - centre[0]) ** 2 + (p[1] - centre[1]) ** 2)
            far = max(math.dist(origin, p) for p in group)
            reach = far * (RAY_SHORTEST + (1 - RAY_SHORTEST) * wave(f, phase))
            for p, c in group.items():
                if math.dist(origin, p) <= reach + 0.5:
                    image.putpixel(p, c)
        frames.append(image)
    return frames


def pillar_frames(layer):
    """The pillar's top rises and falls, and the sparkle above it moves with the top and blinks."""
    groups = sorted(islands(layer), key=len, reverse=True)
    body, sparkles = groups[0], groups[1:]
    top = min(y for _, y in body)
    frames = []
    for f in range(FRAMES):
        image = Image.new("RGBA", layer.size)
        cut = top + round(PILLAR_DROP * wave(f, 0.5))
        for (x, y), c in body.items():
            if y >= cut:
                image.putpixel((x, y), c)
        for index, group in enumerate(sparkles):
            if wave(f, index * 0.3) > 0.35:
                for (x, y), c in group.items():
                    image.putpixel((x, y + cut - top), c)
        frames.append(image)
    return frames


def build():
    """Every sprite, by file name."""
    sprites = {"chest-closed": closed_chest()}
    sprites["chest-half"], burst = half_chest()
    empty, lit, anchor, scale = open_chest()
    sprites["chest-open"] = place(empty, anchor)
    layers, edge = effects(empty, lit, anchor, scale)
    moving = {
        "motes": mote_frames(layers["motes"]),
        "rays": ray_frames(layers["rays"], (CANVAS[0] / 2, edge)),
        "pillar": pillar_frames(layers["pillar"]),
    }
    for colour in COLOURS:
        sprites[f"chest-burst-{colour}"] = recolour(burst, colour)
        for f in range(FRAMES):
            pulse = PULSE[0] + (PULSE[1] - PULSE[0]) * wave(f)
            frame = place(clear_specks(tinted_glow(empty, lit, TINTS[colour], pulse)), anchor)
            for name in EFFECTS[colour]:
                frame.alpha_composite(recolour(moving[name][f], colour))
            sprites[f"chest-light-{colour}-{f}"] = frame
    return sprites


# ---------------------------------------------------------------- preview

def knight():
    """The knight standing, facing the viewer, with the head at its anchor."""
    hero = os.path.join(FOREST, "Hero")
    anchors = {f["name"]: f for f in json.load(open(os.path.join(hero, "anchors.json")))["frames"]}
    canvas = Image.new("RGBA", (96, 140))
    for part in ("legs-idle", "torso-idle"):
        canvas.alpha_composite(Image.open(os.path.join(hero, f"hero-{part}-down.png")).convert("RGBA"), (0, 44))
    head = anchors["hero-idle-down"]["head"]
    head_image = Image.open(os.path.join(hero, "hero-head-down.png")).convert("RGBA")
    canvas.alpha_composite(head_image, (round(head["x"]), round(136 - head["y"] - 40)))
    return canvas.crop(canvas.getchannel("A").getbbox())


def preview(path, sprites):
    """Opening and three loops for every colour, with the empty chest and the knight, at three times size.
    WebP keeps every colour: a GIF's 256 colours turned the blue grey-green beside the grass."""
    hero = knight()
    width, base = 70 + 62 * (len(COLOURS) + 1), 112
    states = [("closed", 700), ("half", 120)] + [(f, FRAME_MS) for f in range(FRAMES)] * 3
    frames = []
    for state, _ in states:
        image = Image.new("RGBA", (width, 120), (112, 160, 72, 255))
        image.alpha_composite(hero, (34 - hero.width // 2, base - hero.height))
        for i, colour in enumerate(COLOURS + ("empty",)):
            x, y = 100 + i * 62 - CANVAS[0] // 2, base - CANVAS[1]
            if colour == "empty":
                image.alpha_composite(sprites["chest-open"], (x, y))
            elif state == "closed":
                image.alpha_composite(sprites["chest-closed"], (x, y))
            elif state == "half":
                image.alpha_composite(sprites["chest-half"], (x, y))
                image.alpha_composite(sprites[f"chest-burst-{colour}"], (x, y))
            else:
                image.alpha_composite(sprites["chest-open"], (x, y))
                image.alpha_composite(sprites[f"chest-light-{colour}-{state}"], (x, y))
        big = image.resize((image.width * 3, image.height * 3), Image.NEAREST).convert("RGB")
        ImageDraw.Draw(big).text((8, 8), "white  blue  purple  gold  empty", fill=(255, 255, 255))
        frames.append(big)
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=[d for _, d in states], loop=0,
                   lossless=True)


def main():
    sprites = build()
    os.makedirs(OUT, exist_ok=True)
    for name, image in sprites.items():
        image.save(os.path.join(OUT, name + ".png"))
    print(f"{len(sprites)} chest sprites written to {OUT}")
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1], sprites)


if __name__ == "__main__":
    main()
