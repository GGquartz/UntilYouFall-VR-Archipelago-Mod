"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

from BaseClasses import ItemClassification
from uyf_world_test import UntilYouFallWorldTest


class TestStartInventory(UntilYouFallWorldTest):
    def test_elite_trap_start_inventory_is_supported(self):
        self.options = {"start_inventory": {"Elite Encounter Trap": 1}, "trap_percent": 0}
        self.world_setup(seed=12345)
        self.assertEqual(self.world.options.start_inventory.value, {"Elite Encounter Trap": 1})
        self.assertEqual(self.world.create_item("Elite Encounter Trap").classification, ItemClassification.trap)

    def test_dodge_trap_start_inventory_is_supported(self):
        self.options = {"start_inventory": {"Dodge Only Trap": 1}, "trap_percent": 0}
        self.world_setup(seed=12345)
        self.assertEqual(self.world.options.start_inventory.value, {"Dodge Only Trap": 1})
        self.assertEqual(self.world.create_item("Dodge Only Trap").classification, ItemClassification.trap)

    def test_exhaustion_start_inventory_is_supported(self):
        self.options = {"start_inventory": {"Exhaustion Trap": 1}, "trap_percent": 0}
        self.world_setup(seed=12345)
        self.assertEqual(self.world.options.start_inventory.value, {"Exhaustion Trap": 1})
        self.assertEqual(self.world.create_item("Exhaustion Trap").classification, ItemClassification.trap)

    def test_extra_weapon_start_inventory_leaves_pool(self):
        self.options = {"starting_weapon": "engraved_arming_sword", "start_inventory": {"Mistcutter": 1}}
        self.world_setup(seed=12345)
        pool = [item.name for item in self.multiworld.itempool]
        self.assertNotIn("Mistcutter", pool)

    def test_starting_weapon_in_start_inventory_is_not_duplicated(self):
        self.options = {"starting_weapon": "engraved_arming_sword", "start_inventory": {"Engraved Arming Sword": 1}}
        self.world_setup(seed=12345)
        self.assertEqual(sum(item.name == "Engraved Arming Sword" for item in self.multiworld.precollected_items[1]), 0)
        self.assertNotIn("Engraved Arming Sword", [item.name for item in self.multiworld.itempool])

    def test_weapon_start_inventory_allows_one_copy(self):
        self.options = {"start_inventory": {"Mistcutter": 2}}
        with self.assertRaisesRegex(ValueError, "start inventory|start_inventory"):
            self.world_setup(seed=12345)

    def test_hand_start_inventory_is_rejected_in_every_mode(self):
        for mode in ("both", "left_only", "right_only"):
            for hand in ("Left Hand", "Right Hand"):
                with self.subTest(starting_hands=mode, hand=hand):
                    self.options = {"starting_hands": mode, "start_inventory": {hand: 1}}
                    with self.assertRaisesRegex(ValueError, "start inventory|start_inventory"):
                        self.world_setup(seed=12345)

    def test_from_pool_validation_matches_start_inventory(self):
        for option, value in (
            ("start_inventory_from_pool", {"Left Hand": 1}),
            ("start_inventory_from_pool", {"Mistcutter": 2}),
            ("start_inventory_from_pool", {"Difficulty: Nightmare": 1}),
        ):
            with self.subTest(value=value):
                self.options = {option: value, "max_difficulty": "relaxed", "starting_difficulty": "relaxed"}
                with self.assertRaises(ValueError):
                    self.world_setup(seed=12345)

    def test_trait_slot_start_inventory_leaves_pool(self):
        self.options = {"start_inventory": {"Extra Trait Slot: Mistcutter": 1}}
        self.world_setup(seed=12345)
        self.assertNotIn("Extra Trait Slot: Mistcutter", [item.name for item in self.multiworld.itempool])
