using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;

namespace Icewireless.AccountServiceDashboard.Application.Services;

public sealed class AccountExclusionService : IAccountExclusionService
{
    private readonly IAccountExclusionRepository _repository;
    private readonly IAccountExclusionPatternProvider _patterns;

    public AccountExclusionService(
        IAccountExclusionRepository repository,
        IAccountExclusionPatternProvider patterns)
    {
        _repository = repository;
        _patterns = patterns;
    }

    public Task<PagedResult<AccountExclusionRuleDto>> SearchAsync(AccountExclusionSearchDto search, CancellationToken cancellationToken = default) =>
        _repository.SearchAsync(search, cancellationToken);

    public Task<AccountExclusionRuleDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<AccountExclusionRuleDto> CreateAsync(AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(rule, cancellationToken);

    public Task<AccountExclusionRuleDto?> UpdateAsync(long id, AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(id, rule, cancellationToken);

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    public Task<AccountExclusionRuleDto?> EnableAsync(long id, CancellationToken cancellationToken = default) =>
        _repository.SetActiveAsync(id, true, cancellationToken);

    public Task<AccountExclusionRuleDto?> DisableAsync(long id, CancellationToken cancellationToken = default) =>
        _repository.SetActiveAsync(id, false, cancellationToken);

    public Task<long> CountExcludedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        _repository.CountExcludedAccountsAsync(filter, cancellationToken);

    public IReadOnlyList<string> GetActiveContainsPatterns() => _patterns.GetActiveContainsPatterns();

    public string GetExclusionFilterPreview(bool includeTestAccounts)
    {
        var patterns = GetActiveContainsPatterns();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-- Test / Demo account exclusion filter");
        sb.AppendLine("-- Fields: ACCOUNTS.ACCOUNTNAME, ACCOUNTS.CODE, ACCTCONTACTS first/middle/last name");
        sb.AppendLine("-- Match type: CONTAINS (from App_Data/account-exclusion-rules.json)");
        sb.AppendLine();

        if (includeTestAccounts)
        {
            sb.AppendLine("-- Status: BYPASSED");
            sb.AppendLine("-- Include Test / Demo Accounts is checked.");
            sb.AppendLine("-- Exclusion rules are NOT applied to KPIs, charts, tables, or exports.");
            if (patterns.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("-- Active rules (ignored while bypassed):");
                foreach (var p in patterns)
                    sb.AppendLine($"--   CONTAINS '{p.Replace("'", "''", StringComparison.Ordinal)}'");
            }

            return sb.ToString().TrimEnd();
        }

        if (patterns.Count == 0)
        {
            sb.AppendLine("-- Status: NO ACTIVE RULES");
            sb.AppendLine("-- No accounts are excluded.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine("-- Status: APPLIED (Include Test / Demo Accounts is unchecked)");
        sb.AppendLine("-- Active patterns:");
        foreach (var p in patterns)
            sb.AppendLine($"--   CONTAINS '{p.Replace("'", "''", StringComparison.Ordinal)}'");
        sb.AppendLine();
        sb.AppendLine("AND (");
        sb.AppendLine("      :p_include_test_accounts = 1");
        sb.AppendLine("      OR NOT (");
        for (var i = 0; i < patterns.Count; i++)
        {
            var lit = patterns[i].Replace("'", "''", StringComparison.Ordinal);
            if (i > 0) sb.AppendLine("           OR");
            sb.AppendLine($"          UPPER(NVL(a.accountname, CHR(0))) LIKE '%' || UPPER('{lit}') || '%'");
        }

        sb.AppendLine("      )");
        sb.Append(")");
        return sb.ToString();
    }
}
