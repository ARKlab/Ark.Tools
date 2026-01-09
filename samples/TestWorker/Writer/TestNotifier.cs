using Ark.Tools.Activity;
using Ark.Tools.Activity.Provider;
using Ark.Tools.ResourceWatcher.WorkerHost;

using NodaTime;

using System.Threading;
using System.Threading.Tasks;

using TestWorker.Constants;
using TestWorker.Dto;

namespace TestWorker.Writer;

sealed class TestWriter_Notifier : IResourceProcessor<Test_File, Test_FileMetadataDto>
{
    private readonly RebusResourceNotifier _notifier;

    public TestWriter_Notifier(RebusResourceNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task Process(Test_File file, CancellationToken ctk = default)
    {
        var zonedNow = new LocalDate(2019, 01, 01).AtStartOfDayInZone(DateTimeZoneProviders.Tzdb[Test_Constants.DataTimezone]);
        var notifymessage = "first";


        await _notifier.Notify(notifymessage, Slice.From(zonedNow)).ConfigureAwait(false);
    }
}