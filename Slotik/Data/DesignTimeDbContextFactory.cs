using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Slotik.Data;

// Load the same configuration providers as the application without invoking Program.Main.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(DesignTimeDbContextFactory).Assembly.GetName().Name
        });
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is missing. Configure it through .NET User Secrets " +
                "in Development or the ConnectionStrings__DefaultConnection environment variable. " +
                "Run EF from the Slotik project directory; pass -- --environment Development to use User Secrets.");

        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
