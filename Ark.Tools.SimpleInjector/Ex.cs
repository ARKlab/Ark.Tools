// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;
using SimpleInjector.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Ark.Tools.SimpleInjector
{
    public static partial class Ex
    {
        private static void _resolveCollectionsHandler(object? sender, UnregisteredTypeEventArgs e)
        {
            var container = sender as Container;
            if (container == null) throw new ArgumentNullException(nameof(sender));

            // Only handle IEnumerable<>.
            // Works only with GetAllInstances
            if (!e.UnregisteredServiceType.IsGenericType ||
                e.UnregisteredServiceType.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            {
                return;
            }

            Type serviceType = e.UnregisteredServiceType.GetGenericArguments()[0];

            var registrations = (
                from r in container.GetCurrentRegistrations()
                where r.ServiceType == r.Registration.ImplementationType && serviceType.IsAssignableFrom(r.Registration.ImplementationType)
                select r)
                .ToArray();

            if (registrations.Any())
            {
                var instances = registrations.Select(r => r.GetInstance());

                var castMethod = typeof(Enumerable).GetMethod("Cast")!
                    .MakeGenericMethod(serviceType);

                var castedInstances = castMethod.Invoke(null, new[] { instances });

                e.Register(() => castedInstances!);
            }
        }

        /// <summary>
        /// Allow the container to resolve collection when using GetAllInstances(Type).
        /// </summary>
        /// <param name="container"></param>
        public static void AllowToResolveServicesCollections(this Container container)
        {
            // ensure it's registered only once
            container.ResolveUnregisteredType -= _resolveCollectionsHandler;
            container.ResolveUnregisteredType += _resolveCollectionsHandler;
        }

        public static void RegisterFuncFactory<TService, TImpl>(
                this Container container, Lifestyle? lifestyle = null)
            where TService : class
            where TImpl : class, TService
        {
            lifestyle = lifestyle ?? Lifestyle.Transient;

            // Register the Func<T> that resolves that instance.
            container.RegisterSingleton<Func<TService>>(() =>
            {
                var producer = new InstanceProducer(typeof(TService),
                    lifestyle.CreateRegistration<TImpl>(container));
                producer.Registration.SuppressDiagnosticWarning(DiagnosticType.DisposableTransientComponent, "Ignored during explicit Func Registration");

                Func<TService> instanceCreator =
                    () => (TService)producer.GetInstance();

                //if (container.IsVerifying())
                //{
                //    instanceCreator.Invoke();
                //}

                return instanceCreator;
            });
        }

        private static void _resolveVariantCollectionsHandler(object? sender, UnregisteredTypeEventArgs e)
        {
            var container = sender as Container;
            if (container == null) throw new ArgumentNullException(nameof(sender));

            // Only handle IEnumerable<>.
            // Works only with GetAllInstances
            if (!e.UnregisteredServiceType.IsGenericType ||
                e.UnregisteredServiceType.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            {
                return;
            }

            Type serviceType = e.UnregisteredServiceType.GetGenericArguments()[0];

            if (!serviceType.IsGenericType)
            {
                return;
            }

            Type def = serviceType.GetGenericTypeDefinition();

            var registrations = (
                from r in container.GetCurrentRegistrations()
                where r.ServiceType.IsGenericType
                where r.ServiceType.GetGenericTypeDefinition() == def
                where serviceType.IsAssignableFrom(r.ServiceType)
                select r)
                .ToArray();

            if (registrations.Any())
            {
                var instances = registrations.Select(r => r.GetInstance());

                var castMethod = typeof(Enumerable).GetMethod("Cast")!
                    .MakeGenericMethod(serviceType);

                var castedInstances = castMethod.Invoke(null, new[] { instances });

                e.Register(() => castedInstances!);
            }
        }

        /// <summary>
        /// Allow the container to resolve variant collection when using GetAllInstances(Type).
        /// </summary>
        /// <param name="container"></param>
        public static void AllowToResolveVariantCollections(this Container container)
        {
            // ensure it's registered only once
            container.ResolveUnregisteredType -= _resolveVariantCollectionsHandler;
            container.ResolveUnregisteredType += _resolveVariantCollectionsHandler;
        }

        private static void _resolveVariantTypesHandler(object? sender, UnregisteredTypeEventArgs e)
        {
            var container = sender as Container;
            if (container == null) throw new ArgumentNullException(nameof(sender));
            Type serviceType = e.UnregisteredServiceType;

            if (!serviceType.IsGenericType)
            {
                return;
            }

            Type def = serviceType.GetGenericTypeDefinition();

            var registrations = (
                from r in container.GetCurrentRegistrations()
                where r.ServiceType.IsGenericType
                where r.ServiceType.GetGenericTypeDefinition() == def
                where serviceType.IsAssignableFrom(r.ServiceType)
                select r)
                .ToArray();

            if (!registrations.Any())
            {
                // No registration found. We're done.
            }
            else if (registrations.Length == 1)
            {
                var registration = registrations[0];
                registration.Registration.SuppressDiagnosticWarning(DiagnosticType.DisposableTransientComponent, "Ignored during automatic Variant Registration");
                e.Register(registration.BuildExpression());
            }
            else
            {
                var names = string.Join(", ", registrations
                    .Select(r => string.Format("{0}", r.ServiceType)));

                throw new ActivationException(string.Format(
                    "It is impossible to resolve type {0}, because there are {1} " +
                    "registrations that are applicable. Ambiguous registrations: {2}.",
                    serviceType, registrations.Length, names));
            }
        }

        public static void AllowToResolveVariantTypes(this Container container)
        {
            // ensure it's registered only once
            container.ResolveUnregisteredType -= _resolveVariantTypesHandler;
            container.ResolveUnregisteredType += _resolveVariantTypesHandler;
        }

        private static void _resolvingFuncFactoriesHandler(object? sender, UnregisteredTypeEventArgs e)
        {
            var container = sender as Container;
            if (container == null) throw new ArgumentNullException(nameof(sender));
            var type = e.UnregisteredServiceType;

            if (!type.IsGenericType ||
                type.GetGenericTypeDefinition() != typeof(Func<>))
            {
                return;
            }

            Type serviceType = type.GetGenericArguments().First();

            InstanceProducer producer = container
                .GetRegistration(serviceType, true)!;

            producer.Registration.SuppressDiagnosticWarning(DiagnosticType.DisposableTransientComponent, "Ignored during automatic Func Registration");

            Type funcType =
                typeof(Func<>).MakeGenericType(serviceType);

            var factoryDelegate = Expression.Lambda(funcType,
                producer.BuildExpression()).Compile();

            var registration = Lifestyle.Singleton.CreateRegistration(funcType, () => factoryDelegate, container);

            e.Register(registration);
        }

        public static void AllowResolvingFuncFactories(
            this Container container)
        {
            // ensure it's registered only once
            container.ResolveUnregisteredType -= _resolvingFuncFactoriesHandler;
            container.ResolveUnregisteredType += _resolvingFuncFactoriesHandler;
        }

        private static void _resolvingLazyServicesHandler(object? sender, UnregisteredTypeEventArgs e)
        {
            var container = sender as Container;
            if (container == null) throw new ArgumentNullException(nameof(sender));
            var type = e.UnregisteredServiceType;

            if (!type.IsGenericType ||
                type.GetGenericTypeDefinition() != typeof(Lazy<>))
            {
                return;
            }

            Type serviceType = type.GetGenericArguments().First();

            InstanceProducer registration = container
                .GetRegistration(serviceType, true)!;
            registration.Registration.SuppressDiagnosticWarning(DiagnosticType.DisposableTransientComponent, "Ignored during automatic Lazy Registration");

            Type funcType =
                typeof(Func<>).MakeGenericType(serviceType);

            Type lazyType =
                typeof(Lazy<>).MakeGenericType(serviceType);

            var funcExpression = Expression.Lambda(funcType,
                registration.BuildExpression());

            var lazyDelegate = Expression.Lambda(lazyType,
                Expression.New(lazyType.GetConstructor(new[] { funcType })!, funcExpression)).Compile();

            e.Register(Expression.Constant(lazyDelegate));
        }

        public static void AllowResolvingLazyServices(
            this Container container)
        {
            // ensure it's registered only once
            container.ResolveUnregisteredType -= _resolvingLazyServicesHandler;
            container.ResolveUnregisteredType += _resolvingLazyServicesHandler;
        }

        public static bool HasService<TService>(this Container container) where TService : class
        {
            return container.GetCurrentRegistrations()
                     .Any(x => x.ServiceType == typeof(TService));
        }

        public static bool HasService(this Container container, Type serviceType)
        {
            return container.GetCurrentRegistrations()
                     .Any(x => x.ServiceType == serviceType);
        }

        public static void RequireSingleton<TConcrete>(this Container container) where TConcrete : class
        {
            if (!container.HasService<TConcrete>())
                container.RegisterSingleton<TConcrete>();
        }

        public static void RequireSingleton<TService, TImplementation>(this Container container)
            where TService : class
            where TImplementation : class, TService
        {
            if (!container.HasService<TService>())
                container.RegisterSingleton<TService, TImplementation>();
        }

        [Obsolete("Use RegisterInstance")]
        public static void RequireSingleton<TService>(this Container container, TService instance) where TService : class
        {
            if (!container.HasService<TService>())
                container.RegisterSingleton<TService>(instance);
        }

        public static void RequireInstance<TService>(this Container container, TService instance) where TService : class
        {
            if (!container.HasService<TService>())
                container.RegisterInstance(instance);
        }

        public static void Require<TConcrete>(this Container container) where TConcrete : class
        {
            if (!container.HasService<TConcrete>())
                container.Register<TConcrete>();
        }

        public static void Require<TService, TImplementation>(this Container container)
            where TService : class
            where TImplementation : class, TService
        {
            if (!container.HasService<TService>())
                container.Register<TService, TImplementation>();
        }
    }
}
