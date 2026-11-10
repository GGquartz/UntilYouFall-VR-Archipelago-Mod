"""Named protocol definitions must agree with the shared catalogs, not just contain valid IDs."""

import hashlib
import json
import pathlib
import re
import unittest

from BaseClasses import MultiWorld
from worlds.until_you_fall import UntilYouFallWorld, options
from worlds.until_you_fall.catalogs import DIFFICULTIES, FIXED_ITEMS, LOCATIONS, OPTION_RANGES
from worlds.until_you_fall.items import item_table

MOD = pathlib.Path(__file__).resolve().parents[1] / "src" / "mod"


def mod_text(relative: str) -> str:
    return (MOD / relative).read_text(encoding="utf-8")


class ModApWorldContractTests(unittest.TestCase):
    def test_every_mod_item_id_exists_in_the_apworld(self):
        world_ids = set(UntilYouFallWorld(MultiWorld(1), 1).item_name_to_id.values())
        mod_ids = dict(re.findall(r"const long (\w+) = (\d+);", mod_text("Archipelago/ApItems.Generated.cs")))
        self.assertEqual(mod_ids, {item["Key"] + "Id": str(item["Id"]) for item in FIXED_ITEMS})
        self.assertTrue(set(map(int, mod_ids.values())).issubset(world_ids))

    def test_fixed_item_names_ids_and_classifications_match_the_catalog(self):
        for item in FIXED_ITEMS:
            with self.subTest(item=item["Name"]):
                data = item_table[item["Name"]]
                self.assertEqual(data.code, item["Id"])
                self.assertEqual(data.classification.name, item["Classification"])

    def test_location_id_bases_match(self):
        constants = dict(re.findall(r"const int (\w+) = (\d+);", mod_text("Checks/LocationDefinitions.Generated.cs")))
        self.assertEqual(constants, {key: str(value) for key, value in LOCATIONS.items() if isinstance(value, int)})

    def test_shop_prices_match(self):
        prices = re.search(r"ShopPrices = \{([^}]*)\}", mod_text("Checks/LocationDefinitions.Generated.cs")).group(1)
        self.assertEqual([int(price) for price in prices.split(",")], LOCATIONS["ShopPrices"])

    def test_difficulty_ids_stay_consecutive_for_existing_receipts(self):
        first_id = DIFFICULTIES[0]["Id"]
        self.assertEqual(
            [difficulty["Id"] for difficulty in DIFFICULTIES], list(range(first_id, first_id + len(DIFFICULTIES)))
        )

    def test_option_ranges_match_the_shared_definitions(self):
        constants = dict(re.findall(r"const int (\w+) = (\d+);", mod_text("Archipelago/OptionRanges.Generated.cs")))
        for name, limits in OPTION_RANGES.items():
            with self.subTest(option=name):
                option = getattr(options, name)
                self.assertEqual(
                    (option.range_start, option.range_end, option.default),
                    (limits["Minimum"], limits["Maximum"], limits["Default"]),
                )
                for field, value in limits.items():
                    self.assertEqual(constants[name + field], str(value))

    def test_location_names_ids_and_order_match_the_existing_catalog(self):
        catalog = list(UntilYouFallWorld.location_name_to_id.items())
        self.assertEqual(len(catalog), 543)
        self.assertEqual(len({code for _name, code in catalog}), len(catalog))
        self.assertEqual(
            hashlib.sha256(json.dumps(catalog).encode()).hexdigest(),
            "9ea98265d0984220bf8313a24f1b9d2cf2836f0b409387ca926025423ad00ba4",
        )


if __name__ == "__main__":
    unittest.main()
