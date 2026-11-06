"""Check rendered doorway and floor joins in the meshes referenced by both scenes."""

from collections import defaultdict
from dataclasses import dataclass
from functools import cache, cached_property
import re
import struct

from validate_world import ROOT, ROOMS, asset_guid, documents, file_id, guid, read, require, value


EPSILON = 0.0001  # Ignore sub-millimetre roundoff from Unity's cardinal rotations.


def vector(text, axes="xyz"):
    return tuple(float(re.search(r"\b" + axis + r": ([-+.\deE]+)", text)[1]) for axis in axes)


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def rotate(point, quaternion):
    xyz, w = quaternion[:3], quaternion[3]
    first = cross(xyz, point)
    second = cross(xyz, first)
    return tuple(point[i] + 2*(w*first[i]+second[i]) for i in range(3))


@dataclass
class Face:
    points: tuple

    @cached_property
    def low(self):
        return tuple(min(p[i] for p in self.points) for i in range(3))

    @cached_property
    def high(self):
        return tuple(max(p[i] for p in self.points) for i in range(3))

    @cached_property
    def normal(self):
        a, b, c = self.points[:3]
        return cross(tuple(b[i]-a[i] for i in range(3)), tuple(c[i]-a[i] for i in range(3)))

    @property
    def up(self):
        return self.normal[1] > 0 and self.high[1]-self.low[1] < EPSILON


@cache
def faces(path):
    """Read each indexed quad, using the serialized channel offsets and stride."""
    source = read(path)
    vertex_count = int(re.search(r"m_VertexCount: (\d+)", source)[1])
    data = bytes.fromhex(re.search(r"^    _typelessdata: ([a-f0-9]+)$", source, re.M)[1])
    channels = re.findall(r"- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)", source)
    require(channels and channels[0][0] == "0" and channels[0][2:] == ("0", "3"),
            f"{path.name} needs Float32 positions")
    require(all(stream == "0" for stream, _, _, dim in channels if dim != "0"),
            f"{path.name} uses an unsupported vertex stream")
    require(len(data) % vertex_count == 0, f"{path.name} has a malformed vertex buffer")
    stride = len(data)//vertex_count
    offset = int(channels[0][1])
    positions = [struct.unpack_from("<3f", data, i*stride+offset) for i in range(vertex_count)]
    index_data = bytes.fromhex(value(source, "m_IndexBuffer"))
    index_type = "I" if value(source, "m_IndexFormat") == "1" else "H"
    indices = [item[0] for item in struct.iter_unpack("<"+index_type, index_data)]
    require(len(indices) % 6 == 0, f"{path.name} is not made of paired triangles")
    result = []
    for i in range(0, len(indices), 6):
        a, b, c, again_a, again_c, d = indices[i:i+6]
        require(a == again_a and c == again_c, f"{path.name} has an unsupported triangle pairing")
        result.append(Face(tuple(positions[k] for k in (a, b, c, d))))
    return result


class Scene:
    def __init__(self, edition):
        self.directory = ROOT / "Generated" / edition / "Architecture"
        self.components = defaultdict(list)
        docs = {oid: (kind, body) for kind, oid, body in documents(read(ROOT / "Scenes" / f"liminal uwu - {edition}.unity"))}
        self.transforms = {}
        self.named = {}
        names = {oid: value(body, "m_Name") for oid, (kind, body) in docs.items() if kind == 1}
        for oid, (kind, body) in docs.items():
            self.components[(file_id(value(body, "m_GameObject")), kind)].append(body)
            if kind == 4:
                self.transforms[oid] = body
        for tid, body in self.transforms.items():
            parent = tid
            parts = []
            while parent != "0":
                node = self.transforms[parent]
                parts.append(names[file_id(value(node, "m_GameObject"))])
                parent = file_id(value(node, "m_Father"))
            path = tuple(reversed(parts))
            self.named[path] = tid

    def transform(self, path):
        full = ("liminal uwu", *path)
        require(full in self.named, f"Missing scene object {path}")
        return self.named[full]

    @cache
    def frame(self, tid):
        if tid == "0":
            return ((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0))
        body = self.transforms[tid]
        scale = vector(value(body, "m_LocalScale"))
        rotation = vector(value(body, "m_LocalRotation"), "xyzw")
        origin = vector(value(body, "m_LocalPosition"))
        columns = [rotate(tuple(scale[j] if i == j else 0 for i in range(3)), rotation) for j in range(3)]
        local = [tuple(columns[j][i] for j in range(3))+(origin[i],) for i in range(3)]
        parent = self.frame(file_id(value(body, "m_Father")))
        return tuple(tuple(sum(parent[i][k]*local[k][j] for k in range(3))
                           + (parent[i][3] if j == 3 else 0) for j in range(4)) for i in range(3))

    def world(self, point, tid):
        return tuple(sum(row[j]*point[j] for j in range(3))+row[3] for row in self.frame(tid))

    def mesh(self, path, filename, world=False):
        tid = self.transform(path)
        owner = file_id(value(self.transforms[tid], "m_GameObject"))
        meshes = self.components[(owner, 33)]
        renderers = self.components[(owner, 23)]
        file = self.directory / (filename+".asset")
        require(len(meshes) == 1 and guid(value(meshes[0], "m_Mesh")) == asset_guid(file),
                f"Unexpected mesh on {path}")
        require(len(renderers) == 1 and value(renderers[0], "m_Enabled") == "1", f"Hidden geometry on {path}")
        result = faces(file)
        if world:
            result = [Face(tuple(self.world(p, tid) for p in face.points)) for face in result]
        return result


def floor(mesh):
    candidates = [f for f in mesh if f.up and abs(f.low[1]) < EPSILON]
    require(candidates, "Missing rendered ground floor")
    return max(candidates, key=lambda f: (f.high[0]-f.low[0])*(f.high[2]-f.low[2]))


def intersection(a, b):
    return tuple(min(a.high[i], b.high[i])-max(a.low[i], b.low[i]) for i in (0, 2))


def joined(a, b, label, failures):
    overlap = intersection(a, b)
    if min(overlap) > EPSILON:
        failures.append(f"{label} has coplanar floor overlap {overlap[0]:.5f} x {overlap[1]:.5f} m")
    elif min(overlap) < -EPSILON:
        failures.append(f"{label} leaves a rendered floor gap of {-min(overlap):.5f} m")


def openings(mesh, room, doorways, failures):
    base = floor(mesh)
    # These local bases follow the four actual room facades, in clockwise order.
    bases = (((1, 0, 0), (0, 0, 1)), ((0, 0, -1), (1, 0, 0)),
             ((-1, 0, 0), (0, 0, -1)), ((0, 0, 1), (-1, 0, 0)))
    height = 3.05 if room == "service" else 5.6
    count = 0
    for direction in doorways:
        right, outward = bases[direction]
        depth = max(sum(p[i]*outward[i] for i in range(3)) for p in base.points)-0.16
        projected = [Face(tuple((sum(p[i]*right[i] for i in range(3)), p[1],
                               sum(p[i]*outward[i] for i in range(3))-depth)
                              for p in face.points)) for face in mesh]
        for side in (-1, 1):
            jambs, walls = [], []
            for f in projected:
                lo, hi = f.low, f.high
                radial = side*(lo[0]+hi[0])/2
                if (hi[0]-lo[0] > EPSILON or not 4.2 < radial < 4.8
                        or abs(lo[1]) > EPSILON or abs(lo[2]+hi[2]) > 1
                        or side*f.normal[0] >= 0):
                    continue
                if abs(hi[1]-height) < EPSILON and abs((hi[2]-lo[2])-.78) < EPSILON:
                    jambs.append(radial)
                if hi[1] >= height and abs((hi[2]-lo[2])-.46) < EPSILON and abs(radial-4.5) < EPSILON:
                    walls.append(radial)
            label = f"{room} doorway {direction}, side {side}"
            require(len(jambs) == len(walls) == 1, f"Could not identify the rendered jamb and wall at {label}")
            clearance = walls[0]-jambs[0]
            if clearance < .04-EPSILON:
                failures.append(f"{label} has only {clearance:.6f} m jamb/end-cap clearance")
            count += 1
    return count


def check(edition):
    scene = Scene(edition)
    failures = []
    local = {room: scene.mesh((room, "Architecture"), room) for room in ROOMS}
    floors = {room: floor(scene.mesh((room, "Architecture"), room, world=True)) for room in ROOMS}
    positions = {room: scene.world((0, 0, 0), scene.transform((room,))) for room in ROOMS}
    doors = defaultdict(set)
    passages = [path[-1] for path in scene.named if len(path) == 2 and path[-1].startswith("Passage ")]
    require(len(passages) == 12, "Expected twelve rendered main passages")
    for passage in passages:
        a, b = re.fullmatch(r"Passage (\w+) to (\w+)", passage).groups()
        sheet = floor(scene.mesh((passage, "Architecture"), passage, world=True))
        for room, other in ((a, b), (b, a)):
            joined(floors[room], sheet, f"{passage} / {room}", failures)
            dx = positions[other][0]-positions[room][0]
            dz = positions[other][2]-positions[room][2]
            direction = (1 if dx > 0 else 3) if abs(dx) > abs(dz) else (0 if dz > 0 else 2)
            doors[room].add(direction)
    doors["gallery"].add(2)
    doors["service"].add(3)
    jamb_count = sum(openings(local[room], room, doors[room], failures) for room in sorted(ROOMS))
    require(jamb_count == 52, "Expected two jambs on each of twenty-six shell doorways")

    bay_count = 0
    for wing, prefix, parent in (("Diminishing Baths", "Bath bay ", "gallery"),
                                 ("Night Reception", "Reception bay ", "service")):
        previous = floors[parent]
        for number in range(1, 11):
            bay = prefix + f"{number:02}"
            current = floor(scene.mesh((wing, bay, "Architecture"), bay, world=True))
            joined(previous, current, f"{wing} / {bay}", failures)
            previous = current
            bay_count += 1

    balcony = [f for f in local["gallery"] if f.up and abs(f.low[1]-4.4) < EPSILON
               and (f.high[0]-f.low[0])*(f.high[2]-f.low[2]) > 4]
    require(len(balcony) == 3, "Expected the gallery's three horizontal balcony sections")
    cross_deck = max(balcony, key=lambda f: f.high[0]-f.low[0])
    for deck in balcony:
        if deck is not cross_deck:
            joined(deck, cross_deck, "Gallery balcony junction", failures)
    print(f"{edition}: checked 24 passage endpoints, {jamb_count} jambs, {bay_count} shrinking bays and 2 balcony joins")
    for failure in failures:
        print("  "+failure)
    return failures


if __name__ == "__main__":
    failures = [item for edition in ("PC", "Quest") for item in check(edition)]
    require(not failures, f"{len(failures)} rendered surface joins need correction")
