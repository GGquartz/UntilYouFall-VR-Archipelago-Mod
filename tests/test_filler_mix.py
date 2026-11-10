import unittest

from worlds.until_you_fall.items import filler_counts


class FillerMixTests(unittest.TestCase):
    def test_exact_pool_and_largest_remainders(self):
        self.assertEqual(
            filler_counts(227), {"100 Aether": 159, "Super Charge": 23, "Enemy Stasis": 22, "Shard of Vitality": 23}
        )
        self.assertEqual(
            filler_counts(1), {"100 Aether": 1, "Super Charge": 0, "Enemy Stasis": 0, "Shard of Vitality": 0}
        )
        self.assertEqual(
            filler_counts(5), {"100 Aether": 4, "Super Charge": 0, "Enemy Stasis": 0, "Shard of Vitality": 1}
        )
        self.assertEqual(
            filler_counts(100), {"100 Aether": 70, "Super Charge": 10, "Enemy Stasis": 10, "Shard of Vitality": 10}
        )
        for slots in range(400):
            counts = filler_counts(slots)
            self.assertEqual(sum(counts.values()), slots)
            self.assertTrue(all(count >= 0 for count in counts.values()))
            for name, share in (
                ("100 Aether", 70),
                ("Super Charge", 10),
                ("Enemy Stasis", 10),
                ("Shard of Vitality", 10),
            ):
                self.assertLess(abs(counts[name] - slots * share / 100), 1)
