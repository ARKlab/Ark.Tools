using NLog;


namespace Ark.Tools.Activity.Processor;


[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Designed to be overridden")]
public abstract class CalendarSliceActivity : ISliceActivity
{
    private readonly IEnumerable<Slice> _calendar;
    private readonly Dictionary<Resource, Dictionary<Slice, List<Slice>>> _reverseMap;

    protected CalendarSliceActivity()
    {
        _calendar = _generateCalendar();
        _reverseMap = Dependencies
            .GroupBy(k => k.Resource)
            .ToDictionary(k => k.Key, v => _calendar.SelectMany(c => v.SelectMany(d => d.GetResourceSlices(c).Select(ds => new { C = c, D = ds })))
                .GroupBy(x => x.D)
                .ToDictionary(x => x.Key, y => y.Select(z => z.C).ToList())
                );
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
        return _reverseMap[resource][slice];
    }

    public abstract Task Process(Slice activitySlice);
    protected abstract IEnumerable<Slice> _generateCalendar();
}