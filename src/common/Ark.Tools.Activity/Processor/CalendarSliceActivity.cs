using NLog;


namespace Ark.Tools.Activity.Processor;


[SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Designed to be overridden")]
public abstract class CalendarSliceActivity : ISliceActivity
{
    private readonly Lazy<Dictionary<Resource, Dictionary<Slice, List<Slice>>>> _reverseMap;

    protected CalendarSliceActivity()
    {
        _reverseMap = new Lazy<Dictionary<Resource, Dictionary<Slice, List<Slice>>>>(
            _buildReverseMap,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IEnumerable<Resource> Resources
    {
        get
        {
            return Dependencies.Select(x => x.Resource).Distinct();
        }
    }

    public abstract ResourceDependency[] Dependencies { get; }
    public abstract ILogger Logger { get; }
    public abstract Resource Resource { get; }

    public abstract TimeSpan? CoolDown { get; }

    public virtual IEnumerable<Slice> ImpactedSlices(Resource resource, Slice slice)
    {
        return _reverseMap.Value[resource][slice];
    }

    public abstract Task Process(Slice activitySlice);
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Legacy API")]
    protected abstract IEnumerable<Slice> _generateCalendar();

    private Dictionary<Resource, Dictionary<Slice, List<Slice>>> _buildReverseMap()
    {
        var calendar = _generateCalendar().ToArray();
        return Dependencies
            .GroupBy(k => k.Resource)
            .ToDictionary(k => k.Key, v => calendar
                .SelectMany(c => v.SelectMany(d => d.GetResourceSlices(c).Select(ds => new { C = c, D = ds })))
                .GroupBy(x => x.D)
                .ToDictionary(x => x.Key, y => y.Select(z => z.C).ToList()));
    }
}