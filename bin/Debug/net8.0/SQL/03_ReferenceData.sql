-- ============================================================================
-- PoultryPro — STEP 3 : Reference / master data (idempotent)
-- Settings, default login accounts, breed standards, disease library,
-- vaccination templates and feed formulations.
-- Every block only inserts when the data is missing, so it is safe to re-run.
-- ============================================================================
USE PoultryProDB;
GO
SET NOCOUNT ON;
GO

-- ---------------------------------------------------------------------------
-- 1. APPLICATION SETTINGS
-- ---------------------------------------------------------------------------
DECLARE @s TABLE ([Key] NVARCHAR(80), [Value] NVARCHAR(500), GroupName NVARCHAR(50), Description NVARCHAR(300));
INSERT INTO @s VALUES
 ('Company.Name',            'PoultryPro Integrated Farms Ltd.', 'General',   'Organisation name shown on invoices and reports'),
 ('Company.Address',         'House 12, Road 7, Uttara, Dhaka-1230, Bangladesh', 'General', 'Postal address used on invoices'),
 ('Company.Phone',           '+880 1700-000000',                 'General',   'Contact phone on invoices'),
 ('Currency.Symbol',         N'৳',                               'General',   'Currency symbol'),
 ('Alert.MortalitySpikeFactor', '2.5',  'Analytics', 'Daily mortality greater than (factor x 7-day mean) raises a spike alert'),
 ('Alert.MortalityMinPct',   '0.30',    'Analytics', 'Minimum daily mortality % before a spike alert is raised'),
 ('Alert.THIWarning',        '80',      'Analytics', 'Temperature-Humidity Index at which heat-stress WARNING is raised'),
 ('Alert.THICritical',       '85',      'Analytics', 'Temperature-Humidity Index at which heat-stress CRITICAL is raised'),
 ('Alert.AmmoniaPpm',        '25',      'Analytics', 'Ammonia concentration (ppm) above which an air-quality alert is raised'),
 ('Alert.GrowthLagPct',      '12',      'Analytics', 'Body weight below breed standard by this % raises a growth-lag alert'),
 ('Alert.SyncStaleDays',     '3',       'Analytics', 'Field data not synced for this many days raises an alert'),
 ('Harvest.TargetWeightKg',  '2.0',     'Analytics', 'Default broiler market weight (kg) used by the harvest forecaster'),
 ('Carbon.FeedFactor',       '0.80',    'Sustainability', 'kg CO2e per kg of compound feed (cradle-to-farm-gate, indicative)'),
 ('Carbon.ChickFactor',      '0.12',    'Sustainability', 'kg CO2e per day-old chick placed (hatchery footprint, indicative)'),
 ('Carbon.ElectricityKwhPerBird', '0.35','Sustainability', 'kWh of electricity per bird per cycle (lighting, fans, brooding)'),
 ('Carbon.GridFactor',       '0.67',    'Sustainability', 'kg CO2e per kWh of grid electricity (Bangladesh grid, indicative)'),
 ('Carbon.ManureFactor',     '0.08',    'Sustainability', 'kg CO2e per bird per cycle from manure management (indicative)'),
 ('Visit.GeofenceRadiusM',   '300',     'Field',     'A visit check-in within this many metres (plus the reported GPS accuracy) of the farm coordinates counts as GPS-verified'),
 ('Api.MobileSyncKey',      'PP-MOBILE-2026-DEMO-KEY', 'Integration', 'Shared key the field-officer mobile app sends in the X-Api-Key header'),
 ('Price.BroilerPerKg',      '165',     'Market', 'Reference live broiler price per kg (BDT)'),
 ('Price.SonaliPerKg',       '260',     'Market', 'Reference live Sonali price per kg (BDT)'),
 ('Price.EggPerPiece',       '11.5',    'Market', 'Reference farm-gate egg price per piece (BDT)');

INSERT INTO dbo.AppSettings ([Key], [Value], GroupName, Description)
SELECT s.[Key], s.[Value], s.GroupName, s.Description
FROM @s s
WHERE NOT EXISTS (SELECT 1 FROM dbo.AppSettings a WHERE a.[Key] = s.[Key]);
GO

-- ---------------------------------------------------------------------------
-- 2. DEFAULT LOGIN ACCOUNTS (only when the user table is empty)
--    Password hash = SHA-256(password + 'PoultryProSalt2025') in lower-case hex.
--    All demo passwords are listed in README.md — change them after first login.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.AppUsers)
BEGIN
    INSERT INTO dbo.AppUsers (FullName, Username, Email, Phone, PasswordHash, Role) VALUES
    ('System Administrator', 'admin',      'admin@poultrypro.local',      '01700000001', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Admin@123PoultryProSalt2025'), 2)),      'Admin'),
    ('Farhana Rahman',       'manager',    'manager@poultrypro.local',    '01700000002', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Manager@123PoultryProSalt2025'), 2)),    'Manager'),
    ('Dr. Mahmudul Hasan',   'vet',        'vet@poultrypro.local',        '01700000003', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Vet@123PoultryProSalt2025'), 2)),        'Veterinarian'),
    ('Nusrat Jahan',         'accountant', 'accounts@poultrypro.local',   '01700000004', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Account@123PoultryProSalt2025'), 2)),    'Accountant'),
    ('Guest Researcher',     'researcher', 'research@poultrypro.local',   '01700000005', LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Research@123PoultryProSalt2025'), 2)),   'Viewer');
    PRINT 'Default user accounts created.';
END
GO

-- ---------------------------------------------------------------------------
-- 3. BREED PERFORMANCE STANDARDS (indicative weekly reference curves)
--    Values are rounded from publicly available breed performance objectives
--    and local Sonali field data; replace them with the official breed-manual
--    tables for publication-grade analysis (Analytics > Breed Standards).
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.BreedStandards)
BEGIN
    INSERT INTO dbo.BreedStandards (Breed, AgeDay, TargetWeightGm, DailyFeedGm, TargetFCR, TargetCumMortPct) VALUES
    ('Cobb 500', 0,   42,   0,   0.000, 0.00),
    ('Cobb 500', 7,  196,  39,   0.860, 1.00),
    ('Cobb 500', 14, 524,  84,   1.040, 1.50),
    ('Cobb 500', 21, 1035, 128,  1.210, 2.00),
    ('Cobb 500', 28, 1680, 171,  1.360, 2.50),
    ('Cobb 500', 35, 2373, 203,  1.500, 3.00),
    ('Cobb 500', 42, 3058, 224,  1.630, 3.50),
    ('Cobb 500', 49, 3700, 234,  1.760, 4.00),

    ('Ross 308', 0,   44,   0,   0.000, 0.00),
    ('Ross 308', 7,  190,  37,   0.880, 1.00),
    ('Ross 308', 14, 500,  80,   1.070, 1.50),
    ('Ross 308', 21, 985,  124,  1.240, 2.00),
    ('Ross 308', 28, 1620, 166,  1.390, 2.50),
    ('Ross 308', 35, 2300, 198,  1.530, 3.00),
    ('Ross 308', 42, 2980, 220,  1.660, 3.50),
    ('Ross 308', 49, 3620, 231,  1.790, 4.00),

    ('Sonali',   0,   35,   0,   0.000, 0.00),
    ('Sonali',   7,   75,  15,   1.300, 1.00),
    ('Sonali',   14, 150,  28,   1.550, 1.50),
    ('Sonali',   21, 250,  40,   1.750, 2.00),
    ('Sonali',   28, 370,  50,   1.900, 2.50),
    ('Sonali',   35, 490,  58,   2.050, 3.00),
    ('Sonali',   42, 610,  65,   2.200, 3.30),
    ('Sonali',   49, 720,  70,   2.350, 3.60),
    ('Sonali',   56, 830,  74,   2.480, 3.90),
    ('Sonali',   63, 920,  78,   2.600, 4.20);

    UPDATE dbo.BreedStandards SET CumFeedGm = ROUND(TargetWeightGm * TargetFCR, 0);
    PRINT 'Breed standards loaded.';
END
GO

-- ---------------------------------------------------------------------------
-- 4. DISEASE LIBRARY (knowledge base)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.DiseaseLibrary)
BEGIN
    INSERT INTO dbo.DiseaseLibrary (Name, LocalName, Category, Severity, IsZoonotic, IsNotifiable, AgeGroup, MortalityRange, Symptoms, Transmission, Prevention, Treatment) VALUES
    ('Newcastle Disease (ND)', 'Ranikhet', 'Viral', 'Critical', 0, 1, 'All ages', '50-100%',
     'Gasping, coughing, greenish diarrhoea, twisted neck (torticollis), paralysis, sudden drop in egg production, swelling around eyes.',
     'Direct contact with infected birds, contaminated feed/water, equipment, people and wild birds; airborne over short distance.',
     'Strict vaccination programme (ND live vaccine day 5-7, booster day 18-21), biosecurity, restrict visitors, disinfect vehicles.',
     'No specific treatment. Supportive care: vitamins and electrolytes; antibiotics only to control secondary infection. Report to DLS.'),
    ('Infectious Bursal Disease (Gumboro)', 'Gumboro', 'Viral', 'High', 0, 0, '3-6 weeks', '10-30%',
     'Depression, ruffled feathers, whitish watery diarrhoea, vent pecking, dehydration; enlarged haemorrhagic bursa at post-mortem.',
     'Highly contagious via faeces; virus survives months in litter and housing.',
     'Intermediate-plus IBD vaccine day 10-14 with booster; thorough cleaning and downtime between flocks.',
     'Supportive therapy with electrolytes and multivitamins; reduce stress; control secondary E. coli infection.'),
    ('Highly Pathogenic Avian Influenza (HPAI H5N1)', 'Bird Flu', 'Viral', 'Critical', 1, 1, 'All ages', '90-100%',
     'Sudden death, cyanosis of comb and wattles, swelling of head, haemorrhages on shanks, respiratory distress, egg drop.',
     'Wild waterfowl, live-bird markets, contaminated equipment, people and vehicles.',
     'Strict biosecurity, keep away from ducks and wild birds, all-in all-out, immediate reporting of suspicious mortality.',
     'No treatment permitted. Notify Department of Livestock Services immediately; stamping-out and zoning per national policy.'),
    ('Coccidiosis', 'Rokto amasha', 'Parasitic', 'High', 0, 0, '3-8 weeks', '5-20%',
     'Bloody or mucoid diarrhoea, reduced feed intake, poor growth, pale combs, huddling.',
     'Ingestion of sporulated oocysts from wet, contaminated litter.',
     'Keep litter dry (< 25% moisture), coccidiostats in feed or anticoccidial vaccine, avoid water spillage.',
     'Amprolium or toltrazuril in drinking water for 3-5 days plus vitamin A and K supplementation.'),
    ('Chronic Respiratory Disease (Mycoplasma gallisepticum)', 'CRD', 'Bacterial', 'Medium', 0, 0, '4 weeks onwards', '5-10%',
     'Sneezing, nasal discharge, rales, swollen face, reduced feed intake and weight gain.',
     'Vertical (egg) transmission and horizontal by aerosol and contact.',
     'Source MG-free chicks, good ventilation, low ammonia, avoid multi-age sites.',
     'Tylosin, tiamulin or doxycycline as prescribed by a veterinarian.'),
    ('Colibacillosis (E. coli)', 'Coli', 'Bacterial', 'Medium', 0, 0, 'All ages', '5-20%',
     'Respiratory distress, depression, airsacculitis, pericarditis, perihepatitis, omphalitis in chicks.',
     'Faecal contamination of water, feed and dust; secondary to viral or environmental stress.',
     'Clean water lines, chlorinate water, control dust and ammonia, good hatchery hygiene.',
     'Antibiotic according to sensitivity test; improve management.'),
    ('Fowl Cholera (Pasteurellosis)', 'Kolera', 'Bacterial', 'High', 0, 0, 'Adults / layers', '20-50%',
     'Sudden death, fever, greenish diarrhoea, swollen wattles and joints, cyanosis.',
     'Carrier birds, rodents, wild birds, contaminated water.',
     'Rodent control, vaccination of layers, biosecurity.',
     'Sulphonamides or tetracyclines under veterinary supervision.'),
    ('Marek''s Disease', 'Marek', 'Viral', 'High', 0, 0, '6-20 weeks', '10-50%',
     'Paralysis of legs or wings (one leg forward one back), grey iris, weight loss, tumours in organs.',
     'Feather-follicle dander, airborne, persists in poultry house dust.',
     'Day-old vaccination at the hatchery (HVT / Rispens), cleaning of dust between flocks.',
     'No treatment; cull affected birds.'),
    ('Infectious Bronchitis (IB)', 'IB', 'Viral', 'Medium', 0, 0, 'All ages', '5-25%',
     'Coughing, sneezing, tracheal rales, watery eyes; misshapen thin-shelled eggs in layers; kidney damage with some strains.',
     'Airborne, highly contagious.',
     'Live IB vaccine (day 1 spray, booster), good ventilation.',
     'Supportive care, keep birds warm, electrolytes; antibiotics for secondary infection.'),
    ('Salmonellosis (Pullorum / Fowl Typhoid)', 'Salmonella', 'Bacterial', 'High', 1, 1, 'Chicks / all ages', '10-80%',
     'White pasty vent in chicks, depression, yellow diarrhoea, anaemia, drop in egg production.',
     'Vertical through eggs, contaminated feed, rodents and litter.',
     'Salmonella-free breeder source, feed hygiene, rodent control, vaccination of layers.',
     'Antibiotics reduce losses but carriers remain; consider depopulation of positive breeders.'),
    ('Fowl Pox', 'Bosonto', 'Viral', 'Low', 0, 0, 'Growers and adults', '1-5%',
     'Wart-like scabs on comb, wattles and face (dry form); yellow plaques in mouth and trachea (wet form).',
     'Mosquitoes and direct contact with scabs.',
     'Wing-web vaccination, mosquito control.',
     'No specific treatment; iodine on lesions, vitamins, prevent secondary infection.'),
    ('Heat Stress', 'Tap-chap', 'Environmental', 'High', 0, 0, 'Finishing broilers and layers', '1-30%',
     'Panting, wings held away from body, reduced feed intake, increased water intake, sudden deaths during hot afternoons.',
     'Environmental: temperature above 30 C combined with high humidity (THI above 78).',
     'Ventilation and fans, evaporative cooling, cool clean water, feed during cool hours, lower stocking density.',
     'Electrolytes, vitamin C and betaine in water, reduce handling, emergency cooling.'),
    ('Necrotic Enteritis', 'NE', 'Bacterial', 'Medium', 0, 0, '2-6 weeks', '5-30%',
     'Sudden death, dark diarrhoea, ruffled feathers; necrotic lesions in small intestine.',
     'Clostridium perfringens overgrowth, often following coccidiosis or diet change.',
     'Coccidiosis control, quality feed, probiotics, dry litter.',
     'Amoxicillin or bacitracin in water per veterinary prescription.'),
    ('Aspergillosis (Brooder Pneumonia)', 'Fungal', 'Fungal', 'Medium', 0, 0, 'Chicks 0-3 weeks', '5-50%',
     'Gasping, rapid breathing, drowsiness, yellow-green nodules in lungs and air sacs.',
     'Inhalation of spores from mouldy litter or feed.',
     'Use dry mould-free litter and feed, clean hatchery and brooders.',
     'No effective treatment in flocks; remove source and improve ventilation.');
    PRINT 'Disease library loaded.';
END
GO

-- ---------------------------------------------------------------------------
-- 5. VACCINATION SCHEDULE TEMPLATES
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.VaccineScheduleTemplates)
BEGIN
    DECLARE @t1 INT, @t2 INT, @t3 INT;

    INSERT INTO dbo.VaccineScheduleTemplates (Name, BatchType, Description)
    VALUES ('Broiler Standard Programme (BD)', 'Broiler', 'Common commercial broiler programme for Bangladesh field conditions.');
    SET @t1 = SCOPE_IDENTITY();
    INSERT INTO dbo.VaccineScheduleItems (TemplateId, AgeDay, VaccineName, Route, DoseMl, Notes) VALUES
    (@t1, 1,  'IB + ND (live, spray)',              'Spray',          0.03, 'At hatchery or on arrival'),
    (@t1, 5,  'Newcastle Disease (BCRDV)',          'Eye drop',       0.03, 'One drop per eye'),
    (@t1, 10, 'Gumboro / IBD (intermediate plus)',  'Drinking water', NULL, 'Withhold water 1-2 h before'),
    (@t1, 18, 'Gumboro / IBD booster',              'Drinking water', NULL, NULL),
    (@t1, 21, 'Newcastle Disease booster (Lasota)', 'Drinking water', NULL, NULL);

    INSERT INTO dbo.VaccineScheduleTemplates (Name, BatchType, Description)
    VALUES ('Sonali Programme', 'Sonali', 'Programme for Sonali (RIR x Fayoumi cross) reared 8-10 weeks.');
    SET @t2 = SCOPE_IDENTITY();
    INSERT INTO dbo.VaccineScheduleItems (TemplateId, AgeDay, VaccineName, Route, DoseMl, Notes) VALUES
    (@t2, 3,  'Newcastle Disease (BCRDV)',          'Eye drop',       0.03, NULL),
    (@t2, 10, 'Gumboro / IBD',                      'Drinking water', NULL, NULL),
    (@t2, 18, 'Gumboro / IBD booster',              'Drinking water', NULL, NULL),
    (@t2, 24, 'Newcastle Disease booster',          'Drinking water', NULL, NULL),
    (@t2, 35, 'Fowl Pox',                           'Wing web',       NULL, NULL),
    (@t2, 45, 'Newcastle Disease (RDV, killed)',    'Injection (SC)', 0.50, NULL);

    INSERT INTO dbo.VaccineScheduleTemplates (Name, BatchType, Description)
    VALUES ('Commercial Layer Programme', 'Layer', 'Rearing and pre-lay programme for commercial brown layers.');
    SET @t3 = SCOPE_IDENTITY();
    INSERT INTO dbo.VaccineScheduleItems (TemplateId, AgeDay, VaccineName, Route, DoseMl, Notes) VALUES
    (@t3, 1,   'Marek''s Disease (HVT)',            'Injection (SC)', 0.20, 'At hatchery'),
    (@t3, 5,   'ND + IB (live)',                    'Eye drop',       0.03, NULL),
    (@t3, 12,  'Gumboro / IBD',                     'Drinking water', NULL, NULL),
    (@t3, 21,  'Gumboro / IBD booster',             'Drinking water', NULL, NULL),
    (@t3, 28,  'ND + IB booster',                   'Drinking water', NULL, NULL),
    (@t3, 42,  'Fowl Pox',                          'Wing web',       NULL, NULL),
    (@t3, 56,  'Fowl Cholera (killed)',             'Injection (SC)', 0.50, NULL),
    (@t3, 84,  'Infectious Coryza',                 'Injection (IM)', 0.50, NULL),
    (@t3, 112, 'ND + IB + EDS (killed, pre-lay)',   'Injection (IM)', 0.50, 'Before point of lay');
    PRINT 'Vaccine templates loaded.';
END
GO

-- ---------------------------------------------------------------------------
-- 6. FEED FORMULATIONS
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.FeedFormulations)
BEGIN
    INSERT INTO dbo.FeedFormulations (Name, Stage, BatchType, FromAgeDay, ToAgeDay, CrudeProteinPct, EnergyKcalPerKg, CalciumPct, PhosphorusPct, LysinePct, CostPerKg, Ingredients, Notes) VALUES
    ('Broiler Pre-Starter Crumble', 'Pre-Starter', 'Broiler', 0, 10, 23.00, 3000, 0.96, 0.48, 1.44, 72.00, 'Maize 52%, Soybean meal 38%, Soy oil 4%, DCP 1.8%, Limestone 1.2%, Premix 1%, Salt 0.3%, DL-Methionine 0.35%, L-Lysine 0.25%', 'Crumble form'),
    ('Broiler Starter',             'Starter',     'Broiler', 11, 22, 21.50, 3050, 0.87, 0.44, 1.29, 68.00, 'Maize 56%, Soybean meal 34%, Soy oil 5%, DCP 1.6%, Limestone 1.1%, Premix 1%, Salt 0.3%', NULL),
    ('Broiler Grower',              'Grower',      'Broiler', 23, 32, 19.50, 3150, 0.78, 0.39, 1.15, 64.00, 'Maize 60%, Soybean meal 30%, Soy oil 6%, DCP 1.4%, Limestone 1%, Premix 1%, Salt 0.3%', 'Pellet'),
    ('Broiler Finisher',            'Finisher',    'Broiler', 33, 45, 18.00, 3200, 0.72, 0.36, 1.03, 62.00, 'Maize 63%, Soybean meal 27%, Soy oil 6.5%, DCP 1.3%, Limestone 0.9%, Premix 1%, Salt 0.3%', 'Pellet; observe drug withdrawal'),
    ('Sonali Grower',               'Grower',      'Sonali',  0, 70, 18.00, 2900, 0.90, 0.42, 0.95, 55.00, 'Maize 58%, Soybean meal 24%, Rice polish 12%, DCP 1.5%, Limestone 1.5%, Premix 1%, Salt 0.3%', NULL),
    ('Layer Pre-Lay',               'Pre-Layer',   'Layer',   112, 126, 17.50, 2750, 2.50, 0.45, 0.85, 58.00, 'Maize 58%, Soybean meal 25%, Rice polish 6%, Limestone 6%, DCP 1.5%, Premix 1%, Salt 0.3%', NULL),
    ('Layer Phase-1',               'Layer',       'Layer',   127, 350, 17.00, 2750, 4.00, 0.40, 0.80, 56.00, 'Maize 57%, Soybean meal 24%, Limestone (grit) 9.5%, Rice polish 5%, DCP 1.3%, Premix 1%, Salt 0.3%', 'Mash');
    PRINT 'Feed formulations loaded.';
END
GO

INSERT INTO dbo.SchemaVersions (ScriptName) VALUES ('03_ReferenceData.sql');
PRINT '03_ReferenceData.sql completed.';
GO
