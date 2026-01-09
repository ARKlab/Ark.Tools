using NLog;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Activity(net10.0)', Before:
namespace Ark.Tools.Activity.Processor
{
    public interface ISliceActivity
    {
        ILogger Logger { get; }

        Resource Resource { get; }

        ResourceDependency[] Dependencies { get; }

        IEnumerable<Slice> ImpactedSlices(Resource resource, Slice slice);

        Task Process(Slice activitySlice);

        TimeSpan? CoolDown { get; }
    }


=======
namespace Ark.Tools.Activity.Processor;

public interface ISliceActivity
{
    ILogger Logger { get; }

    Resource Resource { get; }

    ResourceDependency[] Dependencies { get; }

    IEnumerable<Slice> ImpactedSlices(Resource resource, Slice slice);

    Task Process(Slice activitySlice);

    TimeSpan? CoolDown { get; }
>>>>>>> After
    namespace Ark.Tools.Activity.Processor;

    public interface ISliceActivity
    {
        ILogger Logger { get; }

        Resource Resource { get; }

        ResourceDependency[] Dependencies { get; }

        IEnumerable<Slice> ImpactedSlices(Resource resource, Slice slice);

        Task Process(Slice activitySlice);

        TimeSpan? CoolDown { get; }
    }