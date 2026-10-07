-- ============================================================================
-- PoultryPro — Precision Poultry Farm Management & Research Platform
-- STEP 0 : Create the database
-- ----------------------------------------------------------------------------
-- You normally do NOT need to run these scripts by hand: the web application
-- creates and upgrades the database automatically on first start
-- (appsettings.json -> "Database": { "AutoSetup": true }).
--
-- Manual route (SSMS): run 00 -> 01 -> 02 -> 03 -> 04 in this order.
-- ============================================================================
USE master;
GO

IF DB_ID(N'PoultryProDB') IS NULL
BEGIN
    CREATE DATABASE PoultryProDB COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Database PoultryProDB created.';
END
ELSE
    PRINT 'Database PoultryProDB already exists - nothing to do.';
GO
