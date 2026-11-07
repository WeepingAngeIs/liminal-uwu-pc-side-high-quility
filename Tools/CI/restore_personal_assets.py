"""Restore owner-licensed assets without publishing their source files.

The authenticated encrypted archive is versioned; its key is a repository secret.
Do not print the key or upload the decrypted Personal directory as an artifact.
"""
import base64
import io
import os
from pathlib import Path, PurePosixPath
import zipfile
from nacl.secret import SecretBox


def restore():
    encoded = os.environ.get("LIMINAL_PERSONAL_ASSET_KEY", "")
    if not encoded:
        raise RuntimeError("The owner-licensed asset key is required for this world build.")
    key = base64.b64decode(encoded, validate=True)
    if len(key) != SecretBox.KEY_SIZE:
        raise ValueError("Invalid asset key length")
    archive = SecretBox(key).decrypt(Path("PrivateAssets/PersonalAssets.bin").read_bytes())
    root = Path.cwd().resolve()
    with zipfile.ZipFile(io.BytesIO(archive)) as zipped:
        names = set()
        for entry in zipped.infolist():
            path = PurePosixPath(entry.filename)
            if (path.is_absolute() or ".." in path.parts or ":" in entry.filename or "\\" in entry.filename
                or not entry.filename.startswith("Assets/Stillwater/Personal/") or entry.filename in names):
                raise ValueError("Invalid private asset path")
            names.add(entry.filename)
            if entry.is_dir():
                continue
            if entry.file_size > 100_000_000:
                raise ValueError("Unexpected asset size")
            target = root.joinpath(*path.parts)
            if not target.resolve().is_relative_to(root):
                raise ValueError("Private asset destination escaped workspace")
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(zipped.read(entry))
    print("Restored owner-licensed assets for this build; raw sources remain untracked.")


if __name__ == "__main__":
    restore()
