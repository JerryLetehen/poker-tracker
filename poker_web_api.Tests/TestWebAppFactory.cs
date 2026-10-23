using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using poker_web_api.Data;

namespace poker_web_api.Tests;

public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all EF Core related registrations
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<PokerDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                    d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Remove the DbContext itself
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PokerDbContext));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            // Add in-memory database
            services.AddDbContext<PokerDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });

        builder.UseEnvironment("Testing");
    }
}
