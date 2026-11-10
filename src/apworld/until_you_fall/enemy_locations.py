from .catalogs import ENEMIES


def first_kill_name(enemy: dict) -> str:
    return f"First Kill: {enemy['Name']}"


def first_kill_locations() -> dict[str, int]:
    return {first_kill_name(enemy): enemy["Id"] for enemy in ENEMIES}
