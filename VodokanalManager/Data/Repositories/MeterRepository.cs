using System;
using System.Collections.Generic;
using System.Data;
using VodokanalManager.Models;

namespace VodokanalManager.Data.Repositories
{
    public interface IMeterRepository
    {
        List<Meter> GetAll();
        List<Meter> GetBySubscriberId(int subscriberId);
        Meter GetById(int id);
        void Add(Meter meter);
        void SetActive(int meterId, bool isActive);
    }

    public class MeterRepository : IMeterRepository
    {
        private readonly IDatabase _database;

        public MeterRepository(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public List<Meter> GetAll()
        {
            var list = new List<Meter>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        m.Id,
                        m.SubscriberId,
                        s.AccountNo AS SubscriberAccountNo,
                        s.Name AS SubscriberName,
                        m.ServiceTypeId,
                        st.Name AS ServiceTypeName,
                        m.SerialNo,
                        m.InstallDate,
                        m.IsActive,
                        (SELECT r.ValueL FROM MeterReadings r WHERE r.MeterId = m.Id ORDER BY r.Period DESC LIMIT 1) AS LastReadingL,
                        (SELECT r.Period FROM MeterReadings r WHERE r.MeterId = m.Id ORDER BY r.Period DESC LIMIT 1) AS LastReadingPeriod
                    FROM Meters m
                    JOIN Subscribers s ON m.SubscriberId = s.Id
                    JOIN ServiceTypes st ON m.ServiceTypeId = st.Id
                    ORDER BY s.Name COLLATE RU_CI ASC, m.InstallDate DESC;";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapMeter(reader));
                    }
                }
            }
            return list;
        }

        public List<Meter> GetBySubscriberId(int subscriberId)
        {
            var list = new List<Meter>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        m.Id,
                        m.SubscriberId,
                        s.AccountNo AS SubscriberAccountNo,
                        s.Name AS SubscriberName,
                        m.ServiceTypeId,
                        st.Name AS ServiceTypeName,
                        m.SerialNo,
                        m.InstallDate,
                        m.IsActive,
                        (SELECT r.ValueL FROM MeterReadings r WHERE r.MeterId = m.Id ORDER BY r.Period DESC LIMIT 1) AS LastReadingL,
                        (SELECT r.Period FROM MeterReadings r WHERE r.MeterId = m.Id ORDER BY r.Period DESC LIMIT 1) AS LastReadingPeriod
                    FROM Meters m
                    JOIN Subscribers s ON m.SubscriberId = s.Id
                    JOIN ServiceTypes st ON m.ServiceTypeId = st.Id
                    WHERE m.SubscriberId = @subId
                    ORDER BY m.ServiceTypeId ASC, m.InstallDate DESC;";

                AddParam(cmd, "@subId", subscriberId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapMeter(reader));
                    }
                }
            }
            return list;
        }

        public Meter GetById(int id)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        m.Id,
                        m.SubscriberId,
                        s.AccountNo AS SubscriberAccountNo,
                        s.Name AS SubscriberName,
                        m.ServiceTypeId,
                        st.Name AS ServiceTypeName,
                        m.SerialNo,
                        m.InstallDate,
                        m.IsActive,
                        (SELECT r.ValueL FROM MeterReadings r WHERE r.MeterId = m.Id ORDER BY r.Period DESC LIMIT 1) AS LastReadingL,
                        (SELECT r.Period FROM MeterReadings r WHERE r.MeterId = m.Id ORDER BY r.Period DESC LIMIT 1) AS LastReadingPeriod
                    FROM Meters m
                    JOIN Subscribers s ON m.SubscriberId = s.Id
                    JOIN ServiceTypes st ON m.ServiceTypeId = st.Id
                    WHERE m.Id = @id;";

                AddParam(cmd, "@id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapMeter(reader);
                    }
                }
            }
            return null;
        }

        public void Add(Meter meter)
        {
            if (meter == null) throw new ArgumentNullException(nameof(meter));

            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Meters (SubscriberId, ServiceTypeId, SerialNo, InstallDate, IsActive)
                    VALUES (@subId, @serviceId, @serial, @date, @active);";

                AddParam(cmd, "@subId", meter.SubscriberId);
                AddParam(cmd, "@serviceId", meter.ServiceTypeId);
                AddParam(cmd, "@serial", meter.SerialNo.Trim());
                AddParam(cmd, "@date", _database.Dialect.FormatDate(meter.InstallDate));
                AddParam(cmd, "@active", meter.IsActive ? 1 : 0);

                cmd.ExecuteNonQuery();
            }
        }

        public void SetActive(int meterId, bool isActive)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE Meters SET IsActive = @active WHERE Id = @id;";
                AddParam(cmd, "@id", meterId);
                AddParam(cmd, "@active", isActive ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        private static Meter MapMeter(IDataRecord reader)
        {
            var meter = new Meter
            {
                Id = Convert.ToInt32(reader["Id"]),
                SubscriberId = Convert.ToInt32(reader["SubscriberId"]),
                SubscriberAccountNo = reader["SubscriberAccountNo"].ToString(),
                SubscriberName = reader["SubscriberName"].ToString(),
                ServiceTypeId = Convert.ToInt32(reader["ServiceTypeId"]),
                ServiceTypeName = reader["ServiceTypeName"].ToString(),
                SerialNo = reader["SerialNo"].ToString(),
                InstallDate = DateTime.Parse(reader["InstallDate"].ToString()),
                IsActive = Convert.ToInt32(reader["IsActive"]) == 1
            };

            if (!(reader["LastReadingL"] is DBNull))
            {
                meter.LastReadingL = Convert.ToInt64(reader["LastReadingL"]);
            }

            if (!(reader["LastReadingPeriod"] is DBNull))
            {
                meter.LastReadingPeriod = DateTime.Parse(reader["LastReadingPeriod"].ToString());
            }

            return meter;
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
