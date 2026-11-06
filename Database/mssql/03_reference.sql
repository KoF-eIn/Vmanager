-- Справочные данные и базовые настройки для MS SQL Server (идемпотентно)

IF NOT EXISTS (SELECT * FROM dbo.AppInfo WHERE Name = N'VodokanalManager')
    INSERT INTO dbo.AppInfo (Name, SchemaVersion) VALUES (N'VodokanalManager', 1);

IF NOT EXISTS (SELECT * FROM dbo.AppSettings WHERE SettingKey = N'AutoFillDemoData')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue) VALUES (N'AutoFillDemoData', N'1');

IF NOT EXISTS (SELECT * FROM dbo.AppSettings WHERE SettingKey = N'DatabaseMode')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue) VALUES (N'DatabaseMode', N'SqlServer');

IF NOT EXISTS (SELECT * FROM dbo.AppSettings WHERE SettingKey = N'AppTitle')
    INSERT INTO dbo.AppSettings (SettingKey, SettingValue) VALUES (N'AppTitle', N'МУП "Водоканал-Сервис" г. Канск');

-- Роли
IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 1) INSERT INTO dbo.Roles (Id, Name) VALUES (1, N'Администратор');
IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 2) INSERT INTO dbo.Roles (Id, Name) VALUES (2, N'Оператор');
IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 3) INSERT INTO dbo.Roles (Id, Name) VALUES (3, N'Кассир');
IF NOT EXISTS (SELECT * FROM dbo.Roles WHERE Id = 4) INSERT INTO dbo.Roles (Id, Name) VALUES (4, N'Абонент');

-- Типы абонентов
IF NOT EXISTS (SELECT * FROM dbo.SubscriberTypes WHERE Id = 1) INSERT INTO dbo.SubscriberTypes (Id, Name) VALUES (1, N'Физическое лицо');
IF NOT EXISTS (SELECT * FROM dbo.SubscriberTypes WHERE Id = 2) INSERT INTO dbo.SubscriberTypes (Id, Name) VALUES (2, N'Организация');

-- Виды услуг
IF NOT EXISTS (SELECT * FROM dbo.ServiceTypes WHERE Id = 1) INSERT INTO dbo.ServiceTypes (Id, Name) VALUES (1, N'Холодное водоснабжение');
IF NOT EXISTS (SELECT * FROM dbo.ServiceTypes WHERE Id = 2) INSERT INTO dbo.ServiceTypes (Id, Name) VALUES (2, N'Горячее водоснабжение');

-- Способы оплаты
IF NOT EXISTS (SELECT * FROM dbo.PaymentMethods WHERE Id = 1) INSERT INTO dbo.PaymentMethods (Id, Name) VALUES (1, N'Наличные');
IF NOT EXISTS (SELECT * FROM dbo.PaymentMethods WHERE Id = 2) INSERT INTO dbo.PaymentMethods (Id, Name) VALUES (2, N'Банковская карта');
IF NOT EXISTS (SELECT * FROM dbo.PaymentMethods WHERE Id = 3) INSERT INTO dbo.PaymentMethods (Id, Name) VALUES (3, N'Банковский перевод');

-- Базовые пользователи (пароли PBKDF2 с солью)
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

-- Тарифы
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
