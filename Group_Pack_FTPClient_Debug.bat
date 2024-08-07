@echo off
setlocal
set PROGRAMS=%ProgramFiles%
for %%e in (Community Professional Enterprise) do (
    if exist "%PROGRAMS%\Microsoft Visual Studio\2022\%%e\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD=%PROGRAMS%\Microsoft Visual Studio\2022\%%e\MSBuild\Current\Bin\MSBuild.exe"
    )
)
if exist "%MSBUILD%" goto :operations

:nomsbuild
echo Microsoft Build version 17.0 (or later) does not appear to be
echo installed on this machine, which is required to build the solution.
goto end

:operations
setlocal

"%MSBUILD%" "Ark.Tools.FtpClient.ArxOne\Ark.Tools.FtpClient.ArxOne.csproj"							/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.FtpClient.Core\Ark.Tools.FtpClient.Core.csproj"								/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.FtpClient.FluentFtp\Ark.Tools.FtpClient.FluentFtp.csproj"					/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.FtpClient.FtpProxy\Ark.Tools.FtpClient.FtpProxy.csproj"						/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.FtpClient.SftpClient\Ark.Tools.FtpClient.SftpClient.csproj"					/t:Pack /p:Configuration=Debug

pause

:end