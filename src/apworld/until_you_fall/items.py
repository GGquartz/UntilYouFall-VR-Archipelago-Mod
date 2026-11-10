from typing import NamedTuple

from BaseClasses import Item, ItemClassification

from .catalogs import (
    DIFFICULTIES,
    FIXED_ITEMS,
    GAME_NAME,
    ITEM_DEFINITIONS,
    ITEMS_BY_KEY,
    STARTING_TRAITS,
    WEAPON_ABILITIES,
    WEAPONS,
)

STARTING_TRAIT_ITEMS = {
    f"Starting Trait: {trait['Name']}": ITEM_DEFINITIONS["StartingTraitCopies"] for trait in STARTING_TRAITS
}
STARTING_TRAIT_ITEMS.update(
    {item["Name"]: item["Copies"] for item in FIXED_ITEMS if item.get("Group") == "StartingTraitUpgrade"}
)

WEAPON_ABILITY_ITEMS = {
    ability["Name"]: ability["Id"] for ability in sorted(WEAPON_ABILITIES, key=lambda ability: ability["Id"])
}

WEAPON_UPGRADE_LEVEL_ITEM = ITEMS_BY_KEY["ProgressiveWeaponUpgradeLevel"]["Name"]
WEAPON_UPGRADE_LEVEL_ITEM_COUNT = ITEMS_BY_KEY["ProgressiveWeaponUpgradeLevel"]["Copies"]

AETHER_FILLER_ITEM = ITEMS_BY_KEY["AetherFiller"]["Name"]
AETHER_FILLER_AMOUNT = ITEMS_BY_KEY["AetherFiller"]["Amount"]

AETHER_MULTIPLIER_ITEM = ITEMS_BY_KEY["ProgressiveAetherMultiplier"]["Name"]
AETHER_MULTIPLIER_ITEM_COUNT = ITEMS_BY_KEY["ProgressiveAetherMultiplier"]["Copies"]

SHOP_UNLOCK_ITEM = ITEMS_BY_KEY["ProgressiveShopUnlock"]["Name"]

FILLER_SHARES = tuple((item["Name"], item["Id"], item["FillerShare"]) for item in FIXED_ITEMS if "FillerShare" in item)
FILLER_NAMES = {name for name, _item_id, _share in FILLER_SHARES}


def difficulty_item_name(tier: int) -> str:
    return f"Difficulty: {DIFFICULTIES[tier]['Name']}"


DIFFICULTY_ITEMS = {}
DIFFICULTY_ITEM_TIERS = {}
for tier, difficulty in enumerate(DIFFICULTIES):
    name = difficulty_item_name(tier)
    DIFFICULTY_ITEMS[name] = difficulty["Id"]
    DIFFICULTY_ITEM_TIERS[name] = tier


def filler_counts(slots: int) -> dict[str, int]:
    counts = {name: slots * share // 100 for name, _item_id, share in FILLER_SHARES}
    order = sorted(FILLER_SHARES, key=lambda entry: (-(slots * entry[2] % 100), entry[1]))
    for name, _item_id, _share in order[: slots - sum(counts.values())]:
        counts[name] += 1

    return counts


TRAP_ITEMS = tuple(item["Name"] for item in FIXED_ITEMS if item.get("Group") == "Trap")


def trap_counts(total: int, weights: dict[str, int]) -> dict[str, int]:
    weights = {name: weights.get(name, 0) for name in TRAP_ITEMS}
    weight_sum = sum(weights.values())
    if total == 0:
        return dict.fromkeys(TRAP_ITEMS, 0)

    counts = {name: total * weight // weight_sum for name, weight in weights.items()}
    order = sorted(TRAP_ITEMS, key=lambda name: (-(total * weights[name] % weight_sum), TRAP_ITEMS.index(name)))
    for name in order[: total - sum(counts.values())]:
        counts[name] += 1

    return counts


AETHER_BULK_ITEMS = {
    item["Name"]: item["Copies"] for item in FIXED_ITEMS if item.get("Group") == "AetherBundle" and "Copies" in item
}

PLAYER_BONUS_ITEMS = {item["Name"]: item["Copies"] for item in FIXED_ITEMS if item.get("Group") == "PlayerBonus"}

DAMAGE_STATS = tuple(ITEM_DEFINITIONS["DamageStats"])
DAMAGE_ITEM_COPIES = ITEM_DEFINITIONS["DamageItemCopies"]


class ItemData(NamedTuple):
    code: int
    classification: ItemClassification


item_table = {item["Name"]: ItemData(item["Id"], ItemClassification[item["Classification"]]) for item in FIXED_ITEMS}
for name, item_id in DIFFICULTY_ITEMS.items():
    item_table[name] = ItemData(item_id, ItemClassification.progression)
for name, item_id in WEAPON_ABILITY_ITEMS.items():
    item_table[name] = ItemData(item_id, ItemClassification.progression)
for trait in STARTING_TRAITS:
    item_table[f"Starting Trait: {trait['Name']}"] = ItemData(trait["Id"], ItemClassification.useful)

TRAIT_SLOT_ITEMS = {}
WEAPON_DAMAGE_ITEMS = {}
for weapon in WEAPONS:
    item_table[weapon["Name"]] = ItemData(weapon["Id"], ItemClassification.progression)
    trait_slot_name = f"Extra Trait Slot: {weapon['Name']}"
    TRAIT_SLOT_ITEMS[trait_slot_name] = weapon["TraitSlotItemId"]
    item_table[trait_slot_name] = ItemData(weapon["TraitSlotItemId"], ItemClassification.useful)
    if weapon.get("NoDamageItems"):
        continue
    for offset, stat in enumerate(DAMAGE_STATS):
        item_name = f"{weapon['Name']} {stat} Damage +1"
        item_id = weapon["DamageItemId"] + offset
        item_table[item_name] = ItemData(item_id, ItemClassification.filler)
        WEAPON_DAMAGE_ITEMS[item_name] = DAMAGE_ITEM_COPIES


class UntilYouFallItem(Item):
    game = GAME_NAME
