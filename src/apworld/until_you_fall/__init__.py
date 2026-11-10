from collections import Counter
from typing import Any

from BaseClasses import Item, ItemClassification, Location, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .catalogs import ENEMIES, GAME_NAME, WEAPONS, load_catalog
from .difficulty_logic import GLITCH_ITEM
from .hand_placement import enforce_early_hand
from .hands import missing_hand_item, starting_hand_items
from .item_pool import create_all_items
from .items import (
    AETHER_FILLER_AMOUNT,
    AETHER_FILLER_ITEM,
    AETHER_MULTIPLIER_ITEM_COUNT,
    DIFFICULTY_ITEMS,
    TRAP_ITEMS,
    WEAPON_ABILITY_ITEMS,
    UntilYouFallItem,
    difficulty_item_name,
    item_table,
)
from .locations import LOCATION_NAME_TO_ID, create_all_locations
from .options import SecondStartingWeapon, StartingWeapon, UntilYouFallOptions
from .quest_locations import pick_quests
from .rules import set_all_rules
from .shop_locations import enforce_shop_quality
from .stage_reward_locations import STAGE_REWARD_NAME_PREFIX, enforce_stage_reward_pacing
from .stage_weapon_locations import STAGE_COUNT, WEAPONS_BY_NAME
from .starting_loadout import resolve_starting_weapons
from .validation import (
    validate_goal,
    validate_quest_selection,
    validate_random_starting_difficulty,
    validate_start_inventory,
    validate_starting_difficulty,
    validate_trap_weights,
)


class UntilYouFallWebWorld(WebWorld):
    theme = "ocean"
    tutorials = [
        Tutorial(
            f"{GAME_NAME} Setup Guide",
            f"A guide to installing and connecting {GAME_NAME} for Archipelago",
            "English",
            "setup_en.md",
            "setup/en",
            load_catalog("archipelago.json")["authors"],
        )
    ]


class UntilYouFallWorld(World):
    """Physically active VR sword-fighting roguelite: fight through neon stages, grow stronger, fall, rise again."""

    game = GAME_NAME
    web = UntilYouFallWebWorld()
    glitches_item_name = GLITCH_ITEM
    ut_can_gen_without_yaml = True
    options_dataclass = UntilYouFallOptions
    options: UntilYouFallOptions
    item_name_groups = {
        "Weapons": {weapon["Name"] for weapon in WEAPONS},
        "Traps": set(TRAP_ITEMS),
        "Weapon Abilities": set(WEAPON_ABILITY_ITEMS),
        "Difficulties": set(DIFFICULTY_ITEMS),
    }
    item_name_to_id = {name: data.code for name, data in item_table.items()}
    location_name_to_id = LOCATION_NAME_TO_ID

    quests: list[int]
    starting_weapons: list[str]

    def generate_early(self) -> None:
        passthrough = getattr(self.multiworld, "re_gen_passthrough", {}).get(self.game)
        if passthrough:
            self._apply_tracker_slot_data(passthrough)
        else:
            self.quests = self._pick_quests()

        validate_goal(self.options)
        validate_trap_weights(self.options)
        if not passthrough:
            self._resolve_random_starting_difficulty()
        validate_starting_difficulty(self.options)
        requested = Counter(self.options.start_inventory.value) + Counter(self.options.start_inventory_from_pool.value)
        validate_start_inventory(self.options, requested)
        self._resolve_starting_weapons()
        self._grant_starting_items(requested)

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        create_all_locations(self, menu)
        self.multiworld.regions.append(menu)

    def create_items(self) -> None:
        create_all_items(self)

    def set_rules(self) -> None:
        set_all_rules(self)

    def fill_hook(
        self,
        progitempool: list[Item],
        usefulitempool: list[Item],
        filleritempool: list[Item],
        fill_locations: list[Location],
    ) -> None:
        fill_locations.sort(
            key=lambda location: (
                location.player != self.player or not location.name.startswith(STAGE_REWARD_NAME_PREFIX)
            )
        )

    def finalize_multiworld(self) -> None:
        enforce_shop_quality(self.multiworld, self.player)
        enforce_stage_reward_pacing(self.multiworld, self.player, self.options.stage_reward_checks.value)
        hand_name = missing_hand_item(self.options.starting_hands.value)
        if hand_name:
            enforce_early_hand(self.multiworld, self.player, hand_name, self.options.early_hand.value)

    def create_item(self, name: str) -> UntilYouFallItem:
        if name == GLITCH_ITEM:
            return UntilYouFallItem(name, ItemClassification.progression, None, self.player)

        data = item_table[name]
        return UntilYouFallItem(name, data.classification, data.code, self.player)

    def get_filler_item_name(self) -> str:
        return AETHER_FILLER_ITEM

    def _pick_quests(self) -> list[int]:
        if not self.options.quest_board:
            return []

        validate_quest_selection(self.options)

        return pick_quests(self.random.sample, self.options.required_quests.value, self.options.excluded_quests.value)

    def _resolve_random_starting_difficulty(self) -> None:
        if self.options.starting_difficulty.is_random:
            validate_random_starting_difficulty(self.options)
            self.options.starting_difficulty.value = self.random.randrange(self.options.max_difficulty.value)

    def _resolve_starting_weapons(self) -> None:
        first, second = self.options.starting_weapon, self.options.second_starting_weapon
        weapon_ids = resolve_starting_weapons(
            first.value,
            first.is_random,
            second.value,
            second.is_random,
            self.options.starting_hands.value,
            StartingWeapon.weapon_ids(),
            self.random.choice,
            {weapon["Id"] for weapon in WEAPONS if weapon["TwoHanded"]},
            SecondStartingWeapon.weapon_ids(),
        )

        first.value = weapon_ids[0]
        second.value = weapon_ids[1] if len(weapon_ids) == 2 else 0
        names = {weapon["Id"]: weapon["Name"] for weapon in WEAPONS}
        self.starting_weapons = [names[weapon_id] for weapon_id in weapon_ids]

    def _grant_starting_items(self, requested: Counter) -> None:
        for name in self.starting_weapons:
            if not requested.get(name, 0):
                self.multiworld.push_precollected(self.create_item(name))

        for hand_name in starting_hand_items(self.options.starting_hands.value):
            self.multiworld.push_precollected(self.create_item(hand_name))

        starting_difficulty = difficulty_item_name(self.options.starting_difficulty.value)
        self.options.start_inventory_from_pool.value.pop(starting_difficulty, None)
        if not self.options.start_inventory.value.get(starting_difficulty, 0):
            self.multiworld.push_precollected(self.create_item(starting_difficulty))

    def fill_slot_data(self) -> dict[str, Any]:
        return {
            "stage_count": STAGE_COUNT,
            "enemy_sanity": True,
            "enemy_count": len(ENEMIES),
            "weapon_unlocks": True,
            "weapon_hints": True,
            "starting_weapons": self.starting_weapons,
            "starting_hands": self.options.starting_hands.current_key,
            "extra_trait_slots": True,
            "progressive_weapon_upgrade_level": True,
            "aether_filler": True,
            "aether_filler_amount": AETHER_FILLER_AMOUNT,
            "progressive_aether_multiplier": True,
            "progressive_aether_multiplier_count": AETHER_MULTIPLIER_ITEM_COUNT,
            "weapon_supers": self.options.weapon_supers.current_key,
            "player_bonuses": True,
            "stage_reward_checks": self.options.stage_reward_checks.value,
            "trap_percent": self.options.trap_percent.value,
            "trap_effect_length": self.options.trap_effect_length.value,
            "starting_difficulty": self.options.starting_difficulty.value,
            "max_difficulty": self.options.max_difficulty.value,
            "death_link": self.options.death_link_mode.current_key != "off",
            "death_link_mode": self.options.death_link_mode.current_key,
            "goal": self.options.goal.current_key,
            "goal_runs": self.options.goal_runs.value,
            "quests": self.quests,
            "quest_hint_cost": self.options.quest_hint_cost.value,
        }

    @staticmethod
    def interpret_slot_data(slot_data: dict[str, Any]) -> dict[str, Any]:
        return slot_data

    def _apply_tracker_slot_data(self, data: dict[str, Any]) -> None:
        options = self.options
        for name in ("starting_hands", "weapon_supers"):
            option = getattr(options, name)
            option.value = type(option).options[data[name]]
        options.stage_reward_checks.value = data["stage_reward_checks"]
        options.trap_percent.value = data["trap_percent"]
        options.trap_effect_length.value = data["trap_effect_length"]
        options.starting_difficulty.value = data["starting_difficulty"]
        options.max_difficulty.value = data["max_difficulty"]
        options.quest_board.value = bool(data["quests"])
        options.quest_hint_cost.value = data.get("quest_hint_cost", type(options.quest_hint_cost).default)
        options.goal.value = type(options.goal).options[data["goal"]]
        options.goal_runs.value = data["goal_runs"]
        first_name = data["starting_weapons"][0]
        options.starting_weapon.value = WEAPONS_BY_NAME[first_name]["Id"]
        second_names = data["starting_weapons"][1:]
        if second_names:
            options.second_starting_weapon.value = WEAPONS_BY_NAME[second_names[0]]["Id"]
        else:
            options.second_starting_weapon.value = 0
        self.quests = data["quests"]
