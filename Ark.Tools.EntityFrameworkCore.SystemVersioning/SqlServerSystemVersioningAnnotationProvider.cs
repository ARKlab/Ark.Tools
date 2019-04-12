using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	public class SqlServerSystemVersioningAnnotationProvider : SqlServerMigrationsAnnotationProvider
	{
		public SqlServerSystemVersioningAnnotationProvider(MigrationsAnnotationProviderDependencies dependencies)
			: base(dependencies)
		{
		}

		public override IEnumerable<IAnnotation> For(IEntityType entityType)
		{
			var baseAnnotations = base.For(entityType);

			entityType = entityType.GetBaseEntity();

			var annotationBase = entityType.FindAnnotation(SystemVersioningConstants.SqlServerSystemVersioning);
			if (annotationBase != null)
				return baseAnnotations.Concat(new[] { annotationBase });
			
			return baseAnnotations;
		}
	}
}
