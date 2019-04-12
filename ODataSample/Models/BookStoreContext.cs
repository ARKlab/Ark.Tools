using Ark.Tools.EntityFrameworkCore.SystemVersioning;
using Ark.Tools.EntityFrameworkCore.SystemVersioning.Audit;
using Ark.Tools.Solid;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ODataSample.Models
{
    public class BookStoreContext : AuditDbContext
    {
        public BookStoreContext(DbContextOptions<BookStoreContext> options, IContextProvider<ClaimsPrincipal> principalProvider)
          : base(options, principalProvider)
        {            
        }

        public DbSet<Book> Books { get; set; }
        public DbSet<Press> Press { get; set; }

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
                    a.Property<int>("Id");
                    a.HasKey("BookId", "Id");
				});


            modelBuilder.Entity<Book>()
                .HasOne<Audit>("Audit")
                    .WithMany();

			modelBuilder.AllTemporalTable();
			
		}
    }
}
