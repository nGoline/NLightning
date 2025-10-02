using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Extensions;

using Infrastructure.Persistence.Contexts;

public static class DatabaseExtensions
{
    /// <summary>
    /// Runs database migrations if configured to do so
    /// </summary>
    public static async Task MigrateDatabaseIfConfiguredAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Check if migrations should run
        var runMigrations = configuration.GetValue("Database:RunMigrations", false);

        if (!runMigrations)
        {
            logger.LogInformation("Database migrations are disabled in configuration");
            return;
        }

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<NLightningDbContext>();

            // Check if there are pending migrations
            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();

            if (pendingMigrations.Count > 0)
            {
                logger.LogInformation("Found {Count} pending migrations. Applying...", pendingMigrations.Count);
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations completed successfully");
            }
            else
            {
                logger.LogInformation("Database is up to date, no migrations needed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations");
            throw;
        }
    }
}