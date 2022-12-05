# Minimal linux web app:

This service has minimal configuration to be hosted under azure linux environment.
In the below list there are some usefull notes of the problems faced during the implementation of the service
- Service is derived from "BackgroundService"
- Configuration in Linux AzureWebApp should be changed:
  | From | To |
  | ------ | ------ |
  | KeyVault:BaseUrl | KeyVault__BaseUrl |
	Because Linux is translating '__' to ':'
- ConnectionStrings in Linux AzureWebApp are converted from '.' to '_'
	It has been added logic inside function AddArkLegacyEnvironmentVariables, to duplicate ConnectionStrings with '_' to '.'
- Application logs in Azure are on the **App Service** -> **Diagnose and solve problems** -> **Application Logs** (also **Platform** logs are visible in this page).
  Other specific logs are browsable from the left menu in this section (**Container Crash** and others).
  **Log Stream** from **Monitoring** menu, is also showing application log, but it suffer of some lag. 
- **Startup.cs** is empty but was necessary to have and call from the **Program.cs** (UseStartup<Startup>()) in order to prevent the following error:
  >InvalidOperationException: No application configured. Please specify an application via IWebHostBuilder.UseStartup

Ark S.r.l.