"""Put the legs of a chosen run GIF under the upper bodies of one row of the run sheets.

Usage: python transplant_run_legs.py <output folder>
Reads the sheets as they were before 2026-11-15 from Assets (restore them from git first) and writes the four
body sheets, hero-body-run[-sword] and hero-run[-sword], with the back row and the back diagonal row on new legs.
The legs sheet and the gear sheets are then made from the body sheet with their original edit prompts.

The legs are cut from the GIF below the tunic's hem. A boot kicked up over the tunic is kept on top of the
body sheets and cut as a hole out of the torso sheets, so the legs layer shows through it in the game.
"""
import os, sys, json
from collections import deque
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
F = os.path.join(ROOT, "Assets", "Art", "Generated", "ForestLook")
GIFS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Inputs", "run-legs")
MAGENTA = (255, 0, 255, 255)
TUNIC_SHARE = 0.18

def label(mask):
    h, w = mask.shape
    lab = np.zeros((h, w), int); n = 0
    for y0, x0 in zip(*np.nonzero(mask)):
        if lab[y0, x0]: continue
        n += 1; lab[y0, x0] = n; q = deque([(y0, x0)])
        while q:
            y, x = q.popleft()
            for ny, nx in ((y+1,x),(y-1,x),(y,x+1),(y,x-1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not lab[ny, nx]:
                    lab[ny, nx] = n; q.append((ny, nx))
    return lab, n

def keyed(img):
    a = np.array(img.convert("RGBA")).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    bg = (r > 60) & (b > 60) & (g < np.minimum(r, b) * 0.72)
    return a, (a[..., 3] > 0) & ~bg

def colours(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    blue = (b > 110) & (b > r + 50) & (b > g + 25) & (g > r - 5)
    brown = (r > 70) & (r < 190) & (g > 30) & (g < 120) & (b < 80) & (r - g > 35)
    gold = (r > 170) & (g > 110) & (b < 90) & (r - b > 110)
    grey = (np.abs(r - g) < 22) & (np.abs(g - b) < 22) & (r > 150)
    return blue, brown, gold, grey

def head_band(fg):
    ys, xs = np.nonzero(fg); top = ys.min()
    rows = fg[top: top + max(4, (ys.max() - top) // 6)]
    hx = np.nonzero(rows.any(0))[0]
    return top, (hx.min() + hx.max()) / 2, hx.max() - hx.min()

def body_info(a, fg):
    blue = colours(a)[0]
    top, cx, hw = head_band(fg)
    lo, hi = int(cx - 0.45 * hw), int(cx + 0.45 * hw)
    hem = np.nonzero(blue[:, lo:hi])[0].max()
    return dict(top=int(top), hem=int(hem), cx=float(cx), hw=float(hw), bottom=int(np.nonzero(fg)[0].max()))

def gif_frames(path):
    g = Image.open(path); out = []
    for i in range(g.n_frames):
        g.seek(i); out.append(g.convert("RGBA").copy())
    return out

def goldish(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return (r > 150) & (g > 0.55 * r) & (b < 0.4 * g)

def tunic_colour(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return ((b > r + 30) & (b > g + 30)) | goldish(a)

SHORTS = (30, 24, 28, 255)
UNDERPAINT = 0
GAP = 8
HOLE = 40

def gif_keyed(img):
    a = np.array(img.convert("RGBA")).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return a, (a[..., 3] > 0) & ~(((255 - r) + g + (255 - b)) < 150)

def tunic_body(a, fg, info):
    """The blue and gold pieces that make up the tunic: those reaching the middle just above the hem."""
    mask = tunic_colour(a) & fg
    lab, n = label(mask)
    up = info["hem"] - info["top"]
    y0, y1 = int(info["hem"] - 0.2 * up), int(info["hem"] + 1)
    x0, x1 = int(info["cx"] - 0.25 * info["hw"]), int(info["cx"] + 0.25 * info["hw"])
    ids = set(np.unique(lab[y0:y1, x0:x1])) - {0}
    return np.isin(lab, list(ids))

def column_hem(tunic, fg, info, depth):
    """The lowest row of the tunic in each column, near the middle hem; -1 where there is no tunic."""
    h, w = tunic.shape
    low = np.full(w, -1)
    rows = slice(int(info["hem"] - depth), int(info["hem"] + depth))
    for x in range(w):
        ys = np.nonzero(tunic[rows, x])[0]
        if len(ys):
            low[x] = rows.start + ys.max()
    return low

def below_tunic(fg, tunic, low, centre_hem):
    mask = fg & ~tunic
    yy = np.arange(fg.shape[0])[:, None]
    limit = np.where(low >= 0, low + 2, centre_hem + 2)[None, :]
    return mask & (yy > limit)

def gif_legs(frame, boot_over=True):
    """The legs under the tunic's lower edge, read column by column, as a full-size layer."""
    a, fg = gif_keyed(frame)
    blue, brown, gold, grey = colours(a)
    info = body_info(a, fg)
    hem, cx, hw = info["hem"], info["cx"], info["hw"]
    tunic = tunic_body(a, fg, info)
    low = column_hem(tunic, fg, info, 0.25 * (hem - info["top"]))
    cand = below_tunic(fg, tunic, low, hem)
    lab, n = label(cand)
    legs = np.zeros_like(cand)
    for k in range(1, n + 1):
        comp = lab == k; ys, xs = np.nonzero(comp)
        topx = xs[ys <= ys.min() + 3]
        touching = ys.min() <= hem + 12 and (np.abs(topx - cx) < 0.5 * hw).any()
        detached = (comp & brown).sum() > 100 and abs(xs.mean() - cx) < 0.6 * hw
        if (touching or detached) and comp.sum() > 30 and not held_like(comp, a):
            legs |= comp
    layer = a.copy().astype(np.uint8); layer[..., 3] = np.where(legs, 255, 0)
    # paint dark shorts a little above the top of each leg column, under the tunic
    for x in np.nonzero(legs.any(0))[0]:
        y0 = np.nonzero(legs[:, x])[0].min()
        for y in range(max(0, y0 - UNDERPAINT), y0):
            layer[y, x] = SHORTS
    return Image.fromarray(layer, "RGBA"), np.zeros_like(legs), info

def shrink_layer(img, mask, s):
    w, h = img.size; size = (round(w * s), round(h * s))
    m = Image.fromarray((mask * 255).astype(np.uint8)).resize(size, Image.NEAREST)
    return img.resize(size, Image.NEAREST), np.array(m) > 0

def cells(sheet, rows, row):
    W, H = sheet.size
    for col in range(6):
        yield col, (round(col * W / 6), round(row * H / rows), round((col + 1) * W / 6), round((row + 1) * H / rows))

def paste_mask(shape, mask, at):
    out = np.zeros(shape, bool); x, y = at; h, w = mask.shape
    x0, y0 = max(0, x), max(0, y); x1, y1 = min(shape[1], x + w), min(shape[0], y + h)
    if x1 > x0 and y1 > y0:
        out[y0:y1, x0:x1] = mask[y0 - y:y1 - y, x0 - x:x1 - x]
    return out

def held_like(comp, a):
    blue, brown, gold, grey = colours(a)
    return (comp & grey).sum() > 0.25 * comp.sum() or (comp & (blue | gold)).sum() > 0.2 * comp.sum()

def do_body(name, rows, row, legs, mode, held, force=None):
    sheet = Image.open(os.path.join(F, name)).convert("RGB")
    data = []
    for col, box in cells(sheet, rows, row):
        a, fg = keyed(sheet.crop(box)); data.append((box, a, fg, body_info(a, fg)))
    g = [l[2] for l in legs]
    if mode == "legs":
        s = np.mean([(data[c][3]["bottom"] - data[c][3]["hem"]) / (g[c]["bottom"] - g[c]["hem"]) for c in (0, 3)])
    else:
        s = np.median([(d[3]["hem"] - d[3]["top"]) / (gi["hem"] - gi["top"]) for d, gi in zip(data, g)])
    if force is not None:
        s = force
    out = sheet.copy(); placements = []
    for (box, a, fg, info), (layer, over, ginfo) in zip(data, legs):
        hem, cx, hw = info["hem"], info["cx"], info["hw"]
        tunic = tunic_body(a, fg, info)
        low = column_hem(tunic, fg, info, 0.25 * (hem - info["top"]))
        below = below_tunic(fg, tunic, low, hem)
        lab, n = label(below); old = np.zeros_like(below)
        for k in range(1, n + 1):
            comp = lab == k; ys, xs = np.nonzero(comp)
            if held_like(comp, a):
                continue
            topx = xs[ys <= ys.min() + 3]
            touching = ys.min() <= hem + 12 and (np.abs(topx - cx) < 0.5 * hw).any()
            if touching or (ys.min() >= hem + 15 and abs(xs.mean() - cx) < 0.7 * hw):
                old |= comp
        # a glove reaching down from the arm stays, even where an old leg touched it, with its dark edge
        r, g, b = a[..., 0], a[..., 1], a[..., 2]
        glove = fg & (r >= 70) & (r <= 190) & (r > g + 22) & (g > b + 8) & (b < 90)
        glab, gn = label(glove); keep = np.zeros_like(glove)
        for k in range(1, gn + 1):
            comp = glab == k
            if np.nonzero(comp)[0].min() < hem - 2:
                keep |= comp
        edge = np.zeros_like(keep)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                edge |= np.roll(np.roll(keep, dy, 0), dx, 1)
        old &= ~(edge & fg & ((a[..., :3].max(-1) < 100) | keep))
        if held:
            blade = blade_pixels(a, fg)
            old &= ~blade
            # the sword hand hangs beside the tunic, on the side away from the shield
            tcols = np.nonzero(tunic.any(0))[0]
            yy = np.arange(fg.shape[0])[:, None]; xx = np.arange(fg.shape[1])[None, :]
            old &= ~((xx > tcols.max() - 4) & (yy < hem + 0.15 * (hem - info["top"])))
            # the fist on the blade's hilt stays too
            near = np.zeros_like(blade)
            for dy in range(-4, 5):
                for dx in range(-4, 5):
                    near |= np.roll(np.roll(blade, dy, 0), dx, 1)
            for k in range(1, gn + 1):
                comp = glab == k
                if (comp & near).any():
                    ring = np.zeros_like(comp)
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ring |= np.roll(np.roll(comp, dy, 0), dx, 1)
                    old &= ~(ring & fg & ((a[..., :3].max(-1) < 100) | comp))
        small, smask = shrink_layer(layer, over, s)
        at = (round(cx - ginfo["cx"] * s), round(hem - ginfo["hem"] * s))
        upper = a.copy().astype(np.uint8); upper[..., 3] = np.where(fg & ~old, 255, 0)
        boot = small.copy(); ba = np.array(boot); ba[..., 3] = np.where(smask, ba[..., 3], 0)
        base = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), MAGENTA)
        base.alpha_composite(small, at); base.alpha_composite(Image.fromarray(upper, "RGBA"))
        base.alpha_composite(Image.fromarray(ba, "RGBA"), at)
        base = finish(base, fg & ~old, at, small)
        base = outline_gloves(base, fg & ~old, a)
        out.paste(base.convert("RGB"), box[:2])
        placements.append(dict(info, at=at))
    return out, s, placements

def finish(base, upper_mask, at, small):
    """Fill short gaps between the tunic and the legs with dark shorts, and small holes inside the legs."""
    a = np.array(base).copy()
    bg = (a[..., 0] == 255) & (a[..., 1] == 0) & (a[..., 2] == 255)
    legs = paste_mask(bg.shape, np.array(small)[..., 3] > 0, at)
    h, w = bg.shape
    for x in range(w):
        col_up = np.nonzero(upper_mask[:, x])[0]
        col_leg = np.nonzero(legs[:, x])[0]
        if not len(col_up) or not len(col_leg):
            continue
        y_up = col_up.max()
        after = col_leg[col_leg > y_up]
        if len(after) and 0 < after.min() - y_up - 1 <= GAP and bg[y_up + 1:after.min(), x].all():
            a[y_up + 1:after.min(), x] = SHORTS
    bg = (a[..., 0] == 255) & (a[..., 1] == 0) & (a[..., 2] == 255)
    lab, n = label(bg)
    for k in range(1, n + 1):
        comp = lab == k; ys, xs = np.nonzero(comp)
        if comp.sum() <= HOLE and ys.min() > 0 and xs.min() > 0 and ys.max() < h - 1 and xs.max() < w - 1:
            a[comp] = SHORTS
    return Image.fromarray(a, "RGBA")

OUTLINE = (20, 12, 10, 255)

def blade_pixels(a, fg):
    """A held blade: a long grey run, with the gold guard and the dark edge touching it."""
    blue, brown, gold, grey = colours(a)
    pale = fg & (np.abs(a[..., 0] - a[..., 2]) < 30) & (np.abs(a[..., 1] - a[..., 2]) < 30) & (a[..., :3].min(-1) > 110)
    lab, n = label(pale); blade = np.zeros_like(pale)
    for k in range(1, n + 1):
        comp = lab == k; ys, xs = np.nonzero(comp)
        if comp.sum() > 150 and np.hypot(np.ptp(ys), np.ptp(xs)) > 40:
            blade |= comp
    near = np.zeros_like(blade)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            near |= np.roll(np.roll(blade, dy, 0), dx, 1)
    return blade | (near & fg & ((a[..., :3].max(-1) < 90) | goldish(a)))

def outline_gloves(base, upper_mask, a):
    """Keep a dark edge between the upper body's gloves and the legs, so a boot never joins a fist."""
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    glove = upper_mask & (r >= 70) & (r <= 190) & (r > g + 22) & (g > b + 8) & (b < 90)
    near = np.zeros_like(glove)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        near |= np.roll(np.roll(glove, dy, 0), dx, 1)
    c = np.array(base).copy()
    bg = (c[..., 0] == 255) & (c[..., 1] == 0) & (c[..., 2] == 255)
    c[near & ~upper_mask & ~bg] = OUTLINE
    return Image.fromarray(c, "RGBA")

def torso_info(a, fg):
    ys, xs = np.nonzero(fg); top = ys.min(); bottom = ys.max()
    rows = fg[top: top + (bottom - top) // 3]; hx = np.nonzero(rows.any(0))[0]
    cx = (hx.min() + hx.max()) / 2
    band = fg[:, int(cx - 10):int(cx + 10)]
    return dict(hem=int(np.nonzero(band)[0].max()), cx=float(cx))

def do_torso(name, rows, row, legs, s, body_places):
    sheet = Image.open(os.path.join(F, name)).convert("RGB"); out = sheet.copy(); holes = 0
    for (col, box), (layer, over, ginfo), bp in zip(cells(sheet, rows, row), legs, body_places):
        a, fg = keyed(sheet.crop(box)); t = torso_info(a, fg)
        _, smask = shrink_layer(layer, over, s)
        # the body's hem sits where the torso's hem sits; the body's centre is the helmet's, the torso's is its top
        at = (round(t["cx"] + (bp["at"][0] - bp["cx"])), round(t["hem"] + (bp["at"][1] - bp["hem"])))
        hole = paste_mask(fg.shape, smask, at) & fg
        cell = a.copy().astype(np.uint8); cell[hole] = MAGENTA; holes += int(hole.sum())
        out.paste(Image.fromarray(cell, "RGBA").convert("RGB"), box[:2])
    return out, holes

def belt_row(a, fg, info):
    gold = colours(a)[2]; lo, hi = int(info["cx"] - 0.3 * info["hw"]), int(info["cx"] + 0.3 * info["hw"])
    ys = np.nonzero(gold[info["top"]:info["hem"] - 8, lo:hi].sum(1) > 6)[0]
    return int(info["top"] + ys.min()) if len(ys) else None

def do_legs(name, rows, row, legs, s, body_name):
    sheet = Image.open(os.path.join(F, name)).convert("RGB"); body = Image.open(os.path.join(F, body_name)).convert("RGB")
    out = sheet.copy()
    for (col, box), (layer, over, ginfo) in zip(cells(sheet, rows, row), legs):
        ba, bfg = keyed(body.crop(box)); bi = body_info(ba, bfg)
        # the tunic covers about the top 18% of the upper body's height below the belt
        belt = bi["hem"] - round(TUNIC_SHARE * (bi["hem"] - bi["top"]))
        a, fg = keyed(sheet.crop(box)); ys, xs = np.nonzero(fg); top = ys.min()
        topx = xs[ys < top + 6]; cx = (topx.min() + topx.max()) / 2
        hem = int(top + (bi["hem"] - belt))
        cell = a.copy().astype(np.uint8)
        keep = fg.copy(); keep[hem + 1:] = False
        upper = cell.copy(); upper[..., 3] = np.where(keep, 255, 0)
        small, smask = shrink_layer(layer, over, s)
        at = (round(cx - ginfo["cx"] * s + (bi["cx"] - (bi["cx"]))), round(hem - ginfo["hem"] * s))
        # the legs sheet is centred on the waist, the body on the helmet: shift by the body's waist offset
        bys, bxs = np.nonzero(bfg[belt:belt + 6]); waist = (bxs.min() + bxs.max()) / 2
        at = (round(cx - (waist - bi["cx"]) - ginfo["cx"] * s), at[1])
        ba2 = np.array(small); ba2[..., 3] = np.where(smask, ba2[..., 3], 0)
        base = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), MAGENTA)
        base.alpha_composite(small, at); base.alpha_composite(Image.fromarray(upper, "RGBA"))
        base.alpha_composite(Image.fromarray(ba2, "RGBA"), at)
        base = finish(base, keep, at, small)
        out.paste(base.convert("RGB"), box[:2])
    return out

if __name__ == "__main__":
    outdir = sys.argv[1]
    report = {}
    for suffix, rows, row, gif, mode in (("-01.png", 3, 2, "run-up-kick.gif", "legs"), ("-diagonal-01.png", 2, 1, "run-upside-kick.gif", "upper")):
        legs = [gif_legs(f, mode == "legs") for f in gif_frames(os.path.join(GIFS, gif))]
        places = None
        for base, held in (("hero-body-run", False), ("hero-body-run-sword", False), ("hero-run", True), ("hero-run-sword", True)):
            img, s, p = do_body(base + suffix, rows, row, legs, mode, held, None if places is None else s_body)
            img.save(os.path.join(outdir, base + suffix)); report[base + suffix] = round(float(s), 3)
            if base == "hero-body-run": places, s_body = p, s
    print(json.dumps(report, indent=1))


def visible_legs(a, fg, info):
    tunic = tunic_body(a, fg, info)
    low = column_hem(tunic, fg, info, 0.25 * (info["hem"] - info["top"]))
    below = below_tunic(fg, tunic, low, info["hem"])
    lab, n = label(below); legs = np.zeros_like(below)
    for k in range(1, n + 1):
        comp = lab == k; ys, xs = np.nonzero(comp)
        if held_like(comp, a) or comp.sum() < 30:
            continue
        topx = xs[ys <= ys.min() + 3]
        if ys.min() <= info["hem"] + 12 and (np.abs(topx - info["cx"]) < 0.5 * info["hw"]).any():
            legs |= comp
    return legs, low

def legs_from_body(body_path, old_legs_path, rows, row, out_path):
    """The legs sheet's row in the body's place: the body's legs under the tunic, over the old sheet's shorts."""
    body = Image.open(body_path).convert("RGB"); old = Image.open(old_legs_path).convert("RGB")
    out = old.copy()
    for col, box in cells(body, rows, row):
        a, fg = keyed(body.crop(box)); info = body_info(a, fg)
        legs, low = visible_legs(a, fg, info)
        oa, ofg = keyed(old.crop(box)); ys, xs = np.nonzero(ofg); otop = ys.min()
        shorts_h = round(SHORTS_SHARE * (info["hem"] - info["top"]))
        shorts = ofg.copy(); shorts[otop + shorts_h:] = False
        sys_, sxs = np.nonzero(shorts); scx = (sxs.min() + sxs.max()) / 2
        tunic = tunic_body(a, fg, info); tys, txs = np.nonzero(tunic)
        tcx = (txs.min() + txs.max()) / 2
        dx, dy = round(tcx - scx), round(info["hem"] - (otop + shorts_h))
        base = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), MAGENTA)
        layer = oa.copy().astype(np.uint8); layer[..., 3] = np.where(shorts, 255, 0)
        base.alpha_composite(Image.fromarray(layer, "RGBA"), (dx, dy))
        lg = a.copy().astype(np.uint8); lg[..., 3] = np.where(legs, 255, 0)
        base.alpha_composite(Image.fromarray(lg, "RGBA"))
        out.paste(base.convert("RGB"), box[:2])
    out.save(out_path)

SHORTS_SHARE = 0.22
