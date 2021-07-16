@echo off
if not Exist .\env\Scripts\activate.bat (
  echo Error: Python environment not found!!!
  echo Please, launch install_virtual_environment or build the solution to create it.
  pause
  exit 1
)
start "ODModelBuilderTF" powershell.exe -ExecutionPolicy ByPass -NoProfile -NoExit -command ".\env\Scripts\activate.ps1"
