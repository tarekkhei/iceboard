Cursor Implementation Prompt — Icewireless.AccountServiceDashboard

You are a senior .NET solution architect, data engineer, and UX engineer. Build a production-quality ASP.NET Core dashboard named:

Icewireless.AccountServiceDashboard

The application must retrieve Ice Wireless account and service data directly from the existing Oracle database through read-only, parameterized queries, normalize the results, calculate operational KPIs, and display a modern Power BI-style dashboard.

Do not build a static mock-up. Implement a working end-to-end solution with clean architecture, testable services, responsive pages, charts, filtering, validation, logging, and sample data.

1. Technology stack

.NET 8 or the latest stable LTS available in the existing repository.

ASP.NET Core MVC or Razor Pages. If the repository already uses Blazor, preserve it.

C#, nullable reference types, async/await, dependency injection, and strongly typed models.

Entity Framework Core with SQLite for Phase 1 local persistence; isolate persistence behind interfaces so SQL Server can replace it later.

Oracle.ManagedDataAccess.Core for Oracle connectivity.

Bootstrap 5 plus custom CSS for a polished enterprise interface.

Chart.js for interactive charts.

xUnit for unit and integration tests.

Serilog for structured logging.

Swagger/OpenAPI for ingestion and dashboard APIs.

First inspect the existing repository and reuse its conventions. Do not replace working code or introduce a second competing application structure.

2. Phase 1 scope

Implement:

Secure Oracle database connectivity.

Read-only, parameterized queries for dashboard summaries, trends, breakdowns, and drill-down records.

A configurable Oracle-to-domain mapping layer.

Optional local caching of normalized dashboard data and synchronization metadata.

A weekly executive dashboard driven by Oracle data.

Drill-down tables and export of filtered results.

Data-quality monitoring for nulls, invalid status codes, duplicate service identifiers, and inconsistent dates.

Do not add Excel or CSV upload functionality. Oracle is the system of record.

3. Oracle data-access workflow

Create a secure Oracle integration using Oracle.ManagedDataAccess.Core.

Configuration:

Read the connection string from configuration using ConnectionStrings:IceWirelessOracle.

In development, support .NET User Secrets.

In deployed environments, use the organization’s approved secret store or injected environment configuration.

Never commit credentials, usernames, passwords, wallets, certificates, or production connection strings.

Use a dedicated read-only Oracle account with access limited to the required views/tables.

Support Oracle wallet/TLS configuration if required by the environment.

Validate connectivity through an authenticated admin-only health check without exposing credentials or sensitive database details.

Query requirements:

Use parameterized SQL only. Never concatenate filter values into SQL.

Use bind variables for date range, region, product, channel/dealer, status, account, and pagination.

Set command timeouts and pass cancellation tokens.

Open connections late and dispose them immediately after use.

Select only required columns; never use SELECT *.

Keep SQL in focused query classes or embedded .sql resources, not controllers or Razor views.

Use explicit aliases that map Oracle columns to application DTOs.

Use Oracle-compatible pagination and date handling.

Keep all queries read-only. The dashboard must not insert, update, delete, or execute stored procedures that mutate production data.

Performance:

Push filtering, grouping, distinct counts, and pagination to Oracle.

Avoid loading the full service population into application memory.

Review execution plans for high-volume queries.

Document recommended indexes or materialized views, but do not create or change production Oracle objects automatically.

Use short-lived caching for expensive aggregates where appropriate.

Include the selected filters and the latest source-data timestamp in cache keys.

Prevent cache stampedes and define a reasonable expiration policy.

Data synchronization:

Prefer live Oracle queries for current dashboard data.

If local caching is needed for performance, implement a background synchronization service with watermarks and idempotent upserts.

Record last successful refresh, source watermark, duration, rows processed, and failure details.

Never present stale cached data as current; display the last successful refresh time and a stale-data warning.

A failed refresh must retain the last valid snapshot.

Normalization and data quality:

Trim source strings and normalize dates to a consistent internal type.

Preserve the original Oracle status code.

Treat nullable or malformed optional fields safely.

Capture data-quality warnings without logging sensitive customer values.

Unknown status codes must remain visible and filterable.

Duplicate rows must not inflate distinct service/line counts.

4. Canonical data model

Create models such as:

OracleRefreshRun

OracleSourceRecord

AccountServiceRecord

OracleColumnMapping

DashboardFilter

DashboardSummary

WeeklyMetric

The normalized AccountServiceRecord should support these fields when present:

Source table/view and stable source record identifier when available.

Report date or reporting week start/end.

Account ID/code and account name.

Customer ID.

Service/line ID, MSISDN, SIM/ICCID, and product/plan.

Region, province, city, market, channel, dealer, or agent.

Activation date.

Cancellation/closure date.

Current status code and normalized status.

Suspension date.

Revenue or recurring charge when present.

Source system timestamp or last-modified timestamp when available.

Fields unavailable from the Oracle source must remain nullable; querying or mapping must not fail simply because an optional field is absent.

5. Status mapping

Apply this exact business mapping:

Code

Status

N

New

A

Active

S

Suspended

C

Pending to close

T

Permanently closed

O

Old

R

Archived

Requirements:

Status matching is case-insensitive and trimmed.

Preserve the original code.

Display a friendly label and colored badge.

Unknown or blank codes must be classified as Unknown, surfaced in data-quality warnings, and remain filterable.

Do not automatically count N as an activation unless an activation date or a documented source rule confirms it.

Do not automatically count C as a completed cancellation; it is pending closure.

By default, count T as closed/cancelled only when the report semantics support that interpretation.

Centralize these rules in a testable StatusMappingService, not in controllers or views.

Suggested badge treatment:

New: blue.

Active: green.

Suspended: amber.

Pending to close: orange.

Permanently closed: red.

Old: gray.

Archived: slate.

Unknown: purple.

6. KPI definitions

Build a calculation service with explicit, documented rules. Where the source report provides event dates, calculate metrics from event dates rather than current status alone.

Total Activations: distinct service/line IDs activated inside the selected period.

Total Cancellations: distinct service/line IDs actually cancelled or permanently closed inside the selected period.

Net Growth = Total Activations - Total Cancellations.

Total Active Lines: distinct service/line IDs active at the end of the selected period.

Churn Rate = Total Cancellations / Active Lines at Beginning of Period × 100.

If beginning active lines cannot be reliably derived, show N/A with an explanatory tooltip instead of inventing a denominator.

Compare each KPI against the immediately preceding equivalent period.

Prevent divide-by-zero errors.

Use distinct stable line/service identifiers to prevent duplicate row inflation.

Place formulas and assumptions in a visible Metric Definitions panel and in code comments/tests.

7. Dashboard UX

Title: Ice Wireless Weekly Dashboard

Create a modern, spacious, executive-grade layout—not a generic admin template. Use Ice Wireless branding if brand assets already exist; otherwise use a restrained palette with deep navy, ice blue, white, green for positive results, red for negative results, and accessible neutral grays.

Top bar

Dashboard title.

Last successful Oracle refresh date.

Refresh status and an admin-only Refresh Data action if caching is enabled.

Filters: date range/week, region, product/plan, channel/dealer, and status.

Reset filters button.

Filters must update every KPI, chart, and table consistently.

KPI cards

Display horizontally on desktop and responsively on smaller screens:

Total Activations — large green value and percentage change versus previous period.

Total Cancellations — red value and directional trend.

Net Growth — green when positive, red when negative, neutral when zero.

Churn Rate — percentage and up/down trend; lower churn should be visually treated as favorable.

Total Active Lines — neutral primary summary with comparison.

Each card needs an icon, value, previous-period comparison, and tooltip explaining the calculation.

Weekly Trends

Chart 1: Activations vs Cancellations

Interactive line chart.

X-axis: week/date.

Y-axis: distinct line count.

Activations: green.

Cancellations: red.

Tooltip with exact values.

Chart 2: Net Growth Over Time

Area or column chart.

X-axis: week/date.

Y-axis: net gain/loss.

Positive values green; negative values red.

Operational breakdown

Add:

Active Lines by Product/Plan — horizontal bar chart.

Activations by Region — bar chart or map only if reliable geographical data exists.

Service Status Distribution — doughnut chart using the status badge colors.

Churn by Product/Plan — ranked chart when the denominator is available.

Top Dealers/Channels by Net Growth — ranked table or bar chart.

Avoid chart clutter. Show No data available states instead of empty chart frames.

Detail table

Create a searchable, sortable, paginated table with:

Service/line identifier.

Account/customer.

Product/plan.

Region.

Activation date.

Closure date.

Status badge.

Source sheet and row.

Add row drill-down to show normalized fields, source identifiers, and validation warnings. Allow export of the currently filtered dataset. If CSV export is retained, it is an output-only feature and must not be described or implemented as a data-ingestion method.

8. Architecture

Use a clean separation:

Domain: entities, enums, value objects, business rules.

Application: interfaces, query orchestration, mapping, KPI calculations, DTOs, validation.

Infrastructure: Oracle data access, optional EF Core cache, synchronization, and logging.

Web: controllers/pages, APIs, view models, JavaScript, charts, UI.

Tests: unit and integration tests.

If splitting into projects would conflict with the existing repository, use folders with equivalent boundaries.

Suggested services:

IOracleConnectionFactory

IOracleDashboardRepository

IOracleQueryService

IOracleDataMappingService

IDashboardRefreshService

IStatusMappingService

IDashboardMetricService

IReportRepository

IExportService

Controllers/pages must remain thin. Business calculations must not live in JavaScript.

9. APIs

Provide endpoints similar to:

GET /api/dashboard/refresh-status

POST /api/dashboard/refresh — admin-only and only when local caching is enabled

GET /api/dashboard/summary

GET /api/dashboard/trends

GET /api/dashboard/breakdowns

GET /api/dashboard/records

GET /api/dashboard/export

Use a consistent error format based on ProblemDetails. Validate query parameters and support cancellation tokens.

10. Oracle query and mapping configuration

Before writing final SQL, inspect the existing repository for Oracle entities, queries, schemas, views, synonyms, and naming conventions. Do not invent production table or column names.

Create a documented mapping from confirmed Oracle columns to the canonical domain fields:

Stable service/line identifier.

Account and customer identifiers.

MSISDN, SIM/ICCID, product, and plan.

Region, province, market, channel, dealer, or agent.

Activation, suspension, cancellation, and last-modified dates.

Current status code.

Recurring charge or revenue when available.

If the schema or required query is not available in the repository:

Create the repository interfaces, DTOs, and query placeholders.

Put all unresolved identifiers in one clearly marked configuration/query file.

Add TODO: Confirm Oracle schema comments.

Do not fabricate table names, joins, business meanings, or credentials.

Document the exact schema information required to complete each query.

Centralize mapping in the data-access layer and use explicit DTO projections. Fail fast at startup for missing mandatory configuration, but keep optional source fields nullable.

11. Security and reliability

Enforce least-privilege, read-only Oracle access.

Protect credentials through secrets management and prevent them from appearing in logs or error responses.

Use parameterized SQL and allowlisted sort columns.

Apply query timeouts, cancellation, bounded page sizes, and rate limiting where appropriate.

If CSV export is supported, protect exported values from spreadsheet formula injection.

Require authorization if authentication already exists.

Log refreshes and failures without logging sensitive full row contents.

If caching is enabled, use transactions so a failed refresh cannot replace the last valid dataset.

Add database indexes for report date, status, product, region, and service/line ID.

12. Testing

Create tests for:

Every status mapping, including lowercase, whitespace, blank, and unknown codes.

KPI formulas.

Previous-period comparisons.

Zero denominator and missing denominator for churn.

Duplicate line records.

Oracle column-to-DTO mapping.

Parameter binding and SQL-injection resistance.

Connection, timeout, cancellation, and transient database failures.

Null values, invalid dates, and unexpected status codes.

Duplicate source rows.

Refresh watermark and idempotent cache synchronization.

Preservation of the last valid snapshot after refresh failure.

CSV formula-injection protection.

Dashboard API filtering.

Create a fake/in-memory repository or a safe test fixture covering all status codes and multiple weeks. Unit tests must not require production Oracle access and must not contain real customer information or credentials.

13. Accessibility and responsiveness

Meet WCAG 2.1 AA basics.

Do not rely on red/green alone; include arrows, labels, or icons.

Keyboard-accessible filters and tables.

Proper labels and ARIA text for controls and chart summaries.

Responsive layouts for desktop, tablet, and mobile.

14. Deliverables

Implement the solution, not merely a design document. Deliver:

Compiling source code.

EF Core migrations and seed/sample data.

Functional Oracle query and dashboard workflows.

Unit and integration tests.

README.md with prerequisites, Oracle configuration, setup, migration, run, test, refresh, mapping, and KPI-definition instructions.

A short IMPLEMENTATION_NOTES.md listing assumptions, source-column mappings, known data limitations, and Phase 2 recommendations.

15. Execution sequence

Work in this order:

Inspect the repository and summarize its current structure, including any existing Oracle access.

Identify assumptions, schema gaps, or conflicts.

Create the domain and Oracle data-access foundation.

Add persistence and migrations.

Implement mapping and KPI services with tests.

Implement APIs.

Build the dashboard UI and charts.

Add refresh monitoring, data-quality review, drill-down, and export.

Run formatter, build, migrations, and tests.

Fix all errors and provide a concise completion report with changed files, commands used, test results, and remaining assumptions.

Do not stop after scaffolding. Continue until the application builds and the main Phase 1 workflow is functional. If production Oracle schema details or connectivity are unavailable, complete and test the implementation against the repository abstraction/fake provider, clearly identify the unresolved SQL mappings, and do not claim that live Oracle connectivity was verified.