using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Security.Claims;
using Ark.Tools.Solid;
using Newtonsoft.Json;

namespace Ark.Tools.EntityFrameworkCore.SystemVersioning.Audit
{
    public abstract class AuditDbContext : DbContext
    {
		private IContextProvider<ClaimsPrincipal> _principalProvider;

		public AuditDbContext(DbContextOptions options, IContextProvider<ClaimsPrincipal> principalProvider)
        : base(options)
        {
			_principalProvider = principalProvider;
		}

		public DbSet<Audit> Audits { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Audit>()
				.HasKey(p => p.AuditId);

			modelBuilder.Entity<Audit>()
				.HasAnnotation(SystemVersioningConstants.SqlServerSystemVersioning, true);

			modelBuilder.Entity<Audit>()
				.Property(p => p.Timestamp)
				.HasComputedColumnSql("[SysStartTime]");

			modelBuilder.Entity<Audit>()
				.OwnsMany(b => b.AffectedEntities, affectedEntity =>
				{
					affectedEntity.HasKey(p => p.EntityId);
					affectedEntity.HasForeignKey(p => p.AuditId);
					
					affectedEntity.Property(x => x.KeyValues)
						.HasConversion(
						v => JsonConvert.SerializeObject(v),
						v => JsonConvert.DeserializeObject<Dictionary<string, object>>(v));

					affectedEntity.Ignore(p => p.TemporaryProperties);
				});
		}

        public override int SaveChanges()
        {
            var result = 0;
            var guid = Guid.NewGuid();

            bool isAuditable = _hasAuditableEntity();

            if (isAuditable)
            {
                var auditEntries = _onBeforeSaveChanges(guid);
                result = base.SaveChanges() - 1;

                if (auditEntries.Entity.AffectedEntities.Any(x => x.TemporaryProperties.Any()))
                {
                    _onAfterSaveChanges(auditEntries);
                    var res = base.SaveChanges();
                }
            }
            else
                result = base.SaveChanges();

            return result;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ctk = default(CancellationToken))
        {
            var result = 0;
            var guid = Guid.NewGuid();

            bool isAuditable = _hasAuditableEntity();

            if (isAuditable)
            {
                var auditEntries = _onBeforeSaveChanges(guid);
                result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ctk) - 1;
                if (auditEntries.Entity.AffectedEntities.Any(x => x.TemporaryProperties.Any()))
                {
                    _onAfterSaveChanges(auditEntries);
                    await base.SaveChangesAsync(ctk);
                }
            }
            else
                result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ctk);

            return result;
        }

        private bool _hasAuditableEntity()
        {
            return ChangeTracker.Entries().Select(s => s.Entity).OfType<IAuditable>().Any();
        }

        private EntityEntry<Audit> _onBeforeSaveChanges(Guid guid)
        {
            ChangeTracker.DetectChanges();

            var auditEntry = new Audit()
            {
                AuditId = guid,
				UserId =  _principalProvider.Current?.Identity?.Name
			};

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Audit || entry.Entity is AffectedEntity || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                if (entry.Entity is IAuditable a)
                {
                    a.AuditId = guid;

                    var affectedEntityEntry = new AffectedEntity()
                    {
                        AuditId = guid,
                        EntityAction = entry.State.ToString(),
                        TableName = entry.Metadata.Relational().TableName
                    };

                    foreach (var property in entry.Properties)
                    {
                        string propertyName = property.Metadata.Name;

                        if (property.Metadata.IsPrimaryKey())
                        {
                            if (property.IsTemporary)
                            {
                                affectedEntityEntry.KeyValues[propertyName] = null;
                                affectedEntityEntry.TemporaryProperties.Add(property);
                            }
                            else
                                affectedEntityEntry.KeyValues[propertyName] = property.CurrentValue;
                        }
                    }

                    auditEntry.AffectedEntities.Add(affectedEntityEntry);
                }
            }

            return Audits.Add(auditEntry);

        }

        private void _onAfterSaveChanges(EntityEntry<Audit> audit)
        {
			foreach (var affectedEntityEnty in audit.Entity.AffectedEntities)
            {
                // Get the final value of the temporary properties
                foreach (var prop in affectedEntityEnty.TemporaryProperties)
                {
					if (prop.Metadata.IsPrimaryKey())
					{
						affectedEntityEnty.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
					}
				}

				affectedEntityEnty.KeyValues = new Dictionary<string, object>(affectedEntityEnty.KeyValues);
			}

			Audits.Update(audit.Entity);
        }
    }

}
