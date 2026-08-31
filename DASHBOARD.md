# Wireless Dashboard — What Was Built

ASP.NET Core MVC dashboard over the Ice Wireless / Iristel Oracle account system. Operators filter by provider and date, review KPIs and charts, drill into line and status-event tables, and export the same filtered data.

Local URL after `./run.sh`: **http://127.0.0.1:5088**

---

## Stack

| Layer | Project | Role |
|---|---|---|
| Web | `Icewireless.AccountServiceDashboard.Web` | MVC, Razor, Chart.js, CSS/JS |
| Application | `Icewireless.AccountServiceDashboard.Application` | Services, DTOs, filter model |
| Infrastructure | `Icewireless.AccountServiceDashboard.Infrastructure` | Oracle SQL, repositories, JSON exclusion store |
| Domain | `Icewireless.AccountServiceDashboard.Domain` | Status codes, enums, entities |
| Tests | `Icewireless.AccountServiceDashboard.Tests` | Unit and route smoke tests |

Oracle is read-only. Queries are parameterized. Schema owner is **MINDPROD**. Product code **ICENP_MASTER** is always excluded.

Auth0 is configured as a placeholder only (`Auth0:Enabled` is false). There is no login screen.

---

## Home dashboard (`/` and `/dashboard`)

### KPI cards

Each card shows the current-period value, a previous-period comparison, and (where it applies) a sparkline. Cards open:

- **SQL** — the live Oracle used for that KPI, with current bind values
- **Chart** — larger trend for that metric
- **Related lines** — paged rows for that KPI (`/api/dashboard/kpi-lines/{key}`)

| KPI | Definition |
|---|---|
| **New Active Lines** | Distinct accounts whose `ACCOUNTS.REGISTRATIONDATE` falls in the selected range. Does **not** use `ACTIVATIONDATE` / `FIRSTACTIVATIONDATE`. |
| **Current Active Lines** | Distinct accounts with `OWNSTATUS = A` and an activation date. Snapshot; **not** limited by the date range. |
| **Suspended Events** | Distinct accounts that entered status **S** in the period (`ACCTSTATUS.FROMDATE`). |
| **Pending Cancellation** | Distinct accounts that entered **C** (Pending to Close) in the period. |
| **Cancellation Events** | Distinct accounts that entered **T** (Permanently Closed) in the period. |
| **Permanent Closed** | Same definition as Cancellation Events. |
| **Net Growth** | New Active Lines − Cancellation Events. |
| **Churn Rate** | Cancellation Events ÷ Opening Active Lines × 100. |

`TODATE` on status rows is the end of that status only. Event timing is always `FROMDATE`.

### Charts (two rows)

**Line charts**

- Activations Trend
- Net Growth
- Completed Cancellations

**Pie charts** (top slices + Other; HTML legend under the canvas)

- Region Analysis — new active lines by state
- Product Analysis — new active lines by product
- Cancellation Reasons — English label from `STATREASON` → `LANG_TRANSLATIONS` (`ISO3CODE = 'eng'`), then `STATREASON.DESCRIPTION`, then the numeric code
- Status Distribution — status **events** in the period (`ACCTSTATUS.FROMDATE`), not the current `OWNSTATUS` snapshot

### Snapshot export

**Save PNG** and **Save PDF** capture the dashboard as shown (filters, KPIs, charts).

---

## Global filters

Shown on the dashboard and on detail / analysis / report pages. Query string is preserved across navigation and exports.

| Filter | Notes |
|---|---|
| **Provider** | **Ice Wireless** (`ICENP`) or **Iristel Wireless** (`IRISWV`). Every account query uses `a.providercode = :p_provider`. Region / product / service-type lists are for the selected provider. |
| **From / To date** | Default: last 28 days through today (UTC date). |
| **Region** | `ACCTCONTACTS.STATE` |
| **Product** | Product code (catalog description shown where available) |
| **Service type** | `ACCOUNTSERVICES.SERVICETYPE` |
| **Status** | Canonical codes via status mapping (not hardcoded in the UI) |
| **Search** | Account code, account name, service / product text |
| **Include Test / Demo Accounts** | When unchecked (default), exclusion patterns apply |

Header badge shows the selected provider display name.

---

## Test / demo account exclusions

Stored in `src/Icewireless.AccountServiceDashboard.Web/App_Data/account-exclusion-rules.json` (not an Oracle table).

Default patterns include TEST, DEMO, QA, TRAINING, SAMPLE, DUMMY. Operators add person names and other fragments.

**Gear** next to Include Test / Demo Accounts opens a popup to **add** and **delete** patterns. Closing after a change reloads the page so KPIs pick up the new rules. The same rules can be managed under Administration → Account Exclusion Rules.

A pattern is a case-insensitive **contains** match against:

- `ACCOUNTS.ACCOUNTNAME`
- `ACCOUNTS.CODE`
- Contact full name (`ACCTCONTACTS` first / middle / last)

Duplicate patterns are not stored twice. Adding the same text again returns the existing rule.

---

## Line Details

| Page | Route | Content |
|---|---|---|
| New Active Lines | `/line-details/new-active` | Accounts registered in the date range |
| Current Active Lines | `/line-details/current-active` | Currently active accounts |

Paged tables, sort, CSV / Excel download. Same global filters.

---

## Status Event Details

All event pages count **unique accounts** that entered that status in the date range (`ACCTSTATUS.FROMDATE`).

| Page | Route | Status |
|---|---|---|
| Active Status Events | `/status-events/active` | A |
| Suspended Events | `/status-events/suspended` | S |
| Pending Cancellation | `/status-events/pending-cancellation` | C |
| Cancellation Events | `/status-events/cancellation` | T |
| Permanent Closed | `/status-events/permanently-closed` | T |
| Old Status Events | `/status-events/old` | O |
| Archived Status Events | `/status-events/archived` | R |

CSV / Excel export per event type. Cancellation reason on those rows uses the English translation lookup described above.

---

## Analysis

| Page | Route | View |
|---|---|---|
| Region Analysis | `/analysis/region` | Pie + table of new active by region |
| Product Analysis | `/analysis/product` (also `/analysis/service`) | Pie + table by product |
| Cancellation by Reason | `/analysis/cancellation-reason` | Pie + table of completed cancellations by reason |
| Activation Trends | `/analysis/activation-trends` | Line |
| Cancellation Trends | `/analysis/cancellation-trends` | Line |
| Net Growth | `/analysis/net-growth` | Line |
| Churn Analysis | `/analysis/churn` | Line (cancellations vs opening active) |

---

## Reports

| Page | Route |
|---|---|
| Account Details | `/reports/account-details` |
| Service Details | `/reports/service-details` |
| Status History | `/reports/status-history` |
| Export Data | `/reports/export` |

Export Data lists CSV and Excel links for the main report keys (new active, current active, status history, suspended, etc.). Filters on that page apply to the downloads. CSV is UTF-8. The Excel action currently serves CSV with an `.xlsx` filename so spreadsheet apps can open it.

---

## Administration

| Page | Route | Notes |
|---|---|---|
| Database Status | `/admin/database-status` | Connection / Oracle health |
| Query Diagnostics | `/admin/query-diagnostics` | Placeholder |
| Status Mapping | `/admin/status-mapping` | Canonical N / A / S / C / T / O / R labels |
| Account Exclusion Rules | `/admin/account-exclusions` | Full add / enable / disable / delete of JSON rules |
| Settings | `/admin/settings` | Shows configured page size, timeout, provider; Auth0 not enabled |

REST API for exclusions (used by the filter popup):

- `GET /api/admin/account-exclusions`
- `POST /api/admin/account-exclusions`
- `DELETE /api/admin/account-exclusions/{id}`
- `PATCH .../enable` and `.../disable`

---

## Status codes

UI never hardcodes status letters. Mapping is centralized (`StatusCodes` / `StatusMappingService`):

| Code | Meaning |
|---|---|
| N | New |
| A | Active |
| S | Suspended |
| C | Pending to Close |
| T | Permanently Closed |
| O | Old |
| R | Archived |

---

## Oracle tables used

- `ACCOUNTS` — registration, activation, current status, name, code, provider
- `ACCOUNTSERVICES` — product, service type, own status
- `ACCTCONTACTS` — region (state), customer name
- `ACCTSTATUS` — status events (`FROMDATE` / `TODATE`, reason)
- `STATREASON` + `LANG_TRANSLATIONS` — cancellation reason labels
- `PRODUCTS` — product description for display

---

## How to run

Oracle connection is typically in user secrets, not in `appsettings.json`.

```bash
cd src/Icewireless.AccountServiceDashboard.Web
dotnet user-secrets set "ConnectionStrings:IceWirelessOracle" "User Id=...;Password=...;Data Source=..."
```

From the repo root:

```bash
./run.sh
```

That restores, builds, and serves **http://127.0.0.1:5088**. Razor views are compiled into the Web DLL — layout and `.cshtml` changes need a rebuild/restart. Static files under `wwwroot` (`app.js`, `app.css`) pick up on refresh (`asp-append-version`).

---

## Configuration (`DashboardSettings`)

| Setting | Default |
|---|---|
| `ProviderCode` | `ICENP` |
| `ProviderCodes` | `ICENP`, `IRISWV` |
| `PageSize` | 50 |
| `CommandTimeoutSeconds` | 120 |
| `ApplicationName` | Wireless Dashboard |
| `AccountExclusionRulesPath` | `App_Data/account-exclusion-rules.json` |

---

## Not in this build

- Auth0 login / session enforcement
- True `.xlsx` binary Excel (CSV content with an Excel filename)
- Write-back to Oracle (dashboard is read-only)
