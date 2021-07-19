@echo off
setlocal enabledelayedexpansion
set SyncJupyterNotebook="..\SyncJupyterNotebook\bin\Release\net5.0\SyncJupyterNotebook.exe"
if not exist !SyncJupyterNotebook! (
  set SyncJupyterNotebook="..\SyncJupyterNotebook\bin\Debug\net5.0\SyncJupyterNotebook.exe"
  if not exist !SyncJupyterNotebook! (
    dotnet build ..\SyncJupyterNotebook -c Release
    if not ERRORLEVEL == 0 (
      exit /B %ERRORLEVEL%
    )
    set SyncJupyterNotebook="..\SyncJupyterNotebook\bin\Release\net5.0\SyncJupyterNotebook.exe"
  )
)
endlocal & set SyncJupyterNotebook=%SyncJupyterNotebook%
