@echo off
if not defined AP_DIR set "AP_DIR=C:\ProgramData\Archipelago"
if not defined AP_PORT set "AP_PORT=38281"
set "SEED=%~dp0..\ap-test\UntilYouFall_Test.zip"
"%AP_DIR%\ArchipelagoServer.exe" "%SEED%" --host 127.0.0.1 --port %AP_PORT% --savefile "%SEED:.zip=.apsave%" --password= --server_password=
pause
