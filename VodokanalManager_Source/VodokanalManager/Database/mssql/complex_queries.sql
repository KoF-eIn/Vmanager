

WITH ChargesCTE AS (
    
    SELECT 
        SubscriberId,
        SUM(AmountKop) AS TotalChargedKop
    FROM dbo.vw_Charges
    GROUP BY SubscriberId
),
PaymentsCTE AS (
    
    SELECT 
        SubscriberId,
        SUM(AmountKop) AS TotalPaidKop,
        MAX(PaymentDate) AS LastPaymentDate
    FROM dbo.Payments
    GROUP BY SubscriberId
),
DebtorsCTE AS (
    
    SELECT 
        s.Id AS SubscriberId,
        s.AccountNo,
        s.Name AS SubscriberName,
        st.Name AS SubscriberTypeName,
        s.Address,
        s.Phone,
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
    AccountNo AS [Лицевой счет],
    SubscriberName AS [Абонент],
    SubscriberTypeName AS [Тип абонента],
    Address AS [Адрес],
    Phone AS [Телефон],
    CAST(DebtKop / 100.0 AS DECIMAL(15, 2)) AS [Задолженность, руб.],
    CAST(TotalChargedKop / 100.0 AS DECIMAL(15, 2)) AS [Всего начислено, руб.],
    CAST(TotalPaidKop / 100.0 AS DECIMAL(15, 2)) AS [Всего оплачено, руб.],
    LastPaymentDate AS [Дата последней оплаты]
FROM DebtorsCTE
ORDER BY DebtRank ASC;

WITH ConsumptionHistoryCTE AS (
    
    SELECT 
        c.ReadingId,
        c.MeterId,
        c.SubscriberId,
        c.ServiceTypeId,
        c.MeterSerialNo,
        c.Period,
        c.ConsumptionL,
        c.IsInitialReading,
        AVG(CAST(c.ConsumptionL AS FLOAT)) OVER (
            PARTITION BY c.MeterId 
            ORDER BY c.Period 
            ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
        ) AS AvgPrev3MonthsL,
        COUNT(c.ReadingId) OVER (
            PARTITION BY c.MeterId 
            ORDER BY c.Period 
            ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
        ) AS PrevPeriodsCount
    FROM dbo.vw_Consumption c
    WHERE c.IsInitialReading = 0
)
SELECT 
    s.AccountNo AS [Лицевой счет],
    s.Name AS [Абонент],
    st.Name AS [Услуга],
    h.MeterSerialNo AS [Номер счетчика],
    CONVERT(VARCHAR(7), h.Period, 120) AS [Расчетный месяц],
    CAST(h.ConsumptionL / 1000.0 AS DECIMAL(10, 3)) AS [Текущий расход, м3],
    CAST(ROUND(h.AvgPrev3MonthsL / 1000.0, 3) AS DECIMAL(10, 3)) AS [Средний расход за 3 мес., м3],
    CAST(ROUND(h.ConsumptionL / h.AvgPrev3MonthsL, 2) AS DECIMAL(6, 2)) AS [Коэффициент превышения],
    N'Возможна скрытая утечка или неисправность прибора' AS [Примечание]
FROM ConsumptionHistoryCTE h
JOIN dbo.Subscribers s ON h.SubscriberId = s.Id
JOIN dbo.ServiceTypes st ON h.ServiceTypeId = st.Id
WHERE h.PrevPeriodsCount >= 2                
  AND h.AvgPrev3MonthsL > 0                  
  AND (h.ConsumptionL / h.AvgPrev3MonthsL) >= 1.50 
ORDER BY (h.ConsumptionL / h.AvgPrev3MonthsL) DESC;
