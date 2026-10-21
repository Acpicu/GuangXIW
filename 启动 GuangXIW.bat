@echo off
rem  GuangXIW desktop launcher - opens the app as a standalone window
cd /d "%~dp0"
set "APP=%~dp0index.html"
set "EDGE=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
set "EDGE2=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"
set "CHROME=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
set "CHROME2=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"

if exist "%EDGE%"  ( start "" "%EDGE%"  --app="file:///%APP:\=/%" --window-size=1280,860 & exit /b 0 )
if exist "%EDGE2%" ( start "" "%EDGE2%" --app="file:///%APP:\=/%" --window-size=1280,860 & exit /b 0 )
if exist "%CHROME%"  ( start "" "%CHROME%"  --app="file:///%APP:\=/%" --window-size=1280,860 & exit /b 0 )
if exist "%CHROME2%" ( start "" "%CHROME2%" --app="file:///%APP:\=/%" --window-size=1280,860 & exit /b 0 )

start "" "%APP%"