-- ============================================================================
-- PoultryPro — STEP 2 : Views & stored procedures (CREATE OR ALTER, re-runnable)
-- Requires SQL Server 2016 SP1 or newer.
-- ============================================================================
USE PoultryProDB;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Dashboard summary
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_DashboardSummary AS
SELECT
    ISNULL((SELECT SUM(CurrentCount) FROM dbo.Batches WHERE IsActive = 1), 0)            AS TotalBirds,
    (SELECT COUNT(*) FROM dbo.Batches WHERE IsActive = 1)                                AS ActiveBatches,
    (SELECT COUNT(*) FROM dbo.Farms   WHERE IsActive = 1)                                AS TotalFarms,
    (SELECT COUNT(*) FROM dbo.Employees WHERE IsActive = 1 AND Status = 'On Duty')       AS StaffOnDuty,
    (SELECT COUNT(*) FROM dbo.VaccinationRecords
       WHERE CompletedDate IS NULL AND (Status = 'Overdue' OR DueDate < CAST(GETDATE() AS DATE))) AS OverdueVaccinations,
    (SELECT COUNT(*) FROM dbo.InventoryItems WHERE Quantity <= MinimumLevel)             AS LowStockCount,
    ISNULL((SELECT SUM(Amount) FROM dbo.Transactions
       WHERE TxnDate >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1) AND Amount > 0), 0)      AS MonthlyRevenue,
    ISNULL((SELECT SUM(ABS(Amount)) FROM dbo.Transactions
       WHERE TxnDate >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1) AND Amount < 0), 0)      AS MonthlyExpenses;
GO

-- ---------------------------------------------------------------------------
-- Batch details + production KPIs (FCR, livability, EPEF)
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_BatchDetails AS
SELECT
    b.Id, b.BatchCode, b.Breed, b.BatchType, b.FarmId,
    f.Name      AS FarmName,
    f.Location  AS FarmLocation,
    f.FarmType,
    b.ShedNo, b.HatcheryName, b.ChickCostPerBird,
    b.InitialCount, b.CurrentCount, b.MortalityCount, b.CullingCount,
    b.HarvestedCount, b.HarvestWeightKg,
    b.PlacementDate, b.ExpectedHarvestDate, b.ClosedDate,
    DATEDIFF(DAY, b.PlacementDate, ISNULL(b.ClosedDate, CAST(GETDATE() AS DATE))) AS AgeInDays,
    b.HealthStatus, b.AverageWeightKg, b.TotalFeedKg, b.MortalityRate,
    b.IsActive, b.Notes,
    CAST(CASE WHEN b.InitialCount > 0
              THEN CAST(b.InitialCount - b.MortalityCount - b.CullingCount AS DECIMAL(12,4)) / b.InitialCount * 100
              ELSE 0 END AS DECIMAL(6,2)) AS LivabilityPct,
    k.LiveMassKg,
    CAST(CASE WHEN k.LiveMassKg > 0 THEN b.TotalFeedKg / k.LiveMassKg ELSE 0 END AS DECIMAL(10,3)) AS FCR,
    (SELECT COUNT(*)        FROM dbo.FarmVisits v WHERE v.BatchId = b.Id) AS TotalVisits,
    (SELECT MAX(v.VisitDate) FROM dbo.FarmVisits v WHERE v.BatchId = b.Id) AS LastVisitDate,
    b.TraceSealHash, b.TraceSealedAt, b.TraceSealedBy
FROM dbo.Batches b
INNER JOIN dbo.Farms f ON b.FarmId = f.Id
CROSS APPLY (SELECT CAST(CASE WHEN b.HarvestWeightKg > 0 THEN b.HarvestWeightKg
                              ELSE b.CurrentCount * b.AverageWeightKg END AS DECIMAL(14,3)) AS LiveMassKg) k;
GO

-- European Production Efficiency Factor per batch
--   EPEF = Livability(%) x LiveWeight(kg) x 100 / (Age(days) x FCR)
CREATE OR ALTER VIEW dbo.vw_BatchPerformance AS
SELECT d.*,
    CAST(CASE WHEN d.AgeInDays > 0 AND d.FCR > 0
              THEN d.LivabilityPct * d.AverageWeightKg * 100 / (d.AgeInDays * d.FCR)
              ELSE 0 END AS DECIMAL(12,1)) AS EPEF,
    CAST(CASE WHEN d.AgeInDays > 0 THEN d.AverageWeightKg * 1000 / d.AgeInDays ELSE 0 END AS DECIMAL(10,1)) AS AvgDailyGainGm,
    CAST(CASE WHEN d.CurrentCount > 0 AND d.AgeInDays > 0
              THEN d.TotalFeedKg * 1000 / NULLIF((d.InitialCount + d.CurrentCount) / 2.0, 0) / d.AgeInDays
              ELSE 0 END AS DECIMAL(12,1)) AS AvgFeedPerBirdDayGm
FROM dbo.vw_BatchDetails d;
GO

-- ---------------------------------------------------------------------------
-- Inventory
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_InventoryStatus AS
SELECT
    i.Id, i.Name, i.SKU, i.Category, i.Quantity, i.Unit,
    i.MinimumLevel, i.ExpiryDate, i.UnitCost, i.SupplierId,
    CAST(i.Quantity * i.UnitCost AS DECIMAL(14,2)) AS TotalValue,
    s.Name AS SupplierName,
    CASE WHEN i.Quantity <= 0              THEN 'Out of Stock'
         WHEN i.Quantity <= i.MinimumLevel THEN 'Low Stock'
         ELSE 'In Stock' END AS StockStatus,
    CAST(CASE WHEN i.ExpiryDate IS NOT NULL AND i.ExpiryDate <= DATEADD(DAY, 30, GETDATE()) THEN 1 ELSE 0 END AS BIT) AS IsExpiringSoon
FROM dbo.InventoryItems i
LEFT JOIN dbo.Suppliers s ON i.SupplierId = s.Id;
GO

-- ---------------------------------------------------------------------------
-- Finance
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_MonthlyFinancials AS
SELECT
    YEAR(TxnDate)            AS [Year],
    MONTH(TxnDate)           AS [Month],
    DATENAME(MONTH, TxnDate) AS MonthName,
    SUM(CASE WHEN Amount > 0 THEN Amount ELSE 0 END)      AS Revenue,
    SUM(CASE WHEN Amount < 0 THEN ABS(Amount) ELSE 0 END) AS Expenses,
    SUM(Amount)                                           AS NetProfit
FROM dbo.Transactions
GROUP BY YEAR(TxnDate), MONTH(TxnDate), DATENAME(MONTH, TxnDate);
GO

CREATE OR ALTER VIEW dbo.vw_OverdueVaccinations AS
SELECT
    v.Id, v.VaccineName, v.DueDate,
    DATEDIFF(DAY, v.DueDate, GETDATE()) AS DaysOverdue,
    b.Id AS BatchId, b.BatchCode, b.Breed, b.CurrentCount, b.FarmId,
    f.Name AS FarmName
FROM dbo.VaccinationRecords v
INNER JOIN dbo.Batches b ON v.BatchId = b.Id
INNER JOIN dbo.Farms   f ON b.FarmId  = f.Id
WHERE v.CompletedDate IS NULL AND b.IsActive = 1
  AND (v.Status = 'Overdue' OR v.DueDate < CAST(GETDATE() AS DATE));
GO

-- ---------------------------------------------------------------------------
-- Field operations
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_FarmVisitDetails AS
SELECT
    v.Id, v.FieldOfficerId, fo.FullName AS FieldOfficerName,
    v.FarmId, f.Name AS FarmName, f.Location AS FarmLocation,
    v.BatchId, b.BatchCode, b.Breed,
    CASE WHEN b.Id IS NULL THEN NULL ELSE DATEDIFF(DAY, b.PlacementDate, v.VisitDate) END AS AgeInDays,
    v.VisitDate, v.VisitPurpose, v.VisitStatus,
    v.CheckInTime, v.CheckOutTime, v.CheckInLatitude, v.CheckInLongitude, v.GpsAccuracyMeters,
    v.OverallRemark, v.SyncStatus, v.SyncedAt, v.CreatedAt,
    (SELECT COUNT(*) FROM dbo.DailyFarmRecords d WHERE d.VisitId = v.Id) AS DailyRecordCount,
    (SELECT COUNT(*) FROM dbo.VisitPhotos      p WHERE p.VisitId = v.Id) AS PhotoCount,
    (SELECT COUNT(*) FROM dbo.FarmerSignatures s WHERE s.VisitId = v.Id) AS SignatureCount,
    f.Latitude AS FarmLatitude, f.Longitude AS FarmLongitude
FROM dbo.FarmVisits v
INNER JOIN dbo.FieldOfficers fo ON v.FieldOfficerId = fo.Id
INNER JOIN dbo.Farms f          ON v.FarmId = f.Id
LEFT  JOIN dbo.Batches b        ON v.BatchId = b.Id;
GO

CREATE OR ALTER VIEW dbo.vw_DailyRecordSummary AS
SELECT
    dr.Id, dr.VisitId, dr.BatchId, dr.FarmId, dr.RecordDate,
    dr.BirdCount, dr.MortalityCount, dr.CullingCount,
    dr.FeedConsumptionKg, dr.WaterConsumptionLtr,
    dr.TemperatureC, dr.HumidityPercent, dr.LightHours, dr.AverageWeightGm,
    dr.MedicineUsed, dr.MedicineDosage, dr.VaccineUsed, dr.Remarks,
    dr.SyncStatus, dr.SyncedAt, dr.CreatedAt,
    b.BatchCode, b.Breed,
    DATEDIFF(DAY, b.PlacementDate, dr.RecordDate) AS AgeInDays,
    f.Name AS FarmName,
    ISNULL(fo.FullName, 'Farm Manager') AS FieldOfficerName,
    CAST(CASE WHEN dr.BirdCount > 0 THEN dr.FeedConsumptionKg * 1000 / dr.BirdCount ELSE 0 END AS DECIMAL(10,2)) AS FeedPerBird
FROM dbo.DailyFarmRecords dr
INNER JOIN dbo.Batches b      ON dr.BatchId = b.Id
INNER JOIN dbo.Farms f        ON dr.FarmId  = f.Id
LEFT  JOIN dbo.FarmVisits v   ON dr.VisitId = v.Id
LEFT  JOIN dbo.FieldOfficers fo ON v.FieldOfficerId = fo.Id;
GO

CREATE OR ALTER VIEW dbo.vw_FieldOfficerPendingSync AS
SELECT
    fo.Id AS FieldOfficerId, fo.FullName,
    (SELECT COUNT(*) FROM dbo.FarmVisits v WHERE v.FieldOfficerId = fo.Id AND v.SyncStatus = 'Pending') AS PendingVisits,
    (SELECT COUNT(*) FROM dbo.DailyFarmRecords d INNER JOIN dbo.FarmVisits v ON d.VisitId = v.Id
        WHERE v.FieldOfficerId = fo.Id AND d.SyncStatus = 'Pending') AS PendingDailyRecords,
    (SELECT COUNT(*) FROM dbo.VisitPhotos p INNER JOIN dbo.FarmVisits v ON p.VisitId = v.Id
        WHERE v.FieldOfficerId = fo.Id AND p.SyncStatus = 'Pending') AS PendingPhotos,
    (SELECT COUNT(*) FROM dbo.FarmerSignatures s INNER JOIN dbo.FarmVisits v ON s.VisitId = v.Id
        WHERE v.FieldOfficerId = fo.Id AND s.SyncStatus = 'Pending') AS PendingSignatures
FROM dbo.FieldOfficers fo
WHERE fo.IsActive = 1;
GO

-- ---------------------------------------------------------------------------
-- Layers
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_EggProductionSummary AS
SELECT
    e.Id, e.BatchId, b.BatchCode, b.Breed, b.FarmId, f.Name AS FarmName,
    e.RecordDate, DATEDIFF(DAY, b.PlacementDate, e.RecordDate) / 7 AS AgeWeeks,
    e.HenCount, e.TotalEggs, e.GradeA, e.GradeB, e.CrackedEggs, e.DirtyEggs,
    e.AvgEggWeightGm, e.FeedKg, e.HenDayPct, e.Notes,
    CAST(CASE WHEN e.TotalEggs > 0 AND e.FeedKg IS NOT NULL
              THEN e.FeedKg * 1000 / (e.TotalEggs / 12.0) ELSE 0 END AS DECIMAL(12,1)) AS FeedPerDozenGm,
    CAST(CASE WHEN e.TotalEggs > 0 THEN CAST(e.CrackedEggs AS DECIMAL(10,4)) / e.TotalEggs * 100 ELSE 0 END AS DECIMAL(5,2)) AS CrackedPct
FROM dbo.EggProductionRecords e
INNER JOIN dbo.Batches b ON e.BatchId = b.Id
INNER JOIN dbo.Farms   f ON b.FarmId  = f.Id;
GO

-- ---------------------------------------------------------------------------
-- Farm benchmark (one row per farm)
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_FarmBenchmark AS
SELECT
    f.Id AS FarmId, f.Name AS FarmName, f.Location, f.FarmType, f.Capacity,
    COUNT(p.Id)                                       AS TotalBatches,
    SUM(CASE WHEN p.IsActive = 1 THEN 1 ELSE 0 END)   AS ActiveBatches,
    ISNULL(SUM(CASE WHEN p.IsActive = 1 THEN p.CurrentCount ELSE 0 END), 0) AS CurrentBirds,
    CAST(ISNULL(AVG(p.MortalityRate), 0) AS DECIMAL(6,2))  AS AvgMortalityRate,
    CAST(ISNULL(AVG(NULLIF(p.FCR, 0)), 0) AS DECIMAL(10,3)) AS AvgFCR,
    CAST(ISNULL(AVG(NULLIF(p.EPEF, 0)), 0) AS DECIMAL(12,1)) AS AvgEPEF,
    (SELECT TOP 1 a.ScorePct FROM dbo.BiosecurityAudits a WHERE a.FarmId = f.Id ORDER BY a.AuditDate DESC) AS LastBiosecurityPct
FROM dbo.Farms f
LEFT JOIN dbo.vw_BatchPerformance p ON p.FarmId = f.Id
WHERE f.IsActive = 1
GROUP BY f.Id, f.Name, f.Location, f.FarmType, f.Capacity;
GO

-- ---------------------------------------------------------------------------
-- Supplier performance
-- ---------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_SupplierPerformance AS
SELECT
    s.Id, s.Name, s.Category, s.Rating,
    COUNT(po.Id) AS TotalOrders,
    ISNULL(SUM(CASE WHEN po.Status <> 'Cancelled' THEN po.TotalAmount ELSE 0 END), 0) AS TotalSpend,
    SUM(CASE WHEN po.Status = 'Received' THEN 1 ELSE 0 END) AS ReceivedOrders,
    SUM(CASE WHEN po.Status = 'Received' AND po.ExpectedDate IS NOT NULL AND po.ReceivedDate <= po.ExpectedDate THEN 1 ELSE 0 END) AS OnTimeOrders
FROM dbo.Suppliers s
LEFT JOIN dbo.PurchaseOrders po ON po.SupplierId = s.Id
GROUP BY s.Id, s.Name, s.Category, s.Rating;
GO

-- ---------------------------------------------------------------------------
-- Stored procedure: per-batch KPI report (used by Research exports)
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_BatchKpiReport
    @FarmId INT = NULL,
    @ActiveOnly BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BatchCode, FarmName, Breed, BatchType, PlacementDate, ClosedDate, AgeInDays,
           InitialCount, CurrentCount, MortalityCount, CullingCount, LivabilityPct,
           AverageWeightKg, TotalFeedKg, FCR, EPEF, AvgDailyGainGm
    FROM dbo.vw_BatchPerformance
    WHERE (@FarmId IS NULL OR FarmId = @FarmId)
      AND (@ActiveOnly = 0 OR IsActive = 1)
    ORDER BY PlacementDate DESC;
END
GO

-- ---------------------------------------------------------------------------
-- Stored procedure: apply a vaccine schedule template to a batch
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ApplyVaccineTemplate
    @BatchId INT,
    @TemplateId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @placement DATE = (SELECT PlacementDate FROM dbo.Batches WHERE Id = @BatchId);
    IF @placement IS NULL RETURN;

    INSERT INTO dbo.VaccinationRecords (BatchId, VaccineName, DueDate, Status, DoseMl, Route, Notes)
    SELECT @BatchId, i.VaccineName, DATEADD(DAY, i.AgeDay, @placement),
           CASE WHEN DATEADD(DAY, i.AgeDay, @placement) < CAST(GETDATE() AS DATE) THEN 'Overdue' ELSE 'Planned' END,
           i.DoseMl, i.Route, CONCAT('Auto-scheduled from template (day ', i.AgeDay, ')')
    FROM dbo.VaccineScheduleItems i
    WHERE i.TemplateId = @TemplateId
      AND NOT EXISTS (SELECT 1 FROM dbo.VaccinationRecords r
                      WHERE r.BatchId = @BatchId AND r.VaccineName = i.VaccineName
                        AND r.DueDate = DATEADD(DAY, i.AgeDay, @placement));

    SELECT @@ROWCOUNT AS Inserted;
END
GO

PRINT '02_Views.sql completed.';
GO
