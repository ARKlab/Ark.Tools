![image](http://www.ark-energy.eu/wp-content/uploads/ark-dark.png)
# Ark.Tools
This is a set of core libraries developed and maintained by Ark as a set of helper or extensions of the libraries Ark choose to use in their LOB applications.

## Getting Started
All libraries are provided in NuGet.

Support for .NET Framework 4.7.1,.NET Standard 2.x, .NET 5.0. 
Support for other frameworks is up-for-grabs ;)

## Quick Start
The main library used by Ark in its stack are

* [NodaTime](https://nodatime.org/)
* [SimpleInjector](https://simpleinjector.org/)
* [Polly](http://www.thepollyproject.org/)
* [Dapper](http://dapper-tutorial.net/)
* [AspNetCore](https://docs.microsoft.com/en-us/aspnet/core/)

If you want to learn more about each project, look the respective Readme when present or directly at code.
Documentation improvements are up-for-grabs ;)

## Migrate from v2 to v3

- **BREAKING:** Microsoft.AspNetCore v5
   - change netcoreapp3.1 to net5.0 on all projects referencing Ark.Tools.AspNetCore.* projects
- **BREAKING:** from `System.Data.SqlClient` to `Microsoft.Data.SqlClient`
   - remove any Nuget reference to `System.Data.SqlClient` and replace, where needed, with `Microsoft.Data.SqlClient`
- **BREAKING:** upgraded to Flurl v3
   - most usages should be fine, but those that expected Flurl method to return a HttpMessageResponse, as not returns IFlurlResponse **Disposable!**
- **BREAKING:** change to AspNetCore base Startup on RegisterContainer()
   - RegisterContainer() no longer takes IApplicationBuilder parameter but a IServicesCollection as the Container registration has been moved during ConfigureServices()
   - this affects mostly those cases where IServiceProvider was used to check for Tests overrides of mocked services
   - Use IHostEnvironment or services.HasService
- **BREAKING:** change to AspNetCore Startups. Now defaults to System.Text.Json instead of Newtonsoft.Json. 
   - Use the parameter `useNewtonsoftJson: true` of base ctor to keep old behaviour
   - Migrate from the `Ark.Tools.SystemTextJson.JsonPolymorphicConverter` instead of `Ark.Tools.NewtonsoftJson.JsonPolymorphicConverter`

## Contributing
Feel free to send PRs or to raise issues if you spot them. We try our best to improve our libraries.
Please avoid adding more dependencies to 3rd party libraries.

## Links
* [Nuget](https://www.nuget.org/packages/MessagePack.NodaTime/)
* [Github](https://github.com/ARKlab/MessagePack)
* [Ark Energy](http://www.ark-energy.eu/)

## License
This project is licensed under the MIT License - see the [LICENSE](https://github.com/ARKlab/Ark.Tools/blob/master/LICENSE) file for details.

## Licence Claims
A part of this code is taken from StackOverflow or blogs or example. Where possible we included reference to original links 
but if you spot some missing Acknolegment please open an Issue right away.

