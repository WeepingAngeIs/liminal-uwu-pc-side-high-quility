"""Weave a repeatable four-colour moquette for the Aperture Line stock."""
from pathlib import Path
import numpy as np
from PIL import Image

size = 512
y, x = np.mgrid[:size, :size]
rng = np.random.default_rng(310)
# Broken arcs, route rails and staggered punched-ticket marks. All periods tile.
row = y // 128
u, v = (x + row * 64) % 128, y % 128
radius = np.hypot(u - 48., (v - 67.) * 1.18)
arc = (abs(radius - 32.) < 4.7) & ((v > 66) | (u > 53))
inner = (abs(radius - 21.) < 3.6) & (v < 75) & (u < 55)
rails = (abs(((u - v * .5 + 64) % 128) - 64) < 3.2) & (v > 10) & (v < 105)
dashes = (u > 85) & (u < 109) & ((v % 32) > 5) & ((v % 32) < 10)
picks = (x // 3 + y // 4) % 5 == 0
fabric = np.zeros((size, size, 3)) + [31., 49., 58.]
fabric += np.where(((x // 16 - y // 16) % 4 == 0)[..., None], [4, 6, 8], 0)
fabric[arc] = [59, 120, 119]
fabric[inner] = [163, 94, 77]
fabric[rails & ~arc] = [84, 102, 135]
fabric[dashes] = [174, 145, 99]
fabric[picks] = fabric[picks] * .81 + [4, 3, 3]
weft = np.cos(2 * np.pi * (x / 4 + .5 * (y // 4 % 2)))
warp = np.cos(2 * np.pi * y / 4)
nap = 1 + .094 * weft + .067 * warp + rng.normal(0, .05, (size, size))
out = Path(__file__).resolve().parents[1] / "Assets/Stillwater/Media/Surfaces/Platform moquette.jpg"
Image.fromarray(np.uint8(np.clip(fabric * nap[..., None], 0, 255))).save(out, quality=93, subsampling=0)
