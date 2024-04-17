using Moq;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public class MockExternalContext
    {
        public Mock<IExternalContext>? MockedService { get; protected set; }

        public void BeforeScenario()
        {
            MockedService = new Mock<IExternalContext>(MockBehavior.Loose);

            MockedService.Setup(x => x.ReadData())
            .Returns(() =>
            {
                
                return 1;
            });
        }
    }
}
