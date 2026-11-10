from .catalogs import LOCATIONS, WEAPONS

STAGE_COUNT = LOCATIONS["StageCount"]
WEAPON_COUNT = len(WEAPONS)
WEAPONS_BY_NAME = {weapon["Name"]: weapon for weapon in WEAPONS}
WEAPON_OFFSETS = {weapon["Id"]: index for index, weapon in enumerate(WEAPONS, 1)}
LOCATION_ID_BASE = LOCATIONS["StageWeaponBaseId"]


def location_id(stage: int, weapon_id: int) -> int:
    if stage < 1 or stage > STAGE_COUNT:
        raise ValueError(f"stage must be between 1 and {STAGE_COUNT}")
    weapon_offset = WEAPON_OFFSETS.get(weapon_id)
    if weapon_offset is None:
        raise ValueError("unknown weapon id")

    return LOCATION_ID_BASE + (stage - 1) * WEAPON_COUNT + weapon_offset


def stage_name(stage: int) -> str:
    suffix = LOCATIONS["StageBossSuffixes"].get(str(stage), "")
    return f"Clear Stage {stage}{suffix}"


def location_name(stage: int, weapon_name: str) -> str:
    return f"{stage_name(stage)} with {weapon_name}"


def stage_weapon_locations() -> dict[str, int]:
    return {
        location_name(stage, weapon["Name"]): location_id(stage, weapon["Id"])
        for stage in range(1, STAGE_COUNT + 1)
        for weapon in WEAPONS
    }


def requires_both_hands(weapon: dict) -> bool:
    return weapon["LogicRequiresBothHands"]
