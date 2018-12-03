@echo off

call Group_Pack_Core.bat
call Group_Pack_AspnetCore.bat
call Group_Pack_FTPClient.bat
call Group_Pack_ResourceWatcher.bat

pause

:end