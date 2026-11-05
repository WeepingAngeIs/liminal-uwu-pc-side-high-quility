"""Measure a finished VRChat world bundle against the installed SDK's upload limits."""
import argparse
import json
import os
from pathlib import Path


def size_text(value):
    return f"{value:,} bytes ({value / 1024**2:.2f} MiB)"


def check(edition):
    directory = Path("Builds") / edition
    bundle = directory / "liminal-uwu.vrcw"
    report = json.loads((directory / "size-report.json").read_text(encoding="utf-8-sig"))
    expected_target = {"PC": "StandaloneWindows64", "Quest": "Android"}[edition]
    if report["platform"] != expected_target:
        raise ValueError(f"Expected a {expected_target} SDK report for {edition}")
    with bundle.open("rb") as contents:
        if contents.read(8) != b"UnityFS\x00":
            raise ValueError(f"{bundle} has no UnityFS header")
    download = bundle.stat().st_size
    if download < 1024 or download != report["downloadBytes"]:
        raise ValueError("The exported bundle and its Unity build report do not agree")

    # A missing mobile limit must not silently disable the purpose of the Quest job.
    if edition == "Quest" and report["downloadLimitBytes"] <= 0:
        raise ValueError("The SDK reported no mobile world download limit")
    rows = [f"## {edition} world upload size", "", "| Measurement | Size | SDK limit | Margin |",
            "| --- | ---: | ---: | ---: |"]
    over = []
    for label, actual, limit in (
        ("Compressed download", download, report["downloadLimitBytes"]),
        ("Unpacked assets", report["unpackedAssetBytes"], report["unpackedLimitBytes"]),
    ):
        if actual == -1 and limit == -1:
            rows.append(f"| {label} | No Unity measurement | No SDK cap | — |")
            continue
        if actual <= 0:
            raise ValueError(f"Missing {label} measurement for SDK limit {limit}")
        if limit == -1:
            rows.append(f"| {label} | {size_text(actual)} | No SDK cap | — |")
            continue
        if limit <= 0:
            raise ValueError(f"Invalid {label} limit in SDK report")
        margin = limit - actual
        rows.append(f"| {label} | {size_text(actual)} | {size_text(limit)} | {size_text(margin)} |")
        if margin < 0:
            over.append(f"{label} exceeds the limit by {size_text(-margin)}.")
    rows += ["", "**FAIL**" if over else "**PASS**", *over, ""]
    summary = "\n".join(rows)
    print(summary)
    if path := os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(path, "a", encoding="utf-8") as stream:
            stream.write(summary + "\n")
    return not over


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("edition", choices=["PC", "Quest"])
    raise SystemExit(0 if check(parser.parse_args().edition) else 1)
