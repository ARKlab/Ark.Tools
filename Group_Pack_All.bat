@echo off

call Group_Pack_Core.bat
call Group_Pack_FTPClient.bat
call Group_Pack_ResourceWatcher.bat
call Group_Pack_EntityFrameworkCore.bat
call Group_Pack_RavenDb.bat
call Group_Pack_AspnetCore.bat
call Group_Pack_EventSourcing.bat

pause

:end