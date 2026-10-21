@echo off
rem  GuangXIW - start the desktop app (standalone window via Edge/Chrome app mode)
cd /d "%~dp0"
if exist "GuangXIW.exe" (
    start "" "GuangXIW.exe"
) else (
    start "" "index.html"
)