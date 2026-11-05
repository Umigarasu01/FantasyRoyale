"""Generate one forest art candidate with the OpenAI image edit endpoint.

Usage:
    python generate_forest_asset.py <asset-name> <output-directory>

The API key is read from the user's environment and is never printed or saved.
Each run creates a new image and is billed, and the same pixels cannot be regenerated.
Existing output files are refused so that fixed candidates are never overwritten.
"""
import base64
import hashlib
import io
import json
import os
import sys
import urllib.error
import urllib.request
import uuid
import winreg

from PIL import Image

DESIGN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Inputs", "design-reference.png")
MODEL = "gpt-image-2"

COMMON_SHEET_RULES = """The background must be one completely flat solid pure magenta colour, RGB 255,0,255, with no shading, no gradient, no ground, no cast shadow and no texture. Keep clear magenta space around every item so that no two items touch, and keep every item fully inside the canvas.

Match the reference for pixel-art style, palette and lighting: oblique overhead view, light from the top left, crisp irregular one-pixel steps, connected colour masses, dark green-blue shadow clusters, restrained lime highlights. This is a new sprite sheet, not a collage of the reference.

Do not include: text, labels, numbers, grid lines, panels, borders, watermark, characters, animals, buildings, water, roads. No soft gradients, airbrush, blur, painterly rendering, post-processed mosaic or mirrored duplicates."""

ASSETS = {
    "forest-trees-01": {
        "reference_box": (930, 536, 1526, 800),
        "prompt": """Create ONE NEW sprite sheet of six separate forest trees for a top-down 2D fantasy game, arranged in two rows of three, using image 1 only as the style reference.

Each tree has a full rounded leafy crown, a visible brown trunk with small roots at its base, and nothing else. Match the size of the trees in the reference: each crown is about 200 pixels wide and each tree about 260 pixels tall. Make the six trees clearly different: a broad round oak, a taller upright tree, a tree with a split double crown, a smaller young tree, a wide low tree, and a dense dark tree. Their crowns use the deep green leaf clusters of the reference with dark teal shadow pockets and small lime highlights on the upper left.

""" + COMMON_SHEET_RULES,
    },
    "forest-understory-01": {
        "reference_box": (930, 536, 1526, 800),
        "prompt": """Create ONE NEW sprite sheet of twenty-four separate small forest-floor plants and stones for a top-down 2D fantasy game, arranged in four rows of six, using image 1 only as the style reference.

Include this mix, all clearly different from each other: low ferns, broad-leaf shrubs, round leafy bushes, white flower clumps, yellow flower clumps, pink flower clumps, mossy grey boulders, small pebble groups, tall grass tufts and low grass tufts. Match the size used in the reference: the largest shrubs and boulders are about 110 pixels wide, and the small tufts and pebbles about 40 pixels wide. Every item sits on nothing: draw only the plant or stone itself.

""" + COMMON_SHEET_RULES,
    },
    "forest-features-01": {
        "reference_box": (778, 124, 1526, 520),
        "prompt": """Create ONE NEW sprite sheet of four separate landmarks for a top-down 2D fantasy game, arranged in two rows of two, using image 1 only as the style reference.

Top left: a small natural forest spring, a rounded pond of deep blue water with an organic irregular shoreline of brown earth, mossy rocks, reeds and ferns, about 500 pixels wide.
Top right: a small herb garden, a low bed of dark soil edged by small stones, with neat rows of leafy herbs and a few tiny flowers, about 440 pixels wide.
Bottom left: a monster nest, a rough ring of dark twigs, roots and earth holding three large speckled eggs, about 440 pixels wide.
Bottom right: a rock cliff outcrop, a steep grey stone wall with an irregular natural outline, cracks and moss, and grass hanging over its top edge, about 560 pixels wide and 440 pixels tall.

""" + COMMON_SHEET_RULES.replace("buildings, water, roads", "buildings, roads"),
    },
    "river-bank-gentle-01": {
        "reference_box": (778, 124, 1526, 520),
        "prompt": """Create ONE NEW pixel-art river asset for a top-down 2D fantasy game, using image 1 strictly for pixel-art style, scale, palette and lighting. This is a new composition, not a collage of the reference.

Subject: a single calm river running from the top edge to the bottom edge of the canvas with smooth, wide, gentle curves and no sharp corners. Keep the river width constant, about one fifth of the canvas width. Both banks: eroded brown earth with irregular crumbling steps, a darker undercut at the waterline, small roots and pebbles, low grass clumps hanging over the edge, and a few reeds and small mossy rocks placed only on the banks. The bank band stays narrow and continuous and follows the curve naturally, with no repeated stamp pattern.

Water: the same deep saturated blue as the reference with subtle darker patches and a few tiny white ripple lines. Land beyond the banks: quiet low green grass, the same green as the reference, calm and mostly empty.

The art must be fully opaque and fill the canvas edge to edge. Do not include: trees, bridges, buildings, roads, characters, animals, text, labels, panels, borders, watermark, horizon. No soft gradients, airbrush, blur, painterly rendering, post-processed mosaic, mirrored repetition or tiled pattern.""",
    },
}


def read_key():
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as key:
        return winreg.QueryValueEx(key, "OPENAI_IMAGE_API_KEY")[0]


def multipart(fields, files):
    boundary = uuid.uuid4().hex
    body = io.BytesIO()
    for name, value in fields.items():
        body.write(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode("utf-8"))
    for name, filename, data in files:
        header = (
            f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"; filename="{filename}"\r\n'
            "Content-Type: image/png\r\n\r\n"
        )
        body.write(header.encode("utf-8"))
        body.write(data)
        body.write(b"\r\n")
    body.write(f"--{boundary}--\r\n".encode("utf-8"))
    return boundary, body.getvalue()


def main():
    name, out_dir = sys.argv[1], sys.argv[2]
    asset = ASSETS[name]
    target = os.path.join(out_dir, name + ".png")
    if os.path.exists(target):
        sys.exit(f"refused: {target} already exists")

    reference = Image.open(DESIGN).convert("RGB").crop(asset["reference_box"])
    buffer = io.BytesIO()
    reference.save(buffer, format="PNG")
    fields = {
        "model": MODEL,
        "prompt": asset["prompt"],
        "n": "1",
        "size": "1536x1024",
        "quality": "high",
        "output_format": "png",
    }
    boundary, payload = multipart(fields, [("image[]", "reference.png", buffer.getvalue())])
    request = urllib.request.Request(
        "https://api.openai.com/v1/images/edits",
        data=payload,
        headers={"Authorization": "Bearer " + read_key(), "Content-Type": "multipart/form-data; boundary=" + boundary},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=280) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        print("HTTP", error.code)
        if error.code != 401:
            detail = json.load(error).get("error", {})
            print({key: detail.get(key) for key in ("type", "code", "param", "message")})
        sys.exit(1)

    data = base64.b64decode(result["data"][0]["b64_json"])
    with open(target, "wb") as file:
        file.write(data)
    record = {
        "status": "candidate",
        "file": name + ".png",
        "sha256": hashlib.sha256(data).hexdigest(),
        "bytes": len(data),
        "endpoint": "POST https://api.openai.com/v1/images/edits",
        "model": MODEL,
        "size": fields["size"],
        "quality": fields["quality"],
        "n": 1,
        "reference": f"approved-reference.png crop {asset['reference_box']}",
        "usage": result.get("usage"),
        "prompt": asset["prompt"],
    }
    with open(os.path.join(out_dir, name + ".json"), "w", encoding="utf-8") as file:
        json.dump(record, file, ensure_ascii=False, indent=2)
    print(name, record["sha256"][:16], record["bytes"], "usage", (result.get("usage") or {}).get("total_tokens"))


if __name__ == "__main__":
    main()
