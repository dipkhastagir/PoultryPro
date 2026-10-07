-- ============================================================================
-- PoultryPro — STEP 4 : Demonstration data set
-- ----------------------------------------------------------------------------
-- Generates a realistic, internally consistent data set relative to TODAY:
--   5 farms, 11 flocks (active + harvested), ~400 daily flock records,
--   field visits with GPS, vaccination programmes, disease cases, IoT sensor
--   telemetry (7 days), layer egg production (90 days), purchase orders,
--   sales orders, 12 months of finance, HR attendance & payroll, audits,
--   and two field trials for the research module.
-- The script runs ONLY when the Farms table is empty, so it never touches
-- an existing production database.
-- ============================================================================
USE PoultryProDB;
GO
SET NOCOUNT ON;
GO
-- Skip when the database already holds data the demo set could collide with
-- (farms, or field-officer user names / PO numbers, which are unique keys).
IF EXISTS (SELECT 1 FROM dbo.Farms) OR EXISTS (SELECT 1 FROM dbo.FieldOfficers) OR EXISTS (SELECT 1 FROM dbo.PurchaseOrders)
BEGIN
    PRINT 'Demo data skipped: the database already contains operational data.';
    SET NOEXEC ON;
END
GO

-- All-or-nothing: if any statement fails the whole demo set is rolled back, so the
-- next start-up (which only seeds an empty database) can simply try again.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @today DATE = CAST(GETDATE() AS DATE);
DECLARE @f1 INT, @f2 INT, @f3 INT, @f4 INT, @f5 INT;

/* ---------------------------------------------------------------------------
   FARMS
   ------------------------------------------------------------------------- */
INSERT INTO dbo.Farms (Name, Location, Capacity, CurrentStock, EmployeeCount, CertStatus, CertExpiry, FarmType, Latitude, Longitude, OwnerName, OwnerPhone, Zone, ShedCount, AreaSqFt, Notes)
VALUES ('Gazipur North Farm', 'Kaliakair, Gazipur', 12000, 0, 0, 'Valid', DATEADD(MONTH, 9, @today), 'Broiler', 24.073900, 90.216700, 'Abdul Karim', '01711223344', 'Gazipur Zone', 2, 9000, 'Environment-controlled broiler houses with tunnel ventilation.');
SET @f1 = SCOPE_IDENTITY();
INSERT INTO dbo.Farms (Name, Location, Capacity, CurrentStock, EmployeeCount, CertStatus, CertExpiry, FarmType, Latitude, Longitude, OwnerName, OwnerPhone, Zone, ShedCount, AreaSqFt, Notes)
VALUES ('Savar Unit', 'Ashulia, Savar, Dhaka', 14000, 0, 0, 'Expiring Soon', DATEADD(DAY, 14, @today), 'Broiler', 23.858300, 90.266700, 'Monir Hossain', '01811223355', 'Dhaka South', 2, 10500, 'Open-sided houses with curtain management.');
SET @f2 = SCOPE_IDENTITY();
INSERT INTO dbo.Farms (Name, Location, Capacity, CurrentStock, EmployeeCount, CertStatus, CertExpiry, FarmType, Latitude, Longitude, OwnerName, OwnerPhone, Zone, ShedCount, AreaSqFt, Notes)
VALUES ('Tongi Sonali Farm', 'Tongi, Gazipur', 8000, 0, 0, 'Valid', DATEADD(MONTH, 14, @today), 'Sonali', 23.891500, 90.402300, 'Rina Begum', '01911223366', 'Gazipur Zone', 1, 6000, 'Sonali (RIR x Fayoumi) grow-out unit.');
SET @f3 = SCOPE_IDENTITY();
INSERT INTO dbo.Farms (Name, Location, Capacity, CurrentStock, EmployeeCount, CertStatus, CertExpiry, FarmType, Latitude, Longitude, OwnerName, OwnerPhone, Zone, ShedCount, AreaSqFt, Notes)
VALUES ('Narayanganj Layer Unit', 'Rupganj, Narayanganj', 15000, 0, 0, 'Valid', DATEADD(MONTH, 6, @today), 'Layer', 23.790000, 90.530000, 'Selim Reza', '01611223377', 'Dhaka South', 3, 12000, 'Commercial brown-egg layer unit with cage system.');
SET @f4 = SCOPE_IDENTITY();
INSERT INTO dbo.Farms (Name, Location, Capacity, CurrentStock, EmployeeCount, CertStatus, CertExpiry, FarmType, Latitude, Longitude, OwnerName, OwnerPhone, Zone, ShedCount, AreaSqFt, Notes)
VALUES ('Mymensingh Research Farm', 'Trishal, Mymensingh', 20000, 0, 0, 'Valid', DATEADD(MONTH, 18, @today), 'Broiler', 24.582000, 90.395000, 'PoultryPro R&D', '01511223388', 'Mymensingh', 2, 15000, 'Twin identical sheds used for controlled field trials (treatment vs control).');
SET @f5 = SCOPE_IDENTITY();

/* ---------------------------------------------------------------------------
   SUPPLIERS
   ------------------------------------------------------------------------- */
DECLARE @sFeed1 INT, @sFeed2 INT, @sMed1 INT, @sMed2 INT, @sSup INT, @sEq INT, @sBio INT, @sChick INT;
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('ACI Agri', 'Rahman Saheb', '01711000001', 'sales@aci-agri.example', 'Tejgaon, Dhaka', 'Feed', 4.5, 'Net 30');
SET @sFeed1 = SCOPE_IDENTITY();
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('Nourish Poultry', 'Karim Bhai', '01811000002', 'orders@nourish.example', 'Shreepur, Gazipur', 'Feed', 4.2, 'Net 15');
SET @sFeed2 = SCOPE_IDENTITY();
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('Opsonin Pharma', 'Dr. Sajjad', '01911000003', 'vet@opsonin.example', 'Barishal', 'Medicine', 4.6, 'Net 30');
SET @sMed1 = SCOPE_IDENTITY();
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('Renata Ltd', 'Hasan Saheb', '01711000004', 'animalhealth@renata.example', 'Mirpur, Dhaka', 'Vaccine', 4.4, 'Cash on delivery');
SET @sMed2 = SCOPE_IDENTITY();
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('Eskayef BD', 'Rina Apa', '01811000005', 'agrovet@eskayef.example', 'Tongi, Gazipur', 'Supplement', 4.0, 'Net 30');
SET @sSup = SCOPE_IDENTITY();
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('Walton BD', 'Arif Vai', '01911000006', 'corporate@walton.example', 'Chandra, Gazipur', 'Equipment', 3.8, 'Advance 50%');
SET @sEq = SCOPE_IDENTITY();
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('Aftab Agro', 'Siraj Bhai', '01711000007', 'info@aftab.example', 'Bhairab, Kishoreganj', 'Biosecurity', 4.1, 'Net 15');
SET @sBio = SCOPE_IDENTITY();
INSERT INTO dbo.Suppliers (Name, ContactPerson, Phone, Email, Address, Category, Rating, PaymentTerms) VALUES ('Kazi Hatchery', 'Jamal Uddin', '01611000008', 'chicks@kazihatch.example', 'Panchagarh', 'Day-old Chicks', 4.7, 'Advance');
SET @sChick = SCOPE_IDENTITY();

/* ---------------------------------------------------------------------------
   BATCH PLAN (temporary helper table)
   ------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#B') IS NOT NULL DROP TABLE #B;
-- String columns of the temp tables use COLLATE DATABASE_DEFAULT so that joins with dbo tables
-- also work when the database collation differs from the server (tempdb) collation.
CREATE TABLE #B (
    Code NVARCHAR(20) COLLATE DATABASE_DEFAULT, Breed NVARCHAR(100) COLLATE DATABASE_DEFAULT,
    Kind NVARCHAR(20) COLLATE DATABASE_DEFAULT, FarmId INT, Initial INT,
    PlaceOffset INT, CloseAge INT NULL, Perf DECIMAL(5,3), FeedF DECIMAL(5,3),
    Shed NVARCHAR(20) COLLATE DATABASE_DEFAULT, ChickCost DECIMAL(10,2), Spike BIT, BatchId INT NULL, Placement DATE NULL);

INSERT INTO #B (Code, Breed, Kind, FarmId, Initial, PlaceOffset, CloseAge, Perf, FeedF, Shed, ChickCost, Spike) VALUES
('B-2510', 'Cobb 500',      'Broiler', @f1, 8000, 150, 35,   0.970, 1.020, 'S-1', 48, 0),
('B-2511', 'Sonali',        'Sonali',  @f3, 6500, 170, 63,   0.980, 1.000, 'S-1', 32, 0),
('B-2512', 'Cobb 500',      'Broiler', @f1, 8200,  95, 35,   0.995, 0.990, 'S-2', 52, 0),
('B-2513', 'Ross 308',      'Broiler', @f2, 7200, 100, 36,   0.950, 1.040, 'S-1', 50, 0),
('B-2601', 'Cobb 500',      'Broiler', @f1, 8200,  24, NULL, 0.975, 1.010, 'S-1', 55, 1),
('B-2602', 'Ross 308',      'Broiler', @f2, 7500,  36, NULL, 0.960, 1.030, 'S-1', 54, 0),
('B-2603', 'Sonali',        'Sonali',  @f3, 6800,  45, NULL, 1.000, 1.000, 'S-1', 34, 0),
('B-2604', 'Hy-Line Brown', 'Layer',   @f4, 5400, 210, NULL, 1.000, 1.000, 'L-1', 95, 0),
('B-2605', 'Cobb 500',      'Broiler', @f5, 9000,  30, NULL, 1.020, 0.970, 'S-1', 55, 0),
('B-2606', 'Cobb 500',      'Broiler', @f5, 9000,  30, NULL, 0.975, 1.020, 'S-2', 55, 0),
('B-2607', 'Ross 308',      'Broiler', @f2, 6000,   6, NULL, 0.990, 1.000, 'S-2', 56, 0);

UPDATE #B SET Placement = DATEADD(DAY, -PlaceOffset, @today);

INSERT INTO dbo.Batches (BatchCode, Breed, BatchType, FarmId, InitialCount, CurrentCount, MortalityCount, CullingCount,
                         PlacementDate, ExpectedHarvestDate, ClosedDate, HealthStatus, AverageWeightKg, TotalFeedKg, IsActive,
                         ShedNo, HatcheryName, ChickCostPerBird, Notes)
SELECT Code, Breed, Kind, FarmId, Initial, Initial, 0, 0,
       Placement,
       CASE Kind WHEN 'Broiler' THEN DATEADD(DAY, 35, Placement) WHEN 'Sonali' THEN DATEADD(DAY, 63, Placement) ELSE NULL END,
       CASE WHEN CloseAge IS NULL THEN NULL ELSE DATEADD(DAY, CloseAge, Placement) END,
       'Healthy', 0, 0,
       CASE WHEN CloseAge IS NULL THEN 1 ELSE 0 END,
       Shed, 'Kazi Hatchery', ChickCost,
       CASE Code WHEN 'B-2605' THEN 'Trial TR-2026-01 TREATMENT shed: Bacillus subtilis probiotic in drinking water.'
                 WHEN 'B-2606' THEN 'Trial TR-2026-01 CONTROL shed: standard programme, no probiotic.'
                 WHEN 'B-2601' THEN 'Heat-wave event recorded around day 17-19.'
                 ELSE NULL END
FROM #B;

UPDATE b SET BatchId = x.Id FROM #B b INNER JOIN dbo.Batches x ON x.BatchCode = b.Code;

/* ---------------------------------------------------------------------------
   DAILY FLOCK RECORDS (generated from breed standards + noise)
   ------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#N') IS NOT NULL DROP TABLE #N;
SELECT TOP (400) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
INTO #N FROM sys.all_objects a CROSS JOIN sys.all_objects b;

IF OBJECT_ID('tempdb..#D') IS NOT NULL DROP TABLE #D;
CREATE TABLE #D (
    BatchId INT, FarmId INT, Code NVARCHAR(20) COLLATE DATABASE_DEFAULT, Kind NVARCHAR(20) COLLATE DATABASE_DEFAULT,
    Breed NVARCHAR(100) COLLATE DATABASE_DEFAULT, n INT, RecordDate DATE,
    StartCount INT, StdW DECIMAL(10,2) NULL, StdF DECIMAL(10,2) NULL, Perf DECIMAL(5,3), FeedF DECIMAL(5,3), Spike BIT,
    Mort INT NULL, Cull INT NULL, FeedGm DECIMAL(10,2) NULL, WeightGm DECIMAL(10,2) NULL,
    TempC DECIMAL(5,2) NULL, Hum DECIMAL(5,2) NULL, WaterF DECIMAL(6,3) NULL, BirdCount INT NULL);

INSERT INTO #D (BatchId, FarmId, Code, Kind, Breed, n, RecordDate, StartCount, StdW, StdF, Perf, FeedF, Spike)
SELECT b.BatchId, b.FarmId, b.Code, b.Kind, b.Breed, t.n, DATEADD(DAY, t.n, b.Placement),
       CASE WHEN b.Kind = 'Layer' THEN CAST(b.Initial * 0.95 AS INT) ELSE b.Initial END,
       CASE WHEN s0.A0 IS NULL THEN NULL
            WHEN s1.A1 IS NULL THEN s0.W0
            ELSE s0.W0 + (s1.W1 - s0.W0) * (t.n - s0.A0) / CAST(s1.A1 - s0.A0 AS DECIMAL(9,3)) END,
       CASE WHEN s0.A0 IS NULL THEN NULL
            WHEN s1.A1 IS NULL THEN s0.F0
            ELSE s0.F0 + (s1.F1 - s0.F0) * (t.n - s0.A0) / CAST(s1.A1 - s0.A0 AS DECIMAL(9,3)) END,
       b.Perf, b.FeedF, b.Spike
FROM #B b
INNER JOIN #N t ON t.n BETWEEN (CASE WHEN b.Kind = 'Layer' THEN b.PlaceOffset - 59 ELSE 1 END)
                           AND ISNULL(b.CloseAge, b.PlaceOffset)
OUTER APPLY (SELECT TOP 1 s.AgeDay AS A0, s.TargetWeightGm AS W0, s.DailyFeedGm AS F0
             FROM dbo.BreedStandards s WHERE s.Breed = b.Breed AND s.AgeDay <= t.n ORDER BY s.AgeDay DESC) s0
OUTER APPLY (SELECT TOP 1 s.AgeDay AS A1, s.TargetWeightGm AS W1, s.DailyFeedGm AS F1
             FROM dbo.BreedStandards s WHERE s.Breed = b.Breed AND s.AgeDay > t.n ORDER BY s.AgeDay) s1;

-- biological values with random noise (NEWID evaluated per row)
UPDATE #D SET
    Mort = CAST(ROUND(StartCount
            * (CASE Kind WHEN 'Layer' THEN 0.00025 WHEN 'Sonali' THEN 0.00050
                         ELSE CASE WHEN n <= 7 THEN 0.00130 ELSE 0.00060 END END)
            * (2.0 - Perf)
            * (CASE WHEN Spike = 1 AND n BETWEEN 17 AND 19 THEN 6.0 ELSE 1.0 END)
            * (0.40 + (ABS(CHECKSUM(NEWID()) % 120)) / 100.0), 0) AS INT),
    Cull = CASE WHEN ABS(CHECKSUM(NEWID()) % 9) = 0 THEN 1 + ABS(CHECKSUM(NEWID()) % 4) ELSE 0 END,
    WeightGm = CASE WHEN Kind = 'Layer' THEN 1690 + (ABS(CHECKSUM(NEWID()) % 80))
                    ELSE ISNULL(StdW, 40) * Perf * (0.98 + (ABS(CHECKSUM(NEWID()) % 40)) / 1000.0) END,
    FeedGm = CASE WHEN Kind = 'Layer' THEN 110 + (ABS(CHECKSUM(NEWID()) % 7))
                  ELSE (CASE WHEN ISNULL(StdF, 0) < 12 THEN 12 ELSE StdF END) * FeedF
                       * (0.97 + (ABS(CHECKSUM(NEWID()) % 60)) / 1000.0)
                       * (CASE WHEN Spike = 1 AND n BETWEEN 16 AND 19 THEN 0.85 ELSE 1.0 END) END,
    TempC = CASE WHEN Spike = 1 AND n BETWEEN 16 AND 19 THEN 35.0 + (ABS(CHECKSUM(NEWID()) % 15)) / 10.0
                 WHEN Kind = 'Broiler' THEN (CASE WHEN 33 - 0.3 * n < 24 THEN 24 ELSE 33 - 0.3 * n END) + ((ABS(CHECKSUM(NEWID()) % 30)) - 15) / 10.0
                 WHEN Kind = 'Sonali'  THEN (CASE WHEN 31 - 0.15 * n < 26 THEN 26 ELSE 31 - 0.15 * n END) + ((ABS(CHECKSUM(NEWID()) % 30)) - 15) / 10.0
                 ELSE 28 + ((ABS(CHECKSUM(NEWID()) % 40)) - 20) / 10.0 END,
    Hum = CASE WHEN Spike = 1 AND n BETWEEN 16 AND 19 THEN 80 + ABS(CHECKSUM(NEWID()) % 6)
               ELSE 58 + ABS(CHECKSUM(NEWID()) % 20) END,
    WaterF = (1.80 + (ABS(CHECKSUM(NEWID()) % 30)) / 100.0) * (CASE WHEN Spike = 1 AND n BETWEEN 16 AND 19 THEN 1.4 ELSE 1.0 END);

-- running live-bird count
;WITH c AS (
    SELECT BirdCount, StartCount - SUM(Mort + Cull) OVER (PARTITION BY BatchId ORDER BY n ROWS UNBOUNDED PRECEDING) AS bc
    FROM #D)
UPDATE c SET BirdCount = bc;

INSERT INTO dbo.DailyFarmRecords
    (VisitId, BatchId, FarmId, RecordDate, BirdCount, MortalityCount, CullingCount,
     FeedConsumptionKg, WaterConsumptionLtr, TemperatureC, HumidityPercent, LightHours, AverageWeightGm,
     MedicineUsed, MedicineDosage, VaccineUsed, Remarks, SyncStatus, SyncedAt, CreatedAt)
SELECT NULL, d.BatchId, d.FarmId, d.RecordDate, d.BirdCount, d.Mort, d.Cull,
       CAST(d.BirdCount * d.FeedGm / 1000.0 AS DECIMAL(10,2)),
       CAST(d.BirdCount * d.FeedGm / 1000.0 * d.WaterF AS DECIMAL(10,2)),
       d.TempC, d.Hum,
       CASE WHEN d.Kind = 'Broiler' AND d.n <= 7 THEN 23 WHEN d.Kind = 'Broiler' THEN 20 ELSE 16 END,
       CAST(d.WeightGm AS DECIMAL(8,2)),
       CASE WHEN d.Spike = 1 AND d.n BETWEEN 16 AND 19 THEN 'Vitamin C + Electrolyte'
            WHEN d.n BETWEEN 1 AND 3 AND d.Kind <> 'Layer' THEN 'Multivitamin (Rena-WS)'
            WHEN d.Kind <> 'Layer' AND d.n IN (11, 12, 19, 22) THEN 'Vitamin AD3E'
            ELSE NULL END,
       CASE WHEN d.Spike = 1 AND d.n BETWEEN 16 AND 19 THEN '1 g/L water'
            WHEN d.n BETWEEN 1 AND 3 AND d.Kind <> 'Layer' THEN '1 g/2 L water'
            WHEN d.Kind <> 'Layer' AND d.n IN (11, 12, 19, 22) THEN '1 ml/L water'
            ELSE NULL END,
       CASE WHEN d.Kind = 'Broiler' AND d.n = 5  THEN 'Newcastle Disease (BCRDV)'
            WHEN d.Kind = 'Broiler' AND d.n = 10 THEN 'Gumboro / IBD (intermediate plus)'
            WHEN d.Kind = 'Broiler' AND d.n = 18 THEN 'Gumboro / IBD booster'
            WHEN d.Kind = 'Sonali'  AND d.n = 3  THEN 'Newcastle Disease (BCRDV)'
            WHEN d.Kind = 'Sonali'  AND d.n = 10 THEN 'Gumboro / IBD'
            ELSE NULL END,
       CASE WHEN d.Spike = 1 AND d.n BETWEEN 16 AND 19 THEN 'Heat wave: heavy panting observed, extra fans and foggers running.'
            WHEN d.n % 10 = 0 THEN 'Litter turned and top-dressed.'
            ELSE NULL END,
       CASE WHEN d.RecordDate >= DATEADD(DAY, -1, @today) THEN 'Pending' ELSE 'Synced' END,
       CASE WHEN d.RecordDate >= DATEADD(DAY, -1, @today) THEN NULL ELSE DATEADD(HOUR, 18, CAST(d.RecordDate AS DATETIME)) END,
       DATEADD(HOUR, 17, CAST(d.RecordDate AS DATETIME))
FROM #D d;

/* ---------------------------------------------------------------------------
   BATCH AGGREGATES
   ------------------------------------------------------------------------- */
UPDATE bt SET
    MortalityCount  = a.Mort + (CASE WHEN b.Kind = 'Layer' THEN b.Initial - CAST(b.Initial * 0.95 AS INT) ELSE 0 END),
    CullingCount    = a.Cull,
    TotalFeedKg     = a.Feed,
    CurrentCount    = l.BirdCount,
    AverageWeightKg = CAST(l.AverageWeightGm / 1000.0 AS DECIMAL(6,3))
FROM dbo.Batches bt
INNER JOIN #B b ON b.BatchId = bt.Id
CROSS APPLY (SELECT SUM(MortalityCount) AS Mort, SUM(CullingCount) AS Cull, SUM(FeedConsumptionKg) AS Feed
             FROM dbo.DailyFarmRecords WHERE BatchId = bt.Id) a
CROSS APPLY (SELECT TOP 1 BirdCount, AverageWeightGm FROM dbo.DailyFarmRecords WHERE BatchId = bt.Id ORDER BY RecordDate DESC) l;

UPDATE dbo.Batches SET HarvestedCount = CurrentCount,
                       HarvestWeightKg = CAST(CurrentCount * AverageWeightKg AS DECIMAL(12,2)),
                       HealthStatus = 'Harvested'
WHERE BatchCode IN ('B-2510','B-2511','B-2512','B-2513');
UPDATE dbo.Batches SET HealthStatus = 'Ready for Sale'    WHERE BatchCode = 'B-2602';
UPDATE dbo.Batches SET HealthStatus = 'Under Observation' WHERE BatchCode = 'B-2601';
UPDATE dbo.Batches SET HealthStatus = 'Vaccination Due'   WHERE BatchCode = 'B-2603';
UPDATE dbo.Batches SET HealthStatus = 'In Production'     WHERE BatchCode = 'B-2604';

/* ---------------------------------------------------------------------------
   WEIGHT SAMPLES & MORTALITY LOG
   ------------------------------------------------------------------------- */
INSERT INTO dbo.WeightRecords (BatchId, RecordDate, AgeInDays, AvgWeightKg, SampleSize, UniformityPct)
SELECT d.BatchId, d.RecordDate, d.n, CAST(d.WeightGm / 1000.0 AS DECIMAL(6,3)), 50, 78 + ABS(CHECKSUM(NEWID()) % 12)
FROM #D d
WHERE d.Kind <> 'Layer' AND (d.n % 7 = 0 OR d.n = 1);

INSERT INTO dbo.MortalityLogs (BatchId, LogDate, Count, Cause, Notes, CreatedAt)
SELECT d.BatchId, d.RecordDate, d.Mort,
       CASE WHEN d.Spike = 1 AND d.n BETWEEN 16 AND 19 THEN 'Heat stress'
            WHEN d.n <= 7 THEN 'Weak chicks / yolk-sac infection'
            ELSE CASE ABS(CHECKSUM(NEWID()) % 6) WHEN 0 THEN 'Leg disorder' WHEN 1 THEN 'Ascites'
                     WHEN 2 THEN 'Respiratory' WHEN 3 THEN 'Sudden death syndrome'
                     WHEN 4 THEN 'Coccidiosis suspected' ELSE 'Unknown' END END,
       CASE WHEN d.Spike = 1 AND d.n BETWEEN 16 AND 19 THEN 'Afternoon shed temperature above 35 C.' ELSE NULL END,
       DATEADD(HOUR, 17, CAST(d.RecordDate AS DATETIME))
FROM #D d
WHERE d.Mort > 0 AND (d.n % 4 = 0 OR (d.Spike = 1 AND d.n BETWEEN 16 AND 19));

/* ---------------------------------------------------------------------------
   FIELD OFFICERS, ASSIGNMENTS, VISITS, SIGNATURES, PHOTOS
   ------------------------------------------------------------------------- */
DECLARE @o1 INT, @o2 INT, @o3 INT, @o4 INT;
INSERT INTO dbo.FieldOfficers (FullName, Username, PasswordHash, Phone, Email, Zone, Designation, LastLoginAt)
VALUES ('Rakib Hossain', 'rakib', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Officer@123PoultryProSalt2025'), 2)), '01755000001', 'rakib@poultrypro.local', 'Gazipur Zone', 'Senior Field Officer', DATEADD(HOUR, -3, GETDATE()));
SET @o1 = SCOPE_IDENTITY();
INSERT INTO dbo.FieldOfficers (FullName, Username, PasswordHash, Phone, Email, Zone, Designation, LastLoginAt)
VALUES ('Sharmin Akter', 'sharmin', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Officer@123PoultryProSalt2025'), 2)), '01755000002', 'sharmin@poultrypro.local', 'Dhaka South', 'Field Officer', DATEADD(HOUR, -26, GETDATE()));
SET @o2 = SCOPE_IDENTITY();
INSERT INTO dbo.FieldOfficers (FullName, Username, PasswordHash, Phone, Email, Zone, Designation, LastLoginAt)
VALUES ('Tanvir Ahmed', 'tanvir', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Officer@123PoultryProSalt2025'), 2)), '01755000003', 'tanvir@poultrypro.local', 'Mymensingh', 'Research Field Officer', DATEADD(HOUR, -5, GETDATE()));
SET @o3 = SCOPE_IDENTITY();
INSERT INTO dbo.FieldOfficers (FullName, Username, PasswordHash, Phone, Email, Zone, Designation, LastLoginAt)
VALUES ('Mitu Das', 'mitu', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Officer@123PoultryProSalt2025'), 2)), '01755000004', 'mitu@poultrypro.local', 'Dhaka North', 'Trainee Field Officer', NULL);
SET @o4 = SCOPE_IDENTITY();

INSERT INTO dbo.FieldOfficerFarmAssignments (FieldOfficerId, FarmId, AssignedDate) VALUES
(@o1, @f1, DATEADD(MONTH, -8, @today)), (@o1, @f3, DATEADD(MONTH, -8, @today)),
(@o2, @f2, DATEADD(MONTH, -7, @today)), (@o2, @f4, DATEADD(MONTH, -7, @today)),
(@o3, @f5, DATEADD(MONTH, -4, @today)), (@o4, @f2, DATEADD(MONTH, -1, @today));

IF NOT EXISTS (SELECT 1 FROM dbo.AppUsers WHERE Username = 'rakib')
    INSERT INTO dbo.AppUsers (FullName, Username, Email, Phone, PasswordHash, Role, FieldOfficerId)
    VALUES ('Rakib Hossain', 'rakib', 'rakib@poultrypro.local', '01755000001',
            LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Officer@123PoultryProSalt2025'), 2)), 'FieldOfficer', @o1);

INSERT INTO dbo.FarmVisits (FieldOfficerId, FarmId, BatchId, VisitDate, VisitPurpose, VisitStatus,
                            CheckInTime, CheckOutTime, CheckInLatitude, CheckInLongitude, GpsAccuracyMeters,
                            OverallRemark, SyncStatus, SyncedAt, CreatedAt)
SELECT CASE WHEN d.FarmId IN (@f1, @f3) THEN @o1 WHEN d.FarmId IN (@f2, @f4) THEN @o2 ELSE @o3 END,
       d.FarmId, d.BatchId, d.RecordDate,
       CASE WHEN d.Spike = 1 AND d.n BETWEEN 16 AND 19 THEN 'Disease Investigation'
            WHEN d.n IN (5, 10, 18, 21) THEN 'Vaccination'
            WHEN d.n % 9 = 0 THEN 'Weight Sampling'
            ELSE 'Routine Check' END,
       CASE WHEN d.RecordDate = @today THEN 'In Progress' ELSE 'Completed' END,
       DATEADD(MINUTE, 540 + ABS(CHECKSUM(NEWID()) % 180), CAST(d.RecordDate AS DATETIME)),
       CASE WHEN d.RecordDate = @today THEN NULL
            ELSE DATEADD(MINUTE, 780 + ABS(CHECKSUM(NEWID()) % 90), CAST(d.RecordDate AS DATETIME)) END,
       f.Latitude  + ((ABS(CHECKSUM(NEWID()) % 200)) - 100) / 100000.0,
       f.Longitude + ((ABS(CHECKSUM(NEWID()) % 200)) - 100) / 100000.0,
       4 + ABS(CHECKSUM(NEWID()) % 12),
       CASE WHEN d.Spike = 1 AND d.n BETWEEN 16 AND 19 THEN 'Heat stress signs; advised foggers, electrolytes and feed withdrawal 11am-4pm.'
            WHEN d.Mort > 8 THEN 'Mortality slightly high, post-mortem samples taken.'
            ELSE CASE ABS(CHECKSUM(NEWID()) % 4) WHEN 0 THEN 'Flock active, litter dry, drinkers clean.'
                                                WHEN 1 THEN 'Good uniformity; ventilation adequate.'
                                                WHEN 2 THEN 'Feed and water intake normal.'
                                                ELSE 'Biosecurity footbath refreshed; flock normal.' END END,
       CASE WHEN d.RecordDate >= DATEADD(DAY, -1, @today) THEN 'Pending' ELSE 'Synced' END,
       CASE WHEN d.RecordDate >= DATEADD(DAY, -1, @today) THEN NULL ELSE DATEADD(HOUR, 18, CAST(d.RecordDate AS DATETIME)) END,
       DATEADD(HOUR, 9, CAST(d.RecordDate AS DATETIME))
FROM #D d
INNER JOIN dbo.Farms f ON f.Id = d.FarmId
WHERE d.n % 3 = 0 OR d.RecordDate = @today OR (d.Spike = 1 AND d.n BETWEEN 16 AND 19);

UPDATE dr SET VisitId = v.Id
FROM dbo.DailyFarmRecords dr
INNER JOIN dbo.FarmVisits v ON v.BatchId = dr.BatchId AND v.VisitDate = dr.RecordDate;

INSERT INTO dbo.FarmerSignatures (VisitId, FarmerName, FarmerPhone, SignatureFilePath, IsVerifiedByFarmer, SignedAt, SyncStatus, SyncedAt)
SELECT v.Id, f.OwnerName, f.OwnerPhone, '/img/samples/signature.svg', 1, v.CheckOutTime, v.SyncStatus, v.SyncedAt
FROM dbo.FarmVisits v INNER JOIN dbo.Farms f ON f.Id = v.FarmId
WHERE v.VisitStatus = 'Completed' AND v.Id % 2 = 0;

INSERT INTO dbo.VisitPhotos (VisitId, PhotoType, FileName, FilePath, FileSizeKb, Caption, CapturedAt, SyncStatus, SyncedAt)
SELECT TOP (40) v.Id,
       CASE v.Id % 4 WHEN 0 THEN 'Shed' WHEN 1 THEN 'Birds' WHEN 2 THEN 'Feed' ELSE 'Water' END,
       CONCAT('visit_', v.Id, '.svg'),
       CASE v.Id % 4 WHEN 0 THEN '/img/samples/shed.svg' WHEN 1 THEN '/img/samples/birds.svg'
                     WHEN 2 THEN '/img/samples/feed.svg' ELSE '/img/samples/water.svg' END,
       120 + v.Id % 300,
       CASE v.Id % 4 WHEN 0 THEN 'Shed overview' WHEN 1 THEN 'Flock condition' WHEN 2 THEN 'Feeder line' ELSE 'Nipple drinker line' END,
       DATEADD(MINUTE, 20, v.CheckInTime), v.SyncStatus, v.SyncedAt
FROM dbo.FarmVisits v
ORDER BY v.VisitDate DESC;

/* ---------------------------------------------------------------------------
   VACCINATION PROGRAMMES (from templates)
   ------------------------------------------------------------------------- */
INSERT INTO dbo.VaccinationRecords (BatchId, VaccineName, DueDate, CompletedDate, Status, DoseMl, Route, AdministeredBy, Notes)
SELECT b.BatchId, i.VaccineName, DATEADD(DAY, i.AgeDay, b.Placement),
       CASE WHEN DATEADD(DAY, i.AgeDay, b.Placement) < @today
                 AND NOT (b.Code = 'B-2601' AND i.AgeDay = 21)
                 AND NOT (b.Code = 'B-2603' AND i.AgeDay = 35)
            THEN DATEADD(DAY, i.AgeDay, b.Placement) ELSE NULL END,
       CASE WHEN (b.Code = 'B-2601' AND i.AgeDay = 21) OR (b.Code = 'B-2603' AND i.AgeDay = 35) THEN 'Overdue'
            WHEN DATEADD(DAY, i.AgeDay, b.Placement) < @today THEN 'Done'
            ELSE 'Planned' END,
       i.DoseMl, i.Route,
       CASE WHEN DATEADD(DAY, i.AgeDay, b.Placement) < @today THEN 'Field team' ELSE NULL END,
       CONCAT('Programme: ', t.Name)
FROM #B b
INNER JOIN dbo.VaccineScheduleTemplates t ON t.BatchType = b.Kind
INNER JOIN dbo.VaccineScheduleItems i ON i.TemplateId = t.Id
WHERE b.CloseAge IS NULL OR i.AgeDay <= b.CloseAge;

/* ---------------------------------------------------------------------------
   DISEASE CASES
   ------------------------------------------------------------------------- */
INSERT INTO dbo.DiseaseCases (BatchId, DiseaseName, ReportedDate, AffectedCount, DeathCount, Treatment, Status, ResolvedDate, DiagnosedBy, Notes)
SELECT BatchId, 'Coccidiosis', DATEADD(DAY, 24, Placement), 350, 41, 'Amprolium 1 g/L for 5 days + Vitamin K', 'Resolved', DATEADD(DAY, 30, Placement), 'Dr. Mahmudul Hasan', 'Wet litter near drinker line 2.' FROM #B WHERE Code = 'B-2512'
UNION ALL
SELECT BatchId, 'Colibacillosis (E. coli)', DATEADD(DAY, 15, Placement), 220, 30, 'Enrofloxacin per sensitivity test, water-line sanitation', 'Resolved', DATEADD(DAY, 21, Placement), 'Dr. Mahmudul Hasan', NULL FROM #B WHERE Code = 'B-2513'
UNION ALL
SELECT BatchId, 'Heat Stress', DATEADD(DAY, 17, Placement), 900, 95, 'Electrolytes + Vitamin C, foggers, feed withdrawal in afternoon', 'Resolved', DATEADD(DAY, 20, Placement), 'Rakib Hossain (FO)', 'Ambient 37 C heat wave.' FROM #B WHERE Code = 'B-2601'
UNION ALL
SELECT BatchId, 'Chronic Respiratory Disease (Mycoplasma gallisepticum)', DATEADD(DAY, -3, @today), 140, 6, 'Tylosin 0.5 g/L for 5 days; improve ventilation', 'Under Treatment', NULL, 'Dr. Mahmudul Hasan', 'Rales heard in 2% of birds.' FROM #B WHERE Code = 'B-2603'
UNION ALL
SELECT BatchId, 'Infectious Bronchitis (IB)', DATEADD(DAY, -40, @today), 300, 12, 'Supportive care, electrolytes', 'Resolved', DATEADD(DAY, -30, @today), 'Dr. Mahmudul Hasan', 'Temporary drop in egg production.' FROM #B WHERE Code = 'B-2604';

/* ---------------------------------------------------------------------------
   LAYER EGG PRODUCTION (90 days)
   ------------------------------------------------------------------------- */
INSERT INTO dbo.EggProductionRecords (BatchId, RecordDate, HenCount, TotalEggs, GradeA, GradeB, CrackedEggs, DirtyEggs, AvgEggWeightGm, FeedKg, Notes)
SELECT x.BatchId, x.RecDate, x.Hens, x.Eggs,
       CAST(x.Eggs * 0.875 AS INT), CAST(x.Eggs * 0.085 AS INT), CAST(x.Eggs * 0.025 AS INT),
       x.Eggs - CAST(x.Eggs * 0.875 AS INT) - CAST(x.Eggs * 0.085 AS INT) - CAST(x.Eggs * 0.025 AS INT),
       CAST(CASE WHEN 47 + (x.Age - 120) * 0.15 > 61 THEN 61 ELSE 47 + (x.Age - 120) * 0.15 END AS DECIMAL(5,1)),
       CAST(x.Hens * 0.112 AS DECIMAL(10,2)),
       CASE WHEN x.Age BETWEEN 168 AND 178 THEN 'IB episode - production dip' ELSE NULL END
FROM (
    SELECT b.BatchId, DATEADD(DAY, -t.n + 1, @today) AS RecDate, b.PlaceOffset - t.n + 1 AS Age,
           CAST(b.Initial * 0.95 - (90 - t.n) * 1.2 AS INT) AS Hens,
           CAST((b.Initial * 0.95 - (90 - t.n) * 1.2)
                * (CASE WHEN b.PlaceOffset - t.n + 1 < 126 THEN 5 + (b.PlaceOffset - t.n + 1 - 120) * 2.0
                        WHEN 15 + (b.PlaceOffset - t.n + 1 - 126) * 1.6 > 94 THEN 94
                        ELSE 15 + (b.PlaceOffset - t.n + 1 - 126) * 1.6 END
                   - CASE WHEN b.PlaceOffset - t.n + 1 BETWEEN 168 AND 178 THEN 9 ELSE 0 END
                   + ((ABS(CHECKSUM(NEWID()) % 40)) - 20) / 10.0) / 100.0 AS INT) AS Eggs
    FROM #B b INNER JOIN #N t ON t.n <= 90
    WHERE b.Code = 'B-2604'
) x;

/* ---------------------------------------------------------------------------
   INVENTORY & STOCK LEDGER
   ------------------------------------------------------------------------- */
INSERT INTO dbo.InventoryItems (Name, SKU, Category, Quantity, Unit, MinimumLevel, ExpiryDate, SupplierId, UnitCost) VALUES
('Broiler Starter Crumble',      'FD-BST-01', 'Feed',        4200, 'kg',    2000, DATEADD(DAY, 60, @today),  @sFeed1, 68),
('Broiler Grower Pellet',        'FD-BGR-01', 'Feed',        1500, 'kg',    3000, DATEADD(DAY, 75, @today),  @sFeed1, 64),
('Broiler Finisher Pellet',      'FD-BFN-01', 'Feed',        6800, 'kg',    3000, DATEADD(DAY, 80, @today),  @sFeed2, 62),
('Sonali Grower Mash',           'FD-SGR-01', 'Feed',        2600, 'kg',    1000, DATEADD(DAY, 70, @today),  @sFeed2, 55),
('Layer Phase-1 Mash',           'FD-LY1-01', 'Feed',        9500, 'kg',    4000, DATEADD(DAY, 50, @today),  @sFeed1, 56),
('ND (BCRDV) Vaccine 1000 ds',   'VC-ND-01',  'Vaccine',       14, 'vial',     10, DATEADD(DAY, 20, @today),  @sMed2, 450),
('IBD Intermediate Plus 1000 ds','VC-IBD-01', 'Vaccine',        6, 'vial',     10, DATEADD(DAY, 120, @today), @sMed2, 620),
('Amprolium 20% WSP',            'MD-AMP-01', 'Medicine',      18, 'kg',       5, DATEADD(DAY, 300, @today), @sMed1, 1450),
('Tylosin Tartrate',             'MD-TYL-01', 'Medicine',       3, 'kg',       4, DATEADD(DAY, 200, @today), @sMed1, 3800),
('Vitamin C + Electrolyte',      'SP-VCE-01', 'Supplement',    40, 'kg',      15, DATEADD(DAY, 25, @today),  @sSup,  520),
('Multivitamin (Rena-WS)',       'SP-MVT-01', 'Supplement',    22, 'kg',      10, DATEADD(DAY, 240, @today), @sSup,  780),
('Probiotic (Bacillus subtilis)','SP-PRB-01', 'Supplement',    12, 'kg',       5, DATEADD(DAY, 180, @today), @sSup,  2100),
('Virkon-S Disinfectant',        'BS-VRK-01', 'Biosecurity',    8, 'kg',      10, DATEADD(DAY, 400, @today), @sBio,  3200),
('Lime Powder (footbath)',       'BS-LIM-01', 'Biosecurity',  300, 'kg',     100, NULL,                      @sBio,  18),
('Nipple Drinker Line (3 m)',    'EQ-NPL-01', 'Equipment',     24, 'pcs',     10, NULL,                      @sEq,   1650),
('Exhaust Fan 50 inch',          'EQ-FAN-01', 'Equipment',      3, 'pcs',      2, NULL,                      @sEq,   38500),
('Rice Husk Litter',             'BS-LIT-01', 'Biosecurity', 1800, 'kg',     500, NULL,                      @sBio,  9);

INSERT INTO dbo.InventoryTransactions (ItemId, TxnType, Quantity, Reason, TxnDate, UnitCost, FarmId, Reference, CreatedBy)
SELECT i.Id, CASE WHEN t.n % 3 = 0 THEN 'IN' ELSE 'OUT' END,
       CASE WHEN i.Unit = 'kg' AND i.Category = 'Feed' THEN 500 + (ABS(CHECKSUM(NEWID()) % 20)) * 100
            WHEN i.Unit = 'kg' THEN 1 + ABS(CHECKSUM(NEWID()) % 5)
            ELSE 1 + ABS(CHECKSUM(NEWID()) % 3) END,
       CASE WHEN t.n % 3 = 0 THEN 'Supplier delivery' ELSE 'Issued to shed' END,
       DATEADD(DAY, -t.n * 4, @today), i.UnitCost,
       CASE t.n % 5 WHEN 0 THEN @f1 WHEN 1 THEN @f2 WHEN 2 THEN @f3 WHEN 3 THEN @f4 ELSE @f5 END,
       CONCAT('ST-', FORMAT(DATEADD(DAY, -t.n * 4, @today), 'yyMMdd'), '-', i.Id),
       'store.keeper'
FROM dbo.InventoryItems i
INNER JOIN #N t ON t.n <= 8
WHERE i.Category IN ('Feed', 'Medicine', 'Supplement', 'Vaccine', 'Biosecurity');

/* ---------------------------------------------------------------------------
   PURCHASE ORDERS
   ------------------------------------------------------------------------- */
DECLARE @po INT;
INSERT INTO dbo.PurchaseOrders (PoNumber, SupplierId, FarmId, OrderDate, ExpectedDate, ReceivedDate, Status, PaymentStatus, Notes, CreatedBy)
VALUES ('PO-2601-001', @sFeed1, @f1, DATEADD(DAY, -40, @today), DATEADD(DAY, -36, @today), DATEADD(DAY, -36, @today), 'Received', 'Paid', 'Starter + finisher for B-2601', 'manager');
SET @po = SCOPE_IDENTITY();
INSERT INTO dbo.PurchaseOrderItems (PurchaseOrderId, InventoryItemId, Quantity, UnitCost)
SELECT @po, Id, 8000, UnitCost FROM dbo.InventoryItems WHERE SKU IN ('FD-BST-01', 'FD-BFN-01');

INSERT INTO dbo.PurchaseOrders (PoNumber, SupplierId, FarmId, OrderDate, ExpectedDate, ReceivedDate, Status, PaymentStatus, Notes, CreatedBy)
VALUES ('PO-2601-002', @sMed2, NULL, DATEADD(DAY, -30, @today), DATEADD(DAY, -27, @today), DATEADD(DAY, -25, @today), 'Received', 'Paid', 'Vaccines for next month''s placements', 'vet');
SET @po = SCOPE_IDENTITY();
INSERT INTO dbo.PurchaseOrderItems (PurchaseOrderId, InventoryItemId, Quantity, UnitCost)
SELECT @po, Id, 20, UnitCost FROM dbo.InventoryItems WHERE SKU IN ('VC-ND-01', 'VC-IBD-01');

INSERT INTO dbo.PurchaseOrders (PoNumber, SupplierId, FarmId, OrderDate, ExpectedDate, ReceivedDate, Status, PaymentStatus, Notes, CreatedBy)
VALUES ('PO-2601-003', @sSup, @f5, DATEADD(DAY, -32, @today), DATEADD(DAY, -30, @today), DATEADD(DAY, -30, @today), 'Received', 'Paid', 'Probiotic for trial TR-2026-01', 'researcher');
SET @po = SCOPE_IDENTITY();
INSERT INTO dbo.PurchaseOrderItems (PurchaseOrderId, InventoryItemId, Quantity, UnitCost)
SELECT @po, Id, 25, UnitCost FROM dbo.InventoryItems WHERE SKU = 'SP-PRB-01';

INSERT INTO dbo.PurchaseOrders (PoNumber, SupplierId, FarmId, OrderDate, ExpectedDate, ReceivedDate, Status, PaymentStatus, Notes, CreatedBy)
VALUES ('PO-2601-004', @sFeed2, @f2, DATEADD(DAY, -3, @today), DATEADD(DAY, 2, @today), NULL, 'Ordered', 'Unpaid', 'Grower pellet top-up', 'manager');
SET @po = SCOPE_IDENTITY();
INSERT INTO dbo.PurchaseOrderItems (PurchaseOrderId, InventoryItemId, Quantity, UnitCost)
SELECT @po, Id, 6000, UnitCost FROM dbo.InventoryItems WHERE SKU = 'FD-BGR-01';

INSERT INTO dbo.PurchaseOrders (PoNumber, SupplierId, FarmId, OrderDate, ExpectedDate, ReceivedDate, Status, PaymentStatus, Notes, CreatedBy)
VALUES ('PO-2601-005', @sBio, NULL, @today, DATEADD(DAY, 5, @today), NULL, 'Draft', 'Unpaid', 'Disinfectant restock (below minimum)', 'manager');
SET @po = SCOPE_IDENTITY();
INSERT INTO dbo.PurchaseOrderItems (PurchaseOrderId, InventoryItemId, Quantity, UnitCost)
SELECT @po, Id, 20, UnitCost FROM dbo.InventoryItems WHERE SKU = 'BS-VRK-01';

INSERT INTO dbo.PurchaseOrders (PoNumber, SupplierId, FarmId, OrderDate, ExpectedDate, ReceivedDate, Status, PaymentStatus, Notes, CreatedBy)
VALUES ('PO-2601-006', @sEq, @f1, DATEADD(DAY, -60, @today), DATEADD(DAY, -50, @today), DATEADD(DAY, -46, @today), 'Received', 'Paid', 'Replacement exhaust fans', 'manager');
SET @po = SCOPE_IDENTITY();
INSERT INTO dbo.PurchaseOrderItems (PurchaseOrderId, InventoryItemId, Quantity, UnitCost)
SELECT @po, Id, 2, UnitCost FROM dbo.InventoryItems WHERE SKU = 'EQ-FAN-01';

UPDATE po SET TotalAmount = ISNULL((SELECT SUM(LineTotal) FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId = po.Id), 0)
FROM dbo.PurchaseOrders po;

/* ---------------------------------------------------------------------------
   CUSTOMERS & SALES ORDERS
   ------------------------------------------------------------------------- */
DECLARE @c1 INT, @c2 INT, @c3 INT, @c4 INT, @c5 INT, @c6 INT, @c7 INT, @c8 INT;
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Karwan Bazar Poultry Traders', '01700100001', 'kbpt@example.com', 'Karwan Bazar, Dhaka', 'Wholesaler', 1500000); SET @c1 = SCOPE_IDENTITY();
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Rahman Wholesale', '01700100002', 'rahman.ws@example.com', 'Tongi Bazar, Gazipur', 'Wholesaler', 1000000); SET @c2 = SCOPE_IDENTITY();
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Green Mart Super Shop', '01700100003', 'purchase@greenmart.example', 'Gulshan-2, Dhaka', 'Super Shop', 600000); SET @c3 = SCOPE_IDENTITY();
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Dhaka Kitchen Restaurant', '01700100004', 'dk.rest@example.com', 'Dhanmondi 27, Dhaka', 'Restaurant', 200000); SET @c4 = SCOPE_IDENTITY();
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Uttara Egg Distributors', '01700100005', 'ued@example.com', 'Sector 10, Uttara, Dhaka', 'Distributor', 800000); SET @c5 = SCOPE_IDENTITY();
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Mirpur Meat House', '01700100006', 'mmh@example.com', 'Mirpur-10, Dhaka', 'Retailer', 150000); SET @c6 = SCOPE_IDENTITY();
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Fresh Basket Online', '01700100007', 'sourcing@freshbasket.example', 'Banani, Dhaka', 'E-commerce', 300000); SET @c7 = SCOPE_IDENTITY();
INSERT INTO dbo.Customers (Name, Phone, Email, Address, CustomerType, CreditLimit) VALUES ('Mymensingh Agro Organic', '01700100008', 'mao@example.com', 'Trishal, Mymensingh', 'Retailer', 100000); SET @c8 = SCOPE_IDENTITY();

-- harvested batches -> live bird sales
INSERT INTO dbo.Orders (OrderCode, CustomerId, FarmId, Product, Quantity, Unit, Grade, UnitPrice, OrderDate, DeliveryDate, Status, PaymentStatus, PaidAmount, Notes, BatchId, BirdCount)
SELECT CONCAT('SO-', REPLACE(b.BatchCode, 'B-', '')),
       CASE b.BatchCode WHEN 'B-2510' THEN @c1 WHEN 'B-2511' THEN @c2 WHEN 'B-2512' THEN @c1 ELSE @c2 END,
       b.FarmId,
       CASE WHEN b.BatchType = 'Sonali' THEN 'Live Sonali' ELSE 'Live Broiler' END,
       b.HarvestWeightKg, 'kg', 'A',
       CASE WHEN b.BatchType = 'Sonali' THEN 255 ELSE 158 END,
       DATEADD(DAY, -3, b.ClosedDate), b.ClosedDate, 'Delivered', 'Paid', 0,
       CONCAT('Full-house harvest of ', b.BatchCode), b.Id, b.HarvestedCount
FROM dbo.Batches b
WHERE b.BatchCode IN ('B-2510', 'B-2511', 'B-2512', 'B-2513');

-- weekly egg sales (12 weeks)
INSERT INTO dbo.Orders (OrderCode, CustomerId, FarmId, Product, Quantity, Unit, Grade, UnitPrice, OrderDate, DeliveryDate, Status, PaymentStatus, PaidAmount, Notes, BatchId)
SELECT CONCAT('SO-EGG-', RIGHT(CONCAT('00', t.n), 2)),
       CASE t.n % 3 WHEN 0 THEN @c5 WHEN 1 THEN @c3 ELSE @c7 END,
       @f4, 'Brown Eggs', 18000 + (ABS(CHECKSUM(NEWID()) % 12)) * 1000, 'pcs',
       CASE t.n % 4 WHEN 0 THEN 'B' ELSE 'A' END,
       CAST(10.80 + (ABS(CHECKSUM(NEWID()) % 12)) / 10.0 AS DECIMAL(10,2)),
       DATEADD(DAY, -t.n * 7, @today), DATEADD(DAY, -t.n * 7 + 1, @today),
       CASE WHEN t.n = 1 THEN 'Dispatched' ELSE 'Delivered' END,
       CASE WHEN t.n <= 2 THEN 'Unpaid' WHEN t.n = 3 THEN 'Partial' ELSE 'Paid' END, 0,
       'Weekly egg contract', (SELECT BatchId FROM #B WHERE Code = 'B-2604')
FROM #N t WHERE t.n <= 12;

-- open orders for current flocks and by-products
INSERT INTO dbo.Orders (OrderCode, CustomerId, FarmId, Product, Quantity, Unit, Grade, UnitPrice, OrderDate, DeliveryDate, Status, PaymentStatus, PaidAmount, Notes, BatchId, BirdCount)
SELECT 'SO-2602-A', @c1, @f2, 'Live Broiler', 9800, 'kg', 'A', 162, DATEADD(DAY, -1, @today), DATEADD(DAY, 2, @today), 'Confirmed', 'Partial', 500000, 'Advance received, harvest day 38', BatchId, 4500 FROM #B WHERE Code = 'B-2602'
UNION ALL
SELECT 'SO-2602-B', @c4, @f2, 'Live Broiler', 1200, 'kg', 'A', 170, @today, DATEADD(DAY, 2, @today), 'Pending', 'Unpaid', 0, NULL, BatchId, 550 FROM #B WHERE Code = 'B-2602'
UNION ALL
SELECT 'SO-2603-A', @c6, @f3, 'Live Sonali', 1500, 'kg', 'A', 265, @today, DATEADD(DAY, 14, @today), 'Pending', 'Unpaid', 0, 'Booked for day 60', BatchId, 2200 FROM #B WHERE Code = 'B-2603'
UNION ALL
SELECT 'SO-MAN-01', @c8, @f4, 'Poultry Manure (organic)', 12000, 'kg', 'B', 4, DATEADD(DAY, -20, @today), DATEADD(DAY, -18, @today), 'Delivered', 'Paid', 0, 'Composted layer manure', NULL, NULL
UNION ALL
SELECT 'SO-SPH-01', @c2, @f4, 'Spent Hen', 2600, 'kg', 'B', 210, DATEADD(DAY, -75, @today), DATEADD(DAY, -74, @today), 'Delivered', 'Paid', 0, 'Culled hens from previous flock', NULL, 1400;

UPDATE dbo.Orders SET PaidAmount = TotalAmount WHERE PaymentStatus = 'Paid';
UPDATE dbo.Orders SET PaidAmount = ROUND(TotalAmount * 0.5, 0) WHERE PaymentStatus = 'Partial' AND PaidAmount = 0;

/* ---------------------------------------------------------------------------
   EMPLOYEES, ATTENDANCE, PAYROLL
   ------------------------------------------------------------------------- */
INSERT INTO dbo.Employees (EmployeeCode, FullName, Role, FarmId, Phone, Email, Status, MonthlySalary, JoinDate, Address) VALUES
('EMP-001', 'Abdul Karim',       'Farm Manager',       @f1, '01722000001', 'karim@poultrypro.local',   'On Duty',  45000, DATEADD(MONTH, -40, @today), 'Kaliakair, Gazipur'),
('EMP-002', 'Jahid Hasan',       'Shed Supervisor',    @f1, '01722000002', NULL,                        'On Duty',  24000, DATEADD(MONTH, -26, @today), 'Kaliakair, Gazipur'),
('EMP-003', 'Rubel Mia',         'Shed Worker',        @f1, '01722000003', NULL,                        'On Duty',  14000, DATEADD(MONTH, -14, @today), 'Kaliakair, Gazipur'),
('EMP-004', 'Monir Hossain',     'Farm Manager',       @f2, '01722000004', 'monir@poultrypro.local',   'On Duty',  42000, DATEADD(MONTH, -30, @today), 'Ashulia, Savar'),
('EMP-005', 'Shahin Alam',       'Shed Worker',        @f2, '01722000005', NULL,                        'On Leave', 14000, DATEADD(MONTH, -9, @today),  'Ashulia, Savar'),
('EMP-006', 'Kamal Uddin',       'Shed Worker',        @f2, '01722000006', NULL,                        'On Duty',  14000, DATEADD(MONTH, -12, @today), 'Ashulia, Savar'),
('EMP-007', 'Rina Begum',        'Farm Manager',       @f3, '01722000007', 'rina@poultrypro.local',    'On Duty',  38000, DATEADD(MONTH, -22, @today), 'Tongi, Gazipur'),
('EMP-008', 'Sohel Rana',        'Shed Worker',        @f3, '01722000008', NULL,                        'On Duty',  13500, DATEADD(MONTH, -7, @today),  'Tongi, Gazipur'),
('EMP-009', 'Selim Reza',        'Farm Manager',       @f4, '01722000009', 'selim@poultrypro.local',   'On Duty',  46000, DATEADD(MONTH, -36, @today), 'Rupganj, Narayanganj'),
('EMP-010', 'Nazma Khatun',      'Egg Collector',      @f4, '01722000010', NULL,                        'On Duty',  13000, DATEADD(MONTH, -18, @today), 'Rupganj, Narayanganj'),
('EMP-011', 'Liton Sarkar',      'Egg Grader',         @f4, '01722000011', NULL,                        'On Duty',  15000, DATEADD(MONTH, -15, @today), 'Rupganj, Narayanganj'),
('EMP-012', 'Dr. Mahmudul Hasan','Veterinarian',       NULL,'01722000012', 'vet@poultrypro.local',     'On Duty',  65000, DATEADD(MONTH, -28, @today), 'Uttara, Dhaka'),
('EMP-013', 'Nusrat Jahan',      'Accountant',         NULL,'01722000013', 'accounts@poultrypro.local','On Duty',  40000, DATEADD(MONTH, -20, @today), 'Uttara, Dhaka'),
('EMP-014', 'Habibur Rahman',    'Store Keeper',       NULL,'01722000014', NULL,                        'On Duty',  22000, DATEADD(MONTH, -16, @today), 'Tongi, Gazipur'),
('EMP-015', 'Asif Iqbal',        'Research Assistant', @f5, '01722000015', 'asif@poultrypro.local',    'On Duty',  35000, DATEADD(MONTH, -5, @today),  'Trishal, Mymensingh'),
('EMP-016', 'Jamal Sheikh',      'Driver',             NULL,'01722000016', NULL,                        'Off Duty', 18000, DATEADD(MONTH, -11, @today), 'Gazipur'),
('EMP-017', 'Kalam Mia',         'Security Guard',     @f5, '01722000017', NULL,                        'On Duty',  12500, DATEADD(MONTH, -4, @today),  'Trishal, Mymensingh');

INSERT INTO dbo.Attendance (EmployeeId, AttendanceDate, Status, CheckIn, CheckOut, Notes)
SELECT e.Id, x.D,
       x.St,
       CASE WHEN x.St IN ('Present', 'Late', 'Half Day') THEN CAST(DATEADD(MINUTE, CASE WHEN x.St = 'Late' THEN 510 + x.R % 60 ELSE 465 + x.R % 25 END, CAST('00:00' AS TIME)) AS TIME(0)) ELSE NULL END,
       CASE WHEN x.St IN ('Present', 'Late') THEN CAST(DATEADD(MINUTE, 1020 + x.R % 60, CAST('00:00' AS TIME)) AS TIME(0))
            WHEN x.St = 'Half Day' THEN CAST('13:00' AS TIME(0)) ELSE NULL END,
       NULL
FROM dbo.Employees e
CROSS JOIN #N t
CROSS APPLY (SELECT DATEADD(DAY, -t.n, @today) AS D, ABS(CHECKSUM(NEWID()) % 100) AS R) r0
CROSS APPLY (SELECT r0.D, r0.R,
                    CASE WHEN r0.R < 85 THEN 'Present' WHEN r0.R < 91 THEN 'Late' WHEN r0.R < 95 THEN 'Leave'
                         WHEN r0.R < 97 THEN 'Half Day' ELSE 'Absent' END AS St) x
WHERE t.n <= 45 AND DATEPART(WEEKDAY, x.D) <> ((5 - @@DATEFIRST + 7) % 7 + 1);   -- skip Fridays

INSERT INTO dbo.Payroll (EmployeeId, PeriodYear, PeriodMonth, BasicSalary, Allowances, Overtime, Deductions, DaysPresent, DaysAbsent, Status, PaidDate)
SELECT e.Id, YEAR(p.M), MONTH(p.M), e.MonthlySalary,
       ROUND(e.MonthlySalary * 0.10, 0),
       CASE WHEN e.Role LIKE '%Worker%' THEN 1500 ELSE 0 END,
       ROUND(e.MonthlySalary * 0.02, 0),
       24 + ABS(CHECKSUM(NEWID()) % 3), ABS(CHECKSUM(NEWID()) % 2),
       'Paid', DATEADD(DAY, 4, DATEADD(MONTH, 1, p.M))
FROM dbo.Employees e
CROSS JOIN (SELECT DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1)) AS M
            UNION ALL SELECT DATEADD(MONTH, -2, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1))
            UNION ALL SELECT DATEADD(MONTH, -3, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1))) p
WHERE e.JoinDate < p.M;

/* ---------------------------------------------------------------------------
   FINANCE (12 months)
   ------------------------------------------------------------------------- */
-- sales from delivered orders
INSERT INTO dbo.Transactions (TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference, BatchId, PaymentMethod)
SELECT o.DeliveryDate, 'Sale', o.Product, CONCAT('Sale ', o.OrderCode, ' - ', c.Name), o.TotalAmount,
       CASE o.PaymentStatus WHEN 'Paid' THEN 'Paid' ELSE 'Pending' END, o.FarmId, o.OrderCode, o.BatchId,
       CASE WHEN o.TotalAmount > 300000 THEN 'Bank Transfer' ELSE 'bKash' END
FROM dbo.Orders o INNER JOIN dbo.Customers c ON c.Id = o.CustomerId
WHERE o.Status IN ('Delivered', 'Dispatched');

-- chick purchases at placement
INSERT INTO dbo.Transactions (TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference, BatchId, PaymentMethod)
SELECT b.PlacementDate, 'Purchase', 'Day-old Chicks', CONCAT(b.InitialCount, ' DOC for ', b.BatchCode, ' (', b.Breed, ')'),
       -(b.InitialCount * b.ChickCostPerBird), 'Paid', b.FarmId, b.BatchCode, b.Id, 'Bank Transfer'
FROM dbo.Batches b WHERE b.PlacementDate >= DATEADD(MONTH, -12, @today);

-- feed cost per farm per month (from daily records)
INSERT INTO dbo.Transactions (TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference, PaymentMethod)
SELECT EOMONTH(MIN(dr.RecordDate)), 'Purchase', 'Feed',
       CONCAT('Feed consumed - ', f.Name, ' - ', FORMAT(MIN(dr.RecordDate), 'MMM yyyy')),
       -ROUND(SUM(dr.FeedConsumptionKg) * 63, 0), 'Paid', f.Id, CONCAT('FEED-', FORMAT(MIN(dr.RecordDate), 'yyMM'), '-', f.Id), 'Bank Transfer'
FROM dbo.DailyFarmRecords dr INNER JOIN dbo.Farms f ON f.Id = dr.FarmId
GROUP BY f.Id, f.Name, YEAR(dr.RecordDate), MONTH(dr.RecordDate);
UPDATE dbo.Transactions SET TxnDate = @today WHERE Category = 'Feed' AND TxnDate > @today;

-- monthly overheads for the last 12 months
INSERT INTO dbo.Transactions (TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference, PaymentMethod)
SELECT DATEADD(DAY, 4, m.M), 'Salary', 'Salary', CONCAT('Payroll ', FORMAT(m.M, 'MMM yyyy')),
       -(SELECT SUM(MonthlySalary) FROM dbo.Employees), 'Paid', NULL, CONCAT('PAY-', FORMAT(m.M, 'yyMM')), 'Bank Transfer'
FROM (SELECT DATEADD(MONTH, -t.n, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1)) AS M FROM #N t WHERE t.n <= 12) m
UNION ALL
SELECT DATEADD(DAY, 10, m.M), 'Expense', 'Utilities', CONCAT('Electricity & fuel ', FORMAT(m.M, 'MMM yyyy')),
       -(55000 + ABS(CHECKSUM(NEWID()) % 25000)), 'Paid', NULL, CONCAT('UTL-', FORMAT(m.M, 'yyMM')), 'Bank Transfer'
FROM (SELECT DATEADD(MONTH, -t.n, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1)) AS M FROM #N t WHERE t.n <= 12) m
UNION ALL
SELECT DATEADD(DAY, 14, m.M), 'Purchase', 'Medicine & Vaccine', CONCAT('Veterinary supplies ', FORMAT(m.M, 'MMM yyyy')),
       -(30000 + ABS(CHECKSUM(NEWID()) % 30000)), 'Paid', NULL, CONCAT('VET-', FORMAT(m.M, 'yyMM')), 'Cash'
FROM (SELECT DATEADD(MONTH, -t.n, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1)) AS M FROM #N t WHERE t.n <= 12) m
UNION ALL
-- historical flock revenue (before the detailed batch history in this data set)
SELECT DATEADD(DAY, 20, m.M), 'Sale', 'Live Broiler', CONCAT('Live broiler sales ', FORMAT(m.M, 'MMM yyyy'), ' (historical flocks)'),
       1800000 + ABS(CHECKSUM(NEWID()) % 900000), 'Paid', NULL, CONCAT('HIST-', FORMAT(m.M, 'yyMM')), 'Bank Transfer'
FROM (SELECT DATEADD(MONTH, -t.n, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1)) AS M FROM #N t WHERE t.n BETWEEN 6 AND 12) m
UNION ALL
SELECT DATEADD(DAY, 21, m.M), 'Purchase', 'Feed', CONCAT('Feed purchases ', FORMAT(m.M, 'MMM yyyy'), ' (historical flocks)'),
       -(1150000 + ABS(CHECKSUM(NEWID()) % 300000)), 'Paid', NULL, CONCAT('HISTF-', FORMAT(m.M, 'yyMM')), 'Bank Transfer'
FROM (SELECT DATEADD(MONTH, -t.n, DATEFROMPARTS(YEAR(@today), MONTH(@today), 1)) AS M FROM #N t WHERE t.n BETWEEN 6 AND 12) m;

-- current month overheads (partial)
INSERT INTO dbo.Transactions (TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference, PaymentMethod) VALUES
(DATEADD(DAY, -2, @today), 'Expense', 'Maintenance', 'Exhaust fan motor rewinding - Gazipur', -14500, 'Paid', @f1, 'MNT-001', 'Cash'),
(DATEADD(DAY, -1, @today), 'Expense', 'Transport',   'Feed transport Savar',                  -9800,  'Paid', @f2, 'TRN-014', 'Cash'),
(@today,                    'Expense', 'Utilities',   'Generator diesel',                      -12600, 'Pending', @f5, 'UTL-DSL', 'Cash');

INSERT INTO dbo.Budgets ([Year], [Month], Category, BudgetType, PlannedAmount, FarmId, Notes)
SELECT YEAR(@today), MONTH(@today), x.Category, x.BType, x.Amount, NULL, 'Approved monthly budget'
FROM (VALUES ('Feed', 'Expense', 3200000), ('Day-old Chicks', 'Expense', 900000), ('Salary', 'Expense', 520000),
             ('Utilities', 'Expense', 80000), ('Medicine & Vaccine', 'Expense', 60000), ('Maintenance', 'Expense', 40000),
             ('Live Broiler', 'Revenue', 4200000), ('Brown Eggs', 'Revenue', 900000), ('Live Sonali', 'Revenue', 600000)) x(Category, BType, Amount);

/* ---------------------------------------------------------------------------
   CERTIFICATIONS, BIOSECURITY AUDITS, TASKS
   ------------------------------------------------------------------------- */
INSERT INTO dbo.Certifications (FarmId, CertName, IssuedDate, ExpiryDate, Status, IssuingAuthority, CertificateNo)
SELECT f.Id, x.CertName, DATEADD(MONTH, -12 + x.Shift, f.CertExpiry), DATEADD(MONTH, x.Shift, f.CertExpiry),
       CASE WHEN DATEADD(MONTH, x.Shift, f.CertExpiry) < @today THEN 'Expired'
            WHEN DATEADD(MONTH, x.Shift, f.CertExpiry) <= DATEADD(DAY, 30, @today) THEN 'Expiring Soon' ELSE 'Valid' END,
       x.Authority, CONCAT(x.Prefix, '-', f.Id, '-', YEAR(@today))
FROM dbo.Farms f
CROSS JOIN (VALUES ('DLS Farm Registration', 0, 'Department of Livestock Services', 'DLS'),
                   ('Biosecurity Compliance Certificate', 3, 'District Livestock Office', 'BIO'),
                   ('Trade Licence', 6, 'City / Union Parishad', 'TL')) x(CertName, Shift, Authority, Prefix);

IF OBJECT_ID('tempdb..#Q') IS NOT NULL DROP TABLE #Q;
CREATE TABLE #Q (Category NVARCHAR(80) COLLATE DATABASE_DEFAULT, Question NVARCHAR(300) COLLATE DATABASE_DEFAULT, Weight INT);
INSERT INTO #Q VALUES
('Perimeter & Access', 'Farm has a fence and a single controlled entry gate', 2),
('Perimeter & Access', 'Visitor log book is maintained and signed', 1),
('Perimeter & Access', 'Vehicle disinfection at entry (spray / wheel dip)', 2),
('Personnel Hygiene',  'Footbath with active disinfectant at every shed door', 2),
('Personnel Hygiene',  'Dedicated boots and clothing used inside sheds', 2),
('Personnel Hygiene',  'Hand-wash station with soap available', 1),
('Flock Management',   'All-in all-out placement with minimum 14 days downtime', 3),
('Flock Management',   'Dead birds removed at least twice daily and disposed in pit/incinerator', 3),
('Flock Management',   'No other poultry, ducks or pet birds on the premises', 2),
('Feed & Water',       'Feed stored off the floor in a rodent-proof store', 2),
('Feed & Water',       'Drinking water tested or chlorinated', 2),
('Pest Control',       'Rodent bait stations maintained and logged', 2),
('Pest Control',       'Wild-bird proof netting on all openings', 2),
('Records',            'Vaccination and medication records up to date', 2);

DECLARE @a INT;
DECLARE @fid INT, @k INT = 0;
DECLARE farm_cur CURSOR LOCAL FAST_FORWARD FOR SELECT Id FROM dbo.Farms ORDER BY Id;
OPEN farm_cur;
FETCH NEXT FROM farm_cur INTO @fid;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @k = @k + 1;
    -- previous audit (90 days ago) and latest audit (10-20 days ago)
    INSERT INTO dbo.BiosecurityAudits (FarmId, AuditDate, AuditorName, Notes)
    VALUES (@fid, DATEADD(DAY, -90 - @k, @today), 'Dr. Mahmudul Hasan', 'Quarterly biosecurity audit');
    SET @a = SCOPE_IDENTITY();
    INSERT INTO dbo.BiosecurityAuditItems (AuditId, Category, Question, Weight, Passed, Remark)
    SELECT @a, Category, Question, Weight, CASE WHEN ABS(CHECKSUM(NEWID()) % 100) < 70 THEN 1 ELSE 0 END, NULL FROM #Q;

    INSERT INTO dbo.BiosecurityAudits (FarmId, AuditDate, AuditorName, Notes)
    VALUES (@fid, DATEADD(DAY, -10 - @k * 2, @today), 'Dr. Mahmudul Hasan', 'Follow-up audit after corrective actions');
    SET @a = SCOPE_IDENTITY();
    INSERT INTO dbo.BiosecurityAuditItems (AuditId, Category, Question, Weight, Passed, Remark)
    SELECT @a, Category, Question, Weight,
           CASE WHEN ABS(CHECKSUM(NEWID()) % 100) < (CASE WHEN @fid = @f2 THEN 65 ELSE 85 END) THEN 1 ELSE 0 END, NULL FROM #Q;

    FETCH NEXT FROM farm_cur INTO @fid;
END
CLOSE farm_cur;
DEALLOCATE farm_cur;

UPDATE a SET Score = x.S, MaxScore = x.M,
             Grade = CASE WHEN x.S * 100.0 / x.M >= 90 THEN 'A' WHEN x.S * 100.0 / x.M >= 75 THEN 'B'
                          WHEN x.S * 100.0 / x.M >= 60 THEN 'C' ELSE 'D' END
FROM dbo.BiosecurityAudits a
CROSS APPLY (SELECT SUM(CASE WHEN Passed = 1 THEN Weight ELSE 0 END) AS S, SUM(Weight) AS M
             FROM dbo.BiosecurityAuditItems WHERE AuditId = a.Id) x;

INSERT INTO dbo.FarmTasks (Title, Description, FarmId, BatchId, AssignedToEmployeeId, Category, Priority, Status, DueDate, CompletedAt, CreatedBy)
SELECT 'Administer ND booster to B-2601', 'Overdue Lasota booster via drinking water. Withhold water 2 h.', @f1, (SELECT BatchId FROM #B WHERE Code = 'B-2601'),
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-002'), 'Vaccination', 'Urgent', 'Open', @today, NULL, 'vet'
UNION ALL SELECT 'Arrange harvest crew for B-2602', 'Catching crew + 3 trucks for 7,000 birds.', @f2, (SELECT BatchId FROM #B WHERE Code = 'B-2602'),
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-004'), 'Sales', 'High', 'In Progress', DATEADD(DAY, 2, @today), NULL, 'manager'
UNION ALL SELECT 'Replace footbath disinfectant', 'Virkon-S below minimum - use lime until PO arrives.', @f2, NULL,
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-006'), 'Cleaning', 'Medium', 'Open', DATEADD(DAY, 1, @today), NULL, 'manager'
UNION ALL SELECT 'Fowl pox vaccination B-2603', 'Wing-web vaccination is overdue.', @f3, (SELECT BatchId FROM #B WHERE Code = 'B-2603'),
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-007'), 'Vaccination', 'High', 'Open', @today, NULL, 'vet'
UNION ALL SELECT 'Weekly weight sampling - trial sheds', 'Sample 100 birds per shed, record uniformity.', @f5, (SELECT BatchId FROM #B WHERE Code = 'B-2605'),
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-015'), 'General', 'High', 'Open', DATEADD(DAY, 1, @today), NULL, 'researcher'
UNION ALL SELECT 'Service generator', 'Oil change and battery check.', @f5, NULL,
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-017'), 'Maintenance', 'Low', 'Open', DATEADD(DAY, 6, @today), NULL, 'manager'
UNION ALL SELECT 'Egg grading machine calibration', NULL, @f4, NULL,
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-011'), 'Maintenance', 'Medium', 'Done', DATEADD(DAY, -3, @today), DATEADD(DAY, -3, GETDATE()), 'manager'
UNION ALL SELECT 'Clean & disinfect shed S-2 (Gazipur)', 'Downtime after B-2512 harvest.', @f1, NULL,
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-003'), 'Cleaning', 'Medium', 'Done', DATEADD(DAY, -50, @today), DATEADD(DAY, -52, GETDATE()), 'manager'
UNION ALL SELECT 'Submit DLS renewal - Savar', 'Registration expiring within 14 days.', @f2, NULL,
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-013'), 'General', 'High', 'In Progress', DATEADD(DAY, 7, @today), NULL, 'admin'
UNION ALL SELECT 'Install ammonia sensor - layer shed L-2', 'Second climate node for the layer unit.', @f4, NULL,
       (SELECT Id FROM dbo.Employees WHERE EmployeeCode = 'EMP-009'), 'Maintenance', 'Medium', 'Open', DATEADD(DAY, 10, @today), NULL, 'admin';

/* ---------------------------------------------------------------------------
   FIELD TRIALS (research module)
   ------------------------------------------------------------------------- */
INSERT INTO dbo.FieldTrials (TrialCode, Title, Objective, Hypothesis, Intervention, TreatmentBatchId, ControlBatchId, PrimaryMetric, StartDate, EndDate, Status, Conclusion, PrincipalInvestigator)
SELECT 'TR-2026-01', 'Effect of Bacillus subtilis probiotic on broiler FCR',
       'Quantify the effect of a water-administered Bacillus subtilis probiotic on feed conversion, growth and livability of Cobb 500 broilers under Bangladesh summer conditions.',
       'H1: The probiotic-treated flock achieves at least 4% lower FCR than the control flock at day 35.',
       '1 g / 4 L drinking water from day 1 to day 35 in shed S-1; shed S-2 receives the standard programme only.',
       t.BatchId, c.BatchId, 'FCR', t.Placement, DATEADD(DAY, 35, t.Placement), 'Running', NULL, 'Dr. Mahmudul Hasan'
FROM #B t CROSS JOIN #B c WHERE t.Code = 'B-2605' AND c.Code = 'B-2606'
UNION ALL
SELECT 'TR-2025-04', 'Tunnel ventilation vs. conventional curtain housing',
       'Compare production efficiency (EPEF) of two consecutive Cobb 500 flocks after the tunnel-ventilation retrofit.',
       'H1: The retrofitted house improves EPEF by at least 20 points.',
       'Tunnel ventilation + cooling pads installed before B-2512; B-2510 reared under the old curtain system.',
       t.BatchId, c.BatchId, 'EPEF', c.Placement, t.ClosedDate, 'Completed',
       'B-2512 achieved better livability and FCR than B-2510 with the same breed and hatchery. Result supports adoption of tunnel ventilation in the remaining houses.',
       'Asif Iqbal'
FROM (SELECT b.*, DATEADD(DAY, b.CloseAge, b.Placement) AS ClosedDate FROM #B b WHERE b.Code = 'B-2512') t
CROSS JOIN #B c WHERE c.Code = 'B-2510';

/* ---------------------------------------------------------------------------
   IoT SENSOR NETWORK (7 days of 30-minute telemetry)
   ------------------------------------------------------------------------- */
INSERT INTO dbo.SensorDevices (DeviceCode, Name, FarmId, ShedNo, DeviceType, ApiKey, FirmwareVersion, IsActive)
SELECT x.Code, x.Name, x.FarmId, x.Shed, 'ESP32 Climate Node (DHT22 + MQ-137 + MH-Z19)',
       LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONCAT(x.Code, CAST(NEWID() AS VARCHAR(36)))), 2)), 'v1.4.2', 1
FROM (VALUES ('PP-NODE-001', 'Gazipur S-1 climate node', @f1, 'S-1'),
             ('PP-NODE-002', 'Savar S-1 climate node',   @f2, 'S-1'),
             ('PP-NODE-003', 'Tongi S-1 climate node',   @f3, 'S-1'),
             ('PP-NODE-004', 'Layer L-1 climate node',   @f4, 'L-1'),
             ('PP-NODE-005', 'Research S-1 (treatment)', @f5, 'S-1'),
             ('PP-NODE-006', 'Research S-2 (control)',   @f5, 'S-2')) x(Code, Name, FarmId, Shed);

IF OBJECT_ID('tempdb..#H') IS NOT NULL DROP TABLE #H;
SELECT TOP (336) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS h INTO #H FROM sys.all_objects;

DECLARE @now DATETIME = DATEADD(MINUTE, -(DATEPART(MINUTE, GETDATE()) % 30), DATEADD(SECOND, -DATEPART(SECOND, GETDATE()), GETDATE()));
INSERT INTO dbo.SensorReadings (DeviceId, FarmId, ReadingTime, TemperatureC, HumidityPercent, AmmoniaPpm, Co2Ppm, LightLux, WaterFlowLtr, FeedLevelPct)
SELECT d.Id, d.FarmId, r.T,
       CAST(26.0 + 3.5 * SIN(2 * PI() * (DATEPART(HOUR, r.T) - 9) / 24.0)
            + (CASE WHEN d.DeviceCode = 'PP-NODE-002' AND r.T > DATEADD(HOUR, -30, @now) THEN 3.5 ELSE 0 END)
            + ((ABS(CHECKSUM(NEWID()) % 20)) - 10) / 10.0 AS DECIMAL(5,2)),
       CAST(70 - 12 * SIN(2 * PI() * (DATEPART(HOUR, r.T) - 9) / 24.0) + ((ABS(CHECKSUM(NEWID()) % 60)) - 30) / 10.0 AS DECIMAL(5,2)),
       CAST(CASE WHEN d.DeviceCode = 'PP-NODE-004' THEN (CASE WHEN r.T > DATEADD(HOUR, -36, @now) THEN 25 ELSE 15 END)
                 ELSE 8 + ABS(CHECKSUM(NEWID()) % 8) END
            + (ABS(CHECKSUM(NEWID()) % 30)) / 10.0 AS DECIMAL(6,2)),
       CAST(900 + ABS(CHECKSUM(NEWID()) % 900) + (CASE WHEN DATEPART(HOUR, r.T) BETWEEN 0 AND 5 THEN 600 ELSE 0 END) AS DECIMAL(8,2)),
       CAST(CASE WHEN DATEPART(HOUR, r.T) BETWEEN 6 AND 18 THEN 45 + ABS(CHECKSUM(NEWID()) % 25) ELSE 12 + ABS(CHECKSUM(NEWID()) % 6) END AS DECIMAL(8,2)),
       CAST(35 + ABS(CHECKSUM(NEWID()) % 25) + (CASE WHEN DATEPART(HOUR, r.T) BETWEEN 11 AND 16 THEN 20 ELSE 0 END) AS DECIMAL(8,2)),
       CAST(100 - (h.h % 48) * 1.8 AS DECIMAL(5,2))
FROM dbo.SensorDevices d
CROSS JOIN #H h
CROSS APPLY (SELECT DATEADD(MINUTE, -30 * h.h, @now) AS T) r
WHERE NOT (d.DeviceCode = 'PP-NODE-003' AND h.h < 96);     -- node 003 went offline 2 days ago

UPDATE d SET LastSeenAt = (SELECT MAX(ReadingTime) FROM dbo.SensorReadings r WHERE r.DeviceId = d.Id)
FROM dbo.SensorDevices d;

/* ---------------------------------------------------------------------------
   FINAL TOUCHES
   ------------------------------------------------------------------------- */
UPDATE f SET CurrentStock  = ISNULL((SELECT SUM(CurrentCount) FROM dbo.Batches b WHERE b.FarmId = f.Id AND b.IsActive = 1), 0),
             EmployeeCount = (SELECT COUNT(*) FROM dbo.Employees e WHERE e.FarmId = f.Id AND e.IsActive = 1)
FROM dbo.Farms f;

INSERT INTO dbo.AuditLogs (UserName, Action, Entity, EntityId, Details, IpAddress)
VALUES ('system', 'Seed', 'Database', NULL, 'Demonstration data set generated by 04_DemoData.sql', '127.0.0.1');

INSERT INTO dbo.SchemaVersions (ScriptName) VALUES ('04_DemoData.sql');

COMMIT TRANSACTION;
PRINT 'Demo data generated successfully.';
GO

SET NOEXEC OFF;
GO
