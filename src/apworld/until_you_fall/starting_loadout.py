from collections.abc import Callable, Collection, Iterable, Sequence

from Options import OptionError

from .stage_weapon_locations import requires_both_hands

EARLY_WEAPON_COUNT = 2


def resolve_starting_weapons(
    first_id: int,
    first_random: bool,
    second_id: int,
    second_random: bool,
    starting_hands: int,
    eligible_ids: Sequence[int],
    choose: Callable[[Sequence[int]], int],
    two_handed_ids: Collection[int],
    second_eligible_ids: Sequence[int] | None = None,
) -> list[int]:
    if first_random:
        first_id = choose(
            tuple(
                weapon_id
                for weapon_id in eligible_ids
                if (starting_hands == 0 or weapon_id not in two_handed_ids)
                and (second_random or weapon_id != second_id)
            )
        )
    if starting_hands != 0 and first_id in two_handed_ids:
        raise OptionError("Until You Fall: a two-handed starting_weapon requires starting_hands: both")
    if second_random:
        second_pool = second_eligible_ids or eligible_ids
        second_id = choose(tuple(weapon_id for weapon_id in second_pool if weapon_id != first_id))
    if first_id == second_id:
        raise OptionError("Until You Fall: starting_weapon and second_starting_weapon must be different")

    return [first_id, second_id] if second_id else [first_id]


def pick_early_weapons(
    weapons: Iterable[dict],
    owned: Collection[str],
    has_both_hands: bool,
    sample: Callable[[Sequence[str], int], list[str]],
) -> list[str]:
    candidates = [
        weapon["Name"]
        for weapon in weapons
        if weapon["Name"] not in owned and (has_both_hands or not requires_both_hands(weapon))
    ]

    return sample(candidates, min(EARLY_WEAPON_COUNT, len(candidates)))
