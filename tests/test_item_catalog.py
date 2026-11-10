"""Golden snapshots of the item catalog: ids, classifications and counts."""

import hashlib
import importlib.util
import json
import unittest
from unittest.mock import patch

from BaseClasses import MultiWorld
from worlds.until_you_fall import UntilYouFallWorld, catalogs, items
from worlds.until_you_fall.items import UntilYouFallItem, filler_counts
from worlds.until_you_fall.locations import UntilYouFallLocation


class ItemCatalogCompatibilityTests(unittest.TestCase):
    def test_ap_classes_share_the_manifest_game_name(self):
        self.assertEqual(catalogs.GAME_NAME, "Until You Fall")
        for game_type in (UntilYouFallWorld, UntilYouFallLocation, UntilYouFallItem):
            with self.subTest(game_type=game_type.__name__):
                self.assertEqual(game_type.game, catalogs.GAME_NAME)

    def test_item_ids_and_classifications_match_existing_catalog(self):
        world = UntilYouFallWorld(MultiWorld(1), 1)
        catalog = sorted(
            (name, code, int(world.create_item(name).classification)) for name, code in world.item_name_to_id.items()
        )
        self.assertEqual(len(catalog), 125)
        self.assertEqual(len({code for _name, code, _classification in catalog}), len(catalog))
        self.assertEqual(
            hashlib.sha256(json.dumps(catalog).encode()).hexdigest(),
            "aff0c348f765bb38a693d9d328273392b798275e486d88d339f566407c0867b1",
        )

    def test_fixed_item_needs_no_second_category_registration(self):
        entry = {
            "Key": "CatalogFixture",
            "Id": 919999,
            "Name": "Uncategorized catalog fixture",
            "Classification": "useful",
        }
        spec = importlib.util.spec_from_file_location("worlds.until_you_fall._items_fixture", items.__file__)
        module = importlib.util.module_from_spec(spec)
        with patch.object(catalogs, "FIXED_ITEMS", [*catalogs.FIXED_ITEMS, entry]):
            spec.loader.exec_module(module)
        self.assertIn(entry["Name"], module.item_table)
        self.assertEqual(module.item_table[entry["Name"]].code, entry["Id"])
        self.assertEqual(module.item_table[entry["Name"]].classification.name, entry["Classification"])

    def test_consumers_share_the_loaded_catalog_objects(self):
        cases = (
            ("items", "WEAPONS"),
            ("items", "STARTING_TRAITS"),
            ("stage_weapon_locations", "WEAPONS"),
            ("enemy_locations", "ENEMIES"),
            ("quest_locations", "QUESTS"),
        )
        for module_name, catalog_name in cases:
            with self.subTest(module=module_name, catalog=catalog_name):
                module = importlib.import_module(f"worlds.until_you_fall.{module_name}")
                self.assertIs(getattr(module, catalog_name), getattr(catalogs, catalog_name))

    def test_filler_counts_and_order_match_existing_rounding(self):
        allocations = [filler_counts(slots) for slots in range(1000)]
        self.assertEqual(
            hashlib.sha256(json.dumps(allocations).encode()).hexdigest(),
            "2d99e3989fed35c5bb7254a3b04b5e1db21689bc424811906576bf0038e7b20a",
        )
