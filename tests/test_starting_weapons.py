"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

from unittest.mock import patch

from BaseClasses import MultiWorld
from Fill import distribute_items_restrictive
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.catalogs import (
    WEAPONS,
)
from worlds.until_you_fall.options import (
    SecondStartingWeapon,
)
from worlds.until_you_fall.stage_weapon_locations import location_name


class TestStartingWeapons(UntilYouFallWorldTest):
    def test_explicit_starter_is_the_only_precollected_weapon(self):
        for key, name in (
            ("engraved_arming_sword", "Engraved Arming Sword"),
            ("heartsplitter", "Heartsplitter"),
            ("ritual_dagger", "Ritual Dagger"),
            ("high_arcanists_blade", "High Arcanist's Blade"),
            ("fates_end", "Fate's End"),
            ("cold_iron_greataxe", "Cold Iron Greataxe"),
            ("captains_warhammer", "Captain's Warhammer"),
            ("mistcutter", "Mistcutter"),
            ("gemini_stranded", "Gemini Stranded"),
            ("gemini_esper", "Gemini Esper"),
            ("vestals_wrath", "Vestal's Wrath"),
            ("rokari_keenblade", "Rokari Keenblade"),
            ("duelists_edge", "Duelist's Edge"),
        ):
            with self.subTest(weapon=key):
                self.options = {"starting_weapon": key, "starting_hands": "both"}
                self.world_setup(seed=12345)
                starting_weapons = [
                    item.name
                    for item in self.multiworld.precollected_items[self.player]
                    if item.name in {weapon["Name"] for weapon in WEAPONS}
                ]
                self.assertEqual(starting_weapons, [name])
                self.assertNotIn(name, [item.name for item in self.multiworld.itempool])
                self.assertEqual(self.world.fill_slot_data()["starting_weapons"], [name])

    def test_two_starting_weapons_are_ordered_owned_and_removed_from_pool(self):
        self.assertEqual(SecondStartingWeapon.default, SecondStartingWeapon.option_none)
        for hands in ("both", "left_only", "right_only"):
            with self.subTest(hands=hands):
                self.options = {
                    "starting_weapon": "ritual_dagger",
                    "second_starting_weapon": "cold_iron_greataxe",
                    "starting_hands": hands,
                }
                self.world_setup(seed=17)
                names = ["Ritual Dagger", "Cold Iron Greataxe"]
                self.assertEqual(self.world.fill_slot_data()["starting_weapons"], names)
                precollected = [item.name for item in self.multiworld.precollected_items[self.player]]
                self.assertEqual(precollected[:2], names)
                for name in names:
                    self.assertEqual(precollected.count(name), 1)
                    self.assertFalse(any(item.name == name for item in self.multiworld.itempool))
                self.assertEqual(len(self.multiworld.itempool), 417)
                self.assertTrue(
                    self.multiworld.get_location(location_name(1, names[0]), self.player).can_reach(
                        self.multiworld.state
                    )
                )
                self.assertEqual(
                    self.multiworld.get_location(location_name(1, names[1]), self.player).can_reach(
                        self.multiworld.state
                    ),
                    hands == "both",
                )

    def test_second_starter_allows_charm_and_crest_but_first_does_not(self):
        for name in ("spellswords_charm", "knight_blooded_crest"):
            self.options = {
                "starting_weapon": "ritual_dagger",
                "second_starting_weapon": name,
                "starting_hands": "both",
            }
            self.world_setup(seed=3)
            self.assertEqual(len(self.world.fill_slot_data()["starting_weapons"]), 2)
        seen = set()
        for seed in range(80):
            self.options = {"starting_weapon": "random", "second_starting_weapon": "random", "starting_hands": "both"}
            self.world_setup(seed=seed)
            first, second = self.world.fill_slot_data()["starting_weapons"]
            self.assertNotIn(first, ("Spellsword's Charm", "Knight-Blooded Crest"))
            seen.add(second)
        self.assertTrue({"Spellsword's Charm", "Knight-Blooded Crest"} <= seen)

    def test_duplicate_starting_weapons_are_rejected(self):
        self.options = {"starting_weapon": "mistcutter", "second_starting_weapon": "mistcutter"}
        with self.assertRaisesRegex(ValueError, "different"):
            self.world_setup(seed=17)

    def test_two_starters_allow_complete_fill_with_quests_and_all_hand_modes(self):
        for hands in ("both", "left_only", "right_only"):
            with self.subTest(hands=hands):
                self.options = {
                    "starting_weapon": "ritual_dagger",
                    "second_starting_weapon": "random",
                    "starting_hands": hands,
                    "quest_board": True,
                    "goal": "quests",
                }
                self.world_setup(seed=17)
                distribute_items_restrictive(self.multiworld)
                self.world.finalize_multiworld()
                self.assertEqual(len(self.multiworld.get_unfilled_locations()), 0)
                self.assertTrue(self.multiworld.can_beat_game())

    def test_random_second_starter_never_duplicates_and_tracker_preserves_order(self):
        for first, second in (("random", "random"), ("random", "ritual_dagger"), ("ritual_dagger", "random")):
            for seed in range(8):
                with self.subTest(first=first, second=second, seed=seed):
                    self.options = {
                        "starting_weapon": first,
                        "second_starting_weapon": second,
                        "starting_hands": "both",
                    }
                    self.world_setup(seed=seed)
                    data = self.world.fill_slot_data()
                    self.assertEqual(len(set(data["starting_weapons"])), 2)
                    self.world_setup(seed=seed)
                    self.assertEqual(self.world.fill_slot_data(), data)
                    self.options = {}
                    with patch.object(MultiWorld, "re_gen_passthrough", {self.world.game: data}, create=True):
                        self.world_setup(seed=seed + 100)
                    self.assertEqual(self.world.fill_slot_data(), data)

    def test_random_start_is_reproducible_and_excludes_passive_weapons(self):
        starts = []
        for seed in range(12):
            self.options = {"starting_weapon": "random"}
            self.world_setup(seed=seed)
            first = [
                item.name
                for item in self.multiworld.precollected_items[self.player]
                if item.name in {weapon["Name"] for weapon in WEAPONS}
            ]
            self.assertEqual(len(first), 1)
            self.assertNotIn(first[0], ("Spellsword's Charm", "Knight-Blooded Crest"))
            starts.append(first[0])
            self.world_setup(seed=seed)
            self.assertEqual(
                [
                    item.name
                    for item in self.multiworld.precollected_items[self.player]
                    if item.name in {weapon["Name"] for weapon in WEAPONS}
                ],
                first,
            )
        self.assertGreater(len(set(starts)), 1)

    def test_single_hand_random_excludes_two_handed_weapons(self):
        for mode in ("left_only", "right_only"):
            for seed in range(20):
                with self.subTest(starting_hands=mode, seed=seed):
                    self.options = {"starting_weapon": "random", "starting_hands": mode}
                    self.world_setup(seed=seed)
                    weapon = self.multiworld.precollected_items[self.player][0]
                    self.assertFalse(next(entry["TwoHanded"] for entry in WEAPONS if entry["Id"] == weapon.code))

    def test_invalid_starting_weapons_are_rejected(self):
        from worlds.until_you_fall.options import StartingWeapon

        for name in ("spellswords_charm", "knight_blooded_crest", "unknown_weapon"):
            with self.subTest(weapon=name), self.assertRaises(KeyError):
                StartingWeapon.from_any(name)
