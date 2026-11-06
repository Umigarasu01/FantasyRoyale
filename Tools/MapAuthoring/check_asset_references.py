"""Check that the gameplay data and the forest scene refer only to assets that exist in this project.

Usage:
    python check_asset_references.py

Unity shows a broken reference as an empty field, so a wrong script GUID only appears as gear that
silently is not drawn. This finds it without opening Unity. Run it after copying files from another
project. References into Packages are not listed in Assets, so they are ignored by EXTERNAL.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
CHECKED = ("Data/Gameplay", "ForestMapPreview")
SKIPPED_FOLDERS = ("_Recovery",)
EXTERNAL = {"0000000000000000f000000000000000", "0000000000000000e000000000000000"}
META_GUID = re.compile(r"^guid: (\w+)", re.M)
REFERENCE = re.compile(r"guid: ([0-9a-f]{32})")


def project_guids():
    found = set()
    for root, folders, files in os.walk(ASSETS):
        folders[:] = [folder for folder in folders if folder not in SKIPPED_FOLDERS]
        for file in files:
            if file.endswith(".meta"):
                with open(os.path.join(root, file), encoding="utf-8", errors="ignore") as meta:
                    match = META_GUID.search(meta.read(256))
                if match:
                    found.add(match.group(1))
    return found


def main():
    known = project_guids() | EXTERNAL
    checked = 0
    broken = 0
    for folder in CHECKED:
        for root, _, files in os.walk(os.path.join(ASSETS, folder)):
            for file in files:
                if not file.endswith((".asset", ".unity")):
                    continue
                path = os.path.join(root, file)
                with open(path, encoding="utf-8", errors="ignore") as asset:
                    missing = sorted(set(REFERENCE.findall(asset.read())) - known)
                checked += 1
                if missing:
                    broken += 1
                    print(f"BROKEN {os.path.relpath(path, ROOT)}: {', '.join(missing[:3])}")
    print(f"checked {checked} files, {broken} with broken references")
    sys.exit(1 if broken else 0)


if __name__ == "__main__":
    main()
