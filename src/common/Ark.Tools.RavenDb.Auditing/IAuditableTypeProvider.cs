
namespace Ark.Tools.RavenDb.Auditing;

public interface IAuditableTypeProvider
{
    List<Type> TypeList { get; }
}

public sealed class AuditableTypeProvider : IAuditableTypeProvider
{
    public AuditableTypeProvider(List<Type> typeList)
    {
        TypeList = typeList;
    }

    public List<Type> TypeList { get; set; }
}