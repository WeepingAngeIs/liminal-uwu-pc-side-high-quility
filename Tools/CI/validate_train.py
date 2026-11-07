"""Check the actual serialized ride on both platforms, including animation and station bindings."""
import hashlib
import math
import re
from collections import Counter
from pathlib import Path

from validate_world import ROOT, asset_guid, documents, file_id, guid, read, require, value


def scene(edition):
    yaml = read(ROOT / "Scenes" / f"liminal uwu - {edition}.unity")
    docs = {oid: (kind, body) for kind, oid, body in documents(yaml)}
    finishes = ROOT / "Generated" / edition / "Finishes"
    for surface, mode in (("ochre", 1), ("black", 2), ("brass", 3), ("ivory", 0), ("teal", 0)):
        mat = finishes / ("Rail " + surface + ".mat")
        spec = read(mat)
        require(asset_guid(mat) in yaml, "Missing carriage finish " + surface)
        require(asset_guid(ROOT / "Shaders/RailEnamel.shader") in spec and f"- _Finish: {mode}" in spec,
                "Wrong carriage material on " + surface)
        require(("RAIL_MOBILE" in spec) == (edition == "Quest"), "Wrong rolling stock detail tier")
        if surface == "ochre":
            require(asset_guid(ROOT / "Media/Surfaces/Platform moquette.jpg") in spec, "Upholstery texture is absent")
    names = {oid: value(body, "m_Name") for oid, (kind, body) in docs.items() if kind == 1}
    transforms = {oid: (file_id(value(body, "m_GameObject")), file_id(value(body, "m_Father")))
                  for oid, (kind, body) in docs.items() if kind == 4}
    go_transform = {go: tr for tr, (go, _) in transforms.items()}

    def trpath(tr):
        parts = []
        while tr != "0":
            go, tr = transforms[tr]
            parts.append(names[go])
        return "/".join(reversed(parts))

    def path(body):
        return trpath(go_transform[file_id(value(body, "m_GameObject"))])

    def refs(body, name):
        group = re.search(r"^  " + name + r":\n((?:  - \{fileID: [-\d]+\}\n)+)", body, re.M)
        return re.findall(r"fileID: ([-\d]+)", group[1]) if group else []

    controller_guid = asset_guid(ROOT / "Runtime/ApertureExpress.cs")
    seat_guid = asset_guid(ROOT / "Runtime/ExpressSeat.cs")
    approach_guid = asset_guid(ROOT / "Runtime/ApertureApproach.cs")
    proxies = {k: b for k, (kind, b) in docs.items() if kind == 114}
    clocks = [(oid, body) for oid, body in proxies.items() if guid(value(body, "m_Script")) == controller_guid]
    require(len(clocks) == 1, "Expected one shared dispatch clock")
    clock_id, clock = clocks[0]
    fields = [(oid, body) for oid, body in proxies.items() if guid(value(body, "m_Script")) == approach_guid]
    require(len(fields) == 1, "Expected one local aperture approach controller")
    approach_id, approach = fields[0]
    require(file_id(value(approach, "service")) == clock_id, "Pull field must recognize the six train seats")
    require(trpath(file_id(value(approach, "arrival"))).endswith("/World descriptor/Arrival"), "The pull must return to the authored world spawn")
    deck = file_id(value(approach, "safeDeck"))
    require(trpath(deck).endswith("/Aperture overlook"), "Missing safe upper landing")
    require(float(re.search(r"y: ([-+eE.\d]+)", value(docs[deck][1], "m_LocalPosition"))[1]) > 230,
            "The overlook did not move with the extended train")
    origin = file_id(value(approach, "approachStart"))
    rotation = tuple(float(v) for v in re.findall(r"[xyzw]: ([-+eE.\d]+)", value(docs[origin][1], "m_LocalRotation")))
    x, y, z, w = rotation
    forward = (2*(x*z+y*w), 2*(y*z-x*w), 1-2*(x*x+y*y))
    require(sum(a*b for a, b in zip(forward, (0, .743144825, .669130606))) > .99999,
            "Pull direction must align with the black-hole sky pole")
    rumble_id = file_id(value(approach, "rumble"))
    require(docs[rumble_id][0] == 82, "Approach rumble must use an audio source")
    rumble = docs[rumble_id][1]
    require(value(rumble, "Spatialize") == "0" and value(rumble, "m_PlayOnAwake") == "0" and value(rumble, "Loop") == "1",
            "Approach rumble must be a quiet local loop controlled by proximity")
    require(asset_guid(ROOT / "Media/Audio/aperture.wav") == guid(value(rumble, "m_audioClip")), "Wrong aperture rumble")
    require(guid(value(approach, "localSky")) == asset_guid(finishes / "Night sky.mat"), "Approach must use this platform's local sky")
    require(file_id(value(approach, "audioDirector")) in proxies, "Approach must respect the player's atmosphere volume")
    service = file_id(value(clock, "service"))
    require(docs[service][0] == 95 and value(docs[service][1], "m_CullingMode") == "0",
            "Train animation must update even when the passenger looks away")
    crew = [trpath(tr) for tr in refs(clock, "coaches")]
    require(len(crew) == 3 and len(set(crew)) == 3, "A three-car clock binding is required")
    require(trpath(file_id(value(clock, "platformExit"))).endswith("/Platform 01/Dock safe exit"), "Missing safe dock exit")
    panels = refs(clock, "displayPanels")
    require(len(panels) == 6 and all(names[oid].startswith("Platform departure indicator ") for oid in panels),
            "Departure board must show boarding, door closing, return request, ascent, summit and descent")
    atlas = asset_guid(ROOT / "Transit/Platform lettering.png")
    for row in range(10):
        printed = ROOT / "Transit" / f"Lettering {row}.mat"
        require(asset_guid(printed) in yaml and atlas in read(printed), "Missing opaque express sign face " + str(row))
    teams = [b for b in proxies.values() if guid(value(b, "m_Script")) == seat_guid]
    require(len(teams) == 6, "Expected six interactive VRCStations")
    encoded = []
    for behaviour in teams:
        require(file_id(value(behaviour, "service")) == clock_id, "A seat uses the wrong clock")
        require(file_id(value(behaviour, "approach")) == approach_id, "A seat is not protected from the pull field")
        require(trpath(file_id(value(behaviour, "apertureExit"))).endswith("/Aperture overlook/Aperture safe exit"),
                "Missing optional safe summit exit")
        station = proxies[file_id(value(behaviour, "seat"))]
        require(path(station) == path(behaviour), "Seat and use target must share a moving transform")
        require(value(station, "seated") == "1" and value(station, "PlayerMobility") == "2", "Station cannot carry its passenger")
        require(value(station, "disableStationExit") == "0", "Passengers must be able to exit")
        pivot = trpath(file_id(value(station, "stationEnterPlayerLocation")))
        landing = trpath(file_id(value(station, "stationExitPlayerLocation")))
        require(pivot.startswith(path(station) + "/"), "Seat stick point is detached from its coach")
        require("/Platform 01/Coach " in landing and landing.endswith(" exit"), "Release point must remain on the stationary platform")
        backing = proxies[file_id(value(behaviour, "_udonSharpBackingUdonBehaviour"))]
        require(value(backing, "interactText") == "Sit and ride", "Seat interaction is not installed")
        encoded.append((path(station), pivot, landing))
    require(Counter(p.rsplit("/", 1)[0] for p, _, _ in encoded) == {p: 2 for p in crew}, "Each coach needs two seats")
    for p in crew:
        for suffix in ["Door forward", "Door aft", "Car floor"]:
            require(any(trpath(tr) == p + "/" + suffix for tr in transforms), f"Missing {suffix} on {p}")
    return sorted(encoded), tuple(value(docs[file_id(value(approach, name))][1], "m_LocalPosition")
                                  for name in ("approachStart", "sink", "safeDeck"))


def motion(edition):
    clip = read(ROOT / "Generated" / edition / "SpiralService.anim")
    positions = clip.split("  m_PositionCurves:\n", 1)[1].split("  m_ScaleCurves:", 1)[0]
    tracks = {}
    for group in positions.split("  - curve:\n")[1:]:
        label = re.search(r"^    path: (.+)$", group, re.M)[1]
        keys = re.findall(r"time: ([-+eE.\d]+)\n        value: \{x: ([-+eE.\d]+), y: ([-+eE.\d]+), z: ([-+eE.\d]+)\}", group)
        tracks[label] = {float(t): tuple(float(v) for v in (x, y, z)) for t, x, y, z in keys}
    require(len(tracks) == 9, "Three coach and six sliding-door motions are required")
    for i in range(1, 4):
        name = f"Coach {i}"
        keys = tracks[name]
        ground = keys[0]
        require(ground[1] < .15 and ground == keys[320] and ground == keys[36], "Ground dwell/return missing")
        require(keys[166][1] > 220 and keys[166][2] > 120 and keys[166] == keys[196], "Extended summit layover missing")
        require(keys[36][1] < keys[101][1] < keys[166][1], "Outbound trip must climb")
        require(keys[196][1] > keys[255][1] > keys[314][1], "Inbound trip must descend")
        approach = tuple(b-a for a,b in zip(keys[140], keys[150]))
        along = math.sqrt(sum(v*v for v in approach))
        require(along > 1 and sum(a*b for a,b in zip(approach,(0,.743144825,.669130606)))/along > .999,
                "Final train approach must point at the black-hole sky pole")
        for leaf, direction in [("Door aft", -1), ("Door forward", 1)]:
            door = tracks[f"{name}/{leaf}"]
            require(math.isclose(door[0][2], direction*1.49, abs_tol=.002), "Doorway obstructed at platform")
            require(math.isclose(door[35][2], direction*.49, abs_tol=.002), "Door remains open in motion")
            require(door[0] == door[320], "Door must reopen for next passengers")
    return hashlib.sha256(positions.encode()).hexdigest()


if __name__ == "__main__":
    require(scene("PC") == scene("Quest"), "Seat and dock paths differ across platforms")
    require(motion("PC") == motion("Quest"), "Physical rolling stock follows different trajectories across platforms")
    print("Aperture Express: 3 level-boarding coaches, 6 protected VRCStations, extended pole-aligned route, safe overlook, local pull/audio/sky bindings, and matching timetable on both platforms.")
