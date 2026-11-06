-- Представления базы данных SQLite для расчёта расхода, начислений и баланса абонентов

-- 1. Представление расхода по каждому показанию прибора учёта
-- Первое показание счётчика считается начальным (расход = 0)
CREATE VIEW IF NOT EXISTS vw_Consumption AS
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
        WHEN LAG(r.ValueL) OVER (PARTITION BY r.MeterId ORDER BY r.Period) IS NULL THEN 0
        ELSE (r.ValueL - LAG(r.ValueL) OVER (PARTITION BY r.MeterId ORDER BY r.Period))
    END AS ConsumptionL,
    CASE 
        WHEN LAG(r.ValueL) OVER (PARTITION BY r.MeterId ORDER BY r.Period) IS NULL THEN 1 
        ELSE 0 
    END AS IsInitialReading
FROM MeterReadings r
JOIN Meters m ON r.MeterId = m.Id;

-- 2. Представление начислений с применением действующего тарифа
-- Формула начисления: (расход в литрах * тариф в копейках за м3 + 500) / 1000
CREATE VIEW IF NOT EXISTS vw_Charges AS
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
        WHEN c.IsInitialReading = 1 THEN 0
        WHEN t.PriceKopPerM3 IS NULL THEN 0
        ELSE CAST((c.ConsumptionL * t.PriceKopPerM3 + 500) / 1000 AS INTEGER)
    END AS AmountKop
FROM vw_Consumption c
LEFT JOIN Tariffs t ON t.ServiceTypeId = c.ServiceTypeId 
   AND t.ValidFrom = (
       SELECT MAX(t2.ValidFrom) 
       FROM Tariffs t2 
       WHERE t2.ServiceTypeId = c.ServiceTypeId 
         AND t2.ValidFrom <= c.Period
   );

-- 3. Представление баланса и задолженности абонентов
-- Долг = Сумма всех начислений - Сумма всех платежей
CREATE VIEW IF NOT EXISTS vw_SubscriberBalance AS
SELECT 
    s.Id AS SubscriberId,
    s.AccountNo,
    s.Name AS SubscriberName,
    s.SubscriberTypeId,
    st.Name AS SubscriberTypeName,
    s.Address,
    s.Phone,
    s.IsActive,
    COALESCE(ch.TotalChargedKop, 0) AS TotalChargedKop,
    COALESCE(p.TotalPaidKop, 0) AS TotalPaidKop,
    (COALESCE(ch.TotalChargedKop, 0) - COALESCE(p.TotalPaidKop, 0)) AS DebtKop
FROM Subscribers s
JOIN SubscriberTypes st ON s.SubscriberTypeId = st.Id
LEFT JOIN (
    SELECT SubscriberId, SUM(AmountKop) AS TotalChargedKop
    FROM vw_Charges
    GROUP BY SubscriberId
) ch ON s.Id = ch.SubscriberId
LEFT JOIN (
    SELECT SubscriberId, SUM(AmountKop) AS TotalPaidKop
    FROM Payments
    GROUP BY SubscriberId
) p ON s.Id = p.SubscriberId;
