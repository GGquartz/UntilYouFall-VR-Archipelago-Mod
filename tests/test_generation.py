"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

import hashlib
import json
from unittest.mock import patch

from BaseClasses import ItemClassification, MultiWorld
from test.bases import WorldTestBase
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.catalogs import (
    ENEMIES,
    FIXED_ITEMS,
    ITEM_DEFINITIONS,
    STARTING_TRAITS,
    WEAPON_ABILITIES,
    WEAPONS,
)
from worlds.until_you_fall.items import (
    DIFFICULTY_ITEMS,
)
from worlds.until_you_fall.options import (
    DeathLinkMode,
    QuestHintCost,
)
from worlds.until_you_fall.quest_locations import (
    all_quest_locations,
)
from worlds.until_you_fall.stage_reward_locations import (
    MAX_STAGE_REWARD_CHECKS,
    stage_reward_id,
    stage_reward_name,
)
from worlds.until_you_fall.stage_weapon_locations import location_id, location_name


class TestGeneration(UntilYouFallWorldTest):
    test_all_state_can_reach_everything = WorldTestBase.test_all_state_can_reach_everything
    test_empty_state_can_reach_something = WorldTestBase.test_empty_state_can_reach_something
    test_fill = WorldTestBase.test_fill

    def test_generation_matches_golden_hashes(self):
        cases = (
            ({}, "314fb8f17394c4c61a36a3225dba0500275399aa8e51a2a69110d96fd94a27f1"),
            (
                {"quest_board": True, "goal": "quests", "weapon_supers": "separate_items"},
                "bd1c13149bab5375b5a458fa7a4529aa13428488f93cd6aea26c6b64ba8676b1",
            ),
            (
                {
                    "starting_hands": "both",
                    "starting_weapon": "random",
                    "second_starting_weapon": "random",
                    "stage_reward_checks": 30,
                },
                "2f7e4bfa47eb303d3631afc0befb0b9f135ce3f7e666580328963b5f5e1445e6",
            ),
        )
        for options, expected in cases:
            with self.subTest(options=options):
                self.options = options
                self.world_setup(seed=12345)
                snapshot = {
                    "pool": [(item.name, item.code, int(item.classification)) for item in self.multiworld.itempool],
                    "precollected": [
                        (item.name, item.code) for item in self.multiworld.precollected_items[self.player]
                    ],
                    "slot": self.world.fill_slot_data(),
                    # The next random draw must stay the same too, not just the items already generated.
                    "rng": repr(self.world.random.getstate()),
                }
                self.assertEqual(hashlib.sha256(json.dumps(snapshot).encode()).hexdigest(), expected)

    def test_quest_hint_cost_range_default_and_slot_data(self):
        self.assertEqual(QuestHintCost.range_start, 0)
        self.assertEqual(QuestHintCost.range_end, 5000)
        self.assertEqual(QuestHintCost.default, 500)
        self.assertEqual(QuestHintCost.display_name, "Quest Hint Cost")
        for value in (0, 500, 5000):
            for enabled in (False, True):
                with self.subTest(cost=value, quest_board=enabled):
                    self.options = {"quest_hint_cost": value, "quest_board": enabled}
                    self.world_setup(seed=12345)
                    data = self.world.fill_slot_data()
                    self.assertEqual(data["quest_hint_cost"], value)
                    self.assertEqual(bool(self.world.quests), enabled)
                    self.world.options.quest_hint_cost.value = 42
                    self.world._apply_tracker_slot_data(data)
                    self.assertEqual(self.world.options.quest_hint_cost.value, value)
                    data.pop("quest_hint_cost")
                    self.world._apply_tracker_slot_data(data)
                    self.assertEqual(self.world.options.quest_hint_cost.value, 500)
        for value in (-1, 5001):
            with self.subTest(invalid=value), self.assertRaises(Exception):
                QuestHintCost(value)

    def test_death_link_modes_preserve_values_and_slot_data(self):
        self.assertEqual(DeathLinkMode.default, DeathLinkMode.option_deaths_door)
        for mode, value in {"hard": 0, "soft": 1, "trap": 2, "off": 3, "deaths_door": 4}.items():
            with self.subTest(mode=mode):
                self.assertEqual(DeathLinkMode.options[mode], value)
                self.world.options.death_link_mode = DeathLinkMode(value)
                slot_data = self.world.fill_slot_data()
                self.assertEqual(slot_data["death_link_mode"], mode)
                self.assertEqual(slot_data["death_link"], mode != "off")

    def test_slot_contract_and_stable_ids(self):
        self.maxDiff = None
        for name in ("Super Charge", "Enemy Stasis"):
            self.assertEqual(self.world.create_item(name).classification, ItemClassification.filler)
        expected_items = {item["Name"]: item["Id"] for item in FIXED_ITEMS}
        expected_items.update(DIFFICULTY_ITEMS)
        expected_items.update({ability["Name"]: ability["Id"] for ability in WEAPON_ABILITIES})
        for weapon in WEAPONS:
            expected_items[weapon["Name"]] = weapon["Id"]
            expected_items[f"Extra Trait Slot: {weapon['Name']}"] = weapon["TraitSlotItemId"]
            if weapon.get("NoDamageItems"):
                continue
            for offset, stat in enumerate(ITEM_DEFINITIONS["DamageStats"]):
                expected_items[f"{weapon['Name']} {stat} Damage +1"] = weapon["DamageItemId"] + offset
        for trait in STARTING_TRAITS:
            expected_items[f"Starting Trait: {trait['Name']}"] = trait["Id"]
        self.assertEqual(self.world.item_name_to_id, expected_items)
        self.assertEqual(len({weapon["Guid"] for weapon in WEAPONS}), 15)
        expected = {
            location_name(stage, weapon["Name"]): location_id(stage, weapon["Id"])
            for stage in range(1, 22)
            for weapon in WEAPONS
        }
        names = [
            "Lesser Husk",
            "Unraveled Husk",
            "Rokari Knight",
            "Rokari Captain",
            "Rokari Spellweaver",
            "Empowered Knight",
            "Rokari Honor Guard",
            "Empowered Captain",
            "Empowered Spellweaver",
            "Aether Horror",
        ]
        expected.update({f"First Kill: {name}": 910301 + index for index, name in enumerate(names)})
        expected.update(
            {stage_reward_name(index): stage_reward_id(index) for index in range(1, MAX_STAGE_REWARD_CHECKS + 1)}
        )
        expected.update(
            {
                f"{location_name(stage, '').removesuffix(' with ')} on {name}": 911500 + (tier - 1) * 21 + stage
                for tier, name in enumerate(("Relaxed", "Challenging", "Relentless", "Nightmare"))
                if tier > 0
                for stage in range(1, 22)
            }
        )
        expected.update(
            {
                f"Shop Item {section}-{slot}": 911600 + (section - 1) * 6 + slot
                for section in range(1, 6)
                for slot in range(1, 7)
            }
        )
        expected.update(all_quest_locations())
        self.assertEqual(self.world.location_name_to_id, expected)
        self.assertEqual(len(set(expected.values())), len(expected))
        self.assertEqual(len({guid for enemy in ENEMIES for guid in enemy["Guids"]}), 11)
        self.assertEqual(
            self.world.fill_slot_data(),
            {
                "stage_count": 21,
                "starting_difficulty": 1,
                "max_difficulty": 3,
                "enemy_sanity": True,
                "enemy_count": 10,
                "weapon_unlocks": True,
                "weapon_hints": True,
                "starting_weapons": [self.multiworld.precollected_items[self.player][0].name],
                "starting_hands": "right_only",
                "extra_trait_slots": True,
                "progressive_weapon_upgrade_level": True,
                "aether_filler": True,
                "aether_filler_amount": 100,
                "progressive_aether_multiplier": True,
                "progressive_aether_multiplier_count": 5,
                "weapon_supers": "separate_items",
                "player_bonuses": True,
                "stage_reward_checks": 20,
                "trap_percent": 10,
                "trap_effect_length": 60,
                "death_link": True,
                "death_link_mode": "deaths_door",
                "goal": "runs",
                "goal_runs": 5,
                "quests": [],
                "quest_hint_cost": 500,
            },
        )

    def test_general_option_catalog_matches_archipelago(self):
        import inspect
        import typing
        from pathlib import Path

        from Options import Choice
        from worlds.until_you_fall.options import UntilYouFallOptions

        root = Path(__file__).resolve().parent.parent
        catalog = json.loads((root / "src/launcher/Resources/general-options.json").read_text(encoding="utf-8"))
        self.assertEqual(
            [entry["key"] for entry in catalog],
            [
                "accessibility",
                "progression_balancing",
                "local_items",
                "non_local_items",
                "start_inventory_from_pool",
                "start_hints",
                "start_location_hints",
                "priority_locations",
                "exclude_locations",
            ],
        )
        hints = typing.get_type_hints(UntilYouFallOptions)
        for entry in catalog:
            with self.subTest(entry["key"]):
                option = hints[entry["key"]]
                self.assertEqual(entry["display"], option.display_name)
                self.assertEqual(entry["help"], inspect.cleandoc(option.__doc__ or ""))
                default = option.default
                if issubclass(option, Choice):
                    default = option.name_lookup[default]
                elif isinstance(default, (set, frozenset)):
                    default = sorted(default)
                self.assertEqual(entry["default"], default)

    def test_tracker_regeneration_from_slot_data_matches_locations(self):
        self.options = {"quest_board": True, "starting_difficulty": "random", "max_difficulty": 3}
        self.world_setup(seed=3)
        data = self.world.fill_slot_data()
        expected = sorted(location.name for location in self.multiworld.get_locations(self.player))
        self.options = {}
        with patch.object(MultiWorld, "re_gen_passthrough", {self.world.game: data}, create=True):
            self.world_setup(seed=99)
        self.assertEqual(sorted(location.name for location in self.multiworld.get_locations(self.player)), expected)
        self.assertEqual(self.world.fill_slot_data(), data)
