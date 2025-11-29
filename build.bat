@echo off
setlocal enabledelayedexpansion

REM -----------------------------
REM CONFIGURE THESE PATHS
REM -----------------------------
set MSBUILD="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
set SLN_PATH="C:\project_files\Scripts\TabHelper\TABHelperMod\TABHelperMod.sln"
set CONFIG=Debug
set PLATFORM="Any CPU"

set DLL_SOURCE="C:\project_files\Scripts\TabHelper\TABHelperMod\bin\Debug\TABHelperMod.dll"
set DLL_DEST="D:\Steam\steamapps\common\They Are Billions\Mods\3357549187\TABHelperMod.dll"

REM -----------------------------

echo Building solution...
%MSBUILD% %SLN_PATH% /p:Configuration=%CONFIG% /p:Platform=%PLATFORM%
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Build failed. Stopping script.
    exit /b 1
)

echo Build success.

echo Moving DLL...
copy /y %DLL_SOURCE% %DLL_DEST%
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Failed to copy DLL. Stopping script.
    exit /b 1
)

echo DLL moved successfully.

echo All steps completed successfully.
exit /b 0
