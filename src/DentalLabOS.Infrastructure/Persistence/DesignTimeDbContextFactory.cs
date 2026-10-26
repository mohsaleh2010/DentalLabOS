using DentalLabOS.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DentalLabOS.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to build the model for migrations (no database connection is opened).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__DentalLabOS") ?? "Server=localhost;Database=DentalLabOS_Design;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new AppDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public bool IsPlatformContext => false;
        public IDisposable BeginScope(Guid tenantId) => throw new NotSupportedException();
    }
}
