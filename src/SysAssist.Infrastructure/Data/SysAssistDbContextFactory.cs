using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SysAssist.Infrastructure.Data;

public sealed class SysAssistDbContextFactory : IDesignTimeDbContextFactory<SysAssistDbContext>
{
    public SysAssistDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SysAssistDb")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("SYSASSIST_MIGRATION_CONNECTION")
            ?? "Host=localhost;Port=26257;Database=sysassist;Username=root;Password=;SSL Mode=Disable";
        var optionsBuilder = new DbContextOptionsBuilder<SysAssistDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(SysAssistDbContext).Assembly.FullName));

        return new SysAssistDbContext(optionsBuilder.Options);
    }
}
