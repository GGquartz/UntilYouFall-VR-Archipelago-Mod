from .catalogs import DIFFICULTIES, LOCATIONS
from .stage_weapon_locations import STAGE_COUNT, stage_name

DIFFICULTY_NAMES = tuple(difficulty["Name"] for difficulty in DIFFICULTIES)


def location_id(stage: int, tier: int) -> int:
    if stage < 1 or stage > STAGE_COUNT or tier < 1 or tier >= len(DIFFICULTY_NAMES):
        raise ValueError(f"difficulty check requires stage 1-{STAGE_COUNT} and tier 1-{len(DIFFICULTY_NAMES) - 1}")

    return LOCATIONS["StageDifficultyBaseId"] + (tier - 1) * STAGE_COUNT + stage


def location_name(stage: int, tier: int) -> str:
    return f"{stage_name(stage)} on {DIFFICULTY_NAMES[tier]}"


def is_goal_check(goal: str, goal_runs: int, starting: int, maximum: int, stage: int, tier: int) -> bool:
    return (
        goal == "runs_max_difficulty"
        and goal_runs == 1
        and maximum > starting
        and tier == maximum
        and stage == STAGE_COUNT
    )


def active_stage_difficulties(starting: int, maximum: int, goal: str = "", goal_runs: int = 0) -> dict[str, int]:
    return {
        location_name(stage, tier): location_id(stage, tier)
        for tier in range(starting + 1, maximum + 1)
        for stage in range(1, STAGE_COUNT + 1)
        if not is_goal_check(goal, goal_runs, starting, maximum, stage, tier)
    }
