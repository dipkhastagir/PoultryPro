-- ============================================================
-- PoultryPro - Seed Data Script
-- Run AFTER 01_CreateDatabase.sql
-- ============================================================

USE PoultryProDB;
GO

-- ============================================================
-- FARMS
-- ============================================================
INSERT INTO Farms (Name, Location, Capacity, CurrentStock, EmployeeCount, CertStatus, CertExpiry) VALUES
('Gazipur North',      'Gazipur, Dhaka',     12000, 9360, 8,  'Valid',         '2026-01-15'),
('Savar Unit',         'Savar, Dhaka',        10000, 6200, 6,  'Expiring Soon', DATEADD(DAY, 14, GETDATE())),
('Tongi Farm',         'Tongi, Gazipur',      8000,  7280, 5,  'Valid',         '2027-03-20'),
('Narayanganj Unit',   'Narayanganj',         15000, 6750, 10, 'Valid',         '2026-06-30');
GO

-- ============================================================
-- SUPPLIERS
-- ============================================================
INSERT INTO Suppliers (Name, ContactPerson, Phone, Category) VALUES
('ACI Agri',        'Rahman Saheb',    '01711000001', 'Feed'),
('Nourish Poultry', 'Karim Bhai',      '01811000002', 'Feed'),
('Opsonin Pharma',  'Dr. Sajjad',      '01911000003', 'Medicine'),
('Renata Ltd',      'Hasan Saheb',     '01711000004', 'Medicine'),
('Eskayef BD',      'Rina Apa',        '01811000005', 'Supplement'),
('Walton BD',       'Arif Vai',        '01911000006', 'Equipment'),
('Aftab Agro',      'Siraj Bhai',      '01711000007', 'Biosecurity');
GO

-- ============================================================
-- BATCHES
-- ============================================================
INSERT INTO Batches (BatchCode, Breed, FarmId, InitialCount, CurrentCount, MortalityCount, PlacementDate, HealthStatus, AverageWeightKg, TotalFeedKg) VALUES
('B-07', 'Cobb 500',     1, 8200, 8188, 12, DATEADD(DAY,-21,GETDATE()), 'Vaccination Due', 0.820, 3200.00),
('B-08', 'Ross 308',     2, 7500, 7492, 8,  DATEADD(DAY,-35,GETDATE()), 'Healthy',         1.740, 9800.00),
('B-09', 'Sonali',       3, 6800, 6800, 0,  DATEADD(DAY,-14,GETDATE()), 'Healthy',         0.420, 1500.00),
('B-10', 'Cobb 500',     1, 9100, 9078, 22, DATEADD(DAY,-42,GETDATE()), 'Ready for Sale',  2.080, 24500.00),
('B-11', 'Layer White',  4, 5400, 5400, 0,  DATEADD(DAY,-8, GETDATE()), 'Healthy',         0.180, 480.00);
GO

-- ============================================================
-- WEIGHT RECORDS (for B-08, Id=2)
-- ============================================================
INSERT INTO WeightRecords (BatchId, RecordDate, AgeInDays, AvgWeightKg, SampleSize) VALUES
(2, DATEADD(DAY,-35,GETDATE()),  1,  0.040, 50),
(2, DATEADD(DAY,-28,GETDATE()),  7,  0.190, 50),
(2, DATEADD(DAY,-21,GETDATE()), 14,  0.420, 50),
(2, DATEADD(DAY,-14,GETDATE()), 21,  0.840, 50),
(2, DATEADD(DAY,-7, GETDATE()), 28,  1.350, 50),
(2, GETDATE(),                  35,  1.740, 50);
GO

-- ============================================================
-- VACCINATION RECORDS
-- ============================================================
INSERT INTO VaccinationRecords (BatchId, VaccineName, DueDate, Status) VALUES
(1, 'Newcastle Disease (ND)',      DATEADD(DAY,-2,GETDATE()), 'Overdue'),
(2, 'Gumboro (IBD)',               DATEADD(DAY, 3,GETDATE()), 'Planned'),
(3, 'Avian Influenza H5N1',        DATEADD(DAY, 5,GETDATE()), 'Planned'),
(4, 'Marek''s Disease',            DATEADD(DAY, 7,GETDATE()), 'Planned'),
(1, 'Infectious Bronchitis (IB)',  DATEADD(DAY,-30,GETDATE()), 'Done');
GO
UPDATE VaccinationRecords SET CompletedDate = DATEADD(DAY,-30,GETDATE()) WHERE VaccineName = 'Infectious Bronchitis (IB)';
GO

-- ============================================================
-- DISEASE CASES
-- ============================================================
INSERT INTO DiseaseCases (BatchId, DiseaseName, ReportedDate, AffectedCount, Treatment, Status) VALUES
(2, 'Respiratory Syndrome',  DATEADD(DAY,-3,GETDATE()), 12, 'Enrofloxacin 10mg/L water — Day 3 of 5', 'Under Treatment'),
(1, 'Heat Stress (Suspected)',DATEADD(DAY,-1,GETDATE()),  0, 'Fan speed increased, Vitamin C supplement', 'Monitoring');
GO

-- ============================================================
-- INVENTORY ITEMS
-- ============================================================
INSERT INTO InventoryItems (Name, Category, Quantity, Unit, MinimumLevel, SupplierId, UnitCost, ExpiryDate) VALUES
('Broiler Starter Feed',     'Feed',       12400, 'kg',   5000, 1, 5.00,  NULL),
('Broiler Grower Feed',      'Feed',        8200, 'kg',   8000, 1, 5.00,  NULL),
('Broiler Finisher Feed',    'Feed',        6000, 'kg',   4000, 2, 5.20,  NULL),
('Newcastle Vaccine (ND)',   'Medicine',    1200, 'dose', 2000, 3, 5.00,  DATEADD(DAY, 30, GETDATE())),
('Enrofloxacin 10%',         'Medicine',       5, 'L',      2, 4, 2000.00, DATEADD(DAY, 90, GETDATE())),
('Vitamin E / Selenium',     'Supplement',   800, 'g',    1000, 5, 4.00,  DATEADD(DAY, 60, GETDATE())),
('Exhaust Fan (36 inch)',     'Equipment',      3, 'pcs',    2, 6, 6000.00, NULL),
('Drinking Nipple',          'Equipment',    500, 'pcs',  200, 6, 15.00,  NULL),
('Disinfectant (Virkon)',     'Biosecurity',   10, 'kg',     5, 7, 800.00, NULL);
GO

-- ============================================================
-- CUSTOMERS
-- ============================================================
INSERT INTO Customers (Name, Phone, Address, CustomerType) VALUES
('Karwan Bazar Traders',  '01711111111', 'Karwan Bazar, Dhaka',   'Wholesaler'),
('Mirpur Poultry Shop',   '01811111112', 'Mirpur-10, Dhaka',      'Retailer'),
('Dhaka Egg Wholesale',   '01911111113', 'Tejgaon, Dhaka',        'Wholesaler'),
('Narshingdi Agro',       '01711111114', 'Narshingdi',            'Wholesaler'),
('Comilla Retailer',      '01911111115', 'Comilla Sadar',         'Retailer');
GO

-- ============================================================
-- ORDERS
-- ============================================================
INSERT INTO Orders (OrderCode, CustomerId, FarmId, Product, Quantity, Unit, Grade, UnitPrice, OrderDate, DeliveryDate, Status, PaymentStatus) VALUES
('ORD-0241', 1, 1, 'Live Broiler',  800,  'kg',  'A',       153, GETDATE(),             DATEADD(DAY,1,GETDATE()),  'Out for Delivery', 'Unpaid'),
('ORD-0240', 2, 2, 'Live Broiler',  500,  'kg',  'A',       153, DATEADD(DAY,-1,GETDATE()), DATEADD(DAY,-1,GETDATE()), 'Delivered',        'Paid'),
('ORD-0239', 3, 4, 'Table Eggs',   2000,  'pcs', 'Premium',  14, DATEADD(DAY,-2,GETDATE()), DATEADD(DAY,-2,GETDATE()), 'Delivered',        'Paid'),
('ORD-0238', 4, 1, 'Live Broiler', 1200,  'kg',  'B',       144, DATEADD(DAY,-1,GETDATE()), DATEADD(DAY,3,GETDATE()),  'Processing',       'Unpaid'),
('ORD-0237', 5, 3, 'Live Broiler',  350,  'kg',  'A',       153, GETDATE(),             DATEADD(DAY,5,GETDATE()),  'Pending',          'Unpaid');
GO

-- ============================================================
-- FINANCIAL TRANSACTIONS
-- ============================================================
INSERT INTO Transactions (TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference) VALUES
(GETDATE(),             'Sale',     'Live Broiler', 'Live broiler — Karwan Bazar Traders',    122400,  'Paid',    1, 'ORD-0241'),
(DATEADD(DAY,-1,GETDATE()), 'Purchase', 'Feed',         'Broiler Grower Feed — ACI Agri 10 ton',  -50000,  'Paid',    1, 'PO-1120'),
(DATEADD(DAY,-2,GETDATE()), 'Expense',  'Utilities',    'Electricity bill — Gazipur North',        -12800,  'Paid',    1, NULL),
(DATEADD(DAY,-3,GETDATE()), 'Sale',     'Table Eggs',   'Eggs — Dhaka Egg Wholesale, Tejgaon',      68000,  'Pending', 4, 'ORD-0239'),
(DATEADD(DAY,-4,GETDATE()), 'Purchase', 'Medicine',     'Vaccines — Opsonin Pharma',                -8400,  'Paid',    2, NULL),
(DATEADD(DAY,-5,GETDATE()), 'Sale',     'Live Broiler', 'Live broiler — Mirpur Poultry Shop',       76500,  'Paid',    2, 'ORD-0240'),
(DATEADD(DAY,-6,GETDATE()), 'Expense',  'Labour',       'Monthly wages — All Farms',              -124000,  'Paid',    NULL, NULL),
(DATEADD(DAY,-7,GETDATE()), 'Purchase', 'Feed',         'Starter feed — Nourish Poultry',          -31200,  'Paid',    3, NULL),
(DATEADD(DAY,-10,GETDATE()),'Sale',     'Live Broiler', 'Batch B-05 harvest — Narshingdi Agro',   185600,  'Paid',    1, NULL),
(DATEADD(DAY,-12,GETDATE()),'Expense',  'Utilities',    'Electricity — Savar Unit',                -9200,  'Paid',    2, NULL),
(DATEADD(DAY,-15,GETDATE()),'Purchase', 'Equipment',    'Exhaust fans 3 units — Walton',           -18000,  'Paid',    3, NULL),
(DATEADD(DAY,-20,GETDATE()),'Sale',     'Live Broiler', 'Live broiler — Comilla Retailer',          53550,  'Paid',    3, 'ORD-0237');
GO

-- ============================================================
-- EMPLOYEES
-- ============================================================
INSERT INTO Employees (FullName, Role, FarmId, Phone, Status, MonthlySalary, JoinDate) VALUES
('Karim Uddin',        'Farm Manager',        1, '01711000101', 'On Duty', 22000, '2020-03-01'),
('Dr. Rahim Hossain',  'Veterinarian',        NULL,'01811000102','On Duty', 35000, '2019-06-15'),
('Sumaiya Akter',      'Inventory Manager',   1, '01911000103', 'On Duty', 18000, '2021-01-10'),
('Jamal Mia',          'Farm Worker',         2, '01711000104', 'On Leave',10000, '2022-05-20'),
('Nasrin Begum',       'Lab Technician',      3, '01811000105', 'On Duty', 16000, '2021-08-01'),
('Arif Khan',          'Driver / Delivery',   NULL,'01911000106','On Duty', 12000, '2020-11-05'),
('Sohel Rana',         'Farm Worker',         4, '01711000107', 'On Duty', 10000, '2023-02-14'),
('Mitu Begum',         'Farm Worker',         2, '01811000108', 'On Duty', 10000, '2023-07-01'),
('Rubel Hossain',      'Security Guard',      3, '01911000109', 'On Duty',  9000, '2022-01-15');
GO

-- ============================================================
-- MORTALITY LOGS
-- ============================================================
INSERT INTO MortalityLogs (BatchId, LogDate, Count, Cause) VALUES
(1, DATEADD(DAY,-1,GETDATE()), 12, 'Heat Stress'),
(2, DATEADD(DAY,-2,GETDATE()),  8, 'Respiratory'),
(4, DATEADD(DAY,-3,GETDATE()), 22, 'Unknown');
GO

-- ============================================================
-- CERTIFICATIONS
-- ============================================================
INSERT INTO Certifications (FarmId, CertName, IssuedDate, ExpiryDate, Status) VALUES
(1, 'BSTI Food Safety',   '2024-01-15', '2026-01-15', 'Valid'),
(2, 'BSTI Food Safety',   '2023-11-01', DATEADD(DAY,14,GETDATE()), 'Expiring Soon'),
(3, 'ISO 22000',          '2024-03-20', '2027-03-20', 'Valid'),
(4, 'BSTI Food Safety',   '2024-06-30', '2026-06-30', 'Valid');
GO

PRINT 'Seed data inserted successfully.';
GO

-- Quick verify
SELECT 'Farms'       AS [Table], COUNT(*) AS Rows FROM Farms       UNION ALL
SELECT 'Batches',          COUNT(*) FROM Batches                    UNION ALL
SELECT 'Employees',        COUNT(*) FROM Employees                  UNION ALL
SELECT 'InventoryItems',   COUNT(*) FROM InventoryItems             UNION ALL
SELECT 'Orders',           COUNT(*) FROM Orders                     UNION ALL
SELECT 'Transactions',     COUNT(*) FROM Transactions               UNION ALL
SELECT 'Vaccinations',     COUNT(*) FROM VaccinationRecords         UNION ALL
SELECT 'DiseaseCases',     COUNT(*) FROM DiseaseCases;
GO
