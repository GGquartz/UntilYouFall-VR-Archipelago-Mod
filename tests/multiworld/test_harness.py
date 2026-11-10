"""Diagnostics must identify stalled generations without hiding generation failures."""

import io
import subprocess
import sys
import unittest
from contextlib import redirect_stdout
from unittest.mock import patch

import harness


class TestGenerationDiagnostics(unittest.TestCase):
    def test_watchdog_is_cancelled_after_success(self):
        output = io.StringIO()
        with (
            patch.object(harness.faulthandler, "dump_traceback_later") as schedule_dump,
            patch.object(harness.faulthandler, "cancel_dump_traceback_later") as cancel_dump,
            redirect_stdout(output),
        ):
            with harness._generation_watchdog("seed=123 games=Until You Fall"):
                schedule_dump.assert_called_once_with(harness.GENERATION_TIMEOUT_SECONDS, exit=True)
                cancel_dump.assert_not_called()

        cancel_dump.assert_called_once_with()
        self.assertIn("START seed=123 games=Until You Fall", output.getvalue())
        self.assertIn("PASS seed=123 games=Until You Fall", output.getvalue())

    def test_watchdog_is_cancelled_without_hiding_failure(self):
        output = io.StringIO()
        failure = RuntimeError("generation failed")
        with (
            patch.object(harness.faulthandler, "dump_traceback_later"),
            patch.object(harness.faulthandler, "cancel_dump_traceback_later") as cancel_dump,
            redirect_stdout(output),
        ):
            with self.assertRaises(RuntimeError) as raised:
                with harness._generation_watchdog("seed=456 games=Until You Fall"):
                    raise failure

        self.assertIs(raised.exception, failure)
        cancel_dump.assert_called_once_with()
        self.assertIn("FAIL seed=456 games=Until You Fall", output.getvalue())
        self.assertNotIn("PASS seed=456", output.getvalue())

    def test_stalled_generation_exits_with_thread_dump(self):
        result = subprocess.run(
            [
                sys.executable,
                "-c",
                "import harness, time\n"
                "harness.GENERATION_TIMEOUT_SECONDS = 0.1\n"
                "with harness._generation_watchdog('stalled fixture'):\n"
                "    time.sleep(30)\n",
            ],
            capture_output=True,
            text=True,
            timeout=60,
        )

        self.assertEqual(result.returncode, 1)
        self.assertIn("START stalled fixture", result.stdout)
        self.assertNotIn("PASS stalled fixture", result.stdout)
        self.assertIn("Timeout", result.stderr)
        self.assertIn('File "<string>"', result.stderr)
