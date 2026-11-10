from collections import Counter
from collections.abc import Callable, Iterable
from functools import partial
from typing import TYPE_CHECKING

from BaseClasses import CollectionState, Item, ItemClassification, Location
from worlds.generic.Rules import set_rule

from .catalogs import ENEMIES, QUESTS, WEAPONS
from .difficulty_logic import GLITCH_ITEM, stage_reward_need, upgrade_need
from .enemy_locations import first_kill_name
from .goal_requirements import RUN_EVENTS, event_names, goal_targets
from .hands import LEFT_HAND_ITEM, RIGHT_HAND_ITEM
from .items import (
    AETHER_BULK_ITEMS,
    DIFFICULTY_ITEMS,
    FILLER_NAMES,
    SHOP_UNLOCK_ITEM,
    WEAPON_ABILITY_ITEMS,
    WEAPON_UPGRADE_LEVEL_ITEM,
    UntilYouFallItem,
    difficulty_item_name,
)
from .locations import UntilYouFallLocation
from .quest_locations import TIER_UPGRADES, quest_location_name
from .shop_locations import SHOP_SECTIONS, SHOP_SLOTS, shop_name
from .stage_difficulty_locations import is_goal_check
from .stage_difficulty_locations import location_name as difficulty_location_name
from .stage_reward_locations import LATE_REWARD_MIN_UPGRADES, stage_reward_name
from .stage_weapon_locations import STAGE_COUNT, location_name, requires_both_hands

if TYPE_CHECKING:
    from . import UntilYouFallWorld

Rule = Callable[[CollectionState], bool]


def set_all_rules(world: "UntilYouFallWorld") -> None:
    """A lot of checks logic is based on weapon upgrade level - this makes the progression smoother; the checks are still accessible but you will never be required to do late stages or quests with no upgrades"""
    set_first_kill_rules(world)
    set_difficulty_stage_rules(world)
    set_weapon_stage_rules(world)
    set_stage_reward_rules(world)
    set_shop_rules(world)
    set_quest_rules(world)
    create_goal_events(world)
    world.multiworld.completion_condition[world.player] = lambda state: state.has("Victory", world.player)


def strong_enough(state: CollectionState, player: int, need: int) -> bool:
    return need == 0 or state.has(GLITCH_ITEM, player) or state.has(WEAPON_UPGRADE_LEVEL_ITEM, player, need)


def has_both_hands(state: CollectionState, player: int) -> bool:
    return state.has_all((LEFT_HAND_ITEM, RIGHT_HAND_ITEM), player)


def upgrades_rule(world: "UntilYouFallWorld", need: int) -> Rule:
    return lambda state: strong_enough(state, world.player, need)


def all_reachable_rule(locations: Iterable[Location]) -> Rule:
    locations = list(locations)

    return lambda state: all(location.can_reach(state) for location in locations)


def any_reachable_rule(locations: Iterable[Location]) -> Rule:
    locations = list(locations)

    return lambda state: any(location.can_reach(state) for location in locations)


def can_hold_reward(world: "UntilYouFallWorld", item: Item, forbidden_own_names: Iterable[str] = ()) -> bool:
    """Shared restriction of some of the reward locations: no traps and no plain filler from our world"""
    if item.trap:
        return False
    if item.player != world.player:
        return True

    return item.name not in FILLER_NAMES and item.name not in forbidden_own_names


def can_hold_stage_reward(world: "UntilYouFallWorld", late: bool, item: Item) -> bool:
    if not can_hold_reward(world, item, AETHER_BULK_ITEMS):
        return False

    return not (
        late
        and item.game == world.game
        and item.name in {WEAPON_UPGRADE_LEVEL_ITEM, SHOP_UNLOCK_ITEM, *DIFFICULTY_ITEMS}
    )


def can_hold_shop_item(world: "UntilYouFallWorld", item: Item) -> bool:
    return can_hold_reward(world, item, {SHOP_UNLOCK_ITEM, *AETHER_BULK_ITEMS})


def can_hold_quest_reward(world: "UntilYouFallWorld", allow_progression: bool, item: Item) -> bool:
    return can_hold_reward(world, item) and (allow_progression or not item.advancement)


def set_first_kill_rules(world: "UntilYouFallWorld") -> None:
    """Enemy sanity checks: by the upgrade level of the earliest stage where you can get that enemy"""
    for enemy in ENEMIES:
        location = world.get_location(first_kill_name(enemy))
        set_rule(location, upgrades_rule(world, upgrade_need(enemy["EarliestStage"], 0)))


def is_goal_check_location(world: "UntilYouFallWorld", stage: int, tier: int) -> bool:
    """Prevent check on Victory location"""
    options = world.options
    return is_goal_check(
        options.goal.current_key,
        options.goal_runs.value,
        options.starting_difficulty.value,
        options.max_difficulty.value,
        stage,
        tier,
    )


def difficulty_stage_rule(world: "UntilYouFallWorld", stage: int, tier: int) -> Rule:
    item_name = difficulty_item_name(tier)
    need = upgrade_need(stage, tier - world.options.starting_difficulty.value)

    return lambda state: state.has(item_name, world.player) and strong_enough(state, world.player, need)


def set_difficulty_stage_rules(world: "UntilYouFallWorld") -> None:
    """The 'Clear stage X on difficulty Y' checks: need the difficulty item and upgrade levels"""
    for tier in range(world.options.starting_difficulty.value + 1, world.options.max_difficulty.value + 1):
        for stage in range(1, STAGE_COUNT + 1):
            if is_goal_check_location(world, stage, tier):
                continue
            set_rule(
                world.get_location(difficulty_location_name(stage, tier)), difficulty_stage_rule(world, stage, tier)
            )


def weapon_stage_rule(world: "UntilYouFallWorld", weapon: dict, stage: int) -> Rule:
    weapon_name = weapon["Name"]
    both_hands = requires_both_hands(weapon)
    need = upgrade_need(stage, 0)
    player = world.player
    return lambda state: (
        state.has(weapon_name, player)
        and strong_enough(state, player, need)
        and (not both_hands or has_both_hands(state, player))
    )


def set_weapon_stage_rules(world: "UntilYouFallWorld") -> None:
    """The 'Clear stage X with weapon Y' checks: need the weapon, upgrade levels and both hands if required for the weapon"""
    for stage in range(1, STAGE_COUNT + 1):
        for weapon in WEAPONS:
            set_rule(world.get_location(location_name(stage, weapon["Name"])), weapon_stage_rule(world, weapon, stage))


def set_stage_reward_rules(world: "UntilYouFallWorld") -> None:
    """
    Stage Reward Checks: later rewards need higher upgrade levels
    These items cannot be placed here: traps, own filler (except for dmg +1 items), aether items
    """
    count = world.options.stage_reward_checks.value
    for index in range(1, count + 1):
        location = world.get_location(stage_reward_name(index))
        need = stage_reward_need(index, count)
        location.item_rule = partial(can_hold_stage_reward, world, need >= LATE_REWARD_MIN_UPGRADES)
        set_rule(location, upgrades_rule(world, need))


def set_shop_rules(world: "UntilYouFallWorld") -> None:
    """
    Shop items: progressive unlock
    These items cannot be placed here: traps, shop unlocks, own filler (except for dmg +1 items), aether items
    """
    for section in range(1, SHOP_SECTIONS + 1):
        for slot in range(1, SHOP_SLOTS + 1):
            location = world.get_location(shop_name(section, slot))
            location.item_rule = partial(can_hold_shop_item, world)
            if section > 1:
                set_rule(location, partial(state_has_shop_unlocks, world.player, section - 1))


def state_has_shop_unlocks(player: int, count: int, state: CollectionState) -> bool:
    return state.has(SHOP_UNLOCK_ITEM, player, count)


def quest_access_rule(world: "UntilYouFallWorld", quest: dict) -> Rule:
    player = world.player
    need = quest["Need"]
    tier_upgrades = TIER_UPGRADES[quest["Tier"]]
    quest_upgrades = need.get("Upgrades", 0)
    weapons = need.get("Weapons", ())
    any_weapons = need.get("AnyWeapons")
    any_count = need.get("AnyCount")
    weapon_count = need.get("WeaponCount")
    separate_abilities = world.options.weapon_supers.current_key == "separate_items"
    abilities = need.get("Abilities", ()) if separate_abilities else ()
    super_groups = ()
    both_hands = any(requires_both_hands(weapon) for weapon in WEAPONS if weapon["Name"] in weapons)
    if need.get("AllSupers"):
        both_hands = True
        super_groups = tuple(
            tuple(weapon["Name"] for weapon in WEAPONS if weapon["Super"] == name) for name in WEAPON_ABILITY_ITEMS
        )
        if separate_abilities:
            abilities = tuple(WEAPON_ABILITY_ITEMS)
    all_weapon_names = [weapon["Name"] for weapon in WEAPONS]

    return lambda state: (
        strong_enough(state, player, tier_upgrades)
        and state.has(WEAPON_UPGRADE_LEVEL_ITEM, player, quest_upgrades)
        and all(state.has(name, player) for name in weapons)
        and all(state.has(name, player) for name in abilities)
        and all(state.has_any(group, player) for group in super_groups)
        and (not both_hands or has_both_hands(state, player))
        and (not any_weapons or state.has_from_list(any_weapons, player, any_count))
        and (not weapon_count or state.has_from_list(all_weapon_names, player, weapon_count))
    )


def set_quest_rules(world: "UntilYouFallWorld") -> None:
    """
    Quest reward checks: I = 1 weapon upgrade, II = 3 weapon upgrades, III = 6 weapon upgrades
    Some quests have specific requirements for weapons, weapon supers etc.
    These items cannot be placed here: traps, own filler (except for dmg +1 items)
    """
    for index in world.quests:
        quest = QUESTS[index]
        access_rule = quest_access_rule(world, quest)
        # Don't place progression in this quest yet because you can do it only with specific weapons, todo is to check what weapons can do this and set the rules correctly
        allow_progression = not quest.get("NoProgressionRewards")
        for reward in range(1, quest["Tier"] + 1):
            location = world.get_location(quest_location_name(quest, reward))
            location.item_rule = partial(can_hold_quest_reward, world, allow_progression)
            set_rule(location, access_rule)


def run_goal_rule(world: "UntilYouFallWorld") -> Rule:
    options = world.options
    maximum = options.max_difficulty.value
    if options.goal.current_key == "runs_max_difficulty" and maximum > options.starting_difficulty.value:
        if is_goal_check_location(world, STAGE_COUNT, maximum):
            return difficulty_stage_rule(world, STAGE_COUNT, maximum)

        return any_reachable_rule([world.get_location(difficulty_location_name(STAGE_COUNT, maximum))])

    return any_reachable_rule(world.get_location(location_name(STAGE_COUNT, weapon["Name"])) for weapon in WEAPONS)


def create_goal_events(world: "UntilYouFallWorld") -> None:
    """
    Every goal creates event locations: one for run goals, multiple for weapon clear goals and quest goals
    Victory needs all events
    """
    menu = world.get_region("Menu")
    goal = world.options.goal.current_key
    if goal in RUN_EVENTS:
        location_name_, item_name = RUN_EVENTS[goal]
        steps = [(location_name_, item_name, run_goal_rule(world))]
    else:
        steps = []
        for _, label, locations in goal_targets(world):
            location_name_, item_name = event_names(world, label)
            steps.append((location_name_, item_name, all_reachable_rule(locations)))
    for location_name_, item_name, rule in steps:
        event = UntilYouFallLocation(world.player, location_name_, None, menu)
        event.access_rule = rule
        event.place_locked_item(UntilYouFallItem(item_name, ItemClassification.progression, None, world.player))
        menu.locations.append(event)

    needed = Counter(item_name for _, item_name, _ in steps)
    world.get_location("Victory").access_rule = lambda state: all(
        state.has(item_name, world.player, count) for item_name, count in needed.items()
    )
