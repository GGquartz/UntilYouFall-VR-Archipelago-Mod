from collections import Counter
from typing import NoReturn

from Options import OptionError

from .catalogs import WEAPONS
from .hands import HAND_ITEMS
from .items import DIFFICULTY_ITEM_TIERS, TRAIT_SLOT_ITEMS, TRAP_ITEMS
from .options import UntilYouFallOptions
from .quest_locations import quest_selection_error

MAX_TRAP_WEIGHT = 100


def fail(message: str) -> NoReturn:
    raise OptionError("Until You Fall: " + message)


def validate_quest_selection(options: UntilYouFallOptions) -> None:
    problem = quest_selection_error(options.required_quests.value, options.excluded_quests.value)
    if problem:
        fail(problem)


def validate_goal(options: UntilYouFallOptions) -> None:
    if options.goal.current_key == "quests" and not options.quest_board:
        fail("Quests goal requires Quest Board to be enabled")


def validate_trap_weights(options: UntilYouFallOptions) -> None:
    weights = options.trap_weights.value
    if any(
        not isinstance(weight, int) or isinstance(weight, bool) or not 0 <= weight <= MAX_TRAP_WEIGHT
        for weight in weights.values()
    ):
        fail(f"Trap weights must be whole numbers from 0 to {MAX_TRAP_WEIGHT}")

    if options.trap_percent.value and not any(weights.get(name, 0) for name in TRAP_ITEMS):
        fail("Trap weights needs at least one trap above 0 while Trap % is above 0")


def validate_random_starting_difficulty(options: UntilYouFallOptions) -> None:
    if options.max_difficulty.value == 0:
        fail("Random starting difficulty requires max difficulty above Relaxed")


def validate_starting_difficulty(options: UntilYouFallOptions) -> None:
    if options.starting_difficulty.value > options.max_difficulty.value:
        fail("Starting difficulty can not be harder than max difficulty")


def validate_start_inventory(options: UntilYouFallOptions, requested: Counter) -> None:
    for name, tier in DIFFICULTY_ITEM_TIERS.items():
        if requested.get(name, 0) and tier > options.max_difficulty.value:
            fail(f"{name} is outside the max difficulty range")

    if any(requested.get(name, 0) for name in HAND_ITEMS):
        fail("Hand start_inventory is not supported, use starting_hands")

    single_copy = [*(weapon["Name"] for weapon in WEAPONS), *TRAIT_SLOT_ITEMS]
    if any(requested.get(name, 0) > 1 for name in single_copy):
        fail("Weapons and extra trait slots can only be added once in start_inventory")
