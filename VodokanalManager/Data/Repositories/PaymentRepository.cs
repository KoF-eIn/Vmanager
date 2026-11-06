using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using VodokanalManager.Models;

namespace VodokanalManager.Data.Repositories
{
    public interface IPaymentRepository
    {
        List<Payment> GetAll(int? subscriberId = null, DateTime? fromDate = null, DateTime? toDate = null);
        void AddPayment(Payment payment);
        decimal GetPaymentsSumForPeriod(int subscriberId, DateTime fromDate, DateTime toDate);
    }

    public class PaymentRepository : IPaymentRepository
    {
        private readonly IDatabase _database;

        public PaymentRepository(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public List<Payment> GetAll(int? subscriberId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var list = new List<Payment>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                var sb = new StringBuilder(@"
                    SELECT 
                        p.Id,
                        p.SubscriberId,
                        s.AccountNo AS SubscriberAccountNo,
                        s.Name AS SubscriberName,
                        s.Address AS SubscriberAddress,
                        p.PaymentDate,
                        p.AmountKop,
                        p.PaymentMethodId,
                        pm.Name AS PaymentMethodName,
                        p.UserId,
                        u.FullName AS UserName
                    FROM Payments p
                    JOIN Subscribers s ON p.SubscriberId = s.Id
                    JOIN PaymentMethods pm ON p.PaymentMethodId = pm.Id
                    LEFT JOIN Users u ON p.UserId = u.Id
                    WHERE 1=1 ");

                if (subscriberId.HasValue && subscriberId.Value > 0)
                {
                    sb.Append(" AND p.SubscriberId = @subId ");
                    AddParam(cmd, "@subId", subscriberId.Value);
                }

                if (fromDate.HasValue)
                {
                    sb.Append(" AND p.PaymentDate >= @from ");
                    AddParam(cmd, "@from", _database.Dialect.FormatDate(fromDate.Value));
                }

                if (toDate.HasValue)
                {
                    sb.Append(" AND p.PaymentDate <= @to ");
                    AddParam(cmd, "@to", _database.Dialect.FormatDate(toDate.Value));
                }

                sb.Append(" ORDER BY p.PaymentDate DESC, p.Id DESC;");
                cmd.CommandText = sb.ToString();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapPayment(reader));
                    }
                }
            }
            return list;
        }

        public void AddPayment(Payment payment)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (payment.AmountKop <= 0)
                throw new ArgumentException("Сумма платежа должна быть строго больше нуля.", nameof(payment.AmountKop));

            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Payments (SubscriberId, PaymentDate, AmountKop, PaymentMethodId, UserId)
                    VALUES (@subId, @date, @amount, @methodId, @userId);";

                AddParam(cmd, "@subId", payment.SubscriberId);
                AddParam(cmd, "@date", _database.Dialect.FormatDate(payment.PaymentDate));
                AddParam(cmd, "@amount", payment.AmountKop);
                AddParam(cmd, "@methodId", payment.PaymentMethodId);
                AddParam(cmd, "@userId", (object)payment.UserId ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        public decimal GetPaymentsSumForPeriod(int subscriberId, DateTime fromDate, DateTime toDate)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COALESCE(SUM(AmountKop), 0)
                    FROM Payments
                    WHERE SubscriberId = @subId 
                      AND PaymentDate >= @from 
                      AND PaymentDate <= @to;";

                AddParam(cmd, "@subId", subscriberId);
                AddParam(cmd, "@from", _database.Dialect.FormatDate(fromDate));
                AddParam(cmd, "@to", _database.Dialect.FormatDate(toDate));

                long sumKop = Convert.ToInt64(cmd.ExecuteScalar());
                return sumKop / 100m;
            }
        }

        private static Payment MapPayment(IDataRecord reader)
        {
            return new Payment
            {
                Id = Convert.ToInt32(reader["Id"]),
                SubscriberId = Convert.ToInt32(reader["SubscriberId"]),
                SubscriberAccountNo = reader["SubscriberAccountNo"].ToString(),
                SubscriberName = reader["SubscriberName"].ToString(),
                SubscriberAddress = reader["SubscriberAddress"].ToString(),
                PaymentDate = DateTime.Parse(reader["PaymentDate"].ToString()),
                AmountKop = Convert.ToInt64(reader["AmountKop"]),
                PaymentMethodId = Convert.ToInt32(reader["PaymentMethodId"]),
                PaymentMethodName = reader["PaymentMethodName"].ToString(),
                UserId = reader["UserId"] is DBNull ? (int?)null : Convert.ToInt32(reader["UserId"]),
                UserName = reader["UserName"] is DBNull ? "Касса" : reader["UserName"].ToString()
            };
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
