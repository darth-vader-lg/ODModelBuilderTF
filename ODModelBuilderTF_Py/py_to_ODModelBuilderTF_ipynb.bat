@echo off
rem Updating of the train.ipynb with the content of the python modules
setlocal
call set_sync_jupyter_notebook_env.bat
if ERRORLEVEL 1 exit /b %ERRORLEVEL%
%SyncJupyterNotebook% nb ODModelBuilderTF.ipynb
