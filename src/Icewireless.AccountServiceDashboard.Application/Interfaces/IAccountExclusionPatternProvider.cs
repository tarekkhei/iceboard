namespace Icewireless.AccountServiceDashboard.Application.Interfaces;

/// <summary>
/// Provides active CONTAINS patterns from the exclusion rules store for SQL expansion.
/// </summary>
public interface IAccountExclusionPatternProvider
{
    IReadOnlyList<string> GetActiveContainsPatterns();
}
