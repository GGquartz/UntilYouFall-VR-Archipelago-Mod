"""Until You Fall together with one other game, for every game Archipelago ships. Run with the full Archipelago source."""

import unittest

from harness import Slot, assert_playable, generate, partner_games
from our_options import option_combinations

SEEDS_PER_GAME = 3
# Archipelago 0.6.8 ships 80 other games, a lower count means some world failed to load in this environment
MINIMUM_PARTNER_GAMES = 80


class TestPartnerGames(unittest.TestCase):
    def test_all_partner_worlds_are_loaded(self):
        self.assertGreaterEqual(len(partner_games()), MINIMUM_PARTNER_GAMES)

    def test_every_game_generates_with_ours(self):
        combinations = option_combinations()
        for game_number, game in enumerate(partner_games()):
            for seed in range(SEEDS_PER_GAME):
                ours = combinations[(game_number * SEEDS_PER_GAME + seed) % len(combinations)]
                # Alternate who is player 1, the order changes fill and balancing
                slots = [ours, Slot(game)] if seed % 2 == 0 else [Slot(game), ours]
                with self.subTest(game=game, seed=seed, options=ours.options, common=ours.common_options):
                    self.generate_or_skip_broken_partner(slots, game, seed)

    def generate_or_skip_broken_partner(self, slots, game, seed):
        try:
            multiworld = generate(slots, seed)
        except Exception:
            if self.partner_generates_alone(game, seed, write_output=True):
                raise
            # Some games need their ROM to write the output, the fill can still be checked
            if not self.partner_generates_alone(game, seed, write_output=False):
                self.skipTest(f"{game} does not generate on its own with seed {seed}")
            multiworld = generate(slots, seed, write_output=False)

        assert_playable(self, multiworld)
        self.assertEqual({slot.game for slot in slots}, set(multiworld.game.values()))

    @staticmethod
    def partner_generates_alone(game, seed, write_output):
        try:
            generate([Slot(game)], seed, write_output)
        except Exception:
            return False

        return True
