"""Check walking routes against the serialized PC and Quest collision geometry."""

import argparse
from collections import deque
from dataclasses import dataclass
from functools import cache
from itertools import product
import json
import math
from pathlib import Path
import re

import numpy as np
from PIL import Image, ImageDraw, ImageFont


STEP = 0.5
RADIUS = 0.33
HEIGHT = 1.85
ROOMS = ("hub", "rail", "air", "grove", "motor", "gravity", "observatory", "gallery", "service")
EXPECTED_LANDINGS = {
    "Arrival": 1,
    "Unscheduled arrival": 8,
    "Gallery landing": 1,
    "Northern corridor landing": 1,
    "Arrival on rotated architecture": 2,
    "Weightless starting position": 1,
}
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?$", re.MULTILINE)
FIELD = re.compile(r"^  (m_\w+): (.*)$", re.MULTILINE)
FLOAT = re.compile(r"\b([xyzw]): ([+\-\d.eE]+)")
FILE_ID = re.compile(r"\bfileID: (-?\d+)")


def vector(field, dimensions="xyz"):
    parts = dict(FLOAT.findall(field))
    return np.array([float(parts[axis]) for axis in dimensions])


def file_id(field):
    return int(FILE_ID.search(field).group(1))


def matrix(fields):
    x, y, z, w = vector(fields["m_LocalRotation"], "xyzw")
    rot = np.array([
        [1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
        [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
        [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)],
    ])
    out = np.eye(4)
    out[:3, :3] = rot * vector(fields["m_LocalScale"])
    out[:3, 3] = vector(fields["m_LocalPosition"])
    return out


@dataclass
class Box:
    name: str
    center: np.ndarray
    inverse: np.ndarray
    half_size: np.ndarray
    bounds: np.ndarray

    def vertical(self, x, z, radius=0):
        """Vertical line interval inside the actual oriented box (vertical cylinder for clearance)."""
        shape = np.broadcast(x, z).shape
        low = np.full(shape, -np.inf)
        high = np.full(shape, np.inf)
        hits = np.ones(shape, dtype=bool)
        for axis, half in zip(self.inverse, self.half_size):
            # Expanding each local plane by a horizontal disk gives a conservative clearance
            # test for a rotated box. With zero radius this is an exact vertical ray cast.
            half += radius * math.hypot(axis[0], axis[2])
            offset = (x - self.center[0])*axis[0] + (z - self.center[2])*axis[2]
            if abs(axis[1]) < 1e-7:
                hits &= np.abs(offset) <= half + 1e-6
            else:
                end1 = (-half - offset) / axis[1] + self.center[1]
                end2 = (half - offset) / axis[1] + self.center[1]
                low = np.maximum(low, np.minimum(end1, end2))
                high = np.minimum(high, np.maximum(end1, end2))
        return hits & (low <= high + 1e-6), low, high


class Scene:
    def __init__(self, path):
        source = path.read_text(encoding="utf-8-sig")
        if not source.startswith("%YAML"):
            raise ValueError(f"{path} must use Unity text serialization")
        self.named = {}
        self.boxes = []
        objects, transforms, colliders, unsupported = {}, {}, [], []
        docs = list(HEADER.finditer(source))
        for i, match in enumerate(docs):
            kind, ident = map(int, match.groups())
            if kind not in (1, 4, 54, 64, 65, 135, 136, 143, 146, 154, 1001):
                continue
            end = docs[i+1].start() if i+1 < len(docs) else len(source)
            data = dict(FIELD.findall(source[match.end():end]))
            if kind == 1:
                objects[ident] = data
            elif kind == 4:
                transforms[file_id(data["m_GameObject"])] = (ident, data)
            elif kind == 65:
                colliders.append(data)
            else:
                unsupported.append(kind)
        if unsupported:
            raise ValueError(f"Scene contains unsupported prefab or physics class(es) {sorted(set(unsupported))}; update the collision model.")
        by_transform = {ident: (obj, data) for obj, (ident, data) in transforms.items()}

        @cache
        def world(ident):
            if ident == 0:
                return np.eye(4), True
            obj, fields = by_transform[ident]
            parent, enabled = world(file_id(fields["m_Father"]))
            game_object = objects[obj]
            live = enabled and game_object.get("m_IsActive", "1") == "1" and game_object.get("m_TagString") != "EditorOnly"
            return parent @ matrix(fields), live

        for obj, (ident, _) in transforms.items():
            pose, active = world(ident)
            if active:
                name = objects[obj]["m_Name"].strip("'\x22")
                self.named.setdefault(name, []).append(pose[:3, 3])
        corners = np.array(list(product((-1, 1), repeat=3)))
        for fields in colliders:
            if fields.get("m_Enabled", "1") != "1" or fields.get("m_IsTrigger", "0") == "1":
                continue
            obj = file_id(fields["m_GameObject"])
            pose, active = world(transforms[obj][0])
            if not active:
                continue
            half_size = vector(fields["m_Size"]) * 0.5
            m = pose[:3, :3]
            center = m @ vector(fields["m_Center"]) + pose[:3, 3]
            vertices = (corners*half_size) @ m.T + center
            bounds = np.array([vertices.min(axis=0), vertices.max(axis=0)])
            self.boxes.append(Box(objects[obj]["m_Name"], center, np.linalg.inv(m), half_size, bounds))

    def single(self, name):
        matches = self.named.get(name, [])
        if len(matches) != 1:
            raise ValueError(f"Expected exactly one {name!r}, found {len(matches)}")
        return matches[0]

    def arrival(self, point):
        x, y, z = point
        near = [b for b in self.boxes if np.all(b.bounds[0, [0, 2]] - RADIUS <= [x, z])
                and np.all(b.bounds[1, [0, 2]] + RADIUS >= [x, z])]
        tops = []
        for b in near:
            hit, _, top = b.vertical(x, z)
            if hit and y-1.55 <= top <= y+0.07:
                tops.append(float(top))
        if not tops:
            return False, "No floor within 1.55 m below arrival"
        floor = max(tops)
        for b in near:
            hit, low, high = b.vertical(x, z, RADIUS)
            if hit and high > floor+0.08 and low < y+HEIGHT-0.03:
                return False, f"Body clearance intersects {b.name}"
        return True, f"Floor drop {y-floor:.2f} m"


class GroundGrid:
    def __init__(self, scene, centers):
        low = np.floor(np.min(centers, axis=0)[[0, 2]]/STEP)*STEP - 36
        high = np.ceil(np.max(centers, axis=0)[[0, 2]]/STEP)*STEP + 36
        self.x, self.z = [np.arange(lo, hi+STEP*0.1, STEP) for lo, hi in zip(low, high)]
        self.support = np.zeros((len(self.z), len(self.x)), dtype=bool)
        self.blocked = np.zeros_like(self.support)
        ground = float(np.median(np.array(centers)[:, 1]))
        for box in scene.boxes:
            b = box.bounds
            if b[1, 1] < ground-0.30 or b[0, 1] > ground+HEIGHT:
                continue
            xs = slice(np.searchsorted(self.x, b[0, 0]-RADIUS), np.searchsorted(self.x, b[1, 0]+RADIUS, side="right"))
            zs = slice(np.searchsorted(self.z, b[0, 2]-RADIUS), np.searchsorted(self.z, b[1, 2]+RADIUS, side="right"))
            if xs.stop <= xs.start or zs.stop <= zs.start:
                continue
            x = self.x[xs][None, :]
            z = self.z[zs][:, None]
            hit, _, top = box.vertical(x, z)
            self.support[zs, xs] |= hit & (top >= ground-0.26) & (top <= ground+0.26)
            hit, bottom, top = box.vertical(x, z, RADIUS)
            self.blocked[zs, xs] |= hit & (top > ground+0.27) & (bottom < ground+HEIGHT)
        self.walk = self.support & ~self.blocked

    def index(self, point):
        return round((point[2]-self.z[0])/STEP), round((point[0]-self.x[0])/STEP)

    def path(self, a, b):
        # Restrict each search to one direct link. A trip around the next district must
        # not mask a collapsed passage. Only edge neighbours connect (no corner cuts).
        pad = round(16/STEP)
        z0, x0 = a
        z1, x1 = b
        lz, lx = max(min(z0,z1)-pad, 0), max(min(x0,x1)-pad, 0)
        sub = self.walk[lz:max(z0,z1)+pad+1, lx:max(x0,x1)+pad+1]
        start = z0-lz, x0-lx
        goal = z1-lz, x1-lx
        if not sub[start] or not sub[goal]:
            return []
        rows, cols = sub.shape
        seen = {start: None}
        pending = deque([start])
        while pending:
            z, x = pending.popleft()
            if (z, x) == goal:
                route = []
                pos = goal
                while pos is not None:
                    route.append((pos[0]+lz, pos[1]+lx))
                    pos = seen[pos]
                return route[::-1]
            for q, p in ((z+1,x), (z-1,x), (z,x+1), (z,x-1)):
                if 0 <= q < rows and 0 <= p < cols and sub[q,p] and (q,p) not in seen:
                    seen[q,p] = z,x
                    pending.append((q,p))
        return []


def font(size):
    for path in ("DejaVuSans.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            pass
    return ImageFont.load_default()


def draw_map(output, platform, grid, centers, routes):
    image = np.zeros((*grid.walk.shape, 3), dtype=np.uint8)
    image[:] = (15, 28, 35)
    image[grid.support] = (91, 77, 70)
    image[grid.walk] = (50, 95, 100)
    image[grid.blocked] = (137, 86, 67)
    art = Image.fromarray(image[::-1]).resize((image.shape[1]*2, image.shape[0]*2), Image.Resampling.NEAREST)
    overlay = ImageDraw.Draw(art)

    def px(cell):
        z, x = cell
        return x*2, (image.shape[0]-1-z)*2

    for route in routes:
        if route:
            overlay.line([px(p) for p in route], fill=(241, 183, 99), width=3)
    for name, point in centers.items():
        x, y = px(grid.index(point))
        overlay.ellipse((x-4, y-4, x+4, y+4), fill=(252, 218, 163))
        overlay.text((x, y+8), name.upper(), font=font(17), anchor="mt", fill=(242, 243, 231),
                     stroke_width=2, stroke_fill=(26, 47, 53))
    page = Image.new("RGB", (art.width, art.height+105), (15, 28, 35))
    page.paste(art, (0, 75))
    draw = ImageDraw.Draw(page)
    draw.text((23, 14), f"{platform}   /   Walking routes", font=font(28), fill=(239, 231, 202))
    draw.text((24, 48), f"{len(routes)-sum(not r for r in routes)} direct links   |   {STEP:.1f} m sample spacing   |   {HEIGHT:.2f} m headroom", font=font(15), fill=(168, 197, 197))
    draw.text((24, page.height-25), "Teal   floor with clearance        Rust   obstructions        Gold   sampled paths",
              font=font(14), fill=(191, 209, 199))
    page.save(output / f"{platform}-routes.png")


def review(scene_path, platform, output):
    scene = Scene(scene_path)
    centers = {key: scene.single(key) for key in ROOMS}
    grid = GroundGrid(scene, list(centers.values()))
    failures, routes, links = [], [], []
    connections = []
    for a, p in centers.items():
        for b, q in centers.items():
            dx, dz = q[0]-p[0], q[2]-p[2]
            if (abs(dx-80)<0.1 and abs(dz)<0.1) or (abs(dz-80)<0.1 and abs(dx)<0.1):
                connections.append((a, b))
    if len(connections) != 12:
        failures.append(f"Expected the twelve cardinal links, found {len(connections)}.")
    for a, b in connections:
        passage = f"Passage {a} to {b}"
        # Verify the physical link with independent floor-supported graph searches to the
        # serialized midpoint of the actual passage object, not just the two nearby halls.
        mid = scene.single(passage)
        leg1 = grid.path(grid.index(centers[a]), grid.index(mid))
        leg2 = grid.path(grid.index(mid), grid.index(centers[b]))
        points = leg1 + leg2[1:] if leg1 and leg2 else []
        routes.append(points)
        links.append({"from": a, "to": b, "pass": bool(points), "sampled_path_m": max(0, len(points)-1)*STEP})
        if not points:
            failures.append(f"{passage} has no clear path through its midpoint.")
    trips = []
    for name, expected in EXPECTED_LANDINGS.items():
        anchors = scene.named.get(name, [])
        if len(anchors) != expected:
            failures.append(f"Expected {expected} {name!r} targets, found {len(anchors)}.")
        for point in anchors:
            valid, detail = scene.arrival(point)
            trips.append({"target": name, "world_xyz": np.round(point, 3).tolist(), "pass": valid, "detail": detail})
            if not valid:
                failures.append(f"{name} at {np.round(point, 2).tolist()}. {detail}")
    spawn = scene.single("Arrival")
    if not grid.path(grid.index(spawn), grid.index(centers["hub"])):
        failures.append("Spawn has no clear ground route to the central interchange.")
    draw_map(output, platform, grid, centers, routes)
    return {
        "platform": platform, "scene": scene_path.name, "active_solid_boxes": len(scene.boxes),
        "sampled_walkable_area_m2": float(grid.walk.sum())*STEP**2, "links": links,
        "landings": trips, "failures": failures,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scenes", type=Path, default=Path("Assets/Stillwater/Scenes"))
    parser.add_argument("--output", type=Path, default=Path("route-review"))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    reports = []
    for platform in ("PC", "Quest"):
        scene = args.scenes / f"liminal uwu - {platform}.unity"
        report = review(scene, platform, args.output)
        reports.append(report)
        print(f"{platform}: {sum(i['pass'] for i in report['links'])}/12 links, "
              f"{sum(i['pass'] for i in report['landings'])}/{len(report['landings'])} clear arrivals, "
              f"{report['active_solid_boxes']} collision boxes.")
        for failure in report["failures"]:
            print(f"  {failure}")
    (args.output / "routes.json").write_text(json.dumps(reports, indent=2)+"\n", encoding="utf-8")
    return int(any(report["failures"] for report in reports))


if __name__ == "__main__":
    raise SystemExit(main())
