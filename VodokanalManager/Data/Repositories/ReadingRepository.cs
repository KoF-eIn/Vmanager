using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using VodokanalManager.Models;

namespace VodokanalManager.Data.Repositories
{
    public interface IReadingRepository
    {
        List<MeterReading> GetAll(int? subscriberId = null, int? meterId = null, DateTime? period = null);
        MeterReading GetLastReading(int meterId);
        void AddReading(int meterId, DateTime period, long valueL, int? userId);
    }

    public class ReadingRepository : IReadingRepository
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");
        private readonly IDatabase _database;

        public ReadingRepository(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public List<MeterReading> GetAll(int? subscriberId = null, int? meterId = null, DateTime? period = null)
        {
            var list = new List<MeterReading>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                var sb = new StringBuilder(@"
                    SELECT 
                        ch.ReadingId AS Id,
                        ch.MeterId,
                        ch.MeterSerialNo,
                        ch.SubscriberId,
                        s.AccountNo AS SubscriberAccountNo,
                        s.Name AS SubscriberName,
                        ch.ServiceTypeId,
                        st.Name AS ServiceTypeName,
                        ch.Period,
                        ch.ValueL,
                        ch.PrevValueL,
                        ch.ConsumptionL,
                        ch.IsInitialReading,
                        ch.PriceKopPerM3,
                        ch.AmountKop,
                        r.UserId,
                        u.FullName AS UserName
                    FROM vw_Charges ch
                    JOIN MeterReadings r ON ch.ReadingId = r.Id
                    JOIN Subscribers s ON ch.SubscriberId = s.Id
                    JOIN ServiceTypes st ON ch.ServiceTypeId = st.Id
                    LEFT JOIN Users u ON r.UserId = u.Id
                    WHERE 1=1 ");

                if (subscriberId.HasValue && subscriberId.Value > 0)
                {
                    sb.Append(" AND ch.SubscriberId = @subId ");
                    AddParam(cmd, "@subId", subscriberId.Value);
                }

                if (meterId.HasValue && meterId.Value > 0)
                {
                    sb.Append(" AND ch.MeterId = @meterId ");
                    AddParam(cmd, "@meterId", meterId.Value);
                }

                if (period.HasValue)
                {
                    string periodStr = _database.Dialect.FormatDate(new DateTime(period.Value.Year, period.Value.Month, 1));
                    sb.Append(" AND ch.Period = @period ");
                    AddParam(cmd, "@period", periodStr);
                }

                sb.Append(" ORDER BY ch.Period DESC, s.Name COLLATE RU_CI ASC;");
                cmd.CommandText = sb.ToString();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapReading(reader));
                    }
                }
            }
            return list;
        }

        public MeterReading GetLastReading(int meterId)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        ch.ReadingId AS Id,
                        ch.MeterId,
                        ch.MeterSerialNo,
                        ch.SubscriberId,
                        s.AccountNo AS SubscriberAccountNo,
                        s.Name AS SubscriberName,
                        ch.ServiceTypeId,
                        st.Name AS ServiceTypeName,
                        ch.Period,
                        ch.ValueL,
                        ch.PrevValueL,
                        ch.ConsumptionL,
                        ch.IsInitialReading,
                        ch.PriceKopPerM3,
                        ch.AmountKop,
                        r.UserId,
                        u.FullName AS UserName
                    FROM vw_Charges ch
                    JOIN MeterReadings r ON ch.ReadingId = r.Id
                    JOIN Subscribers s ON ch.SubscriberId = s.Id
                    JOIN ServiceTypes st ON ch.ServiceTypeId = st.Id
                    LEFT JOIN Users u ON r.UserId = u.Id
                    WHERE ch.MeterId = @meterId
                    ORDER BY ch.Period DESC
                    LIMIT 1;";

                AddParam(cmd, "@meterId", meterId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapReading(reader);
                    }
                }
            }
            return null;
        }

        public void AddReading(int meterId, DateTime period, long valueL, int? userId)
        {
            if (valueL < 0)
                throw new ArgumentException("Показание не может быть отрицательным числом.", nameof(valueL));

            DateTime normalizedPeriod = new DateTime(period.Year, period.Month, 1);
            string periodStr = _database.Dialect.FormatDate(normalizedPeriod);

            using (var conn = _database.CreateConnection())
            using (var trans = conn.BeginTransaction())
            {
                try
                {
                    // 1. Проверка: показание за этот месяц уже существует?
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = "SELECT COUNT(*) FROM MeterReadings WHERE MeterId = @meterId AND Period = @period;";
                        AddParam(cmd, "@meterId", meterId);
                        AddParam(cmd, "@period", periodStr);

                        long existsCount = Convert.ToInt64(cmd.ExecuteScalar());
                        if (existsCount > 0)
                        {
                            throw new InvalidOperationException($"Показание для этого прибора за период {normalizedPeriod:MM.yyyy} уже внесено.");
                        }
                    }

                    // 2. Проверка: показание не меньше предыдущего
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            SELECT ValueL, Period 
                            FROM MeterReadings 
                            WHERE MeterId = @meterId AND Period < @period 
                            ORDER BY Period DESC 
                            LIMIT 1;";

                        AddParam(cmd, "@meterId", meterId);
                        AddParam(cmd, "@period", periodStr);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                long prevValueL = Convert.ToInt64(reader["ValueL"]);
                                if (valueL < prevValueL)
                                {
                                    decimal prevM3 = prevValueL / 1000m;
                                    decimal currentM3 = valueL / 1000m;
                                    throw new InvalidOperationException(
                                        $"Показание ({currentM3:N3} м³) не может быть меньше предыдущего зафиксированного ({prevM3:N3} м³).");
                                }
                            }
                        }
                    }

                    // 3. Проверка: действующий тариф на период существует?
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            SELECT COUNT(*) 
                            FROM Tariffs t
                            JOIN Meters m ON m.ServiceTypeId = t.ServiceTypeId
                            WHERE m.Id = @meterId AND t.ValidFrom <= @period;";

                        AddParam(cmd, "@meterId", meterId);
                        AddParam(cmd, "@period", periodStr);

                        long tariffsCount = Convert.ToInt64(cmd.ExecuteScalar());
                        if (tariffsCount == 0)
                        {
                            throw new InvalidOperationException(
                                $"На расчетный период {normalizedPeriod:MM.yyyy} отсутствует действующий тариф для данной услуги.");
                        }
                    }

                    // 4. Вставка показания
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            INSERT INTO MeterReadings (MeterId, Period, ValueL, UserId)
                            VALUES (@meterId, @period, @valueL, @userId);";

                        AddParam(cmd, "@meterId", meterId);
                        AddParam(cmd, "@period", periodStr);
                        AddParam(cmd, "@valueL", valueL);
                        AddParam(cmd, "@userId", (object)userId ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }

        private static MeterReading MapReading(IDataRecord reader)
        {
            return new MeterReading
            {
                Id = Convert.ToInt32(reader["Id"]),
                MeterId = Convert.ToInt32(reader["MeterId"]),
                MeterSerialNo = reader["MeterSerialNo"].ToString(),
                SubscriberId = Convert.ToInt32(reader["SubscriberId"]),
                SubscriberAccountNo = reader["SubscriberAccountNo"].ToString(),
                SubscriberName = reader["SubscriberName"].ToString(),
                ServiceTypeId = Convert.ToInt32(reader["ServiceTypeId"]),
                ServiceTypeName = reader["ServiceTypeName"].ToString(),
                Period = DateTime.Parse(reader["Period"].ToString()),
                ValueL = Convert.ToInt64(reader["ValueL"]),
                PrevValueL = reader["PrevValueL"] is DBNull ? (long?)null : Convert.ToInt64(reader["PrevValueL"]),
                ConsumptionL = Convert.ToInt64(reader["ConsumptionL"]),
                IsInitialReading = Convert.ToInt32(reader["IsInitialReading"]) == 1,
                PriceKopPerM3 = reader["PriceKopPerM3"] is DBNull ? 0 : Convert.ToInt32(reader["PriceKopPerM3"]),
                AmountKop = Convert.ToInt64(reader["AmountKop"]),
                UserId = reader["UserId"] is DBNull ? (int?)null : Convert.ToInt32(reader["UserId"]),
                UserName = reader["UserName"] is DBNull ? "Система" : reader["UserName"].ToString()
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
