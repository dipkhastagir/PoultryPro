-- ============================================================
-- PoultryPro Farm Management System
-- Database Creation Script (SQL Server)
-- Run this FIRST before starting the application
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PoultryProDB')
BEGIN
    CREATE DATABASE PoultryProDB
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO

USE PoultryProDB;
GO

-- ============================================================
-- 1. FARMS
-- ============================================================
IF OBJECT_ID('Farms', 'U') IS NOT NULL DROP TABLE Farms;
GO
CREATE TABLE Farms (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150)   NOT NULL,
    Location        NVARCHAR(200)   NOT NULL,
    Capacity        INT             NOT NULL DEFAULT 0,
    CurrentStock    INT             NOT NULL DEFAULT 0,
    EmployeeCount   INT             NOT NULL DEFAULT 0,
    CertStatus      NVARCHAR(50)    NOT NULL DEFAULT 'Valid',
    CertExpiry      DATE            NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 2. BATCHES (Bird groups) - FIXED
-- ============================================================
IF OBJECT_ID('Batches', 'U') IS NOT NULL DROP TABLE Batches;
GO
CREATE TABLE Batches (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchCode       NVARCHAR(20)    NOT NULL UNIQUE,
    Breed           NVARCHAR(100)   NOT NULL,
    FarmId          INT             NOT NULL REFERENCES Farms(Id),
    InitialCount    INT             NOT NULL,
    CurrentCount    INT             NOT NULL,
    MortalityCount  INT             NOT NULL DEFAULT 0,
    PlacementDate   DATE            NOT NULL,
    HealthStatus    NVARCHAR(50)    NOT NULL DEFAULT 'Healthy',
    AverageWeightKg DECIMAL(6,3)    NOT NULL DEFAULT 0,
    TotalFeedKg     DECIMAL(10,2)   NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    -- Computed (Dynamic: PERSISTED removed because GETDATE() changes daily)
    AgeInDays       AS DATEDIFF(DAY, PlacementDate, CAST(GETDATE() AS DATE)),
    -- Computed (Deterministic: Can stay PERSISTED)
    MortalityRate   AS CAST(
        CASE WHEN InitialCount > 0
             THEN CAST(MortalityCount AS DECIMAL(10,4)) / InitialCount * 100
             ELSE 0 END AS DECIMAL(6,2)) PERSISTED
);
GO
CREATE INDEX IX_Batches_FarmId ON Batches(FarmId);
GO

-- ============================================================
-- 3. WEIGHT RECORDS
-- ============================================================
IF OBJECT_ID('WeightRecords', 'U') IS NOT NULL DROP TABLE WeightRecords;
GO
CREATE TABLE WeightRecords (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES Batches(Id),
    RecordDate      DATE            NOT NULL,
    AgeInDays       INT             NOT NULL,
    AvgWeightKg     DECIMAL(6,3)    NOT NULL,
    SampleSize      INT             NOT NULL DEFAULT 50
);
GO
CREATE INDEX IX_WeightRecords_BatchId ON WeightRecords(BatchId);
GO

-- ============================================================
-- 4. MORTALITY LOGS
-- ============================================================
IF OBJECT_ID('MortalityLogs', 'U') IS NOT NULL DROP TABLE MortalityLogs;
GO
CREATE TABLE MortalityLogs (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES Batches(Id),
    LogDate         DATE            NOT NULL,
    Count           INT             NOT NULL,
    Cause           NVARCHAR(150)   NOT NULL DEFAULT 'Unknown',
    Notes           NVARCHAR(500)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_MortalityLogs_BatchId ON MortalityLogs(BatchId);
GO

-- ============================================================
-- 5. VACCINATION RECORDS
-- ============================================================
IF OBJECT_ID('VaccinationRecords', 'U') IS NOT NULL DROP TABLE VaccinationRecords;
GO
CREATE TABLE VaccinationRecords (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES Batches(Id),
    VaccineName     NVARCHAR(150)   NOT NULL,
    DueDate         DATE            NOT NULL,
    CompletedDate   DATE            NULL,
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'Planned',
    DoseMl          DECIMAL(6,2)    NULL,
    Notes           NVARCHAR(500)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_Vax_BatchId ON VaccinationRecords(BatchId);
CREATE INDEX IX_Vax_DueDate ON VaccinationRecords(DueDate);
GO

-- ============================================================
-- 6. DISEASE CASES
-- ============================================================
IF OBJECT_ID('DiseaseCases', 'U') IS NOT NULL DROP TABLE DiseaseCases;
GO
CREATE TABLE DiseaseCases (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES Batches(Id),
    DiseaseName     NVARCHAR(150)   NOT NULL,
    ReportedDate    DATE            NOT NULL,
    AffectedCount   INT             NOT NULL DEFAULT 0,
    Treatment       NVARCHAR(500)   NOT NULL DEFAULT '',
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'Active',
    ResolvedDate    DATE            NULL,
    Notes           NVARCHAR(500)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_Disease_BatchId ON DiseaseCases(BatchId);
GO

-- ============================================================
-- 7. SUPPLIERS
-- ============================================================
IF OBJECT_ID('Suppliers', 'U') IS NOT NULL DROP TABLE Suppliers;
GO
CREATE TABLE Suppliers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150)   NOT NULL,
    ContactPerson   NVARCHAR(100)   NULL,
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(100)   NULL,
    Address         NVARCHAR(300)   NULL,
    Category        NVARCHAR(50)    NOT NULL DEFAULT 'General',
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 8. INVENTORY ITEMS
-- ============================================================
IF OBJECT_ID('InventoryItems', 'U') IS NOT NULL DROP TABLE InventoryItems;
GO
CREATE TABLE InventoryItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(200)   NOT NULL,
    Category        NVARCHAR(50)    NOT NULL,  -- Feed, Medicine, Equipment, Supplement, Biosecurity
    Quantity        DECIMAL(12,2)   NOT NULL DEFAULT 0,
    Unit            NVARCHAR(20)    NOT NULL DEFAULT 'kg',
    MinimumLevel    DECIMAL(12,2)   NOT NULL DEFAULT 0,
    ExpiryDate      DATE            NULL,
    SupplierId      INT             NULL REFERENCES Suppliers(Id),
    UnitCost        DECIMAL(12,2)   NOT NULL DEFAULT 0,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_Inventory_Category ON InventoryItems(Category);
GO

-- ============================================================
-- 9. INVENTORY TRANSACTIONS (Stock In/Out)
-- ============================================================
IF OBJECT_ID('InventoryTransactions', 'U') IS NOT NULL DROP TABLE InventoryTransactions;
GO
CREATE TABLE InventoryTransactions (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ItemId          INT             NOT NULL REFERENCES InventoryItems(Id),
    TxnType         NVARCHAR(10)    NOT NULL,  -- IN, OUT
    Quantity        DECIMAL(12,2)   NOT NULL,
    Reason          NVARCHAR(200)   NULL,
    TxnDate         DATE            NOT NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 10. CUSTOMERS
-- ============================================================
IF OBJECT_ID('Customers', 'U') IS NOT NULL DROP TABLE Customers;
GO
CREATE TABLE Customers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150)   NOT NULL,
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(100)   NULL,
    Address         NVARCHAR(300)   NULL,
    CustomerType    NVARCHAR(50)    NOT NULL DEFAULT 'Retailer',
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 11. ORDERS
-- ============================================================
IF OBJECT_ID('Orders', 'U') IS NOT NULL DROP TABLE Orders;
GO
CREATE TABLE Orders (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    OrderCode       NVARCHAR(20)    NOT NULL UNIQUE,
    CustomerId      INT             NOT NULL REFERENCES Customers(Id),
    FarmId          INT             NOT NULL REFERENCES Farms(Id),
    Product         NVARCHAR(150)   NOT NULL,
    Quantity        DECIMAL(12,2)   NOT NULL,
    Unit            NVARCHAR(20)    NOT NULL DEFAULT 'kg',
    Grade           NVARCHAR(10)    NOT NULL DEFAULT 'A',
    UnitPrice       DECIMAL(10,2)   NOT NULL,
    TotalAmount     AS CAST(Quantity * UnitPrice AS DECIMAL(14,2)) PERSISTED,
    OrderDate       DATE            NOT NULL,
    DeliveryDate    DATE            NOT NULL,
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'Pending',
    PaymentStatus   NVARCHAR(50)    NOT NULL DEFAULT 'Unpaid',
    Notes           NVARCHAR(500)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_Orders_CustomerId ON Orders(CustomerId);
CREATE INDEX IX_Orders_Status ON Orders(Status);
GO

-- ============================================================
-- 12. FINANCIAL TRANSACTIONS
-- ============================================================
IF OBJECT_ID('Transactions', 'U') IS NOT NULL DROP TABLE Transactions;
GO
CREATE TABLE Transactions (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TxnDate         DATE            NOT NULL,
    TxnType         NVARCHAR(20)    NOT NULL,  -- Sale, Purchase, Expense, Investment
    Category        NVARCHAR(100)   NOT NULL,
    Description     NVARCHAR(300)   NOT NULL,
    Amount          DECIMAL(14,2)   NOT NULL,  -- positive=income, negative=expense
    PaymentStatus   NVARCHAR(50)    NOT NULL DEFAULT 'Paid',
    FarmId          INT             NULL REFERENCES Farms(Id),
    Reference       NVARCHAR(100)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_Txn_Date ON Transactions(TxnDate);
CREATE INDEX IX_Txn_Type ON Transactions(TxnType);
GO

-- ============================================================
-- 13. EMPLOYEES
-- ============================================================
IF OBJECT_ID('Employees', 'U') IS NOT NULL DROP TABLE Employees;
GO
CREATE TABLE Employees (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150)   NOT NULL,
    Role            NVARCHAR(100)   NOT NULL,
    FarmId          INT             NULL REFERENCES Farms(Id),
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(100)   NULL,
    Status          NVARCHAR(30)    NOT NULL DEFAULT 'On Duty',
    MonthlySalary   DECIMAL(12,2)   NOT NULL DEFAULT 0,
    JoinDate        DATE            NOT NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 14. CERTIFICATIONS
-- ============================================================
IF OBJECT_ID('Certifications', 'U') IS NOT NULL DROP TABLE Certifications;
GO
CREATE TABLE Certifications (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FarmId          INT             NOT NULL REFERENCES Farms(Id),
    CertName        NVARCHAR(150)   NOT NULL,
    IssuedDate      DATE            NOT NULL,
    ExpiryDate      DATE            NOT NULL,
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'Valid',
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- USEFUL VIEWS
-- ============================================================

-- Dashboard summary view
IF OBJECT_ID('vw_DashboardSummary', 'V') IS NOT NULL DROP VIEW vw_DashboardSummary;
GO
CREATE VIEW vw_DashboardSummary AS
SELECT
    (SELECT SUM(CurrentStock) FROM Farms WHERE IsActive = 1)           AS TotalBirds,
    (SELECT COUNT(*) FROM Batches WHERE IsActive = 1)                    AS ActiveBatches,
    (SELECT COUNT(*) FROM Farms WHERE IsActive = 1)                      AS TotalFarms,
    (SELECT COUNT(*) FROM Employees WHERE IsActive = 1 AND Status = 'On Duty') AS StaffOnDuty,
    (SELECT COUNT(*) FROM VaccinationRecords WHERE Status = 'Overdue')  AS OverdueVaccinations,
    (SELECT COUNT(*) FROM InventoryItems WHERE Quantity <= MinimumLevel) AS LowStockCount,
    (SELECT SUM(Amount) FROM Transactions
     WHERE TxnDate >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
       AND Amount > 0)                                                                  AS MonthlyRevenue,
    (SELECT SUM(ABS(Amount)) FROM Transactions
     WHERE TxnDate >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
       AND Amount < 0)                                                                  AS MonthlyExpenses;
GO

-- Batch detail view
IF OBJECT_ID('vw_BatchDetails', 'V') IS NOT NULL DROP VIEW vw_BatchDetails;
GO
CREATE VIEW vw_BatchDetails AS
SELECT
    b.Id, b.BatchCode, b.Breed,
    b.InitialCount, b.CurrentCount, b.MortalityCount,
    b.PlacementDate, b.AgeInDays,
    b.HealthStatus, b.AverageWeightKg, b.TotalFeedKg,
    b.MortalityRate, b.IsActive,
    f.Name AS FarmName, f.Location AS FarmLocation,
    CASE WHEN b.AverageWeightKg > 0 AND b.CurrentCount > 0
         THEN CAST(b.TotalFeedKg / (b.CurrentCount * b.AverageWeightKg) AS DECIMAL(6,3))
         ELSE 0 END AS FCR
FROM Batches b
INNER JOIN Farms f ON b.FarmId = f.Id;
GO

-- Inventory stock status view
IF OBJECT_ID('vw_InventoryStatus', 'V') IS NOT NULL DROP VIEW vw_InventoryStatus;
GO
CREATE VIEW vw_InventoryStatus AS
SELECT
    i.Id, i.Name, i.Category, i.Quantity, i.Unit,
    i.MinimumLevel, i.ExpiryDate, i.UnitCost,
    CAST(i.Quantity * i.UnitCost AS DECIMAL(14,2)) AS TotalValue,
    s.Name AS SupplierName,
    CASE
        WHEN i.Quantity <= 0                        THEN 'Out of Stock'
        WHEN i.Quantity <= i.MinimumLevel           THEN 'Low Stock'
        ELSE                                             'In Stock'
    END AS StockStatus,
    CASE
        WHEN i.ExpiryDate IS NOT NULL
         AND i.ExpiryDate <= DATEADD(DAY, 30, GETDATE()) THEN 1
        ELSE 0
    END AS IsExpiringSoon
FROM InventoryItems i
LEFT JOIN Suppliers s ON i.SupplierId = s.Id;
GO

-- Monthly financials view
IF OBJECT_ID('vw_MonthlyFinancials', 'V') IS NOT NULL DROP VIEW vw_MonthlyFinancials;
GO
CREATE VIEW vw_MonthlyFinancials AS
SELECT
    YEAR(TxnDate) AS [Year],
    MONTH(TxnDate) AS [Month],
    DATENAME(MONTH, TxnDate) AS MonthName,
    SUM(CASE WHEN Amount > 0 THEN Amount ELSE 0 END)       AS Revenue,
    SUM(CASE WHEN Amount < 0 THEN ABS(Amount) ELSE 0 END)  AS Expenses,
    SUM(CASE WHEN Amount > 0 THEN Amount ELSE 0 END)
    - SUM(CASE WHEN Amount < 0 THEN ABS(Amount) ELSE 0 END) AS NetProfit
FROM Transactions
GROUP BY YEAR(TxnDate), MONTH(TxnDate), DATENAME(MONTH, TxnDate);
GO

-- Overdue vaccinations view
IF OBJECT_ID('vw_OverdueVaccinations', 'V') IS NOT NULL DROP VIEW vw_OverdueVaccinations;
GO
CREATE VIEW vw_OverdueVaccinations AS
SELECT
    v.Id, v.VaccineName, v.DueDate,
    DATEDIFF(DAY, v.DueDate, GETDATE()) AS DaysOverdue,
    b.BatchCode, b.Breed, b.CurrentCount,
    f.Name AS FarmName
FROM VaccinationRecords v
INNER JOIN Batches b ON v.BatchId = b.Id
INNER JOIN Farms f ON b.FarmId = f.Id
WHERE v.Status = 'Overdue' OR (v.CompletedDate IS NULL AND v.DueDate < CAST(GETDATE() AS DATE));
GO

PRINT 'All tables and views created successfully.';
GO