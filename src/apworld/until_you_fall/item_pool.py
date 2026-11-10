from typing import TYPE_CHECKING

from BaseClasses import Item

from .catalogs import WEAPONS
from .hands import missing_hand_item
from .items import (
    AETHER_BULK_ITEMS,
    AETHER_MULTIPLIER_ITEM,
    AETHER_MULTIPLIER_ITEM_COUNT,
    DIFFICULTY_ITEM_TIERS,
    PLAYER_BONUS_ITEMS,
    SHOP_UNLOCK_ITEM,
    STARTING_TRAIT_ITEMS,
    TRAIT_SLOT_ITEMS,
    TRAP_ITEMS,
    WEAPON_ABILITY_ITEMS,
    WEAPON_DAMAGE_ITEMS,
    WEAPON_UPGRADE_LEVEL_ITEM,
    WEAPON_UPGRADE_LEVEL_ITEM_COUNT,
    filler_counts,
    trap_counts,
)
from .shop_locations import SHOP_UNLOCK_ITEM_COUNT

if TYPE_CHECKING:
    from . import UntilYouFallWorld


def create_copies(world: "UntilYouFallWorld", catalog: dict[str, int]) -> list[Item]:
    items = []
    for name, copies in catalog.items():
        items += [world.create_item(name) for _ in range(copies)]

    return items


def starting_item_names(world: "UntilYouFallWorld") -> set[str]:
    precollected = {item.name for item in world.multiworld.precollected_items[world.player]}

    return precollected | {name for name, count in world.options.start_inventory.value.items() if count}


def create_fixed_items(world: "UntilYouFallWorld") -> list[Item]:
    options = world.options
    starting = starting_item_names(world)
    hand_name = missing_hand_item(options.starting_hands.value)

    weapons = [world.create_item(weapon["Name"]) for weapon in WEAPONS if weapon["Name"] not in starting]
    hands = [world.create_item(hand_name)] if hand_name else []
    trait_slots = [world.create_item(name) for name in TRAIT_SLOT_ITEMS if name not in starting]
    upgrade_levels = create_copies(world, {WEAPON_UPGRADE_LEVEL_ITEM: WEAPON_UPGRADE_LEVEL_ITEM_COUNT})
    multipliers = create_copies(world, {AETHER_MULTIPLIER_ITEM: AETHER_MULTIPLIER_ITEM_COUNT})
    abilities = []
    if options.weapon_supers.current_key == "separate_items":
        abilities = [world.create_item(name) for name in WEAPON_ABILITY_ITEMS]
    difficulties = [
        world.create_item(name)
        for name, tier in DIFFICULTY_ITEM_TIERS.items()
        if tier <= options.max_difficulty.value and tier != options.starting_difficulty.value
    ]

    return (
        create_copies(world, {SHOP_UNLOCK_ITEM: SHOP_UNLOCK_ITEM_COUNT})
        + weapons
        + hands
        + trait_slots
        + upgrade_levels
        + multipliers
        + create_copies(world, AETHER_BULK_ITEMS)
        + abilities
        + create_copies(world, PLAYER_BONUS_ITEMS)
        + difficulties
        + create_copies(world, WEAPON_DAMAGE_ITEMS)
        + create_copies(world, STARTING_TRAIT_ITEMS)
    )


def create_traps(world: "UntilYouFallWorld", trap_count: int) -> list[Item]:
    counts = trap_counts(trap_count, world.options.trap_weights.value)
    traps = []
    for copy_number in range(max(counts.values())):
        for name in TRAP_ITEMS:
            if copy_number < counts[name]:
                traps.append(world.create_item(name))

    return traps


def create_filler(world: "UntilYouFallWorld", filler_count: int) -> list[Item]:
    items = []
    for name, count in filler_counts(filler_count).items():
        items += [world.create_item(name) for _ in range(count)]

    return items


def create_all_items(world: "UntilYouFallWorld") -> None:
    items = create_fixed_items(world)
    free_slots = len(world.multiworld.get_unfilled_locations(world.player)) - len(items)

    trap_count = round(free_slots * world.options.trap_percent.value / 100)
    items += create_traps(world, trap_count)
    items += create_filler(world, free_slots - trap_count)

    world.multiworld.itempool.extend(items)
