using NLog;


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