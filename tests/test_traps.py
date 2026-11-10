"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

from BaseClasses import ItemClassification
from Fill import distribute_items_restrictive
from Options import OptionError
from test.general import setup_multiworld
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.items import (
    UntilYouFallItem,
    filler_counts,
)
from worlds.until_you_fall.options import (
    TrapEffectLength,
    TrapPercent,
)
from worlds.until_you_fall.stage_reward_locations import (
    stage_reward_name,
)


class TestTraps(UntilYouFallWorldTest):
    def test_trap_effect_length_exports_duration_without_changing_pool_or_locations(self):
        self.assertEqual(TrapEffectLength.display_name, "Trap Effect Length")
        self.assertEqual(
            (TrapEffectLength.range_start, TrapEffectLength.range_end, TrapEffectLength.default), (30, 300, 60)
        )
        self.world_setup(seed=12345)
        self.assertEqual(self.world.fill_slot_data()["trap_effect_length"], 60)
        pool = sorted(item.name for item in self.multiworld.itempool)
        locations = sorted((location.name, location.address) for location in self.multiworld.get_locations())
        for duration in (30, 31, 60, 90, 300):
            with self.subTest(duration=duration):
                self.options = {"trap_effect_length": duration}
                self.world_setup(seed=12345)
                self.assertEqual(self.world.fill_slot_data()["trap_effect_length"], duration)
                self.assertEqual(sorted(item.name for item in self.multiworld.itempool), pool)
                self.assertEqual(
                    sorted((location.name, location.address) for location in self.multiworld.get_locations()), locations
                )
        for duration in (0, 29, 301):
            with self.subTest(invalid_duration=duration), self.assertRaises(Exception):
                TrapEffectLength.from_any(duration)

    def test_trap_weights_split_traps_by_fixed_counts(self):
        from worlds.until_you_fall.items import TRAP_ITEMS, trap_counts

        self.assertEqual(sum(trap_counts(195, {name: 20 for name in TRAP_ITEMS}).values()), 195)
        self.assertEqual(
            trap_counts(10, {"Enemy Spawn Trap": 80, "Dodge Only Trap": 20}),
            {
                "Enemy Spawn Trap": 8,
                "Elite Encounter Trap": 0,
                "Dodge Only Trap": 2,
                "Exhaustion Trap": 0,
                "Cursed Trap": 0,
            },
        )
        for total in range(0, 120):
            counts = trap_counts(total, {"Enemy Spawn Trap": 7, "Cursed Trap": 3, "Dodge Only Trap": 1})
            self.assertEqual(sum(counts.values()), total)
            self.assertEqual(counts["Elite Encounter Trap"] + counts["Exhaustion Trap"], 0)
        weights = {"Enemy Spawn Trap": 80, "Dodge Only Trap": 20}
        self.options = {"trap_percent": 100, "trap_weights": weights}
        for seed in (1, 2, 3):
            with self.subTest(seed=seed):
                self.world_setup(seed=seed)
                traps = [item.name for item in self.multiworld.itempool if item.trap]
                self.assertEqual(
                    {name: traps.count(name) for name in set(traps)},
                    {name: count for name, count in trap_counts(len(traps), weights).items() if count},
                )
                self.assertGreater(traps.count("Enemy Spawn Trap"), 3 * traps.count("Dodge Only Trap"))

    def test_invalid_trap_weights_are_option_errors(self):
        for label, weights, percent in (
            ("all zero", {"Cursed Trap": 0}, 10),
            ("empty", {}, 10),
            ("negative", {"Cursed Trap": -1, "Dodge Only Trap": 5}, 10),
            ("fraction", {"Cursed Trap": 1.5}, 10),
        ):
            with self.subTest(label):
                self.options = {"trap_percent": percent, "trap_weights": weights}
                with self.assertRaises(OptionError):
                    self.world_setup(seed=1)
        self.options = {"trap_percent": 0, "trap_weights": {"Cursed Trap": 0}}
        self.world_setup(seed=1)

    def test_trap_option_pool_and_rounding(self):
        self.assertEqual((TrapPercent.range_start, TrapPercent.range_end, TrapPercent.default), (0, 100, 10))
        for percent, traps in ((0, 0), (5, 10), (10, 20), (25, 49), (100, 195)):
            with self.subTest(trap_percent=percent):
                self.options = {"trap_percent": percent}
                self.world_setup(seed=12345)
                items = self.multiworld.itempool
                self.assertEqual(len(items), 417)
                self.assertEqual(sum(item.name == "Enemy Spawn Trap" for item in items), (traps + 4) // 5)
                self.assertEqual(sum(item.name == "Elite Encounter Trap" for item in items), (traps + 3) // 5)
                self.assertEqual(sum(item.name == "Dodge Only Trap" for item in items), (traps + 2) // 5)
                self.assertEqual(sum(item.name == "Exhaustion Trap" for item in items), (traps + 1) // 5)
                self.assertEqual(sum(item.name == "Cursed Trap" for item in items), traps // 5)
                for name, count in filler_counts(195 - traps).items():
                    self.assertEqual(sum(item.name == name for item in items), count)
                self.assertEqual(self.world.fill_slot_data()["trap_percent"], percent)
                trap = self.world.create_item("Enemy Spawn Trap")
                self.assertEqual(trap.code, 910450)
                self.assertEqual(trap.classification, ItemClassification.trap)
                elite_trap = self.world.create_item("Elite Encounter Trap")
                self.assertEqual(elite_trap.code, 910451)
                self.assertEqual(elite_trap.classification, ItemClassification.trap)
                dodge_trap = self.world.create_item("Dodge Only Trap")
                self.assertEqual(dodge_trap.code, 910452)
                self.assertEqual(dodge_trap.classification, ItemClassification.trap)
                cursed_trap = self.world.create_item("Cursed Trap")
                self.assertEqual(cursed_trap.code, 910454)
                self.assertEqual(cursed_trap.classification, ItemClassification.trap)

        self.options = {"starting_hands": "both", "trap_percent": 50}
        self.world_setup(seed=12345)
        for name, count in (
            ("Enemy Spawn Trap", 20),
            ("Elite Encounter Trap", 20),
            ("Dodge Only Trap", 20),
            ("Exhaustion Trap", 19),
            ("Cursed Trap", 19),
        ):
            self.assertEqual(sum(item.name == name for item in self.multiworld.itempool), count)

    def test_enemy_spawn_trap_never_fills_stage_reward(self):
        for name, item_id in (
            ("Enemy Spawn Trap", 910450),
            ("Elite Encounter Trap", 910451),
            ("Dodge Only Trap", 910452),
            ("Exhaustion Trap", 910453),
            ("Cursed Trap", 910454),
        ):
            own_trap = self.world.create_item(name)
            foreign_trap = UntilYouFallItem(name, ItemClassification.trap, item_id, 2)
            for index in range(1, self.world.options.stage_reward_checks.value + 1):
                location = self.multiworld.get_location(stage_reward_name(index), self.player)
                self.assertFalse(location.can_fill(self.multiworld.state, own_trap))
                self.assertFalse(location.can_fill(self.multiworld.state, foreign_trap))

    def test_trap_counts_follow_stage_reward_count(self):
        for count, traps in ((10, 18), (30, 20)):
            with self.subTest(stage_reward_checks=count):
                self.options = {"stage_reward_checks": count}
                self.world_setup(seed=12345)
                items = self.multiworld.itempool
                self.assertEqual(len(items), 397 + count)
                self.assertEqual(sum(item.name == "Enemy Spawn Trap" for item in items), (traps + 4) // 5)
                self.assertEqual(sum(item.name == "Elite Encounter Trap" for item in items), (traps + 3) // 5)
                self.assertEqual(sum(item.name == "Dodge Only Trap" for item in items), (traps + 2) // 5)
                self.assertEqual(sum(item.name == "Exhaustion Trap" for item in items), (traps + 1) // 5)
                self.assertEqual(sum(item.name == "Cursed Trap" for item in items), traps // 5)
                for name, copies in filler_counts(175 + count - traps).items():
                    self.assertEqual(sum(item.name == name for item in items), copies)

    def test_traps_still_allow_complete_fill(self):
        for count in (10, 30):
            for percent in (0, 5, 10, 100):
                with self.subTest(stage_reward_checks=count, trap_percent=percent):
                    self.options = {"trap_percent": percent, "stage_reward_checks": count}
                    self.world_setup(seed=12345)
                    distribute_items_restrictive(self.multiworld)
                    self.world.finalize_multiworld()
                    self.assertEqual(len(self.multiworld.get_unfilled_locations()), 0)
                    self.assertTrue(self.multiworld.can_beat_game())
                    for index in range(1, count + 1):
                        location = self.multiworld.get_location(stage_reward_name(index), self.player)
                        self.assertFalse(location.item.trap)

    def test_trap_fill_in_multiworld(self):
        multiworld = setup_multiworld(
            [type(self.world), type(self.world)],
            seed=12345,
            options=[{"trap_percent": 100, "stage_reward_checks": 30}, {"trap_percent": 5}],
        )
        distribute_items_restrictive(multiworld)
        for world in multiworld.worlds.values():
            world.finalize_multiworld()
        self.assertEqual(len(multiworld.get_unfilled_locations()), 0)
        self.assertTrue(multiworld.can_beat_game())
        for location in multiworld.get_locations():
            if location.name.startswith("Stage Reward "):
                self.assertFalse(location.item.trap)

    def test_trap_percent_rejects_values_outside_range(self):
        for value in (-1, 101):
            with self.subTest(value=value), self.assertRaises(Exception):
                TrapPercent.from_any(value)
