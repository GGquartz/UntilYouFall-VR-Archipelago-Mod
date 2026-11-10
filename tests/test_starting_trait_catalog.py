import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class StartingTraitCatalogTests(unittest.TestCase):
    def test_catalog_has_only_the_28_approved_reward_traits(self):
        catalog = json.loads((ROOT / "src/apworld/until_you_fall/starting_traits.json").read_text())
        expected = {
            "Alternating Strikes",
            "Balanced",
            "Bloodthirsty",
            "Fleet",
            "Hungering",
            "Juggernaut",
            "Nimble",
            "Resonant",
            "Sparklight",
            "Tenacious",
            "Weighted",
            "Flame-Starved",
            "Guarded",
            "Heartseeker",
            "Kinetic Siphon",
            "Lethal Elegance",
            "Pristine",
            "Staggering",
            "Stalwart",
            "Void Touched",
            "Arcane",
            "Chrono-Locked",
            "Enkindled",
            "Frenzied",
            "Honed",
            "Serrated",
            "Unyielding",
            "Void Scarred",
        }
        self.assertEqual(expected, {entry["Name"] for entry in catalog})
        self.assertEqual(28, len(catalog))
        self.assertEqual(list(range(910600, 910628)), [entry["Id"] for entry in catalog])
        self.assertEqual(28, len({entry["RewardGuid"] for entry in catalog}))
        self.assertTrue(all(len(entry["RewardGuid"]) == 32 for entry in catalog))


if __name__ == "__main__":
    unittest.main()
