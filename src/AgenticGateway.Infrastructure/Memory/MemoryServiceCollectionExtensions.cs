using AgenticGateway.Core.Memory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticGateway.Infrastructure.Memory;

public static class MemoryServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteMemory(this IServiceCollection services, IConfiguration configuration)
    {
        var databasePath = configuration["Memory:DatabasePath"]
            ?? Environment.GetEnvironmentVariable("AGENTIC_GATEWAY_MEMORY_DB")
            ?? Path.Combine(AppContext.BaseDirectory, "data", "memory.db");

        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
            Pooling = true
        }.ToString();

        services.AddDbContextFactory<MemoryDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMemoryStore, SqliteMemoryStore>();
        services.AddSingleton<MemoryService>();
        services.AddSingleton<MemoryDatabaseInitializer>();
        return services;
    }
}
