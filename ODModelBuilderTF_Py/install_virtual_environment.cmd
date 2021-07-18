@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command ".\install_virtual_environment.ps1"
exit /b %ErrorLevel%
