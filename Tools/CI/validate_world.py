"""Validate the actual authored scene assets on a fresh hosted runner."""
import collections
import hashlib
import json
import math
import os
from pathlib import Path
import re
import wave

ROOT = Path("Assets/Stillwater")
ROOMS = {"hub", "rail", "air", "grove", "motor", "gravity", "observatory", "gallery", "service"}
OUTER_ROOMS = {"billboard", "concourse", "causeway", "stacks", "returns"}
NEW_ROOMS = {"waterfall", "ballroom", "lounge", "formation", "planet"}
ALL_ROOMS = ROOMS | OUTER_ROOMS | NEW_ROOMS
PASSAGES = 18
GUID = r"[0-9a-f]{32}"
DOCUMENT = re.compile(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n", re.M)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(path):
    return Path(path).read_text(encoding="utf-8-sig")


def documents(yaml):
    markers = list(DOCUMENT.finditer(yaml))
    for i, mark in enumerate(markers):
        end = markers[i + 1].start() if i + 1 < len(markers) else len(yaml)
        yield int(mark[1]), mark[2], yaml[mark.end():end]


def value(body, name):
    match = re.search(r"^  " + name + r": (.+)$", body, re.M)
    return match[1].strip() if match else ""


def file_id(ref):
    found = re.search(r"fileID: (-?\d+)", ref)
    return found[1] if found else "0"


def guid(ref):
    m = re.search("guid: (" + GUID + ")", ref)
    return m[1] if m else ""


def asset_guid(path):
    return guid(read(str(path) + ".meta"))


def media():
    digests = set()
    for room in ROOMS:
        with wave.open(str(ROOT / "Media/Audio" / (room + ".wav")), "rb") as stream:
            frames = stream.getnframes()
            require(stream.getnchannels() == 1 and stream.getsampwidth() == 2, room + " needs mono PCM source")
            require(15 <= frames / stream.getframerate() <= 60, room + " soundscape length")
            samples = stream.readframes(frames)
            require(len(set(samples)) > 150, room + " contains silent or invalid media")
            digests.add(hashlib.sha256(samples).digest())
    require(len(digests) == len(ROOMS), "Every area requires its own soundscape")
    for i in range(4):
        path = ROOT / "Media/Prints" / f"Plate{i}.jpg"
        require(path.read_bytes()[:2] == b"\xff\xd8", f"Missing original print {i}")


def check_captured_sky():
    """Inspect the rendered +Y cubemap face above the observatory, including its ring."""
    source = read(ROOT / "Generated/PC/Finishes/observatory reflection.asset")
    width = int(value(source, "m_Width"))
    stride = int(value(source, "m_CompleteImageSize"))
    require(value(source, "m_TextureFormat") == "4", "Expected a readable RGBA32 sky capture")
    pixels = bytes.fromhex(value(source, "_typelessdata"))[stride*2:stride*3]
    interior, annulus = [], []
    elevation = math.radians(48)
    for y in range(width):
        for x in range(width):
            dx, dz = (2*x+1)/width-1, (2*y+1)/width-1
            along = (math.sin(elevation)+dz*math.cos(elevation)) / math.sqrt(1+dx*dx+dz*dz)
            angle = math.acos(max(-1, min(1, along)))
            offset = (y*width+x)*4
            peak = max(pixels[offset:offset+3])
            if angle < .14:
                interior.append(peak)
            if .183 < angle < .207:
                annulus.append(peak)
    require(len(interior) > 100 and max(interior) == 0, "Rendered observatory aperture contains light")
    require(annulus and max(annulus) > 160, "Rendered observatory is missing the luminous rim")


def check_scene(edition):
    scene = ROOT / "Scenes" / f"liminal uwu - {edition}.unity"
    yaml = read(scene)
    source_guids = set(re.findall("guid: (" + GUID + ")", yaml))
    docs = list(documents(yaml))
    by_kind = collections.defaultdict(list)
    for kind, oid, body in docs:
        by_kind[kind].append((oid, body))
    names = {oid: value(body, "m_Name") for oid, body in by_kind[1]}
    transforms = {oid: (file_id(value(body, "m_GameObject")), file_id(value(body, "m_Father")))
                  for oid, body in by_kind[4]}
    room_names = {names[go] for go, parent in transforms.values()
                  if parent in transforms and names[transforms[parent][0]] == "liminal uwu"}
    require(ALL_ROOMS <= room_names, "Missing districts in " + edition)
    routes = set()
    for name in room_names:
        edge = re.fullmatch(r"Passage (\w+) to (\w+)", name)
        if edge:
            a, b = edge.groups()
            require(a in ALL_ROOMS and b in ALL_ROOMS and a != b, "Broken passage endpoint " + name)
            routes.add(tuple(sorted((a, b))))
    connected = {"hub"}
    for _ in ALL_ROOMS:
        for a, b in routes:
            if a in connected or b in connected:
                connected.update((a, b))
    require(connected == ALL_ROOMS - NEW_ROOMS and len(routes) == PASSAGES,
            "The preserved fourteen-district architecture must retain eighteen sealed passages")
    require(len(by_kind[65]) >= 70, "Missing architectural colliders")
    target = ROOT / "Generated" / edition
    other = "Quest" if edition == "PC" else "PC"
    for key in ("wet", "ice", "pink", "amber", "art0", "art1", "art2", "art3"):
        mat = target / "Finishes" / (key + ".mat")
        require(asset_guid(mat) in source_guids, f"{edition} has no instances of {key}")
        require(asset_guid(ROOT / "Shaders/StationFinish.shader") in read(mat), "Finish shader mismatch")
    wet = read(target / "Finishes/wet.mat")
    require("SW_WET" in wet, "Wet floor variant is missing")
    require(("SW_MOBILE" in wet) == (edition == "Quest"), "Wrong platform water variant")
    require(asset_guid(ROOT / "Generated" / other / "Finishes/wet.mat") not in source_guids, "Mixed platform floor")
    sky = target / "Finishes/Night sky.mat"
    skyref = value(by_kind[104][0][1], "m_SkyboxMaterial")
    require(guid(skyref) == asset_guid(sky), "Scene skybox is unassigned")
    sky_material = read(sky)
    require(asset_guid(ROOT / "Shaders/Eclipse.shader") in sky_material, "Skybox requires the ring shader")
    require(("SW_MOBILE" in sky_material) == (edition == "Quest"), "Incorrect eclipse rendering tier")
    for texture in ("Orbital streams.png", "Deep field.jpg"):
        require(asset_guid(ROOT / "Media/Optics" / texture) in sky_material, f"Sky missing {texture}")
    assert_black = re.search(r"if\s*\(\s*radius\s*<\s*edge\s*\)\s*return\s+half4\(0,0,0,1\)", read(ROOT / "Shaders/Eclipse.shader"))
    require(assert_black, "The sky aperture must sample absolute black before the annular emission pass")
    if edition == "PC":
        for path in (target / "Finishes").glob("* reflection.asset"):
            data = bytes.fromhex(value(read(path), "_typelessdata"))
            # Unity's fallback on a missing shader is exact saturated magenta.
            require(data.count(bytes((255, 0, 255, 255))) < 30, f"Broken shader color in {path.name}")
        hub_floor = target / "Finishes/hub wet.mat"
        floor_yaml = read(hub_floor)
        require(asset_guid(hub_floor) in source_guids and "SW_PROBE" in floor_yaml
                and asset_guid(target / "Finishes/hub reflection.asset") in floor_yaml
                and asset_guid(target / "Finishes/hub pavement irradiance.asset") in floor_yaml,
                "Hub floor requires its own captured architecture reflection and pavement lighting")
    require(not any(" / room tone" in name for oid, name in names.items()
                    if any(file_id(value(b, "m_GameObject")) == oid for _, b in by_kind[82])),
            "Legacy point ambience must not compete with the room-wide local audio buses")
    rain = [body for _, body in by_kind[198] if names[file_id(value(body, "m_GameObject"))] == "Courtyard rain"]
    require(len(rain) == 2, "Two exposed rain courtyards are required")
    controllers = [body for oid, body in by_kind[95] if asset_guid(target / "SpiralService.controller") in body]
    require(controllers, "Train has no platform-specific animator")
    coach_count = sum(bool(re.fullmatch("Coach [1-9]", n)) for n in names.values())
    require(coach_count == 3, f"Wrong train consist for {edition}")
    require("m_LoopTime: 1" in read(target / "SpiralService.anim"), "Rail return working must loop")
    pipelines = [body for _, body in by_kind[114] if "  blueprintId:" in body]
    require(len(pipelines) == 1 and value(pipelines[0], "contentType") == "1", "Pipeline must identify this scene as a VRChat world")
    descriptors = [body for _, body in by_kind[114] if re.search(r"^  spawns:$", body, re.M)]
    require(len(descriptors) == 1 and re.search(r"  spawns:\n  - \{fileID: [1-9]", descriptors[0]), "Missing arrival")
    udon = [body for _, body in by_kind[114] if "  programSource:" in body]
    expected_programs = {
        "Transit/LastTrainBoard.asset": 5,
        "Runtime/AdventureJourney.asset": 1,
        "Runtime/UwuBallPit.asset": 1,
        "Runtime/RoomFormation.asset": 1,
        "Runtime/LoungeSeat.asset": 4,
        "Runtime/FloatingStreetObjects.asset": 1,
        "Runtime/LocalPracticalLights.asset": 1,
        "Runtime/PlanetGalleryControl.asset": 1,
        "Personal/PlanetMovement/BobysPlanetMovement.asset": 1,
        "Personal/PlanetMovement/PlanetMove_GravityField.asset": 1,
        "Personal/PlanetMovement/PlanetMove_StationListener.asset": 1,
        "Personal/PlanetMovement/PlanetMove_AutoExitManager.asset": 1,
        "Runtime/StillwaterGravityPort.asset": 8,
        "Runtime/ApertureExpress.asset": 1,
        "Runtime/ExpressSeat.asset": 6,
        "Runtime/CitySignalClock.asset": 1,
        "Runtime/WorldAudioDirector.asset": 1,
        "Runtime/DistrictVisibility.asset": 1,
        "Runtime/BillboardPlayer.asset": 1,
        "Runtime/BillboardMenuToggle.asset": 2,
        "Runtime/ApertureApproach.asset": 1,
    }
    program_counts = collections.Counter(guid(value(body, "programSource")) for body in udon)
    for path, count in expected_programs.items():
        require(program_counts[asset_guid(ROOT / path)] == count, f"{edition} needs {count} instances of {path}")
    require(len(udon) == sum(expected_programs.values()), f"{edition} has unexpected Udon components")
    for body in udon:
        name = names[file_id(value(body, "m_GameObject"))]
        program = guid(value(body, "programSource"))
        require(program, f"{edition} Udon component on {name} has no program source")
        targets = PROGRAMS.get(program)
        require(targets and targets.is_file(), f"{edition} Udon program on {name} is absent from source control")
        serialized = guid(value(body, "serializedProgramAsset"))
        if not serialized:
            program_text = read(targets)
            m = re.search(r"(?:serializedUdonProgramAsset|serializedProgramAsset): \{fileID: [^,]+, guid: (" + GUID + ")", program_text)
            serialized = m[1] if m else None
        require(serialized in PROGRAMS, f"{edition} Udon program on {name} has no compiled backing asset")
    meshes = list(target.glob("Landmark-*.asset")) + [target / "Local-Carriage.asset", target / "Pocket door.asset"]
    require(len(meshes) >= 8, edition + " lacks landmark meshes")
    vertices = 0
    for mesh in meshes:
        require(asset_guid(mesh) in source_guids, f"Orphaned landmark: {mesh}")
        with mesh.open("rb") as stream:
            data = stream.read()
        counts = re.findall(rb"^\s*m_VertexCount:\s*(\d+)\s*$", data, re.M)
        require(counts and int(counts[0]) > 0, f"No serialized geometry found in {mesh}")
        vertices += int(counts[0])
    probes = len(by_kind[215])
    require(probes == 0 if edition == "Quest" else probes >= 9, f"{edition} reflection probe budget")
    instances = collections.Counter(guid(value(body, "m_Mesh")) for _, body in by_kind[33])
    model_triangles = 0
    for mesh_id, copies in instances.items():
        mesh_path = PROGRAMS.get(mesh_id)
        if mesh_path is None:
            continue
        # The serialized count includes all instances before any culling, including rotated wings.
        mesh_body = read(mesh_path)
        counts = re.findall(r"^    indexCount: (\d+)$", mesh_body, re.M)
        require(counts and all(int(n) % 3 == 0 for n in counts), f"Invalid triangle data in {mesh_path}")
        model_triangles += sum(int(n)//3 for n in counts) * copies
    require(model_triangles > 0, "No measurable scene geometry")
    return {"districts": len(ALL_ROOMS), "preserved_sealed_passages": len(routes),
            "box_colliders": len(by_kind[65]), "udon_behaviours": len(udon), "coaches": coach_count,
            "unique_landmark_vertices": vertices, "reflection_probes": probes, "rain_courtyards": len(rain),
            "authored_mesh_triangles_before_culling": model_triangles}


if __name__ == "__main__":
    PROGRAMS = {asset_guid(path): path for path in Path("Assets").rglob("*.asset") if Path(str(path) + ".meta").is_file()}
    media()
    check_captured_sky()
    results = {platform: check_scene(platform) for platform in ("PC", "Quest")}
    require(results["Quest"]["unique_landmark_vertices"] < results["PC"]["unique_landmark_vertices"],
            "Quest must use reduced geometry")
    require(results["Quest"]["authored_mesh_triangles_before_culling"] < .72 * results["PC"]["authored_mesh_triangles_before_culling"],
            "Quest should retain less than 72 percent of the total PC triangle count")
    print(json.dumps(results, indent=2))
    if os.getenv("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
            summary.write("## Scene source checks\n\n| Metric | PC | Quest |\n|---|---:|---:|\n")
            for key in results["PC"]:
                summary.write(f"| {key.replace('_', ' ')} | {results['PC'][key]:,} | {results['Quest'][key]:,} |\n")
            summary.write("\nThe Visual Review workflow checks walkable clearance. The manual export workflow builds Unity bundles.\n")
