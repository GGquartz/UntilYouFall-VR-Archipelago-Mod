import importlib
import importlib.util
import inspect
import json
import subprocess
import sys
import typing
from importlib.machinery import ModuleSpec, SourceFileLoader
from pathlib import Path

from Options import Choice, ItemDict, ItemSet, LocationSet, OptionDict, OptionSet, Range, Toggle

root = Path(__file__).resolve().parents[1]
subprocess.run(
    [sys.executable, str(root / "dev/export-game-data.py"), *(["--check"] if "--check" in sys.argv else [])],
    check=True,
)
target = root / "src" / "launcher" / "Resources" / "options.json"
general_target = target.with_name("general-options.json")
items_target = root / "src" / "launcher" / "Resources" / "items.json"

package_dir = root / "src" / "apworld" / "until_you_fall"
package_spec = ModuleSpec(
    "uyf_pkg",
    SourceFileLoader("uyf_pkg", str(package_dir / "__init__.py")),
    origin=str(package_dir / "__init__.py"),
    is_package=True,
)
package_spec.has_location = True
package_spec.submodule_search_locations = [str(package_dir)]
package = importlib.util.module_from_spec(package_spec)
sys.modules["uyf_pkg"] = package
items_module = importlib.import_module("uyf_pkg.items")
hands_module = importlib.import_module("uyf_pkg.hands")
shop_module = importlib.import_module("uyf_pkg.shop_locations")
UntilYouFallOptions = importlib.import_module("uyf_pkg.options").UntilYouFallOptions
quest_module = importlib.import_module("uyf_pkg.quest_locations")
WEAPONS = importlib.import_module("uyf_pkg.catalogs").WEAPONS


def help_text(option):
    text = " ".join(inspect.cleandoc(option.__doc__ or "").split())
    return text.replace("; ", ";\n")


def option_entry(key, option):
    entry = {"key": key, "display": option.display_name, "help": help_text(option)}
    if issubclass(option, Choice):
        entry["type"] = "choice"
        entry["choices"] = [option.name_lookup[value] for value in sorted(option.name_lookup)]
        default = option.name_lookup.get(option.default, option.default)
        entry["default"] = str(default)
        if "random_valid" in entry["choices"]:
            entry["choices"].remove("random_valid")
            entry["choices"].append("random")
        if key == "starting_weapon":
            entry["both_hands_choices"] = [
                option.name_lookup[weapon["Id"]] for weapon in WEAPONS if weapon["TwoHanded"]
            ]
    elif issubclass(option, Range):
        entry.update(type="range", min=option.range_start, max=option.range_end, default=option.default)
    elif issubclass(option, ItemSet):
        entry.update(type="items", default=sorted(option.default))
    elif issubclass(option, LocationSet):
        entry.update(type="list", default=sorted(option.default))
    elif issubclass(option, ItemDict):
        entry.update(type="inventory", default=dict(option.default))
    elif issubclass(option, OptionSet):
        tiers = {quest["Name"]: quest["Tier"] for quest in quest_module.QUESTS}
        totals = {tier: sum(quest["Tier"] == tier for quest in quest_module.QUESTS) for tier in (1, 2, 3)}
        per_tier = quest_module.QUESTS_PER_TIER
        limits = {
            str(tier): per_tier if key == "required_quests" else total - per_tier for tier, total in totals.items()
        }
        entry.update(
            type="set",
            default="",
            tier_limits=limits,
            values=[{"name": name, "tier": tiers[name]} for name in sorted(tiers)],
        )
    elif issubclass(option, OptionDict):
        entry.update(type="weights", default=dict(option.default), keys=list(option.default))
    elif issubclass(option, Toggle):
        entry.update(type="toggle", default=bool(option.default))
    else:
        raise SystemExit(f"Unsupported option type: {key}")
    return entry


own = [
    key
    for key in UntilYouFallOptions.__dataclass_fields__
    if key in UntilYouFallOptions.__annotations__ and key != "start_inventory_from_pool"
]
hints = typing.get_type_hints(UntilYouFallOptions)
options = [option_entry(key, hints[key]) for key in own]
text = json.dumps(options, indent=2, ensure_ascii=False) + "\n"
general_options = []
for key in (
    "accessibility",
    "progression_balancing",
    "local_items",
    "non_local_items",
    "start_inventory_from_pool",
    "start_hints",
    "start_location_hints",
    "priority_locations",
    "exclude_locations",
):
    entry = option_entry(key, hints[key])
    entry["help"] = inspect.cleandoc(hints[key].__doc__ or "")
    general_options.append(entry)
general_text = json.dumps(general_options, indent=2, ensure_ascii=False) + "\n"


def item_entries():
    pool = {
        items_module.WEAPON_UPGRADE_LEVEL_ITEM: items_module.WEAPON_UPGRADE_LEVEL_ITEM_COUNT,
        items_module.AETHER_MULTIPLIER_ITEM: items_module.AETHER_MULTIPLIER_ITEM_COUNT,
        items_module.SHOP_UNLOCK_ITEM: shop_module.SHOP_UNLOCK_ITEM_COUNT,
        **items_module.AETHER_BULK_ITEMS,
        **items_module.PLAYER_BONUS_ITEMS,
        **items_module.WEAPON_DAMAGE_ITEMS,
        **items_module.STARTING_TRAIT_ITEMS,
    }
    entries = []

    def can_start_with(name, classification):
        return (
            name not in hands_module.HAND_ITEMS
            and name not in items_module.TRAP_ITEMS
            and (classification != "filler" or name in items_module.WEAPON_DAMAGE_ITEMS)
        )

    for name, data in sorted(items_module.item_table.items(), key=lambda entry: entry[1].code):
        classification = data.classification.name
        if classification not in ("trap", "progression", "useful"):
            classification = "filler"
        default_max = 10 if name in items_module.FILLER_NAMES or name in items_module.TRAP_ITEMS else 1
        entries.append(
            {
                "name": name,
                "class": classification,
                "maxCopies": pool.get(name, default_max),
                "canStartWith": can_start_with(name, classification),
            }
        )
    return entries


items_text = json.dumps(item_entries(), indent=2, ensure_ascii=False) + "\n"
if "--check" in sys.argv:
    if (
        target.read_text(encoding="utf-8") != text
        or not general_target.exists()
        or general_target.read_text(encoding="utf-8") != general_text
        or items_target.read_text(encoding="utf-8") != items_text
    ):
        raise SystemExit("options.json, general-options.json or items.json is stale")
    print("options.json, general-options.json and items.json are current")
else:
    target.write_text(text, encoding="utf-8")
    general_target.write_text(general_text, encoding="utf-8")
    items_target.write_text(items_text, encoding="utf-8")
    print(f"wrote {len(options)} options and {len(items_text.splitlines())} item lines")
