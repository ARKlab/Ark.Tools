@echo off

call Group_Pack_Core_Debug.bat
call Group_Pack_FTPClient_Debug.bat
call Group_Pack_ResourceWatcher_Debug.bat
rem call Group_Pack_EntityFrameworkCore_Debug.bat
call Group_Pack_RavenDb_Debug.bat
call Group_Pack_AspnetCore_Debug.bat
call Group_Pack_EventSourcing_Debug.bat

pause

:end