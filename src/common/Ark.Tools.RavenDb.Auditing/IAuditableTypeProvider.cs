using System;
using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.RavenDb.Auditing(net10.0)', Before:
namespace Ark.Tools.RavenDb.Auditing
{
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


=======
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
>>>>>>> After
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