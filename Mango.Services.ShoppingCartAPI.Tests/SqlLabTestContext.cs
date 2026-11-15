using System.Data.Common;
using Mango.Services.ShoppingCartAPI.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mango.Services.ShoppingCartAPI.Tests;

internal sealed class SqlLabTestContext(DbContextOptions<AppDbContext> options, string[]? owners = null) : AppDbContext(options)
{
    public int SaveCalls { get; set; }

    public static SqlLabTestContext Create(params string[] owners)
    {
        var builder = new SqlConnectionStringBuilder { DataSource = ".", InitialCatalog = "Mango_ShoppingCart_Concurrency_Test", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
        var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN DB_NAME() = N'Mango_ShoppingCart_Concurrency_Test' AND CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) = N'22LAP5CD1195RMS' AND SERVERPROPERTY('InstanceName') IS NULL THEN 1 ELSE 0 END";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) { connection.Dispose(); throw new InvalidOperationException("Unexpected SQL laboratory target"); }
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection, o => o.CommandTimeout(10)).AddInterceptors(new WriteCounter()).Options;
        return new SqlLabTestContext(opts, owners);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        return base.SaveChangesAsync(cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        if (owners is not null)
        {
            await CartHeaders.Where(x => owners.Contains(x.UserId!)).ExecuteDeleteAsync();
            await Database.CloseConnectionAsync();
            await Database.GetDbConnection().DisposeAsync();
        }
        await base.DisposeAsync();
    }

    private sealed class WriteCounter : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.Connection?.Database != "Mango_ShoppingCart_Concurrency_Test") throw new InvalidOperationException("Unexpected database target");
            if (eventData.Context is SqlLabTestContext db && (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) || command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase))) db.SaveCalls++;
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.Connection?.Database != "Mango_ShoppingCart_Concurrency_Test") throw new InvalidOperationException("Unexpected database target");
            if (eventData.Context is SqlLabTestContext db && (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) || command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase))) db.SaveCalls++;
            return ValueTask.FromResult(result);
        }
    }
}
