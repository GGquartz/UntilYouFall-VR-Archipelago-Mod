import json
import unittest
from importlib import import_module
from pathlib import Path

WORLD_DIR = Path(__file__).parents[1] / "src" / "apworld" / "until_you_fall"


def load(name):
    return import_module(f"worlds.until_you_fall.{name}")


class TestStartingLoadoutRules(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        resolve = load("starting_loadout").resolve_starting_weapons
        cls.resolve_starting_weapon = staticmethod(
            lambda first, random, hands, eligible, choose, two_handed: resolve(
                first, random, 0, False, hands, eligible, choose, two_handed
            )[0]
        )
        cls.starting_hand_items = staticmethod(load("hands").starting_hand_items)

    def test_available_hands_are_precollected(self):
        self.assertEqual(self.starting_hand_items(0), ("Left Hand", "Right Hand"))
        self.assertEqual(self.starting_hand_items(1), ("Left Hand",))
        self.assertEqual(self.starting_hand_items(2), ("Right Hand",))

    def test_single_hand_random_uses_only_one_handed_candidates(self):
        candidates = (910012, 910016, 910017, 910018, 910024)
        seen = []

        def choose(values):
            seen.extend(values)
            return values[-1]

        selected = self.resolve_starting_weapon(910016, True, 1, candidates, choose, {910016, 910017, 910018})
        self.assertEqual(selected, 910024)
        self.assertEqual(seen, [910012, 910024])

    def test_explicit_two_handed_starter_requires_both_hands(self):
        for hands in (1, 2):
            with self.subTest(hands=hands), self.assertRaisesRegex(ValueError, "starting_hands: both"):
                self.resolve_starting_weapon(
                    910016, False, hands, (910012, 910016), lambda values: values[0], {910016, 910017, 910018}
                )
        self.assertEqual(
            self.resolve_starting_weapon(
                910016, False, 0, (910012, 910016), lambda values: values[0], {910016, 910017, 910018}
            ),
            910016,
        )


class TestStageWeaponLocations(unittest.TestCase):
    def test_all_stage_weapon_ids_names_and_metadata(self):
        rules = load("stage_weapon_locations")
        weapons = json.loads((WORLD_DIR / "weapons.json").read_text())
        ids = [rules.location_id(stage, weapon["Id"]) for stage in range(1, 22) for weapon in weapons]
        names = [rules.location_name(stage, weapon["Name"]) for stage in range(1, 22) for weapon in weapons]
        self.assertEqual(ids, list(range(911001, 911316)))
        self.assertEqual(len(set(names)), 315)
        self.assertEqual(rules.location_name(7, "Mistcutter"), "Clear Stage 7 - Defeat Boss 1 with Mistcutter")
        self.assertEqual(rules.location_name(14, "Mistcutter"), "Clear Stage 14 - Defeat Boss 2 with Mistcutter")
        self.assertEqual(rules.location_name(21, "Mistcutter"), "Clear Stage 21 - Defeat Final Boss with Mistcutter")
        self.assertEqual(
            {weapon["Name"] for weapon in weapons if weapon["LogicRequiresBothHands"]},
            {
                "Spellsword's Charm",
                "Knight-Blooded Crest",
                "Fate's End",
                "Cold Iron Greataxe",
                "Captain's Warhammer",
            },
        )
        self.assertEqual(
            {weapon["Name"] for weapon in weapons if weapon["TwoHanded"]},
            {
                "Fate's End",
                "Cold Iron Greataxe",
                "Captain's Warhammer",
            },
        )


if __name__ == "__main__":
    unittest.main()
