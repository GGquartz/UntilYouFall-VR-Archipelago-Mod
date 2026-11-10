"""Option combinations for Until You Fall slots: every value of the main options appears in at least one combination."""

import random

from harness import Slot
from worlds.until_you_fall.catalogs import WEAPONS
from worlds.until_you_fall.items import TRAP_ITEMS
from worlds.until_you_fall.options import StartingHands, StartingWeapon, WeaponSupers

TWO_HANDED_WEAPONS = {
    name
    for name, value in StartingWeapon.options.items()
    if value in {weapon["Id"] for weapon in WEAPONS if weapon["TwoHanded"]}
}
CHOICE_DIMENSIONS = {
    "goal": ["quests", "all_weapons", "runs_max_difficulty", "runs"],
    "goal_runs": [1, 5, 20],
    "quest_board": [True, False],
    "stage_reward_checks": [10, 20, 30],
    "trap_percent": [0, 10, 100],
    "starting_hands": sorted(StartingHands.options),
    "early_hand": [25, 50, 100],
    "weapon_supers": sorted(WeaponSupers.options),
    "starting_weapon": ["random", *(name for name, value in StartingWeapon.options.items() if value > 0)],
    "second_starting_weapon": ["none", "random"],
    "starting_difficulty": ["relaxed", "random"],
    "max_difficulty": ["relaxed", "nightmare", "relentless"],
    "death_link_mode": ["off", "hard", "soft", "trap", "deaths_door"],
    "required_quests": [[], ["Champion of the Runeguard", "Full Arsenal"]],
    "excluded_quests": [[], ["Speedrun", "Untouched"]],
    "trap_weights": [None, {TRAP_ITEMS[0]: 100}, {TRAP_ITEMS[0]: 0, TRAP_ITEMS[-1]: 50}],
}
COMMON_DIMENSIONS = {
    "accessibility": ["full", "minimal"],
    "progression_balancing": [0, 50],
}


def _repaired(options: dict) -> dict:
    """Only the combinations the world accepts: the Quests goal needs the board, a random start needs a harder maximum,
    a two-handed starting weapon needs both hands."""
    if options["starting_weapon"] in TWO_HANDED_WEAPONS:
        options["starting_hands"] = "both"
    if options["goal"] == "quests":
        options["quest_board"] = True
    if options["starting_difficulty"] == "random" and options["max_difficulty"] == "relaxed":
        options["max_difficulty"] = "nightmare"
    if options["trap_weights"] is None:
        del options["trap_weights"]
    return options


def option_combinations(seed: int = 0) -> list[Slot]:
    """One slot per value of every dimension, the other options are picked at random with a fixed seed."""
    chooser = random.Random(seed)
    dimensions = {**CHOICE_DIMENSIONS, **COMMON_DIMENSIONS}
    slots = []
    for name, values in dimensions.items():
        for value in values:
            picked = {other: chooser.choice(options) for other, options in dimensions.items()}
            picked[name] = value
            common = {key: picked.pop(key) for key in COMMON_DIMENSIONS}
            slots.append(Slot("Until You Fall", _repaired(picked), common))

    return slots
