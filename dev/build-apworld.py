import argparse
import json
import subprocess
import sys
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--ap-source", type=Path, required=True)
args = parser.parse_args()
sys.path.insert(0, str(args.ap_source.resolve()))

from Utils import tuplize_version
from worlds.Files import APWorldContainer

root = Path(__file__).resolve().parents[1]
subprocess.run([sys.executable, str(root / "dev/export-game-data.py"), "--check"], check=True)
source = root / "src/apworld/until_you_fall"
output = root / "dist/until_you_fall.apworld"
metadata = json.loads((source / "archipelago.json").read_text())


class UntilYouFallContainer(APWorldContainer):
    manifest_path = "until_you_fall/archipelago.json"
    game = metadata["game"]
    world_version = tuplize_version(metadata["world_version"])
    minimum_ap_version = tuplize_version(metadata["minimum_ap_version"])

    def get_manifest(self):
        manifest = super().get_manifest()
        manifest["authors"] = metadata["authors"]
        return manifest

    def write_contents(self, archive):
        super().write_contents(archive)
        for path in sorted(
            path for path in source.rglob("*") if path.suffix in (".py", ".json") and path.name != "archipelago.json"
        ):
            archive.write(path, "until_you_fall/" + path.relative_to(source).as_posix())
        archive.write(root / "docs/SETUP.md", "until_you_fall/docs/setup_en.md")
        archive.write(root / "LICENSE", "until_you_fall/LICENSE")


output.parent.mkdir(parents=True, exist_ok=True)
UntilYouFallContainer(str(output)).write()
print(output)
