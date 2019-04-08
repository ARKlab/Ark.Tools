using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace Ark.Tools.EntityFrameworkCore.Nodatime.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices(s =>
                {
                    s.AddDbContext<Context>(o => o.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=Suca;Integrated Security=True"));
                })
                .Build()
                ;

            using (host)
            {
                host.Start();
                using (var ctx = host.Services.GetService<Context>())
                {
                    ctx.EntityBs.Where(x => x.OffsetDateTime.ToDateTimeOffset() > x.OffsetDateTime.ToDateTimeOffset()).ToList();
                    ctx.EntityBs.Where(x => x.OffsetDateTime.Date > x.OffsetDateTime.Date).ToList();
                    ctx.EntityBs.Where(x => x.LocalDate.PlusDays(1) > x.LocalDateTime.Date).ToList();
                    ctx.EntityBs.Where(x => x.LocalDateTime.ToDateTimeUnspecified() == DateTime.Now).ToList();

                    ctx.EntityBs.Add(new EntityB
                    {
                        Address = new Address
                        {
                            City = "string",
                            Street = "string"
                        }
                    });

                    ctx.SaveChanges(); 
                }
            }
        }
    }
}
