@echo off
setlocal
set PROGRAMS=%ProgramFiles(x86)%
for %%e in (Community Professional Enterprise) do (
    if exist "%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\MSBuild.exe" (
        set "MSBUILD=%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\MSBuild.exe"
    )
)
if exist "%MSBUILD%" goto :operations

:nomsbuild
echo Microsoft Build version 15.1 (or later) does not appear to be
echo installed on this machine, which is required to build the solution.
goto end

:operations
setlocal

"%MSBUILD%" "Ark.Tools.ResourceWatcher\Ark.Tools.ResourceWatcher.csproj"										/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.ResourceWatcher.ApplicationInsights\Ark.Tools.ResourceWatcher.ApplicationInsights.csproj"/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.ResourceWatcher.Sql\Ark.Tools.ResourceWatcher.Sql.csproj"								/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.ResourceWatcher.WorkerHost\Ark.Tools.ResourceWatcher.WorkerHost.csproj"					/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.ResourceWatcher.WorkerHost.Ftp\Ark.Tools.ResourceWatcher.WorkerHost.Ftp.csproj"			/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.ResourceWatcher.WorkerHost.Hosting\Ark.Tools.ResourceWatcher.WorkerHost.Hosting.csproj"	/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.ResourceWatcher.WorkerHost.Sql\Ark.Tools.ResourceWatcher.WorkerHost.Sql.csproj"			/t:Pack /p:Configuration=Release

pause

:end