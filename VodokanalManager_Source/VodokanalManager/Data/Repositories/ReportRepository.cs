using System;
using System.Collections.Generic;
using System.Data;
using VodokanalManager.Models;

namespace VodokanalManager.Data.Repositories
{
    public interface IReportRepository
    {
        List<DebtorReportItem> GetDebtorsReport();
        List<SuspiciousConsumptionItem> GetSuspiciousConsumptionReport();
        List<MonthlyConsumptionItem> GetMonthlyConsumptionReport(DateTime fromDate, DateTime toDate);
        List<PaymentSummaryItem> GetPaymentSummaryReport(DateTime fromDate, DateTime toDate);
    }

    public class ReportRepository : IReportRepository
    {
        private readonly IDatabase _database;

        public ReportRepository(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public List<DebtorReportItem> GetDebtorsReport()
        {
            var list = new List<DebtorReportItem>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    WITH ChargesCTE AS (
                        SELECT SubscriberId, SUM(AmountKop) AS TotalChargedKop
                        FROM vw_Charges 
                        GROUP BY SubscriberId
                    ),
                    PaymentsCTE AS (
                        SELECT SubscriberId, SUM(AmountKop) AS TotalPaidKop, MAX(PaymentDate) AS LastPaymentDate
                        FROM Payments 
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
                            COALESCE(c.TotalChargedKop, 0) AS TotalChargedKop,
                            COALESCE(p.TotalPaidKop, 0) AS TotalPaidKop,
                            (COALESCE(c.TotalChargedKop, 0) - COALESCE(p.TotalPaidKop, 0)) AS DebtKop,
                            p.LastPaymentDate
                        FROM Subscribers s
                        JOIN SubscriberTypes st ON s.SubscriberTypeId = st.Id
                        LEFT JOIN ChargesCTE c ON s.Id = c.SubscriberId
                        LEFT JOIN PaymentsCTE p ON s.Id = p.SubscriberId
                        WHERE (COALESCE(c.TotalChargedKop, 0) - COALESCE(p.TotalPaidKop, 0)) > 0
                    )
                    SELECT 
                        RANK() OVER (ORDER BY DebtKop DESC) AS DebtRank,
                        SubscriberId,
                        AccountNo,
                        SubscriberName,
                        SubscriberTypeName,
                        Address,
                        Phone,
                        DebtKop,
                        TotalChargedKop,
                        TotalPaidKop,
                        LastPaymentDate
                    FROM DebtorsCTE
                    ORDER BY DebtRank ASC;";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new DebtorReportItem
                        {
                            Rank = Convert.ToInt32(reader["DebtRank"]),
                            SubscriberId = Convert.ToInt32(reader["SubscriberId"]),
                            AccountNo = reader["AccountNo"].ToString(),
                            Name = reader["SubscriberName"].ToString(),
                            SubscriberTypeName = reader["SubscriberTypeName"].ToString(),
                            Address = reader["Address"].ToString(),
                            Phone = reader["Phone"] is DBNull ? string.Empty : reader["Phone"].ToString(),
                            DebtKop = Convert.ToInt64(reader["DebtKop"]),
                            TotalChargedKop = Convert.ToInt64(reader["TotalChargedKop"]),
                            TotalPaidKop = Convert.ToInt64(reader["TotalPaidKop"])
                        };

                        if (!(reader["LastPaymentDate"] is DBNull))
                        {
                            item.LastPaymentDate = DateTime.Parse(reader["LastPaymentDate"].ToString());
                        }

                        list.Add(item);
                    }
                }
            }
            return list;
        }

        public List<SuspiciousConsumptionItem> GetSuspiciousConsumptionReport()
        {
            var list = new List<SuspiciousConsumptionItem>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
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
                            AVG(CAST(c.ConsumptionL AS REAL)) OVER (
                                PARTITION BY c.MeterId 
                                ORDER BY c.Period 
                                ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
                            ) AS AvgPrev3MonthsL,
                            COUNT(c.ReadingId) OVER (
                                PARTITION BY c.MeterId 
                                ORDER BY c.Period 
                                ROWS BETWEEN 3 PRECEDING AND 1 PRECEDING
                            ) AS PrevPeriodsCount
                        FROM vw_Consumption c
                        WHERE c.IsInitialReading = 0
                    )
                    SELECT 
                        h.ReadingId,
                        s.AccountNo,
                        s.Name AS SubscriberName,
                        st.Name AS ServiceTypeName,
                        h.MeterSerialNo,
                        h.Period,
                        h.ConsumptionL,
                        h.AvgPrev3MonthsL,
                        ROUND(CAST(h.ConsumptionL AS REAL) / h.AvgPrev3MonthsL, 2) AS Ratio
                    FROM ConsumptionHistoryCTE h
                    JOIN Subscribers s ON h.SubscriberId = s.Id
                    JOIN ServiceTypes st ON h.ServiceTypeId = st.Id
                    WHERE h.PrevPeriodsCount >= 2 
                      AND h.AvgPrev3MonthsL > 0 
                      AND (CAST(h.ConsumptionL AS REAL) / h.AvgPrev3MonthsL) >= 1.50
                    ORDER BY Ratio DESC;";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        double avgL = Convert.ToDouble(reader["AvgPrev3MonthsL"]);
                        double ratio = Convert.ToDouble(reader["Ratio"]);

                        list.Add(new SuspiciousConsumptionItem
                        {
                            ReadingId = Convert.ToInt32(reader["ReadingId"]),
                            AccountNo = reader["AccountNo"].ToString(),
                            SubscriberName = reader["SubscriberName"].ToString(),
                            ServiceTypeName = reader["ServiceTypeName"].ToString(),
                            MeterSerialNo = reader["MeterSerialNo"].ToString(),
                            Period = DateTime.Parse(reader["Period"].ToString()),
                            ConsumptionL = Convert.ToInt64(reader["ConsumptionL"]),
                            AvgPrev3MonthsM3 = (decimal)(avgL / 1000.0),
                            Ratio = ratio,
                            Note = ratio >= 2.0 ? "Критический скачок (>200%)" : "Существенное превышение (>150%)"
                        });
                    }
                }
            }
            return list;
        }

        public List<MonthlyConsumptionItem> GetMonthlyConsumptionReport(DateTime fromDate, DateTime toDate)
        {
            var list = new List<MonthlyConsumptionItem>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        c.Period,
                        COALESCE(SUM(CASE WHEN c.ServiceTypeId = 1 THEN c.ConsumptionL ELSE 0 END), 0) AS ColdWaterL,
                        COALESCE(SUM(CASE WHEN c.ServiceTypeId = 2 THEN c.ConsumptionL ELSE 0 END), 0) AS HotWaterL
                    FROM vw_Consumption c
                    WHERE c.IsInitialReading = 0
                      AND c.Period >= @from 
                      AND c.Period <= @to
                    GROUP BY c.Period
                    ORDER BY c.Period ASC;";

                AddParam(cmd, "@from", _database.Dialect.FormatDate(new DateTime(fromDate.Year, fromDate.Month, 1)));
                AddParam(cmd, "@to", _database.Dialect.FormatDate(new DateTime(toDate.Year, toDate.Month, 1)));

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime p = DateTime.Parse(reader["Period"].ToString());
                        long coldL = Convert.ToInt64(reader["ColdWaterL"]);
                        long hotL = Convert.ToInt64(reader["HotWaterL"]);

                        list.Add(new MonthlyConsumptionItem
                        {
                            PeriodKey = p.ToString("yyyy-MM"),
                            PeriodName = p.ToString("MMMM yyyy"),
                            ColdWaterM3 = coldL / 1000m,
                            HotWaterM3 = hotL / 1000m
                        });
                    }
                }
            }
            return list;
        }

        public List<PaymentSummaryItem> GetPaymentSummaryReport(DateTime fromDate, DateTime toDate)
        {
            var list = new List<PaymentSummaryItem>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        pm.Name AS PaymentMethodName,
                        COUNT(p.Id) AS PaymentsCount,
                        COALESCE(SUM(p.AmountKop), 0) AS TotalAmountKop
                    FROM PaymentMethods pm
                    LEFT JOIN Payments p ON pm.Id = p.PaymentMethodId 
                        AND p.PaymentDate >= @from 
                        AND p.PaymentDate <= @to
                    GROUP BY pm.Id, pm.Name
                    ORDER BY TotalAmountKop DESC;";

                AddParam(cmd, "@from", _database.Dialect.FormatDate(fromDate));
                AddParam(cmd, "@to", _database.Dialect.FormatDate(toDate));

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new PaymentSummaryItem
                        {
                            PaymentMethodName = reader["PaymentMethodName"].ToString(),
                            PaymentsCount = Convert.ToInt32(reader["PaymentsCount"]),
                            TotalAmountKop = Convert.ToInt64(reader["TotalAmountKop"])
                        });
                    }
                }
            }
            return list;
        }

        private static void AddParam(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
