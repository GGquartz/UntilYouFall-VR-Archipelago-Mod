"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

import unittest
from unittest.mock import patch

from BaseClasses import Item, ItemClassification, Location, Region
from test.general import TestWorld, setup_multiworld
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.hand_placement import collect_location_spheres, enforce_early_hand
from worlds.until_you_fall.options import (
    EarlyHand,
)
from worlds.until_you_fall.stage_weapon_locations import location_name


class TestHands(UntilYouFallWorldTest):
    def test_single_hand_modes_add_only_the_missing_hand(self):
        for option, expected_hand in (("left_only", "Right Hand"), ("right_only", "Left Hand")):
            with self.subTest(starting_hands=option):
                self.options = {"starting_hands": option}
                self.world_setup(seed=12345)
                items = self.multiworld.itempool
                self.assertEqual(len(items), 417)
                self.assertEqual(sum(item.name == "Shard of Vitality" for item in items), 18)
                self.assertEqual(sum(item.name == "100 Aether" for item in items), 123)
                hands = [item for item in items if item.name in ("Left Hand", "Right Hand")]
                self.assertEqual([item.name for item in hands], [expected_hand])
                self.assertEqual(hands[0].classification, ItemClassification.progression)
                available_hand = "Left Hand" if option == "left_only" else "Right Hand"
                self.assertIn(available_hand, [item.name for item in self.multiworld.precollected_items[self.player]])
                self.assertEqual(self.world.fill_slot_data()["starting_hands"], option)

    def test_early_hand_finalizes_after_other_placement_rules(self):
        for hands in ("both", "left_only", "right_only"):
            for option, percent in (
                ("first_25_percent_of_checks", 25),
                ("first_50_percent_of_checks", 50),
                ("unrestricted", 100),
            ):
                with self.subTest(hands=hands, early_hand=option):
                    self.options = {"starting_hands": hands, "early_hand": option}
                    self.world_setup(seed=12345)
                    events = []
                    with (
                        patch(
                            "worlds.until_you_fall.enforce_shop_quality",
                            side_effect=lambda *args: events.append("shop"),
                        ),
                        patch(
                            "worlds.until_you_fall.enforce_stage_reward_pacing",
                            side_effect=lambda *args: events.append("rewards"),
                        ),
                        patch(
                            "worlds.until_you_fall.enforce_early_hand", side_effect=lambda *args: events.append("hand")
                        ) as hand,
                    ):
                        self.world.finalize_multiworld()
                    if hands == "both":
                        hand.assert_not_called()
                        self.assertEqual(events, ["shop", "rewards"])
                    else:
                        missing = "Right Hand" if hands == "left_only" else "Left Hand"
                        hand.assert_called_once_with(self.multiworld, self.player, missing, percent)
                        self.assertEqual(events, ["shop", "rewards", "hand"])

    def test_both_hands_adds_no_hand_item(self):
        self.options = {"starting_hands": "both"}
        self.world_setup(seed=12345)
        self.assertEqual(self.world.fill_slot_data()["starting_hands"], "both")
        self.assertFalse(any(item.name in ("Left Hand", "Right Hand") for item in self.multiworld.itempool))
        self.assertEqual(sum(item.name == "Shard of Vitality" for item in self.multiworld.itempool), 18)
        self.assertEqual(sum(item.name == "100 Aether" for item in self.multiworld.itempool), 123)

    def test_stage_weapon_access_rules(self):
        self.options = {"starting_weapon": "duelists_edge", "starting_hands": "both"}
        self.world_setup(seed=12345)
        state = self.multiworld.state
        self.assertTrue(self.multiworld.get_location(location_name(1, "Duelist's Edge"), self.player).can_reach(state))
        mistcutter = self.multiworld.get_location(location_name(1, "Mistcutter"), self.player)
        self.assertFalse(mistcutter.can_reach(state))
        state.collect(self.world.create_item("Mistcutter"), True)
        self.assertTrue(mistcutter.can_reach(state))

    def test_special_stage_weapon_checks_require_both_hands(self):
        for weapon in (
            "Spellsword's Charm",
            "Knight-Blooded Crest",
            "Fate's End",
            "Cold Iron Greataxe",
            "Captain's Warhammer",
        ):
            with self.subTest(weapon=weapon):
                self.options = {"starting_weapon": "duelists_edge", "starting_hands": "right_only"}
                self.world_setup(seed=12345)
                state = self.multiworld.state
                state.collect(self.world.create_item(weapon), True)
                location = self.multiworld.get_location(location_name(1, weapon), self.player)
                self.assertFalse(location.can_reach(state))
                state.collect(self.world.create_item("Left Hand"), True)
                self.assertTrue(location.can_reach(state))

    def test_one_handed_stage_check_needs_no_second_hand(self):
        self.options = {"starting_weapon": "duelists_edge", "starting_hands": "right_only"}
        self.world_setup(seed=12345)
        state = self.multiworld.state
        state.collect(self.world.create_item("Mistcutter"), True)
        self.assertTrue(self.multiworld.get_location(location_name(1, "Mistcutter"), self.player).can_reach(state))

    def test_explicit_two_handed_starter_requires_both_hands(self):
        for weapon in ("fates_end", "cold_iron_greataxe", "captains_warhammer"):
            for mode in ("left_only", "right_only"):
                with self.subTest(weapon=weapon, starting_hands=mode):
                    self.options = {"starting_weapon": weapon, "starting_hands": mode}
                    with self.assertRaisesRegex(ValueError, "starting_hands: both"):
                        self.world_setup(seed=12345)


class TestHandPlacement(unittest.TestCase):
    def test_option_defaults_to_first_half(self):
        self.assertEqual(EarlyHand.default, 50)
        for name, value in (
            ("first_25_percent_of_checks", 25),
            ("first_50_percent_of_checks", 50),
            ("unrestricted", 100),
        ):
            with self.subTest(name=name):
                self.assertEqual(EarlyHand.from_any(name).value, value)

    def test_quarter_uses_spheres_and_rounds_up(self):
        for count, limit in ((1, 1), (4, 1), (5, 2), (8, 2), (9, 3)):
            with self.subTest(count=count):
                multiworld, _ = self.make_multiworld(count, count)
                enforce_early_hand(multiworld, 1, "Left Hand", 25)
                self.assertLessEqual(self.hand_sphere(multiworld), limit)
                self.assertTrue(multiworld.can_beat_game())

    def test_unrestricted_does_not_compute_spheres_or_move_hand(self):
        multiworld, location = self.make_multiworld(4, 4)
        hand = location.item
        with patch.object(multiworld, "get_sendable_spheres", side_effect=AssertionError("unexpected scan")):
            enforce_early_hand(multiworld, 1, "Left Hand", 100)
        self.assertIs(location.item, hand)
        self.assertEqual(self.hand_sphere(multiworld), 4)

    def test_quarter_preserves_locked_plando(self):
        multiworld, location = self.make_multiworld(4, 4, locked=True)
        hand = location.item
        enforce_early_hand(multiworld, 1, "Left Hand", 25)
        self.assertIs(location.item, hand)
        self.assertEqual(self.hand_sphere(multiworld), 4)

    def test_quarter_rolls_back_unbeatable_swaps(self):
        multiworld, location = self.make_multiworld(8, 8)
        original = {entry: entry.item for entry in multiworld.get_filled_locations()}
        with patch.object(multiworld, "can_beat_game", return_value=False):
            with self.assertRaisesRegex(RuntimeError, "first 25%"):
                enforce_early_hand(multiworld, 1, "Left Hand", 25)
        self.assertEqual({entry: entry.item for entry in multiworld.get_filled_locations()}, original)
        self.assertEqual(location.item.location, location)
        self.assertEqual(self.hand_sphere(multiworld), 8)

    def make_multiworld(self, sphere_count, hand_sphere, locked=False, early_progression_only=False):
        multiworld = setup_multiworld([TestWorld, TestWorld], steps=(), seed=12345)
        menus = [Region("Menu", player, multiworld) for player in (1, 2)]
        multiworld.regions += menus

        hand_location = None
        for sphere in range(1, sphere_count + 1):
            rule = (lambda state: True) if sphere == 1 else (lambda state, gate=f"Gate {sphere}": state.has(gate, 1))
            key_location = Location(2, f"Sphere {sphere} key", 920000 + sphere * 2, menus[1])
            filler_location = Location(2, f"Sphere {sphere} filler", 920001 + sphere * 2, menus[1])
            key_location.access_rule = rule
            filler_location.access_rule = rule
            menus[1].locations += [key_location, filler_location]

            key_name = "Victory" if sphere == sphere_count else f"Gate {sphere + 1}"
            key = Item(key_name, ItemClassification.progression, 930000 + sphere, 1)
            filler_class = (
                ItemClassification.progression
                if early_progression_only and sphere <= (sphere_count + 1) // 2
                else ItemClassification.filler
            )
            filler = Item(f"Filler {sphere}", filler_class, 940000 + sphere, 2)
            key_location.item = key
            key.location = key_location
            filler_location.item = filler
            filler.location = filler_location

            if sphere == hand_sphere:
                hand = Item("Left Hand", ItemClassification.progression, 910003, 1)
                filler_location.item = hand
                hand.location = filler_location
                hand_location = filler_location

        hand_location.locked = locked
        multiworld.completion_condition[1] = lambda state: state.has("Victory", 1)
        multiworld.completion_condition[2] = lambda state: True
        return multiworld, hand_location

    def hand_sphere(self, multiworld, player=1):
        spheres = collect_location_spheres(multiworld)
        return next(
            index
            for index, sphere in enumerate(spheres, 1)
            if any(location.item.name == "Left Hand" and location.item.player == player for location in sphere)
        )

    def test_four_spheres_move_hand_into_first_two(self):
        multiworld, old_location = self.make_multiworld(4, 4)
        enforce_early_hand(multiworld, 1, "Left Hand")
        self.assertLessEqual(self.hand_sphere(multiworld), 2)
        self.assertIsNot(old_location.item, None)
        hand = next(
            location.item
            for location in multiworld.get_filled_locations()
            if location.item.name == "Left Hand" and location.item.player == 1
        )
        self.assertEqual(hand.player, 1)
        self.assertEqual(hand.location.player, 2)
        self.assertTrue(multiworld.can_beat_game())

    def test_five_spheres_allow_hand_in_third(self):
        multiworld, location = self.make_multiworld(5, 3)
        enforce_early_hand(multiworld, 1, "Left Hand")
        self.assertIs(location.item.location, location)
        self.assertEqual(self.hand_sphere(multiworld), 3)
        self.assertTrue(multiworld.can_beat_game())

    def test_retries_other_candidate_after_failed_swap(self):
        multiworld, old_location = self.make_multiworld(4, 4)
        with patch.object(multiworld, "can_beat_game", side_effect=[False, True]) as beatable:
            enforce_early_hand(multiworld, 1, "Left Hand")
            self.assertEqual(beatable.call_count, 2)
        self.assertLessEqual(self.hand_sphere(multiworld), 2)
        self.assertNotEqual(old_location.item.name, "Left Hand")
        self.assertTrue(multiworld.can_beat_game())

    def test_only_moves_the_requested_players_hand_item(self):
        multiworld, _ = self.make_multiworld(4, 4)
        other_location = next(
            location for location in multiworld.get_filled_locations() if location.name == "Sphere 1 filler"
        )
        other_hand = Item("Left Hand", ItemClassification.progression, 910003, 2)
        other_location.item = other_hand
        other_hand.location = other_location
        enforce_early_hand(multiworld, 1, "Left Hand")
        self.assertIs(other_location.item, other_hand)
        self.assertLessEqual(self.hand_sphere(multiworld, 1), 2)

    def test_preserves_locked_plando_hand_item(self):
        multiworld, location = self.make_multiworld(4, 4, locked=True)
        hand = location.item
        enforce_early_hand(multiworld, 1, "Left Hand")
        self.assertIs(location.item, hand)
        self.assertIs(hand.location, location)
        self.assertEqual(self.hand_sphere(multiworld), 4)

    def test_fails_without_early_non_progression_candidate(self):
        multiworld, _ = self.make_multiworld(4, 4, early_progression_only=True)
        with self.assertRaisesRegex(RuntimeError, "non-progression"):
            enforce_early_hand(multiworld, 1, "Left Hand")

    def test_hand_taken_from_shop_slot_is_replaced_by_quality_item(self):
        for seed in range(5):
            multiworld, shop_slot = self.make_multiworld(4, 4)
            shop_slot.name = "Shop Item 1-1"
            useful_location = next(
                location for location in multiworld.get_filled_locations() if location.name == "Sphere 1 filler"
            )
            useful_location.item.classification = ItemClassification.useful
            multiworld.worlds[1].random.seed(seed)
            enforce_early_hand(multiworld, 1, "Left Hand")
            self.assertEqual(shop_slot.item.classification, ItemClassification.useful)
            self.assertEqual(useful_location.item.name, "Left Hand")

    def test_hand_taken_from_shop_slot_fails_without_quality_candidate(self):
        multiworld, shop_slot = self.make_multiworld(4, 4)
        shop_slot.name = "Shop Item 1-1"
        with self.assertRaisesRegex(RuntimeError, "non-progression"):
            enforce_early_hand(multiworld, 1, "Left Hand")


if __name__ == "__main__":
    unittest.main()
