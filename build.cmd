@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command ".\build.ps1 -restore -build -warnAsError 0 %*"
exit /b %ErrorLevel%
