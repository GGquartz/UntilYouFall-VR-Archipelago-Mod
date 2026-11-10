from typing import TYPE_CHECKING

from BaseClasses import Location

from .catalogs import QUESTS, WEAPONS
from .quest_locations import quest_location_name
from .stage_weapon_locations import STAGE_COUNT, location_name

if TYPE_CHECKING:
    from . import UntilYouFallWorld

RUN_EVENTS = {
    "runs": ("Clear a Run", "Run Cleared"),
    "runs_max_difficulty": ("Clear a Run On Max Difficulty", "Run Cleared On Max Difficulty"),
}

EVENT_LABELS = {"quests": ("Quest: ", "Quest Completed"), "all_weapons": ("Run With: ", "Run Cleared")}


def event_names(world: "UntilYouFallWorld", label: str) -> tuple[str, str]:
    prefix, item = EVENT_LABELS[world.options.goal.current_key]
    return prefix + label, item


def goal_targets(world: "UntilYouFallWorld") -> list[tuple[str, str, list[Location]]]:
    multiworld, player = world.multiworld, world.player
    goal = world.options.goal.current_key

    if goal == "quests":
        targets = []
        for index in world.quests:
            quest = QUESTS[index]
            locations = [
                multiworld.get_location(quest_location_name(quest, reward), player)
                for reward in range(1, quest["Tier"] + 1)
            ]
            targets.append(("Quest completion", quest["Name"], locations))

        return targets

    if goal == "all_weapons":
        return [
            (
                "Clear a run with all weapons",
                weapon["Name"],
                [multiworld.get_location(location_name(STAGE_COUNT, weapon["Name"]), player)],
            )
            for weapon in WEAPONS
        ]

    category = "Finish runs on max difficulty" if goal == "runs_max_difficulty" else "Finish runs"

    return [(category, "Victory", [multiworld.get_location(RUN_EVENTS[goal][0], player)])]
