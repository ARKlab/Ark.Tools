using Ark.Tools.ResourceWatcher.WorkerHost;


using TestWorker.Dto;

namespace TestWorker.Writer;

public class TestWriter : IResourceProcessor<Test_File, Test_FileMetadataDto>
{
    public TestWriter()
    {
    }

    public Task Process(Test_File file, CancellationToken ctk = default)
    {
        if (file.Metadata.FileName == "TestFileName1")
            throw new InvalidOperationException("Failure handling test");

        return Task.CompletedTask;
    }
}