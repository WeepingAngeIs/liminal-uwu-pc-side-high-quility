"""Install the exact official VPM packages named by the project's lock, on clean CI runners."""
import hashlib
import json
from pathlib import Path, PurePosixPath
from shutil import copytree
import tempfile
from urllib.request import Request, urlopen
from zipfile import ZipFile

PACKAGES = Path("Packages")
INDEX = "https://vrchat.github.io/packages/index.json"
# Pin the official payloads as well as their versions.
HASHES = {
    ("com.vrchat.base", "3.10.6"): "c1f891298905e80c5b0c4bcd4f8e48303bf27ec04a0826256b7b532884c9f9c9",
    ("com.vrchat.worlds", "3.10.6"): "2792eb597356cdf3fa7263bf3d089a1f516f04544e4244ed180c7879ae90576b",
    ("com.vrchat.core.vpm-resolver", "0.1.29"): "81d3ab96bfa4f7a8efd6699f51400ae69a1dea56cddbe623d3e28500f075bcb9",
}


def fetch(url):
    return urlopen(Request(url, headers={"User-Agent": "Stillwater-world-CI"}), timeout=120)


def restore():
    lock = json.loads((PACKAGES / "vpm-manifest.json").read_text())["locked"]
    with fetch(INDEX) as response:
        official = json.load(response)["packages"]
    for name, data in lock.items():
        version = data["version"]
        expected = HASHES[name, version]
        entry = official[name]["versions"][version]
        if entry.get("zipSHA256", "").lower() != expected:
            raise ValueError(f"Official digest changed for {name} {version}")
        with tempfile.TemporaryDirectory(dir=PACKAGES) as tmp:
            archive = Path(tmp) / "package.zip"
            digest = hashlib.sha256()
            with fetch(entry["url"]) as stream, archive.open("wb") as target:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
                    target.write(chunk)
            if digest.hexdigest() != expected:
                raise ValueError(f"Unexpected archive digest for {name}")
            extracted = Path(tmp) / "unpacked"
            with ZipFile(archive) as package:
                for item in package.infolist():
                    path = PurePosixPath(item.filename.replace("\\", "/"))
                    if path.is_absolute() or ".." in path.parts or ":" in item.filename:
                        raise ValueError(f"Invalid archive path in {name}")
                package.extractall(extracted)
            info = json.loads((extracted / "package.json").read_text(encoding="utf-8-sig"))
            if (info["name"], info["version"]) != (name, version):
                raise ValueError(f"Package identity mismatch for {name}")
            destination = PACKAGES / name
            if destination.exists():
                raise FileExistsError(f"{destination} exists. Restore into a clean checkout.")
            extracted.rename(destination)
            print(f"Restored {name} {version} ({expected[:12]})")
    # The SDK installs these scripts on first editor startup. In a single
    # batch-mode invocation their MonoScripts otherwise precede the reload
    # of Assembly-CSharp, which prevents UdonSharp from resolving the types.
    utilities = PACKAGES / "com.vrchat.worlds/Integrations/UdonSharp/Samples~/Utilities"
    copytree(utilities, Path("Assets/UdonSharp/UtilityScripts"))


if __name__ == "__main__":
    restore()
