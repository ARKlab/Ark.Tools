using System;
using NodaTime;

namespace Ark.Tools.ResourceWatcher
{

    public class ResourceState : IResourceTrackedState
    {
        public virtual string Tenant { get; set; }
        public virtual string ResourceId { get; set; }
        public virtual string CheckSum { get; set; }
        public virtual LocalDateTime Modified { get; set; }
        public virtual Instant LastEvent { get; set; }
        public virtual Instant? RetrievedAt { get; set; }
        public virtual int RetryCount { get; set; }
        public virtual object Extensions { get; set; }
        public virtual Exception LastException { get; set; }
    }
}
