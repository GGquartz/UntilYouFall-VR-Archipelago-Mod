from BaseClasses import Item, MultiWorld

from .catalogs import LOCATIONS

SHOP_BASE_ID = LOCATIONS["ShopBaseId"]
SHOP_SECTIONS = LOCATIONS["ShopSections"]
SHOP_SLOTS = LOCATIONS["ShopSlotsPerSection"]
SHOP_PRICES = tuple(LOCATIONS["ShopPrices"])
SHOP_UNLOCK_ITEM_COUNT = SHOP_SECTIONS - 1
MIN_QUALITY_PER_SECTION = 3
SHOP_NAME_PREFIX = "Shop Item "


def shop_name(section: int, slot: int) -> str:
    return f"{SHOP_NAME_PREFIX}{section}-{slot}"


def shop_id(section: int, slot: int) -> int:
    return SHOP_BASE_ID + (section - 1) * SHOP_SLOTS + slot


def active_shop_locations() -> dict[str, int]:
    return {
        shop_name(section, slot): shop_id(section, slot)
        for section in range(1, SHOP_SECTIONS + 1)
        for slot in range(1, SHOP_SLOTS + 1)
    }


def is_quality(item: Item) -> bool:
    return item.advancement or item.useful


def enforce_shop_quality(multiworld: MultiWorld, player: int) -> None:
    """Every shop section has at least three useful or progression items to avoid useless shops"""
    from Fill import swap_location_item

    protected_shop_names = set()
    for section in range(1, SHOP_SECTIONS + 1):
        shop = [multiworld.get_location(shop_name(section, slot), player) for slot in range(1, SHOP_SLOTS + 1)]
        protected_shop_names.update(location.name for location in shop)
        missing = MIN_QUALITY_PER_SECTION - sum(is_quality(location.item) for location in shop)
        targets = [location for location in shop if not location.locked and not is_quality(location.item)]
        for target in targets[: max(missing, 0)]:
            source = next(
                (
                    location
                    for location in multiworld.get_locations(player)
                    if location.name not in protected_shop_names
                    and not location.locked
                    and location.item is not None
                    and location.item.useful
                    and not location.item.advancement
                    and target.can_fill(multiworld.state, location.item, False)
                    and location.can_fill(multiworld.state, target.item, False)
                ),
                None,
            )
            if source is None:
                break

            swap_location_item(source, target, check_locked=False)
