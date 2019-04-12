using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.Sql;
using System.Linq;
using Ark.Tools.EntityFrameworkCore.SystemVersioning.ExpressionsOverride;
using Ark.Tools.EntityFrameworkCore.SystemVersioning.Generators;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	public static class Ex
	{
		public static void AllTemporalTable(this ModelBuilder modelBuilder)
		{
			var entityTypes = modelBuilder.Model.GetEntityTypes().ToList();

			var baseEntities = entityTypes.Where(w => w.GetBaseEntity().Name == w.Name).ToList();

			foreach (var entityType in baseEntities)
			{
				foreach (var annotation in entityType.Model.GetAnnotations())
				{
					if (annotation.Name != SystemVersioningConstants.SqlServerSystemVersioning)
						entityType.SetAnnotation(SystemVersioningConstants.SqlServerSystemVersioning, true);
				}
			}
		}

		public static IEntityType GetBaseEntity(this IEntityType entityType)
		{
			var foreignKeys = entityType.GetForeignKeys().Where(x => x.IsOwnership);

			if (foreignKeys.Any() && foreignKeys.Count() == 1)
			{
				return GetBaseEntity(foreignKeys.Single().PrincipalEntityType);
			}

			return entityType;
		}

		public static DbContextOptionsBuilder AddSqlServerSystemVersioningAudit(this DbContextOptionsBuilder optionsBuilder)
		{
			//Temporal Tables
			optionsBuilder.ReplaceService<IMigrationsSqlGenerator, SqlServerSystemVersioningSqlGenerator>();
			optionsBuilder.ReplaceService<IMigrationsAnnotationProvider, SqlServerSystemVersioningAnnotationProvider>();

			//System Versioning 
			optionsBuilder.ReplaceService<INodeTypeProviderFactory, SqlServerSystemVersioningMethodInfoTypeRegistryFactory>();
			optionsBuilder.ReplaceService<ISelectExpressionFactory, SqlServerSystemVersioningSelectExpressionFactory>();
			optionsBuilder.ReplaceService<IQuerySqlGeneratorFactory, SqlServerSystemVersioningQuerySqlGeneratorFactory>();

			return optionsBuilder;
		}
	}
}
