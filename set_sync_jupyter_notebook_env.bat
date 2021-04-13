if not exist "..\SyncJupyterNotebook\" (
  echo The submodule SyncJupyterNotebook doesn't exist. Please add it to the solution
  exit /B 1
)
set SyncJupyterNotebook="..\SyncJupyterNotebook\bin\Release\net5.0\SyncJupyterNotebook.exe"
if not exist %SyncJupyterNotebook% (
  set SyncJupyterNotebook="..\SyncJupyterNotebook\bin\Debug\net5.0\SyncJupyterNotebook.exe"
  if not exist %SyncJupyterNotebook% (
    echo the SyncJupyterNotebook executable doesn't exist. Please build the solution.
    exit /B 1
  )
)
