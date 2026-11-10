from BaseClasses import CollectionState, Location, MultiWorld

from .shop_locations import SHOP_NAME_PREFIX, is_quality


def collect_location_spheres(multiworld: MultiWorld) -> list[list[Location]]:
    spheres = []
    for sphere in multiworld.get_sendable_spheres():
        if not sphere:
            break
        spheres.append(sorted(sphere, key=lambda location: (location.player, location.address, location.name)))

    return spheres


def enforce_early_hand(multiworld: MultiWorld, player: int, item_name: str, first_percent: int = 50) -> None:
    from Fill import swap_location_item

    if first_percent == 100:
        return

    spheres = collect_location_spheres(multiworld)
    if not spheres:
        raise RuntimeError(f"Until You Fall: no reachable location spheres for {item_name}")

    source = None
    source_sphere = None
    for sphere_number, sphere in enumerate(spheres, 1):
        for location in sphere:
            if location.item.name == item_name and location.item.player == player:
                source = location
                source_sphere = sphere_number
                break
        if source:
            break

    if source is None:
        raise RuntimeError(f"Until You Fall: could not find {item_name} for player {player} in reachable spheres")

    if source.locked:
        return

    limit = (len(spheres) * first_percent + 99) // 100
    if source_sphere <= limit:
        return

    state = CollectionState(multiworld)
    source_is_shop_slot = source.name.startswith(SHOP_NAME_PREFIX)
    candidates = [
        location
        for sphere in spheres[:limit]
        for location in sphere
        if not location.locked
        and location.item is not None
        and not location.item.advancement
        and (not source_is_shop_slot or is_quality(location.item))
        and location.can_fill(state, source.item, check_access=False)
        and source.can_fill(state, location.item, check_access=False)
    ]
    if not candidates:
        raise RuntimeError(f"Until You Fall: no compatible early non-progression location for {item_name}")

    while candidates:
        target = multiworld.worlds[player].random.choice(candidates)
        candidates.remove(target)
        swap_location_item(source, target, check_locked=False)

        updated_spheres = collect_location_spheres(multiworld)
        updated_limit = (len(updated_spheres) * first_percent + 99) // 100
        moved_sphere = next(
            (number for number, sphere in enumerate(updated_spheres, 1) if target in sphere),
            None,
        )
        if moved_sphere is not None and moved_sphere <= updated_limit and multiworld.can_beat_game():
            return

        swap_location_item(source, target, check_locked=False)

    raise RuntimeError(
        f"Until You Fall: moving {item_name} caused the seed to be unbeatable with first {first_percent}% placement"
    )
