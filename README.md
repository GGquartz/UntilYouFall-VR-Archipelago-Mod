# Until You Fall Archipelago

An [Archipelago](https://archipelago.gg/) integration for the Steam PC VR version of the game **Until You Fall**

This project is in alpha, bug reports are appreciated

## How to play

- [Releases](https://github.com/GGquartz/UntilYouFall-VR-Archipelago-Mod/releases)

- [Setup Guide](docs/SETUP.md)


## How it works

Play Until You Fall in a randomized multiworld by yourself or with friends, if you are not familiar with Archipelago, check out the [Archipelago FAQ](https://archipelago.gg/faq/en/)

Progress through the game by collecting rewards via custom objectives such as stage clears, shops and quests

Find your own items like weapon and ability unlocks or upgrades

Collect items for other players and other players send items to you

## Goals

Choose a victory condition:

- **Quests:** Complete all randomized quests
- **All Weapons:** Clear a run with each of the 15 weapons
- **Runs on Max Difficulty:** Clear a select number of runs on the maximum difficulty available
- **Runs:** Clear a select number of runs

## Items

Items you can find or other players find for you

### Core unlocks
- **Weapons:** Unlock individual weapons
- **Weapon supers:** Unlock individual weapon abilities
- **Max Weapon Upgrade Level:** Raise the maximum level to which weapons can be upgraded
- **Second hand:** Start with only one usable hand
- **Difficulties:** Unlock playing on individual difficulties
- **Shop Unlocks:** Unlock sections of the custom Aether shop

### Player Buffs
- **Aether Multiplier:** Increase Aether gain
- **Player Bonuses:** Improve maximum health, dash charges, dash impact, combo limit, super level, critical strike window or dash recharge time
- **Extra Trait Slot:** Add a permanent extra trait slot to individual weapons
- **Starting Traits:** Get offered a selection of extra traits at the start of each run, unlock different traits, more options, more picks

### Filler
- **Weapon Damage:** Increase Health or Guard damage for individual weapons
- **Aether:** Grant bundles of Aether
- **Shard of Vitality:** Restore health during a run
- **Super Charge:** Charge weapon supers during a run
- **Enemy Stasis:** Put enemies into stasis during a run

### Traps
- **Enemy Spawn Trap:** Spawns an enemy in front of you
- **Elite Encounter Trap:** Your next room will be a random elite enemy stage
- **Dodge Only Trap:** Lose the ability to block for 60 seconds
- **Exhaustion Trap:** Dash refills slower for 60 seconds
- **Cursed Trap:** Take double damage for 60 seconds

Some of these are configurable and depend on your YAML options

## Locations

Checks to complete that reward items

- **Enemy sanity:** Defeating an enemy for the first time
- **Weapon stage clears:** Clearing each of the 21 stages with unique weapons
- **Difficulty stage clears:** Clearing stages on difficulties above your starting difficulty
- **Stage rewards:** Clearing rooms can sometimes offer an item
- **Aether shop:** Custom shop in the Runeforge
- **Quest rewards:** Custom quest board in the Runeforge

Some of these are configurable and depend on your YAML options

*Stuck? Try crushing a locked weapon to hint the location of your next weapon in progression*

## Reporting Problems

Use **Help -> Export logs** in the launcher, then attach the export to a [GitHub issue](https://github.com/GGquartz/UntilYouFall-VR-Archipelago-Mod/issues) or send it in the Until You Fall thread on the Archipelago Discord

Describe what happened, if the problem is about generation or progression logic, include the spoiler log if available

## Planned Features

Features planned before a full 1.0 release, some of these might be researched and scrapped or are subject to change

- Better hinting mechanisms
- More quests, add more quests tied to weapons and weapon supers
- Show the AP console in the pause menu
- Connect to the server directly in the game, without the launcher: Launcher-less install
- Option to randomize weapon abilities between weapons
- Live launcher settings for the wrist console (on/off, toggle hide during combat, font size, opacity and dominant hand)
- Live launcher settings for DeathLink mode
- A hard-logic YAML option, removing weapon upgrade requirements and removing some logic helpers
- YAML options for shorter runs / fewer checks
- A YAML option for excluded weapons
- A "Reward Reroll Token" filler item for rerolling room rewards
- APBingo support: show the bingo board in the Runeforge if present in the multiworld
- Pin / track a quest and see its progress during a run
- Enemy Spawn Trap for specific enemies
- Quest descriptions in the launcher Options Creator
- Custom AP shop models for specific games
- Map and add weapon logic requirements for Quest: On the Wings of the Knight

## Known Bugs

- Enemy Stasis is applying to enemies while they are spawning in specific encounters, which can result in invincible enemies or confusing visuals 

## Credits

- Thanks to kiwi on the Archipelago discord for creating the AP Logo 3D model
- Custom quest icons were created by me through editing existing steam achievement icons