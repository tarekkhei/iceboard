namespace Icewireless.AccountServiceDashboard.Domain.Enums;

public static class ExclusionMatchTypes
{
    public const string Contains = "CONTAINS";
    public const string StartsWith = "STARTS_WITH";
    public const string EndsWith = "ENDS_WITH";
    public const string Exact = "EXACT";
    public const string Regex = "REGEX";

    public static readonly IReadOnlyList<string> Supported = [Contains];

    public static bool IsSupported(string? matchType) =>
        Supported.Any(s => string.Equals(s, matchType?.Trim(), StringComparison.OrdinalIgnoreCase));
}
