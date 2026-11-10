import json
import pkgutil
from typing import Any


def load_catalog(filename: str) -> Any:
    return json.loads(pkgutil.get_data(__package__, filename))


GAME_NAME = load_catalog("archipelago.json")["game"]
ITEM_DEFINITIONS = load_catalog("items.json")
FIXED_ITEMS = ITEM_DEFINITIONS["Items"]
ITEMS_BY_KEY = {item["Key"]: item for item in FIXED_ITEMS}
WEAPONS = load_catalog("weapons.json")
WEAPON_ABILITIES = load_catalog("weapon_abilities.json")
STARTING_TRAITS = load_catalog("starting_traits.json")
ENEMIES = load_catalog("enemies.json")
QUESTS = load_catalog("quests.json")
DIFFICULTIES = load_catalog("difficulties.json")
LOCATIONS = load_catalog("locations.json")
OPTION_RANGES = load_catalog("option_ranges.json")
