namespace Ark.Tools.Core.EntityTag
{
    public interface IEntityWithETag
    {
        string _ETag { get; set; }
    }
}
