using Ark.Tools.EntityFrameworkCore.SystemVersioning;
using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using Ark.Tools.Solid;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ODataEntityFrameworkSample.Models
{
    public class ODataSampleContext : AuditDbContext
    {
        public ODataSampleContext(DbContextOptions<ODataSampleContext> options, IContextProvider<ClaimsPrincipal> principalProvider)
          : base(options, principalProvider)
        {            
        }

        public DbSet<Book> Books { get; set; }
        public DbSet<Press> Press { get; set; }

		public DbSet<Country> Countries { get; set; }
		public DbSet<City> Cities { get; set; }
		public DbSet<Test> Tests { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Book>()
                .OwnsOne(c => c.Location)
                ;

            modelBuilder.Entity<Book>()
                .OwnsMany(b => b.Addresses, a =>
                {
                    a.HasForeignKey("BookId");
                    //a.Property<int>("Id");
                    a.HasKey("BookId", "Id");
				});


            modelBuilder.Entity<Book>()
                .HasOne<Audit>("Audit")
                    .WithMany();


			modelBuilder.Entity<Book>()
				.OwnsOne(c => c.Bibliografy, a =>
				{
					a.HasForeignKey("BookId");
					a.HasKey("Id");
					a.ToTable("Bibliography");
					a.OwnsMany<Code>(x => x.Codes, b =>
					{
						b.HasForeignKey("BibliographyId");
						b.HasKey("Id");
					});
				});


			modelBuilder.Entity<Country>()
				.HasMany(x => x.Cities)
					.WithOne()
					.HasForeignKey(x => x.CountryId)
				;

			modelBuilder.AllTemporalTable();
			
		}

		public void UpdateChildCollection<Tparent, Tid, Tchild>(Tparent dbItem, Tparent newItem, Func<Tparent, IEnumerable<Tchild>> selector, Func<Tchild, Tid> idSelector) where Tchild : class
		{
			var dbItems = selector(dbItem).ToList();
			var newItems = selector(newItem).ToList();

			if (dbItems == null && newItems == null)
				return;

			var original = dbItems?.ToDictionary(idSelector) ?? new Dictionary<Tid, Tchild>();
			var updated = newItems?.ToDictionary(idSelector) ?? new Dictionary<Tid, Tchild>();

			var toRemove = original.Where(i => !updated.ContainsKey(i.Key)).ToArray();
			var removed = toRemove.Select(i => this.Entry(i.Value).State = EntityState.Deleted).ToArray();

			var toUpdate = original.Where(i => updated.ContainsKey(i.Key)).ToList();
			toUpdate.ForEach(i => this.Entry(i.Value).CurrentValues.SetValues(updated[i.Key]));

			var toAdd = updated.Where(i => !original.ContainsKey(i.Key)).ToList();
			toAdd.ForEach(i => this.Set<Tchild>().Add(i.Value));
		}

		public void TestUpdate(object item)
		{
			var props = item.GetType().GetProperties();
			foreach (var prop in props)
			{
				object value = prop.GetValue(item);
				if (prop.PropertyType.IsInterface && value != null)
				{
					foreach (var iItem in (System.Collections.IEnumerable)value)
					{
						TestUpdate(iItem);
					}
				}
			}

			int id = (int)item.GetType().GetProperty("Id").GetValue(item);
			if (id == 0)
			{
				this.Entry(item).State = EntityState.Added;
			}
			else
			{
				this.Entry(item).State = EntityState.Modified;
			}

		}
	}
}
