using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.Activity
{
    public class ResourceDependency
    {
        public Resource Resource { get; internal set; }

        internal Func<Slice, IEnumerable<Slice>> _getDependentSlice = s => Enumerable.Empty<Slice>();

        public virtual IEnumerable<Slice> GetResourceSlices(Slice activitySlice)
        {
            return _getDependentSlice(activitySlice);
        }

        public static ResourceDependency OneSlice(Resource resource, Func<Slice, Slice> getDependentSourceSlice)
        {
            return new ResourceDependency()
            {
                Resource = resource,
                _getDependentSlice = s => [getDependentSourceSlice(s)]
            };
        }

        public static ResourceDependency OneSlice(string provider, string resourceId, Func<Slice, Slice> getDependentSourceSlice)
        {
            return OneSlice(Resource.Create(provider, resourceId), getDependentSourceSlice);
        }

        public static ResourceDependency ManySlices(Resource source, Func<Slice, IEnumerable<Slice>> getDependentSourceSlice)
        {
            return new ResourceDependency()
            {
                Resource = source,
                _getDependentSlice = getDependentSourceSlice
            };
        }

        public static ResourceDependency ManySlices(string provider, string resourceId, Func<Slice, IEnumerable<Slice>> getDependentSourceSlice)
        {
            return ManySlices(Resource.Create(provider, resourceId), getDependentSourceSlice);
        }
    }
}
