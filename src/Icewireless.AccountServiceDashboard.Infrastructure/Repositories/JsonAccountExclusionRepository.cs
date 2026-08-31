using System.Data;
using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Repositories;

/// <summary>
/// Stores exclusion rules in a local JSON file (no Oracle table).
/// Active patterns are expanded into dashboard SQL at query bind time.
/// </summary>
public sealed class JsonAccountExclusionRepository : IAccountExclusionRepository, IAccountExclusionPatternProvider
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private readonly IOracleConnectionFactory _connections;
    private readonly DashboardSettings _settings;
    private readonly ILogger<JsonAccountExclusionRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonAccountExclusionRepository(
        string filePath,
        IOracleConnectionFactory connections,
        IOptions<DashboardSettings> settings,
        ILogger<JsonAccountExclusionRepository> logger)
    {
        _filePath = filePath;
        _connections = connections;
        _settings = settings.Value;
        _logger = logger;
    }

    public IReadOnlyList<string> GetActiveContainsPatterns()
    {
        _lock.Wait();
        try
        {
            var rules = ReadAllUnsafe();
            return AccountExclusionQueryBuilder.NormalizeContainsPatterns(
                rules.Select(r => (r.Pattern, r.MatchType, r.IsActive)));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PagedResult<AccountExclusionRuleDto>> SearchAsync(AccountExclusionSearchDto search, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var rules = ReadAllUnsafe().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search.Search))
            {
                var q = search.Search.Trim();
                rules = rules.Where(r =>
                    r.Pattern.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (r.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (search.IsActive is not null)
                rules = rules.Where(r => r.IsActive == search.IsActive.Value);

            var ordered = rules
                .OrderBy(r => r.Pattern, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Id)
                .ToList();

            var page = search.Page < 1 ? 1 : search.Page;
            var pageSize = search.PageSize < 1 ? 50 : Math.Min(search.PageSize, 200);
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<AccountExclusionRuleDto>(items, ordered.Count, page, pageSize);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AccountExclusionRuleDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return ReadAllUnsafe().FirstOrDefault(r => r.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AccountExclusionRuleDto> CreateAsync(AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default)
    {
        Validate(rule);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var rules = ReadAllUnsafe();
            var pattern = rule.Pattern.Trim();
            var existing = rules.FirstOrDefault(r =>
                string.Equals(r.Pattern.Trim(), pattern, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            var now = DateTime.UtcNow;
            var created = new AccountExclusionRuleDto(
                rules.Count == 0 ? 1 : rules.Max(r => r.Id) + 1,
                pattern,
                NormalizeMatchType(rule.MatchType),
                rule.IsActive,
                rule.Description?.Trim(),
                string.IsNullOrWhiteSpace(rule.CreatedBy) ? "DASHBOARD" : rule.CreatedBy.Trim(),
                now,
                now);
            rules.Add(created);
            WriteAllUnsafe(rules);
            _logger.LogInformation("Added exclusion rule {Id} pattern={Pattern} to {Path}", created.Id, created.Pattern, _filePath);
            return created;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AccountExclusionRuleDto?> UpdateAsync(long id, AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default)
    {
        Validate(rule);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var rules = ReadAllUnsafe();
            var idx = rules.FindIndex(r => r.Id == id);
            if (idx < 0) return null;
            var existing = rules[idx];
            var updated = existing with
            {
                Pattern = rule.Pattern.Trim(),
                MatchType = NormalizeMatchType(rule.MatchType),
                IsActive = rule.IsActive,
                Description = rule.Description?.Trim(),
                LastUpdated = DateTime.UtcNow
            };
            rules[idx] = updated;
            WriteAllUnsafe(rules);
            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var rules = ReadAllUnsafe();
            var removed = rules.RemoveAll(r => r.Id == id) > 0;
            if (removed) WriteAllUnsafe(rules);
            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AccountExclusionRuleDto?> SetActiveAsync(long id, bool isActive, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var rules = ReadAllUnsafe();
            var idx = rules.FindIndex(r => r.Id == id);
            if (idx < 0) return null;
            var updated = rules[idx] with { IsActive = isActive, LastUpdated = DateTime.UtcNow };
            rules[idx] = updated;
            WriteAllUnsafe(rules);
            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<long> CountExcludedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        if (!_connections.HasConnectionString) return 0;
        var active = GetActiveContainsPatterns();
        if (active.Count == 0) return 0;

        filter = filter.Normalize(_settings.PageSize);
        var sql = AccountExclusionQueryBuilder.BuildExcludedAccountsCountSql(active);
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, sql, _settings);
        OracleCommandFactory.AddProvider(cmd, _settings, filter);
        OracleCommandFactory.BindExclusionPatterns(cmd, active);
        return await OracleCommandFactory.ExecuteCountAsync(cmd, cancellationToken);
    }

    private List<AccountExclusionRuleDto> ReadAllUnsafe()
    {
        EnsureFileExistsUnsafe();
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return System.Text.Json.JsonSerializer.Deserialize<List<AccountExclusionRuleDto>>(json, JsonOptions) ?? [];
    }

    private void WriteAllUnsafe(List<AccountExclusionRuleDto> rules)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = System.Text.Json.JsonSerializer.Serialize(rules, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private void EnsureFileExistsUnsafe()
    {
        if (File.Exists(_filePath))
            return;

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var seed = DefaultRules();
        WriteAllUnsafe(seed);
        _logger.LogInformation("Created exclusion rules file at {Path} with {Count} default patterns", _filePath, seed.Count);
    }

    private static List<AccountExclusionRuleDto> DefaultRules()
    {
        var now = DateTime.UtcNow;
        var patterns = new[] { "TEST", "DEMO", "QA", "TRAINING", "SAMPLE", "DUMMY" };
        return patterns.Select((p, i) => new AccountExclusionRuleDto(
            i + 1,
            p,
            Domain.Enums.ExclusionMatchTypes.Contains,
            true,
            $"Exclude names containing {p}",
            "DASHBOARD",
            now,
            now)).ToList();
    }

    private static void Validate(AccountExclusionRuleWriteDto rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Pattern))
            throw new ArgumentException("Pattern is required.", nameof(rule));
        var matchType = NormalizeMatchType(rule.MatchType);
        if (!Domain.Enums.ExclusionMatchTypes.IsSupported(matchType))
            throw new ArgumentException($"Match type '{matchType}' is not supported yet. Use CONTAINS.", nameof(rule));
    }

    private static string NormalizeMatchType(string? matchType) =>
        string.IsNullOrWhiteSpace(matchType)
            ? Domain.Enums.ExclusionMatchTypes.Contains
            : matchType.Trim().ToUpperInvariant();
}
