

IF DB_ID(N'VodokanalDB') IS NULL 
    CREATE DATABASE VodokanalDB COLLATE Cyrillic_General_CI_AS;
GO

USE VodokanalDB;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'AppInfo')
BEGIN
    CREATE TABLE AppInfo (
        Name NVARCHAR(100) NOT NULL PRIMARY KEY,
        SchemaVersion INT NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'AppSettings')
BEGIN
    CREATE TABLE AppSettings (
        SettingKey NVARCHAR(100) NOT NULL PRIMARY KEY,
        SettingValue NVARCHAR(MAX) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Roles')
BEGIN
    CREATE TABLE Roles (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'SubscriberTypes')
BEGIN
    CREATE TABLE SubscriberTypes (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Subscribers')
BEGIN
    CREATE TABLE Subscribers (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AccountNo NVARCHAR(50) NOT NULL CONSTRAINT UQ_Subscribers_AccountNo UNIQUE,
        Name NVARCHAR(200) NOT NULL,
        SubscriberTypeId INT NOT NULL CONSTRAINT FK_Subscribers_SubscriberTypes REFERENCES SubscriberTypes(Id),
        Address NVARCHAR(300) NOT NULL,
        Phone NVARCHAR(50) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Subscribers_IsActive DEFAULT 1
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Users')
BEGIN
    CREATE TABLE Users (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Login NVARCHAR(100) NOT NULL CONSTRAINT UQ_Users_Login UNIQUE,
        PasswordHash NVARCHAR(256) NOT NULL,
        Salt NVARCHAR(100) NOT NULL,
        FullName NVARCHAR(200) NOT NULL,
        RoleId INT NOT NULL CONSTRAINT FK_Users_Roles REFERENCES Roles(Id),
        IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
        SubscriberId INT NULL CONSTRAINT FK_Users_Subscribers REFERENCES Subscribers(Id) ON DELETE SET NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'ServiceTypes')
BEGIN
    CREATE TABLE ServiceTypes (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Meters')
BEGIN
    CREATE TABLE Meters (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SubscriberId INT NOT NULL CONSTRAINT FK_Meters_Subscribers REFERENCES Subscribers(Id) ON DELETE CASCADE,
        ServiceTypeId INT NOT NULL CONSTRAINT FK_Meters_ServiceTypes REFERENCES ServiceTypes(Id),
        SerialNo NVARCHAR(100) NOT NULL CONSTRAINT UQ_Meters_SerialNo UNIQUE,
        InstallDate DATE NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Meters_IsActive DEFAULT 1
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'MeterReadings')
BEGIN
    CREATE TABLE MeterReadings (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MeterId INT NOT NULL CONSTRAINT FK_MeterReadings_Meters REFERENCES Meters(Id) ON DELETE CASCADE,
        Period DATE NOT NULL,
        ValueL BIGINT NOT NULL CONSTRAINT CK_MeterReadings_ValueL CHECK (ValueL >= 0),
        UserId INT NULL CONSTRAINT FK_MeterReadings_Users REFERENCES Users(Id) ON DELETE SET NULL,
        CONSTRAINT UQ_MeterReadings_Meter_Period UNIQUE (MeterId, Period)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Tariffs')
BEGIN
    CREATE TABLE Tariffs (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ServiceTypeId INT NOT NULL CONSTRAINT FK_Tariffs_ServiceTypes REFERENCES ServiceTypes(Id),
        ValidFrom DATE NOT NULL,
        PriceKopPerM3 INT NOT NULL CONSTRAINT CK_Tariffs_Price CHECK (PriceKopPerM3 > 0),
        CONSTRAINT UQ_Tariffs_Service_Date UNIQUE (ServiceTypeId, ValidFrom)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'PaymentMethods')
BEGIN
    CREATE TABLE PaymentMethods (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Payments')
BEGIN
    CREATE TABLE Payments (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SubscriberId INT NOT NULL CONSTRAINT FK_Payments_Subscribers REFERENCES Subscribers(Id) ON DELETE CASCADE,
        PaymentDate DATE NOT NULL,
        AmountKop BIGINT NOT NULL CONSTRAINT CK_Payments_Amount CHECK (AmountKop > 0),
        PaymentMethodId INT NOT NULL CONSTRAINT FK_Payments_PaymentMethods REFERENCES PaymentMethods(Id),
        UserId INT NULL CONSTRAINT FK_Payments_Users REFERENCES Users(Id) ON DELETE SET NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Subscribers_AccountNo')
    CREATE NONCLUSTERED INDEX IX_Subscribers_AccountNo ON Subscribers(AccountNo);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Subscribers_Name')
    CREATE NONCLUSTERED INDEX IX_Subscribers_Name ON Subscribers(Name);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Meters_SubscriberId')
    CREATE NONCLUSTERED INDEX IX_Meters_SubscriberId ON Meters(SubscriberId);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MeterReadings_MeterId')
    CREATE NONCLUSTERED INDEX IX_MeterReadings_MeterId ON MeterReadings(MeterId);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MeterReadings_Period')
    CREATE NONCLUSTERED INDEX IX_MeterReadings_Period ON MeterReadings(Period);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Payments_SubscriberId')
    CREATE NONCLUSTERED INDEX IX_Payments_SubscriberId ON Payments(SubscriberId);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Payments_PaymentDate')
    CREATE NONCLUSTERED INDEX IX_Payments_PaymentDate ON Payments(PaymentDate);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Tariffs_Service_ValidFrom')
    CREATE NONCLUSTERED INDEX IX_Tariffs_Service_ValidFrom ON Tariffs(ServiceTypeId, ValidFrom);
GO

IF OBJECT_ID(N'dbo.vw_SubscriberBalance', N'V') IS NOT NULL
    DROP VIEW dbo.vw_SubscriberBalance;
GO

IF OBJECT_ID(N'dbo.vw_Charges', N'V') IS NOT NULL
    DROP VIEW dbo.vw_Charges;
GO

IF OBJECT_ID(N'dbo.vw_Consumption', N'V') IS NOT NULL
    DROP VIEW dbo.vw_Consumption;
GO

CREATE VIEW dbo.vw_Consumption AS
SELECT 
    r.Id AS ReadingId,
    r.MeterId,
    m.SubscriberId,
    m.ServiceTypeId,
    m.SerialNo AS MeterSerialNo,
    r.Period,
    r.ValueL,
    LAG(r.ValueL) OVER (PARTITION BY r.MeterId ORDER BY r.Period) AS PrevValueL,
    CASE 
        WHEN LAG(r.ValueL) OVER (PARTITION BY r.MeterId ORDER BY r.Period) IS NULL THEN CAST(0 AS BIGINT)
        ELSE (r.ValueL - LAG(r.ValueL) OVER (PARTITION BY r.MeterId ORDER BY r.Period))
    END AS ConsumptionL,
    CASE 
        WHEN LAG(r.ValueL) OVER (PARTITION BY r.MeterId ORDER BY r.Period) IS NULL THEN 1 
        ELSE 0 
    END AS IsInitialReading
FROM dbo.MeterReadings r
JOIN dbo.Meters m ON r.MeterId = m.Id;
GO

CREATE VIEW dbo.vw_Charges AS
SELECT 
    c.ReadingId,
    c.MeterId,
    c.SubscriberId,
    c.ServiceTypeId,
    c.MeterSerialNo,
    c.Period,
    c.ValueL,
    c.PrevValueL,
    c.ConsumptionL,
    c.IsInitialReading,
    t.PriceKopPerM3,
    t.Id AS TariffId,
    CASE 
        WHEN c.IsInitialReading = 1 THEN CAST(0 AS BIGINT)
        WHEN t.PriceKopPerM3 IS NULL THEN CAST(0 AS BIGINT)
        ELSE CAST((c.ConsumptionL * CAST(t.PriceKopPerM3 AS BIGINT) + 500) / 1000 AS BIGINT)
    END AS AmountKop
FROM dbo.vw_Consumption c
OUTER APPLY (
    SELECT TOP (1) tar.Id, tar.PriceKopPerM3
    FROM dbo.Tariffs tar
    WHERE tar.ServiceTypeId = c.ServiceTypeId
      AND tar.ValidFrom <= c.Period
    ORDER BY tar.ValidFrom DESC
) t;
GO

CREATE VIEW dbo.vw_SubscriberBalance AS
SELECT 
    s.Id AS SubscriberId,
    s.AccountNo,
    s.Name AS SubscriberName,
    s.SubscriberTypeId,
    st.Name AS SubscriberTypeName,
    s.Address,
    s.Phone,
    s.IsActive,
    ISNULL(ch.TotalChargedKop, CAST(0 AS BIGINT)) AS TotalChargedKop,
    ISNULL(p.TotalPaidKop, CAST(0 AS BIGINT)) AS TotalPaidKop,
    (ISNULL(ch.TotalChargedKop, CAST(0 AS BIGINT)) - ISNULL(p.TotalPaidKop, CAST(0 AS BIGINT))) AS DebtKop
FROM dbo.Subscribers s
JOIN dbo.SubscriberTypes st ON s.SubscriberTypeId = st.Id
LEFT JOIN (
    SELECT SubscriberId, SUM(AmountKop) AS TotalChargedKop
    FROM dbo.vw_Charges
    GROUP BY SubscriberId
) ch ON s.Id = ch.SubscriberId
LEFT JOIN (
    SELECT SubscriberId, SUM(AmountKop) AS TotalPaidKop
    FROM dbo.Payments
    GROUP BY SubscriberId
) p ON s.Id = p.SubscriberId;
GO

IF NOT EXISTS (SELECT * FROM dbo.AppInfo WHERE Name = N'VodokanalManager')
    INSERT INTO dbo.AppInfo (Name, SchemaVersion) VALUES (N'VodokanalManager', 1);

IF NOT EXISTS (SELECT * FROM dbo.AppSettings WHERE SettingKey = N'AutoFillDemoData')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue) VALUES (N'AutoFillDemoData', N'1');

IF NOT EXISTS (SELECT * FROM dbo.AppSettings WHERE SettingKey = N'DatabaseMode')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue) VALUES (N'DatabaseMode', N'SqlServer');

IF NOT EXISTS (SELECT * FROM dbo.AppSettings WHERE SettingKey = N'AppTitle')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue) VALUES (N'AppTitle', N'МУП "Водоканал-Сервис" г. Канск');

IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 1) INSERT INTO dbo.Roles (Id, Name) VALUES (1, N'Администратор');
IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 2) INSERT INTO dbo.Roles (Id, Name) VALUES (2, N'Оператор');
IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 3) INSERT INTO dbo.Roles (Id, Name) VALUES (3, N'Кассир');
IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 4) INSERT INTO dbo.Roles (Id, Name) VALUES (4, N'Абонент');

IF NOT EXISTS (SELECT * FROM dbo.SubscriberTypes WHERE Id = 1) INSERT INTO dbo.SubscriberTypes (Id, Name) VALUES (1, N'Физическое лицо');
IF NOT EXISTS (SELECT * FROM dbo.SubscriberTypes WHERE Id = 2) INSERT INTO dbo.SubscriberTypes (Id, Name) VALUES (2, N'Организация');

IF NOT EXISTS (SELECT * FROM dbo.ServiceTypes WHERE Id = 1) INSERT INTO dbo.ServiceTypes (Id, Name) VALUES (1, N'Холодное водоснабжение');
IF NOT EXISTS (SELECT * FROM dbo.ServiceTypes WHERE Id = 2) INSERT INTO dbo.ServiceTypes (Id, Name) VALUES (2, N'Горячее водоснабжение');

IF NOT EXISTS (SELECT * FROM dbo.PaymentMethods WHERE Id = 1) INSERT INTO dbo.PaymentMethods (Id, Name) VALUES (1, N'Наличные');
IF NOT EXISTS (SELECT * FROM dbo.PaymentMethods WHERE Id = 2) INSERT INTO dbo.PaymentMethods (Id, Name) VALUES (2, N'Банковская карта');
IF NOT EXISTS (SELECT * FROM dbo.PaymentMethods WHERE Id = 3) INSERT INTO dbo.PaymentMethods (Id, Name) VALUES (3, N'Банковский перевод');

IF NOT EXISTS (SELECT * FROM dbo.Users WHERE Login = N'admin')
BEGIN
    INSERT INTO dbo.Users (Login, PasswordHash, Salt, FullName, RoleId, IsActive, SubscriberId)
    VALUES (N'admin', N'VuGeycb49ak2f12dyIjmjDCfJ/17J5utppF6dcGgslQ=', N'JTpn5IIfAH5rDZTTxChEWA==', N'Администратор системы', 1, 1, NULL);
END

IF NOT EXISTS (SELECT * FROM dbo.Users WHERE Login = N'operator')
BEGIN
    INSERT INTO dbo.Users (Login, PasswordHash, Salt, FullName, RoleId, IsActive, SubscriberId)
    VALUES (N'operator', N'96O2JM9agzx2ExZrzeVnvL0vg1Y52UCt0kWfXw4Geyo=', N'rK9UNyXrBCJou4Bu9xYykQ==', N'Михайлова Елена Сергеевна', 2, 1, NULL);
END

IF NOT EXISTS (SELECT * FROM dbo.Users WHERE Login = N'kassir')
BEGIN
    INSERT INTO dbo.Users (Login, PasswordHash, Salt, FullName, RoleId, IsActive, SubscriberId)
    VALUES (N'kassir', N'BOdxSUsALLoK7+E2FnSBfbq2zVvBEbPSiOCnYUOEYpg=', N'6E8fWmJGaW1kbVOWo36w6Q==', N'Петрова Ольга Владимировна', 3, 1, NULL);
END

IF NOT EXISTS (SELECT * FROM dbo.Tariffs WHERE ServiceTypeId = 1 AND ValidFrom = '2025-01-01')
    INSERT INTO dbo.Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES (1, '2025-01-01', 3860);

IF NOT EXISTS (SELECT * FROM dbo.Tariffs WHERE ServiceTypeId = 2 AND ValidFrom = '2025-01-01')
    INSERT INTO dbo.Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES (2, '2025-01-01', 15420);

IF NOT EXISTS (SELECT * FROM dbo.Tariffs WHERE ServiceTypeId = 1 AND ValidFrom = '2025-07-01')
    INSERT INTO dbo.Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES (1, '2025-07-01', 4210);

IF NOT EXISTS (SELECT * FROM dbo.Tariffs WHERE ServiceTypeId = 2 AND ValidFrom = '2025-07-01')
    INSERT INTO dbo.Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES (2, '2025-07-01', 16800);

IF NOT EXISTS (SELECT * FROM dbo.Tariffs WHERE ServiceTypeId = 1 AND ValidFrom = '2026-01-01')
    INSERT INTO dbo.Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES (1, '2026-01-01', 4530);

IF NOT EXISTS (SELECT * FROM dbo.Tariffs WHERE ServiceTypeId = 2 AND ValidFrom = '2026-01-01')
    INSERT INTO dbo.Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES (2, '2026-01-01', 17950);
GO

WITH ChargesCTE AS (
    SELECT SubscriberId, SUM(AmountKop) AS TotalChargedKop
    FROM dbo.vw_Charges GROUP BY SubscriberId
),
PaymentsCTE AS (
    SELECT SubscriberId, SUM(AmountKop) AS TotalPaidKop, MAX(PaymentDate) AS LastPaymentDate
    FROM dbo.Payments GROUP BY SubscriberId
),
DebtorsCTE AS (
    SELECT 
        s.Id AS SubscriberId, s.AccountNo, s.Name AS SubscriberName, st.Name AS SubscriberTypeName, s.Address, s.Phone,
        ISNULL(c.TotalChargedKop, CAST(0 AS BIGINT)) AS TotalChargedKop,
        ISNULL(p.TotalPaidKop, CAST(0 AS BIGINT)) AS TotalPaidKop,
        (ISNULL(c.TotalChargedKop, CAST(0 AS BIGINT)) - ISNULL(p.TotalPaidKop, CAST(0 AS BIGINT))) AS DebtKop,
        p.LastPaymentDate
    FROM dbo.Subscribers s
    JOIN dbo.SubscriberTypes st ON s.SubscriberTypeId = st.Id
    LEFT JOIN ChargesCTE c ON s.Id = c.SubscriberId
    LEFT JOIN PaymentsCTE p ON s.Id = p.SubscriberId
    WHERE (ISNULL(c.TotalChargedKop, CAST(0 AS BIGINT)) - ISNULL(p.TotalPaidKop, CAST(0 AS BIGINT))) > 0
)
SELECT 
    RANK() OVER (ORDER BY DebtKop DESC) AS DebtRank,
    AccountNo AS [Лицевой счет], SubscriberName AS [Абонент], SubscriberTypeName AS [Тип абонента],
    CAST(DebtKop / 100.0 AS DECIMAL(15, 2)) AS [Задолженность, руб.],
    LastPaymentDate AS [Дата последней оплаты]
FROM DebtorsCTE
ORDER BY DebtRank ASC;
GO

WITH ConsumptionHistoryCTE AS (
    SELECT 
        c.ReadingId, c.MeterId, c.SubscriberId, c.ServiceTypeId, c.MeterSerialNo, c.Period, c.ConsumptionL,
        AVG(CAST(c.ConsumptionL AS FLOAT)) OVER (
            PARTITION BY c.MeterId ORDER BY c.Period ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
        ) AS AvgPrev3MonthsL,
        COUNT(c.ReadingId) OVER (
            PARTITION BY c.MeterId ORDER BY c.Period ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
        ) AS PrevPeriodsCount
    FROM dbo.vw_Consumption c
    WHERE c.IsInitialReading = 0
)
SELECT 
    s.AccountNo AS [Лицевой счет], s.Name AS [Абонент], st.Name AS [Услуга], h.MeterSerialNo AS [Номер счетчика],
    CONVERT(VARCHAR(7), h.Period, 120) AS [Расчетный месяц],
    CAST(h.ConsumptionL / 1000.0 AS DECIMAL(10, 3)) AS [Текущий расход, м3],
    CAST(ROUND(h.AvgPrev3MonthsL / 1000.0, 3) AS DECIMAL(10, 3)) AS [Средний расход за 3 мес., м3],
    CAST(ROUND(h.ConsumptionL / h.AvgPrev3MonthsL, 2) AS DECIMAL(6, 2)) AS [Коэффициент превышения]
FROM ConsumptionHistoryCTE h
JOIN dbo.Subscribers s ON h.SubscriberId = s.Id
JOIN dbo.ServiceTypes st ON h.ServiceTypeId = st.Id
WHERE h.PrevPeriodsCount >= 2 AND h.AvgPrev3MonthsL > 0 AND (h.ConsumptionL / h.AvgPrev3MonthsL) >= 1.50
ORDER BY (h.ConsumptionL / h.AvgPrev3MonthsL) DESC;
GO
