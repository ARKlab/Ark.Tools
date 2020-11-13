using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
    /// <summary>
    /// Common definition of transactional 'Context', disposable and committable.
    /// </summary>
    public interface IContext : IDisposable
    {
        void Commit();
    }
}
