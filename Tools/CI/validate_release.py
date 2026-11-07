"""Check release-only features against the two serialized Unity scenes."""
from collections import defaultdict
import json
import math
import re
import struct

from validate_world import ROOT, ALL_ROOMS, PASSAGES, asset_guid, documents, file_id, guid, read, require, value


def vector(text):
    return tuple(float(re.search(r"\b" + axis + r": ([-+.\deE]+)", text)[1]) for axis in "xyz")


def block(body, key):
    match = re.search(r"^  " + re.escape(key) + r":([^\n]*)(?:\n(.*?))?(?=^  [A-Za-z_]|\Z)", body, re.M | re.S)
    require(match is not None, "Missing serialized field " + key)
    return (match[1] + "\n" + (match[2] or "")).strip()


def rows(body, key):
    return re.findall(r"^\s*- (.+)$", block(body, key), re.M)


def integers(body, key, boolean=False):
    data = block(body, key)
    if data.startswith("-"):
        return [int(v) for v in rows(body, key)]
    if data == "[]":
        return []
    raw = bytes.fromhex(data)
    if boolean:
        return list(raw)
    require(len(raw) % 4 == 0, key + " has malformed integer storage")
    return [item[0] for item in struct.iter_unpack("<i", raw)]


def vectors(body, key):
    return [vector(row) for row in rows(body, key)]


def floats(body, key):
    items = rows(body, key)
    if items:
        return [float(item) for item in items]
    raw = bytes.fromhex(block(body, key))
    require(len(raw) % 4 == 0, key + " has malformed float storage")
    return [item[0] for item in struct.iter_unpack("<f", raw)]


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
        self.transforms = {**self.by_id[4], **self.by_id[224]}
        for tid, body in self.transforms.items():
            owner = file_id(value(body, "m_GameObject"))
            segments = []
            while tid != "0":
                transform = self.transforms[tid]
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
        owner = self.at(path)
        body = (self.components[(owner, 4)] + self.components[(owner, 224)])[0]
        return vector(value(body, "m_LocalPosition"))

    def script(self, name):
        script_id = asset_guid(ROOT / "Runtime" / (name + ".cs"))
        matches = [body for body in self.by_id[114].values() if guid(value(body, "m_Script")) == script_id]
        require(len(matches) == 1, "Expected one serialized " + name)
        return matches[0]

    def resolve(self, reference, kind=None):
        oid = file_id(reference)
        if kind is not None:
            require(oid in self.by_id[kind], "Missing serialized component " + reference)
            return self.by_id[kind][oid]
        for entries in self.by_id.values():
            if oid in entries:
                return entries[oid]
        raise ValueError("Missing scene reference " + reference)

    def reference_path(self, reference):
        body = self.resolve(reference)
        owner = file_id(value(body, "m_GameObject"))
        return self.paths.get(owner, self.paths.get(file_id(reference)))

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
    control = scene.script("WorldAudioDirector")
    low, high = vectors(control, "zoneMin"), vectors(control, "zoneMax")
    clips, rain = rows(control, "atmosphereClips"), integers(control, "rainy", boolean=True)
    require(len(low) == len(high) == len(clips) == len(rain) == len(ALL_ROOMS) + PASSAGES + 5,
            "Audio needs bounds, ambience and rain flags for every district, passage and annex")
    available = {asset_guid(path) for path in (ROOT / "Media/Audio").glob("*.wav")}
    require(all(guid(clip) in available for clip in clips), "An audio sector has a missing clip")
    require(all(all(a < b for a, b in zip(lo, hi)) for lo, hi in zip(low, high)), "An audio sector has empty bounds")
    for room in ALL_ROOMS:
        point = scene.position((room,))
        require(any(all(a <= p <= b for p, a, b in zip(point, lo, hi)) for lo, hi in zip(low, high)),
                "Room-wide audio does not cover " + room)
    sources, levels = [], {}
    for bus in ("rain", "atmosphere"):
        source = scene.resolve(value(control, bus), 82)
        sources.append(file_id(value(control, bus)))
        gain = float(value(source, "m_Volume"))
        slider = float(value(control, bus + "Volume"))
        require(0 <= gain <= .1 and 0 < slider <= .5, "World audio should start quiet: " + bus)
        require(value(source, "m_Enabled") == "1" and value(source, "m_PlayOnAwake") == "0"
                and (value(source, "Loop") or value(source, "m_Loop")) == "1", "Audio bus must loop under local control")
        require(value(source, "Spatialize") == "0" and value(source, "DopplerLevel") == "0",
                "Room sound must remain uniform while walking")
        spatial = block(source, "panLevelCustomCurve")
        blend = [float(v) for v in re.findall(r"^\s+value: (.+)$", spatial, re.M)]
        require(blend and all(v == 0 for v in blend), "Room audio is still distance-localized")
        owner = file_id(value(source, "m_GameObject"))
        require(scene.paths[owner][1] == "Local room soundscapes", "Audio bus escaped the local director")
        levels[bus] = (slider, gain)
    require(len(set(sources)) == 2, "Rain and atmosphere require independently controlled sources")
    require(guid(value(scene.resolve(value(control, "rain"), 82), "m_audioClip")) == asset_guid(ROOT / "Media/Audio/rain.wav"),
            "Rain slider has the wrong sound")
    bus_sources = [oid for oid, b in scene.by_id[82].items()
                   if scene.paths[file_id(value(b, "m_GameObject"))][1] == "Local room soundscapes"]
    require(set(bus_sources) == set(sources), "Unexpected room audio source competes with the two buses")
    return {"levels": levels, "bounds": list(zip(low, high)), "clips": [guid(c) for c in clips], "rain": rain}


def visibility(scene):
    control = scene.script("DistrictVisibility")
    low, high = vectors(control, "zoneMin"), vectors(control, "zoneMax")
    require(len(low) == len(high) == len(ALL_ROOMS) + PASSAGES + 5, "Missing room visibility bounds")
    renderers, sectors = rows(control, "renderers"), integers(control, "rendererSector")
    require(len(renderers) == len(sectors) and renderers, "Renderer visibility groups are not wired")
    groups = defaultdict(set)
    for ref, index in zip(renderers, sectors):
        require(0 <= index < len(low), "Renderer refers to an invalid room")
        path = scene.reference_path(ref)
        require(path and len(path) > 2 and "Portable local menu" not in path and "Local wrist video button" not in path,
                "Portable controls must remain outside district culling")
        groups[index].add(path[1])
    require(len(groups) == len(low) and all(len(names) == 1 for names in groups.values()), "Visibility sector mixes districts or is empty")
    names = {i: next(iter(items)) for i, items in groups.items()}
    require(ALL_ROOMS <= set(names.values()), "An entire district is omitted from visibility control")
    a, b = integers(control, "portalA"), integers(control, "portalB")
    centers = vectors(control, "portalCenter")
    radii = floats(control, "portalRadius")
    require(len(a) == len(b) == len(centers) == len(radii) == 2,
            "Only the two open shrinking annexes should reveal adjacent rooms")
    edges = set()
    for x, y, radius in zip(a, b, radii):
        require(x != y and x in names and y in names and radius > 0, "Invalid room visibility portal")
        edge = tuple(sorted((names[x], names[y])))
        require(edge not in edges, "Duplicate room visibility portal")
        edges.add(edge)
    require(all(not endpoint.startswith("Passage ") for edge in edges for endpoint in edge),
            "Sealed legacy passages must not reveal another district")
    require(tuple(sorted(("gallery", "Diminishing Baths"))) in edges and
            tuple(sorted(("service", "Night Reception"))) in edges, "Shrinking annex portal is missing")
    return {"bounds": list(zip(low, high)), "links": sorted(edges), "portals": list(zip(centers, radii))}


def billboard(scene):
    control = scene.script("BillboardPlayer")
    owner = file_id(value(control, "m_GameObject"))
    native = scene.resolve(value(control, "video"), 114)
    require(file_id(value(native, "m_GameObject")) == owner, "Video callbacks must reach the player behaviour")
    require(value(native, "autoPlay") == "0" and value(native, "loop") == "0"
            and value(native, "renderMode") == "0", "Billboard must use the controlled RenderTexture player")
    texture = scene.assets / "Billboard target.renderTexture"
    require(guid(value(native, "targetTexture")) == asset_guid(texture), "Native player has no billboard output")
    target = read(texture)
    expected = (512, 288) if scene.assets.name == "Quest" else (1024, 576)
    require((int(value(target, "m_Width")), int(value(target, "m_Height"))) == expected, "Wrong billboard platform resolution")
    material = scene.assets / "Finishes/Billboard video.mat"
    require(guid(value(control, "screenMaterial")) == asset_guid(material), "Playback status targets the wrong screen")
    require(asset_guid(texture) in read(material) and asset_guid(ROOT / "Shaders/BillboardVideo.shader") in read(material),
            "Billboard shader is missing its video texture")
    speaker = scene.resolve(value(control, "speaker"), 82)
    require(0 < float(value(speaker, "m_Volume")) <= .2 and value(speaker, "m_PlayOnAwake") == "0",
            "Billboard audio should be quiet and playback-controlled")
    require([file_id(ref) for ref in rows(native, "targetAudioSources")] == [file_id(value(control, "speaker"))],
            "Native video audio is disconnected")
    audio = scene.script("WorldAudioDirector")
    require(scene.resolve(value(control, "worldAudio"), 114) == audio, "Portable world audio controls are unwired")
    menu = scene.reference_path(value(control, "menu"))
    wrist = scene.reference_path(value(control, "wristButton"))
    require(menu == ("liminal uwu", "Portable local menu") and wrist == ("liminal uwu", "Local wrist video button"),
            "Portable controls must remain accessible outside the billboard district")
    require(value(scene.resolve(value(control, "menu"), 1), "m_IsActive") == "0", "Portable menu should start closed")
    for slider_name, callback in (("videoVolume", "UpdateLocalVolume"), ("rainVolume", "RainVolumeChanged"),
                                  ("atmosphereVolume", "AtmosphereVolumeChanged")):
        slider = scene.resolve(value(control, slider_name), 114)
        require(scene.reference_path(value(control, slider_name))[:2] == menu, "Local volume slider is outside its panel")
        require("m_StringArgument: " + callback in slider and "m_MethodName: SendCustomEvent" in slider,
                "Volume slider has no persistent event: " + slider_name)
    for callback in ("LoadForEveryone", "PlayForEveryone", "PauseForEveryone", "StopForEveryone", "RetryLocal", "CloseMenu"):
        matches = [body for body in scene.by_id[114].values() if "m_StringArgument: " + callback in body
                   and scene.paths.get(file_id(value(body, "m_GameObject")), ())[:2] == menu]
        require(len(matches) == 1 and "m_MethodName: SendCustomEvent" in matches[0], "Missing usable video menu action " + callback)
    require(scene.reference_path(value(control, "urlInput"))[:2] == menu, "URL entry is outside the local menu")
    for path in (("hub", "Embankment signal kiosk"), ("billboard", "Side street billboard"),
                 ("billboard", "Sidewalk video controls")):
        require(abs(scene.position(path)[0]) > 10, "A video fixture obstructs the road at " + str(path))
    require(sum("stationEnterPlayerLocation:" in b for b in scene.by_id[114].values()) == 11,
            "World must preserve six train seats, four lounge seats and the gravity station")
    return {"menu": menu, "wrist": wrist, "local_audio_sliders": 3}


def film_binding(scene):
    receiver = scene.assets / "Finishes/Embankment receiver.mat"
    material = read(receiver)
    require(guid(value(material, "m_Shader")) == asset_guid(ROOT / "Shaders/Transmission.shader"), "Receiver shader is missing")
    bindings = {name: guid(body) for name, body in re.findall(r"^    - (_\w+):\n(.*?)(?=^    - |^    m_|\Z)", material, re.M | re.S)}
    for property_name, filename in (("_Plasma", "Optics/Orbital streams.png"), ("_Stars", "Optics/Deep field.jpg"),
                                    ("_Nebula", "Sky/Nebula.png")):
        require(bindings.get(property_name) == asset_guid(ROOT / "Media" / filename), "Film is missing its " + property_name + " texture")
    transfer = bindings.get("_GeodesicTransfer", "")
    require(transfer == (asset_guid(ROOT / "Media/Optics/Geodesic transfer.asset") if scene.assets.name == "PC" else ""),
            "Film uses the wrong gravitational-lens quality tier")
    require("_Film" not in bindings and asset_guid(ROOT / "Media/Optics/Embankment signal.jpg") not in material,
            "Obsolete collapsing-city footage remains attached to the release film")
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


def film_contract():
    """Source guardrails; hosted image review is still required for the rendered film."""
    shader = read(ROOT / "Shaders/Transmission.shader")
    clock = read(ROOT / "Runtime/CitySignalClock.cs")
    require(re.search(r"if\s*\(\s*sec\s*>=\s*22(?:\.0*)?\s*\)\s*return\s+half4\(\s*0,\s*0,\s*0,\s*1\)", shader),
            "The final two seconds must reach absolute black")
    require(re.search(r"%\s*24(?:\.0*)?", clock) and "Networking.GetServerTimeInSeconds()" in clock,
            "Film must restart on a shared 24-second clock")
    require("_Film" not in shader and "sampleFrame" not in shader, "Receiver still samples the collapsing-city atlas")


if __name__ == "__main__":
    film_contract()
    results = {}
    for edition in ("PC", "Quest"):
        scene = Scene(edition)
        check_maps(scene)
        film_binding(scene)
        results[edition] = (towers(scene), shrinking_rooms(scene), network_ids(scene), gains(scene), visibility(scene), billboard(scene))
    pc, quest = results["PC"], results["Quest"]
    require(all(a > b for a, b in zip(pc[0], quest[0])), "Quest needs simplified tower meshes")
    for index, feature in ((1, "Shrinking collision profiles"), (2, "Network IDs and hierarchy paths"), (3, "Room audio routing"),
                           (4, "Visibility bounds and portals"), (5, "Portable menu bindings")):
        require(pc[index] == quest[index], feature + " differ between PC and Quest")
    print(json.dumps({"tower_vertices": {k: v[0] for k, v in results.items()}, "shrinking_bays": sum(map(len, pc[1].values())),
                      "matched_network_ids": len(pc[2]), "audio_sectors": len(pc[3]["bounds"]), "world_audio_buses": 2,
                      "visibility_portals": len(pc[4]["links"]), "local_volume_sliders": pc[5]["local_audio_sliders"],
                      "broadcast_seconds": 24, "blackout_seconds": 2}, indent=2))
