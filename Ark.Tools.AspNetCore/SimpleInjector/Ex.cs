using Microsoft.AspNetCore.Builder;
using SimpleInjector;
using SimpleInjector.Diagnostics;

namespace Ark.AspNetCore
{
    public static partial class Ex
    {
        /// <summary>
        /// Cross-wires a registration made in the ASP.NET configuration into Simple Injector with the
        /// <see cref="Lifestyle.Transient">Transient</see> lifestyle, to allow that instance to be injected 
        /// into application components. The service is taken from the ASP.NET request scope.
        /// </summary>
        /// <typeparam name="TService">The type of the ASP.NET abstraction to cross-wire.</typeparam>
        /// <param name="container">The container to cross-wire that registration in.</param>
        /// <param name="applicationBuilder">The ASP.NET application builder instance that references all
        /// framework components.</param>
        public static void CrossWireRequest<TService>(this Container container, IApplicationBuilder applicationBuilder)
            where TService : class
        {
            // Always use the transient lifestyle, because we have no clue what the lifestyle in ASP.NET is,
            // and scoped and singleton lifestyles will dispose instances, while ASP.NET controls them.
            var registration = Lifestyle.Transient.CreateRegistration(
                () => applicationBuilder.GetRequestService<TService>(),
                container);

            // Prevent Simple Injector from throwing exceptions when the service type is disposable (yuck!).
            // Implementing IDisposable on abstractions is a serious design flaw, but ASP.NET does it anyway :-(
            registration.SuppressDiagnosticWarning(DiagnosticType.DisposableTransientComponent, "Owned by ASP.NET");

            container.AddRegistration(typeof(TService), registration);
        }
    }
}
