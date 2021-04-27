@echo off
rem Updating of the python modules with the content of the train.ipynb
setlocal
call set_sync_jupyter_notebook_env.bat
if ERRORLEVEL 1 exit /b %ERRORLEVEL%
%SyncJupyterNotebook% py ODModelBuilderTF.ipynb
