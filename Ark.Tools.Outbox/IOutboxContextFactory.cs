using Ark.Tools.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox
{

    public interface IOutboxContextFactory
    {
        IOutboxContext Create();
    }
}
