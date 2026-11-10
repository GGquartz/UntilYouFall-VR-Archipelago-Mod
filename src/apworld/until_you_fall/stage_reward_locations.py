from collections.abc import Iterable

from BaseClasses import CollectionState, Item, Location, MultiWorld

from .catalogs import LOCATIONS, OPTION_RANGES
from .items import AETHER_BULK_ITEMS
from .shop_locations import SHOP_NAME_PREFIX

STAGE_REWARD_BASE_ID = LOCATIONS["StageRewardBaseId"]
MAX_STAGE_REWARD_CHECKS = OPTION_RANGES["StageRewardChecks"]["Maximum"]
STAGE_REWARD_NAME_PREFIX = "Stage Reward "

LATE_REWARD_MIN_UPGRADES = 5
LATE_REWARD_START_NUMERATOR = 2
LATE_REWARD_START_DENOMINATOR = 3
LATE_PROGRESSION_SHARE_DENOMINATOR = 5


def stage_reward_name(index: int) -> str:
    return f"{STAGE_REWARD_NAME_PREFIX}{index}"


def stage_reward_id(index: int) -> int:
    return STAGE_REWARD_BASE_ID + index


def active_stage_rewards(count: int) -> dict[str, int]:
    return {stage_reward_name(index): stage_reward_id(index) for index in range(1, count + 1)}


def _seed_still_playable(multiworld: MultiWorld) -> bool:
    state = CollectionState(multiworld)
    state.sweep_for_advancements()

    return multiworld.has_beaten_game(state) and all(
        location.can_reach(state)
        for location in multiworld.get_locations()
        if multiworld.worlds[location.player].options.accessibility.current_key != "minimal"
    )


def _item_category(item: Item) -> str:
    if item.advancement:
        return "progression"
    if item.name.endswith("Damage +1"):
        return "damage"

    return "useful" if item.useful else "filler"


def _replace_rewards_with_other_items(
    multiworld: MultiWorld,
    player: int,
    rewards_to_replace: Iterable[Location],
    shortfalls: dict[str, int],
    untouchable_names: set[str],
) -> None:
    from Fill import swap_location_item

    for reward in rewards_to_replace:
        donors = [
            location
            for location in multiworld.get_locations()
            if location.name not in untouchable_names
            and not location.name.startswith(SHOP_NAME_PREFIX)
            and not location.locked
            and location.item is not None
            and location.address is not None
            and not location.item.advancement
            and not location.item.trap
        ]
        donors.sort(key=lambda location: (location.player != player, -shortfalls.get(_item_category(location.item), 0)))
        for donor in donors:
            if not (
                reward.can_fill(multiworld.state, donor.item, False)
                and donor.can_fill(multiworld.state, reward.item, False)
            ):
                continue

            swap_location_item(donor, reward, check_locked=False)
            if _seed_still_playable(multiworld):
                category = _item_category(reward.item)
                shortfalls[category] = shortfalls.get(category, 0) - 1
                break

            swap_location_item(donor, reward, check_locked=False)


def enforce_stage_reward_pacing(multiworld: MultiWorld, player: int, count: int) -> None:
    """Without this stage rewards hold too much progression, we make sure the item categories are balanced and also that the player is not forced to grind rewards"""
    rewards = [multiworld.get_location(stage_reward_name(index), player) for index in range(1, count + 1)]
    reward_names = {location.name for location in rewards}
    other_locations = [
        location
        for location in multiworld.get_locations(player)
        if location.name not in reward_names
        and not location.name.startswith(SHOP_NAME_PREFIX)
        and not location.locked
        and location.item is not None
        and location.address is not None
        and not location.item.trap
        and location.item.name not in AETHER_BULK_ITEMS
    ]
    categories = ("progression", "useful", "damage", "filler")
    wanted_counts = {
        category: round(
            count
            * sum(_item_category(location.item) == category for location in other_locations)
            / max(len(other_locations), 1)
        )
        for category in categories
    }
    wanted_counts["damage"] += wanted_counts["filler"]
    wanted_counts["filler"] = 0

    def count_shortfalls() -> dict[str, int]:
        return {
            category: wanted_counts[category] - sum(_item_category(location.item) == category for location in rewards)
            for category in categories
        }

    late_reward_start_index = -(-LATE_REWARD_START_NUMERATOR * count // LATE_REWARD_START_DENOMINATOR)
    late_rewards = rewards[late_reward_start_index:]
    allowed_late_progression_count = len(late_rewards) // LATE_PROGRESSION_SHARE_DENOMINATOR
    excess_late_progression = [
        location for location in late_rewards if location.item.advancement and not location.locked
    ][allowed_late_progression_count:]

    _replace_rewards_with_other_items(
        multiworld, player, excess_late_progression, count_shortfalls(), {location.name for location in late_rewards}
    )
    shortfalls = count_shortfalls()
    surplus_rewards = [
        location
        for location in rewards
        if not location.locked
        and shortfalls[_item_category(location.item)] < 0
        and _item_category(location.item) != "progression"
    ]
    progression_rewards = [location for location in rewards if location.item.advancement and not location.locked]
    rewards_to_replace = progression_rewards[wanted_counts["progression"] :]
    for category in categories:
        if category == "progression":
            continue
        rewards_to_replace += [location for location in surplus_rewards if _item_category(location.item) == category][
            : max(-shortfalls[category], 0)
        ]

    _replace_rewards_with_other_items(multiworld, player, rewards_to_replace, shortfalls, reward_names)
