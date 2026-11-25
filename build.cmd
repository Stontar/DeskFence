@echo off
setlocal enabledelayedexpansion
set "ROOT=%~dp0"
set "SRC=%ROOT%src"
set "OUT=%ROOT%DeskFence.exe"

rem --- locate a Roslyn csc (VS 2017+ / Build Tools) ---
set "CSC="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
  "%VSWHERE%" -latest -products * -find "MSBuild\**\Bin\Roslyn\csc.exe" > "%TEMP%\deskfence_csc.txt" 2>nul
  set /p CSC=<"%TEMP%\deskfence_csc.txt"
  del "%TEMP%\deskfence_csc.txt" >nul 2>&1
)
if defined CSC if not exist "%CSC%" set "CSC="

if not defined CSC (
  for %%v in (18 2022 2019 2017) do (
    for %%e in (Enterprise Professional Community BuildTools) do (
      if not defined CSC if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\%%v\%%e\MSBuild\Current\Bin\Roslyn\csc.exe" (
        set "CSC=%ProgramFiles(x86)%\Microsoft Visual Studio\%%v\%%e\MSBuild\Current\Bin\Roslyn\csc.exe"
      )
    )
  )
)
if not defined CSC set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo Using compiler: %CSC%
"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu /optimize+ /unsafe+ /out:"%OUT%" ^
  /win32icon:"%SRC%\app.ico" ^
  /win32manifest:"%SRC%\app.manifest" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:System.Web.Extensions.dll /r:Microsoft.VisualBasic.dll ^
  "%SRC%\DeskFence.cs"

if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK -^> "%OUT%"
exit /b 0
