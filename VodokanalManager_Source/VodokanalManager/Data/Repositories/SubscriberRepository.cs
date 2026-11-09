using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using VodokanalManager.Models;
using VodokanalManager.Services;

namespace VodokanalManager.Data.Repositories
{
    public interface ISubscriberRepository
    {
        List<Subscriber> GetAll(string search = null, int? subscriberTypeId = null, bool onlyWithDebt = false);
        Subscriber GetById(int id);
        Subscriber GetByAccountNo(string accountNo);
        int Add(Subscriber subscriber, IPasswordHasher hasher, string initialPassword = null);
        void Update(Subscriber subscriber, string newPassword = null, IPasswordHasher hasher = null);
        void SetActive(int subscriberId, bool isActive);
        void Delete(int subscriberId);
    }

    public class SubscriberRepository : ISubscriberRepository
    {
        private readonly IDatabase _database;

        public SubscriberRepository(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public List<Subscriber> GetAll(string search = null, int? subscriberTypeId = null, bool onlyWithDebt = false)
        {
            var list = new List<Subscriber>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                var sb = new StringBuilder(@"
                    SELECT 
                        b.SubscriberId AS Id,
                        b.AccountNo,
                        b.SubscriberName AS Name,
                        b.SubscriberTypeId,
                        b.SubscriberTypeName,
                        b.Address,
                        b.Phone,
                        b.IsActive,
                        b.TotalChargedKop,
                        b.TotalPaidKop,
                        b.DebtKop
                    FROM vw_SubscriberBalance b
                    WHERE 1=1 ");

                if (subscriberTypeId.HasValue && subscriberTypeId.Value > 0)
                {
                    sb.Append(" AND b.SubscriberTypeId = @subTypeId ");
                    AddParam(cmd, "@subTypeId", subscriberTypeId.Value);
                }

                if (onlyWithDebt)
                {
                    sb.Append(" AND b.DebtKop > 0 ");
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string pSearch = $"%{search.Trim()}%";
                    sb.Append(" AND (");
                    sb.Append(_database.Dialect.CaseInsensitiveLike("b.AccountNo", "@search"));
                    sb.Append(" OR ");
                    sb.Append(_database.Dialect.CaseInsensitiveLike("b.SubscriberName", "@search"));
                    sb.Append(" OR ");
                    sb.Append(_database.Dialect.CaseInsensitiveLike("b.Address", "@search"));
                    sb.Append(") ");
                    AddParam(cmd, "@search", pSearch);
                }

                sb.Append(" ORDER BY b.SubscriberName COLLATE RU_CI ASC;");
                cmd.CommandText = sb.ToString();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapSubscriber(reader));
                    }
                }
            }
            return list;
        }

        public Subscriber GetById(int id)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        b.SubscriberId AS Id,
                        b.AccountNo,
                        b.SubscriberName AS Name,
                        b.SubscriberTypeId,
                        b.SubscriberTypeName,
                        b.Address,
                        b.Phone,
                        b.IsActive,
                        b.TotalChargedKop,
                        b.TotalPaidKop,
                        b.DebtKop
                    FROM vw_SubscriberBalance b
                    WHERE b.SubscriberId = @id;";

                AddParam(cmd, "@id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapSubscriber(reader);
                    }
                }
            }
            return null;
        }

        public Subscriber GetByAccountNo(string accountNo)
        {
            if (string.IsNullOrWhiteSpace(accountNo)) return null;

            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        b.SubscriberId AS Id,
                        b.AccountNo,
                        b.SubscriberName AS Name,
                        b.SubscriberTypeId,
                        b.SubscriberTypeName,
                        b.Address,
                        b.Phone,
                        b.IsActive,
                        b.TotalChargedKop,
                        b.TotalPaidKop,
                        b.DebtKop
                    FROM vw_SubscriberBalance b
                    WHERE " + _database.Dialect.CaseInsensitiveLike("b.AccountNo", "@acc") + ";";

                AddParam(cmd, "@acc", accountNo.Trim());

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapSubscriber(reader);
                    }
                }
            }
            return null;
        }

        public int Add(Subscriber subscriber, IPasswordHasher hasher, string initialPassword = null)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            if (hasher == null) throw new ArgumentNullException(nameof(hasher));

            using (var conn = _database.CreateConnection())
            using (var trans = conn.BeginTransaction())
            {
                try
                {
                    
                    int newSubId;
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            INSERT INTO Subscribers (AccountNo, Name, SubscriberTypeId, Address, Phone, IsActive, IsDemo)
                            VALUES (@acc, @name, @typeId, @address, @phone, @isActive, 0);
                            " + _database.Dialect.GetLastInsertedIdQuery;

                        AddParam(cmd, "@acc", subscriber.AccountNo.Trim());
                        AddParam(cmd, "@name", subscriber.Name.Trim());
                        AddParam(cmd, "@typeId", subscriber.SubscriberTypeId);
                        AddParam(cmd, "@address", subscriber.Address.Trim());
                        AddParam(cmd, "@phone", string.IsNullOrWhiteSpace(subscriber.Phone) ? (object)DBNull.Value : subscriber.Phone.Trim());
                        AddParam(cmd, "@isActive", subscriber.IsActive ? 1 : 0);

                        newSubId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

string password = string.IsNullOrWhiteSpace(initialPassword) ? PasswordGenerator.Generate(8) : initialPassword.Trim();
                    string hash = hasher.HashPassword(password, out string salt);

                    using (var cmdUser = conn.CreateCommand())
                    {
                        cmdUser.Transaction = trans;
                        cmdUser.CommandText = @"
                            INSERT INTO Users (Login, PasswordHash, Salt, InitialPassword, FullName, RoleId, IsActive, SubscriberId, IsDemo)
                            VALUES (@login, @hash, @salt, @initPwd, @fullName, 4, @isActive, @subId, 0);";

                        AddParam(cmdUser, "@login", subscriber.AccountNo.Trim());
                        AddParam(cmdUser, "@hash", hash);
                        AddParam(cmdUser, "@salt", salt);
                        AddParam(cmdUser, "@initPwd", password);
                        AddParam(cmdUser, "@fullName", subscriber.Name.Trim());
                        AddParam(cmdUser, "@isActive", subscriber.IsActive ? 1 : 0);
                        AddParam(cmdUser, "@subId", newSubId);

                        cmdUser.ExecuteNonQuery();
                    }

                    trans.Commit();
                    subscriber.Id = newSubId;
                    return newSubId;
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }

        public void Update(Subscriber subscriber, string newPassword = null, IPasswordHasher hasher = null)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));

            using (var conn = _database.CreateConnection())
            using (var trans = conn.BeginTransaction())
            {
                try
                {
                    
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            UPDATE Subscribers
                            SET Name = @name,
                                SubscriberTypeId = @typeId,
                                Address = @address,
                                Phone = @phone,
                                IsActive = @isActive
                            WHERE Id = @id;";

                        AddParam(cmd, "@id", subscriber.Id);
                        AddParam(cmd, "@name", subscriber.Name.Trim());
                        AddParam(cmd, "@typeId", subscriber.SubscriberTypeId);
                        AddParam(cmd, "@address", subscriber.Address.Trim());
                        AddParam(cmd, "@phone", string.IsNullOrWhiteSpace(subscriber.Phone) ? (object)DBNull.Value : subscriber.Phone.Trim());
                        AddParam(cmd, "@isActive", subscriber.IsActive ? 1 : 0);

                        cmd.ExecuteNonQuery();
                    }

using (var cmdUser = conn.CreateCommand())
                    {
                        cmdUser.Transaction = trans;
                        if (!string.IsNullOrWhiteSpace(newPassword) && hasher != null)
                        {
                            string hash = hasher.HashPassword(newPassword.Trim(), out string salt);
                            cmdUser.CommandText = @"
                                UPDATE Users
                                SET FullName = @name,
                                    IsActive = @isActive,
                                    PasswordHash = @hash,
                                    Salt = @salt,
                                    InitialPassword = @initPwd
                                WHERE SubscriberId = @subId;";

                            AddParam(cmdUser, "@hash", hash);
                            AddParam(cmdUser, "@salt", salt);
                            AddParam(cmdUser, "@initPwd", newPassword.Trim());
                        }
                        else
                        {
                            cmdUser.CommandText = @"
                                UPDATE Users
                                SET FullName = @name,
                                    IsActive = @isActive
                                WHERE SubscriberId = @subId;";
                        }

                        AddParam(cmdUser, "@name", subscriber.Name.Trim());
                        AddParam(cmdUser, "@isActive", subscriber.IsActive ? 1 : 0);
                        AddParam(cmdUser, "@subId", subscriber.Id);

                        int updated = cmdUser.ExecuteNonQuery();
                        if (updated == 0)
                        {
                            
                            string pwd = !string.IsNullOrWhiteSpace(newPassword) ? newPassword.Trim() : PasswordGenerator.Generate(8);
                            string pwdHash = (hasher ?? new PasswordHasher()).HashPassword(pwd, out string pwdSalt);

                            using (var cmdInsertUser = conn.CreateCommand())
                            {
                                cmdInsertUser.Transaction = trans;
                                cmdInsertUser.CommandText = @"
                                    INSERT INTO Users (Login, PasswordHash, Salt, InitialPassword, FullName, RoleId, IsActive, SubscriberId, IsDemo)
                                    VALUES (@login, @hash, @salt, @initPwd, @fullName, 4, @isActive, @subId, 0);";

                                AddParam(cmdInsertUser, "@login", subscriber.AccountNo.Trim());
                                AddParam(cmdInsertUser, "@hash", pwdHash);
                                AddParam(cmdInsertUser, "@salt", pwdSalt);
                                AddParam(cmdInsertUser, "@initPwd", pwd);
                                AddParam(cmdInsertUser, "@fullName", subscriber.Name.Trim());
                                AddParam(cmdInsertUser, "@isActive", subscriber.IsActive ? 1 : 0);
                                AddParam(cmdInsertUser, "@subId", subscriber.Id);

                                cmdInsertUser.ExecuteNonQuery();
                            }
                        }
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

        public void Delete(int subscriberId)
        {
            using (var conn = _database.CreateConnection())
            using (var trans = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            DELETE FROM Payments WHERE SubscriberId = @id;
                            DELETE FROM MeterReadings WHERE MeterId IN (SELECT Id FROM Meters WHERE SubscriberId = @id);
                            DELETE FROM Meters WHERE SubscriberId = @id;
                            DELETE FROM Users WHERE SubscriberId = @id;
                            DELETE FROM Subscribers WHERE Id = @id;
                        ";
                        AddParam(cmd, "@id", subscriberId);
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

        public void SetActive(int subscriberId, bool isActive)
        {
            using (var conn = _database.CreateConnection())
            using (var trans = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = "UPDATE Subscribers SET IsActive = @isActive WHERE Id = @id;";
                        AddParam(cmd, "@id", subscriberId);
                        AddParam(cmd, "@isActive", isActive ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmdUser = conn.CreateCommand())
                    {
                        cmdUser.Transaction = trans;
                        cmdUser.CommandText = "UPDATE Users SET IsActive = @isActive WHERE SubscriberId = @subId;";
                        AddParam(cmdUser, "@subId", subscriberId);
                        AddParam(cmdUser, "@isActive", isActive ? 1 : 0);
                        cmdUser.ExecuteNonQuery();
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

        private static Subscriber MapSubscriber(IDataRecord reader)
        {
            return new Subscriber
            {
                Id = Convert.ToInt32(reader["Id"]),
                AccountNo = reader["AccountNo"].ToString(),
                Name = reader["Name"].ToString(),
                SubscriberTypeId = Convert.ToInt32(reader["SubscriberTypeId"]),
                SubscriberTypeName = reader["SubscriberTypeName"].ToString(),
                Address = reader["Address"].ToString(),
                Phone = reader["Phone"] is DBNull ? string.Empty : reader["Phone"].ToString(),
                IsActive = Convert.ToInt32(reader["IsActive"]) == 1,
                TotalChargedKop = Convert.ToInt64(reader["TotalChargedKop"]),
                TotalPaidKop = Convert.ToInt64(reader["TotalPaidKop"]),
                DebtKop = Convert.ToInt64(reader["DebtKop"])
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
