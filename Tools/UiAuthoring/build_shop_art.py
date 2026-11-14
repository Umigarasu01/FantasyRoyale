"""Cut placeholder shop art out of the game's own sprites: the shopkeepers' faces and the item icons.

Usage:
    python build_shop_art.py

This is placeholder art for the shop screen (docs/fantasy-royale/exec-plans/active/shop-screen.md). The real
portraits and icons are not decided, so nothing is generated: each shopkeeper is cut from the shop's sprite,
where they stand behind a window or a counter, and each icon is cut from the sprite the game already draws.

Reads from Assets/Art/Generated/ForestLook:
    Town/<shop>.png                      the shop houses, the stall and the rug, with their keepers
    Hero/hero-<part>-down.png            the knight's head, torso and legs, seen from the front
    Sets/<id>/hero-<part>-down.png       the other armour sets' parts
    Gear/sword-rot-03.png, bow-down.png
    Town/town-stall.png                  the red potion bottle on the stall

Writes to Assets/Art/Generated/Hud:
    Keepers/keeper-<shop>.png            the face and shoulders, KEEPER_SCALE times larger
    Icons/icon-<id>-<head|torso|legs>.png, icon-weapon-<id>.png, icon-potion.png

Pixel art is only enlarged by whole numbers with nearest neighbour, so its dots stay square. An icon is
cropped to its opaque pixels, enlarged by the largest whole number that fits ICON_SIZE less the margin, and
centred on an ICON_SIZE canvas. The keepers are shown at their native size, so their dots stay whole pixels;
the icons are shown at several sizes and are smoothed when shrunk. Output is deterministic.
"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SOURCE = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
OUT = os.path.join(ROOT, "Assets", "Art", "Generated", "Hud")

KEEPER_SCALE = 4
ICON_SIZE = 128
ICON_MARGIN = 4

# Where each keeper's face and shoulders are in the shop's sprite: (left, top, right, bottom).
KEEPERS = {
    "weapon-shop": (64, 118, 126, 172),
    "armour-shop": (64, 118, 126, 172),
    "town-stall": (49, 50, 107, 102),
    "town-rug": (36, 38, 96, 92),
    "magic-shop": (60, 140, 124, 200),
    "item-shop": (62, 116, 124, 172),
}

# The armour sets: the knight lives in Hero/, the others in Sets/<id>/.
ARMOUR_PARTS = {"head": "hero-head-down.png", "torso": "hero-torso-idle-down.png", "legs": "hero-legs-idle-down.png"}
POTION_BOX = (26, 77, 43, 104)


def load(*parts):
    return Image.open(os.path.join(SOURCE, *parts)).convert("RGBA")


def enlarge(image, scale):
    return image.resize((image.width * scale, image.height * scale), Image.NEAREST)


def icon(image):
    """Crop to the opaque pixels, enlarge by the largest whole number that fits, and centre on the canvas."""
    box = image.getchannel("A").getbbox()
    if box is None:
        raise ValueError("the icon is empty")
    image = image.crop(box)
    scale = (ICON_SIZE - 2 * ICON_MARGIN) // max(image.size)
    if scale < 1:
        raise ValueError(f"the icon is too large for {ICON_SIZE} px: {image.size}")
    image = enlarge(image, scale)
    canvas = Image.new("RGBA", (ICON_SIZE, ICON_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(image, ((ICON_SIZE - image.width) // 2, (ICON_SIZE - image.height) // 2))
    return canvas


# Placeholder icons for things the game has no sprite for yet: the two trial magics and the weapons' own skills.
# They are drawn as tiny pixel pictures on a PIXEL_ICON grid and enlarged like the cut icons, so they sit with
# them. The skill is the weapon's special action, which for now is the other weapon's attack (the sword's skill
# shoots an arrow, the bow's skill swings), so its icon shows that action.
PIXEL_ICON = 16


def drawn(paint):
    image = Image.new("RGBA", (PIXEL_ICON, PIXEL_ICON), (0, 0, 0, 0))
    paint(ImageDraw.Draw(image))
    return icon(image)


def fire(draw):
    draw.polygon([(8, 1), (12, 7), (13, 11), (11, 14), (5, 14), (3, 11), (4, 7)], fill=(232, 96, 40, 255))
    draw.polygon([(8, 6), (10, 10), (10, 13), (6, 13), (6, 10)], fill=(255, 210, 90, 255))


def ice(draw):
    draw.polygon([(8, 1), (13, 8), (8, 15), (3, 8)], fill=(124, 200, 240, 255), outline=(232, 246, 255, 255))
    draw.line([(8, 3), (8, 13)], fill=(232, 246, 255, 255))


def arrow(draw):
    draw.line([(2, 13), (12, 3)], fill=(220, 210, 190, 255), width=1)
    draw.polygon([(13, 2), (13, 6), (9, 2)], fill=(200, 200, 210, 255))
    draw.line([(2, 11), (4, 13)], fill=(200, 60, 60, 255))


def slash(draw):
    draw.arc((1, 1, 15, 15), start=200, end=340, fill=(191, 224, 255, 255), width=2)
    draw.arc((3, 4, 13, 14), start=210, end=330, fill=(111, 168, 232, 255), width=1)


# The equipment slots of the inventory and shop screens show a faint picture of what goes there while they
# are empty, instead of a written name. Each is the matching icon filled with one flat colour, so only its
# outline reads; the screens draw it faintly and hide it once something is worn.
SLOT_SILHOUETTES = {
    "slot-head": "icon-knight-head",
    "slot-torso": "icon-knight-torso",
    "slot-legs": "icon-knight-legs",
    "slot-main-hand": "icon-weapon-sword",
    "slot-off-hand": "icon-weapon-shield",
}


def silhouette(image):
    flat = Image.new("RGBA", image.size, (255, 255, 255, 0))
    # Faint edge pixels left by cutting the icon (a stray dot under the shield) are dropped.
    flat.putalpha(image.getchannel("A").point(lambda v: 255 if v > 128 else 0))
    return flat


def armour_sets():
    """(id, folder parts) for every armour set, the knight first."""
    yield "knight", ("Hero",)
    sets = os.path.join(SOURCE, "Sets")
    for name in sorted(os.listdir(sets)):
        if os.path.isdir(os.path.join(sets, name)):
            yield name, ("Sets", name)


def build():
    out = {}
    for shop, box in KEEPERS.items():
        out[os.path.join("Keepers", f"keeper-{shop}.png")] = enlarge(load("Town", f"{shop}.png").crop(box), KEEPER_SCALE)
    for set_id, folder in armour_sets():
        for part, file in ARMOUR_PARTS.items():
            if os.path.exists(os.path.join(SOURCE, *folder, file)):
                out[os.path.join("Icons", f"icon-{set_id}-{part}.png")] = icon(load(*folder, file))
    # The game draws the sword as the blade from the guard plus a separate pommel, with the grip hidden in the
    # fist. The icon uses the blade and guard only.
    out[os.path.join("Icons", "icon-weapon-sword.png")] = icon(load("Gear", "sword-rot-03.png"))
    out[os.path.join("Icons", "icon-weapon-bow.png")] = icon(load("Gear", "bow-down.png"))
    out[os.path.join("Icons", "icon-potion.png")] = icon(load("Town", "town-stall.png").crop(POTION_BOX))
    out[os.path.join("Icons", "icon-weapon-shield.png")] = icon(load("Gear", "shield-down.png"))
    out[os.path.join("Icons", "icon-magic-fire.png")] = drawn(fire)
    out[os.path.join("Icons", "icon-magic-ice.png")] = drawn(ice)
    out[os.path.join("Icons", "icon-skill-sword.png")] = drawn(arrow)
    out[os.path.join("Icons", "icon-skill-bow.png")] = drawn(slash)
    for slot_name, icon_name in SLOT_SILHOUETTES.items():
        out[os.path.join("Icons", f"{slot_name}.png")] = silhouette(out[os.path.join("Icons", f"{icon_name}.png")])
    return out


def main():
    for path, image in build().items():
        target = os.path.join(OUT, path)
        os.makedirs(os.path.dirname(target), exist_ok=True)
        image.save(target, optimize=True)
        print(f"{path} {image.width}x{image.height}")


if __name__ == "__main__":
    main()
