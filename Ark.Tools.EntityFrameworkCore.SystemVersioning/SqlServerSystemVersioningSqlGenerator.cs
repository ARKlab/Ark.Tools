using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning
{
	public class SqlServerSystemVersioningSqlGenerator : SqlServerMigrationsSqlGenerator
	{
		public SqlServerSystemVersioningSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IMigrationsAnnotationProvider migrationsAnnotations)
			: base(dependencies, migrationsAnnotations)
		{
		}

		protected override void Generate(CreateTableOperation operation, IModel model, MigrationCommandListBuilder builder)
		{
			var memoryOptimized = operation[SqlServerAnnotationNames.MemoryOptimized] as bool? == true;
			var temporal = operation[SystemVersioningConstants.SqlServerSystemVersioning] as bool? == true;
					   
			builder
			.Append("CREATE TABLE ")
			.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema))
			.AppendLine(" (");

			var schema = operation.Schema ?? "dbo";

			using (builder.Indent())
			{
				for (var i = 0; i < operation.Columns.Count; i++)
				{
					var column = operation.Columns[i];
					ColumnDefinition(column, model, builder);

					if (i != operation.Columns.Count - 1)
					{
						builder.AppendLine(",");
					}
				}

				if (operation.PrimaryKey != null)
				{
					builder.AppendLine(",");
					PrimaryKeyConstraint(operation.PrimaryKey, model, builder);
				}

				foreach (var uniqueConstraint in operation.UniqueConstraints)
				{
					builder.AppendLine(",");
					UniqueConstraint(uniqueConstraint, model, builder);
				}

				foreach (var foreignKey in operation.ForeignKeys)
				{
					builder.AppendLine(",");
					ForeignKeyConstraint(foreignKey, model, builder);
				}

				if (temporal)
				{
					builder.AppendLine(",");
					builder.Append(
			   			@"[SysStartTime] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
			   			[SysEndTime] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,  
			   			PERIOD FOR SYSTEM_TIME([SysStartTime], [SysEndTime])");
				}

				builder.AppendLine();
			}

			builder.Append(")");


			if (memoryOptimized || temporal)
			{
				builder.AppendLine();
				using (builder.Indent())
				{
					builder.AppendLine("WITH (");
					using (builder.Indent())
					{
						if (memoryOptimized)
						{
							builder.Append("MEMORY_OPTIMIZED = ON");
						}

						if (temporal)
							builder.Append($"SYSTEM_VERSIONING = ON (HISTORY_TABLE = [{schema}].[{operation.Name}History])");
					}
					builder.AppendLine(")");
				}
			}

			builder
				.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator)
				.EndCommand(suppressTransaction: memoryOptimized);
		}
	}
}
