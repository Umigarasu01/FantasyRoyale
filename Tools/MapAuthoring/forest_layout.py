"""Fixed layout of the forest base map. World units, x to the east, y to the north.

The map is 144 x 108. A central village connects to an outer ring road by spokes, so the
main roads have no dead ends. A river crosses the map in a gentle S with three bridges.
Each region has its own landmark. Only short spurs to special sites end in a dead end.
"""
import math

MAP_WIDTH = 144.0
MAP_HEIGHT = 108.0
ROAD_WIDTH = 1.9

RIVER = [
    (101, 118), (103, 102), (99, 90), (99, 84), (99, 78), (95, 69), (97, 60), (93, 56), (93, 52),
    (93, 48), (97, 39), (93, 29), (89, 24), (89, 20), (89, 16), (86, 4), (87, -10),
]
BRIDGES = [(99, 84), (93, 52), (89, 20)]

ROADS = {
    "west-spine": [(18, 52), (30, 47.5), (44, 51), (50, 52.5), (60, 53)],
    "east-spine": [(60, 53), (72, 50), (82, 51.5), (87, 52), (93, 52), (99, 52), (106, 52), (118, 50.5), (130, 54)],
    "north-spoke": [(50, 52.5), (48, 62), (49, 72), (55, 83), (62, 92)],
    "south-spoke": [(66, 51), (66, 40), (58, 27), (58, 14)],
    "ring-northwest": [(18, 52), (20, 66), (27, 79), (42, 90), (62, 92)],
    "ring-northeast": [(62, 92), (78, 88.5), (90, 84.5), (93, 84), (99, 84), (105, 84), (108, 84), (120, 82), (129, 70), (130, 54)],
    "ring-southeast": [(130, 54), (132, 38), (125, 26), (110, 21), (95, 20), (89, 20), (83, 20), (70, 16), (58, 14)],
    "ring-southwest": [(58, 14), (44, 13), (28, 20), (17, 34), (18, 52)],
    "inn-walk": [(60, 53), (60, 59)],
    "cave-spur": [(48.6, 66), (41, 65), (33, 66.5)],
    "logging-spur": [(59, 29), (49, 30), (41, 34)],
    "mine-spur": [(34, 16.8), (36, 26)],
    "shrine-spur": [(118, 50.5), (115, 58), (113, 68.5)],
    "nest-spur": [(118, 50.5), (115, 45), (112, 42)],
    "farm-spur": [(114, 83.3), (116, 87), (118, 91)],
    "hut-west-walk": [(43, 57.4), (43.4, 54), (44, 51)],
    "hut-east-walk": [(74, 57.4), (74, 54), (73.5, 50.2)],
    "shed-walk": [(28, 27.6), (31, 25.6), (36, 24.2)],
}

# Open ground: centre x, centre y, radius x, radius y.
CLEARINGS = [
    (60, 54, 20, 15),      # village
    (30, 86, 15, 11),      # camp
    (72, 95, 12, 8),       # abandoned yard
    (90, 92, 9, 8),        # mill
    (118, 90, 14, 10),     # farm
    (112, 67, 12, 8),      # shrine
    (112, 38, 8, 6),       # nest
    (115, 24, 16, 10),     # fort
    (76, 19, 11, 8),       # south yard
    (30, 24, 15, 11),      # mine
    (19, 53, 10, 9),       # checkpoint
    (34, 64, 13, 8),       # cave
    (40, 34, 10, 7),       # logging
]

# Bare ground where people gather: centre x, centre y, radius x, radius y.
PLAZAS = [
    (60, 52.8, 4.6, 2.0), (54, 51.8, 2.6, 1.3), (66, 51.4, 2.6, 1.3), (30, 83.5, 2.6, 1.4), (90, 85.6, 2.6, 1.2),
    (113, 22.8, 3.4, 1.2), (36, 24, 2.2, 1.2), (33.5, 65.4, 2, 1), (113, 67.6, 1.8, 1), (41, 34, 1.8, 0.9), (118, 90, 1.5, 0.9),
]

# Facilities: prototype id, foot x, foot y.
FACILITIES = [
    # Village.
    ("inn", 60, 60), ("timber-hut", 74, 58), ("timber-hut", 43, 58),
    ("market-stall", 50, 47.5), ("market-stall", 55, 45.5), ("fountain", 61, 46), ("well", 72, 46.5),
    ("crate-stack", 46.8, 46.6), ("barrel", 64.6, 59.6), ("crate", 55.2, 59.8), ("notice-board", 56, 55.4),
    ("signpost", 62.4, 55), ("loaded-cart", 77.5, 48.4), ("tool-bench", 78.5, 56.5),
    # Checkpoint.
    ("watchtower", 14, 57), ("fence", 10.5, 53.5), ("crate", 16.8, 56),
    # Camp.
    ("tent", 22, 88), ("tent", 31, 93), ("tent", 40, 81), ("fire", 28, 87), ("log-stack", 24.5, 82), ("crate", 35, 92.2),
    # Abandoned yard.
    ("broken-house", 70, 96.5), ("well", 78, 94.5), ("fence", 66, 92.8), ("fence", 70.8, 92.8), ("rubble", 75, 92.8),
    ("stump", 64, 94),
    # Mill.
    ("watermill", 93.5, 91), ("crate", 87.4, 90), ("barrel", 86.6, 88.9),
    # Farm.
    ("timber-hut", 118, 92), ("crop-row", 110, 90), ("crop-row", 110, 94.5), ("well", 124, 90),
    ("fence", 105.6, 87.2), ("fence", 110.4, 87.2), ("fence", 124, 87),
    # Shrine.
    ("wayside-shrine", 113, 70), ("natural-spring", 120, 66), ("herb-bed", 105, 66),
    # Nest.
    ("monster-nest", 112, 38), ("rock-cluster", 117, 38.5), ("rock-cluster", 107, 37),
    # Fort.
    ("ruined-arch", 113, 27), ("ruined-column", 104, 26), ("ruined-column", 122, 31.5), ("rubble", 109, 22),
    ("watchtower", 120, 12), ("fence", 114.8, 13.4), ("fence", 125.2, 13.4), ("crate", 117.6, 13.6),
    # South yard.
    ("tool-bench", 76, 23.5), ("crate", 72, 24.5), ("loaded-cart", 76, 15.4),
    # Mine.
    ("mine-mouth", 36, 27), ("logging-shed", 28, 28.5), ("log-stack", 42, 19.5), ("loaded-cart", 40.2, 25),
    # Cave.
    ("cave-mouth", 33, 68), ("cliff-face-small", 27.5, 68.5), ("cliff-face-small", 41.5, 68.5), ("rock-cluster", 28, 64),
    # Logging.
    ("logging-shed", 40, 37), ("log-stack", 48, 35.5), ("stump", 35, 33), ("stump", 44, 28.5), ("tool-bench", 33.5, 36.5),
    ("fallen-log", 52, 33),
]

# Places to photograph during the Unity check: name, x, y.
VIEWS = [
    ("village", 60, 51), ("bridge-south", 83, 20), ("bridge-centre", 93, 52), ("bridge-north", 99, 84),
    ("river-bend", 91, 37), ("forest-edge", 104, 50), ("camp", 28, 84), ("mill", 92, 86.5),
    ("fort", 112, 23), ("mine", 33, 22), ("cave", 34, 63), ("shrine", 113, 65),
]


def catmull_rom(points, step=0.4):
    """Sample a smooth curve through points at roughly even spacing."""
    padded = [points[0]] + list(points) + [points[-1]]
    result = []
    for index in range(1, len(padded) - 2):
        p0, p1, p2, p3 = padded[index - 1], padded[index], padded[index + 1], padded[index + 2]
        steps = max(2, int(math.dist(p1, p2) / step))
        for k in range(steps):
            t = k / steps
            t2, t3 = t * t, t * t * t
            result.append(tuple(
                0.5 * (2 * p1[c] + (p2[c] - p0[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t2
                       + (3 * p1[c] - p0[c] - 3 * p2[c] + p3[c]) * t3)
                for c in (0, 1)
            ))
    result.append(tuple(points[-1]))
    return result
