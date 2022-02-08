@echo off
setlocal
set PROGRAMS=%ProgramFiles(x86)%
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

"%MSBUILD%" "Ark.Tools.RavenDb\Ark.Tools.RavenDb.csproj"							/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.RavenDb.Auditing\Ark.Tools.RavenDb.Auditing.csproj"			/t:Pack /p:Configuration=Release

pause

:end