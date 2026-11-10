"""Catalog ownership and generated C# definitions must not drift apart."""

import json
import re
import runpy
import subprocess
import sys
import unittest
from collections import defaultdict
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
WORLD = ROOT / "src/apworld/until_you_fall"


class SharedCatalogTests(unittest.TestCase):
    def test_game_identity_is_exported_from_apworld_manifest(self):
        exporter = runpy.run_path(str(ROOT / "dev/export-game-data.py"))
        metadata = json.loads((WORLD / "archipelago.json").read_text())
        self.assertEqual(metadata["game"], "Until You Fall")
        render = exporter["game_identity_constants"]
        with patch.dict(render.__globals__, {"load": lambda name: {"game": "Manifest game fixture"}}):
            self.assertIn('public const string Name = "Manifest game fixture";', render())

    def test_fixed_items_have_a_shared_catalog(self):
        path = WORLD / "items.json"
        self.assertTrue(path.is_file(), "Fixed item definitions must live in items.json")
        items = json.loads(path.read_text())["Items"]
        self.assertEqual(len(items), len({item["Key"] for item in items}))
        self.assertEqual(len(items), len({item["Id"] for item in items}))
        self.assertEqual(len(items), len({item["Name"] for item in items}))

    def test_generated_definitions_are_current(self):
        script = ROOT / "dev/export-game-data.py"
        self.assertTrue(script.is_file(), "C# constants must be exported from the shared catalogs")
        result = subprocess.run([sys.executable, str(script), "--check"], cwd=ROOT, capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_swapping_valid_source_ids_makes_the_export_stale(self):
        exporter = runpy.run_path(str(ROOT / "dev/export-game-data.py"))
        load = exporter["load"]
        items = load("items.json")
        first, second = items["Items"][:2]
        first["Id"], second["Id"] = second["Id"], first["Id"]
        with patch.dict(
            exporter["main"].__globals__, {"load": lambda name: items if name == "items.json" else load(name)}
        ):
            with patch.object(sys, "argv", ["export-game-data.py", "--check"]):
                with self.assertRaisesRegex(SystemExit, "Stale catalog exports"):
                    exporter["main"]()

    def test_shared_option_ranges_have_a_catalog(self):
        path = WORLD / "option_ranges.json"
        self.assertTrue(path.is_file(), "YAML bounds and client-side validation must share their definitions")
        for name, limits in json.loads(path.read_text()).items():
            with self.subTest(option=name):
                self.assertLessEqual(limits["Minimum"], limits["Default"])
                self.assertLessEqual(limits["Default"], limits["Maximum"])

    def test_protocol_ids_are_not_redefined_in_source(self):
        duplicates = []
        for path in (ROOT / "src").rglob("*"):
            if path.suffix not in (".cs", ".py") or {"bin", "obj"}.intersection(path.parts):
                continue
            if path.name.endswith(".Generated.cs"):
                continue
            for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
                if re.search(r"(?<![\w.])9[12]\d{4}(?:[uU]?[lL]|[lL][uU]?)?(?![\w.])", line):
                    duplicates.append(f"{path.relative_to(ROOT)}:{line_number}: {line.strip()}")
        self.assertEqual(duplicates, [], "Use catalog references instead of literal IDs:\n" + "\n".join(duplicates))

    def test_native_guids_have_one_definition(self):
        definitions = defaultdict(list)
        for path in (ROOT / "src").rglob("*"):
            if path.suffix not in (".cs", ".py", ".json") or {"bin", "obj"}.intersection(path.parts):
                continue
            for value in re.findall(r"[\"']([a-f0-9]{32})[\"']", path.read_text(encoding="utf-8"), re.IGNORECASE):
                definitions[value.lower()].append(str(path.relative_to(ROOT)))
        duplicates = {guid: paths for guid, paths in definitions.items() if len(paths) > 1}
        self.assertEqual(duplicates, {}, "Reference the existing GUID definition instead of copying its value")

    def test_shared_weapon_abilities_keep_the_same_name_and_id(self):
        weapons = json.loads((WORLD / "weapons.json").read_text())
        path = WORLD / "weapon_abilities.json"
        self.assertTrue(path.is_file(), "Shared ability IDs belong in weapon_abilities.json, not in every weapon")
        abilities = json.loads(path.read_text())
        self.assertEqual(len(abilities), len({ability["Id"] for ability in abilities}))
        self.assertEqual(len(abilities), len({ability["Name"] for ability in abilities}))
        names = {ability["Name"] for ability in abilities}
        for weapon in weapons:
            self.assertIn(weapon["Super"], names)
            self.assertNotIn("SuperItemId", weapon, "The weapon references the ability instead of redefining its ID")


if __name__ == "__main__":
    unittest.main()
