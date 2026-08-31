using System.Data.Common;
using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

public sealed class OracleConnectionFactory : IOracleConnectionFactory
{
    private readonly string? _connectionString;
    private readonly DashboardSettings _settings;
    private readonly ILogger<OracleConnectionFactory> _logger;

    public OracleConnectionFactory(
        IConfiguration configuration,
        IOptions<DashboardSettings> settings,
        ILogger<OracleConnectionFactory> logger)
    {
        _connectionString = NormalizeConnectionString(configuration.GetConnectionString("IceWirelessOracle"));
        _settings = settings.Value;
        _logger = logger;
    }

    public bool HasConnectionString => IsConfigured(_connectionString);

    public void EnsureConfigured()
    {
        if (!HasConnectionString)
        {
            throw new InvalidOperationException(
                "Oracle is not configured. ConnectionStrings:IceWirelessOracle is empty or still the zip placeholder (YOUR_USER / YOUR_PASSWORD). "
                + "On upgrade, restore the previous appsettings.Production.json, or run set-connection-string.ps1. "
                + "Do not keep the template from the zip — extracting the zip overwrites that file.");
        }
    }

    public Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default) =>
        OracleTransientRetry.RunAsync(OpenOnceAsync, _logger, cancellationToken, "Oracle Open");

    private async Task<DbConnection> OpenOnceAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured(_connectionString))
        {
            EnsureConfigured();
        }

        var connection = new OracleConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        if (!HasConnectionString)
        {
            return false;
        }

        try
        {
            await using var connection = await CreateOpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM DUAL";
            command.CommandTimeout = _settings.CommandTimeoutSeconds;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Oracle connectivity check failed");
            return false;
        }
    }

    private static bool IsConfigured(string? connectionString) =>
        !string.IsNullOrWhiteSpace(connectionString)
        && !connectionString.Contains("YOUR_USER", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("YOUR_PASSWORD", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("YOUR_ORACLE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Adds defensive connection options when missing (does not override existing keys).
    /// </summary>
    private static string? NormalizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var cs = connectionString.Trim().TrimEnd(';');
        if (!ContainsKey(cs, "Connection Timeout") && !ContainsKey(cs, "Connect Timeout"))
            cs += ";Connection Timeout=60";
        if (!ContainsKey(cs, "Validate Connection"))
            cs += ";Validate Connection=true";
        return cs;
    }

    private static bool ContainsKey(string connectionString, string key) =>
        connectionString.Contains(key + "=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains(key + " =", StringComparison.OrdinalIgnoreCase);
}
