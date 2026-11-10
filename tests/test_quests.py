"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

import json

from BaseClasses import Item, ItemClassification
from Fill import distribute_items_restrictive
from Options import OptionError
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.catalogs import (
    WEAPONS,
)
from worlds.until_you_fall.items import (
    WEAPON_UPGRADE_LEVEL_ITEM,
)
from worlds.until_you_fall.options import (
    QuestBoard,
)
from worlds.until_you_fall.quest_locations import (
    QUESTS,
    all_quest_locations,
    quest_location_name,
    quest_upgrades_needed,
)


class TestQuests(UntilYouFallWorldTest):
    def test_quest_board_on_by_default_and_ids_are_stable(self):
        self.assertTrue(QuestBoard.default)
        self.assertEqual(len(QUESTS), 45)
        self.assertEqual(self.world.location_name_to_id["Quest: Unleashed Potential - Reward 1"], 912001)
        self.assertEqual(self.world.location_name_to_id["Quest: Titan's Grip - Reward 3"], 912000 + 21 * 3 + 3)
        self.assertEqual(len(set(all_quest_locations().values())), len(all_quest_locations()))

    def test_every_quest_is_registered_everywhere(self):
        from pathlib import Path

        from worlds.until_you_fall.quest_locations import QUESTS

        root = Path(__file__).resolve().parent.parent
        launcher = json.loads((root / "src/launcher/Resources/options.json").read_text(encoding="utf-8"))
        listed = {
            value["name"] for option in launcher if option["key"] == "required_quests" for value in option["values"]
        }
        steam_icons = (root / "src/shared/SteamQuestIcons.cs").read_text(encoding="utf-8")
        self.assertEqual(len({quest["Name"] for quest in QUESTS}), len(QUESTS), "duplicate quest name")
        for quest in QUESTS:
            with self.subTest(quest["Name"]):
                self.assertIn(quest["Name"], listed, "run dev/export-options.py")
                icon = quest["Icon"]
                if icon.startswith("ACH_"):
                    self.assertIn(f'["{icon}"] = "', steam_icons, "add the Steam icon hash to SteamQuestIcons.cs")
                else:
                    self.assertTrue(
                        (root / "assets" / "quest-icons" / (icon + ".jpg")).exists(), "missing icon " + icon
                    )
                    self.assertFalse(icon.startswith("ACH_"))
        self.assertEqual(list((root / "assets" / "quest-icons").glob("ACH_*")), [], "Steam icons must not be shipped")

    def test_quest_options_accept_exact_quest_names(self):
        from worlds.until_you_fall.options import ExcludedQuests, RequiredQuests

        for option in (RequiredQuests, ExcludedQuests):
            option.from_any(["Brink of Ruin", "dodge, duck, dip, dive and dodge"]).verify_keys()
            with self.assertRaises(OptionError):
                option.from_any(["No Such Quest"]).verify_keys()

    def test_required_and_excluded_quests_shape_the_board(self):
        by_tier = lambda tier: [q["Name"] for q in QUESTS if q["Tier"] == tier]
        required = {"champion of the runeguard", by_tier(1)[0], by_tier(2)[0]}
        excluded = set(by_tier(1)[1:4]) | set(by_tier(2)[1:11]) | set(by_tier(3)[4:15])
        self.options = {"quest_board": True, "required_quests": sorted(required), "excluded_quests": sorted(excluded)}
        for seed in (1, 2, 3):
            with self.subTest(seed=seed):
                self.world_setup(seed=seed)
                chosen = [QUESTS[index] for index in self.world.quests]
                names = {q["Name"].casefold() for q in chosen}
                self.assertEqual([sum(q["Tier"] == tier for q in chosen) for tier in (1, 2, 3)], [6, 6, 6])
                self.assertTrue({name.casefold() for name in required} <= names)
                self.assertFalse(names & {name.casefold() for name in excluded})

    def test_impossible_quest_selections_are_option_errors(self):
        by_tier = lambda tier: [q["Name"] for q in QUESTS if q["Tier"] == tier]
        for label, required, excluded in (
            ("overlap", [by_tier(1)[0]], [by_tier(1)[0]]),
            ("seven required", by_tier(3)[:7], []),
            ("too many excluded", [], by_tier(1)[: len(by_tier(1)) - 5]),
        ):
            with self.subTest(label):
                self.options = {"quest_board": True, "required_quests": required, "excluded_quests": excluded}
                with self.assertRaises(OptionError):
                    self.world_setup(seed=1)
        self.options = {"quest_board": False, "required_quests": by_tier(3)[:7]}
        self.world_setup(seed=1)
        self.assertEqual(self.world.quests, [])

    def test_quest_board_selection_rewards_and_pacing_rules(self):
        self.options = {"quest_board": True}
        for seed in (1, 2, 3, 4):
            with self.subTest(seed=seed):
                self.world_setup(seed=seed)
                chosen = [QUESTS[index] for index in self.world.quests]
                self.assertEqual([sum(q["Tier"] == tier for q in chosen) for tier in (1, 2, 3)], [6, 6, 6])
                names = {
                    location.name for location in self.multiworld.get_locations() if location.name.startswith("Quest: ")
                }
                self.assertEqual(len(names), 6 + 12 + 18)
                self.assertEqual(self.world.fill_slot_data()["quests"], self.world.quests)
                state = self.multiworld.state.copy()
                for quest in chosen:
                    if (
                        quest["Need"].get("Weapons")
                        or quest["Need"].get("AnyWeapons")
                        or quest["Need"].get("WeaponCount")
                        or quest["Need"].get("AllSupers")
                    ):
                        continue
                    location = self.multiworld.get_location(quest_location_name(quest, 1), self.player)
                    need = quest_upgrades_needed(quest)
                    probe = state.copy()
                    for _ in range(max(0, need - 1)):
                        probe.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
                    self.assertEqual(location.can_reach(probe), need == 0, quest["Name"])
                    probe.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
                    self.assertTrue(location.can_reach(probe), quest["Name"])
                hard = next(q for q in chosen if q["Tier"] == 3)
                rule = self.multiworld.get_location(quest_location_name(hard, 1), self.player).item_rule
                self.assertFalse(rule(self.world.create_item("Enemy Spawn Trap")))
                self.assertFalse(rule(self.world.create_item("100 Aether")))
                self.assertTrue(rule(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM)))
                easy = next(q for q in chosen if q["Tier"] == 1)
                self.assertFalse(
                    self.multiworld.get_location(quest_location_name(easy, 1), self.player).item_rule(
                        self.world.create_item("100 Aether")
                    )
                )
                distribute_items_restrictive(self.multiworld)
                self.assertTrue(self.multiworld.can_beat_game())

    def test_wings_quest_has_no_weapon_or_hand_requirement(self):
        self.options = {"quest_board": True}
        wings = next(quest for quest in QUESTS if quest["Name"] == "On the Wings of the Knight")
        self.assertEqual(wings["Need"], {})
        checked = 0
        for seed in range(4):
            self.world_setup(seed=seed)
            if QUESTS.index(wings) not in self.world.quests:
                continue
            state = self.multiworld.state.copy()
            for name in [weapon["Name"] for weapon in WEAPONS] + ["Left Hand", "Right Hand", WEAPON_UPGRADE_LEVEL_ITEM]:
                for _ in range(state.count(name, self.player)):
                    state.remove(self.world.create_item(name))
            for _ in range(quest_upgrades_needed(wings)):
                state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
            self.assertTrue(self.multiworld.get_location(quest_location_name(wings, 1), self.player).access_rule(state))
            checked += 1
        self.assertGreater(checked, 0)

    def test_wings_quest_never_contains_progression(self):
        self.options = {"quest_board": True}
        wings = next(quest for quest in QUESTS if quest["Name"] == "On the Wings of the Knight")
        checked = 0
        for seed in range(4):
            self.world_setup(seed=seed)
            if QUESTS.index(wings) not in self.world.quests:
                continue
            location = self.multiworld.get_location(quest_location_name(wings, 1), self.player)
            for owner in (self.player, self.player + 1):
                for classification in (
                    ItemClassification.progression,
                    ItemClassification.progression_skip_balancing,
                    ItemClassification.progression | ItemClassification.useful,
                ):
                    with self.subTest(seed=seed, owner=owner, classification=classification):
                        item = Item("Test progression", classification, 1, owner)
                        self.assertFalse(location.item_rule(item))
                self.assertFalse(location.item_rule(Item("Test trap", ItemClassification.trap, 2, owner)))
                self.assertTrue(location.item_rule(Item("Test useful", ItemClassification.useful, 3, owner)))
            self.assertTrue(location.item_rule(self.world.create_item("Player Bonus: Combo Limit +1")))
            self.assertFalse(location.item_rule(self.world.create_item("100 Aether")))
            distribute_items_restrictive(self.multiworld)
            self.assertFalse(location.item.advancement)
            self.assertTrue(self.multiworld.can_beat_game())
            checked += 1
        self.assertGreater(checked, 0)

    def test_quest_weapon_requirements(self):
        self.options = {"quest_board": True}
        self.world_setup(seed=1)
        self.world.quests = [index for index, quest in enumerate(QUESTS) if quest["Name"] == "Katarstrophy"]
        quest = QUESTS[self.world.quests[0]]
        self.assertEqual(quest["Need"]["Weapons"], ["Gemini Stranded", "Gemini Esper"])
