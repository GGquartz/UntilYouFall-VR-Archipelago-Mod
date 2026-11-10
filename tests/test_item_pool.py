"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

import unittest

from BaseClasses import ItemClassification
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.catalogs import (
    WEAPONS,
)
from worlds.until_you_fall.items import (
    STARTING_TRAIT_ITEMS,
    WEAPON_ABILITY_ITEMS,
    WEAPON_UPGRADE_LEVEL_ITEM,
)
from worlds.until_you_fall.quest_locations import (
    QUESTS,
    quest_location_name,
    quest_upgrades_needed,
)
from worlds.until_you_fall.stage_reward_locations import (
    stage_reward_name,
)


class TestItemPool(UntilYouFallWorldTest):
    def test_player_timing_bonus_pool_and_placement(self):
        for name, item_id in {
            "Player Bonus: +10% Critical Strike Window Length": 910445,
            "Player Bonus: -10% Dash Recharge Time": 910446,
        }.items():
            with self.subTest(item=name):
                self.assertEqual(self.world.item_name_to_id.get(name), item_id)
                copies = [item for item in self.multiworld.itempool if item.name == name]
                self.assertEqual(len(copies), 3)
                self.assertTrue(all(item.classification == ItemClassification.useful for item in copies))
                for location in self.multiworld.get_locations(self.player):
                    if location.name.startswith(("Shop Item ", "Stage Reward ")):
                        self.assertTrue(location.item_rule(copies[0]), location.name)

    def test_weapon_damage_bonus_pool_and_stage_rewards(self):
        expected = {
            f"{weapon['Name']} {stat} Damage +1": 910500 + (weapon["Id"] - 910011) * 2 + offset
            for weapon in WEAPONS
            if weapon["Id"] != 910010
            for offset, stat in enumerate(("Health", "Guard"))
        }
        self.assertEqual(len(expected), 28)
        self.assertEqual(set(expected.values()), set(range(910500, 910528)))
        for name, item_id in expected.items():
            with self.subTest(name=name):
                self.assertEqual(self.world.item_name_to_id.get(name), item_id)
                copies = [item for item in self.multiworld.itempool if item.name == name]
                self.assertEqual(len(copies), 3)
                self.assertTrue(all(item.classification == ItemClassification.filler for item in copies))
                self.assertTrue(all(not item.advancement and not item.useful and not item.trap for item in copies))
                for index in range(1, self.world.options.stage_reward_checks.value + 1):
                    self.assertTrue(
                        self.multiworld.get_location(stage_reward_name(index), self.player).item_rule(copies[0])
                    )
        self.assertFalse(any("Charm " in name and "Damage +1" in name for name in self.world.item_name_to_id))

    def test_starting_traits_are_fixed_useful_items_without_access_requirements(self):
        self.assertEqual(30, len(STARTING_TRAIT_ITEMS))
        self.assertEqual(32, sum(STARTING_TRAIT_ITEMS.values()))
        self.assertEqual(1, STARTING_TRAIT_ITEMS["Extra Starting Trait Pick"])
        self.assertEqual(3, STARTING_TRAIT_ITEMS["Extra Starting Trait Option"])
        self.assertEqual(910628, self.world.item_name_to_id["Extra Starting Trait Pick"])
        self.assertEqual(910629, self.world.item_name_to_id["Extra Starting Trait Option"])
        self.assertNotIn("Extra Starting Trait", STARTING_TRAIT_ITEMS)
        self.assertNotIn("Extra Starting Trait Choice", STARTING_TRAIT_ITEMS)
        for name, count in STARTING_TRAIT_ITEMS.items():
            with self.subTest(name=name):
                copies = [item for item in self.multiworld.itempool if item.name == name]
                self.assertEqual(count, len(copies))
                self.assertTrue(all(item.classification == ItemClassification.useful for item in copies))
        state = self.multiworld.get_all_state()
        for name, count in STARTING_TRAIT_ITEMS.items():
            for _ in range(count):
                state.remove(self.world.create_item(name))
        self.assertTrue(self.multiworld.completion_condition[self.player](state))

    def test_starting_trait_pool_fits_small_and_large_worlds(self):
        for starting, maximum, count, supers, hands in (
            ("relaxed", "relaxed", 10, "separate_items", "right_only"),
            ("nightmare", "nightmare", 10, "separate_items", "both"),
            ("relaxed", "nightmare", 30, "unlocked_with_weapon", "both"),
        ):
            with self.subTest(starting=starting, maximum=maximum):
                self.options = {
                    "starting_difficulty": starting,
                    "max_difficulty": maximum,
                    "stage_reward_checks": count,
                    "weapon_supers": supers,
                    "starting_hands": hands,
                    "trap_percent": 100,
                }
                self.world_setup(seed=12345)
                self.assertEqual(
                    len(self.multiworld.itempool), len(self.multiworld.get_unfilled_locations(self.player))
                )
                self.assertEqual(32, sum(item.name in STARTING_TRAIT_ITEMS for item in self.multiworld.itempool))

    def test_combo_limit_is_useful_and_not_required_by_quests(self):
        self.options = {"quest_board": True}
        for seed in range(4):
            self.world_setup(seed=seed)
            self.assertEqual(
                self.world.create_item("Player Bonus: Combo Limit +1").classification, ItemClassification.useful
            )
            self.assertFalse(any(quest["Need"].get("ComboLimit") for quest in QUESTS))
            quick = next(quest for quest in QUESTS if quest["Name"] == "Quick on the Draw")
            if QUESTS.index(quick) not in self.world.quests:
                continue
            state = self.multiworld.state.copy()
            for _ in range(quest_upgrades_needed(quick)):
                state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
            self.assertTrue(self.multiworld.get_location(quest_location_name(quick, 1), self.player).can_reach(state))

    def test_weapon_supers_default_adds_one_progression_item_each(self):
        self.assertEqual(
            WEAPON_ABILITY_ITEMS,
            {
                "Bulwark": 910430,
                "Concussive Blast": 910431,
                "Empowered Strike": 910432,
                "Shardstorm": 910433,
                "Soul Burn": 910434,
                "Stasis Strike": 910435,
                "Titan's Strength": 910436,
                "Vicious Strike": 910437,
                "Void Trap": 910438,
            },
        )
        ability_items = [item for item in self.multiworld.itempool if item.name in WEAPON_ABILITY_ITEMS]
        self.assertEqual([item.name for item in ability_items], list(WEAPON_ABILITY_ITEMS))
        self.assertTrue(all(item.classification == ItemClassification.progression for item in ability_items))
        self.assertEqual(sum(item.name == "100 Aether" for item in self.multiworld.itempool), 123)
        self.assertEqual(sum(item.name == "Shard of Vitality" for item in self.multiworld.itempool), 18)
        self.assertEqual(self.world.fill_slot_data()["weapon_supers"], "separate_items")

    def test_unlocked_with_weapon_does_not_add_super_items(self):
        self.options = {"weapon_supers": "unlocked_with_weapon"}
        self.world_setup(seed=12345)
        self.assertFalse(any(item.name in WEAPON_ABILITY_ITEMS for item in self.multiworld.itempool))
        self.assertEqual(sum(item.name == "100 Aether" for item in self.multiworld.itempool), 129)
        self.assertEqual(sum(item.name == "Shard of Vitality" for item in self.multiworld.itempool), 19)
        self.assertEqual(self.world.fill_slot_data()["weapon_supers"], "unlocked_with_weapon")

    def test_replacement_filler_is_always_vitality(self):
        item = self.world.create_filler()
        self.assertEqual(item.name, "100 Aether")
        self.assertEqual(item.code, 910416)
        self.assertEqual(item.classification, ItemClassification.filler)


class TestItemGroups(unittest.TestCase):
    def test_groups_contain_only_real_items(self):
        from worlds.until_you_fall import UntilYouFallWorld

        groups = UntilYouFallWorld.item_name_groups
        for group in ("Traps", "Weapon Abilities", "Difficulties"):
            self.assertTrue(groups[group], group)
            self.assertLessEqual(groups[group], set(UntilYouFallWorld.item_name_to_id), group)
        self.assertFalse(set(groups) & set(UntilYouFallWorld.item_name_to_id))
