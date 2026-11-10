"""Shared base for Until You Fall generation tests. Run with the Archipelago source directory on PYTHONPATH."""

from test.bases import WorldTestBase


class UntilYouFallWorldTest(WorldTestBase):
    game = "Until You Fall"

    # Archipelago's generic reachability/fill checks are identical for every subclass; TestGeneration runs them once.
    test_all_state_can_reach_everything = None
    test_empty_state_can_reach_something = None
    test_fill = None

    def world_setup(self, seed=None):
        # Quest Board is on by default; legacy count tests run without it unless they opt in.
        self.options = {"quest_board": False, "goal": "runs", **self.options}
        super().world_setup(seed)
