# PoultryPro — Precision Poultry Farm Management & Research Platform

ASP.NET Core 8 MVC · Dapper · SQL Server · Bootstrap 5 · Chart.js · Leaflet

PoultryPro started as a farm / field-officer management system. This version keeps every original module
(Dashboard, Farms, Bird Management, Batches, Daily Records, Field Visits with GPS, photos and farmer
signature, Field Officers, Health, Inventory, Finance, Market, Employees, Reports) and extends it into a
complete platform with analytics, IoT, traceability and research tooling. It has 125+ pages, 42 tables,
12 SQL views and 2 stored procedures.

---

## 1. Run it (about 2 minutes)

**You need:** Visual Studio 2022 (or newer) with the *ASP.NET and web development* workload, the .NET 8 SDK,
and a local SQL Server (Developer, Express or LocalDB). SSMS is optional.

1. Unzip the folder and open **`PoultryPro.sln`** in Visual Studio.
2. Press **F5** (or Ctrl+F5). Visual Studio restores the NuGet packages (Dapper, Microsoft.Data.SqlClient,
   Razor runtime compilation) and opens the browser.
3. On first start the app **creates the `PoultryProDB` database by itself**. It creates all tables, views and
   procedures, loads the reference data and fills in about 90 days of realistic demo data. This takes
   10–30 seconds the first time.
4. Sign in with **`admin` / `Admin@123`**.

### If the database is not created automatically
The app tries these SQL Server instances in order: `localhost`, `.\SQLEXPRESS`, `(localdb)\MSSQLLocalDB` and `.`.
If none of them can be reached, you'll see a **Database setup** page that shows exactly what failed, with a *Retry* button.

- Open `appsettings.json` and set `ConnectionStrings:Default` to your server. The server name is the one you
  type in the SSMS *Connect* box. Examples:
  - Windows login: `Server=YOUR-PC\\SQLEXPRESS;Database=PoultryProDB;Trusted_Connection=True;TrustServerCertificate=True;`
  - SQL login: `Server=localhost;Database=PoultryProDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;`
- To build the database manually instead, run the scripts in SSMS in this order:
  `SQL/00_CreateDatabase.sql` → `01_Schema.sql` → `02_Views.sql` → `03_ReferenceData.sql` → `04_DemoData.sql`.
  All scripts can be re-run safely. `04_DemoData.sql` only inserts data when the Farms table is empty.
- Set `"SeedDemoData": false` in `appsettings.json` for an empty production database (the reference data and
  login accounts are still created).

> **Existing database from the old version:** `01_Schema.sql` upgrades a database created by the original
> scripts. It adds the missing columns and tables and does not drop data. The original scripts are kept in `SQL/legacy/`.

### Demo accounts

| Username | Password | Role | Can do |
|---|---|---|---|
| `admin` | `Admin@123` | Admin | Everything, including users, settings and the audit log |
| `manager` | `Manager@123` | Manager | All farm operations, health, analytics and business |
| `vet` | `Vet@123` | Veterinarian | Health, vaccines, biosecurity, disease library, analytics |
| `accountant` | `Account@123` | Accountant | Finance, budgets, sales, purchases, payroll |
| `rakib` | `Officer@123` | Field Officer | Own farms, visits, daily records, photos, signatures |
| `researcher` | `Research@123` | Viewer | Read-only access and research data export |

Change these passwords after the first login (**top-right menu → Change password**). New passwords are stored
with PBKDF2-SHA256 (100,000 iterations).

---

## 2. What's inside

| Area | Pages |
|---|---|
| **Overview** | Dashboard (KPIs, revenue chart, live alerts), Early-Warning Alerts centre, Farm Calendar, Tasks (kanban) |
| **Farm operations** | Farms (with GPS location and geofence), Farm Map, Flocks/Batches (create, detail, close/harvest), Bird Management (mortality, weight), Daily Records (feed, water, temperature, humidity, THI), Field Visits (GPS check-in, photos, farmer signature, sync), Field Officers (farm assignments, sync status) |
| **Health & welfare** | Health monitoring (vaccinations, disease cases), Vaccine Programmes (templates applied to flocks), Biosecurity Audits (scored checklist), Disease Library |
| **Production & IoT** | Egg Production (hen-day %, feed per dozen), Feed Formulations & feed planner, IoT Sensors (device registry, readings, ESP32 + DHT22 sample sketch), Traceability (QR code, lifecycle timeline, public farm-to-fork page) |
| **Intelligence & research** | Analytics Hub, Growth vs Breed Standard, Flock Risk Score, Harvest Forecast, Feed Forecast, Heat-Stress (THI), Anomaly Detection, Efficiency/EPEF Benchmark, Carbon footprint estimate, Field Trials (Welch t-test), Research Data (CSV datasets, data dictionary, correlation matrix), Breed Standards |
| **Business** | Inventory (+ stock ledger), Purchase Orders (receive into stock), Suppliers, Sales Orders (+ printable invoice), Customers, Finance (transactions, profit & loss), Budgets |
| **People & reports** | Employees, Attendance, Payroll (+ payslips), Reports: Production, Financial, Inventory, Farm, HR, Health, Field Operations, Market (all with CSV export / print) |
| **Administration** | Users & Roles, Audit Log (every change is recorded automatically), Settings (thresholds, company info, API keys), Global search |

### APIs for future mobile and IoT clients
- `POST api/mobile/login`, `GET api/mobile/officers/{id}/farms`, `POST api/mobile/visits`,
  `POST api/mobile/visits/{id}/daily-records`, `POST api/mobile/visits/{id}/complete`, `POST api/mobile/visits/{id}/sync`
  are the offline-first field-officer app endpoints. They need the `X-Api-Key` header; the key is set in Settings.
- `POST api/sensors/ingest` and `GET api/sensors/{deviceCode}/latest` take readings from ESP32 environment sensors.
  The full sample sketch is on the **IoT Sensors → API docs** page.
- `GET Trace/Public/{code}` is the public consumer traceability page, opened from the QR code. No login is needed.

---

## 3. Research features (what makes it unique)

These are the parts you can build a research paper around. Each one is computed from the farm data already
in the database, and the formula is shown on its page.

1. **Flock Risk Score.** A composite early-warning index combining mortality trend, feed and water intake
   deviation, heat stress, overdue vaccinations, active disease and biosecurity grade.
2. **Anomaly detection on daily records.** Robust z-scores (median/MAD) and an EWMA control chart flag mortality
   spikes and drops in feed or water before they show up as losses.
3. **Growth benchmarking against breed standards.** Actual weight curves are compared with interpolated Cobb 500,
   Ross 308, Sonali and layer reference curves; you can edit them under Breed Standards.
4. **Harvest and feed forecasting.** Regression on recent weight samples predicts the day a flock reaches market
   weight and the feed it still needs, so purchases can be planned.
5. **Heat-stress analytics.** The Temperature-Humidity Index (THI) is calculated per record and per sensor reading, with
   warning and danger bands from Settings.
6. **Production-efficiency benchmarking.** EPEF, FCR, livability and average daily gain are compared across farms and flocks.
7. **Field-data trust (Visit Trust Score).** Each visit's GPS check-in is measured against the farm location
   (Haversine distance compared with the geofence radius in Settings). The 0–100 score adds points for GPS verified (40),
   photo evidence (20), farmer signature (20), daily records (10) and sync (10). The score is shown per visit, per officer and in
   the Field Operations report.
8. **Tamper-evident traceability.** Each flock's lifecycle events (placement, vaccinations, treatments, visits, harvest,
   sales) are linked into a SHA-256 hash chain. Pressing *Seal record* stores the chain head. After that, the timeline and
   the public QR page show whether the record is unchanged or was modified after sealing.
9. **Field trials.** Compare two groups of flocks (for example a feed additive against control) with a Welch t-test and a
   plain-language interpretation.
10. **Research data hub.** One-click CSV export of each dataset, a data dictionary generated from the live database, and a
    Pearson correlation matrix across production variables.

> The breed reference curves in `03_ReferenceData.sql` are rounded indicative values. Replace them with the
> official breed-manual tables before using them in a publication.

---

## 4. Project layout

```
PoultryPro/
├─ Controllers/        43 MVC + API controllers
├─ Services/           data-access services (Dapper), one folder per module, auto-registered
├─ Models/             Entities, ViewModels, DTOs
├─ Infrastructure/     DB initializer, auth roles, password hashing, audit filter, CSV, PoultryMath (THI, EPEF, regression, EWMA, t-test)
├─ Views/              Razor pages (125+)
├─ SQL/                00–04 install scripts (+ legacy originals)
└─ wwwroot/            CSS design system, JS helpers, images; uploads/ is created at runtime
```

Any class named `XxxService` that implements `IXxxService` is registered automatically in `Program.cs`, so a new
module only needs a service, a controller and its views.

## 5. Troubleshooting

| Symptom | Fix |
|---|---|
| Browser opens on **Database setup** | SQL Server isn't reachable. Check that the SQL Server service is running and fix `ConnectionStrings:Default`, then click *Retry*. |
| `Login failed for user` | Use a SQL login in the connection string, or give your Windows account access in SSMS. |
| HTTPS certificate warning | Accept Visual Studio's prompt to trust the development certificate, or use the `http://localhost:56548` address. |
| Maps, charts or icons missing | These load from a CDN (cdnjs), so the PC needs internet access. |
| A page shows an error after you edited SQL | Re-run `01_Schema.sql` and `02_Views.sql`, or just restart the app; it re-applies them on start-up. |
P o u l t r y P r o  
 