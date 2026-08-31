namespace Icewireless.AccountServiceDashboard.Application.DTOs;

public sealed record StatusMappingDto(string Code, string Label, string EnumName);

public sealed record DatabaseStatusDto(
    bool Configured,
    bool Reachable,
    string Message,
    DateTimeOffset CheckedAtUtc);

public sealed record PagePlaceholderDto(
    string Title,
    string Section,
    string Description,
    bool QueriesImplemented = false);

public sealed record KpiQueryDto(
    string Key,
    string Title,
    string Sql,
    string Notes,
    IReadOnlyList<KpiQueryParameterDto>? Parameters = null);

public sealed record KpiQueryParameterDto(string Name, string Value);
