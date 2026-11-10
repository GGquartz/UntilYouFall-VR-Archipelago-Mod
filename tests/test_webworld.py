import contextlib
import io
import runpy
import sys
import unittest
from pathlib import Path
from unittest.mock import patch
from zipfile import ZipFile

from worlds import AutoWorld
from worlds.AutoWorld import WebWorld
from worlds.Files import APWorldContainer
from worlds.until_you_fall import UntilYouFallWorld
from worlds.until_you_fall.catalogs import load_catalog


class TestWebWorld(unittest.TestCase):
    def test_setup_guide_is_registered(self):
        web = UntilYouFallWorld.web
        self.assertIsInstance(web, WebWorld)
        tutorials = getattr(web, "tutorials", [])
        self.assertEqual(len(tutorials), 1)
        tutorial = tutorials[0]
        self.assertEqual(tutorial.tutorial_name, f"{UntilYouFallWorld.game} Setup Guide")
        self.assertEqual(tutorial.language, "English")
        self.assertEqual(tutorial.file_name, "setup_en.md")
        self.assertEqual(tutorial.link, "setup/en")
        self.assertEqual(tutorial.authors, load_catalog("archipelago.json")["authors"])

    def test_setup_guide_and_license_are_included_in_apworld(self):
        root = Path(__file__).resolve().parents[1]
        script = root / "dev/build-apworld.py"
        ap_source = Path(AutoWorld.__file__).resolve().parents[1]
        with (
            patch.object(sys, "argv", [str(script), "--ap-source", str(ap_source)]),
            patch.object(APWorldContainer, "write"),
            patch("subprocess.run"),
            contextlib.redirect_stdout(io.StringIO()),
        ):
            builder = runpy.run_path(str(script))

        with ZipFile(io.BytesIO(), "w") as archive:
            builder["UntilYouFallContainer"]().write_contents(archive)
            guide_path = "until_you_fall/docs/setup_en.md"
            self.assertIn(guide_path, archive.namelist())
            self.assertEqual(archive.read(guide_path), (root / "docs/SETUP.md").read_bytes())
            license_path = "until_you_fall/LICENSE"
            self.assertIn(license_path, archive.namelist())
            self.assertEqual(archive.read(license_path), (root / "LICENSE").read_bytes())
