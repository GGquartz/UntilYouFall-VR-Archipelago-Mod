from collections.abc import Callable, Iterable

from .catalogs import LOCATIONS, QUESTS

QUEST_BASE_ID = LOCATIONS["QuestBaseId"]
QUESTS_PER_TIER = LOCATIONS["QuestsPerTier"]
TIER_UPGRADES = {1: 1, 2: 3, 3: 6}


def quest_location_name(quest: dict, reward: int) -> str:
    return f"Quest: {quest['Name']} - Reward {reward}"


def quest_id(index: int, reward: int) -> int:
    return QUEST_BASE_ID + index * LOCATIONS["QuestRewardStride"] + reward


def all_quest_locations() -> dict[str, int]:
    return {
        quest_location_name(quest, reward): quest_id(index, reward)
        for index, quest in enumerate(QUESTS)
        for reward in range(1, quest["Tier"] + 1)
    }


QUEST_NAMES = sorted(quest["Name"] for quest in QUESTS)
TIER_LABELS = {1: "Easy", 2: "Medium", 3: "Hard"}


def _by_name(names: Iterable[str]) -> dict[str, dict]:
    known = {quest["Name"].casefold(): quest for quest in QUESTS}

    return {name.casefold(): known[name.casefold()] for name in names if name.casefold() in known}


def quest_selection_error(required: Iterable[str], excluded: Iterable[str]) -> str | None:
    required, excluded = _by_name(required), _by_name(excluded)
    both = sorted(required[key]["Name"] for key in required if key in excluded)
    if both:
        return "Quests cannot be both required and excluded: " + ", ".join(both)

    for tier, label in TIER_LABELS.items():
        total = sum(quest["Tier"] == tier for quest in QUESTS)
        if sum(quest["Tier"] == tier for quest in required.values()) > QUESTS_PER_TIER:
            return f"At most {QUESTS_PER_TIER} {label} quests can be required"
        if total - sum(quest["Tier"] == tier for quest in excluded.values()) < QUESTS_PER_TIER:
            return f"Too many {label} quests excluded: at least {QUESTS_PER_TIER} of {total} must be available"

    return None


def pick_quests(
    choose_sample: Callable[[list[int], int], list[int]],
    required: Iterable[str] = (),
    excluded: Iterable[str] = (),
) -> list[int]:
    required, excluded = _by_name(required), _by_name(excluded)
    chosen = []
    for tier in (1, 2, 3):
        in_tier = [(index, quest["Name"].casefold()) for index, quest in enumerate(QUESTS) if quest["Tier"] == tier]
        fixed = [index for index, name in in_tier if name in required]
        pool = [index for index, name in in_tier if name not in required and name not in excluded]
        chosen += fixed + choose_sample(pool, QUESTS_PER_TIER - len(fixed))

    return sorted(chosen)


def quest_upgrades_needed(quest: dict) -> int:
    return max(TIER_UPGRADES[quest["Tier"]], quest["Need"].get("Upgrades", 0))
