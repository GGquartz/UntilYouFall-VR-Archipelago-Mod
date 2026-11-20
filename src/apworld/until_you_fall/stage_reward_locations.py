from .catalogs import LOCATIONS, OPTION_RANGES

STAGE_REWARD_BASE_ID = LOCATIONS["StageRewardBaseId"]
MAX_STAGE_REWARD_CHECKS = OPTION_RANGES["StageRewardChecks"]["Maximum"]

LATE_REWARD_MIN_UPGRADES = 5
FINAL_REWARD_QUARTER_START = 3


def stage_reward_name(index: int) -> str:
    return f"Stage Reward {index}"


def stage_reward_id(index: int) -> int:
    return STAGE_REWARD_BASE_ID + index


def active_stage_rewards(count: int) -> dict[str, int]:
    return {stage_reward_name(index): stage_reward_id(index) for index in range(1, count + 1)}
