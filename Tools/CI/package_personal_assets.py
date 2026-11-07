"""Package the owner's licensed files, including private compiled programs.

Only authenticated ciphertext is emitted. The key is supplied by the owner's
existing repository secret and is never written to the artifact.
"""
import argparse
import base64
import io
import os
from pathlib import Path
import zipfile
from nacl.secret import SecretBox


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=Path('PrivateAssets/PersonalAssets.bin'))
    args = parser.parse_args()
    key = base64.b64decode(os.environ['LIMINAL_PERSONAL_ASSET_KEY'], validate=True)
    if len(key) != SecretBox.KEY_SIZE:
        raise ValueError('Invalid private key length')
    root = Path('Assets/Stillwater/Personal')
    memory = io.BytesIO()
    files = sorted(p for p in root.rglob('*') if p.is_file())
    if not files:
        raise ValueError('No personal assets to package')
    with zipfile.ZipFile(memory, 'w', zipfile.ZIP_DEFLATED, compresslevel=5) as archive:
        for path in files:
            if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()) or path.stat().st_size > 100_000_000:
                raise ValueError('Unexpected private asset path or size')
            info = zipfile.ZipInfo(path.as_posix(), (1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, path.read_bytes())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(bytes(SecretBox(key).encrypt(memory.getvalue())))
    print('Authenticated private asset package saved; file count:', len(files))


if __name__ == '__main__':
    main()
