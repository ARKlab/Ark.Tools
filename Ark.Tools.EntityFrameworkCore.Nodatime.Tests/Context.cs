using Microsoft.EntityFrameworkCore;
using NodaTime;
using System;
using System.Collections.Generic;

namespace Ark.Tools.EntityFrameworkCore.Nodatime.Tests
{
    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options)
            : base(options)
        { }

        public DbSet<EntityA> EntityAs { get; set; }
        public DbSet<EntityB> EntityBs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityA>()
                .OwnsMany(x => x.Addresses, a =>
                {
                    a.HasForeignKey("EntityAId");
                    a.Property<int>("Id");
                    a.HasKey("EntityAId", "Id");
                });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.AddNodaTimeSqlServer();
        }
    }

    public class EntityA
    {
        public int Id { get; set; }
        public List<Address> Addresses { get; set; }
    }

    public class EntityB
    {
        public int Id { get; set; }
        public Address Address { get; set; }

        // nodatime
        public LocalDate LocalDate { get; set; }
        public LocalDateTime LocalDateTime { get; set; }
        public Instant Instant { get; set; }
        public OffsetDateTime OffsetDateTime { get; set; }

        // system
        public DateTime DateTime { get; set; }
        public DateTimeOffset DateTimeOffset { get; set; }
        public TimeSpan TimeSpan { get; set; }
    }

    [Owned]
    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
    }
}
