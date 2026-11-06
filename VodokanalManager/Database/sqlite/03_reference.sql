-- Справочные данные и базовые настройки (идемпотентная вставка)

INSERT OR IGNORE INTO AppInfo (Name, SchemaVersion) 
VALUES ('VodokanalManager', 1);

INSERT OR IGNORE INTO AppSettings (SettingKey, SettingValue) 
VALUES 
    ('AutoFillDemoData', '1'),
    ('DatabaseMode', 'Sqlite'),
    ('AppTitle', 'МУП "Водоканал-Сервис" г. Канск');

INSERT OR IGNORE INTO Roles (Id, Name) VALUES 
    (1, 'Администратор'),
    (2, 'Оператор'),
    (3, 'Кассир'),
    (4, 'Абонент');

INSERT OR IGNORE INTO SubscriberTypes (Id, Name) VALUES 
    (1, 'Физическое лицо'),
    (2, 'Организация');

INSERT OR IGNORE INTO ServiceTypes (Id, Name) VALUES 
    (1, 'Холодное водоснабжение'),
    (2, 'Горячее водоснабжение');

INSERT OR IGNORE INTO PaymentMethods (Id, Name) VALUES 
    (1, 'Наличные'),
    (2, 'Банковская карта'),
    (3, 'Банковский перевод');

-- Базовые пользователи (пароли захешированы PBKDF2 с солью)
-- admin / admin
INSERT OR IGNORE INTO Users (Id, Login, PasswordHash, Salt, FullName, RoleId, IsActive, SubscriberId)
VALUES (1, 'admin', 'VuGeycb49ak2f12dyIjmjDCfJ/17J5utppF6dcGgslQ=', 'JTpn5IIfAH5rDZTTxChEWA==', 'Администратор системы', 1, 1, NULL);

-- operator / operator
INSERT OR IGNORE INTO Users (Id, Login, PasswordHash, Salt, FullName, RoleId, IsActive, SubscriberId)
VALUES (2, 'operator', '96O2JM9agzx2ExZrzeVnvL0vg1Y52UCt0kWfXw4Geyo=', 'rK9UNyXrBCJou4Bu9xYykQ==', 'Михайлова Елена Сергеевна', 2, 1, NULL);

-- kassir / kassir
INSERT OR IGNORE INTO Users (Id, Login, PasswordHash, Salt, FullName, RoleId, IsActive, SubscriberId)
VALUES (3, 'kassir', 'BOdxSUsALLoK7+E2FnSBfbq2zVvBEbPSiOCnYUOEYpg=', '6E8fWmJGaW1kbVOWo36w6Q==', 'Петрова Ольга Владимировна', 3, 1, NULL);

-- Тарифы на водоснабжение (2 изменения тарифов)
-- С 01.01.2025: ХВС 38.60 руб/м3, ГВС 154.20 руб/м3
INSERT OR IGNORE INTO Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES 
    (1, '2025-01-01', 3860),
    (2, '2025-01-01', 15420);

-- С 01.07.2025: ХВС 42.10 руб/м3, ГВС 168.00 руб/м3
INSERT OR IGNORE INTO Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES 
    (1, '2025-07-01', 4210),
    (2, '2025-07-01', 16800);

-- С 01.01.2026: ХВС 45.30 руб/м3, ГВС 179.50 руб/м3
INSERT OR IGNORE INTO Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES 
    (1, '2026-01-01', 4530),
    (2, '2026-01-01', 17950);
