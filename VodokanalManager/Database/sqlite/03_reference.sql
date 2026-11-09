

INSERT OR IGNORE INTO AppInfo (Name, SchemaVersion) 
VALUES ('VodokanalManager', 1);

INSERT OR IGNORE INTO AppSettings (SettingKey, SettingValue) 
VALUES 
    ('AutoFillDemoData', '1'),
    ('DatabaseMode', 'Sqlite'),
    ('AppTitle', 'ООО "Водоканал-Сервис" г. Канск');

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

INSERT OR IGNORE INTO Users (Id, Login, PasswordHash, Salt, InitialPassword, FullName, RoleId, IsActive, SubscriberId)
VALUES (1, 'admin', 'VuGeycb49ak2f12dyIjmjDCfJ/17J5utppF6dcGgslQ=', 'JTpn5IIfAH5rDZTTxChEWA==', 'admin', 'Администратор системы', 1, 1, NULL);

INSERT OR IGNORE INTO Users (Id, Login, PasswordHash, Salt, InitialPassword, FullName, RoleId, IsActive, SubscriberId)
VALUES (2, 'operator', 'vnPjnw6ojYstymgHOXiwuUZk2ETZE8OGfgl74xcnH+w=', '9aYZy521Gnk75bXpbO0Uxg==', 'VbvJFdoGP', 'Михайлова Елена Сергеевна', 2, 1, NULL);

INSERT OR IGNORE INTO Users (Id, Login, PasswordHash, Salt, InitialPassword, FullName, RoleId, IsActive, SubscriberId)
VALUES (3, 'kassir', 'BOdxSUsALLoK7+E2FnSBfbq2zVvBEbPSiOCnYUOEYpg=', '6E8fWmJGaW1kbVOWo36w6Q==', 'kassir', 'Петрова Ольга Владимировна', 3, 1, NULL);

INSERT OR IGNORE INTO Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES 
    (1, '2025-01-01', 3860),
    (2, '2025-01-01', 15420);

INSERT OR IGNORE INTO Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3) VALUES 
    (1, '2025-07-01', 4210),
    (2, '2025-07-01', 16800);
