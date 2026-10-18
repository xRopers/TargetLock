"""Renders images/icon.png (512x512) for the plugin installer, with no third-party dependencies.

An original drawing of the game's lock-on reticle shape: a diamond outline around a small filled diamond, with a
filled triangle and a chevron on each side pointing in at the target. It does not use any game assets.

Usage: python scripts/make_icon.py
"""
import math
import struct
import zlib
from pathlib import Path

SIZE = 512
OUT = Path(__file__).resolve().parent.parent / "images" / "icon.png"

WHITE = (1.0, 1.0, 1.0)
GLOW = (0.62, 0.82, 1.0)
BG_TOP = (0.09, 0.11, 0.20)
BG_BOTTOM = (0.04, 0.05, 0.10)

CENTER = 256
OUTLINE_RADIUS, OUTLINE_WIDTH = 106, 8   # large diamond outline (centre to corner)
CORE_RADIUS = 28                         # small filled diamond
SIDE_HALF_HEIGHT = 84                    # height of the side markers, from the middle
TRIANGLE_BASE_X, TRIANGLE_TIP_X = 56, 94 # filled triangle pointing in, on the left side
CHEVRON_TOP_X, CHEVRON_TIP_X, CHEVRON_WIDTH = 82, 122, 18


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def coverage(distance):
    return clamp(0.5 - distance)


def rounded_rect(px, py, half, radius):
    qx, qy = abs(px) - (half - radius), abs(py) - (half - radius)
    return math.hypot(max(qx, 0.0), max(qy, 0.0)) + min(max(qx, qy), 0.0) - radius


def segment_distance(px, py, ax, ay, bx, by):
    abx, aby = bx - ax, by - ay
    t = clamp(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby))
    return math.hypot(px - ax - abx * t, py - ay - aby * t)


def polygon_distance(px, py, points):
    """Signed distance to a polygon (negative inside)."""
    d = min(segment_distance(px, py, *points[i], *points[(i + 1) % len(points)]) for i in range(len(points)))
    inside = False
    for i in range(len(points)):
        (ax, ay), (bx, by) = points[i], points[(i + 1) % len(points)]
        if (ay > py) != (by > py) and px < (bx - ax) * (py - ay) / (by - ay) + ax:
            inside = not inside
    return -d if inside else d


def diamond_distance(px, py, radius):
    """Signed distance to a diamond (square rotated 45°) with the given centre-to-corner radius."""
    return (abs(px - CENTER) + abs(py - CENTER) - radius) / math.sqrt(2)


def side_markers_distance(px, py):
    """Triangle + chevron on the left, mirrored on the right, both pointing in at the centre."""
    x = px if px < CENTER else 2 * CENTER - px  # mirror the right half onto the left
    top, bottom = CENTER - SIDE_HALF_HEIGHT, CENTER + SIDE_HALF_HEIGHT
    inset = 12
    triangle = polygon_distance(x, py, [(TRIANGLE_BASE_X, top + inset), (TRIANGLE_TIP_X, CENTER), (TRIANGLE_BASE_X, bottom - inset)])
    chevron = min(segment_distance(x, py, CHEVRON_TOP_X, top, CHEVRON_TIP_X, CENTER),
                  segment_distance(x, py, CHEVRON_TIP_X, CENTER, CHEVRON_TOP_X, bottom)) - CHEVRON_WIDTH / 2
    return min(triangle, chevron)


def shade(x, y):
    px, py = x + 0.5, y + 0.5
    tile = coverage(rounded_rect(px - SIZE / 2, py - SIZE / 2, 240, 104))
    if tile <= 0.0:
        return (0, 0, 0, 0)

    color = mix(BG_TOP, BG_BOTTOM, clamp(y / SIZE))
    ambient = math.exp(-((px - CENTER) ** 2 + (py - CENTER) ** 2) / (2 * 140 * 140)) * 0.25
    color = tuple(c + g * ambient for c, g in zip(color, GLOW))

    outline = abs(diamond_distance(px, py, OUTLINE_RADIUS)) - OUTLINE_WIDTH / 2
    core = diamond_distance(px, py, CORE_RADIUS)
    sides = side_markers_distance(px, py)
    shape = min(outline, core, sides)

    color = tuple(c + g * math.exp(-max(shape, 0) ** 2 / (2 * 12 * 12)) * 0.7 for c, g in zip(color, GLOW))
    color = mix(color, WHITE, coverage(shape))

    return tuple(round(clamp(c) * 255) for c in color) + (round(tile * 255),)


def write_png(path, pixels):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in pixels)
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def main():
    write_png(OUT, [[shade(x, y) for x in range(SIZE)] for y in range(SIZE)])
    print(f"Wrote {OUT}")


if __name__ == "__main__":
    main()
