@echo off
cd /d "%~dp0.."
if not defined UYF_GAME_DIR set "UYF_GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\UntilYouFall"
if not defined AP_DIR set "AP_DIR=C:\ProgramData\Archipelago"
set "DOTNET_CLI_HOME=%CD%\.cache\dotnet"
set "NUGET_PACKAGES=%CD%\.cache\nuget"

dotnet csharpier check src tests || goto failed
python dev\export-game-data.py --check || goto failed
dotnet build src\mod\UntilYouFall.Archipelago.csproj -c Release -p:GameDir="%UYF_GAME_DIR%" || goto failed

set "OUT=src\mod\bin\Release\net6.0"
copy /y "%OUT%\UntilYouFall.Archipelago.dll" "%UYF_GAME_DIR%\Mods" || goto failed
copy /y "%OUT%\Archipelago.MultiClient.Net.dll" "%UYF_GAME_DIR%\UserLibs" || goto failed
copy /y "%OUT%\Newtonsoft.Json.dll" "%UYF_GAME_DIR%\UserLibs" || goto failed
copy /y dist\until_you_fall.apworld "%AP_DIR%\custom_worlds" || goto failed
echo Deploy complete
pause
exit /b 0

:failed
echo Deploy failed
pause
exit /b 1
