@echo off
rem  Rebuild GuangXIW.exe from launcher.cs + index.html + WebView2 SDK dlls.
rem  Uses the C# compiler that ships with Windows (.NET Framework), no extra toolchain.
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" ( echo [ERROR] csc.exe not found & pause & exit /b 1 )

"%CSC%" /nologo /target:winexe /codepage:65001 /platform:x64 /out:GuangXIW.exe ^
 /resource:index.html ^
 /resource:Microsoft.Web.WebView2.Core.dll ^
 /resource:Microsoft.Web.WebView2.WinForms.dll ^
 /resource:WebView2Loader.dll ^
 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
 /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll ^
 launcher.cs
if errorlevel 1 ( echo [ERROR] build failed & pause & exit /b 1 )
echo Built GuangXIW.exe
pause