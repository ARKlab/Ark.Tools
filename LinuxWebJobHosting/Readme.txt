Minimal linux web app:
- Service is derived from "BackgroundService"
- Configuration in AzureWebApp should be changed:
	KeyVault:BaseUrl --> KeyVault__BaseUrl
	Because Linux is translating '__' to ':'
- ConnectionStrings in AzureWebApp are converted from '.' to '_'
	Workers.Database has been converted to Workers_Database
- Application logs in Azure are on the "App Service" -> "Diagnose and solve problems" -> "Application Logs" (also Platform logs are visible in this page).
  Other specific logs are browsable from the left menu in this section (Container Crash and others).
  "Log Stream" from "Monitoring" menu, is also showing application log, but it suffer of some lag. 
- "Startup.cs" is empty but was necessary to have and call from the "Program.cs" (UseStartup<Startup>()) in order to prevent the following error:
  InvalidOperationException: No application configured. Please specify an application via IWebHostBuilder.UseStartup