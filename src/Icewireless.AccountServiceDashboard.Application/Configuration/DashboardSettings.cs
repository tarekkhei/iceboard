namespace Icewireless.AccountServiceDashboard.Application.Configuration;

public sealed class DashboardSettings
{
    public const string SectionName = "DashboardSettings";

    public string ProviderCode { get; set; } = "ICENP";

    /// <summary>Providers available in the dashboard filter. Queries run for the selected code.</summary>
    public string[] ProviderCodes { get; set; } = ["ICENP", "IRISWV"];

    public int PageSize { get; set; } = 50;
    public int CommandTimeoutSeconds { get; set; } = 120;
    public string ApplicationName { get; set; } = "Wireless Dashboard";

    /// <summary>
    /// Path to the JSON exclusion rules file (relative to content root, or absolute).
    /// </summary>
    public string AccountExclusionRulesPath { get; set; } = "App_Data/account-exclusion-rules.json";

    public IReadOnlyList<string> AllowedProviderCodes
    {
        get
        {
            var codes = ProviderCodes is { Length: > 0 } ? ProviderCodes : ["ICENP", "IRISWV"];
            return codes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public string ResolveProvider(string? requested)
    {
        var allowed = AllowedProviderCodes;
        var fallback = string.IsNullOrWhiteSpace(ProviderCode) ? "ICENP" : ProviderCode.Trim();
        if (allowed.Count == 0)
            return fallback;

        if (string.IsNullOrWhiteSpace(requested))
        {
            return allowed.FirstOrDefault(c => string.Equals(c, fallback, StringComparison.OrdinalIgnoreCase))
                ?? allowed[0];
        }

        return allowed.FirstOrDefault(c => string.Equals(c, requested.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? allowed.FirstOrDefault(c => string.Equals(c, fallback, StringComparison.OrdinalIgnoreCase))
            ?? allowed[0];
    }

    public static string ProviderDisplayName(string? code) =>
        string.Equals(code?.Trim(), "IRISWV", StringComparison.OrdinalIgnoreCase)
            ? "Iristel Wireless"
            : "Ice Wireless";
}

public sealed class Auth0Settings
{
    public const string SectionName = "Auth0";

    /// <summary>Placeholder for future Auth0 Domain. Not enforced in this phase.</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Placeholder for future Auth0 ClientId. Not enforced in this phase.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Placeholder for future Auth0 Audience. Not enforced in this phase.</summary>
    public string Audience { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}
