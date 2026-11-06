-- Схема базы данных MS SQL Server для МУП "Водоканал-Сервис"
-- Совместимость с SQL Server 2014+

-- Служебная информация о приложении и версии схемы
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'AppInfo')
BEGIN
    CREATE TABLE AppInfo (
        Name NVARCHAR(100) NOT NULL PRIMARY KEY,
        SchemaVersion INT NOT NULL
    );
END

-- Пользовательские настройки приложения
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'AppSettings')
BEGIN
    CREATE TABLE AppSettings (
        SettingKey NVARCHAR(100) NOT NULL PRIMARY KEY,
        SettingValue NVARCHAR(MAX) NOT NULL
    );
END

-- Роли пользователей
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Roles')
BEGIN
    CREATE TABLE Roles (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END

-- Типы абонентов
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'SubscriberTypes')
BEGIN
    CREATE TABLE SubscriberTypes (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END

-- Абоненты
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

-- Пользователи
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

-- Виды коммунальных услуг
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'ServiceTypes')
BEGIN
    CREATE TABLE ServiceTypes (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END

-- Приборы учёта (счётчики)
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

-- Показания приборов учёта
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

-- Тарифы на водоснабжение
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

-- Способы оплаты
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'PaymentMethods')
BEGIN
    CREATE TABLE PaymentMethods (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END

-- Платежи
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

-- Индексы
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Subscribers_AccountNo')
    CREATE NONCLUSTERED INDEX IX_Subscribers_AccountNo ON Subscribers(AccountNo);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Subscribers_Name')
    CREATE NONCLUSTERED INDEX IX_Subscribers_Name ON Subscribers(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Meters_SubscriberId')
    CREATE NONCLUSTERED INDEX IX_Meters_SubscriberId ON Meters(SubscriberId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MeterReadings_MeterId')
    CREATE NONCLUSTERED INDEX IX_MeterReadings_MeterId ON MeterReadings(MeterId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MeterReadings_Period')
    CREATE NONCLUSTERED INDEX IX_MeterReadings_Period ON MeterReadings(Period);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Payments_SubscriberId')
    CREATE NONCLUSTERED INDEX IX_Payments_SubscriberId ON Payments(SubscriberId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Payments_PaymentDate')
    CREATE NONCLUSTERED INDEX IX_Payments_PaymentDate ON Payments(PaymentDate);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Tariffs_Service_ValidFrom')
    CREATE NONCLUSTERED INDEX IX_Tariffs_Service_ValidFrom ON Tariffs(ServiceTypeId, ValidFrom);
