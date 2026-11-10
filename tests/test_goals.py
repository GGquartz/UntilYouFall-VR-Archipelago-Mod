"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

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
    Goal,
    GoalRuns,
)


class TestGoals(UntilYouFallWorldTest):
    def victory_reachable(self, state):
        for location in self.multiworld.get_locations(self.player):
            if location.address is None and location.item and state.has(location.item.name, self.player):
                state.remove(location.item)
                state.advancements.discard(location)
        state.sweep_for_advancements()
        return self.multiworld.get_location("Victory", self.player).can_reach(state)

    def test_runs_goal_needs_a_final_stage_clear_with_any_weapon(self):
        goal = self.victory_reachable
        state = self.multiworld.get_all_state(use_cache=False)
        self.assertTrue(goal(state))
        for _ in range(9):
            state.remove(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
        self.assertFalse(goal(state))

    def test_max_difficulty_goal_requires_only_the_max_difficulty_unlock(self):
        self.options = {"goal": "runs_max_difficulty"}
        self.world_setup(seed=12345)
        goal = self.victory_reachable
        state = self.multiworld.get_all_state(use_cache=False)
        self.assertTrue(goal(state))
        state.remove(self.world.create_item("Difficulty: Relentless"))
        self.assertTrue(goal(state), "Relentless is not needed for Nightmare")
        state.remove(self.world.create_item("Difficulty: Nightmare"))
        self.assertFalse(goal(state))

    def test_single_max_difficulty_run_goal_replaces_the_final_check(self):
        final = "Clear Stage 21 - Defeat Final Boss on Nightmare"
        for goal, runs, expected in (
            ("runs_max_difficulty", 1, False),
            ("runs_max_difficulty", 2, True),
            ("runs", 1, True),
            ("all_weapons", 1, True),
        ):
            with self.subTest(goal=goal, runs=runs):
                self.options = {"goal": goal, "goal_runs": runs, "starting_hands": "both"}
                self.world_setup(seed=12345)
                names = {location.name for location in self.multiworld.get_locations(self.player)}
                self.assertEqual(final in names, expected)
                self.assertIn("Clear Stage 20 on Nightmare", names)
        self.options = {"goal": "runs_max_difficulty", "goal_runs": 1}
        self.world_setup(seed=12345)
        goal = self.victory_reachable
        state = self.multiworld.get_all_state(use_cache=False)
        self.assertTrue(goal(state))
        state.remove(self.world.create_item("Difficulty: Nightmare"))
        self.assertFalse(goal(state))
        state.collect(self.world.create_item("Difficulty: Nightmare"))
        for _ in range(9):
            state.remove(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
        self.assertFalse(goal(state), "stage 21 on Nightmare still needs its upgrades")

    def test_single_max_difficulty_run_goal_generates_for_every_difficulty_range(self):
        for starting in range(4):
            for maximum in range(starting, 4):
                with self.subTest(starting=starting, maximum=maximum):
                    self.options = {
                        "goal": "runs_max_difficulty",
                        "goal_runs": 1,
                        "starting_difficulty": starting,
                        "max_difficulty": maximum,
                    }
                    self.world_setup(seed=777)
                    self.assertTrue(self.multiworld.can_beat_game(self.multiworld.get_all_state(use_cache=False)))
                    self.assertTrue(self.victory_reachable(self.multiworld.get_all_state(use_cache=False)))

    def test_max_difficulty_goal_equals_start_needs_no_unlock(self):
        self.options = {
            "goal": "runs_max_difficulty",
            "starting_difficulty": "challenging",
            "max_difficulty": "challenging",
        }
        self.world_setup(seed=12345)
        goal = self.victory_reachable
        self.assertTrue(goal(self.multiworld.get_all_state(use_cache=False)))

    def test_all_weapons_goal_needs_every_weapon(self):
        self.options = {"goal": "all_weapons", "starting_hands": "both"}
        self.world_setup(seed=12345)
        goal = self.victory_reachable
        for weapon in WEAPONS:
            state = self.multiworld.get_all_state(use_cache=False)
            self.assertTrue(goal(state))
            if weapon["Name"] in {item.name for item in self.multiworld.precollected_items[self.player]}:
                continue
            state.remove(self.world.create_item(weapon["Name"]))
            self.assertFalse(goal(state), weapon["Name"])

    def test_quests_goal_needs_every_active_quest_reward(self):
        self.options = {"goal": "quests", "quest_board": True}
        self.world_setup(seed=12345)
        goal = self.victory_reachable
        state = self.multiworld.get_all_state(use_cache=False)
        self.assertTrue(goal(state))
        for _ in range(9):
            state.remove(self.world.create_item(WEAPON_UPGRADE_LEVEL_ITEM))
        self.assertFalse(goal(state))

    def test_goal_events_exist_for_every_goal(self):
        expected = {
            "quests": "Quest: ",
            "all_weapons": "Run With: ",
            "runs": "Clear a Run",
            "runs_max_difficulty": "Clear a Run On Max Difficulty",
        }
        for goal, prefix in expected.items():
            with self.subTest(goal=goal):
                self.options = {"goal": goal, "quest_board": True, "starting_hands": "both"}
                self.world_setup(seed=12345)
                events = [
                    location.name
                    for location in self.multiworld.get_locations(self.player)
                    if location.address is None and location.name != "Victory"
                ]
                self.assertTrue(events and all(name.startswith(prefix) for name in events), events)
                for location in self.multiworld.get_locations(self.player):
                    if location.address is None and location.name != "Victory":
                        self.assertNotIn(location.item.name, location.name)

    def test_spoiler_playthrough_lists_each_goal_event_once(self):

        self.options = {"goal": "quests", "quest_board": True}
        self.world_setup(seed=12345)
        distribute_items_restrictive(self.multiworld)
        self.multiworld.spoiler.create_playthrough(create_paths=False)
        events = [
            (location, item)
            for sphere in self.multiworld.spoiler.playthrough.values()
            if isinstance(sphere, dict)
            for location, item in sphere.items()
            if location.startswith("Quest: ") and " - Reward " not in location
        ]
        self.assertTrue(events)
        for location, item in events:
            self.assertEqual(item.count("Quest Completed"), 1, item)
            self.assertEqual(location.count("Quest Completed"), 0, location)

    def test_quests_goal_without_quest_board_is_an_option_error(self):
        self.options = {"goal": "quests", "quest_board": False}
        with self.assertRaises(OptionError):
            self.world_setup(seed=12345)

    def test_goal_defaults_and_ranges(self):
        self.assertEqual(Goal.default, Goal.option_quests)
        self.assertEqual((GoalRuns.range_start, GoalRuns.range_end, GoalRuns.default), (1, 20, 5))
