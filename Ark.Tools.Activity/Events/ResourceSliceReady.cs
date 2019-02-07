namespace Ark.Tools.Activity.Messages
{
    public class ResourceSliceReady
    {
        public Resource Resource { get; set; }
        public Slice Slice { get; set; }
    }
}

namespace Ark.Tasks.Messages
{
    using Ark.Tools.Activity;

    public class ResourceSliceReady
    {
        public Resource Resource { get; set; }
        public Slice Slice { get; set; }
    }
}
