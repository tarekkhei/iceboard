# Ice Wireless Account Service Dashboard

Clean architecture rebuild of **Icewireless.AccountServiceDashboard**.

> This phase delivers architecture, navigation, DI, configuration, placeholders, and a buildable solution.  
> **Oracle business queries and KPI metrics are intentionally not implemented yet.**

## Architecture

| Project | Responsibility |
|---|---|
| `Domain` | Enums, status code constants, entity models |
| `Application` | Interfaces, DTOs, configuration options, service layer |
| `Infrastructure` | Oracle connection factory, repository stubs |
| `Web` | MVC controllers, views, layout, left navigation |
| `Tests` | Unit and smoke tests |

Patterns:

- Repository pattern (interfaces in Application, implementations in Infrastructure)
- Service layer with constructor DI
- Strongly typed `DashboardSettings` / `Auth0Settings`
- Centralized `StatusMappingService` (never hardcode N/A/S/C/T/O/R in UI)

## Folder structure

```
src/
  Icewireless.AccountServiceDashboard.Domain/
  Icewireless.AccountServiceDashboard.Application/
  Icewireless.AccountServiceDashboard.Infrastructure/
  Icewireless.AccountServiceDashboard.Web/
    Controllers/
    Views/
    ViewModels/
    wwwroot/css|js
tests/
  Icewireless.AccountServiceDashboard.Tests/
```

## Configuration

`appsettings.json`:

```json
"ConnectionStrings": {
  "IceWirelessOracle": ""
},
"DashboardSettings": {
  "ProviderCode": "ICENP",
  "PageSize": 50,
  "CommandTimeoutSeconds": 120
},
"Auth0": {
  "Enabled": false,
  "Domain": "",
  "ClientId": "",
  "Audience": ""
}
```

Local secrets (recommended):

```bash
cd src/Icewireless.AccountServiceDashboard.Web
dotnet user-secrets set "ConnectionStrings:IceWirelessOracle" "User Id=...;Password=...;Data Source=..."
dotnet user-secrets set "DashboardSettings:ProviderCode" "ICENP"
```

## Database

Oracle is the system of record. Planned tables:

- `ACCOUNTS`
- `ACCOUNTSERVICES`
- `ASQUANTITY`
- `ACCTCONTACTS`
- `ACCTSTATUS`

Provider filter default: **ICENP**.

Repositories currently return empty placeholders. Parameterized SQL will be added in a later phase.

## Navigation

Collapsible dark left menu with routes for:

- Dashboard
- Line Details
- Status Event Details
- Analysis
- Reports
- Administration

Each page includes breadcrumb + active nav state.

## Auth0

Authentication is **prepared but not enabled**. See comments in `Program.cs` and `Auth0Settings`.

## Exports

`IExportService` provides CSV/Excel placeholder endpoints under `/reports/export/csv/{reportKey}` and `/reports/export/excel/{reportKey}`.

## Run

```bash
./run.sh
# or
dotnet run --project src/Icewireless.AccountServiceDashboard.Web --urls http://127.0.0.1:5088
```

## Test

```bash
dotnet test
```

## How to add a page

1. Add a controller action + route.
2. Add a menu entry in `Views/Shared/_Sidebar.cshtml`.
3. Return `PageViewModel` / dedicated view model.
4. Add a smoke test URL in `SmokeControllerTests`.

## How to add a query

1. Add method to the appropriate repository interface.
2. Implement parameterized SQL in Infrastructure (bind variables only, `BindByName = true`).
3. Call from a service — never from a Razor view.
4. Add repository/service tests.

## Deployment notes

- Inject `ConnectionStrings__IceWirelessOracle` from the secret store.
- Do not commit credentials.
- Target framework uses the installed SDK (`net10.0` on this machine; MVC architecture matches ASP.NET Core LTS practices).

## Status mapping

| Code | Label |
|---|---|
| N | New |
| A | Active |
| S | Suspended |
| C | Pending Close |
| T | Permanent Closed |
| O | Old |
| R | Archived |
