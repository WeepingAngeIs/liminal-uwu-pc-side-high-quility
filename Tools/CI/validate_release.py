"""Check release-only features against the two serialized Unity scenes."""
from collections import defaultdict
import json
import math
import re

from PIL import Image, ImageChops, ImageStat

from validate_world import ROOT, asset_guid, documents, file_id, guid, read, require, value


def vector(text):
    return tuple(float(re.search(r"\b" + axis + r": ([-+.\deE]+)", text)[1]) for axis in "xyz")


class Scene:
    def __init__(self, edition):
        self.assets = ROOT / "Generated" / edition
        self.yaml = read(ROOT / "Scenes" / f"liminal uwu - {edition}.unity")
        self.by_id = defaultdict(dict)
        self.components = defaultdict(list)
        for kind, oid, body in documents(self.yaml):
            self.by_id[kind][oid] = body
            owner = file_id(value(body, "m_GameObject"))
            self.components[(owner, kind)].append(body)
        self.names = {oid: value(body, "m_Name") for oid, body in self.by_id[1].items()}
        self.paths = {}
        self.by_path = defaultdict(list)
        for tid, body in self.by_id[4].items():
            owner = file_id(value(body, "m_GameObject"))
            segments = []
            while tid != "0":
                transform = self.by_id[4][tid]
                segments.append(self.names[file_id(value(transform, "m_GameObject"))])
                tid = file_id(value(transform, "m_Father"))
            path = tuple(reversed(segments))
            self.paths[owner] = path
            self.by_path[path].append(owner)

    def at(self, path):
        matches = self.by_path[("liminal uwu", *path)]
        require(len(matches) == 1, f"Expected one object at {path}, found {len(matches)}")
        return matches[0]

    def component(self, path, kind):
        items = self.components[(self.at(path), kind)]
        require(len(items) == 1, f"Expected one component of class {kind} on {path}")
        return items[0]

    def position(self, path):
        return vector(value(self.component(path, 4), "m_LocalPosition"))

    def mesh(self, path, filename):
        mesh_path = self.assets / filename
        require(guid(value(self.component(path, 33), "m_Mesh")) == asset_guid(mesh_path),
                f"Incorrect mesh on {path}")
        renderer = self.component(path, 23)
        require(value(renderer, "m_Enabled") == "1" and "  m_Materials:\n  - " in renderer,
                f"Missing visible geometry on {path}")
        return mesh_path

    def box(self, path):
        body = self.component(path, 65)
        require(value(body, "m_Enabled") == "1" and value(body, "m_IsTrigger") == "0",
                f"Nonphysical boundary on {path}")
        center = tuple(a + b for a, b in zip(self.position(path), vector(value(body, "m_Center"))))
        return center, vector(value(body, "m_Size"))


def check_maps(scene):
    legacy = {"0f17de99da5752842ab51fb0cf091db3", "e09de288d45bc474490d84b7d71e5c65",
              "dc5e5b80b25c22b4d9ff531191e1b833"}
    scene_assets = set(re.findall(r"guid: ([a-f0-9]{32})", scene.yaml))
    require(legacy.isdisjoint(scene_assets), "Release scene links directly to the full walking map")
    # Catch a new material that still displays the old atlas.
    for path in ROOT.rglob("*.mat"):
        if asset_guid(path) in scene_assets:
            material = read(path)
            require(all(key not in material for key in legacy), f"Active material displays the map: {path}")


def towers(scene):
    counts = []
    for landmark, mesh, location, min_height in (
        ("Five-storey pagoda", "Pagoda roof and timber", (12, 0, 13), 39),
        ("Broadcast tower beyond the garden", "Broadcast tower steelwork", (42, 0, 50), 65),
    ):
        root = ("grove", landmark)
        require(math.dist(scene.position(root), location) < .1, f"{landmark} is outside the shrine site")
        data = read(scene.mesh((*root, mesh), mesh + ".asset"))
        count = re.search(r"^    m_VertexCount: (\d+)$", data, re.M)
        bounds = re.search(r"^  m_LocalAABB:\n    m_Center: .*\n    m_Extent: (.*)$", data, re.M)
        require(bounds and min_height < 2 * vector(bounds[1])[1] < min_height + 4,
                f"Unexpected physical height of {landmark}")
        require(count and int(count[1]) > 1000, f"Missing structural geometry on {landmark}")
        counts.append(int(count[1]))
    return counts


def shrinking_rooms(scene):
    sections = {}
    for room, name, location, floor_label in (
        ("Diminishing Baths", "Bath bay", (0, 0, -133), "porcelain"),
        ("Night Reception", "Reception bay", (-123, 0, -80), "terrazzo"),
    ):
        require(math.dist(scene.position((room,)), location) < .05, room + " entrance was detached")
        profiles = []
        previous_end = None
        for number in range(1, 11):
            bay = (room, f"{name} {number:02}")
            scene.mesh((*bay, "Architecture"), "Architecture/" + bay[-1] + ".asset")
            walls = (*bay, "Walk surfaces and walls")
            left, left_size = scene.box((*walls, "Left boundary"))
            right, right_size = scene.box((*walls, "Right boundary"))
            ceiling, ceiling_size = scene.box((*walls, "Ceiling clearance"))
            width = right[0] - right_size[0] / 2 - left[0] - left_size[0] / 2
            # The trim can reuse the same material name as the walkable floor.
            floors = scene.by_path[("liminal uwu", *walls, floor_label)]
            floor_boxes = [box for oid in floors for box in scene.components[(oid, 65)]
                           if value(box, "m_IsTrigger") == "0" and value(box, "m_Enabled") == "1"
                           and vector(value(box, "m_Size"))[0] >= width]
            floors_underfoot = []
            for floor in floor_boxes:
                owner = file_id(value(floor, "m_GameObject"))
                pos = vector(value(scene.components[(owner, 4)][0], "m_LocalPosition"))
                center = vector(value(floor, "m_Center"))
                size = vector(value(floor, "m_Size"))
                if abs(pos[1] + center[1] + size[1] / 2) < .01 and size[2] >= ceiling_size[2] - .02:
                    floors_underfoot.append(size)
            require(floors_underfoot, f"Missing load-bearing floor in {bay}")
            height = ceiling[1] - ceiling_size[1] / 2
            middle = scene.position(bay)[2] + ceiling[2]
            near, far = middle - ceiling_size[2] / 2, middle + ceiling_size[2] / 2
            if profiles:
                require(0 < width < profiles[-1][0] and 0 < height < profiles[-1][1],
                        f"Collision envelope does not shrink through {bay}")
                require(abs(near - previous_end) < .03, f"Walkable bays have a gap at {bay}")
            profiles.append((round(width, 3), round(height, 3), round(middle, 3)))
            previous_end = far
            if number == 10:
                end, end_size = scene.box((*walls, "Closed end"))
                require(end_size[0] >= width and end_size[1] >= height - .01 and
                        abs(middle + end[2] - far) < .03, "Dead end fails to close " + room)
        require(profiles[0][0] > 9 and profiles[0][1] > 4 and
                profiles[-1][0] < 1 and profiles[-1][1] < 1, room + " does not narrow to miniature scale")
        sections[room] = profiles
    return sections


def network_ids(scene):
    descriptors = [b for b in scene.by_id[114].values() if "\n  spawns:" in b]
    require(len(descriptors) == 1, "Expected a single VRChat scene descriptor")
    table = re.search(r"^  NetworkIDs:\n(.+?)(?=^  \w+:|\Z)", descriptors[0], re.S | re.M)
    require(table, "Scene has no serialized network IDs")
    ids, assigned = set(), {}
    for row in re.split(r"^  - gameObject: ", table[1], flags=re.M)[1:]:
        owner = file_id(row.splitlines()[0])
        require(owner in scene.paths, "Network ID refers to a missing GameObject")
        path = scene.paths[owner]
        number = re.search(r"^    ID: (\d+)\s*$", row, re.M)
        require(number and path not in assigned, f"Missing or duplicate assignment at {path}")
        identifier = int(number[1])
        require(identifier > 0 and identifier not in ids, f"Invalid or reused network ID {identifier}")
        assigned[path] = (identifier, tuple(sorted(re.findall(r"^    - (.+)$", row, re.M))))
        ids.add(identifier)
    require(assigned, "Empty cross-platform network ID table")
    stations = [file_id(value(b, "m_GameObject")) for b in scene.by_id[114].values()
                if re.search(r"^  stationEnterPlayerLocation:", b, re.M)]
    require(stations and all(scene.paths[o] in assigned for o in stations), "A train station has no stable network ID")
    return assigned


def gains(scene):
    levels = {}
    areas = ("hub", "rail", "air", "grove", "motor", "gravity", "observatory", "gallery", "service", "baths", "annex")
    expected = {room + " / room tone": room for room in areas}
    expected["Local field induction tone"] = "induction"
    for body in scene.by_id[82].values():
        owner = file_id(value(body, "m_GameObject"))
        room = expected.get(scene.names[owner])
        if not room:
            continue
        gain = float(value(body, "m_Volume"))
        limit = {"hub": .18, "observatory": .28, "induction": .32}.get(room, .39)
        require(0 < gain <= limit and value(body, "m_Enabled") == "1", f"Missing or excessive area sound in {room}")
        if room != "induction":
            require(asset_guid(ROOT / "Media/Audio" / (room + ".wav")) == guid(value(body, "m_audioClip")) and
                    (value(body, "Loop") or value(body, "m_Loop")) == "1", "Incorrect looping ambience for " + room)
        levels[scene.paths[owner]] = gain
    require({expected[k[-1]] for k in levels} == set(expected.values()), "An area lost its ambience")
    return levels


def film_binding(scene):
    receiver = scene.assets / "Finishes/Embankment receiver.mat"
    material = read(receiver)
    require(guid(value(material, "m_Shader")) == asset_guid(ROOT / "Shaders/Transmission.shader") and
            asset_guid(ROOT / "Media/Optics/Embankment signal.jpg") in material, "Receiver film or shader missing")
    valid_keywords = re.search(r"^  m_ValidKeywords:(.*?)(?=^  \w+:)", material, re.S | re.M)
    require(valid_keywords and ("SW_MOBILE" in valid_keywords[1]) == (scene.assets.name == "Quest"),
            "Receiver uses the wrong platform shader variant")
    receiver_guid = asset_guid(receiver)
    for room, site in (("hub", "Embankment signal kiosk"), ("gallery", "Embankment projection")):
        path = (room, site, "Architecture")
        scene.mesh(path, "Architecture/" + site + ".asset")
        require(receiver_guid in scene.component(path, 23), "Screen has no receiver in " + room)
    clock = scene.components[(scene.at(("Embankment broadcast clock",)), 114)]
    program = asset_guid(ROOT / "Runtime/CitySignalClock.asset")
    require(any(guid(value(b, "programSource")) == program for b in clock), "Missing compiled broadcast controller")
    require(any(guid(value(b, "receiver")) == receiver_guid for b in clock), "Broadcast clock has no target material")


def film_frames():
    with Image.open(ROOT / "Media/Optics/Embankment signal.jpg") as film:
        require(film.size == (4096, 1152), "Missing or truncated 32-frame embankment broadcast")
        frames = []
        for idx in (0, 16, 31):
            x, y = idx % 8 * 512, idx // 8 * 288
            frames.append(film.crop((x, y, x + 512, y + 288)).convert("RGB").resize((96, 54)))
        for a, b in zip(frames, frames[1:]):
            deltas = ImageStat.Stat(ImageChops.difference(a, b)).mean
            require(max(deltas) > .3, "Intact, collapsing and empty frames must differ")
    shader = re.sub(r"\s+", "", read(ROOT / "Shaders/Transmission.shader"))
    clock = re.sub(r"\s+", "", read(ROOT / "Runtime/CitySignalClock.cs"))
    require("if(sec>=22.)returnhalf4(0,0,0,1);" in shader and "saturate((sec-4.)/14.5)*31." in shader and
            "sampleFrame(floor(frame),uv,mip)" in shader, "Film must advance through its atlas and reach absolute black")
    require("Networking.GetServerTimeInSeconds()" in clock and '%24.0' in clock and
            'receiver.SetFloat("_BroadcastSecond"' in clock and "SendCustomEventDelayedSeconds(" in clock,
            "Blackout must restart on the shared 24-second clock")


if __name__ == "__main__":
    film_frames()
    results = {}
    for edition in ("PC", "Quest"):
        scene = Scene(edition)
        check_maps(scene)
        film_binding(scene)
        results[edition] = (towers(scene), shrinking_rooms(scene), network_ids(scene), gains(scene))
    pc, quest = results["PC"], results["Quest"]
    require(all(a > b for a, b in zip(pc[0], quest[0])), "Quest needs simplified tower meshes")
    for index, feature in ((1, "Shrinking collision profiles"), (2, "Network IDs and hierarchy paths"), (3, "Room gains")):
        require(pc[index] == quest[index], feature + " differ between PC and Quest")
    print(json.dumps({"tower_vertices": {k: v[0] for k, v in results.items()}, "shrinking_bays": sum(map(len, pc[1].values())),
                      "matched_network_ids": len(pc[2]), "matched_area_emitters": len(pc[3]),
                      "broadcast_frames": 32, "blackout_seconds": 2}, indent=2))
