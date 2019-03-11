using Ark.Tools.ResourceWatcher.WorkerHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestWorker.Dto;

namespace TestWorker.Writer
{
    public class TestWriter : IResourceProcessor<Test_File, Test_FileMetadataDto>
    {
        public TestWriter()
        {
        }

        public Task Process(Test_File file, CancellationToken ctk = default)
        {
            return Task.CompletedTask;
        }
    }
}
