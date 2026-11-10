"""Five-slot multiworlds with several Until You Fall slots and other games. Run with the full Archipelago source."""

import unittest

from harness import OUR_GAME, Slot, assert_playable, generate, partner_games
from our_options import option_combinations

SEEDS = range(1, 4)
OTHER_GAMES = ["Hollow Knight", "Timespinner", "Pokemon Red and Blue", "Stardew Valley", "Super Mario 64"]


def ours(**options) -> Slot:
    return Slot(OUR_GAME, {"quest_board": True, **options})


SCENARIOS = {
    "five of ours with different goals": [
        ours(goal="quests"),
        ours(goal="all_weapons", quest_board=False),
        ours(goal="runs_max_difficulty", max_difficulty="relentless"),
        ours(goal="runs", goal_runs=20, trap_percent=100),
        ours(goal="runs", goal_runs=1, stage_reward_checks=30),
    ],
    "three of ours with two other games": [
        ours(goal="quests", starting_weapon="heartsplitter"),
        ours(goal="all_weapons", starting_hands="both", starting_weapon="fates_end"),
        ours(goal="runs", second_starting_weapon="random"),
        Slot(OTHER_GAMES[0]),
        Slot(OTHER_GAMES[1]),
    ],
    "two of ours with three other games on minimal accessibility": [
        Slot(OUR_GAME, {"quest_board": True}, {"accessibility": "minimal", "progression_balancing": 0}),
        Slot(OTHER_GAMES[2], {}, {"accessibility": "minimal"}),
        Slot(OUR_GAME, {"goal": "all_weapons", "quest_board": False}, {"accessibility": "minimal"}),
        Slot(OTHER_GAMES[3], {}, {"progression_balancing": 99}),
        Slot(OTHER_GAMES[4]),
    ],
    "our items kept local or sent away": [
        Slot(OUR_GAME, {"quest_board": True, "local_items": ["Weapons"]}),
        Slot(OUR_GAME, {"quest_board": True, "non_local_items": ["Weapons", "Traps"]}),
        Slot(OUR_GAME, {"quest_board": True, "trap_percent": 100, "non_local_items": ["Difficulties"]}),
        Slot(OTHER_GAMES[0]),
        Slot(OTHER_GAMES[1]),
    ],
    "free fillers from other games": [
        ours(stage_reward_checks=30, trap_percent=0),
        ours(stage_reward_checks=30, trap_percent=100),
        Slot(OTHER_GAMES[3]),
        Slot(OTHER_GAMES[4]),
        Slot(OTHER_GAMES[2]),
    ],
}


class TestMultiplayerScenarios(unittest.TestCase):
    def test_scenarios_use_installed_games(self):
        self.assertTrue(set(OTHER_GAMES) <= set(partner_games()))

    def test_scenarios_generate_with_several_seeds(self):
        for name, slots in SCENARIOS.items():
            for seed in SEEDS:
                with self.subTest(scenario=name, seed=seed):
                    assert_playable(self, generate(slots, seed))

    def test_five_slots_of_random_option_combinations(self):
        combinations = option_combinations(seed=99)
        for seed in SEEDS:
            slots = [combinations[(seed * 5 + number) % len(combinations)] for number in range(5)]
            with self.subTest(seed=seed):
                assert_playable(self, generate(slots, seed))
