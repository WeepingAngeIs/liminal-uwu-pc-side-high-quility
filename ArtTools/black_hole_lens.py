"""Bake a Schwarzschild sky transfer into a linear Unity RGBAFloat Texture2D.

R,G are sin(source angle), cos(source angle). In Eclipse's angular basis use
normalize(tangent * R + pole * G) to sample the distant environment.
B,A are the radii at the first two face-on disk-plane crossings, divided by 16.
Missing crossings and radii >= 16 both encode 1, outside the supported disk.

The runtime texture coordinate is
    log(1 + ((angle - .183) / .183) * 64) / log(1 + 2.8 * 64).
Its 2048 samples are at texel centres. Clamp wrapping extends the innermost
finite sample over the singular critical ray. The sky shader must reject
angle < .183 before sampling. Normalize RG after filtering.

The null-geodesic model follows tantaneity/urp-black-hole (MIT), reformulated
as u'' = 1.5 u^2 - u in the ray's orbital plane and integrated offline with RK4.
Reference https://github.com/tantaneity/urp-black-hole
MIT License
Copyright (c) 2026 tantaneity

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"""
from pathlib import Path
from time import perf_counter
import argparse
import math
import re
import numpy as np


EDGE = .183
GAP_SCALE = 64.
MAX_GAP = 2.8
DISK_LIMIT = 16.
WIDTH = 2048
CRITICAL_IMPACT = 1.5 * math.sqrt(3.)
OUTPUT = (Path(__file__).resolve().parents[1]
          / "Assets/Stillwater/Media/Optics/Geodesic transfer.asset")
GUID = "b34a9de7d3914fa5a52a77c5946ceb56"


def camera_radius():
    """Choose the outer solution of b = R sin(edge) / sqrt(1 - 1/R)."""
    low, high = 1.500001, 100.
    for _ in range(70):
        mid = (low + high) / 2.
        impact = mid * math.sin(EDGE) / math.sqrt(1. - 1. / mid)
        if impact < CRITICAL_IMPACT:
            low = mid
        else:
            high = mid
    return (low + high) / 2.


def integrate(angles, subdivisions=1024):
    """Integrate rays from the observer until escape, capture, or 8 pi.

    A radial angle phi starts at the observer and increases in the ray plane.
    Disk intersections occur at pi/2 and 3pi/2. The receiver is at -pole*R,
    so the asymptotic sky direction has angle pi - phi_escape about +pole.
    """
    observer = camera_radius()
    impact = observer * np.sin(angles) / math.sqrt(1. - 1. / observer)
    reciprocal = np.full_like(angles, 1. / observer, dtype=np.float64)
    velocity = np.sqrt(1. / impact**2 - reciprocal**2 + reciprocal**3)
    escape = np.full_like(angles, np.nan, dtype=np.float64)
    crossings = np.full((len(angles), 2), DISK_LIMIT, dtype=np.float64)
    active = np.ones(len(angles), dtype=bool)
    step = math.pi / subdivisions
    crossing_steps = {subdivisions // 2: 0, subdivisions * 3 // 2: 1}

    for index in range(subdivisions * 8):
        lanes = np.flatnonzero(active)
        if not len(lanes):
            break
        u, v = reciprocal[lanes], velocity[lanes]
        acceleration = 1.5 * u * u - u
        u2 = u + .5 * step * v
        v2 = v + .5 * step * acceleration
        a2 = 1.5 * u2 * u2 - u2
        u3 = u + .5 * step * v2
        v3 = v + .5 * step * a2
        a3 = 1.5 * u3 * u3 - u3
        u4 = u + step * v3
        v4 = v + step * a3
        a4 = 1.5 * u4 * u4 - u4
        next_u = u + step / 6. * (v + 2. * v2 + 2. * v3 + v4)
        next_v = v + step / 6. * (acceleration + 2. * a2 + 2. * a3 + a4)
        reciprocal[lanes], velocity[lanes] = next_u, next_v

        escaped = next_u <= 0.
        # A linear zero crossing here has cubic absolute error because u''=0
        # at escape. The integration steps land exactly on both disk planes.
        leaving = lanes[escaped]
        escape[leaving] = step * (index + u[escaped] / (u[escaped] - next_u[escaped]))
        active[leaving] = False
        captured = lanes[next_u >= 1.]
        active[captured] = False

        if index + 1 in crossing_steps:
            slot = crossing_steps[index + 1]
            crossing = lanes[(next_u > 0.) & (next_u < 1.)]
            crossings[crossing, slot] = np.minimum(DISK_LIMIT, 1. / reciprocal[crossing])

    if not np.all(np.isfinite(escape)):
        raise ValueError("An exterior ray failed to escape. Check the shadow calibration or integration budget.")
    source_angle = math.pi - escape
    return source_angle, crossings


def serialize(data):
    """Unity 2022 native Texture2D format 20 is little-endian RGBAFloat."""
    blob = np.asarray(data, dtype="<f4").tobytes().hex()
    byte_count = data.size * 4
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!28 &2800000
Texture2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Geodesic transfer
  m_ImageContentsHash:
    serializedVersion: 2
    Hash: 00000000000000000000000000000000
  m_ForcedFallbackFormat: 4
  m_DownscaleFallback: 0
  m_IsAlphaChannelOptional: 0
  serializedVersion: 2
  m_Width: {len(data)}
  m_Height: 1
  m_CompleteImageSize: {byte_count}
  m_MipsStripped: 0
  m_TextureFormat: 20
  m_MipCount: 1
  m_IsReadable: 1
  m_IsPreProcessed: 0
  m_IgnoreMipmapLimit: 0
  m_MipmapLimitGroupName:
  m_StreamingMipmaps: 0
  m_StreamingMipmapsPriority: 0
  m_VTOnly: 0
  m_AlphaIsTransparency: 0
  m_ImageCount: 1
  m_TextureDimension: 2
  m_TextureSettings:
    serializedVersion: 2
    m_FilterMode: 1
    m_Aniso: 1
    m_MipBias: 0
    m_WrapU: 1
    m_WrapV: 1
    m_WrapW: 1
  m_LightmapFormat: 0
  m_ColorSpace: 0
  m_PlatformBlob:
  image data: {byte_count}
  _typelessdata: {blob}
  m_StreamData:
    serializedVersion: 2
    offset: 0
    size: 0
    path:
"""


def validate_saved_transfer(expected):
    """Validate the actual serialized texture, including its sampling contract."""
    source = OUTPUT.read_text(encoding="utf-8")
    meta = OUTPUT.with_suffix(OUTPUT.suffix + ".meta").read_text(encoding="utf-8")
    if not re.search(r"^--- !u!28 &2800000\s*\nTexture2D:", source, re.M):
        raise ValueError("The saved asset must expose Texture2D object 2800000.")

    def field(text, key):
        values = re.findall(r"^[ \t]*" + re.escape(key) + r":[ \t]*([^\r\n]*)$", text, re.M)
        if len(values) != 1:
            raise ValueError(f"Expected exactly one serialized {key} field.")
        return values[0].strip()

    payload_bytes = WIDTH * 4 * 4
    required = {
        "m_Width": WIDTH, "m_Height": 1, "m_TextureFormat": 20,
        "m_TextureDimension": 2, "m_ImageCount": 1, "m_MipCount": 1,
        "m_MipsStripped": 0, "m_ColorSpace": 0, "m_FilterMode": 1,
        "m_WrapU": 1, "m_WrapV": 1, "m_AlphaIsTransparency": 0,
        "m_CompleteImageSize": payload_bytes, "image data": payload_bytes,
    }
    for key, value in required.items():
        if int(field(source, key)) != value:
            raise ValueError(f"Geodesic texture {key} must be {value}.")
    if int(field(source, "size")) != 0 or field(source, "path"):
        raise ValueError("Geodesic texture must contain its complete pixel data inline.")
    if field(meta, "guid") != GUID or int(field(meta, "mainObjectFileID")) != 2800000:
        raise ValueError("Geodesic texture identity no longer matches its material binding.")

    payload = bytes.fromhex(field(source, "_typelessdata"))
    if len(payload) != payload_bytes:
        raise ValueError(f"Geodesic texture payload has {len(payload)} bytes, expected {payload_bytes}.")
    saved = np.frombuffer(payload, dtype="<f4").reshape(WIDTH, 4)
    if not np.isfinite(saved).all():
        raise ValueError("Geodesic texture contains nonfinite channels.")
    norm_error = np.max(np.abs(np.linalg.norm(saved[:, :2], axis=1) - 1.))
    if norm_error > 2.e-6:
        raise ValueError(f"Geodesic texture source directions are not normalized, error {norm_error}.")
    if np.any(saved[:, 2:] <= 0.) or np.any(saved[:, 2:] > 1.):
        raise ValueError("Geodesic texture crossing channels must be in (0, 1].")
    transfer_error = np.max(np.abs(saved.astype(np.float64) - expected))
    if transfer_error > 2.e-6:
        raise ValueError(f"Saved transfer differs from regenerated geodesics by {transfer_error}.")
    print(f"Saved texture verified, maximum channel error {transfer_error:.3g}.")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n", 1)[0])
    parser.add_argument("--check", action="store_true",
                        help="Validate the saved texture against regenerated rays without writing files.")
    args = parser.parse_args()
    started = perf_counter()
    coordinate = (np.arange(WIDTH) + .5) / WIDTH
    gaps = np.expm1(coordinate * math.log1p(MAX_GAP * GAP_SCALE)) / GAP_SCALE
    angles = EDGE * (1. + gaps)
    source, crossings = integrate(angles)

    # Check numerical convergence at the most curved rays and across the table.
    probes = np.unique(np.r_[np.arange(16), np.linspace(16, WIDTH - 1, 48).astype(int)])
    refined, refined_crossings = integrate(angles[probes], subdivisions=2048)
    error = np.max(np.abs(source[probes] - refined))
    crossing_error = np.max(np.abs(crossings[probes] - refined_crossings))
    if error > 2.e-5 or crossing_error > 2.e-4:
        raise ValueError(f"Geodesic convergence failed, angular {error}, radius {crossing_error}")

    data = np.column_stack((np.sin(source), np.cos(source), crossings / DISK_LIMIT))
    if not np.isfinite(data).all() or np.any(data[:, 2:] <= 0.):
        raise ValueError("Nonfinite ray transfer or invalid crossing radius.")
    if args.check:
        validate_saved_transfer(data)
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_text(serialize(data), encoding="utf-8", newline="\n")
        meta = OUTPUT.with_suffix(OUTPUT.suffix + ".meta")
        if not meta.exists():
            meta.write_text(
                f"fileFormatVersion: 2\nguid: {GUID}\nNativeFormatImporter:\n"
                "  externalObjects: {}\n  mainObjectFileID: 2800000\n"
                "  userData: schwarzschild-transfer-1\n  assetBundleName: \n"
                "  assetBundleVariant: \n", encoding="utf-8", newline="\n")
        print(f"Saved {WIDTH} x 1 RGBAFloat, {data.size * 4} payload bytes.")
    print(f"Observer R={camera_radius():.12f}, critical edge={EDGE:.6f} radians.")
    print(f"RK4 convergence angular={error:.3g} rad, crossing radius={crossing_error:.3g}.")
    for index in (0, 16, 128, 512, 1024, 1536, WIDTH - 1):
        print(f"texel {index:4d}: angle={angles[index]:.9f}, source={source[index]:+.6f}, "
              f"crossings={crossings[index, 0]:.6f}, {crossings[index, 1]:.6f}")
    print(f"{'Validated' if args.check else 'Generated and checked'} in {perf_counter() - started:.2f} seconds.")


if __name__ == "__main__":
    main()
