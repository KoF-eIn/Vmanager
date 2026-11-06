-- Обязательные сложные аналитические запросы к базе данных МУП "Водоканал-Сервис"
-- Разработано для демонстрации аналитических возможностей T-SQL (совместимость с SQL Server 2014+)

----------------------------------------------------------------------------------------------------
-- ЗАПРОС А: "Реестр должников водоканала с ранжированием по сумме задолженности"
-- Назначение:
--   Формирует список абонентов, имеющих положительную задолженность за потребленные услуги ХВС и ГВС.
--   Запрос использует общие табличные выражения (CTE) для предварительной агрегации всех начислений
--   и поступивших платежей, вычисляет точный размер долга, дату последнего внесённого платежа,
--   а также присваивает ранг (позицию) должника с помощью оконной функции RANK().
----------------------------------------------------------------------------------------------------

WITH ChargesCTE AS (
    -- Суммарные начисления по каждому абоненту за весь период учета
    SELECT 
        SubscriberId,
        SUM(AmountKop) AS TotalChargedKop
    FROM dbo.vw_Charges
    GROUP BY SubscriberId
),
PaymentsCTE AS (
    -- Суммарные поступившие оплаты и дата последнего платежа абонента
    SELECT 
        SubscriberId,
        SUM(AmountKop) AS TotalPaidKop,
        MAX(PaymentDate) AS LastPaymentDate
    FROM dbo.Payments
    GROUP BY SubscriberId
),
DebtorsCTE AS (
    -- Объединение начислений, платежей и карточки абонента
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


----------------------------------------------------------------------------------------------------
-- ЗАПРОС Б: "Выявление подозрительного расхода воды (аномальный перерасход)"
-- Назначение:
--   Выявляет случаи аномального всплеска водопотребления, которые могут свидетельствовать
--   об аварии на сетях абонента, скрытой утечке, поломке прибора учёта либо несанкционированном отборе.
--   Для каждого показания прибора учёта с помощью скользящего окна вычисляется средний расход
--   за 3 предшествующих месяца (ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING).
--   Критерий аномалии: расход текущего месяца превышает средний предшествующий более чем в 1.5 раза
--   (коэффициент превышения >= 1.50) при условии наличия базового расхода (> 0).
----------------------------------------------------------------------------------------------------

WITH ConsumptionHistoryCTE AS (
    -- Вычисление скользящего среднего расхода за 3 предшествующих периода
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
WHERE h.PrevPeriodsCount >= 2                -- Наличие хотя бы двух предшествующих месяцев для репрезентативности
  AND h.AvgPrev3MonthsL > 0                  -- Исключение деления на ноль при нулевом базовом расходе
  AND (h.ConsumptionL / h.AvgPrev3MonthsL) >= 1.50 -- Превышение в 1.5 раза и выше
ORDER BY (h.ConsumptionL / h.AvgPrev3MonthsL) DESC;
