"""Check the actual serialized progression, warp landings and PC/Quest parity."""
import json
from collections import defaultdict

from validate_release import Scene, rows, vectors, vector
from validate_world import ROOT, ALL_ROOMS, file_id, guid, read, require, value


EXPECTED = {
    ("hub", "gallery"), ("gallery", "motor"), ("motor", "gravity"),
    ("hub", "service"), ("service", "gravity"), ("gravity", "grove"),
    ("grove", "air"), ("air", "billboard"), ("billboard", "returns"),
    ("returns", "stacks"), ("stacks", "concourse"), ("concourse", "causeway"),
    ("causeway", "waterfall"), ("waterfall", "observatory"), ("observatory", "rail"),
}


def graph(edges):
    require(set(edges) == EXPECTED and len(edges) == len(EXPECTED), "Unexpected or duplicate progression edge")
    following = defaultdict(list)
    for source, target in edges:
        following[source].append(target)
    require(not following["rail"], "The train must be the finale")
    require(len(following["hub"]) == 2 and all(len(following[r]) == 1 for r in ALL_ROOMS - {"hub", "rail"}),
            "The adventure should branch only at its first choice")

    def visit(room, active):
        require(room not in active, "Progression contains a cycle before the finale")
        if room == "rail":
            return {room}
        require(following[room], "A progression route dead-ends at " + room)
        found = {room}
        for target in following[room]:
            found |= visit(target, active | {room})
        return found

    require(visit("hub", set()) == ALL_ROOMS, "Not every room belongs to the adventure")
    for room in ALL_ROOMS:
        require("rail" in visit(room, set()), "Room cannot reach the train: " + room)


def scene(edition):
    s = Scene(edition)
    journey = s.script("AdventureJourney")
    entrances, destinations = rows(journey, "entrances"), rows(journey, "destinations")
    extents = vectors(journey, "halfExtents")
    sources = [r.strip('"') for r in rows(journey, "sourceRooms")]
    targets = [r.strip('"') for r in rows(journey, "destinationRooms")]
    require(len(entrances) == len(destinations) == len(extents) == len(sources) == len(targets) == 15,
            "Every progression door needs matching volume, landing and room bindings")
    graph(list(zip(sources, targets)))
    records = []
    for source, target, entrance, destination, half in zip(sources, targets, entrances, destinations, extents):
        source_path, target_path = s.reference_path(entrance), s.reference_path(destination)
        require(source_path[:2] == ("liminal uwu", source) and target_path[:2] == ("liminal uwu", target),
                "Door/landing is parented to the wrong district")
        require(all(v > 0 for v in half) and half[0] >= 1.5 and half[1] >= 2,
                "Portal activation volume is too small for ordinary walking")
        landing = s.resolve(destination, 4)
        require(vector(value(landing, "m_LocalPosition"))[1] >= 0, "Portal landing is below its floor")
        records.append((source, target, source_path, target_path, half,
                        value(s.resolve(entrance, 4), "m_LocalPosition"),
                        value(s.resolve(entrance, 4), "m_LocalRotation"),
                        value(landing, "m_LocalPosition"), value(landing, "m_LocalRotation")))
    require(not any("Request an unknown platform" in p for p in s.by_path),
            "Legacy random-departure shortcut bypasses progression")
    seals = [body for body in s.by_id[65].values()
             if any(part.startswith("Closed interchange ") for part in s.paths[file_id(value(body, "m_GameObject"))])]
    require(len(seals) == 36 and all(value(b, "m_Enabled") == "1" and value(b, "m_IsTrigger") == "0"
                                   and vector(value(b, "m_Size"))[0] >= 9 for b in seals),
            "Every old passage mouth requires a solid full-width shutter")
    waterfall = s.at(("waterfall",))
    require(waterfall and s.position(("waterfall",)) == (640, 0, 200), "Waterfall district missing")
    require(value(s.resolve(value(journey, "_udonSharpBackingUdonBehaviour"), 114), "programSource"),
            "Progression has no compiled Udon program")
    return sorted(records)


if __name__ == "__main__":
    require(scene("PC") == scene("Quest"), "Progression geometry or destinations differ across platforms")
    print(json.dumps({"adventure_rooms": 15, "walk_through_doors": 15,
                      "starting_branches": 2, "finale": "rail", "pc_quest_match": True}, indent=2))
