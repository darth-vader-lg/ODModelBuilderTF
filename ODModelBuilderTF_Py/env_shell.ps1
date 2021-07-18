if (-not(Test-Path .\env\Activate.ps1)) {
  Write-Output "Error: Python environment not found!!!"
  Write-Output "Please, launch install_virtual_environment or build the solution to create it."
  Exit 1
}
.\env\Activate.ps1
if (-not($PSScriptRoot) -and $psISE) { $scriptRoot = Split-Path $psISE.CurrentFile.FullPath } else { $scriptRoot = $PSScriptRoot }
$dir = Split-Path -Parent $PSScriptRoot
powershell.exe -ExecutionPolicy ByPass -NoProfile -NoExit -command "`$Host.UI.RawUI.WindowTitle = '$dir environment'"
