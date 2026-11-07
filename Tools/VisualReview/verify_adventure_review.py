"""Bind the hosted Unity adventure review to the actual scenes in this checkout.

The former twelve-corridor grid review is retained as historical tooling. Those
corridors are deliberately sealed in the portal adventure. Unity's hosted
review now checks each entrance/landing with physics and the track with raycasts;
ClientSim exercises the actual compiled portal and train behaviours.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "CI"))
from validate_adventure import scene as validate_scene
from validate_world import require


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("route-review"))
    args = parser.parse_args()
    evidence = Path("Tools/CI/Reviews")
    report = json.loads((evidence / "Adventure.json").read_text(encoding="utf-8-sig"))
    require(report.get("authoringSucceeded") is True and report.get("reviewPassed") is True,
            "A successful hosted Unity adventure review is required")
    require(not report.get("errors") and not report.get("failures") and not report.get("shaderMessages"),
            "Hosted review contains errors or shader diagnostics")
    require(report.get("unity") == "2022.3.22f1" and report.get("graphicsApi") == "OpenGLCore",
            "Unexpected review Unity/graphics configuration")
    reviewed = report.get("scenes", [])
    require(len(reviewed) == 2 and {s["edition"] for s in reviewed} == {"PC", "Quest"},
            "Review must contain both platform scenes")
    measurements = []
    for record in reviewed:
        path = Path(f'Assets/Stillwater/Scenes/liminal uwu - {record["edition"]}.unity')
        require(record["scene"] == path.as_posix(), "Review references an unexpected scene")
        # Windows checkout may convert text files to CRLF; Unity on the hosted
        # Linux runner hashes LF. No other normalization or scene edits are allowed.
        actual = hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()
        require(actual == record["sha256"], "Scene changed since hosted physics/image review: " + str(path))
        require(record.get("adventureDoors") == 15 and record.get("trainSeats") == 6,
                "Reviewed scene lacks the required adventure doors or train seats")
        require(record.get("audioZones") == 38 and record.get("visibilityPortals") == 2,
                "Reviewed scene uses stale district isolation")
        require(record.get("personalPlatformLights") == 2 and record.get("carriageAudioBound") is True,
                "Review lacks the two personal platform lights or private carriage sound")
        measurements.append({k: record[k] for k in ("edition", "sha256", "adventureDoors", "colliderCount",
                                                    "trainSeats", "trianglesAllLods", "reflectionCount", "personalPlatformLights", "carriageAudioBound")})
    # Scene hashes alone cannot catch a changed runtime source which has not yet
    # been re-authored. The evidence revision remains an ancestor of this release.
    revision = report["revision"]
    require(len(revision) == 40 and all(c in "0123456789abcdef" for c in revision), "Invalid evidence revision")
    changed = subprocess.check_output(["git", "diff", "--name-only", revision, "--", "Assets/Stillwater"], text=True)
    source_changes = [p for p in changed.splitlines() if Path(p).suffix in (".cs", ".shader", ".cginc")]
    require(not source_changes, "World source changed since hosted review: " + ", ".join(source_changes))
    pc, quest = validate_scene("PC"), validate_scene("Quest")
    require(pc == quest, "PC and Quest adventure bindings differ")
    runtime = (evidence / "Adventure-runtime.txt").read_text(encoding="utf-8-sig")
    checks = [line for line in runtime.splitlines() if "EXPANSION_CLIENTSIM_CHECK " in line]
    passes = [line for line in runtime.splitlines() if "EXPANSION_CLIENTSIM_PASS " in line]
    require(len(checks) >= 30 and len(passes) == 1 and "EXPANSION_CLIENTSIM_FAIL" not in runtime,
            "Hosted ClientSim adventure evidence is incomplete or failed")
    args.output.mkdir(parents=True, exist_ok=True)
    result = {"author_revision": revision, "method": "Serialized graph and scene hashes bound to the hosted Unity physics, image and compiled-Udon review.",
              "runtime_checks": len(checks), "scenes": measurements,
              "routes": [{"from": edge[0], "to": edge[1]} for edge in pc],
              "limitations": "No headset frame-rate, external video URL or multi-client networking measurement."}
    (args.output / "adventure-review.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    (args.output / "hosted-unity-review.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (args.output / "hosted-runtime-checks.txt").write_text(runtime, encoding="utf-8")
    print(f"PC/Quest: 15 matching adventure doors; exact hosted scene hashes; {len(checks)} ClientSim checks passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
