-- ============================================================================
-- PoultryPro — STEP 1 : Tables (idempotent, safe to re-run)
-- ----------------------------------------------------------------------------
-- * Creates every table that does not exist yet.
-- * Upgrades databases created by the ORIGINAL PoultryPro script by adding
--   the newer columns (nothing is dropped, no data is lost).
-- ============================================================================
USE PoultryProDB;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ============================================================================
   MODULE A : CORE FARM OPERATIONS
   ========================================================================== */

-- A1. FARMS -----------------------------------------------------------------
IF OBJECT_ID('dbo.Farms','U') IS NULL
CREATE TABLE dbo.Farms (
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
IF COL_LENGTH('dbo.Farms','FarmType')   IS NULL ALTER TABLE dbo.Farms ADD FarmType   NVARCHAR(30)  NOT NULL CONSTRAINT DF_Farms_FarmType DEFAULT 'Broiler';
IF COL_LENGTH('dbo.Farms','Latitude')   IS NULL ALTER TABLE dbo.Farms ADD Latitude   DECIMAL(9,6)  NULL;
IF COL_LENGTH('dbo.Farms','Longitude')  IS NULL ALTER TABLE dbo.Farms ADD Longitude  DECIMAL(9,6)  NULL;
IF COL_LENGTH('dbo.Farms','OwnerName')  IS NULL ALTER TABLE dbo.Farms ADD OwnerName  NVARCHAR(150) NULL;
IF COL_LENGTH('dbo.Farms','OwnerPhone') IS NULL ALTER TABLE dbo.Farms ADD OwnerPhone NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.Farms','Zone')       IS NULL ALTER TABLE dbo.Farms ADD Zone       NVARCHAR(80)  NULL;
IF COL_LENGTH('dbo.Farms','ShedCount')  IS NULL ALTER TABLE dbo.Farms ADD ShedCount  INT           NOT NULL CONSTRAINT DF_Farms_ShedCount DEFAULT 1;
IF COL_LENGTH('dbo.Farms','AreaSqFt')   IS NULL ALTER TABLE dbo.Farms ADD AreaSqFt   INT           NULL;
IF COL_LENGTH('dbo.Farms','Notes')      IS NULL ALTER TABLE dbo.Farms ADD Notes      NVARCHAR(500) NULL;
GO

-- A2. BATCHES (flocks) -------------------------------------------------------
IF OBJECT_ID('dbo.Batches','U') IS NULL
CREATE TABLE dbo.Batches (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchCode       NVARCHAR(20)    NOT NULL UNIQUE,
    Breed           NVARCHAR(100)   NOT NULL,
    FarmId          INT             NOT NULL REFERENCES dbo.Farms(Id),
    InitialCount    INT             NOT NULL,
    CurrentCount    INT             NOT NULL,
    MortalityCount  INT             NOT NULL DEFAULT 0,
    PlacementDate   DATE            NOT NULL,
    HealthStatus    NVARCHAR(50)    NOT NULL DEFAULT 'Healthy',
    AverageWeightKg DECIMAL(6,3)    NOT NULL DEFAULT 0,
    TotalFeedKg     DECIMAL(12,2)   NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    AgeInDays       AS DATEDIFF(DAY, PlacementDate, CAST(GETDATE() AS DATE)),
    MortalityRate   AS CAST(CASE WHEN InitialCount > 0
                         THEN CAST(MortalityCount AS DECIMAL(10,4)) / InitialCount * 100
                         ELSE 0 END AS DECIMAL(6,2)) PERSISTED
);
GO
IF COL_LENGTH('dbo.Batches','BatchType')           IS NULL ALTER TABLE dbo.Batches ADD BatchType NVARCHAR(30) NOT NULL CONSTRAINT DF_Batches_BatchType DEFAULT 'Broiler';
IF COL_LENGTH('dbo.Batches','CullingCount')        IS NULL ALTER TABLE dbo.Batches ADD CullingCount INT NOT NULL CONSTRAINT DF_Batches_Culling DEFAULT 0;
IF COL_LENGTH('dbo.Batches','Notes')               IS NULL ALTER TABLE dbo.Batches ADD Notes NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Batches','ExpectedHarvestDate') IS NULL ALTER TABLE dbo.Batches ADD ExpectedHarvestDate DATE NULL;
IF COL_LENGTH('dbo.Batches','ClosedDate')          IS NULL ALTER TABLE dbo.Batches ADD ClosedDate DATE NULL;
IF COL_LENGTH('dbo.Batches','ShedNo')              IS NULL ALTER TABLE dbo.Batches ADD ShedNo NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Batches','HatcheryName')        IS NULL ALTER TABLE dbo.Batches ADD HatcheryName NVARCHAR(150) NULL;
IF COL_LENGTH('dbo.Batches','ChickCostPerBird')    IS NULL ALTER TABLE dbo.Batches ADD ChickCostPerBird DECIMAL(10,2) NOT NULL CONSTRAINT DF_Batches_ChickCost DEFAULT 0;
IF COL_LENGTH('dbo.Batches','HarvestedCount')      IS NULL ALTER TABLE dbo.Batches ADD HarvestedCount INT NOT NULL CONSTRAINT DF_Batches_Harvested DEFAULT 0;
IF COL_LENGTH('dbo.Batches','HarvestWeightKg')     IS NULL ALTER TABLE dbo.Batches ADD HarvestWeightKg DECIMAL(12,2) NOT NULL CONSTRAINT DF_Batches_HarvestWt DEFAULT 0;
-- Traceability seal: head of the SHA-256 event hash chain frozen at sealing time
IF COL_LENGTH('dbo.Batches','TraceSealHash')       IS NULL ALTER TABLE dbo.Batches ADD TraceSealHash NVARCHAR(64) NULL;
IF COL_LENGTH('dbo.Batches','TraceSealedAt')       IS NULL ALTER TABLE dbo.Batches ADD TraceSealedAt DATETIME NULL;
IF COL_LENGTH('dbo.Batches','TraceSealedBy')       IS NULL ALTER TABLE dbo.Batches ADD TraceSealedBy NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Batches_FarmId')
    CREATE INDEX IX_Batches_FarmId ON dbo.Batches(FarmId);
GO

-- A3. WEIGHT RECORDS ---------------------------------------------------------
IF OBJECT_ID('dbo.WeightRecords','U') IS NULL
CREATE TABLE dbo.WeightRecords (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES dbo.Batches(Id),
    RecordDate      DATE            NOT NULL,
    AgeInDays       INT             NOT NULL,
    AvgWeightKg     DECIMAL(6,3)    NOT NULL,
    SampleSize      INT             NOT NULL DEFAULT 50
);
GO
IF COL_LENGTH('dbo.WeightRecords','UniformityPct') IS NULL ALTER TABLE dbo.WeightRecords ADD UniformityPct DECIMAL(5,2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_WeightRecords_BatchId')
    CREATE INDEX IX_WeightRecords_BatchId ON dbo.WeightRecords(BatchId);
GO

-- A4. MORTALITY LOGS ---------------------------------------------------------
IF OBJECT_ID('dbo.MortalityLogs','U') IS NULL
CREATE TABLE dbo.MortalityLogs (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES dbo.Batches(Id),
    LogDate         DATE            NOT NULL,
    Count           INT             NOT NULL,
    Cause           NVARCHAR(150)   NOT NULL DEFAULT 'Unknown',
    Notes           NVARCHAR(500)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_MortalityLogs_BatchId')
    CREATE INDEX IX_MortalityLogs_BatchId ON dbo.MortalityLogs(BatchId);
GO

/* ============================================================================
   MODULE B : HEALTH & BIOSECURITY
   ========================================================================== */

-- B1. VACCINATION RECORDS ----------------------------------------------------
IF OBJECT_ID('dbo.VaccinationRecords','U') IS NULL
CREATE TABLE dbo.VaccinationRecords (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES dbo.Batches(Id),
    VaccineName     NVARCHAR(150)   NOT NULL,
    DueDate         DATE            NOT NULL,
    CompletedDate   DATE            NULL,
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'Planned',
    DoseMl          DECIMAL(6,2)    NULL,
    Notes           NVARCHAR(500)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF COL_LENGTH('dbo.VaccinationRecords','Route')        IS NULL ALTER TABLE dbo.VaccinationRecords ADD Route NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.VaccinationRecords','AdministeredBy') IS NULL ALTER TABLE dbo.VaccinationRecords ADD AdministeredBy NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Vax_BatchId') CREATE INDEX IX_Vax_BatchId ON dbo.VaccinationRecords(BatchId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Vax_DueDate') CREATE INDEX IX_Vax_DueDate ON dbo.VaccinationRecords(DueDate);
GO

-- B2. DISEASE CASES ----------------------------------------------------------
IF OBJECT_ID('dbo.DiseaseCases','U') IS NULL
CREATE TABLE dbo.DiseaseCases (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES dbo.Batches(Id),
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
IF COL_LENGTH('dbo.DiseaseCases','DiagnosedBy') IS NULL ALTER TABLE dbo.DiseaseCases ADD DiagnosedBy NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.DiseaseCases','DeathCount')  IS NULL ALTER TABLE dbo.DiseaseCases ADD DeathCount INT NOT NULL CONSTRAINT DF_Disease_Deaths DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Disease_BatchId') CREATE INDEX IX_Disease_BatchId ON dbo.DiseaseCases(BatchId);
GO

-- B3. DISEASE LIBRARY (knowledge base) --------------------------------------
IF OBJECT_ID('dbo.DiseaseLibrary','U') IS NULL
CREATE TABLE dbo.DiseaseLibrary (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150)   NOT NULL,
    LocalName       NVARCHAR(150)   NULL,
    Category        NVARCHAR(50)    NOT NULL DEFAULT 'Viral',   -- Viral, Bacterial, Parasitic, Fungal, Nutritional, Environmental
    Severity        NVARCHAR(20)    NOT NULL DEFAULT 'Medium',  -- Low, Medium, High, Critical
    IsZoonotic      BIT             NOT NULL DEFAULT 0,
    IsNotifiable    BIT             NOT NULL DEFAULT 0,
    Symptoms        NVARCHAR(MAX)   NULL,
    Transmission    NVARCHAR(MAX)   NULL,
    Prevention      NVARCHAR(MAX)   NULL,
    Treatment       NVARCHAR(MAX)   NULL,
    AgeGroup        NVARCHAR(100)   NULL,
    MortalityRange  NVARCHAR(50)    NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- B4. VACCINE SCHEDULE TEMPLATES -------------------------------------------
IF OBJECT_ID('dbo.VaccineScheduleTemplates','U') IS NULL
CREATE TABLE dbo.VaccineScheduleTemplates (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150)   NOT NULL,
    BatchType       NVARCHAR(30)    NOT NULL DEFAULT 'Broiler',
    Description     NVARCHAR(500)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF OBJECT_ID('dbo.VaccineScheduleItems','U') IS NULL
CREATE TABLE dbo.VaccineScheduleItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TemplateId      INT             NOT NULL REFERENCES dbo.VaccineScheduleTemplates(Id) ON DELETE CASCADE,
    AgeDay          INT             NOT NULL,
    VaccineName     NVARCHAR(150)   NOT NULL,
    Route           NVARCHAR(50)    NULL,       -- Eye drop, Drinking water, Spray, Injection (SC/IM), Wing web
    DoseMl          DECIMAL(6,2)    NULL,
    Notes           NVARCHAR(300)   NULL
);
GO

-- B5. BIOSECURITY AUDITS -----------------------------------------------------
IF OBJECT_ID('dbo.BiosecurityAudits','U') IS NULL
CREATE TABLE dbo.BiosecurityAudits (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FarmId          INT             NOT NULL REFERENCES dbo.Farms(Id),
    AuditDate       DATE            NOT NULL,
    AuditorName     NVARCHAR(150)   NOT NULL,
    Score           DECIMAL(6,2)    NOT NULL DEFAULT 0,
    MaxScore        DECIMAL(6,2)    NOT NULL DEFAULT 0,
    ScorePct        AS CAST(CASE WHEN MaxScore > 0 THEN Score / MaxScore * 100 ELSE 0 END AS DECIMAL(6,2)),
    Grade           NVARCHAR(5)     NOT NULL DEFAULT 'C',
    Notes           NVARCHAR(1000)  NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF OBJECT_ID('dbo.BiosecurityAuditItems','U') IS NULL
CREATE TABLE dbo.BiosecurityAuditItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    AuditId         INT             NOT NULL REFERENCES dbo.BiosecurityAudits(Id) ON DELETE CASCADE,
    Category        NVARCHAR(80)    NOT NULL,
    Question        NVARCHAR(300)   NOT NULL,
    Weight          INT             NOT NULL DEFAULT 1,
    Passed          BIT             NOT NULL DEFAULT 0,
    Remark          NVARCHAR(300)   NULL
);
GO

/* ============================================================================
   MODULE C : SUPPLY CHAIN, INVENTORY & FEED
   ========================================================================== */

-- C1. SUPPLIERS --------------------------------------------------------------
IF OBJECT_ID('dbo.Suppliers','U') IS NULL
CREATE TABLE dbo.Suppliers (
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
IF COL_LENGTH('dbo.Suppliers','Rating')       IS NULL ALTER TABLE dbo.Suppliers ADD Rating DECIMAL(3,1) NOT NULL CONSTRAINT DF_Suppliers_Rating DEFAULT 4.0;
IF COL_LENGTH('dbo.Suppliers','PaymentTerms') IS NULL ALTER TABLE dbo.Suppliers ADD PaymentTerms NVARCHAR(100) NULL;
GO

-- C2. INVENTORY ITEMS --------------------------------------------------------
IF OBJECT_ID('dbo.InventoryItems','U') IS NULL
CREATE TABLE dbo.InventoryItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(200)   NOT NULL,
    Category        NVARCHAR(50)    NOT NULL,  -- Feed, Medicine, Vaccine, Equipment, Supplement, Biosecurity
    Quantity        DECIMAL(12,2)   NOT NULL DEFAULT 0,
    Unit            NVARCHAR(20)    NOT NULL DEFAULT 'kg',
    MinimumLevel    DECIMAL(12,2)   NOT NULL DEFAULT 0,
    ExpiryDate      DATE            NULL,
    SupplierId      INT             NULL REFERENCES dbo.Suppliers(Id),
    UnitCost        DECIMAL(12,2)   NOT NULL DEFAULT 0,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    UpdatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF COL_LENGTH('dbo.InventoryItems','SKU') IS NULL ALTER TABLE dbo.InventoryItems ADD SKU NVARCHAR(40) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Inventory_Category') CREATE INDEX IX_Inventory_Category ON dbo.InventoryItems(Category);
GO

-- C3. INVENTORY TRANSACTIONS (stock ledger) ---------------------------------
IF OBJECT_ID('dbo.InventoryTransactions','U') IS NULL
CREATE TABLE dbo.InventoryTransactions (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ItemId          INT             NOT NULL REFERENCES dbo.InventoryItems(Id) ON DELETE CASCADE,
    TxnType         NVARCHAR(10)    NOT NULL,  -- IN, OUT
    Quantity        DECIMAL(12,2)   NOT NULL,
    Reason          NVARCHAR(200)   NULL,
    TxnDate         DATE            NOT NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF COL_LENGTH('dbo.InventoryTransactions','UnitCost')  IS NULL ALTER TABLE dbo.InventoryTransactions ADD UnitCost DECIMAL(12,2) NULL;
IF COL_LENGTH('dbo.InventoryTransactions','FarmId')    IS NULL ALTER TABLE dbo.InventoryTransactions ADD FarmId INT NULL;
IF COL_LENGTH('dbo.InventoryTransactions','BatchId')   IS NULL ALTER TABLE dbo.InventoryTransactions ADD BatchId INT NULL;
IF COL_LENGTH('dbo.InventoryTransactions','Reference') IS NULL ALTER TABLE dbo.InventoryTransactions ADD Reference NVARCHAR(60) NULL;
IF COL_LENGTH('dbo.InventoryTransactions','CreatedBy') IS NULL ALTER TABLE dbo.InventoryTransactions ADD CreatedBy NVARCHAR(100) NULL;
GO

-- C4. PURCHASE ORDERS --------------------------------------------------------
IF OBJECT_ID('dbo.PurchaseOrders','U') IS NULL
CREATE TABLE dbo.PurchaseOrders (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    PoNumber        NVARCHAR(30)    NOT NULL UNIQUE,
    SupplierId      INT             NOT NULL REFERENCES dbo.Suppliers(Id),
    FarmId          INT             NULL REFERENCES dbo.Farms(Id),
    OrderDate       DATE            NOT NULL,
    ExpectedDate    DATE            NULL,
    ReceivedDate    DATE            NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Draft',  -- Draft, Ordered, Received, Cancelled
    PaymentStatus   NVARCHAR(20)    NOT NULL DEFAULT 'Unpaid',
    TotalAmount     DECIMAL(14,2)   NOT NULL DEFAULT 0,
    Notes           NVARCHAR(500)   NULL,
    CreatedBy       NVARCHAR(100)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF OBJECT_ID('dbo.PurchaseOrderItems','U') IS NULL
CREATE TABLE dbo.PurchaseOrderItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    PurchaseOrderId INT             NOT NULL REFERENCES dbo.PurchaseOrders(Id) ON DELETE CASCADE,
    InventoryItemId INT             NOT NULL REFERENCES dbo.InventoryItems(Id),
    Quantity        DECIMAL(12,2)   NOT NULL,
    UnitCost        DECIMAL(12,2)   NOT NULL,
    LineTotal       AS CAST(Quantity * UnitCost AS DECIMAL(14,2)) PERSISTED
);
GO

-- C5. FEED FORMULATIONS ------------------------------------------------------
IF OBJECT_ID('dbo.FeedFormulations','U') IS NULL
CREATE TABLE dbo.FeedFormulations (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    Name                NVARCHAR(150)   NOT NULL,
    Stage               NVARCHAR(30)    NOT NULL,     -- Pre-Starter, Starter, Grower, Finisher, Layer, Pre-Layer
    BatchType           NVARCHAR(30)    NOT NULL DEFAULT 'Broiler',
    FromAgeDay          INT             NOT NULL DEFAULT 0,
    ToAgeDay            INT             NOT NULL DEFAULT 0,
    CrudeProteinPct     DECIMAL(5,2)    NOT NULL DEFAULT 0,
    EnergyKcalPerKg     INT             NOT NULL DEFAULT 0,
    CalciumPct          DECIMAL(5,2)    NULL,
    PhosphorusPct       DECIMAL(5,2)    NULL,
    LysinePct           DECIMAL(5,2)    NULL,
    CostPerKg           DECIMAL(10,2)   NOT NULL DEFAULT 0,
    Ingredients         NVARCHAR(MAX)   NULL,
    Notes               NVARCHAR(500)   NULL,
    IsActive            BIT             NOT NULL DEFAULT 1,
    CreatedAt           DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

/* ============================================================================
   MODULE D : SALES, CUSTOMERS & FINANCE
   ========================================================================== */

-- D1. CUSTOMERS --------------------------------------------------------------
IF OBJECT_ID('dbo.Customers','U') IS NULL
CREATE TABLE dbo.Customers (
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
IF COL_LENGTH('dbo.Customers','CreditLimit') IS NULL ALTER TABLE dbo.Customers ADD CreditLimit DECIMAL(14,2) NOT NULL CONSTRAINT DF_Customers_Credit DEFAULT 0;
GO

-- D2. ORDERS -----------------------------------------------------------------
IF OBJECT_ID('dbo.Orders','U') IS NULL
CREATE TABLE dbo.Orders (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    OrderCode       NVARCHAR(20)    NOT NULL UNIQUE,
    CustomerId      INT             NOT NULL REFERENCES dbo.Customers(Id),
    FarmId          INT             NOT NULL REFERENCES dbo.Farms(Id),
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
IF COL_LENGTH('dbo.Orders','BatchId')    IS NULL ALTER TABLE dbo.Orders ADD BatchId INT NULL;
IF COL_LENGTH('dbo.Orders','BirdCount')  IS NULL ALTER TABLE dbo.Orders ADD BirdCount INT NULL;
IF COL_LENGTH('dbo.Orders','PaidAmount') IS NULL ALTER TABLE dbo.Orders ADD PaidAmount DECIMAL(14,2) NOT NULL CONSTRAINT DF_Orders_Paid DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Orders_CustomerId') CREATE INDEX IX_Orders_CustomerId ON dbo.Orders(CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Orders_Status')     CREATE INDEX IX_Orders_Status ON dbo.Orders(Status);
GO

-- D3. FINANCIAL TRANSACTIONS ------------------------------------------------
IF OBJECT_ID('dbo.Transactions','U') IS NULL
CREATE TABLE dbo.Transactions (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TxnDate         DATE            NOT NULL,
    TxnType         NVARCHAR(20)    NOT NULL,  -- Sale, Purchase, Expense, Investment, Salary
    Category        NVARCHAR(100)   NOT NULL,
    Description     NVARCHAR(300)   NOT NULL,
    Amount          DECIMAL(14,2)   NOT NULL,  -- positive = income, negative = expense
    PaymentStatus   NVARCHAR(50)    NOT NULL DEFAULT 'Paid',
    FarmId          INT             NULL REFERENCES dbo.Farms(Id),
    Reference       NVARCHAR(100)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF COL_LENGTH('dbo.Transactions','BatchId')       IS NULL ALTER TABLE dbo.Transactions ADD BatchId INT NULL;
IF COL_LENGTH('dbo.Transactions','PaymentMethod') IS NULL ALTER TABLE dbo.Transactions ADD PaymentMethod NVARCHAR(30) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Txn_Date') CREATE INDEX IX_Txn_Date ON dbo.Transactions(TxnDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Txn_Type') CREATE INDEX IX_Txn_Type ON dbo.Transactions(TxnType);
GO

-- D4. BUDGETS ----------------------------------------------------------------
IF OBJECT_ID('dbo.Budgets','U') IS NULL
CREATE TABLE dbo.Budgets (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    [Year]          INT             NOT NULL,
    [Month]         INT             NOT NULL,
    Category        NVARCHAR(100)   NOT NULL,
    BudgetType      NVARCHAR(20)    NOT NULL DEFAULT 'Expense', -- Expense, Revenue
    PlannedAmount   DECIMAL(14,2)   NOT NULL,
    FarmId          INT             NULL REFERENCES dbo.Farms(Id),
    Notes           NVARCHAR(300)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

/* ============================================================================
   MODULE E : PEOPLE (HR, ATTENDANCE, PAYROLL, TASKS)
   ========================================================================== */

-- E1. EMPLOYEES --------------------------------------------------------------
IF OBJECT_ID('dbo.Employees','U') IS NULL
CREATE TABLE dbo.Employees (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150)   NOT NULL,
    Role            NVARCHAR(100)   NOT NULL,
    FarmId          INT             NULL REFERENCES dbo.Farms(Id),
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(100)   NULL,
    Status          NVARCHAR(30)    NOT NULL DEFAULT 'On Duty',
    MonthlySalary   DECIMAL(12,2)   NOT NULL DEFAULT 0,
    JoinDate        DATE            NOT NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF COL_LENGTH('dbo.Employees','EmployeeCode') IS NULL ALTER TABLE dbo.Employees ADD EmployeeCode NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Employees','Address')      IS NULL ALTER TABLE dbo.Employees ADD Address NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.Employees','EmergencyContact') IS NULL ALTER TABLE dbo.Employees ADD EmergencyContact NVARCHAR(100) NULL;
GO

-- E2. ATTENDANCE -------------------------------------------------------------
IF OBJECT_ID('dbo.Attendance','U') IS NULL
CREATE TABLE dbo.Attendance (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT             NOT NULL REFERENCES dbo.Employees(Id),
    AttendanceDate  DATE            NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Present',  -- Present, Absent, Leave, Half Day, Late
    CheckIn         TIME(0)         NULL,
    CheckOut        TIME(0)         NULL,
    Notes           NVARCHAR(200)   NULL,
    CONSTRAINT UQ_Attendance_EmpDate UNIQUE (EmployeeId, AttendanceDate)
);
GO

-- E3. PAYROLL ----------------------------------------------------------------
IF OBJECT_ID('dbo.Payroll','U') IS NULL
CREATE TABLE dbo.Payroll (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT             NOT NULL REFERENCES dbo.Employees(Id),
    PeriodYear      INT             NOT NULL,
    PeriodMonth     INT             NOT NULL,
    BasicSalary     DECIMAL(12,2)   NOT NULL,
    Allowances      DECIMAL(12,2)   NOT NULL DEFAULT 0,
    Overtime        DECIMAL(12,2)   NOT NULL DEFAULT 0,
    Deductions      DECIMAL(12,2)   NOT NULL DEFAULT 0,
    NetPay          AS CAST(BasicSalary + Allowances + Overtime - Deductions AS DECIMAL(12,2)) PERSISTED,
    DaysPresent     INT             NOT NULL DEFAULT 0,
    DaysAbsent      INT             NOT NULL DEFAULT 0,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Pending',  -- Pending, Paid
    PaidDate        DATE            NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Payroll_EmpPeriod UNIQUE (EmployeeId, PeriodYear, PeriodMonth)
);
GO

-- E4. FARM TASKS -------------------------------------------------------------
IF OBJECT_ID('dbo.FarmTasks','U') IS NULL
CREATE TABLE dbo.FarmTasks (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Title           NVARCHAR(200)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    FarmId          INT             NULL REFERENCES dbo.Farms(Id),
    BatchId         INT             NULL REFERENCES dbo.Batches(Id),
    AssignedToEmployeeId INT        NULL REFERENCES dbo.Employees(Id),
    Category        NVARCHAR(50)    NOT NULL DEFAULT 'General',  -- General, Cleaning, Vaccination, Feeding, Maintenance, Sales
    Priority        NVARCHAR(20)    NOT NULL DEFAULT 'Medium',   -- Low, Medium, High, Urgent
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Open',     -- Open, In Progress, Done
    DueDate         DATE            NULL,
    CompletedAt     DATETIME        NULL,
    CreatedBy       NVARCHAR(100)   NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- E5. CERTIFICATIONS ---------------------------------------------------------
IF OBJECT_ID('dbo.Certifications','U') IS NULL
CREATE TABLE dbo.Certifications (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FarmId          INT             NOT NULL REFERENCES dbo.Farms(Id),
    CertName        NVARCHAR(150)   NOT NULL,
    IssuedDate      DATE            NOT NULL,
    ExpiryDate      DATE            NOT NULL,
    Status          NVARCHAR(50)    NOT NULL DEFAULT 'Valid',
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF COL_LENGTH('dbo.Certifications','IssuingAuthority') IS NULL ALTER TABLE dbo.Certifications ADD IssuingAuthority NVARCHAR(150) NULL;
IF COL_LENGTH('dbo.Certifications','CertificateNo')    IS NULL ALTER TABLE dbo.Certifications ADD CertificateNo NVARCHAR(60) NULL;
GO

/* ============================================================================
   MODULE F : FIELD OPERATIONS (mobile field officers)
   ========================================================================== */

IF OBJECT_ID('dbo.FieldOfficers','U') IS NULL
CREATE TABLE dbo.FieldOfficers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150)   NOT NULL,
    Username        NVARCHAR(60)    NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(200)   NOT NULL,
    Phone           NVARCHAR(20)    NULL,
    Email           NVARCHAR(100)   NULL,
    Zone            NVARCHAR(80)    NULL,
    Designation     NVARCHAR(80)    NOT NULL DEFAULT 'Field Officer',
    IsActive        BIT             NOT NULL DEFAULT 1,
    LastLoginAt     DATETIME        NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.FieldOfficerFarmAssignments','U') IS NULL
CREATE TABLE dbo.FieldOfficerFarmAssignments (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FieldOfficerId  INT             NOT NULL REFERENCES dbo.FieldOfficers(Id),
    FarmId          INT             NOT NULL REFERENCES dbo.Farms(Id),
    AssignedDate    DATETIME        NOT NULL DEFAULT GETDATE(),
    IsActive        BIT             NOT NULL DEFAULT 1
);
GO

IF OBJECT_ID('dbo.FarmVisits','U') IS NULL
CREATE TABLE dbo.FarmVisits (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    FieldOfficerId      INT             NOT NULL REFERENCES dbo.FieldOfficers(Id),
    FarmId              INT             NOT NULL REFERENCES dbo.Farms(Id),
    BatchId             INT             NULL REFERENCES dbo.Batches(Id),
    VisitDate           DATE            NOT NULL,
    VisitPurpose        NVARCHAR(80)    NOT NULL DEFAULT 'Routine Check',
    VisitStatus         NVARCHAR(30)    NOT NULL DEFAULT 'Pending',  -- Pending, In Progress, Completed, Cancelled
    CheckInTime         DATETIME        NULL,
    CheckOutTime        DATETIME        NULL,
    CheckInLatitude     DECIMAL(9,6)    NULL,
    CheckInLongitude    DECIMAL(9,6)    NULL,
    GpsAccuracyMeters   DECIMAL(8,2)    NULL,
    OverallRemark       NVARCHAR(1000)  NULL,
    SyncStatus          NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
    SyncedAt            DATETIME        NULL,
    CreatedAt           DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FarmVisits_Farm') CREATE INDEX IX_FarmVisits_Farm ON dbo.FarmVisits(FarmId, VisitDate);
GO

IF OBJECT_ID('dbo.DailyFarmRecords','U') IS NULL
CREATE TABLE dbo.DailyFarmRecords (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    VisitId             INT             NULL REFERENCES dbo.FarmVisits(Id) ON DELETE CASCADE,
    BatchId             INT             NOT NULL REFERENCES dbo.Batches(Id),
    FarmId              INT             NOT NULL REFERENCES dbo.Farms(Id),
    RecordDate          DATE            NOT NULL,
    BirdCount           INT             NOT NULL DEFAULT 0,
    MortalityCount      INT             NOT NULL DEFAULT 0,
    CullingCount        INT             NOT NULL DEFAULT 0,
    FeedConsumptionKg   DECIMAL(10,2)   NOT NULL DEFAULT 0,
    WaterConsumptionLtr DECIMAL(10,2)   NOT NULL DEFAULT 0,
    TemperatureC        DECIMAL(5,2)    NULL,
    HumidityPercent     DECIMAL(5,2)    NULL,
    LightHours          DECIMAL(4,1)    NULL,
    AverageWeightGm     DECIMAL(8,2)    NULL,
    MedicineUsed        NVARCHAR(150)   NULL,
    MedicineDosage      NVARCHAR(100)   NULL,
    VaccineUsed         NVARCHAR(150)   NULL,
    Remarks             NVARCHAR(500)   NULL,
    SyncStatus          NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
    SyncedAt            DATETIME        NULL,
    CreatedAt           DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Daily_BatchDate') CREATE INDEX IX_Daily_BatchDate ON dbo.DailyFarmRecords(BatchId, RecordDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Daily_FarmDate')  CREATE INDEX IX_Daily_FarmDate  ON dbo.DailyFarmRecords(FarmId, RecordDate);
GO

IF OBJECT_ID('dbo.VisitPhotos','U') IS NULL
CREATE TABLE dbo.VisitPhotos (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    VisitId         INT             NOT NULL REFERENCES dbo.FarmVisits(Id) ON DELETE CASCADE,
    PhotoType       NVARCHAR(40)    NOT NULL DEFAULT 'General',
    FileName        NVARCHAR(260)   NOT NULL,
    FilePath        NVARCHAR(400)   NOT NULL,
    FileSizeKb      INT             NULL,
    Caption         NVARCHAR(300)   NULL,
    CapturedAt      DATETIME        NOT NULL DEFAULT GETDATE(),
    SyncStatus      NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
    SyncedAt        DATETIME        NULL
);
GO
IF COL_LENGTH('dbo.VisitPhotos','SyncedAt') IS NULL ALTER TABLE dbo.VisitPhotos ADD SyncedAt DATETIME NULL;
GO

IF OBJECT_ID('dbo.FarmerSignatures','U') IS NULL
CREATE TABLE dbo.FarmerSignatures (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    VisitId             INT             NOT NULL REFERENCES dbo.FarmVisits(Id) ON DELETE CASCADE,
    FarmerName          NVARCHAR(150)   NOT NULL,
    FarmerPhone         NVARCHAR(20)    NULL,
    SignatureFilePath   NVARCHAR(400)   NOT NULL,
    IsVerifiedByFarmer  BIT             NOT NULL DEFAULT 0,
    SignedAt            DATETIME        NOT NULL DEFAULT GETDATE(),
    SyncStatus          NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
    SyncedAt            DATETIME        NULL
);
GO
IF COL_LENGTH('dbo.FarmerSignatures','SyncedAt') IS NULL ALTER TABLE dbo.FarmerSignatures ADD SyncedAt DATETIME NULL;
GO

/* ============================================================================
   MODULE G : PRODUCTION SCIENCE (layers, breed standards, field trials)
   ========================================================================== */

IF OBJECT_ID('dbo.EggProductionRecords','U') IS NULL
CREATE TABLE dbo.EggProductionRecords (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    BatchId         INT             NOT NULL REFERENCES dbo.Batches(Id),
    RecordDate      DATE            NOT NULL,
    HenCount        INT             NOT NULL,
    TotalEggs       INT             NOT NULL,
    GradeA          INT             NOT NULL DEFAULT 0,
    GradeB          INT             NOT NULL DEFAULT 0,
    CrackedEggs     INT             NOT NULL DEFAULT 0,
    DirtyEggs       INT             NOT NULL DEFAULT 0,
    AvgEggWeightGm  DECIMAL(5,1)    NULL,
    FeedKg          DECIMAL(10,2)   NULL,
    Notes           NVARCHAR(300)   NULL,
    HenDayPct       AS CAST(CASE WHEN HenCount > 0 THEN CAST(TotalEggs AS DECIMAL(12,4)) / HenCount * 100 ELSE 0 END AS DECIMAL(6,2)) PERSISTED,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Egg_BatchDate UNIQUE (BatchId, RecordDate)
);
GO

-- Breed performance standards (reference growth curves used by the analytics engine)
IF OBJECT_ID('dbo.BreedStandards','U') IS NULL
CREATE TABLE dbo.BreedStandards (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    Breed               NVARCHAR(100)   NOT NULL,
    AgeDay              INT             NOT NULL,
    TargetWeightGm      DECIMAL(8,1)    NOT NULL,
    DailyFeedGm         DECIMAL(8,1)    NOT NULL DEFAULT 0,
    CumFeedGm           DECIMAL(10,1)   NOT NULL DEFAULT 0,
    TargetFCR           DECIMAL(5,3)    NOT NULL DEFAULT 0,
    TargetCumMortPct    DECIMAL(5,2)    NOT NULL DEFAULT 0,
    CONSTRAINT UQ_BreedStd UNIQUE (Breed, AgeDay)
);
GO

-- Field trials / experiments (treatment vs control batch comparison)
IF OBJECT_ID('dbo.FieldTrials','U') IS NULL
CREATE TABLE dbo.FieldTrials (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    TrialCode           NVARCHAR(30)    NOT NULL UNIQUE,
    Title               NVARCHAR(200)   NOT NULL,
    Objective           NVARCHAR(1000)  NULL,
    Hypothesis          NVARCHAR(1000)  NULL,
    Intervention        NVARCHAR(1000)  NULL,
    TreatmentBatchId    INT             NOT NULL REFERENCES dbo.Batches(Id),
    ControlBatchId      INT             NOT NULL REFERENCES dbo.Batches(Id),
    PrimaryMetric       NVARCHAR(50)    NOT NULL DEFAULT 'FCR',  -- FCR, Weight, Mortality, EPEF
    StartDate           DATE            NOT NULL,
    EndDate             DATE            NULL,
    Status              NVARCHAR(20)    NOT NULL DEFAULT 'Running', -- Planned, Running, Completed, Abandoned
    Conclusion          NVARCHAR(MAX)   NULL,
    PrincipalInvestigator NVARCHAR(150) NULL,
    CreatedAt           DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

/* ============================================================================
   MODULE H : IoT SENSOR NETWORK
   ========================================================================== */

IF OBJECT_ID('dbo.SensorDevices','U') IS NULL
CREATE TABLE dbo.SensorDevices (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    DeviceCode      NVARCHAR(40)    NOT NULL UNIQUE,
    Name            NVARCHAR(150)   NOT NULL,
    FarmId          INT             NOT NULL REFERENCES dbo.Farms(Id),
    ShedNo          NVARCHAR(20)    NULL,
    DeviceType      NVARCHAR(60)    NOT NULL DEFAULT 'ESP32 Climate Node',
    ApiKey          NVARCHAR(64)    NOT NULL,
    FirmwareVersion NVARCHAR(20)    NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    LastSeenAt      DATETIME        NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.SensorReadings','U') IS NULL
CREATE TABLE dbo.SensorReadings (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    DeviceId        INT             NOT NULL REFERENCES dbo.SensorDevices(Id) ON DELETE CASCADE,
    FarmId          INT             NOT NULL,
    ReadingTime     DATETIME        NOT NULL,
    TemperatureC    DECIMAL(5,2)    NULL,
    HumidityPercent DECIMAL(5,2)    NULL,
    AmmoniaPpm      DECIMAL(6,2)    NULL,
    Co2Ppm          DECIMAL(8,2)    NULL,
    LightLux        DECIMAL(8,2)    NULL,
    WaterFlowLtr    DECIMAL(8,2)    NULL,
    FeedLevelPct    DECIMAL(5,2)    NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sensor_FarmTime')   CREATE INDEX IX_Sensor_FarmTime   ON dbo.SensorReadings(FarmId, ReadingTime);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sensor_DeviceTime') CREATE INDEX IX_Sensor_DeviceTime ON dbo.SensorReadings(DeviceId, ReadingTime);
GO

/* ============================================================================
   MODULE I : PLATFORM (users, audit, alerts, settings)
   ========================================================================== */

IF OBJECT_ID('dbo.AppUsers','U') IS NULL
CREATE TABLE dbo.AppUsers (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150)   NOT NULL,
    Username        NVARCHAR(60)    NOT NULL UNIQUE,
    Email           NVARCHAR(150)   NULL,
    Phone           NVARCHAR(20)    NULL,
    PasswordHash    NVARCHAR(300)   NOT NULL,
    Role            NVARCHAR(30)    NOT NULL DEFAULT 'Viewer', -- Admin, Manager, Veterinarian, Accountant, FieldOfficer, Viewer
    FieldOfficerId  INT             NULL REFERENCES dbo.FieldOfficers(Id),
    IsActive        BIT             NOT NULL DEFAULT 1,
    LastLoginAt     DATETIME        NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.AuditLogs','U') IS NULL
CREATE TABLE dbo.AuditLogs (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserName        NVARCHAR(60)    NULL,
    Action          NVARCHAR(60)    NOT NULL,
    Entity          NVARCHAR(60)    NOT NULL,
    EntityId        NVARCHAR(40)    NULL,
    Details         NVARCHAR(1000)  NULL,
    IpAddress       NVARCHAR(45)    NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Audit_CreatedAt') CREATE INDEX IX_Audit_CreatedAt ON dbo.AuditLogs(CreatedAt DESC);
GO

IF OBJECT_ID('dbo.Alerts','U') IS NULL
CREATE TABLE dbo.Alerts (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    RuleCode        NVARCHAR(40)    NOT NULL,   -- MORT_SPIKE, HEAT_STRESS, VAX_OVERDUE, LOW_STOCK, CERT_EXPIRY, HARVEST_READY, SYNC_STALE, GROWTH_LAG, AMMONIA, EXPIRY
    Severity        NVARCHAR(20)    NOT NULL DEFAULT 'Warning', -- Info, Warning, Critical
    Title           NVARCHAR(200)   NOT NULL,
    Message         NVARCHAR(1000)  NULL,
    FarmId          INT             NULL,
    BatchId         INT             NULL,
    RefKey          NVARCHAR(100)   NULL,       -- de-duplication key
    Link            NVARCHAR(200)   NULL,
    IsRead          BIT             NOT NULL DEFAULT 0,
    IsResolved      BIT             NOT NULL DEFAULT 0,
    ResolvedBy      NVARCHAR(60)    NULL,
    ResolvedAt      DATETIME        NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Alerts_Open') CREATE INDEX IX_Alerts_Open ON dbo.Alerts(IsResolved, CreatedAt DESC);
GO

IF OBJECT_ID('dbo.AppSettings','U') IS NULL
CREATE TABLE dbo.AppSettings (
    [Key]           NVARCHAR(80)    NOT NULL PRIMARY KEY,
    [Value]         NVARCHAR(500)   NOT NULL,
    GroupName       NVARCHAR(50)    NOT NULL DEFAULT 'General',
    Description     NVARCHAR(300)   NULL,
    UpdatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.SchemaVersions','U') IS NULL
CREATE TABLE dbo.SchemaVersions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ScriptName  NVARCHAR(100) NOT NULL,
    AppliedAt   DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

PRINT '01_Schema.sql completed.';
GO
