from .items import WEAPON_UPGRADE_LEVEL_ITEM_COUNT

GLITCH_ITEM = "Sequence Break"
MAX_UPGRADE_NEED = WEAPON_UPGRADE_LEVEL_ITEM_COUNT
UPGRADE_REQUIREMENT_FREE_STAGES = 7
UPGRADES_PER_LATER_STAGE = 0.4
EARLY_STAGE_REWARD_COUNT = 3
STAGE_REWARD_REQUIREMENT_EXPONENT = 0.75


def upgrade_need(stage: int, tiers_above_starting_difficulty: int) -> int:
    return min(
        MAX_UPGRADE_NEED,
        int(max(0, stage - UPGRADE_REQUIREMENT_FREE_STAGES) * UPGRADES_PER_LATER_STAGE)
        + tiers_above_starting_difficulty,
    )


def stage_reward_need(index: int, total_count: int) -> int:
    if index <= EARLY_STAGE_REWARD_COUNT:
        return 0

    return max(1, round(MAX_UPGRADE_NEED * (index / total_count) ** STAGE_REWARD_REQUIREMENT_EXPONENT))
