using Ark.Tools.SimpleInjector;


using SimpleInjector;


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Ark.Tools.Activity.Processor;

public static class SliceActivityManagerExtensions
{
    public static void RegisterActivities(this Container container, Type activityManagerType, params Assembly[] fromAssemblies)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(activityManagerType);
        // Contract.Assume(activityManagerType.IsGenericTypeDefinition);
        // Contract.Assume(activityManagerType.GetInterfaces().Where(x => x.IsGenericType).Select(x => x.GetGenericTypeDefinition()).Contains(typeof(ISliceActivityManager<>)));

        container.AllowResolvingFuncFactories();
        var activities = container.GetTypesToRegister<ISliceActivity>(fromAssemblies);

        foreach (var a in activities)
        {
            container.Register(a);
            [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType",
                Justification = "Activity types come from SimpleInjector's GetTypesToRegister which only returns concrete types that implement ISliceActivity. These types are explicitly registered in the container and will not be trimmed.")]
            void RegisterManager() => container.RegisterSingleton(typeof(ISliceActivityManager<>).MakeGenericType(a), activityManagerType.MakeGenericType(a));
            RegisterManager();
        }
    }

    public static void RegisterActivities(this Container container, Type activityManagerType, params Type[] activityTypes)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(activityManagerType);
        // Contract.Requires(Contract.ForAll(activityTypes, a => typeof(ISliceActivity).IsAssignableFrom(a)));
        // Contract.Assume(activityManagerType.IsGenericTypeDefinition);
        // Contract.Assume(activityManagerType.GetInterfaces().Where(x => x.IsGenericType).Select(x => x.GetGenericTypeDefinition()).Contains(typeof(ISliceActivityManager<>)));
        // Contract.Assume(activityManagerType.GetInterfaces().Where(x => x.IsGenericType).Select(x => x.GetGenericTypeDefinition()).Contains(typeof(ISliceActivityManager<>)));

        container.AllowResolvingFuncFactories();

        foreach (var a in activityTypes)
        {
            container.Register(a);
            [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType",
                Justification = "Activity types are explicitly passed as parameters by the caller. The caller is responsible for ensuring these types implement ISliceActivity and will not be trimmed.")]
            void RegisterManager() => container.RegisterSingleton(typeof(ISliceActivityManager<>).MakeGenericType(a), activityManagerType.MakeGenericType(a));
            RegisterManager();
        }
    }

    public static Task StartActivities(this Container container)
    {
        ArgumentNullException.ThrowIfNull(container);

        var managers = (from r in container.GetCurrentRegistrations()
                        where typeof(ISliceActivityManager).IsAssignableFrom(r.ServiceType)
                        select (ISliceActivityManager)r.GetInstance());


        return Task.WhenAll(managers.Select(m => m.Start()));
    }

}