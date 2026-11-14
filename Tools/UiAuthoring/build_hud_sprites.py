"""Draw the HUD's black lacquer sprites: the panel, the hearts, the coin, the E key and the prompt's pointer.

Usage:
    python build_hud_sprites.py [--out <folder>] [--preview <png-path>]

The look follows docs/fantasy-royale/product-specs/art/ui-design.md: plain glossy black lacquer with
rounded corners, edged by one thin line of gold, and nothing drawn inside. Hearts are clear glass in a thin
gold bezel with a red core sealed inside; an empty heart is the same glass with nothing in it, and a half
heart is red on its left half.

The panel, the E key and the pointer are drawn as shapes, so the gold line has the same width everywhere and
the panel can be stretched as a nine-slice. The coin and the heart are realistic pictures generated once
with the OpenAI Image API (Inputs/hud-coin-source.png and Inputs/hud-heart-source.png, each with its prompt
in the JSON file beside it); this script only cuts and shrinks them, and makes the half and empty hearts
from the full one. The HUD's reference screen is 1920x1080 and every sprite is drawn at twice that
resolution (SCALE), so the importer gives them 200 pixels per unit against the canvas's 100. Shapes are
drawn SUPERSAMPLE times larger and shrunk, which smooths their edges.

Writes to Assets/Art/Generated/Hud (the default):
    hud-panel.png          nine-slice panel; the import sets a border of PANEL_BORDER pixels on every side
    hud-heart-full.png     hud-heart-half.png     hud-heart-empty.png    from Inputs/hud-heart-source.png
    hud-coin.png           cut from Inputs/hud-coin-source.png
    hud-key-e.png          a key cap with the letter E drawn in
    hud-key.png            a blank key cap; the shop screen writes its letter with the pixel font
    hud-pointer.png        the prompt's pointer, joined to the bottom edge of a panel
    hud-select.png         nine-slice gold frame with a glow, round a selected row; same border as the panel
    hud-arrow.png          a small gold triangle pointing right, from the selected row to the description
    hud-cursor.png         the mouse cursor, a gold arrow; it points with its tip, CURSOR_SHAPE[0]

Output is deterministic.
"""
import argparse
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

SCALE = 2          # sprite pixels per reference pixel (1920x1080)
SUPERSAMPLE = 4    # shapes are drawn this many times larger, then shrunk

# Sizes in sprite pixels (twice the reference pixels).
PANEL_SIZE = 96
PANEL_RADIUS = 24
PANEL_BORDER = 32
GOLD_WIDTH = 3.0   # 1.5 reference pixels
GOLD_INSET = 1.5
HEART_SIZE = (72, 64)
COIN_SIZE = 60     # shown at 30 reference pixels
KEY_SIZE = 56
POINTER_SIZE = (28, 20)
ARROW_SIZE = (24, 36)
CURSOR_SIZE = 64   # shown at 32 reference pixels
ROUND_SIZE = 96    # the magic slot, a circle; scaled down in the HUD
ORB_SIZE = 32      # one MP orb, shown at 16 reference pixels
MANA_STOPS = ((150, 210, 255), (60, 130, 230), (20, 50, 140))
# The cursor's outline in a 0..1 box, tip first. The tip is the point the cursor points at; GameHud puts the
# Image's pivot there (CURSOR_TIP, measured from the top left).
CURSOR_SHAPE = ((0.06, 0.03), (0.06, 0.80), (0.27, 0.62), (0.41, 0.94), (0.55, 0.88), (0.41, 0.57), (0.66, 0.57))
SELECT_GLOW = 8.0

LACQUER_TOP = (36, 36, 40)
LACQUER_BOTTOM = (9, 9, 11)
# Opaque: the project renders in linear colour space, where a dark, slightly see-through panel shows the
# grass under it far more than its alpha suggests.
LACQUER_ALPHA = 255
GOLD_STOPS = ((246, 222, 146), (214, 170, 82), (152, 110, 42))
# The heart's red core is found by colour: hue within RED_HUE of red (0 to 255 scale) and saturation of at
# least RED_SATURATION. An empty heart keeps EMPTY_LIGHTNESS of the core's lightness.
RED_HUE = 14
RED_SATURATION = 80
EMPTY_LIGHTNESS = 0.32

DEFAULT_OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Generated", "Hud")
COIN_SOURCE = os.path.join(os.path.dirname(__file__), "Inputs", "hud-coin-source.png")
HEART_SOURCE = os.path.join(os.path.dirname(__file__), "Inputs", "hud-heart-source.png")


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def vertical_gradient(size, stops):
    """An RGB image whose colour runs through stops from top to bottom."""
    width, height = size
    image = Image.new("RGB", size)
    draw = ImageDraw.Draw(image)
    for y in range(height):
        t = y / max(1, height - 1) * (len(stops) - 1)
        index = min(int(t), len(stops) - 2)
        draw.line([(0, y), (width, y)], fill=lerp(stops[index], stops[index + 1], t - index))
    return image


def shrink(image, size):
    return image.resize(size, Image.LANCZOS)


def erode(mask, pixels):
    """Shrink a mask by pixels on every side, with a round brush so curves keep an even width."""
    radius = max(1, round(pixels))
    width, height = mask.size
    padded = Image.new("L", (width + 2 * radius, height + 2 * radius), 0)
    padded.paste(mask, (radius, radius))
    eroded = padded
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            if dx * dx + dy * dy <= radius * radius:
                eroded = ImageChops.darker(eroded, ImageChops.offset(padded, dx, dy))
    return eroded.crop((radius, radius, radius + width, radius + height))


def rounded_mask(size, inset, radius):
    mask = Image.new("L", size, 0)
    width, height = size
    ImageDraw.Draw(mask).rounded_rectangle(
        (inset, inset, width - 1 - inset, height - 1 - inset), radius=max(0, radius - inset), fill=255)
    return mask


def paint(base, colour_image, mask):
    """Paint colour_image onto base where mask is set."""
    layer = colour_image.convert("RGBA")
    layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
    base.alpha_composite(layer)


def gold_ring(size, outer, width):
    """The gold line along the inside of the outer mask."""
    ring = ImageChops.subtract(outer, erode(outer, width))
    return ring


def panel():
    s = SUPERSAMPLE
    size = (PANEL_SIZE * s, PANEL_SIZE * s)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    outer = rounded_mask(size, GOLD_INSET * s, PANEL_RADIUS * s)
    body = Image.new("RGBA", size, (0, 0, 0, 0))
    paint(body, vertical_gradient(size, (LACQUER_TOP, LACQUER_BOTTOM)), outer)
    body.putalpha(ImageChops.multiply(body.getchannel("A"), Image.new("L", size, LACQUER_ALPHA)))
    image.alpha_composite(body)

    # Soft gloss along the top: lacquer reflects the light above it.
    gloss = Image.new("L", size, 0)
    gloss_draw = ImageDraw.Draw(gloss)
    for y in range(PANEL_BORDER * s):
        gloss_draw.line([(0, y), (size[0], y)], fill=round(22 * (1 - y / (PANEL_BORDER * s)) ** 1.5))
    paint(image, Image.new("RGB", size, (255, 255, 255)), ImageChops.multiply(gloss, erode(outer, GOLD_WIDTH * s)))

    # A dark line just inside the gold sets the gold off the lacquer.
    inner = erode(outer, GOLD_WIDTH * s)
    paint(image, Image.new("RGB", size, (0, 0, 0)), gold_ring(size, inner, 1.0 * s))
    paint(image, vertical_gradient(size, GOLD_STOPS), gold_ring(size, outer, GOLD_WIDTH * s))
    return shrink(image, (PANEL_SIZE, PANEL_SIZE))


def fit(source, size):
    """Crop a generated picture to its opaque pixels and centre it in size, as large as fits."""
    source = source.crop(source.getchannel("A").getbbox())
    scale = min((size[0] - 2) / source.width, (size[1] - 2) / source.height)
    shrunk = source.resize((round(source.width * scale), round(source.height * scale)), Image.LANCZOS, reducing_gap=3.0)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    image.alpha_composite(shrunk, ((size[0] - shrunk.width) // 2, (size[1] - shrunk.height) // 2))
    return image


def core_mask(heart):
    """The inside of the glass where the red core is: red pixels, plus the pale reflections that red
    surrounds. The clear glass band between the core and the bezel is outside it."""
    hue, saturation, _ = heart.convert("RGB").convert("HSV").split()
    red = hue.point(lambda h: 255 if h <= RED_HUE or h >= 255 - RED_HUE else 0)
    red = ImageChops.multiply(red, saturation.point(lambda v: 255 if v >= RED_SATURATION else 0))
    red = ImageChops.multiply(red, heart.getchannel("A").point(lambda a: 255 if a >= 128 else 0))
    width, height = red.size
    outside = Image.new("L", (width + 2, height + 2), 255)
    outside.paste(ImageOps.invert(red), (1, 1))
    ImageDraw.floodfill(outside, (0, 0), 128)
    holes = outside.crop((1, 1, width + 1, height + 1)).point(lambda v: 255 if v == 255 else 0)
    core = ImageChops.lighter(red, holes).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))
    return core.filter(ImageFilter.GaussianBlur(0.5 * SUPERSAMPLE))


def drain(heart):
    """The same heart with the red core gone: inside the glass is dark, while the reflections on the glass
    stay as bright as they were, turned white. How much a pixel darkens follows how red it is, so a pink
    reflection over the red keeps its brightness."""
    hue, saturation, _ = heart.convert("RGB").convert("HSV").split()
    red = hue.point(lambda h: 255 if h <= RED_HUE or h >= 255 - RED_HUE else 0)
    redness = ImageChops.multiply(red, saturation.point(
        lambda v: max(0, min(255, round((v - RED_SATURATION) * 255 / 100)))))
    lightness = heart.convert("L")
    shade = Image.composite(lightness.point(lambda v: round(v * EMPTY_LIGHTNESS)), lightness, redness)
    return Image.merge("RGB", (shade, shade, shade.point(lambda v: min(255, round(v * 1.08)))))


def hearts():
    """The full, half and empty hearts, all from one generated heart so their gold bezels match exactly.

    The source is a clear glass heart in a thin gold bezel with a red core sealed inside, seen straight on
    and symmetric. An empty heart drains the core, keeping the glass and its reflections; a half heart drains
    its right half only. The source's faint glow outside the bezel is cut away first.
    """
    s = SUPERSAMPLE
    size = (HEART_SIZE[0] * s, HEART_SIZE[1] * s)
    source = Image.open(HEART_SOURCE).convert("RGBA")
    source.putalpha(source.getchannel("A").point(lambda a: max(0, min(255, round((a - 96) * 255 / 128)))))
    full = fit(source, size)
    core = core_mask(full)
    drained = drain(full)
    empty = full.copy()
    paint(empty, drained, core)
    right = Image.new("L", size, 0)
    ImageDraw.Draw(right).rectangle((size[0] // 2, 0, size[0], size[1]), fill=255)
    half = full.copy()
    paint(half, drained, ImageChops.multiply(core, right))
    return {name: shrink(image, HEART_SIZE) for name, image in (("full", full), ("half", half), ("empty", empty))}


def coin():
    """The realistic gold coin, cut from its generated source and shrunk to the sprite.

    The source is a coin seen at three quarters, so its milled edge shows; a coin seen straight on reads as
    a gold button.
    """
    return fit(Image.open(COIN_SOURCE).convert("RGBA"), (COIN_SIZE, COIN_SIZE))


def key_cap():
    """A blank key cap. The shop screen writes its letter on it with the pixel font."""
    s = SUPERSAMPLE
    size = (KEY_SIZE * s, KEY_SIZE * s)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    lip = rounded_mask(size, 2 * s, 10 * s)
    paint(image, Image.new("RGB", size, (150, 150, 154)), lip)
    top = Image.new("L", size, 0)
    ImageDraw.Draw(top).rounded_rectangle((4 * s, 3 * s, size[0] - 1 - 4 * s, size[1] - 1 - 8 * s), radius=8 * s, fill=255)
    paint(image, vertical_gradient(size, ((252, 252, 250), (222, 220, 214))), top)
    return image


def key_letter(letter_name):
    """A key cap with E or F drawn as bars, so the HUD needs no font for it. F is E without its bottom bar.
    The interact key moved from E to F on 2026-11-13; hud-key-e is kept for the record."""
    s = SUPERSAMPLE
    size = (KEY_SIZE * s, KEY_SIZE * s)
    image = key_cap()

    letter = Image.new("L", size, 0)
    draw = ImageDraw.Draw(letter)
    stroke = 5 * s
    left, right = 19 * s, 37 * s
    top_y, bottom_y = 14 * s, 39 * s
    middle = (top_y + bottom_y) / 2
    draw.rectangle((left, top_y, left + stroke, bottom_y), fill=255)
    draw.rectangle((left, top_y, right, top_y + stroke), fill=255)
    draw.rectangle((left, middle - stroke / 2, right - 2 * s, middle + stroke / 2), fill=255)
    if letter_name == "E":
        draw.rectangle((left, bottom_y - stroke, right, bottom_y), fill=255)
    paint(image, Image.new("RGB", size, (18, 18, 20)), letter)
    return shrink(image, (KEY_SIZE, KEY_SIZE))


def key_e():
    return key_letter("E")


def pointer():
    """A downward pointer. Its top band covers a panel's bottom gold line where the two join."""
    s = SUPERSAMPLE
    width, height = POINTER_SIZE[0] * s, POINTER_SIZE[1] * s
    size = (width, height)
    band = 6 * s
    shape = Image.new("L", size, 0)
    ImageDraw.Draw(shape).polygon(
        [(0, 0), (width - 1, 0), (width - 1, band), (width / 2, height - 1), (0, band)], fill=255)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    paint(image, vertical_gradient(size, ((14, 14, 16), LACQUER_BOTTOM)), shape)
    image.putalpha(ImageChops.multiply(image.getchannel("A"), Image.new("L", size, LACQUER_ALPHA)))

    edges = Image.new("L", size, 0)
    draw = ImageDraw.Draw(edges)
    line = round(GOLD_WIDTH * s)
    gold_top = round(band - (GOLD_INSET + GOLD_WIDTH) * s)
    draw.line([(0, gold_top + line / 2), (0, band), (width / 2, height - 1 - line / 2)], fill=255, width=line, joint="curve")
    draw.line([(width - 1, gold_top + line / 2), (width - 1, band), (width / 2, height - 1 - line / 2)], fill=255, width=line, joint="curve")
    paint(image, vertical_gradient(size, GOLD_STOPS[1:]), ImageChops.multiply(edges, shape))
    return shrink(image, POINTER_SIZE)


def select_frame():
    """The gold frame round a selected row: a brighter, thicker gold line with a soft glow on both sides.
    It is stretched as a nine-slice with the panel's border and drawn over the row's own panel."""
    s = SUPERSAMPLE
    size = (PANEL_SIZE * s, PANEL_SIZE * s)
    outer = rounded_mask(size, GOLD_INSET * s, PANEL_RADIUS * s)
    line = gold_ring(size, outer, (GOLD_WIDTH + 1.0) * s)
    glow = line.filter(ImageFilter.GaussianBlur(SELECT_GLOW * s / 2)).point(lambda v: min(255, round(v * 1.6)))
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    paint(image, Image.new("RGB", size, (255, 200, 90)), glow.point(lambda v: round(v * 0.55)))
    paint(image, vertical_gradient(size, ((255, 236, 170), (240, 194, 96), (190, 138, 52))), line)
    return shrink(image, (PANEL_SIZE, PANEL_SIZE))


def circle_mask(size, inset):
    mask = Image.new("L", size, 0)
    width, height = size
    ImageDraw.Draw(mask).ellipse((inset, inset, width - 1 - inset, height - 1 - inset), fill=255)
    return mask


def slot_round():
    """The magic slot: the panel's black lacquer and thin gold line, as a circle, so magic reads apart from
    the square weapon and skill slots even before its icon is read."""
    s = SUPERSAMPLE
    size = (ROUND_SIZE * s, ROUND_SIZE * s)
    outer = circle_mask(size, GOLD_INSET * s)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    paint(image, vertical_gradient(size, (LACQUER_TOP, LACQUER_BOTTOM)), outer)
    inner = erode(outer, GOLD_WIDTH * s)
    paint(image, Image.new("RGB", size, (0, 0, 0)), gold_ring(size, inner, 1.0 * s))
    paint(image, vertical_gradient(size, GOLD_STOPS), gold_ring(size, outer, GOLD_WIDTH * s))
    return shrink(image, (ROUND_SIZE, ROUND_SIZE))


def select_round():
    """The selected magic: the select frame's brighter, thicker gold with its glow, as a circle."""
    s = SUPERSAMPLE
    size = (ROUND_SIZE * s, ROUND_SIZE * s)
    outer = circle_mask(size, GOLD_INSET * s)
    line = gold_ring(size, outer, (GOLD_WIDTH + 1.0) * s)
    glow = line.filter(ImageFilter.GaussianBlur(SELECT_GLOW * s / 2)).point(lambda v: min(255, round(v * 1.6)))
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    paint(image, Image.new("RGB", size, (255, 200, 90)), glow.point(lambda v: round(v * 0.55)))
    paint(image, vertical_gradient(size, ((255, 236, 170), (240, 194, 96), (190, 138, 52))), line)
    return shrink(image, (ROUND_SIZE, ROUND_SIZE))


def mana_orb(full):
    """One MP orb under the hearts: a small blue glass bead in a thin gold rim. An empty orb keeps the rim and
    the glass highlight but its blue goes dark, like the empty heart."""
    s = SUPERSAMPLE
    size = (ORB_SIZE * s, ORB_SIZE * s)
    outer = circle_mask(size, 1.0 * s)
    inner = erode(outer, 2.0 * s)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    paint(image, vertical_gradient(size, GOLD_STOPS), outer)
    stops = MANA_STOPS if full else ((44, 50, 66), (24, 28, 40), (12, 14, 20))
    paint(image, vertical_gradient(size, stops), inner)
    shine = Image.new("L", size, 0)
    ImageDraw.Draw(shine).ellipse((0.28 * size[0], 0.18 * size[1], 0.52 * size[0], 0.40 * size[1]), fill=200 if full else 90)
    paint(image, Image.new("RGB", size, (255, 255, 255)), ImageChops.multiply(shine.filter(ImageFilter.GaussianBlur(1.5 * s)), inner))
    return shrink(image, (ORB_SIZE, ORB_SIZE))


def arrow():
    """A small gold triangle pointing right, from the selected row towards the description."""
    s = SUPERSAMPLE
    width, height = ARROW_SIZE[0] * s, ARROW_SIZE[1] * s
    size = (width, height)
    shape = Image.new("L", size, 0)
    ImageDraw.Draw(shape).polygon([(2 * s, 2 * s), (width - 2 * s, height / 2), (2 * s, height - 2 * s)], fill=255)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    paint(image, vertical_gradient(size, GOLD_STOPS), shape)
    return shrink(image, ARROW_SIZE)


def cursor():
    """The mouse cursor: a classic arrow in polished gold with a dark brown edge, and a soft shadow to the
    lower right so it reads on bright grass and stone as well as on the black lacquer panels."""
    s = SUPERSAMPLE
    big = CURSOR_SIZE * s
    size = (big, big)
    outer = Image.new("L", size, 0)
    ImageDraw.Draw(outer).polygon([(x * big, y * big) for x, y in CURSOR_SHAPE], fill=255)
    inner = erode(outer, 2.0 * s)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    shadow = ImageChops.offset(outer, round(1.5 * s), round(2.5 * s)).filter(ImageFilter.GaussianBlur(2 * s))
    paint(image, Image.new("RGB", size, (0, 0, 0)), shadow.point(lambda v: round(v * 0.55)))
    paint(image, Image.new("RGB", size, (92, 62, 18)), outer)
    paint(image, vertical_gradient(size, ((255, 240, 180), (236, 186, 80), (178, 124, 40))), inner)
    shine = Image.new("L", size, 0)
    ImageDraw.Draw(shine).line([(0.12 * big, 0.12 * big), (0.12 * big, 0.62 * big)], fill=200, width=round(2.5 * s))
    paint(image, Image.new("RGB", size, (255, 255, 245)), ImageChops.multiply(shine.filter(ImageFilter.GaussianBlur(1.5 * s)), inner))
    return shrink(image, (CURSOR_SIZE, CURSOR_SIZE))


def build():
    heart = hearts()
    return {
        "hud-panel": panel(),
        "hud-heart-full": heart["full"],
        "hud-heart-half": heart["half"],
        "hud-heart-empty": heart["empty"],
        "hud-coin": coin(),
        "hud-key-e": key_e(),
        "hud-key-f": key_letter("F"),
        "hud-key": shrink(key_cap(), (KEY_SIZE, KEY_SIZE)),
        "hud-pointer": pointer(),
        "hud-select": select_frame(),
        "hud-arrow": arrow(),
        "hud-cursor": cursor(),
        "hud-slot-round": slot_round(),
        "hud-select-round": select_round(),
        "hud-mana-full": mana_orb(True),
        "hud-mana-empty": mana_orb(False),
    }


def preview(sprites, path):
    """All sprites at reference size over grass-green and the lacquer panel, for a quick look."""
    border = PANEL_BORDER // SCALE
    canvas = Image.new("RGBA", (640, 220), (88, 140, 60, 255))
    stretched = nine_slice(sprites["hud-panel"], (360, 120), border)
    canvas.alpha_composite(stretched, (16, 16))
    x = 34
    for name in ("hud-heart-full", "hud-heart-full", "hud-heart-full", "hud-heart-half", "hud-heart-empty"):
        canvas.alpha_composite(sprites[name], (x, 30))
        x += sprites[name].width + 6
    canvas.alpha_composite(sprites["hud-coin"], (36, 100))
    bubble = nine_slice(sprites["hud-panel"], (80, 80), border)
    canvas.alpha_composite(bubble, (420, 20))
    canvas.alpha_composite(sprites["hud-key-e"], (432, 32))
    canvas.alpha_composite(sprites["hud-pointer"], (460 - sprites["hud-pointer"].width // 2, 20 + 80 - 7))
    canvas.save(path)


def nine_slice(image, size, b):
    """Stretch the panel like Unity's sliced Image, for the preview only."""
    w, h = image.size
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    tw, th = size
    xs = [(0, b, 0, b), (b, w - b, b, tw - b), (w - b, w, tw - b, tw)]
    ys = [(0, b, 0, b), (b, h - b, b, th - b), (h - b, h, th - b, th)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            piece = image.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS)
            out.alpha_composite(piece, (dx0, dy0))
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--preview")
    args = parser.parse_args()
    sprites = build()
    os.makedirs(args.out, exist_ok=True)
    for name, image in sprites.items():
        image.save(os.path.join(args.out, name + ".png"), optimize=True)
        print(f"{name}.png {image.width}x{image.height}")
    if args.preview:
        # The preview works at reference size, half the sprites' resolution.
        half = {name: image.resize((image.width // SCALE, image.height // SCALE), Image.LANCZOS)
                for name, image in sprites.items()}
        preview(half, args.preview)
        print(f"preview {args.preview}")


if __name__ == "__main__":
    main()
