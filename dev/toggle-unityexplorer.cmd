@echo off
if not defined UYF_GAME_DIR set "UYF_GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\UntilYouFall"
set "DLL=%UYF_GAME_DIR%\Mods\UnityExplorer.ML.IL2CPP.CoreCLR.dll"
if exist "%DLL%" (
    ren "%DLL%" "UnityExplorer.ML.IL2CPP.CoreCLR.dll.disabled"
    echo UnityExplorer disabled
) else (
    ren "%DLL%.disabled" "UnityExplorer.ML.IL2CPP.CoreCLR.dll"
    echo UnityExplorer enabled
)
