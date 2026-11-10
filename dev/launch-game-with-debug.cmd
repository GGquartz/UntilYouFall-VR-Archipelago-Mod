@echo off
if not defined UYF_GAME_DIR set "UYF_GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\UntilYouFall"
echo Enabled>"%UYF_GAME_DIR%\UserData\Archipelago.debug.enabled"
start "" steam://rungameid/858260
