-- Представления для MS SQL Server (совместимость с SQL Server 2014+)

IF OBJECT_ID(N'dbo.vw_SubscriberBalance', N'V') IS NOT NULL
    DROP VIEW dbo.vw_SubscriberBalance;
GO

IF OBJECT_ID(N'dbo.vw_Charges', N'V') IS NOT NULL
    DROP VIEW dbo.vw_Charges;
GO

IF OBJECT_ID(N'dbo.vw_Consumption', N'V') IS NOT NULL
    DROP VIEW dbo.vw_Consumption;
GO

-- 1. Представление расхода
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

-- 2. Представление начислений
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

-- 3. Представление баланса абонентов
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
