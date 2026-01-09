using Ark.Tools.SimpleInjector;


using SimpleInjector;

using System;

using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
            container.RegisterSingleton(typeof(ISliceActivityManager<>).MakeGenericType(a), activityManagerType.MakeGenericType(a));
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
            container.RegisterSingleton(typeof(ISliceActivityManager<>).MakeGenericType(a), activityManagerType.MakeGenericType(a));
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
