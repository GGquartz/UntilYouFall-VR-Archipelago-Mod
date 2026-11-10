"""Run with the Archipelago 0.6.8 source directory on PYTHONPATH."""

from Fill import distribute_items_restrictive
from uyf_world_test import UntilYouFallWorldTest
from worlds.until_you_fall.items import (
    WEAPON_DAMAGE_ITEMS,
)
from worlds.until_you_fall.shop_locations import enforce_shop_quality


class TestShop(UntilYouFallWorldTest):
    def test_shop_quality_does_not_take_items_from_processed_sections(self):
        for location in self.multiworld.get_locations(self.player):
            location.locked = not location.name.startswith("Shop Item ")
        for section, quality_count in enumerate((3, 0, 6, 3, 3), 1):
            for slot in range(1, 7):
                location = self.multiworld.get_location(f"Shop Item {section}-{slot}", self.player)
                name = "Player Bonus: Combo Limit +1" if slot <= quality_count else next(iter(WEAPON_DAMAGE_ITEMS))
                location.item = self.world.create_item(name)
                location.item.location = location

        enforce_shop_quality(self.multiworld, self.player)

        for section in range(1, 6):
            with self.subTest(section=section):
                items = [
                    self.multiworld.get_location(f"Shop Item {section}-{slot}", self.player).item
                    for slot in range(1, 7)
                ]
                self.assertEqual(sum(item.advancement or item.useful for item in items), 3)

    def test_shop_quality_preserves_locked_targets(self):
        for impossible in (False, True):
            with self.subTest(impossible=impossible):
                self.world_setup(seed=12345)
                for location in self.multiworld.get_locations(self.player):
                    location.locked = not location.name.startswith("Shop Item ")
                for section, quality_count in enumerate((2, 4, 3, 3, 3), 1):
                    for slot in range(1, 7):
                        location = self.multiworld.get_location(f"Shop Item {section}-{slot}", self.player)
                        name = (
                            "Player Bonus: Combo Limit +1" if slot <= quality_count else next(iter(WEAPON_DAMAGE_ITEMS))
                        )
                        location.item = self.world.create_item(name)
                        location.item.location = location
                        if section == 1 and (slot == 3 or impossible and slot > 3):
                            location.locked = True
                locked = self.multiworld.get_location("Shop Item 1-3", self.player)
                original = locked.item
                enforce_shop_quality(self.multiworld, self.player)
                if not impossible:
                    for section in range(1, 6):
                        items = [
                            self.multiworld.get_location(f"Shop Item {section}-{slot}", self.player).item
                            for slot in range(1, 7)
                        ]
                        self.assertEqual(sum(item.advancement or item.useful for item in items), 3)
                self.assertIs(locked.item, original)
                self.assertIs(original.location, locked)
                self.assertTrue(locked.locked)

    def test_shop_checks_items_rules_and_quality(self):
        shop = {
            f"Shop Item {section}-{slot}": 911600 + (section - 1) * 6 + slot
            for section in range(1, 6)
            for slot in range(1, 7)
        }
        self.assertEqual(
            {
                location.name: location.address
                for location in self.multiworld.get_locations()
                if location.name.startswith("Shop Item ")
            },
            shop,
        )
        self.assertEqual(self.world.item_name_to_id["Progressive Shop Unlock"], 910470)
        self.assertEqual(sum(item.name == "Progressive Shop Unlock" for item in self.multiworld.itempool), 4)
        unlock = self.world.create_item("Progressive Shop Unlock")
        self.assertTrue(unlock.advancement)
        for name in ("Shop Item 1-1", "Shop Item 5-6"):
            rule = self.multiworld.get_location(name, self.player).item_rule
            self.assertFalse(rule(unlock))
            self.assertFalse(rule(self.world.create_item("Enemy Spawn Trap")))
            self.assertFalse(rule(self.world.create_item("100 Aether")))
            self.assertFalse(rule(self.world.create_item("500 Aether")))
            self.assertTrue(rule(self.world.create_item(next(iter(WEAPON_DAMAGE_ITEMS)))))
        for seed in (1, 2, 3):
            with self.subTest(seed=seed):
                self.world_setup(seed=seed)
                distribute_items_restrictive(self.multiworld)
                self.world.finalize_multiworld()
                self.assertTrue(self.multiworld.can_beat_game())
                for section in range(1, 6):
                    items = [
                        self.multiworld.get_location(f"Shop Item {section}-{slot}", self.player).item
                        for slot in range(1, 7)
                    ]
                    self.assertGreaterEqual(sum(item.advancement or item.useful for item in items), 3)
                    self.assertFalse(any(item.trap or item.name == "Progressive Shop Unlock" for item in items))
        state = self.multiworld.get_all_state(False)
        for level in range(1, 5):
            state.prog_items[self.player]["Progressive Shop Unlock"] = level - 1
            self.assertFalse(self.multiworld.get_location(f"Shop Item {level + 1}-1", self.player).access_rule(state))
            state.prog_items[self.player]["Progressive Shop Unlock"] = level
            self.assertTrue(self.multiworld.get_location(f"Shop Item {level + 1}-1", self.player).access_rule(state))
