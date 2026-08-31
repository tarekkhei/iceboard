# Deploy Ice Wireless Account Service Dashboard on Windows Server

Package: **self-contained win-x64** publish for ASP.NET Core **.NET 10**, version **1.1.2**
(app ships its own runtime; IIS still needs the Hosting Bundle for the ASP.NET Core Module).

See `RELEASE-NOTES.md` and `VERSION.txt` in this package.

## 1. Server prerequisites

1. Windows Server 2019/2022 (or Windows 10/11 for lab).
2. Install the **.NET 10 Hosting Bundle** (required for IIS / ANCM).  
   Download from: https://dotnet.microsoft.com/download/dotnet/10.0  
   After install, reboot or run:
   ```powershell
   net stop was /y
   net start w3svc
   ```
3. Oracle network access from the server to your Oracle database.
4. Open firewall for the site port (example: `5088` or `80`/`443` for IIS).

Verify Hosting Bundle / ANCM:

```powershell
# AspNetCoreModuleV2 should be present under IIS Modules
Get-WebGlobalModule | Where-Object { $_.Name -like "*AspNetCore*" }
```

## 2. Unpack

```powershell
Expand-Archive .\Icewireless.AccountServiceDashboard-win-x64-1.1.2.zip -DestinationPath C:\Apps\IceBoard -Force
cd C:\Apps\IceBoard
```

## 3. Configure Oracle connection

**Upgrade:** restore your previous `appsettings.Production.json` from backup. The zip no longer overwrites it (template is `appsettings.Production.json.example` only).

**New install** — copy the example, then set the connection:

```powershell
Copy-Item .\appsettings.Production.json.example .\appsettings.Production.json
.\set-connection-string.ps1 -ConnectionString "User Id=...;Password=...;Data Source=HOST:1521/SERVICE"
```

Or edit `appsettings.Production.json` directly.

Confirm:

- `ConnectionStrings:IceWirelessOracle` is a real Oracle string (not `YOUR_USER`)
- `DashboardSettings:ProviderCode` = `ICENP` (or `IRISWV` for Iristel Wireless)
- `DashboardSettings:ProviderCodes` includes `ICENP` and `IRISWV`
- `App_Data\account-exclusion-rules.json` is present and writable if you manage exclusions from the UI

If the dashboard shows all zeros, Oracle is not configured. Open **Administration → Database Status**, or run `set-connection-string.ps1`.

Do **not** commit production passwords into source control.

## 4A. Quick smoke test (Kestrel)

```powershell
.\start-kestrel.ps1
```

Browse: `http://SERVER_IP:5088`

Stop with `Ctrl+C`.

## 4B. Production on IIS (recommended)

1. Install IIS roles: Web Server, ASP.NET, WebSocket (optional).
2. Install **.NET 10 Hosting Bundle**, then reboot or run:
   ```powershell
   net stop was /y
   net start w3svc
   ```
3. Create a folder `C:\Apps\IceBoard` and copy publish files there.
4. IIS Manager → Sites → Add Website:
   - Site name: `IceBoard`
   - Physical path: `C:\Apps\IceBoard`
   - Binding: `http` port `80` (or HTTPS with certificate)
5. Application Pool:
   - .NET CLR version: **No Managed Code**
   - Identity: ApplicationPoolIdentity (or a service account with Oracle/network rights)
6. Ensure `logs\` is writable by the app pool identity.
7. Restart the site and browse `http://SERVER_NAME/`

`web.config` is included by publish and configures the ASP.NET Core Module.

### Optional environment variables (IIS)

In the site → Configuration Editor / web.config env vars, or system environment:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__IceWirelessOracle=...` (overrides JSON)

## 4C. Windows Service (Kestrel without IIS)

```powershell
sc.exe create IceBoard binPath= "C:\Program Files\dotnet\dotnet.exe C:\Apps\IceBoard\Icewireless.AccountServiceDashboard.Web.dll" start= auto
sc.exe failure IceBoard reset= 0 actions= restart/5000
# Prefer setting ASPNETCORE_URLS / ENVIRONMENT via service environment or a wrapper script.
```

Or use **NSSM** to wrap `dotnet Icewireless.AccountServiceDashboard.Web.dll` with `ASPNETCORE_URLS=http://0.0.0.0:5088`.

## 5. Post-deploy checklist

- [ ] Header shows **v1.1.2**
- [ ] Administration → Database Status shows Configured **Yes** and Reachable **Yes**
- [ ] Dashboard loads without 500 errors
- [ ] Provider filter switches Ice Wireless / Iristel Wireless
- [ ] Filters return regions/products (Oracle connectivity OK)
- [ ] KPI cards show numbers for a known date range
- [ ] Exclusion admin page / filter gear works if used
- [ ] Logs appear under `logs\dashboard-YYYYMMDD.log`

## 6. Update / rollback

1. Stop IIS site or service.
2. Backup current folder.
3. Extract new zip over the folder.
4. Restore `appsettings.Production.json` if you already customized the connection string. This zip does not include that file (only `appsettings.Production.json.example`).
5. Restore `App_Data\account-exclusion-rules.json` if operators already maintain patterns on the server.
6. Start site/service.

## Troubleshooting

| Symptom | Check |
|---|---|
| 500.30 / ANCM | Hosting Bundle installed + WAS/W3SVC restarted? App pool = **No Managed Code**? 32-bit = **False**? |
| 500.30 with no clue | Open `logs\stdout_*.log` (stdout is enabled in `web.config`). Or run `.\start-kestrel.ps1` |
| Blank / can't connect Oracle | Connection string, firewall, Oracle listener, provider code |
| Permission denied writing logs/App_Data | App pool ACL Modify on site folder, `logs`, and `App_Data` |
| Wrong environment | `ASPNETCORE_ENVIRONMENT=Production` (set in `web.config`) |

### Diagnose 500.30 in 60 seconds

```powershell
cd C:\Apps\IceBoard
New-Item -ItemType Directory -Force logs, App_Data | Out-Null
icacls . /grant "IIS AppPool\IceBoard:(OI)(CI)M"
.\start-kestrel.ps1
```

- If Kestrel starts and `http://localhost:5088` works → IIS / Hosting Bundle / app-pool issue.
- If Kestrel prints an exception → fix that error (config, permissions, missing file).
- If IIS still fails: read `C:\Apps\IceBoard\logs\stdout_*.log`.

