@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command ".\publish-packages.ps1 %*"
exit /b %ErrorLevel%
