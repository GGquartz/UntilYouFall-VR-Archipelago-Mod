from .catalogs import FIXED_ITEMS, ITEMS_BY_KEY

HAND_ITEMS = {item["Name"]: item["Id"] for item in FIXED_ITEMS if item.get("Group") == "Hand"}
LEFT_HAND_ITEM = ITEMS_BY_KEY["LeftHand"]["Name"]
RIGHT_HAND_ITEM = ITEMS_BY_KEY["RightHand"]["Name"]


def missing_hand_item(starting_hands: int) -> str | None:
    if starting_hands == 1:
        return RIGHT_HAND_ITEM
    if starting_hands == 2:
        return LEFT_HAND_ITEM

    return None


def starting_hand_items(starting_hands: int) -> tuple[str, ...]:
    if starting_hands == 1:
        return (LEFT_HAND_ITEM,)
    if starting_hands == 2:
        return (RIGHT_HAND_ITEM,)

    return (LEFT_HAND_ITEM, RIGHT_HAND_ITEM)
