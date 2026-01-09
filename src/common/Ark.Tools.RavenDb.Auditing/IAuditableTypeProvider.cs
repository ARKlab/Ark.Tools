using System;
using System.Collections.Generic;

namespace Ark.Tools.RavenDb.Auditing;

public interface IAuditableTypeProvider
{
    List<Type> TypeList { get; }
}

public class AuditableTypeProvider : IAuditableTypeProvider
{
    public AuditableTypeProvider(List<Type> typeList)
    {
        TypeList = typeList;
    }

    public List<Type> TypeList { get; set; }
}
