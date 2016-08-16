using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MsBuildDesktopApp
{
    public class Program
    {
        public static void Main() { }
    }

    public class TestContextFactory : IDbContextFactory<TestContext>
    {
        public TestContext Create(DbContextFactoryOptions o)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestContext>();
            optionsBuilder.UseSqlite("Filename=./msbuild.db");
            return new TestContext(optionsBuilder.Options);
        }
    }

    public class TestContext : DbContext
    {
        public TestContext(DbContextOptions<TestContext> options) : base (options)
        { }

        public DbSet<ProjectType> ProjectTypes { get; set; }
    }

    public class ProjectType
    {
        [Key]
        public string Name { get; set; }
    }
}