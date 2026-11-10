# Until You Fall Archipelago Setup Guide

## Required Software

- [Until You Fall](https://store.steampowered.com/app/858260/Until_You_Fall/) on Steam

- [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases) 0.6.7 or newer (required for generating the game or the Options Creator)

- The [latest UYF-Archipelago release](https://github.com/GGquartz/UntilYouFall-VR-Archipelago-Mod/releases):

  - `UYF-Archipelago-windows-x86_64.zip`

  - `UYF-Archipelago-linux-x86_64.zip` (experimental, see [Linux / Steam / Proton](#linux--steam--proton))

## Installing the APWorld

You only need this to generate a multiworld (or to create a YAML in the Archipelago Options Creator)

If someone else is generating the game, skip this section

1. Double-click `until_you_fall.apworld` while Archipelago is installed, or

2. copy it to `C:\ProgramData\Archipelago\custom_worlds\`

Restart the Archipelago Launcher afterwards

## Installing the Mod with the Launcher (recommended)

1. Close Until You Fall

2. Extract the release zip anywhere and run `UYFArchipelagoLauncher.exe`

3. Press **Detect**, or press **Browse** and select the game folder (the one containing `UntilYouFall.exe`)

4. Press **Install / Repair**

5. Launch the game from Steam, you should see `Archipelago Mod Loaded` in the profile selection screen

6. Connect to the server via the launcher

7. Start playing by creating a new profile (save)

To remove the mod, press **Uninstall mod** on the **Install** tab

Press **Install / Repair** at any time to fix missing or outdated files, for example after updating to a newer release

## Installing the Mod Manually

Use this if **Install / Repair** fails, launcher is still required to connect the game to the server (this will change in future releases)

1. Close Until You Fall

2. Download [MelonLoader 0.7.3](https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip)

3. Extract the contents into the game folder, next to `UntilYouFall.exe`

4. Copy the files from the mod's `payload` folder into the game folder, keeping the folder names:

   | File | Destination |
   | --- | --- |
   | `Mods\UntilYouFall.Archipelago.dll` | `<game folder>\Mods\` |
   | `UserLibs\Archipelago.MultiClient.Net.dll` | `<game folder>\UserLibs\` |
   | `UserLibs\Newtonsoft.Json.dll` | `<game folder>\UserLibs\` |

   Create the `Mods` and `UserLibs` folders if they do not exist

5. Launch the game from Steam, you should see `Archipelago Mod Loaded` in the profile selection screen

6. Connect to the server via the launcher

7. Start playing by creating a new profile (save)

## Linux / Steam / Proton

Linux support is very experimental and needs testing

1. Extract `UYF-Archipelago-linux-x86_64.zip` and run `./UYFArchipelagoLauncher` outside Proton

   - If you get "Permission denied", run `chmod +x UYFArchipelagoLauncher`

   - If it does not start, install `fontconfig`, `libice6` and `libsm6` from your distribution (Fedora: `libICE`, `libSM`)

2. In Steam, choose a Proton version: Until You Fall > Properties > Compatibility

3. Use **Detect** or **Browse** to select the game folder (the one containing `UntilYouFall.exe`)

4. Press **Install / Repair**

5. In Steam, open Until You Fall > Properties > Launch Options and use the launch option shown in the launcher:

   ```text
   WINEDLLOVERRIDES="version=n,b" %command%
   ```

6. Launch the game from Steam, you should see `Archipelago Mod Loaded` in the profile selection screen

7. Connect to the server via the launcher

8. Start playing by creating a new profile (save)



## What is a YAML and why do I need one?

Your YAML file contains the options for your player slot: name, goal, quests, difficulty and more

You need to send your YAML to whoever generates the game

More can be found here: [Archipelago Setup Guide](https://archipelago.gg/tutorial/Archipelago/setup/en)

### Creating a YAML

- In the launcher, open the **Options** tab, choose your preferred options, and press **Save YAML** (recommended)

- Use the Options Creator in the Archipelago Launcher (requires installed APWorld)

- Write the file manually, using option names and descriptions from the Options Creator

If you are generating the game, put the file in Archipelago Players folder: `C:\ProgramData\Archipelago\Players\`


## Troubleshooting

- **I don't see "Archipelago Mod Loaded"**

  Make sure you are on the screen where you see your 3 profile save slots

  You should see MelonLoader pop up it's own window when launching the game

  Open `<game folder>\MelonLoader\Latest.log`, it must contain a message that the mod was loaded

  If MelonLoader is missing or the game does not start, press **Install / Repair** again, or try the manual installation above

- **Game launch seems frozen**

  MelonLoader could be generating assemblies, wait a few minutes

- **The launcher cannot connect**

  Make sure the game is running, check the server address and port, the slot name (it is case sensitive) and the password

- **Install / Repair fails**

  Make sure the game is closed and that you have write access to the game folder, if the MelonLoader download fails, try the manual installation

- **Something else is wrong / I ran into a bug**

  In the launcher, open the **Help** tab and press **Export logs**, then send the exported file to the Archipelago Discord Until You Fall thread and describe your issue or bug report
