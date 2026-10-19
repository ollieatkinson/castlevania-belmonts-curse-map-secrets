@echo off
setlocal
echo Map Secrets - standalone installer
echo Close the game before continuing.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -PluginDll "MapAssist.dll" %*
set "install_result=%errorlevel%"
pause
exit /b %install_result%
