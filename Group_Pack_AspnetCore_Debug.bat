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

"%MSBUILD%" "Ark.Tools.AspNetCore\Ark.Tools.AspNetCore.csproj"																			/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.ApplicationInsights\Ark.Tools.AspNetCore.ApplicationInsights.csproj"									/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.Auth0\Ark.Tools.AspNetCore.Auth0.csproj"																/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.BasicAuthAuth0Proxy\Ark.Tools.AspNetCore.BasicAuthAuth0Proxy.csproj"									/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy\Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy.csproj"	/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.CommaSeparatedParameters\Ark.Tools.AspNetCore.CommaSeparatedParameters.csproj"						/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.MessagePack\Ark.Tools.AspNetCore.MessagePack.csproj"												    /t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.NestedStartup\Ark.Tools.AspNetCore.NestedStartup.csproj"											    /t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.RavenDb\Ark.Tools.AspNetCore.RavenDb.csproj"															/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.Swashbuckle\Ark.Tools.AspNetCore.Swashbuckle.csproj"												    /t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.XlsxOutputFormatter\Ark.Tools.AspNetCore.XlsxOutputFormatter.csproj"								    /t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.AspNetCore.HealthChecks\Ark.Tools.AspNetCore.HealthChecks.csproj"												/t:Pack /p:Configuration=Debug

pause

:end