#!/usr/bin/env python3
"""承認済みSourceを正規化し、Unity用の単一Production Atlasへ格納する。

見た目の質感はSourceから保持する。許可する処理は切り出し、外周Magentaの透過、
Nearest Neighbor縮小、透明Padding、道路接続形状の決定的合成、格納と検査に限定する。
"""

from __future__ import annotations

from collections import deque
import hashlib
import json
from pathlib import Path
from statistics import median

from PIL import Image


MASTER_PATH = Path("Assets/Art/Map/Source/Forest/forest-gba-design-master.png")
EXTENSION_ROOT = Path("Assets/Art/Map/Source/Forest/Extensions")
GENERATED_OBSTACLE_ROOT = Path(
    "Assets/Art/Generated/MapAuthoring/Source/ObstacleVisuals"
)
GENERATED_DECORATION_ROOT = Path(
    "Assets/Art/Generated/MapAuthoring/Source/Decorations"
)
GENERATED_SURFACE_ROOT = Path(
    "Assets/Art/Generated/MapAuthoring/Source/Surfaces"
)
GENERATED_EDGE_ROOT = Path(
    "Assets/Art/Generated/MapAuthoring/Source/Edges"
)
OUTPUT_ROOT = Path("Assets/Art/Map/Production/Forest")
ATLAS_PATH = OUTPUT_ROOT / "forest-gba-production-atlas.png"
CATALOG_PATH = OUTPUT_ROOT / "forest-gba-production-atlas.json"
QA_PATH = Path("Assets/Art/Generated/MapAuthoring/QA/forest-gba-production-atlas-v4-3.png")
MASTER_SHA256 = "D90F51F33EFB470728924B8088035E777AAE7E0DF133AB3A799522114A50B705"

ATLAS_SIZE = 1024
TILE_SIZE = 32
EDGE_VARIANT_COUNT = 4
ROAD_SOCKET_START = 6
ROAD_SOCKET_END = 26
ROAD_SOCKET_COLLAR = 2

ROW_1 = (12, 175)
ROW_5 = (710, 900)
ROW_7 = (1094, 1254)
TOP_COLUMNS = (
    (14, 175),
    (188, 347),
    (361, 530),
    (544, 707),
    (720, 890),
    (903, 1063),
    (1077, 1237),
)
PROP_COLUMNS = (
    (14, 172),
    (197, 331),
    (355, 496),
    (512, 642),
    (663, 812),
    (842, 953),
    (990, 1088),
    (1128, 1225),
)

NORTH = 1
EAST = 2
SOUTH = 4
WEST = 8
BLOB_NORTH = 1
BLOB_NORTH_EAST = 2
BLOB_EAST = 4
BLOB_SOUTH_EAST = 8
BLOB_SOUTH = 16
BLOB_SOUTH_WEST = 32
BLOB_WEST = 64
BLOB_NORTH_WEST = 128


def canonical_blob_masks() -> list[int]:
    """隣接Cardinalが揃う対角だけを含む47種類のMaskを返す。"""

    result: list[int] = []
    for cardinal_mask in range(16):
        blob_mask = 0
        if cardinal_mask & NORTH:
            blob_mask |= BLOB_NORTH
        if cardinal_mask & EAST:
            blob_mask |= BLOB_EAST
        if cardinal_mask & SOUTH:
            blob_mask |= BLOB_SOUTH
        if cardinal_mask & WEST:
            blob_mask |= BLOB_WEST

        diagonals: list[int] = []
        if cardinal_mask & NORTH and cardinal_mask & EAST:
            diagonals.append(BLOB_NORTH_EAST)
        if cardinal_mask & EAST and cardinal_mask & SOUTH:
            diagonals.append(BLOB_SOUTH_EAST)
        if cardinal_mask & SOUTH and cardinal_mask & WEST:
            diagonals.append(BLOB_SOUTH_WEST)
        if cardinal_mask & WEST and cardinal_mask & NORTH:
            diagonals.append(BLOB_NORTH_WEST)

        for selection in range(1 << len(diagonals)):
            mask = blob_mask
            for index, bit in enumerate(diagonals):
                if selection & (1 << index):
                    mask |= bit
            result.append(mask)

    masks = sorted(set(result))
    if len(masks) != 47:
        raise ValueError(f"Expected 47 Blob masks, got {len(masks)}")
    return masks


def is_key_color(pixel: tuple[int, int, int, int]) -> bool:
    """ImageGenの平坦なMagentaと、その境界に残る近似色を判定する。"""

    red, green, blue, alpha = pixel
    # ImageGen は指定した単色背景にも、ごく細い色揺れを付けることがある。
    # 境界から連結した高彩度の赤紫だけを Key とみなし、素材内の花色は残す。
    return (
        alpha > 0
        and red >= 130
        and blue >= 125
        and red - green >= 35
        and blue - green >= 35
        and abs(red - blue) <= 95
    )


def is_extended_key_color(pixel: tuple[int, int, int, int]) -> bool:
    """Water Cell外周の生成色揺れまで含め、Crop境界を見つけるための広義Keyを判定する。"""

    red, green, blue, alpha = pixel
    return (
        alpha > 0
        and red >= 85
        and blue >= 85
        and red + blue >= green * 2 + 30
    )


def border_key_to_alpha(image: Image.Image, key_test=is_key_color) -> Image.Image:
    """外周から連続するMagentaだけを透明化し、素材内の花色は保持する。"""

    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    pending: deque[tuple[int, int]] = deque()
    visited: set[tuple[int, int]] = set()
    for x in range(width):
        pending.append((x, 0))
        pending.append((x, height - 1))
    for y in range(height):
        pending.append((0, y))
        pending.append((width - 1, y))

    while pending:
        x, y = pending.popleft()
        if (x, y) in visited or not key_test(pixels[x, y]):
            continue
        visited.add((x, y))
        pixels[x, y] = (255, 0, 255, 0)
        if x > 0:
            pending.append((x - 1, y))
        if x + 1 < width:
            pending.append((x + 1, y))
        if y > 0:
            pending.append((x, y - 1))
        if y + 1 < height:
            pending.append((x, y + 1))

    alpha = rgba.getchannel("A").point(lambda value: 255 if value else 0)
    rgba.putalpha(alpha)
    return rgba


def contiguous_ranges(values: list[bool]) -> list[tuple[int, int]]:
    """Trueが連続する半開区間を返す。"""

    ranges: list[tuple[int, int]] = []
    start: int | None = None
    for index, value in enumerate(values + [False]):
        if value and start is None:
            start = index
        elif not value and start is not None:
            ranges.append((start, index))
            start = None
    return ranges


def non_key_mask(image: Image.Image) -> list[list[bool]]:
    """Source Sheetの作画PixelだけをTrueにしたMaskを作る。"""

    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    return [
        [not is_key_color(pixels[x, y]) for x in range(width)]
        for y in range(height)
    ]


def detect_equal_grid(image: Image.Image, columns: int, rows: int) -> list[tuple[int, int, int, int]]:
    """Magenta gutterで分離された均等Gridを検出し、全Cellへ同じ大きさのRectを返す。"""

    mask = non_key_mask(image)
    width, height = image.size
    row_ranges = contiguous_ranges([any(row) for row in mask])
    row_ranges = [bounds for bounds in row_ranges if bounds[1] - bounds[0] >= 8]
    if len(row_ranges) != rows:
        raise ValueError(f"Expected {rows} occupied rows, got {len(row_ranges)} for {image.size}")

    centers_by_column: list[list[float]] = [[] for _ in range(columns)]
    widths: list[int] = []
    for top, bottom in row_ranges:
        occupied_columns = [any(mask[y][x] for y in range(top, bottom)) for x in range(width)]
        ranges = [bounds for bounds in contiguous_ranges(occupied_columns) if bounds[1] - bounds[0] >= 4]
        expected_in_row = columns - 1 if columns == 8 and rows == 6 and top == row_ranges[-1][0] else columns
        if len(ranges) != expected_in_row:
            raise ValueError(f"Expected {columns} occupied columns, got {len(ranges)} in row {top}:{bottom}")
        for column, (left, right) in enumerate(ranges):
            centers_by_column[column].append((left + right - 1) / 2)
            widths.append(right - left)

    column_centers = [median(values) for values in centers_by_column]
    row_centers = [(top + bottom - 1) / 2 for top, bottom in row_ranges]
    heights = [bottom - top for top, bottom in row_ranges]
    cell_size = max(max(widths), max(heights))
    if cell_size % 2:
        cell_size += 1

    rects: list[tuple[int, int, int, int]] = []
    for row_center in row_centers:
        for column_center in column_centers:
            left = round(column_center - cell_size / 2)
            top = round(row_center - cell_size / 2)
            left = max(0, min(width - cell_size, left))
            top = max(0, min(height - cell_size, top))
            rects.append((left, top, cell_size, cell_size))
    return rects


def crop_rect(image: Image.Image, rect: tuple[int, int, int, int]) -> Image.Image:
    """左上原点のRectを切り出す。"""

    x, y, width, height = rect
    return image.crop((x, y, x + width, y + height))


def normalize_tile(image: Image.Image) -> Image.Image:
    """作画を変更せず、1CellをNearest Neighborで32pxへ規格化する。"""

    rgba = border_key_to_alpha(image)
    return rgba.resize((TILE_SIZE, TILE_SIZE), Image.Resampling.NEAREST)


def normalize_generated_surface_tile(image: Image.Image) -> Image.Image:
    """生成SheetのMagenta gutterを除き、全面地形差分を不透明32pxへ規格化する。"""

    # 生成画像のgutter境界には赤紫の色揺れが一列だけ残る場合がある。
    # 広義Keyを外周連結Pixelだけへ適用し、地形色を巻き込まずに除去する。
    rgba = border_key_to_alpha(image, key_test=is_extended_key_color)
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Generated surface cell has no opaque pixels")
    cropped = rgba.crop(bounds).resize((TILE_SIZE, TILE_SIZE), Image.Resampling.NEAREST)
    cropped.putalpha(Image.new("L", cropped.size, 255))
    pixels = cropped.load()
    if any(
        is_extended_key_color(pixels[x, y])
        for y in range(cropped.height)
        for x in range(cropped.width)
    ):
        raise ValueError("Generated surface cell retains magenta gutter pixels")
    return cropped


def load_surface_variants(
    source_path: Path,
    columns: int,
    rows: int,
) -> list[tuple[Image.Image, tuple[int, int, int, int]]]:
    """均等Gridの生成Sourceから、同一材質の全面差分と元Rectを順に読み出す。"""

    source = Image.open(source_path).convert("RGBA")
    rects = detect_equal_grid(source, columns, rows)
    expected_count = columns * rows
    if len(rects) != expected_count:
        raise ValueError(
            f"Expected {expected_count} surface variants, got {len(rects)}: {source_path}"
        )
    variants = [
        (normalize_generated_surface_tile(crop_rect(source, rect)), rect)
        for rect in rects
    ]
    images = [variant[0] for variant in variants]
    if len({image.tobytes() for image in images}) != expected_count:
        raise ValueError(f"Surface variants must be visually distinct: {source_path}")

    average_colors: list[tuple[float, float, float]] = []
    for image in images:
        pixels = image.load()
        pixel_count = image.width * image.height
        average_colors.append(
            tuple(
                sum(
                    pixels[x, y][channel]
                    for y in range(image.height)
                    for x in range(image.width)
                )
                / pixel_count
                for channel in range(3)
            )
        )
    for channel in range(3):
        channel_values = [color[channel] for color in average_colors]
        if max(channel_values) - min(channel_values) > 8.0:
            raise ValueError(
                f"Surface variants differ too much in average color: {source_path}, channel={channel}"
            )

    # Cell外周だけ暗い／明るい生成物は、並べたときに32pxの枠として現れる。
    # 各辺の平均色がTile全体から大きく逸脱しないことをSource更新時にも保証する。
    for image, average_color in zip(images, average_colors):
        pixels = image.load()
        edges = (
            [pixels[x, 0] for x in range(image.width)],
            [pixels[x, image.height - 1] for x in range(image.width)],
            [pixels[0, y] for y in range(image.height)],
            [pixels[image.width - 1, y] for y in range(image.height)],
        )
        for edge in edges:
            for channel in range(3):
                edge_average = sum(pixel[channel] for pixel in edge) / len(edge)
                if abs(edge_average - average_color[channel]) > 18.0:
                    raise ValueError(
                        f"Surface variant has a visible edge band: {source_path}, channel={channel}"
                    )
    return variants


def normalize_edge_profile_tile(image: Image.Image, target_span: int = 22) -> Image.Image:
    """Magenta上の孤立した道路輪郭を比率維持で32px中央へ置き、道路外を透明にする。"""

    rgba = border_key_to_alpha(image, key_test=is_extended_key_color)
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Generated edge profile has no opaque pixels")
    content = rgba.crop(bounds)
    scale = min(target_span / content.width, target_span / content.height)
    target_size = (
        max(1, round(content.width * scale)),
        max(1, round(content.height * scale)),
    )
    content = content.resize(target_size, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (TILE_SIZE, TILE_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(
        content,
        ((TILE_SIZE - content.width) // 2, (TILE_SIZE - content.height) // 2),
    )
    alpha = canvas.getchannel("A").point(lambda value: 255 if value else 0)
    alpha_pixels = alpha.load()
    remaining = {
        (x, y)
        for y in range(TILE_SIZE)
        for x in range(TILE_SIZE)
        if alpha_pixels[x, y] > 0
    }
    components: list[set[tuple[int, int]]] = []
    while remaining:
        first = remaining.pop()
        component = {first}
        pending = deque([first])
        while pending:
            x, y = pending.popleft()
            for target in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if target in remaining:
                    remaining.remove(target)
                    component.add(target)
                    pending.append(target)
        components.append(component)
    if not components:
        raise ValueError("Generated edge profile has no connected material region")
    largest = max(components, key=len)
    for y in range(TILE_SIZE):
        for x in range(TILE_SIZE):
            alpha_pixels[x, y] = 255 if (x, y) in largest else 0
    canvas.putalpha(alpha)
    if canvas.getchannel("A").getbbox() in (None, (0, 0, TILE_SIZE, TILE_SIZE)):
        raise ValueError("Edge profile must be isolated from every Tile edge")
    return canvas


def load_edge_profile_variants(
    source_path: Path,
) -> list[tuple[Image.Image, tuple[int, int, int, int]]]:
    """2x2生成Sheetを等分し、道路外が透明な4つの孤立輪郭Donorとして読む。"""

    source = Image.open(source_path).convert("RGBA")
    if source.width % 2 or source.height % 2:
        raise ValueError(f"Edge profile sheet must divide into 2x2 cells: {source_path}")
    cell_width = source.width // 2
    cell_height = source.height // 2
    variants: list[tuple[Image.Image, tuple[int, int, int, int]]] = []
    for row in range(2):
        for column in range(2):
            rect = (column * cell_width, row * cell_height, cell_width, cell_height)
            variants.append((normalize_edge_profile_tile(crop_rect(source, rect)), rect))

    if len({variant[0].getchannel("A").tobytes() for variant in variants}) != EDGE_VARIANT_COUNT:
        raise ValueError(f"Edge profile silhouettes must all differ: {source_path}")
    return variants


def average_rgb(image: Image.Image) -> tuple[float, float, float]:
    """不透明Pixelの平均RGBを返し、素材と芝を分ける基準色に使う。"""

    pixels = [pixel for pixel in image.convert("RGBA").get_flattened_data() if pixel[3] > 0]
    if not pixels:
        raise ValueError("Cannot calculate an average color from an empty image")
    return tuple(sum(pixel[channel] for pixel in pixels) / len(pixels) for channel in range(3))


def dilate_mask(mask: list[list[bool]], radius: int) -> list[list[bool]]:
    """材質Coreを指定Pixelだけ広げ、境界影と細い草Fringeを道路Overlayへ含める。"""

    height = len(mask)
    width = len(mask[0])
    result = [[False for _ in range(width)] for _ in range(height)]
    for y in range(height):
        for x in range(width):
            if not mask[y][x]:
                continue
            for offset_y in range(-radius, radius + 1):
                for offset_x in range(-radius, radius + 1):
                    target_x = x + offset_x
                    target_y = y + offset_y
                    if 0 <= target_x < width and 0 <= target_y < height:
                        result[target_y][target_x] = True
    return result


def fill_material_region(core: list[list[bool]]) -> list[list[bool]]:
    """外周から到達できる背景だけを除き、石の目地など道路内部の穴を一体の領域にする。"""

    barrier = dilate_mask(core, 2)
    height = len(barrier)
    width = len(barrier[0])
    outside = [[False for _ in range(width)] for _ in range(height)]
    pending: deque[tuple[int, int]] = deque()
    for x in range(width):
        pending.append((x, 0))
        pending.append((x, height - 1))
    for y in range(height):
        pending.append((0, y))
        pending.append((width - 1, y))

    while pending:
        x, y = pending.popleft()
        if outside[y][x] or barrier[y][x]:
            continue
        outside[y][x] = True
        if x > 0:
            pending.append((x - 1, y))
        if x + 1 < width:
            pending.append((x + 1, y))
        if y > 0:
            pending.append((x, y - 1))
        if y + 1 < height:
            pending.append((x, y + 1))

    return [[not outside[y][x] for x in range(width)] for y in range(height)]


def canonicalize_overlay_edges(
    image: Image.Image,
    surface: Image.Image,
    open_directions: int,
) -> Image.Image:
    """接続辺の中央帯と曲がり角だけを塞ぎ、直線Cell境界の規則的な張り出しを防ぐ。"""

    result = image.copy()
    pixels = result.load()
    surface_pixels = surface.load()

    for depth in range(ROAD_SOCKET_COLLAR):
        for coordinate in range(TILE_SIZE):
            pixels[coordinate, depth] = (0, 0, 0, 0)
            pixels[coordinate, TILE_SIZE - 1 - depth] = (0, 0, 0, 0)
            pixels[depth, coordinate] = (0, 0, 0, 0)
            pixels[TILE_SIZE - 1 - depth, coordinate] = (0, 0, 0, 0)

    # 直線は元Donorと同じ中央20pxだけを接続し、曲がり・分岐で直交する二辺が開く角だけを埋める。
    # 太い道路の内側だけは角まで埋まり、露出外周へ32px周期の張り出しを作らない。
    for direction in (NORTH, EAST, SOUTH, WEST):
        expected_edge = expected_overlay_alpha_edge(open_directions, direction)
        for coordinate, expected_alpha in enumerate(expected_edge):
            if expected_alpha == 0:
                continue
            for depth in range(ROAD_SOCKET_COLLAR):
                if direction == NORTH:
                    x, y = coordinate, depth
                elif direction == SOUTH:
                    x, y = coordinate, TILE_SIZE - 1 - depth
                elif direction == WEST:
                    x, y = depth, coordinate
                else:
                    x, y = TILE_SIZE - 1 - depth, coordinate
                pixels[x, y] = surface_pixels[x, y]
    return result


def expected_overlay_alpha_edge(open_directions: int, direction: int) -> tuple[int, ...]:
    """接続辺中央と、直交する二辺がともに開く角だけを不透明とする期待Alphaを返す。"""

    if direction not in (NORTH, EAST, SOUTH, WEST):
        raise ValueError(f"Unsupported cardinal direction: {direction}")

    direction_is_open = bool(open_directions & direction)
    if direction in (NORTH, SOUTH):
        low_corner_direction = WEST
        high_corner_direction = EAST
    else:
        low_corner_direction = NORTH
        high_corner_direction = SOUTH

    expected: list[int] = []
    for coordinate in range(TILE_SIZE):
        if ROAD_SOCKET_START <= coordinate < ROAD_SOCKET_END:
            is_opaque = direction_is_open
        elif coordinate < ROAD_SOCKET_START:
            is_opaque = direction_is_open and bool(open_directions & low_corner_direction)
        else:
            is_opaque = direction_is_open and bool(open_directions & high_corner_direction)
        expected.append(255 if is_opaque else 0)
    return tuple(expected)


def extract_material_overlay(
    image: Image.Image,
    material_average: tuple[float, float, float],
    grass_average: tuple[float, float, float],
    surface: Image.Image,
    open_directions: int,
) -> Image.Image:
    """旧Donorに焼かれた芝を平均色距離で外し、道路と境界影だけの透明Overlayへ変換する。"""

    rgba = image.convert("RGBA")
    pixels = rgba.load()
    core: list[list[bool]] = []
    for y in range(rgba.height):
        row: list[bool] = []
        for x in range(rgba.width):
            pixel = pixels[x, y]
            material_distance = sum(
                (pixel[channel] - material_average[channel]) ** 2 for channel in range(3)
            )
            grass_distance = sum(
                (pixel[channel] - grass_average[channel]) ** 2 for channel in range(3)
            )
            row.append(material_distance < grass_distance)
        core.append(row)

    material_region = fill_material_region(core)
    alpha = Image.new("L", rgba.size, 0)
    alpha_pixels = alpha.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            alpha_pixels[x, y] = 255 if material_region[y][x] else 0
    rgba.putalpha(alpha)

    # AI Donorの石目地や暗部が色距離で分断されても、接続口から中央までの道路幅は
    # 全材質で共通に保つ。材質Pixelは承認済み全面Surfaceから補い、輪郭だけをDonorへ任せる。
    overlay_pixels = rgba.load()
    surface_pixels = surface.load()
    if open_directions & (NORTH | SOUTH):
        for y in range(TILE_SIZE):
            for x in range(ROAD_SOCKET_START, ROAD_SOCKET_END):
                red, green, blue, _ = surface_pixels[x, y]
                overlay_pixels[x, y] = (red, green, blue, 255)
    if open_directions & (EAST | WEST):
        for y in range(ROAD_SOCKET_START, ROAD_SOCKET_END):
            for x in range(TILE_SIZE):
                red, green, blue, _ = surface_pixels[x, y]
                overlay_pixels[x, y] = (red, green, blue, 255)
    return canonicalize_overlay_edges(rgba, surface, open_directions)


def edge_socket(image: Image.Image, direction: int) -> tuple[tuple[int, int, int, int], ...]:
    """Cardinal edge中央の12pxを取り出し、接続口の同一性検査へ使う。"""

    socket_start = TILE_SIZE // 2 - 6
    socket_end = socket_start + 12
    if direction == NORTH:
        return tuple(image.getpixel((x, 0)) for x in range(socket_start, socket_end))
    if direction == SOUTH:
        return tuple(image.getpixel((x, TILE_SIZE - 1)) for x in range(socket_start, socket_end))
    if direction == EAST:
        return tuple(image.getpixel((TILE_SIZE - 1, y)) for y in range(socket_start, socket_end))
    if direction == WEST:
        return tuple(image.getpixel((0, y)) for y in range(socket_start, socket_end))
    raise ValueError(f"Unsupported cardinal direction: {direction}")


def compose_cardinal_tile(
    closed: Image.Image,
    vertical: Image.Image,
    horizontal: Image.Image,
    interior: Image.Image,
    mask: int,
) -> Image.Image:
    """Mask方向を接続し、隣り合う2方向の角は全面素材で埋めて太い領域を連続させる。"""

    result = closed.copy()
    center = (TILE_SIZE - 1) / 2
    for y in range(TILE_SIZE):
        for x in range(TILE_SIZE):
            delta_x = x - center
            delta_y = y - center
            if abs(delta_x) > abs(delta_y):
                direction = EAST if delta_x > 0 else WEST
                if mask & direction:
                    result.putpixel((x, y), horizontal.getpixel((x, y)))
            else:
                direction = SOUTH if delta_y > 0 else NORTH
                if mask & direction:
                    result.putpixel((x, y), vertical.getpixel((x, y)))

    # Cardinalの十字素材には草の四隅が残るため、そのまま太い道路へ並べると
    # 4 Cellの境界ごとに草穴が生じる。隣接する二方向が共に開く角だけを全面素材で
    # 埋め、1 Cell幅の端形状と3 Cell幅以上の内部形状を同じ16 Maskで両立させる。
    corner_specs = (
        (0, 0, TILE_SIZE // 2, TILE_SIZE // 2, NORTH | WEST),
        (TILE_SIZE // 2, 0, TILE_SIZE, TILE_SIZE // 2, NORTH | EAST),
        (TILE_SIZE // 2, TILE_SIZE // 2, TILE_SIZE, TILE_SIZE, SOUTH | EAST),
        (0, TILE_SIZE // 2, TILE_SIZE // 2, TILE_SIZE, SOUTH | WEST),
    )
    for left, top, right, bottom, required_directions in corner_specs:
        if mask & required_directions != required_directions:
            continue
        result.paste(
            interior.crop((left, top, right, bottom)),
            (left, top),
        )

    return result


def opaque_pixel_count(image: Image.Image) -> int:
    """道路Overlayの不透明Pixel数を返す。"""

    return sum(1 for value in image.getchannel("A").get_flattened_data() if value > 0)


def keep_largest_opaque_region(image: Image.Image) -> Image.Image:
    """色距離抽出で残った孤立Pixelを除き、中央道路につながる最大領域だけを保持する。"""

    result = image.copy()
    alpha = result.getchannel("A")
    pixels = alpha.load()
    remaining = {
        (x, y)
        for y in range(result.height)
        for x in range(result.width)
        if pixels[x, y] > 0
    }
    components: list[set[tuple[int, int]]] = []
    while remaining:
        first = remaining.pop()
        component = {first}
        pending = deque([first])
        while pending:
            x, y = pending.popleft()
            for target in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if target in remaining:
                    remaining.remove(target)
                    component.add(target)
                    pending.append(target)
        components.append(component)
    if not components:
        raise ValueError("Road overlay has no opaque region")
    largest = max(components, key=len)
    for y in range(result.height):
        for x in range(result.width):
            pixels[x, y] = 255 if (x, y) in largest else 0
    result.putalpha(alpha)
    return result


def opaque_region_is_connected(image: Image.Image) -> bool:
    """道路Overlayの不透明領域が4近傍で一つにつながっているかを調べる。"""

    alpha = image.getchannel("A")
    pixels = alpha.load()
    opaque = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if pixels[x, y] > 0
    }
    if not opaque:
        return False
    first = next(iter(opaque))
    visited = {first}
    pending = deque([first])
    while pending:
        x, y = pending.popleft()
        for target in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if target in opaque and target not in visited:
                visited.add(target)
                pending.append(target)
    return len(visited) == len(opaque)


def alpha_edge(image: Image.Image, direction: int, depth: int = 0) -> tuple[int, ...]:
    """指定辺から内側depth位置のAlpha署名を返す。"""

    if depth < 0 or depth >= TILE_SIZE:
        raise ValueError(f"Alpha edge depth is out of range: {depth}")

    alpha = image.getchannel("A")
    if direction == NORTH:
        return tuple(alpha.getpixel((x, depth)) for x in range(TILE_SIZE))
    if direction == SOUTH:
        return tuple(alpha.getpixel((x, TILE_SIZE - 1 - depth)) for x in range(TILE_SIZE))
    if direction == EAST:
        return tuple(alpha.getpixel((TILE_SIZE - 1 - depth, y)) for y in range(TILE_SIZE))
    if direction == WEST:
        return tuple(alpha.getpixel((depth, y)) for y in range(TILE_SIZE))
    raise ValueError(f"Unsupported cardinal direction: {direction}")


def build_cardinal_variant_tiles(
    source: Image.Image,
    rects: list[tuple[int, int, int, int]],
    family: str,
    surface_variants: list[tuple[Image.Image, tuple[int, int, int, int]]],
    edge_profile_variants: list[tuple[Image.Image, tuple[int, int, int, int]]],
    grass_average: tuple[float, float, float],
) -> list[list[Image.Image]]:
    """透明な輪郭Donorと固定Socketから、全Cardinal maskを4シルエット差分で構築する。"""

    if len(surface_variants) != EDGE_VARIANT_COUNT or len(edge_profile_variants) != EDGE_VARIANT_COUNT:
        raise ValueError(f"{family} needs {EDGE_VARIANT_COUNT} surface and edge variants")

    material_average = average_rgb(surface_variants[0][0])
    vertical_source = normalize_tile(crop_rect(source, rects[1]))
    horizontal_source = normalize_tile(crop_rect(source, rects[2]))
    vertical = extract_material_overlay(
        vertical_source,
        material_average,
        grass_average,
        surface_variants[0][0],
        NORTH | SOUTH,
    )
    horizontal = extract_material_overlay(
        horizontal_source,
        material_average,
        grass_average,
        surface_variants[0][0],
        EAST | WEST,
    )

    result: list[list[Image.Image]] = [[] for _ in range(16)]
    for variant_index in range(EDGE_VARIANT_COUNT):
        closed = edge_profile_variants[variant_index][0]
        interior = surface_variants[variant_index][0]
        for mask in range(16):
            tile = (
                interior.copy()
                if mask == NORTH | EAST | SOUTH | WEST
                else compose_cardinal_tile(closed, vertical, horizontal, interior, mask)
            )
            if mask != NORTH | EAST | SOUTH | WEST:
                # 各Donorの輪郭差が大きくても接続枝が中央で分断されないよう、
                # 道路の中心だけは全面Surfaceで共有し、外周Silhouetteだけを差分化する。
                hub_start = TILE_SIZE // 2 - 6
                hub_end = TILE_SIZE // 2 + 6
                tile.paste(
                    interior.crop((hub_start, hub_start, hub_end, hub_end)),
                    (hub_start, hub_start),
                )
            if mask != 0x0F:
                tile = canonicalize_overlay_edges(tile, interior, mask)
                tile = keep_largest_opaque_region(tile)
            alpha = tile.getchannel("A")
            alpha_values = alpha.get_flattened_data()
            if any(value not in (0, 255) for value in alpha_values):
                raise ValueError(f"{family}_{mask:02x} must use binary alpha")
            if not opaque_region_is_connected(tile):
                raise ValueError(f"{family}_{mask:02x} road pixels are disconnected")

            for direction in (NORTH, EAST, SOUTH, WEST):
                expected = expected_overlay_alpha_edge(mask, direction)
                for depth in range(ROAD_SOCKET_COLLAR):
                    edge = alpha_edge(tile, direction, depth)
                    if edge != expected:
                        raise ValueError(
                            f"{family}_{mask:02x} has an invalid edge profile: "
                            f"direction={direction}, depth={depth}"
                        )
            result[mask].append(tile)

    for mask, variants in enumerate(result):
        if len(variants) != EDGE_VARIANT_COUNT:
            raise ValueError(f"{family}_{mask:02x} has an invalid variant count")
        if mask == 0x0F:
            if any(variant.getchannel("A").getextrema() != (255, 255) for variant in variants):
                raise ValueError(f"{family}_0f must be fully opaque")
            continue

        silhouettes = [variant.getchannel("A").tobytes() for variant in variants]
        if len(set(silhouettes)) != EDGE_VARIANT_COUNT:
            raise ValueError(f"{family}_{mask:02x} edge silhouettes must all differ")
        areas = [opaque_pixel_count(variant) for variant in variants]
        average_area = sum(areas) / len(areas)
        if any(abs(area - average_area) / average_area > 0.18 for area in areas):
            raise ValueError(f"{family}_{mask:02x} edge variants differ too much in area: {areas}")

    return result


def normalize_full_tile(image: Image.Image) -> Image.Image:
    """全面を覆う地面素材の外周Keyだけを切り落とし、透明なTile境界を残さず32px化する。"""

    rgba = border_key_to_alpha(image)
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Full tile source has no opaque pixels")
    return rgba.crop(bounds).resize((TILE_SIZE, TILE_SIZE), Image.Resampling.NEAREST)


def normalize_center_interior_tile(image: Image.Image) -> Image.Image:
    """全面地面の外周陰影を除いた中央70%を使い、反復時に暗い格子線を作らない。"""

    rgba = border_key_to_alpha(image)
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Interior tile source has no opaque pixels")
    content = rgba.crop(bounds)
    margin_x = max(1, round(content.width * 0.15))
    margin_y = max(1, round(content.height * 0.15))
    center = content.crop(
        (
            margin_x,
            margin_y,
            content.width - margin_x,
            content.height - margin_y,
        )
    )
    return center.resize((TILE_SIZE, TILE_SIZE), Image.Resampling.NEAREST)


def normalize_opaque_tile(image: Image.Image) -> Image.Image:
    """全面Water Cellの外周Gutterだけを特定し、元のRGBを保持した不透明Tileへ正規化する。"""

    source = image.convert("RGBA")
    keyed = border_key_to_alpha(source, is_extended_key_color)
    bounds = keyed.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Opaque tile source has no content pixels")
    content = source.crop(bounds)
    content.putalpha(Image.new("L", content.size, 255))
    return content.resize((TILE_SIZE, TILE_SIZE), Image.Resampling.NEAREST)


def fit_prop(image: Image.Image, canvas_size: tuple[int, int]) -> Image.Image:
    """透明化したPropを比率維持で縮小し、足元中央を揃えて透明Canvasへ置く。"""

    rgba = border_key_to_alpha(image)
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Prop source has no opaque pixels")
    content = rgba.crop(bounds)
    canvas_width, canvas_height = canvas_size
    scale = min((canvas_width - 2) / content.width, (canvas_height - 2) / content.height, 1.0)
    target_size = (
        max(1, round(content.width * scale)),
        max(1, round(content.height * scale)),
    )
    content = content.resize(target_size, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    x = (canvas_width - content.width) // 2
    y = canvas_height - content.height
    canvas.alpha_composite(content, (x, y))
    return canvas


def split_tree_sheet(image: Image.Image) -> list[tuple[Image.Image, tuple[int, int, int, int]]]:
    """3本のTreeをMagenta区切りから検出し、同じ64px足元契約へ規格化する。"""

    mask = non_key_mask(image)
    width, height = image.size
    occupied_columns = [any(mask[y][x] for y in range(height)) for x in range(width)]
    ranges = [bounds for bounds in contiguous_ranges(occupied_columns) if bounds[1] - bounds[0] >= 16]
    if len(ranges) != 3:
        raise ValueError(f"Expected 3 tree columns, got {len(ranges)}")

    result: list[tuple[Image.Image, tuple[int, int, int, int]]] = []
    for left, right in ranges:
        occupied_rows = [any(mask[y][x] for x in range(left, right)) for y in range(height)]
        row_ranges = [bounds for bounds in contiguous_ranges(occupied_rows) if bounds[1] - bounds[0] >= 16]
        if len(row_ranges) != 1:
            raise ValueError(f"Expected one tree component, got {len(row_ranges)}")
        top, bottom = row_ranges[0]
        rect = (left, top, right - left, bottom - top)
        result.append((fit_prop(crop_rect(image, rect), (64, 64)), rect))
    return result


def source_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main() -> None:
    if (ROAD_SOCKET_START, ROAD_SOCKET_END) != (6, 26):
        raise ValueError("Road socket contract must remain the centered 20px range [6, 26)")
    if source_hash(MASTER_PATH) != MASTER_SHA256:
        raise ValueError("Design master hash differs from the approved v4 contract")

    master = Image.open(MASTER_PATH).convert("RGBA")
    sprites: list[dict[str, object]] = []

    def add_sprite(
        name: str,
        family: str,
        mask: int,
        image: Image.Image,
        source_path: Path,
        source_rect: tuple[int, int, int, int],
        pivot: tuple[float, float] = (0.5, 0.5),
    ) -> None:
        sprites.append(
            {
                "name": name,
                "family": family,
                "mask": mask,
                "image": image.convert("RGBA"),
                "sourcePath": source_path.as_posix(),
                "sourceRect": source_rect,
                "pivot": pivot,
            }
        )

    grass_surface_path = GENERATED_SURFACE_ROOT / "grass-surface-variants-source.png"
    grass_variants = load_surface_variants(grass_surface_path, 4, 2)
    grass_average = tuple(
        sum(average_rgb(variant[0])[channel] for variant in grass_variants)
        / len(grass_variants)
        for channel in range(3)
    )
    for index, (tile, rect) in enumerate(grass_variants):
        add_sprite(
            f"ground_grass_{index + 1:02d}",
            "grass",
            -1,
            tile,
            grass_surface_path,
            rect,
        )

    family_specs = (
        ("dirt", "dirt-cardinal16-source.png", 4, 4, list(range(16))),
        ("stone", "stone-cardinal16-source.png", 4, 4, list(range(16))),
        ("water", "water-blob47-source.png", 8, 6, canonical_blob_masks()),
    )
    for family, filename, columns, rows, masks in family_specs:
        source_path = EXTENSION_ROOT / filename
        source = Image.open(source_path).convert("RGBA")
        rects = detect_equal_grid(source, columns, rows)
        if len(rects) < len(masks):
            raise ValueError(f"{family} needs {len(masks)} cells, got {len(rects)}")
        if family in ("dirt", "stone"):
            surface_path = GENERATED_SURFACE_ROOT / f"{family}-surface-variants-source.png"
            surface_variants = load_surface_variants(surface_path, 2, 2)
            edge_path = GENERATED_EDGE_ROOT / f"{family}-edge-profile-variants-source.png"
            edge_variants = load_edge_profile_variants(edge_path)
            cardinal_variants = build_cardinal_variant_tiles(
                source,
                rects,
                family,
                surface_variants,
                edge_variants,
                grass_average,
            )
            for mask, variants in enumerate(cardinal_variants):
                for variant_index, tile in enumerate(variants, start=1):
                    suffix = "" if variant_index == 1 else f"_v{variant_index:02d}"
                    source_variant = (
                        surface_variants[variant_index - 1]
                        if mask == 0x0F
                        else edge_variants[variant_index - 1]
                    )
                    add_sprite(
                        f"{family}_{mask:02x}{suffix}",
                        family,
                        mask,
                        tile,
                        surface_path if mask == 0x0F else edge_path,
                        source_variant[1],
                    )
            continue

        water_surface_path = GENERATED_SURFACE_ROOT / "water-surface-variants-source.png"
        water_surface_variants = load_surface_variants(water_surface_path, 2, 2)
        for index, mask in enumerate(masks):
            rect = rects[index]
            cell = crop_rect(source, rect)
            tile = (
                water_surface_variants[0][0]
                if mask == 0xFF
                else normalize_opaque_tile(cell)
            )
            sprite_source_path = water_surface_path if mask == 0xFF else source_path
            sprite_source_rect = water_surface_variants[0][1] if mask == 0xFF else rect
            add_sprite(
                f"{family}_{mask:02x}",
                family,
                mask,
                tile,
                sprite_source_path,
                sprite_source_rect,
            )

        for variant_index, (tile, rect) in enumerate(water_surface_variants[1:], start=2):
            add_sprite(
                f"water_ff_v{variant_index:02d}",
                "water",
                0xFF,
                tile,
                water_surface_path,
                rect,
            )

    detail_specs = (
        ("detail_tall_grass", "decoration-tall-grass.png", (48, 32)),
        ("detail_flower_patch", "decoration-flower-patch.png", (48, 32)),
        ("detail_wildflowers", "decoration-wildflowers.png", (32, 32)),
        ("detail_reeds", "decoration-reeds.png", (32, 48)),
    )
    decoration_source_paths: list[Path] = []
    for name, filename, canvas_size in detail_specs:
        source_path = GENERATED_DECORATION_ROOT / filename
        source = Image.open(source_path).convert("RGBA")
        source_rect = (0, 0, source.width, source.height)
        add_sprite(
            name,
            "detail",
            -1,
            fit_prop(source, canvas_size),
            source_path,
            source_rect,
            (0.5, 0.0),
        )
        decoration_source_paths.append(source_path)

    tree_source_path = EXTENSION_ROOT / "tree-variants-source.png"
    tree_source = Image.open(tree_source_path).convert("RGBA")
    for index, (tree, rect) in enumerate(split_tree_sheet(tree_source), start=1):
        add_sprite(
            f"prop_tree_medium_{index:02d}",
            "props",
            -1,
            tree,
            tree_source_path,
            rect,
            (0.5, 0.0),
        )

    prop_specs = (
        ("prop_blocking_bush", PROP_COLUMNS[1], (48, 32)),
        ("prop_mossy_rock", PROP_COLUMNS[2], (48, 32)),
        ("prop_stump", PROP_COLUMNS[3], (32, 32)),
        ("prop_fallen_log", PROP_COLUMNS[4], (48, 32)),
        ("prop_mushroom_patch", PROP_COLUMNS[5], (32, 32)),
        ("prop_chest", PROP_COLUMNS[6], (32, 32)),
        ("prop_signpost", PROP_COLUMNS[7], (32, 32)),
    )
    for name, column, canvas_size in prop_specs:
        left, right = column
        top, bottom = ROW_5
        rect = (left, top, right - left, bottom - top)
        add_sprite(
            name,
            "props",
            -1,
            fit_prop(crop_rect(master, rect), canvas_size),
            MASTER_PATH,
            rect,
            (0.5, 0.0),
        )

    # 森林と崖はTile接続から外し、Collisionと独立した大型表示Stampとして格納する。
    obstacle_specs = (
        ("obstacle_forest_mass_wide", "obstacle-forest-mass-wide.png", (192, 128), (0.5, 0.0)),
        ("obstacle_forest_mass_deep", "obstacle-forest-mass-deep.png", (160, 128), (0.5, 0.0)),
        ("obstacle_forest_front_strip", "obstacle-forest-front-strip.png", (224, 96), (0.5, 0.0)),
        ("obstacle_cliff_straight_wide", "obstacle-cliff-straight-wide.png", (192, 96), (0.5, 0.58)),
        ("obstacle_cliff_outer_corner", "obstacle-cliff-outer-corner.png", (128, 128), (0.5, 0.5)),
    )
    obstacle_source_paths: list[Path] = []
    for name, filename, canvas_size, pivot in obstacle_specs:
        source_path = GENERATED_OBSTACLE_ROOT / filename
        source = Image.open(source_path).convert("RGBA")
        source_rect = (0, 0, source.width, source.height)
        add_sprite(
            name,
            "obstacle_visuals",
            -1,
            fit_prop(source, canvas_size),
            source_path,
            source_rect,
            pivot,
        )
        obstacle_source_paths.append(source_path)

    if len(sprites) != 205:
        raise ValueError(f"Expected 205 sprites, got {len(sprites)}")

    atlas = Image.new("RGBA", (ATLAS_SIZE, ATLAS_SIZE), (0, 0, 0, 0))
    catalog_sprites: list[dict[str, object]] = []
    variable_families = {"detail", "props", "obstacle_visuals"}
    tile_sprites = [sprite for sprite in sprites if sprite["family"] not in variable_families]
    variable_sprites = [sprite for sprite in sprites if sprite["family"] in variable_families]

    for index, sprite in enumerate(tile_sprites):
        x = (index % 32) * TILE_SIZE
        top = (index // 32) * TILE_SIZE
        image = sprite["image"]
        atlas.alpha_composite(image, (x, top))
        source_rect = sprite["sourceRect"]
        pivot = sprite["pivot"]
        catalog_sprites.append(
            {
                "name": sprite["name"],
                "x": x,
                "y": ATLAS_SIZE - top - image.height,
                "width": image.width,
                "height": image.height,
                "pivotX": pivot[0],
                "pivotY": pivot[1],
                "family": sprite["family"],
                "mask": sprite["mask"],
                "sourcePath": sprite["sourcePath"],
                "sourceX": source_rect[0],
                "sourceY": source_rect[1],
                "sourceWidth": source_rect[2],
                "sourceHeight": source_rect[3],
            }
        )

    variable_top = ((len(tile_sprites) + 31) // 32) * TILE_SIZE + 32
    variable_x = 0
    variable_row_height = 0
    for sprite in variable_sprites:
        image = sprite["image"]
        if variable_x + image.width > ATLAS_SIZE:
            variable_x = 0
            variable_top += variable_row_height
            variable_row_height = 0
        if variable_top + image.height > ATLAS_SIZE:
            raise ValueError(f"Variable sprite does not fit atlas: {sprite['name']}")
        atlas.alpha_composite(image, (variable_x, variable_top))
        source_rect = sprite["sourceRect"]
        pivot = sprite["pivot"]
        catalog_sprites.append(
            {
                "name": sprite["name"],
                "x": variable_x,
                "y": ATLAS_SIZE - variable_top - image.height,
                "width": image.width,
                "height": image.height,
                "pivotX": pivot[0],
                "pivotY": pivot[1],
                "family": sprite["family"],
                "mask": sprite["mask"],
                "sourcePath": sprite["sourcePath"],
                "sourceX": source_rect[0],
                "sourceY": source_rect[1],
                "sourceWidth": source_rect[2],
                "sourceHeight": source_rect[3],
            }
        )
        variable_x += image.width
        variable_row_height = max(variable_row_height, image.height)

    alpha_histogram = atlas.getchannel("A").histogram()
    if any(alpha_histogram[value] for value in range(1, 255)):
        raise ValueError("Production Atlas must use binary alpha")
    if len({sprite["name"] for sprite in catalog_sprites}) != len(catalog_sprites):
        raise ValueError("Sprite names must be unique")

    for sprite in tile_sprites:
        if sprite["family"] not in ("grass", "water"):
            continue
        image = sprite["image"]
        if sprite["family"] == "water" and image.getchannel("A").getextrema() != (255, 255):
            raise ValueError(f"Water tile must be fully opaque: {sprite['name']}")
        edge_pixels = (
            [image.getpixel((x, 0)) for x in range(image.width)]
            + [image.getpixel((x, image.height - 1)) for x in range(image.width)]
            + [image.getpixel((0, y)) for y in range(image.height)]
            + [image.getpixel((image.width - 1, y)) for y in range(image.height)]
        )
        if any(pixel[3] == 0 for pixel in edge_pixels):
            raise ValueError(f"Full tile must cover every edge pixel: {sprite['name']}")

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    atlas.save(ATLAS_PATH, optimize=False)
    source_paths = (
        [MASTER_PATH]
        + [EXTENSION_ROOT / spec[1] for spec in family_specs]
        + [
            grass_surface_path,
            GENERATED_SURFACE_ROOT / "dirt-surface-variants-source.png",
            GENERATED_SURFACE_ROOT / "stone-surface-variants-source.png",
            GENERATED_SURFACE_ROOT / "water-surface-variants-source.png",
            GENERATED_EDGE_ROOT / "dirt-edge-profile-variants-source.png",
            GENERATED_EDGE_ROOT / "stone-edge-profile-variants-source.png",
        ]
        + decoration_source_paths
        + [tree_source_path]
        + obstacle_source_paths
    )
    catalog = {
        "schema": "fantasyroyale.map-authoring-atlas.v4.3",
        "atlasPath": ATLAS_PATH.as_posix(),
        "atlasWidth": ATLAS_SIZE,
        "atlasHeight": ATLAS_SIZE,
        "tileSize": TILE_SIZE,
        "spriteCount": len(catalog_sprites),
        "sources": [
            {"path": path.as_posix(), "sha256": source_hash(path)}
            for path in source_paths
        ],
        "sprites": catalog_sprites,
    }
    CATALOG_PATH.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    QA_PATH.parent.mkdir(parents=True, exist_ok=True)
    atlas.resize((ATLAS_SIZE * 2, ATLAS_SIZE * 2), Image.Resampling.NEAREST).save(QA_PATH, optimize=False)
    print(f"sprites={len(catalog_sprites)} atlas={ATLAS_PATH.as_posix()}")
    print(f"catalog={CATALOG_PATH.as_posix()}")
    print(f"qa={QA_PATH.as_posix()}")


if __name__ == "__main__":
    main()
