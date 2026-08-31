# Wireless Dashboard — Release Notes

## 1.1.2 — 31 August 2026

Windows Server package: `Icewireless.AccountServiceDashboard-win-x64-1.1.2.zip`

### Fixes

- All KPIs showing **0** with a normal-looking dashboard meant Oracle was not configured. The zip placeholder connection string (`YOUR_USER`) was treated as “no database,” and queries returned zeros instead of an error. The dashboard now shows the configuration error instead of fake zeros.
- The install zip **no longer contains** `appsettings.Production.json`, so extracting a new version will not wipe the server connection string. Template is `appsettings.Production.json.example`.

### Upgrade from 1.1.1 (zeros on every KPI)

You do **not** have to wait for 1.1.2 to get data back. On the server:

```powershell
cd C:\Apps\IceBoard
# Restore the file from before the 1.1.x zip, or:
.\set-connection-string.ps1 -ConnectionString "User Id=...;Password=...;Data Source=HOST:1521/SERVICE"
```

Then recycle the IIS app pool. Confirm **Administration → Database Status** is Configured Yes / Reachable Yes. Current Active Lines should not be 0 if Oracle has active accounts for the selected provider.

Then install 1.1.2 if you want the zip/error fixes. Keep `appsettings.Production.json` when extracting.

---

## 1.1.1 — 31 August 2026

Windows Server package: `Icewireless.AccountServiceDashboard-win-x64-1.1.1.zip`

### Fixes

- Header version badge showed the literal text `v@appVersion` (Razor treated it as an email). It now shows **v1.1.1**.
- After a failed dashboard load, the error page shows the actual exception (Oracle / missing connection string) instead of only a generic message.
- The zip placeholder `YOUR_USER` / `YOUR_PASSWORD` connection string is detected and explained. **On upgrade, restore your previous `appsettings.Production.json`.** Do not keep the placeholder from the zip.
- `set-connection-string.ps1` no longer rewrites the whole JSON file (that could produce invalid JSON on Windows PowerShell).

### Upgrade

1. Stop IIS.
2. Backup `C:\Apps\IceBoard`.
3. Extract this zip.
4. Copy back **`appsettings.Production.json`** from the backup (Oracle connection).
5. Copy back **`App_Data\account-exclusion-rules.json`** if you already maintain patterns on the server.
6. Start IIS. Header should show **v1.1.1**.

---

## 1.1.0 — 31 August 2026

Windows Server package: `Icewireless.AccountServiceDashboard-win-x64-1.1.0.zip`  
Replaces the previous unversioned **1.0.0** self-contained win-x64 zip.

This is a **behavior change** for status-event KPIs. Counts on Suspended, Pending Cancellation, Cancellation Events, Permanent Closed, Net Growth, and Churn will not match 1.0.0 for the same date range.

### KPI definitions

Status-event KPIs now count unique accounts that **entered** that status in the selected From/To range, using `ACCTSTATUS.FROMDATE` as the event date.

| KPI | 1.0.0 | 1.1.0 |
|---|---|---|
| Suspended Events | Accounts whose S status overlapped the period (`FROMDATE`/`TODATE`) | Accounts that entered **S** in the period (`FROMDATE` only) |
| Pending Cancellation | Overlap on **C** | Entered **C** in the period |
| Cancellation Events | Overlap on **T** | Entered **T** in the period |
| Permanent Closed | Same overlap as Cancellation Events | Same FROMDATE entry as Cancellation Events |

`TODATE` is still shown as the status end date. It is not used to include older records.

- **Net Growth** = New Active Lines − Cancellation Events (new definition).
- **Churn Rate** = Cancellation Events (new definition) ÷ Opening Active Lines.

New Active Lines is unchanged (`ACCOUNTS.REGISTRATIONDATE`). Current Active Lines is unchanged (snapshot: `OWNSTATUS = A` with an activation date).

### Dashboard UI

- KPI chart popup is a **weekly line trend**, not a histogram.
- Query-field footnotes on KPI cards are simplified; the live SQL is still available from the SQL icon.
- Chart icon is hidden on **Current Active Lines** (snapshot, not a weekly event).
- Suspended Events and Churn Rate charts are weekly, matching the other event KPIs.
- Pie charts (region, product, cancellation reasons, status) show an HTML legend under the canvas.
- **Save PNG** and **Save PDF** capture the dashboard as currently filtered.

### Tables, search, and exports

- Product **catalog description** (`PRODUCTS`) is shown with the product code in detail tables, related-lines popup, search, and CSV/Excel exports.
- Cancellation **reason** uses the English label from `STATREASON` → `LANG_TRANSLATIONS` (`ISO3CODE = 'eng'`), then `STATREASON.DESCRIPTION`, then the numeric code.
- Cancellation **subreason** column removed from status-event tables.
- Related-lines popup for Pending Cancellation, Cancellation Events, and Permanent Closed no longer fails with “Unable to load lines for this KPI.”

### Filters and administration

- **Provider** filter: Ice Wireless (`ICENP`) or Iristel Wireless (`IRISWV`). Region/product/service lists follow the selected provider.
- Test/demo exclusion patterns can be added and deleted from the **gear** next to Include Test / Demo Accounts. Rules remain in `App_Data\account-exclusion-rules.json` (not Oracle). Duplicate add of the same pattern is not stored twice.

### Upgrade on Windows Server

1. Stop the IIS site or Windows service.
2. Backup `C:\Apps\IceBoard` (or your install folder).
3. Extract this zip over the folder.
4. **Restore** your existing `appsettings.Production.json` (connection string).
5. **Restore** `App_Data\account-exclusion-rules.json` if operators already maintain patterns on the server. Do not replace that file with the copy from the zip unless you intend to reset rules.
6. Start the site/service.
7. Confirm the header shows **v1.1.0**.

Expect lower Suspended / Pending / Cancellation / Permanent Closed numbers than 1.0.0 for the same dates — that is the FROMDATE change, not missing data.

PNG/PDF snapshot uses Chart.js plus html2canvas/jsPDF from jsDelivr. Those buttons need outbound HTTPS from the browser (or a CDN-reachable workstation). Core dashboard pages do not.

Full install steps: `DEPLOY-WINDOWS.md` in this package.
