using System;
using System.Collections.Generic;
using System.Data;
using VodokanalManager.Models;

namespace VodokanalManager.Data.Repositories
{
    public interface ITariffRepository
    {
        List<Tariff> GetAll();
        Tariff GetActiveTariff(int serviceTypeId, DateTime period);
        void AddTariff(Tariff tariff);
    }

    public class TariffRepository : ITariffRepository
    {
        private readonly IDatabase _database;

        public TariffRepository(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public List<Tariff> GetAll()
        {
            var list = new List<Tariff>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        t.Id,
                        t.ServiceTypeId,
                        st.Name AS ServiceTypeName,
                        t.ValidFrom,
                        t.PriceKopPerM3
                    FROM Tariffs t
                    JOIN ServiceTypes st ON t.ServiceTypeId = st.Id
                    ORDER BY t.ServiceTypeId ASC, t.ValidFrom DESC;";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapTariff(reader));
                    }
                }
            }
            return list;
        }

        public Tariff GetActiveTariff(int serviceTypeId, DateTime period)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        t.Id,
                        t.ServiceTypeId,
                        st.Name AS ServiceTypeName,
                        t.ValidFrom,
                        t.PriceKopPerM3
                    FROM Tariffs t
                    JOIN ServiceTypes st ON t.ServiceTypeId = st.Id
                    WHERE t.ServiceTypeId = @serviceId 
                      AND t.ValidFrom <= @period
                    ORDER BY t.ValidFrom DESC
                    LIMIT 1;";

                AddParam(cmd, "@serviceId", serviceTypeId);
                AddParam(cmd, "@period", _database.Dialect.FormatDate(new DateTime(period.Year, period.Month, 1)));

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapTariff(reader);
                    }
                }
            }
            return null;
        }

        public void AddTariff(Tariff tariff)
        {
            if (tariff == null) throw new ArgumentNullException(nameof(tariff));
            if (tariff.PriceKopPerM3 <= 0)
                throw new ArgumentException("Тариф должен быть строго больше нуля.", nameof(tariff.PriceKopPerM3));

            DateTime normalizedDate = new DateTime(tariff.ValidFrom.Year, tariff.ValidFrom.Month, 1);

            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Tariffs (ServiceTypeId, ValidFrom, PriceKopPerM3)
                    VALUES (@serviceId, @validFrom, @price);";

                AddParam(cmd, "@serviceId", tariff.ServiceTypeId);
                AddParam(cmd, "@validFrom", _database.Dialect.FormatDate(normalizedDate));
                AddParam(cmd, "@price", tariff.PriceKopPerM3);

                cmd.ExecuteNonQuery();
            }
        }

        private static Tariff MapTariff(IDataRecord reader)
        {
            return new Tariff
            {
                Id = Convert.ToInt32(reader["Id"]),
                ServiceTypeId = Convert.ToInt32(reader["ServiceTypeId"]),
                ServiceTypeName = reader["ServiceTypeName"].ToString(),
                ValidFrom = DateTime.Parse(reader["ValidFrom"].ToString()),
                PriceKopPerM3 = Convert.ToInt32(reader["PriceKopPerM3"])
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
