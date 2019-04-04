using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ark.Tools.EntityFrameworkCore.Nodatime.Tests
{
    public class ContextFactory : IDesignTimeDbContextFactory<Context>
    {
        public Context CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<Context>();
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=Suca;Integrated Security=True");

            return new Context(optionsBuilder.Options);
        }
    }
}
