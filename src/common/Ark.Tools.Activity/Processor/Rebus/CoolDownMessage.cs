namespace Ark.Tools.Activity.Messages;

public sealed class CoolDownMessage
{
    public Resource Resource { get; set; }

    public Slice ResourceSlice { get; set; }

    public Slice ActivitySlice { get; set; }
}