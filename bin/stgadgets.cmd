@echo off
rem The stgadgets CLI (docs/cli.md) on Windows, like bin/stgadgets: a quiet incremental build from this repository's
rem source, then run it. With bin\ on your PATH it's just `stgadgets`.
setlocal
set "project=%~dp0..\src\cli\Stgadgets.Cli"
set "log=%TEMP%\stgadgets-build-%RANDOM%%RANDOM%.log"

rem Built from the repository root so dotnet finds global.json, which it looks for from the current folder rather than
rem the project's. The CLI itself runs in the caller's folder. The build's output is only shown if it fails.
pushd "%~dp0.."
dotnet build "%project%" --nologo --verbosity quiet > "%log%" 2>&1
set "built=%errorlevel%"
popd
if not "%built%"=="0" (
  type "%log%" 1>&2
  del "%log%"
  exit /b 1
)
del "%log%"

dotnet "%project%\bin\Debug\net10.0\stgadgets.dll" %*
exit /b %errorlevel%
