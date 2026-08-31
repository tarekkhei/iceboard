using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

/// <summary>
/// Retries transient Oracle / network failures (e.g. ORA-12570 connection reset).
/// </summary>
internal static class OracleTransientRetry
{
    private const int MaxAttempts = 3;

    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> action,
        ILogger logger,
        CancellationToken cancellationToken,
        string operationName = "Oracle operation")
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex) && !IsCancellation(ex, cancellationToken) && attempt < MaxAttempts)
            {
                last = ex;
                var delayMs = 400 * attempt * attempt;
                logger.LogWarning(
                    ex,
                    "{Operation} failed with transient Oracle/network error (attempt {Attempt}/{Max}). Retrying in {DelayMs}ms.",
                    operationName, attempt, MaxAttempts, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        throw last ?? new InvalidOperationException($"{operationName} failed after retries.");
    }

    private static bool IsCancellation(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return true;

        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException or TaskCanceledException)
                return true;
            if (current is OracleException ox && ox.Number == 1013)
                return true;
        }

        return false;
    }

    public static bool IsTransient(Exception ex)
    {
        if (ex is OperationCanceledException or TaskCanceledException)
            return false;

        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException or TaskCanceledException)
                return false;

            // ORA-01013 = operation cancelled — do not retry
            if (current is OracleException cancelOx && cancelOx.Number == 1013)
                return false;

            if (current is SocketException)
                return true;

            if (current is OracleException ox)
            {
                // Common transient TNS / disconnect codes
                if (ox.Number is 12570 or 12571 or 12535 or 12514 or 12541 or 3113 or 3114 or 3135 or 1012 or 28 or 4068)
                    return true;

                var msg = ox.Message ?? string.Empty;
                if (msg.Contains("TNS:packet reader failure", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("ORA-03113", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("ORA-03135", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("ORA-12570", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var typeName = current.GetType().FullName ?? string.Empty;
            if (typeName.Contains("NetworkException", StringComparison.Ordinal)
                || typeName.Contains("OracleException", StringComparison.Ordinal))
            {
                var msg = current.Message ?? string.Empty;
                if (msg.Contains("12570", StringComparison.Ordinal)
                    || msg.Contains("packet reader", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Connection reset", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
