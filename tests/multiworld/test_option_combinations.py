"""Run with the full Archipelago source on PYTHONPATH: python -m unittest discover -s tests/multiworld"""

import unittest

from harness import OUR_GAME, Slot, assert_playable, generate
from our_options import option_combinations


class TestOptionCombinations(unittest.TestCase):
    def test_every_option_value_generates_a_playable_seed(self):
        for fuzz_seed in range(5):
            for index, slot in enumerate(option_combinations(fuzz_seed)):
                with self.subTest(fuzz_seed=fuzz_seed, options=slot.options, common=slot.common_options):
                    assert_playable(self, generate([slot], seed=fuzz_seed * 1000 + index))

    def test_default_yaml_generates_with_several_seeds(self):
        for seed in range(1, 11):
            with self.subTest(seed=seed):
                assert_playable(self, generate([Slot(OUR_GAME)], seed))
