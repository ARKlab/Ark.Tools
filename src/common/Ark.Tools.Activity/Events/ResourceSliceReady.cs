namespace Ark.Tools.Activity.Messages;

public sealed class ResourceSliceReady
{
    public Resource Resource { get; set; }
    public Slice Slice { get; set; }
}