@echo off
py -3.7 install_virtual_environment.py --custom-tf-dir "%USERPROFILE%\Packages"
EXIT /B %errorlevel%
