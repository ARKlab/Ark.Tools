@echo off
setlocal
set PROGRAMS=%ProgramFiles(x86)%
for %%e in (Community Professional Enterprise) do (
    if exist "%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD=%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\MSBuild.exe"
    )
)
if exist "%MSBUILD%" goto :operations

:nomsbuild
echo Microsoft Build version 15.1 (or later) does not appear to be
echo installed on this machine, which is required to build the solution.
goto end

:operations
setlocal

"%MSBUILD%" "Ark.Tools.Auth0\Ark.Tools.Auth0.csproj"													/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Core\Ark.Tools.Core.csproj"														/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Http\Ark.Tools.Http.csproj"														/t:Pack /p:Configuration=Debug

"%MSBUILD%" "Ark.Tools.ApplicationInsights\Ark.Tools.ApplicationInsights.csproj"								/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.ApplicationInsights.HostedService\Ark.Tools.ApplicationInsights.HostedService.csproj"	/t:Pack /p:Configuration=Debug

"%MSBUILD%" "Ark.Tools.NLog\Ark.Tools.NLog.csproj" 														/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.NLog.CloudConfigurationManager\Ark.Tools.NLog.CloudConfigurationManager.csproj" 	/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.NLog.Configuration\Ark.Tools.NLog.Configuration.csproj" 							/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.NLog.ConfigurationManager\Ark.Tools.NLog.ConfigurationManager.csproj" 			/t:Pack /p:Configuration=Debug

"%MSBUILD%" "Ark.Tools.Nodatime\Ark.Tools.Nodatime.csproj" 												/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Nodatime.Json\Ark.Tools.Nodatime.Json.csproj"									/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Nodatime.Dapper\Ark.Tools.Nodatime.Dapper.csproj" 								/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Nodatime.SystemTextJson\Ark.Tools.Nodatime.SystemTextJson.csproj"				/t:Pack /p:Configuration=Debug

"%MSBUILD%" "Ark.Tools.SimpleInjector\Ark.Tools.SimpleInjector.csproj" 									/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Solid\Ark.Tools.Solid.csproj" 													/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Solid.FluentValidaton\Ark.Tools.Solid.FluentValidaton.csproj"					/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Solid.SimpleInjector\Ark.Tools.Solid.SimpleInjector.csproj" 						/t:Pack /p:Configuration=Debug
	
"%MSBUILD%" "Ark.Tools.Rebus\Ark.Tools.Rebus.csproj"													/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Activity\Ark.Tools.Activity.csproj"												/t:Pack /p:Configuration=Debug
	
"%MSBUILD%" "Ark.Tools.SpecFlow\Ark.Tools.SpecFlow.csproj" 												/t:Pack /p:Configuration=Debug
				
"%MSBUILD%" "Ark.Tools.Sql\Ark.Tools.Sql.csproj" 														/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Sql.Oracle\Ark.Tools.Sql.Oracle.csproj" 											/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Sql.SqlServer\Ark.Tools.Sql.SqlServer.csproj" 									/t:Pack /p:Configuration=Debug

"%MSBUILD%" "Ark.Tools.SystemTextJson\Ark.Tools.SystemTextJson.csproj" 									/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.NewtonsoftJson\Ark.Tools.NewtonsoftJson.csproj" 									/t:Pack /p:Configuration=Debug

"%MSBUILD%" "Ark.Tools.Authorization\Ark.Tools.Authorization.csproj" 									/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Solid.Authorization\Ark.Tools.Solid.Authorization.csproj" 						/t:Pack /p:Configuration=Debug

"%MSBUILD%" "Ark.Tools.Outbox\Ark.Tools.Outbox.csproj" 													/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Outbox.Rebus\Ark.Tools.Outbox.Rebus.csproj" 										/t:Pack /p:Configuration=Debug
"%MSBUILD%" "Ark.Tools.Outbox.SqlServer\Ark.Tools.Outbox.SqlServer.csproj" 								/t:Pack /p:Configuration=Debug

pause

:end