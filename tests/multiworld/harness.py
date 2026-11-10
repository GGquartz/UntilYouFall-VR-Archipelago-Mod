"""Generates real multiworlds through Generate.py and Main.py. Needs the full Archipelago source (all worlds) on PYTHONPATH."""

import faulthandler
import os
import tempfile
import time
from contextlib import contextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

os.environ.setdefault("SKIP_REQUIREMENTS_UPDATE", "1")

import settings

# Same as Archipelago's own tests: never write host.yaml
settings.no_gui = True
settings.skip_autosave = True

import Generate
import Main
import yaml
from BaseClasses import CollectionState, MultiWorld
from worlds.AutoWorld import AutoWorldRegister

OUR_GAME = "Until You Fall"
# Event world without a game to play, not interesting as a partner
SKIPPED_PARTNER_GAMES = {"Archipelago"}
GENERATION_TIMEOUT_SECONDS = 300


@contextmanager
def _generation_watchdog(description: str):
    print(f"START {description}", flush=True)
    started = time.perf_counter()
    outcome = "FAIL"
    faulthandler.dump_traceback_later(GENERATION_TIMEOUT_SECONDS, exit=True)
    try:
        yield
        outcome = "PASS"
    finally:
        faulthandler.cancel_dump_traceback_later()
        print(f"{outcome} {description} ({time.perf_counter() - started:.2f}s)", flush=True)


@dataclass
class Slot:
    game: str
    options: dict[str, Any] = field(default_factory=dict)
    common_options: dict[str, Any] = field(default_factory=dict)


def partner_games() -> list[str]:
    return sorted(set(AutoWorldRegister.world_types) - {OUR_GAME} - SKIPPED_PARTNER_GAMES)


def generate(slots: list[Slot], seed: int, write_output: bool = True) -> MultiWorld:
    """Rolls and fills the slots as the Generate command would, including the output and playthrough spoiler.

    Games that patch a ROM can not write their output here, write_output=False then stops after the fill."""
    with (
        _generation_watchdog(f"generate seed={seed} output={write_output} slots={slots}"),
        tempfile.TemporaryDirectory() as yaml_dir,
        tempfile.TemporaryDirectory() as output_dir,
    ):
        for number, slot in enumerate(slots, 1):
            document = {"name": f"Player{number}", "game": slot.game, slot.game: slot.options, **slot.common_options}
            Path(yaml_dir, f"player{number}.yaml").write_text(yaml.safe_dump(document), encoding="utf-8")

        argv = ["--player_files_path", yaml_dir, "--outputpath", output_dir, "--seed", str(seed), "--spoiler", "3"]
        arguments = Generate.mystery_argparse(argv if write_output else [*argv, "--skip_output"])
        multiworld = Main.main(*Generate.main(arguments))

    return multiworld


def assert_playable(test_case, multiworld: MultiWorld) -> None:
    with _generation_watchdog(f"validate seed={multiworld.seed} games={dict(multiworld.game)}"):
        for location in multiworld.get_locations():
            test_case.assertIsNotNone(location.item, f"{location.name} (player {location.player}) was left empty")

        test_case.assertTrue(multiworld.can_beat_game(CollectionState(multiworld)), "the multiworld is not beatable")
