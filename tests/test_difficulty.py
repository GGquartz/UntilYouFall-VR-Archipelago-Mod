"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

from BaseClasses import ItemClassification
from Fill import distribute_items_restrictive
from Options import OptionError
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.catalogs import (
    ENEMIES,
)
from worlds.until_you_fall.difficulty_logic import GLITCH_ITEM, upgrade_need
from worlds.until_you_fall.items import (
    DIFFICULTY_ITEMS,
    WEAPON_UPGRADE_LEVEL_ITEM,
    UntilYouFallItem,
)
from worlds.until_you_fall.options import (
    MaxDifficulty,
    StartingDifficulty,
)
from worlds.until_you_fall.stage_weapon_locations import location_name


class TestDifficulty(UntilYouFallWorldTest):
    def test_upgrade_curve_keeps_its_existing_rounding(self):
        for stage in range(1, 22):
            for tiers_above_starting in range(4):
                with self.subTest(stage=stage, tiers_above_starting=tiers_above_starting):
                    expected = min(9, int(max(0, stage - 7) * 0.4) + tiers_above_starting)
                    self.assertEqual(upgrade_need(stage, tiers_above_starting), expected)

    def test_stage_difficulty_checks_follow_resolved_range_and_exact_unlock(self):
        names = ("Relaxed", "Challenging", "Relentless", "Nightmare")
        base_ids = {location.address for location in self.multiworld.get_locations() if " on " not in location.name}
        for starting in range(4):
            for maximum in range(starting, 4):
                with self.subTest(starting=starting, maximum=maximum):
                    self.options = {"starting_difficulty": starting, "max_difficulty": maximum}
                    self.world_setup(seed=12345)
                    locations = {
                        location.name: location
                        for location in self.multiworld.get_locations()
                        if " on " in location.name
                    }
                    expected = {
                        f"{location_name(stage, '').removesuffix(' with ')} on {names[tier]}": 911500
                        + (tier - 1) * 21
                        + stage
                        for tier in range(starting + 1, maximum + 1)
                        for stage in range(1, 22)
                    }
                    self.assertEqual({name: location.address for name, location in locations.items()}, expected)
                    self.assertFalse(base_ids.intersection(expected.values()))
                    self.assertEqual(len(self.multiworld.itempool), 375 + len(expected))
                    for tier in range(starting + 1, maximum + 1):
                        location = locations[f"Clear Stage 1 on {names[tier]}"]
                        state = self.multiworld.state.copy()
                        self.assertFalse(location.can_reach(state))
                        for other in range(maximum + 1):
                            if other != tier:
                                state.collect(self.world.create_item(f"Difficulty: {names[other]}"))
                        self.assertFalse(location.can_reach(state))
                        state.collect(self.world.create_item(f"Difficulty: {names[tier]}"))
                        self.assertFalse(location.can_reach(state), "difficulty item alone is out of logic")
                        state.collect(self.world.create_item(GLITCH_ITEM))
                        self.assertTrue(location.can_reach(state), "sequence break makes it glitched-reachable")

    def test_upgrade_gated_logic_is_relative_to_starting_difficulty(self):
        names = ("Relaxed", "Challenging", "Relentless", "Nightmare")
        for starting in range(4):
            for maximum in range(starting, 4):
                with self.subTest(starting=starting, maximum=maximum):
                    self.options = {"starting_difficulty": starting, "max_difficulty": maximum}
                    self.world_setup(seed=777)
                    weapon = self.world.starting_weapons[0]
                    hands = [self.world.create_item(name) for name in ("Left Hand", "Right Hand")]
                    for stage in range(1, 22):
                        need = upgrade_need(stage, 0)
                        self.assertEqual(need == 0, stage <= 9, "stages 1-9 need no upgrades")
                        for tier in range(starting + 1, maximum + 1):
                            self.assertEqual(
                                upgrade_need(stage, tier - starting),
                                min(9, int(max(0, stage - 7) * 0.4) + tier - starting),
                            )
                    locations = {location.name: location for location in self.multiworld.get_locations()}
                    for tier in range(starting + 1, maximum + 1):
                        for stage in (1, 7, 14, 21):
                            need = upgrade_need(stage, tier - starting)
                            location = locations[f"{location_name(stage, '').removesuffix(' with ')} on {names[tier]}"]
                            state = self.multiworld.state.copy()
                            state.collect(self.world.create_item(f"Difficulty: {names[tier]}"))
                            for _ in range(need - 1):
                                state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
                            self.assertFalse(location.can_reach(state))
                            state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
                            self.assertTrue(location.can_reach(state))
                    for stage, expected in ((1, 0), (7, 0), (14, 2), (21, 5)):
                        location = locations[location_name(stage, weapon)]
                        state = self.multiworld.state.copy()
                        for hand in hands:
                            state.collect(hand)
                        self.assertEqual(location.can_reach(state), expected == 0)
                        glitched = state.copy()
                        glitched.collect(self.world.create_item(GLITCH_ITEM))
                        self.assertTrue(location.can_reach(glitched))
                        for _ in range(expected):
                            state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
                        self.assertTrue(location.can_reach(state))

    def test_boss_difficulty_names_match_weapon_stage_labels(self):
        expected = {
            "Clear Stage 7 - Defeat Boss 1 on Relentless": 911528,
            "Clear Stage 14 - Defeat Boss 2 on Relentless": 911535,
            "Clear Stage 21 - Defeat Final Boss on Relentless": 911542,
            "Clear Stage 1 on Relentless": 911522,
        }
        for name, address in expected.items():
            self.assertEqual(self.world.location_name_to_id.get(name), address)

    def test_random_starting_difficulty_preserves_request_until_generation(self):
        self.assertTrue(StartingDifficulty.from_text("RaNdOm").is_random)
        self.assertFalse(StartingDifficulty.from_text("challenging").is_random)

    def test_random_starting_difficulty_is_strictly_below_maximum(self):
        for maximum in range(1, 4):
            seen = set()
            for seed in range(12):
                with self.subTest(maximum=maximum, seed=seed):
                    self.options = {"starting_difficulty": "random", "max_difficulty": maximum}
                    self.world_setup(seed=seed)
                    starting = self.world.options.starting_difficulty.value
                    self.assertIn(starting, range(maximum))
                    seen.add(starting)
                    self.assertEqual(self.world.fill_slot_data()["starting_difficulty"], starting)
                    names = list(DIFFICULTY_ITEMS)
                    collected = [
                        item.name
                        for item in self.multiworld.precollected_items[self.player]
                        if item.name in DIFFICULTY_ITEMS
                    ]
                    self.assertEqual(collected, [names[starting]])
                    pool = [item.name for item in self.multiworld.itempool if item.name in DIFFICULTY_ITEMS]
                    self.assertEqual(
                        pool, [name for tier, name in enumerate(names) if tier <= maximum and tier != starting]
                    )
                    self.assertEqual(len(self.multiworld.itempool), 375 + 21 * (maximum - starting))
                    self.assertEqual(
                        {
                            location.name.rsplit(" on ", 1)[1]
                            for location in self.multiworld.get_locations()
                            if " on " in location.name
                        },
                        {name.removeprefix("Difficulty: ") for name in names[starting + 1 : maximum + 1]},
                    )
                    self.world_setup(seed=seed)
                    self.assertEqual(self.world.options.starting_difficulty.value, starting)
            self.assertEqual(seen, set(range(maximum)))

    def test_random_starting_difficulty_rejects_relaxed_maximum(self):
        self.options = {"starting_difficulty": "random", "max_difficulty": "relaxed"}
        with self.assertRaises(OptionError):
            self.world_setup(seed=12345)

    def test_difficulty_ranges_and_independent_progression(self):
        self.assertEqual((StartingDifficulty.default, MaxDifficulty.default), (1, 3))
        names = ["Difficulty: " + name for name in ("Relaxed", "Challenging", "Relentless", "Nightmare")]
        self.assertEqual(DIFFICULTY_ITEMS, {name: 910460 + tier for tier, name in enumerate(names)})
        locations = sorted(
            (location.name, location.address)
            for location in self.multiworld.get_locations()
            if " on " not in location.name
        )
        for starting in range(4):
            for maximum in range(starting, 4):
                with self.subTest(starting=starting, maximum=maximum):
                    self.options = {"starting_difficulty": starting, "max_difficulty": maximum}
                    self.world_setup(seed=12345)
                    collected = [
                        item.name
                        for item in self.multiworld.precollected_items[self.player]
                        if item.name in DIFFICULTY_ITEMS
                    ]
                    self.assertEqual(collected, [names[starting]])
                    items = [item for item in self.multiworld.itempool if item.name in DIFFICULTY_ITEMS]
                    self.assertEqual(
                        [item.name for item in items],
                        [name for tier, name in enumerate(names) if tier <= maximum and tier != starting],
                    )
                    self.assertTrue(all(item.classification == ItemClassification.progression for item in items))
                    self.assertEqual(len(self.multiworld.itempool), 375 + 21 * (maximum - starting))
                    self.assertEqual(
                        sorted(
                            (location.name, location.address)
                            for location in self.multiworld.get_locations()
                            if " on " not in location.name
                        ),
                        locations,
                    )
                    self.assertEqual(self.world.fill_slot_data()["starting_difficulty"], starting)
                    self.assertEqual(self.world.fill_slot_data()["max_difficulty"], maximum)
                    distribute_items_restrictive(self.multiworld)
                    self.assertFalse(self.multiworld.get_unfilled_locations())
                    self.assertTrue(self.multiworld.can_beat_game())

    def test_difficulty_reversed_ranges_rejected(self):
        for starting in range(1, 4):
            for maximum in range(starting):
                self.options = {"starting_difficulty": starting, "max_difficulty": maximum}
                with self.subTest(starting=starting, maximum=maximum), self.assertRaises(OptionError):
                    self.world_setup(seed=12345)

    def test_difficulty_start_inventory_range(self):
        self.options = {"max_difficulty": "relentless", "start_inventory": {"Difficulty: Nightmare": 1}}
        with self.assertRaisesRegex(ValueError, "outside"):
            self.world_setup(seed=12345)
        for name in ("Difficulty: Relaxed", "Difficulty: Challenging", "Difficulty: Nightmare"):
            self.options = {"start_inventory": {name: 1}}
            self.world_setup(seed=12345)
            for item_name, count in self.world.options.start_inventory.value.items():
                for _ in range(count):
                    self.multiworld.push_precollected(self.world.create_item(item_name))
            collected = [item.name for item in self.multiworld.precollected_items[self.player]]
            self.assertEqual(collected.count(name), 1)
            self.assertEqual(collected.count("Difficulty: Challenging"), 1)

    def test_enemy_checks_allow_progression_for_any_player(self):
        for name in self.world.location_name_to_id:
            if not name.startswith("First Kill: "):
                continue
            location = self.multiworld.get_location(name, self.player)
            self.assertTrue(
                location.can_fill(self.multiworld.state, self.world.create_item("Mistcutter"), check_access=False)
            )
            remote = UntilYouFallItem("Foreign progression", ItemClassification.progression, 123, 2)
            self.assertTrue(location.can_fill(self.multiworld.state, remote, check_access=False))

    def test_enemy_checks_follow_stage_upgrade_curve(self):
        expected = {
            "Lesser Husk": (1, 0),
            "Unraveled Husk": (1, 0),
            "Rokari Knight": (3, 0),
            "Rokari Captain": (5, 0),
            "Rokari Spellweaver": (8, 0),
            "Empowered Knight": (12, 2),
            "Rokari Honor Guard": (14, 2),
            "Empowered Captain": (17, 4),
            "Empowered Spellweaver": (17, 4),
            "Aether Horror": (7, 0),
        }
        self.assertEqual(
            {enemy["Name"]: enemy["EarliestStage"] for enemy in ENEMIES},
            {name: stage for name, (stage, _) in expected.items()},
        )
        ranges = [(starting, maximum) for starting in range(4) for maximum in range(starting, 4)]
        ranges.extend(("random", maximum) for maximum in range(1, 4))
        for starting, maximum in ranges:
            with self.subTest(starting=starting, maximum=maximum):
                self.options = {"starting_difficulty": starting, "max_difficulty": maximum}
                self.world_setup(seed=777)
                for name, (stage, need) in expected.items():
                    self.assertEqual(upgrade_need(stage, 0), need)
                    location = self.multiworld.get_location(f"First Kill: {name}", self.player)
                    for copies in range(5):
                        with self.subTest(enemy=name, upgrades=copies):
                            state = self.multiworld.state.copy()
                            for _ in range(copies):
                                state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
                            self.assertEqual(location.can_reach(state), copies >= need)
                            sequence_break = self.world.create_item(GLITCH_ITEM)
                            state.collect(sequence_break)
                            self.assertTrue(location.can_reach(state))
                            state.remove(sequence_break)
                            self.assertEqual(location.can_reach(state), copies >= need)
