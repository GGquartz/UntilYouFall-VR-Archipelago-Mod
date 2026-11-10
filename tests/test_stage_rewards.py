"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

from unittest.mock import patch

from BaseClasses import Item, ItemClassification
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.catalogs import (
    WEAPONS,
)
from worlds.until_you_fall.difficulty_logic import GLITCH_ITEM, stage_reward_need
from worlds.until_you_fall.items import (
    PLAYER_BONUS_ITEMS,
    WEAPON_ABILITY_ITEMS,
    WEAPON_DAMAGE_ITEMS,
    WEAPON_UPGRADE_LEVEL_ITEM,
    UntilYouFallItem,
)
from worlds.until_you_fall.options import (
    StageRewardChecks,
)
from worlds.until_you_fall.stage_reward_locations import (
    MAX_STAGE_REWARD_CHECKS,
    STAGE_REWARD_BASE_ID,
    active_stage_rewards,
    enforce_stage_reward_pacing,
    stage_reward_id,
    stage_reward_name,
)


class TestStageRewards(UntilYouFallWorldTest):
    def test_upgrade_curve_keeps_its_existing_rounding(self):
        for count in range(10, 31):
            for index in range(1, count + 1):
                with self.subTest(count=count, index=index):
                    expected = 0 if index <= 3 else max(1, round(9 * (index / count) ** 0.75))
                    self.assertEqual(stage_reward_need(index, count), expected)

    def test_late_progression_selection_keeps_its_existing_rounding(self):
        for count in range(10, 31):
            with self.subTest(count=count):
                self.options = {"stage_reward_checks": count}
                self.world_setup(seed=777)
                rewards = [
                    self.multiworld.get_location(stage_reward_name(index), self.player) for index in range(1, count + 1)
                ]
                for location in rewards:
                    location.item = self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM)
                with patch("worlds.until_you_fall.stage_reward_locations._replace_rewards_with_other_items") as replace:
                    enforce_stage_reward_pacing(self.multiworld, self.player, count)

                late_start = (2 * count + 2) // 3
                late_rewards = rewards[late_start:]
                first_replacement = replace.call_args_list[0].args
                self.assertEqual(first_replacement[2], late_rewards[len(late_rewards) // 5 :])
                self.assertEqual(first_replacement[4], {location.name for location in late_rewards})

    def test_stage_checks_and_matching_rewards(self):
        locations = [
            location for location in self.multiworld.get_locations(self.player) if location.address is not None
        ]
        self.assertEqual(len(locations), 417)
        self.assertEqual(len(self.multiworld.itempool), 417)
        self.assertEqual(sum(item.name == "Shard of Vitality" for item in self.multiworld.itempool), 18)
        self.assertEqual(sum(item.name == "100 Aether" for item in self.multiworld.itempool), 123)
        self.assertEqual(sum(item.name == "Enemy Spawn Trap" for item in self.multiworld.itempool), 4)
        self.assertEqual(sum(item.name == "Elite Encounter Trap" for item in self.multiworld.itempool), 4)
        self.assertEqual(sum(item.name == "Dodge Only Trap" for item in self.multiworld.itempool), 4)
        self.assertEqual(sum(item.name == "Exhaustion Trap" for item in self.multiworld.itempool), 4)
        self.assertEqual(sum(item.name == "Cursed Trap" for item in self.multiworld.itempool), 4)
        self.assertEqual(sum(item.name == "500 Aether" for item in self.multiworld.itempool), 10)
        self.assertEqual(sum(item.name == "750 Aether" for item in self.multiworld.itempool), 10)
        self.assertEqual(sum(item.name == "1000 Aether" for item in self.multiworld.itempool), 5)
        bulk = [item for item in self.multiworld.itempool if item.name in {"500 Aether", "750 Aether", "1000 Aether"}]
        self.assertTrue(all(item.classification == ItemClassification.useful for item in bulk))
        weapons = [item for item in self.multiworld.itempool if item.name in {weapon["Name"] for weapon in WEAPONS}]
        self.assertEqual(len(weapons), 14)
        self.assertTrue(all(item.classification == ItemClassification.progression for item in weapons))
        trait_slots = [item for item in self.multiworld.itempool if item.name.startswith("Extra Trait Slot: ")]
        self.assertEqual(len(trait_slots), 15)
        self.assertEqual(
            {item.name for item in trait_slots}, {f"Extra Trait Slot: {weapon['Name']}" for weapon in WEAPONS}
        )
        self.assertTrue(all(item.classification == ItemClassification.useful for item in trait_slots))
        upgrade_levels = [
            item for item in self.multiworld.itempool if item.name == "Progressive Max Weapon Upgrade Level"
        ]
        self.assertEqual(len(upgrade_levels), 9)
        self.assertTrue(all(item.classification == ItemClassification.progression for item in upgrade_levels))
        multipliers = [item for item in self.multiworld.itempool if item.name == "Progressive Aether Multiplier"]
        self.assertEqual(len(multipliers), 5)
        self.assertTrue(all(item.classification == ItemClassification.useful for item in multipliers))
        abilities = [item for item in self.multiworld.itempool if item.name in WEAPON_ABILITY_ITEMS]
        self.assertEqual(len(abilities), 9)
        self.assertTrue(all(item.classification == ItemClassification.progression for item in abilities))
        player_bonuses = [item for item in self.multiworld.itempool if item.name in PLAYER_BONUS_ITEMS]
        self.assertEqual(len(player_bonuses), 21)
        self.assertTrue(all(item.classification == ItemClassification.useful for item in player_bonuses))
        self.assertEqual(
            {name: sum(item.name == name for item in player_bonuses) for name in PLAYER_BONUS_ITEMS}, PLAYER_BONUS_ITEMS
        )
        starting = self.multiworld.precollected_items[self.player]
        self.assertEqual(len(starting), 3)
        self.assertNotIn(starting[0].name, ("Spellsword's Charm", "Knight-Blooded Crest"))
        self.assertEqual([item.name for item in starting[1:]], ["Right Hand", "Difficulty: Challenging"])
        self.assertEqual(
            [item.name for item in self.multiworld.itempool if item.name in ("Left Hand", "Right Hand")], ["Left Hand"]
        )
        self.assertEqual({item.name for item in weapons}, {weapon["Name"] for weapon in WEAPONS} - {starting[0].name})
        self.assertTrue(
            all(
                item.classification == ItemClassification.progression
                for item in self.multiworld.precollected_items[self.player]
            )
        )

    def test_stage_reward_option_and_stable_ids(self):
        self.assertEqual(StageRewardChecks.range_start, 10)
        self.assertEqual(StageRewardChecks.range_end, 30)
        self.assertEqual(StageRewardChecks.default, 20)
        self.assertEqual(STAGE_REWARD_BASE_ID, 911400)
        self.assertEqual(stage_reward_name(1), "Stage Reward 1")
        self.assertEqual(stage_reward_id(30), 911430)
        self.assertEqual(
            active_stage_rewards(3),
            {
                "Stage Reward 1": 911401,
                "Stage Reward 2": 911402,
                "Stage Reward 3": 911403,
            },
        )
        self.assertFalse(
            set(self.world.item_name_to_id.values())
            & {stage_reward_id(index) for index in range(1, MAX_STAGE_REWARD_CHECKS + 1)}
        )

    def test_stage_reward_option_creates_only_the_active_prefix(self):
        for count in (10, 30):
            with self.subTest(stage_reward_checks=count):
                self.options = {"stage_reward_checks": count}
                self.world_setup(seed=12345)
                names = {
                    location.name
                    for location in self.multiworld.get_locations(self.player)
                    if location.name.startswith("Stage Reward ")
                }
                self.assertEqual(names, {stage_reward_name(index) for index in range(1, count + 1)})
                self.assertEqual(len(self.multiworld.itempool), 397 + count)
                self.assertEqual(self.world.fill_slot_data()["stage_reward_checks"], count)

    def test_stage_reward_item_rules(self):
        early = self.multiworld.get_location("Stage Reward 1", self.player)
        late = self.multiworld.get_location("Stage Reward 11", self.player)
        own_aether = self.world.create_item("100 Aether")
        own_vitality = self.world.create_item("Shard of Vitality")
        foreign_aether = UntilYouFallItem("100 Aether", ItemClassification.filler, 910416, 2)
        foreign_vitality = UntilYouFallItem("Shard of Vitality", ItemClassification.filler, 910002, 2)
        foreign_filler = Item("Foreign filler", ItemClassification.filler, 123, 2)
        foreign_useful = Item("Foreign useful", ItemClassification.useful, 124, 2)
        foreign_progression = Item("Foreign progression", ItemClassification.progression, 125, 2)
        foreign_trap = Item("Foreign trap", ItemClassification.trap, 126, 2)

        for location in (early, late):
            for name in ("Super Charge", "Enemy Stasis", "500 Aether", "750 Aether", "1000 Aether"):
                self.assertFalse(
                    location.can_fill(self.multiworld.state, self.world.create_item(name), check_access=False)
                )
            self.assertFalse(location.can_fill(self.multiworld.state, own_aether, check_access=False))
            self.assertFalse(location.can_fill(self.multiworld.state, own_vitality, check_access=False))
            for name in WEAPON_DAMAGE_ITEMS:
                self.assertTrue(
                    location.can_fill(self.multiworld.state, self.world.create_item(name), check_access=False)
                )
            self.assertFalse(location.can_fill(self.multiworld.state, foreign_trap, check_access=False))
            self.assertTrue(location.can_fill(self.multiworld.state, foreign_aether, check_access=False))
            self.assertTrue(location.can_fill(self.multiworld.state, foreign_vitality, check_access=False))
            self.assertTrue(location.can_fill(self.multiworld.state, foreign_filler, check_access=False))
            self.assertTrue(location.can_fill(self.multiworld.state, foreign_useful, check_access=False))
        self.assertTrue(early.can_fill(self.multiworld.state, foreign_progression, check_access=False))
        self.assertTrue(late.can_fill(self.multiworld.state, foreign_progression, check_access=False))

    def test_stage_reward_logic_scales_with_index(self):
        needs = [stage_reward_need(index, 30) for index in range(1, 31)]
        self.assertEqual(needs, sorted(needs))
        self.assertEqual((needs[0], needs[2], needs[3], needs[11], needs[22], needs[29]), (0, 0, 2, 5, 7, 9))
        self.assertEqual(stage_reward_need(20, 20), 9)
        for index in (1, 8, 12, 20):
            location = self.multiworld.get_location(stage_reward_name(index), self.player)
            need = stage_reward_need(index, self.world.options.stage_reward_checks.value)
            state = self.multiworld.state.copy()
            for _ in range(max(0, need - 1)):
                state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
            self.assertEqual(location.can_reach(state), need == 0)
            state.collect(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
            self.assertTrue(location.can_reach(state))
            glitched = self.multiworld.state.copy()
            glitched.collect(self.world.create_item(GLITCH_ITEM))
            self.assertTrue(location.can_reach(glitched))
