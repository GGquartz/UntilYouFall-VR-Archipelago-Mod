@echo off
cd /d "%~dp0.."
if not defined UYF_GAME_DIR set "UYF_GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\UntilYouFall"
set "DOTNET_CLI_HOME=%CD%\.cache\dotnet"
set "NUGET_PACKAGES=%CD%\.cache\nuget"
set "MOD_OUT=src\mod\bin\Release\net6.0"

dotnet csharpier check src tests || goto failed
python dev\export-game-data.py --check || goto failed
set "SKIP_REQUIREMENTS_UPDATE=1"
set "PYTHONPATH=%CD%\.cache\archipelago-dev"
.cache\ap-venv\Scripts\python.exe dev\export-options.py --check || goto failed
dotnet build src\mod\UntilYouFall.Archipelago.csproj -c Release -p:GameDir="%UYF_GAME_DIR%" || goto failed

call :publish win-x64 dist\UYFArchipelagoLauncher || goto failed
python dev\zip-release.py dist\UYFArchipelagoLauncher dist\UYF-Archipelago-windows-x86_64.zip UYF-Archipelago-windows-x86_64 || goto failed

call :publish linux-x64 dist\UYFArchipelagoLauncher-linux -p:InvariantGlobalization=true || goto failed
python dev\zip-release.py dist\UYFArchipelagoLauncher-linux dist\UYF-Archipelago-linux-x86_64.zip UYF-Archipelago-linux-x86_64 UYFArchipelagoLauncher || goto failed

echo Launcher build complete
pause
exit /b 0

:publish
if exist "%~2" rmdir /s /q "%~2"
dotnet publish src\launcher\UntilYouFall.Launcher.csproj -c Release -r %1 --self-contained true -o "%~2" %3 || exit /b 1
mkdir "%~2\payload\Mods" "%~2\payload\UserLibs" "%~2\Licenses"
copy /y "%MOD_OUT%\UntilYouFall.Archipelago.dll" "%~2\payload\Mods" || exit /b 1
copy /y "%MOD_OUT%\Archipelago.MultiClient.Net.dll" "%~2\payload\UserLibs" || exit /b 1
copy /y "%MOD_OUT%\Newtonsoft.Json.dll" "%~2\payload\UserLibs" || exit /b 1
copy /y docs\SETUP.md "%~2" || exit /b 1
copy /y LICENSE "%~2\LICENSE" || exit /b 1
copy /y src\launcher\Resources\Fonts\OFL.txt "%~2\Licenses\Jost-OFL.txt" || exit /b 1
exit /b 0

:failed
echo Launcher build failed
pause
exit /b 1
