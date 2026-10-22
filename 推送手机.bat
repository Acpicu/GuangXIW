@echo off
chcp 65001 >nul
setlocal
set "ADB=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe"
if not exist "%ADB%" set "ADB=adb"
echo === GuangXIW - push relations to phone via ADB ===
"%ADB%" devices | findstr /r /c:"device$" >nul || ( echo [ERROR] no device & pause & exit /b 1 )

set "SRC="
for /f "delims=" %%F in ('dir /b /o-d "%USERPROFILE%\Downloads\guangxiw-relations-*.json" 2^>nul') do if not defined SRC set "SRC=%USERPROFILE%\Downloads\%%F"
if not defined SRC (
  echo [INFO] no guangxiw-relations-*.json in Downloads.
  echo        In GuangXIW: 清单 ^> 文件夹 ^> 导出 ^> 给 SJ Lens（手机）
  pause & exit /b 1
)
echo pushing: %SRC%
"%ADB%" push "%SRC%" /sdcard/Download/ || ( echo [ERROR] push failed & pause & exit /b 1 )
echo.
echo DONE. On phone: open SJ Lens ^> 数据本 ^> 关系网 ^> pick the file in Download
pause