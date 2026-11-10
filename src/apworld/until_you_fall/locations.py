from typing import TYPE_CHECKING

from BaseClasses import ItemClassification, Location, Region

from .catalogs import GAME_NAME, QUESTS
from .enemy_locations import first_kill_locations
from .items import UntilYouFallItem
from .quest_locations import all_quest_locations, quest_location_name
from .shop_locations import active_shop_locations
from .stage_difficulty_locations import DIFFICULTY_NAMES, active_stage_difficulties
from .stage_reward_locations import MAX_STAGE_REWARD_CHECKS, active_stage_rewards
from .stage_weapon_locations import stage_weapon_locations

if TYPE_CHECKING:
    from . import UntilYouFallWorld

STAGE_WEAPON_LOCATIONS = stage_weapon_locations()

LOCATION_NAME_TO_ID = {
    **STAGE_WEAPON_LOCATIONS,
    **active_stage_difficulties(0, len(DIFFICULTY_NAMES) - 1),
    **first_kill_locations(),
    **active_stage_rewards(MAX_STAGE_REWARD_CHECKS),
    **active_shop_locations(),
    **all_quest_locations(),
}


class UntilYouFallLocation(Location):
    game = GAME_NAME


def active_locations(world: "UntilYouFallWorld") -> dict[str, int]:
    options = world.options
    locations = {**STAGE_WEAPON_LOCATIONS, **first_kill_locations()}
    locations.update(
        active_stage_difficulties(
            options.starting_difficulty.value,
            options.max_difficulty.value,
            options.goal.current_key,
            options.goal_runs.value,
        )
    )
    locations.update(active_stage_rewards(options.stage_reward_checks.value))
    locations.update(active_shop_locations())

    quest_locations = all_quest_locations()
    for index in world.quests:
        quest = QUESTS[index]
        for reward in range(1, quest["Tier"] + 1):
            name = quest_location_name(quest, reward)
            locations[name] = quest_locations[name]

    return locations


def create_all_locations(world: "UntilYouFallWorld", region: Region) -> None:
    for name, code in active_locations(world).items():
        region.locations.append(UntilYouFallLocation(world.player, name, code, region))

    victory = UntilYouFallLocation(world.player, "Victory", None, region)
    victory.place_locked_item(UntilYouFallItem("Victory", ItemClassification.progression, None, world.player))
    region.locations.append(victory)
