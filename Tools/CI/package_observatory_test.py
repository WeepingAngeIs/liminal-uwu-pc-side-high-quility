"""Authenticate a reviewed, local-only SDK export before putting it in the CI artifact."""
import base64
import hashlib
import json
import os
from pathlib import Path

from nacl.secret import SecretBox


def main():
    output = Path("Builds/ObservatoryReview")
    review = json.loads((output / "report.json").read_text())
    runtime = json.loads((output / "runtime.json").read_text())
    revision = os.environ["GITHUB_SHA"]
    if review["revision"] != revision or runtime["revision"] != revision or runtime["passed"] is not True:
        raise ValueError("Prototype review did not pass at the requested revision")

    photographs = {entry["path"]: entry["sha256"] for entry in review["images"]}
    expected = {f"{platform}/{view}.png" for platform in ("PC", "Quest")
                for view in ("listening-bench", "rain-exchange", "ceiling-clock", "service-dial")}
    if set(photographs) != expected:
        raise ValueError("Missing prototype photographs")
    photographs["PC/unlocked-receiver.png"] = runtime["unlockedBenchPhotoSha256"]
    for name, checksum in photographs.items():
        if hashlib.sha256((output / name).read_bytes()).hexdigest() != checksum:
            raise ValueError(f"Unexpected image checksum for {name}")

    bundle = Path("Builds/ObservatoryPrivate/observatory-pc.vrcw").read_bytes()
    size = json.loads(Path("Builds/ObservatoryPrivate/size-report.json").read_text())
    if not bundle.startswith(b"UnityFS\0") or size["platform"] != "StandaloneWindows64" or size["downloadBytes"] != len(bundle):
        raise ValueError("Unexpected Windows SDK export")
    key = base64.b64decode(os.environ["LIMINAL_PERSONAL_ASSET_KEY"], validate=True)
    target = output / "observatory-pc.vrcw.enc"
    ciphertext = bytes(SecretBox(key).encrypt(bundle))
    target.write_bytes(ciphertext)
    metadata = {"revision": revision, "platform": size["platform"], "downloadBytes": len(bundle),
                "sha256": hashlib.sha256(bundle).hexdigest(), "cipher": "libsodium secretbox / nonce-prefixed",
                "ciphertextSha256": hashlib.sha256(ciphertext).hexdigest(), "photographs": photographs}
    (output / "bundle.json").write_text(json.dumps(metadata, indent=2) + "\n")


if __name__ == "__main__":
    main()
