from dataclasses import dataclass

from Options import Choice, DefaultOnToggle, OptionDict, OptionSet, PerGameCommonOptions, Range, StartInventoryPool

from .catalogs import DIFFICULTIES, OPTION_RANGES, WEAPONS
from .items import TRAP_ITEMS
from .quest_locations import QUEST_NAMES


def _option_name(weapon_name: str) -> str:
    return "option_" + weapon_name.lower().replace("'", "").replace("-", " ").replace(" ", "_")


class RandomChoice(Choice):
    """Delay random selection so we can avoid duplicate random weapons and use rules for random difficulty"""

    option_random_valid = -1

    @classmethod
    def from_text(cls, text: str) -> "RandomChoice":
        if text.lower() == "random":
            return cls(cls.option_random_valid)
        return super().from_text(text)

    @property
    def is_random(self) -> bool:
        return self.value == self.option_random_valid


class StartingWeapon(RandomChoice):
    """Excludes Spellsword's Charm and Knight-Blooded Crest"""

    display_name = "Starting Weapon"
    locals().update(
        {_option_name(weapon["Name"]): weapon["Id"] for weapon in WEAPONS if not weapon.get("SecondaryOnly")}
    )
    default = "random"

    @classmethod
    def weapon_ids(cls) -> tuple[int, ...]:
        return tuple(value for value in cls.name_lookup if value > 0)


class SecondStartingWeapon(StartingWeapon):
    """Optional, must be different than the first weapon"""

    display_name = "Second Starting Weapon"
    locals().update({_option_name(weapon["Name"]): weapon["Id"] for weapon in WEAPONS if weapon.get("SecondaryOnly")})
    option_none = 0
    default = 0


class StartingHands(Choice):
    """Choose which hands are available at the start"""

    display_name = "Starting Hands"
    option_both = 0
    option_left_only = 1
    option_right_only = 2
    default = "right_only"


class EarlyHand(Choice):
    """If starting with only one hand, make sure missing hand is available early"""

    display_name = "Early Hand"
    option_first_25_percent_of_checks = 25
    option_first_50_percent_of_checks = 50
    option_unrestricted = 100
    default = 50


class WeaponSupers(Choice):
    """Unlock weapon supers together with weapons or as separate items"""

    display_name = "Weapon Supers"
    option_unlocked_with_weapon = 0
    option_separate_items = 1
    default = "separate_items"


class TrapPercent(Range):
    """Percentage of filler slots replaced with traps"""

    display_name = "Trap %"
    range_start = 0
    range_end = 100
    default = 10


class StartingDifficulty(RandomChoice):
    """Random selects below maximum difficulty"""

    display_name = "Starting Difficulty"
    locals().update({"option_" + difficulty["Name"].lower(): tier for tier, difficulty in enumerate(DIFFICULTIES)})
    default = 1


class MaxDifficulty(Choice):
    """Hardest available difficulty"""

    display_name = "Max Difficulty"
    locals().update({"option_" + difficulty["Name"].lower(): tier for tier, difficulty in enumerate(DIFFICULTIES)})
    default = 3


class TrapWeights(OptionDict):
    """Relative share of each trap type"""

    display_name = "Trap Weights"
    valid_keys = frozenset(TRAP_ITEMS)
    default = {name: 20 for name in TRAP_ITEMS}


class TrapEffectLength(Range):
    """Active gameplay seconds for timed traps"""

    display_name = "Trap Effect Length"
    range_start = OPTION_RANGES["TrapEffectLength"]["Minimum"]
    range_end = OPTION_RANGES["TrapEffectLength"]["Maximum"]
    default = OPTION_RANGES["TrapEffectLength"]["Default"]


class StageRewardChecks(Range):
    """Clearing rooms has a chance to offer sequential AP checks"""

    display_name = "Stage Reward Checks Count"
    range_start = OPTION_RANGES["StageRewardChecks"]["Minimum"]
    range_end = OPTION_RANGES["StageRewardChecks"]["Maximum"]
    default = OPTION_RANGES["StageRewardChecks"]["Default"]


class QuestBoard(DefaultOnToggle):
    """Adds randomized quests (6 Easy, 6 Medium, 6 Hard) worth 1, 2 and 3 checks each"""

    display_name = "Quest Board"


class RequiredQuests(OptionSet):
    """Quests that are always on the board, random by default"""

    display_name = "Required Quests"
    valid_keys = frozenset(name.casefold() for name in QUEST_NAMES)
    valid_keys_casefold = True


class ExcludedQuests(OptionSet):
    """Set specific quests to never appear on the board"""

    display_name = "Excluded Quests"
    valid_keys = frozenset(name.casefold() for name in QUEST_NAMES)
    valid_keys_casefold = True


class QuestHintCost(Range):
    """How much aether to hint quest rewards"""

    display_name = "Quest Hint Cost"
    range_start = OPTION_RANGES["QuestHintCost"]["Minimum"]
    range_end = OPTION_RANGES["QuestHintCost"]["Maximum"]
    default = OPTION_RANGES["QuestHintCost"]["Default"]


class Goal(Choice):
    """Quests = complete all quests on the board; All Weapons = clear a run with each weapon;
    Runs on max difficulty = finish X runs on the max difficulty you can get; Runs = finish X runs"""

    display_name = "Goal"
    option_quests = 0
    option_all_weapons = 1
    option_runs_max_difficulty = 2
    option_runs = 3
    default = 0


class GoalRuns(Range):
    """Number of required runs for the finish X runs goals (ignored by other goals)"""

    display_name = "Goal Runs"
    range_start = OPTION_RANGES["GoalRuns"]["Minimum"]
    range_end = OPTION_RANGES["GoalRuns"]["Maximum"]
    default = OPTION_RANGES["GoalRuns"]["Default"]


class DeathLinkMode(Choice):
    """Hard = instant loss; Soft = set HP to 1; Trap = random trap; Death's Door = near-death state"""

    display_name = "Death Link Mode"
    option_hard = 0
    option_soft = 1
    option_trap = 2
    option_off = 3
    option_deaths_door = 4
    default = 4


@dataclass
class UntilYouFallOptions(PerGameCommonOptions):
    starting_weapon: StartingWeapon
    second_starting_weapon: SecondStartingWeapon
    starting_hands: StartingHands
    early_hand: EarlyHand
    weapon_supers: WeaponSupers
    stage_reward_checks: StageRewardChecks
    trap_percent: TrapPercent
    trap_weights: TrapWeights
    trap_effect_length: TrapEffectLength
    starting_difficulty: StartingDifficulty
    max_difficulty: MaxDifficulty
    quest_board: QuestBoard
    required_quests: RequiredQuests
    excluded_quests: ExcludedQuests
    quest_hint_cost: QuestHintCost
    goal: Goal
    goal_runs: GoalRuns
    death_link_mode: DeathLinkMode
    start_inventory_from_pool: StartInventoryPool
