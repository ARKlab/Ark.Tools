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

"%MSBUILD%" "Ark.Tools.Auth0\Ark.Tools.Auth0.csproj"													/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Core\Ark.Tools.Core.csproj"														/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Http\Ark.Tools.Http.csproj"														/t:Pack /p:Configuration=Release

"%MSBUILD%" "Ark.Tools.NLog\Ark.Tools.NLog.csproj" 														/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.NLog.CloudConfigurationManager\Ark.Tools.NLog.CloudConfigurationManager.csproj" 	/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.NLog.Configuration\Ark.Tools.NLog.Configuration.csproj" 							/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.NLog.ConfigurationManager\Ark.Tools.NLog.ConfigurationManager.csproj" 			/t:Pack /p:Configuration=Release

"%MSBUILD%" "Ark.Tools.Nodatime\Ark.Tools.Nodatime.csproj" 												/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Nodatime.Json\Ark.Tools.Nodatime.Json.csproj"									/t:Pack /p:Configuration=Release
				
"%MSBUILD%" "Ark.Tools.SimpleInjector\Ark.Tools.SimpleInjector.csproj" 									/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Solid\Ark.Tools.Solid.csproj" 													/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Solid.FluentValidaton\Ark.Tools.Solid.FluentValidaton.csproj"					/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Solid.SimpleInjector\Ark.Tools.Solid.SimpleInjector.csproj" 						/t:Pack /p:Configuration=Release
			
"%MSBUILD%" "Ark.Tools.Activity\Ark.Tools.Activity.csproj"												/t:Pack /p:Configuration=Release
	
"%MSBUILD%" "Ark.Tools.SpecFlow\Ark.Tools.SpecFlow.csproj" 												/t:Pack /p:Configuration=Release
				
"%MSBUILD%" "Ark.Tools.Sql\Ark.Tools.Sql.csproj" 														/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Sql.Oracle\Ark.Tools.Sql.Oracle.csproj" 											/t:Pack /p:Configuration=Release
"%MSBUILD%" "Ark.Tools.Sql.SqlServer\Ark.Tools.Sql.SqlServer.csproj" 									/t:Pack /p:Configuration=Release

pause

:end