using System;
using System.Collections.Generic;
using System.Data;
using VodokanalManager.Models;
using VodokanalManager.Services;

namespace VodokanalManager.Data.Repositories
{
    public interface IUserRepository
    {
        User Authenticate(string login, string password, IPasswordHasher hasher);
        List<User> GetAll();
        User GetById(int id);
        User GetBySubscriberId(int subscriberId);
        void AddUser(User user, string password, IPasswordHasher hasher);
        void UpdateUser(User user, string newPassword, IPasswordHasher hasher);
        void SetActive(int userId, bool isActive);
        void DeleteUser(int userId);
    }

    public class UserRepository : IUserRepository
    {
        private readonly IDatabase _database;

        public UserRepository(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public User Authenticate(string login, string password, IPasswordHasher hasher)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
                return null;

            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT u.Id, u.Login, u.PasswordHash, u.Salt, u.InitialPassword, u.FullName, u.RoleId, r.Name AS RoleName, u.IsActive, u.SubscriberId
                    FROM Users u
                    JOIN Roles r ON u.RoleId = r.Id
                    WHERE " + _database.Dialect.CaseInsensitiveLike("u.Login", "@login") + @"
                    LIMIT 1;";

                var pLogin = cmd.CreateParameter();
                pLogin.ParameterName = "@login";
                pLogin.Value = login.Trim();
                cmd.Parameters.Add(pLogin);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string hash = reader.GetString(reader.GetOrdinal("PasswordHash"));
                        string salt = reader.GetString(reader.GetOrdinal("Salt"));
                        bool isActive = Convert.ToInt32(reader["IsActive"]) == 1;

                        if (!isActive)
                            throw new InvalidOperationException("Учётная запись отключена. Обратитесь к администратору.");

                        if (hasher.VerifyPassword(password, hash, salt))
                        {
                            return MapUser(reader);
                        }
                    }
                }
            }

            return null;
        }

        public List<User> GetAll()
        {
            var list = new List<User>();
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT u.Id, u.Login, u.PasswordHash, u.Salt, u.InitialPassword, u.FullName, u.RoleId, r.Name AS RoleName, u.IsActive, u.SubscriberId
                    FROM Users u
                    JOIN Roles r ON u.RoleId = r.Id
                    ORDER BY u.RoleId ASC, u.Login ASC;";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapUser(reader));
                    }
                }
            }
            return list;
        }

        public User GetById(int id)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT u.Id, u.Login, u.PasswordHash, u.Salt, u.InitialPassword, u.FullName, u.RoleId, r.Name AS RoleName, u.IsActive, u.SubscriberId
                    FROM Users u
                    JOIN Roles r ON u.RoleId = r.Id
                    WHERE u.Id = @id;";

                AddParam(cmd, "@id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapUser(reader);
                    }
                }
            }
            return null;
        }

        public User GetBySubscriberId(int subscriberId)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT u.Id, u.Login, u.PasswordHash, u.Salt, u.InitialPassword, u.FullName, u.RoleId, r.Name AS RoleName, u.IsActive, u.SubscriberId
                    FROM Users u
                    JOIN Roles r ON u.RoleId = r.Id
                    WHERE u.SubscriberId = @subscriberId;";

                var pSub = cmd.CreateParameter();
                pSub.ParameterName = "@subscriberId";
                pSub.Value = subscriberId;
                cmd.Parameters.Add(pSub);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapUser(reader);
                    }
                }
            }
            return null;
        }

        public void AddUser(User user, string password, IPasswordHasher hasher)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Пароль обязателен.", nameof(password));

            if (user.RoleId == 4 && !user.SubscriberId.HasValue)
            {
                throw new InvalidOperationException("Учётная запись абонента создаётся автоматически при добавлении абонента.");
            }

            string hash = hasher.HashPassword(password, out string salt);

            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Users (Login, PasswordHash, Salt, InitialPassword, FullName, RoleId, IsActive, SubscriberId)
                    VALUES (@login, @hash, @salt, @initPwd, @fullName, @roleId, @isActive, @subscriberId);";

                AddParam(cmd, "@login", user.Login.Trim());
                AddParam(cmd, "@hash", hash);
                AddParam(cmd, "@salt", salt);
                AddParam(cmd, "@initPwd", password.Trim());
                AddParam(cmd, "@fullName", user.FullName.Trim());
                AddParam(cmd, "@roleId", user.RoleId);
                AddParam(cmd, "@isActive", user.IsActive ? 1 : 0);
                AddParam(cmd, "@subscriberId", (object)user.SubscriberId ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateUser(User user, string newPassword, IPasswordHasher hasher)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            using (var conn = _database.CreateConnection())
            using (var trans = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        if (!string.IsNullOrWhiteSpace(newPassword))
                        {
                            string hash = hasher.HashPassword(newPassword, out string salt);
                            cmd.CommandText = @"
                                UPDATE Users
                                SET Login = @login,
                                    FullName = @fullName,
                                    RoleId = @roleId,
                                    IsActive = @isActive,
                                    PasswordHash = @hash,
                                    Salt = @salt,
                                    InitialPassword = @initPwd
                                WHERE Id = @id;";

                            AddParam(cmd, "@hash", hash);
                            AddParam(cmd, "@salt", salt);
                            AddParam(cmd, "@initPwd", newPassword.Trim());
                        }
                        else
                        {
                            cmd.CommandText = @"
                                UPDATE Users
                                SET Login = @login,
                                    FullName = @fullName,
                                    RoleId = @roleId,
                                    IsActive = @isActive
                                WHERE Id = @id;";
                        }

                        AddParam(cmd, "@id", user.Id);
                        AddParam(cmd, "@login", user.Login.Trim());
                        AddParam(cmd, "@fullName", user.FullName.Trim());
                        AddParam(cmd, "@roleId", user.RoleId);
                        AddParam(cmd, "@isActive", user.IsActive ? 1 : 0);

                        cmd.ExecuteNonQuery();
                    }

if (user.SubscriberId.HasValue && user.SubscriberId.Value > 0)
                    {
                        using (var cmdSub = conn.CreateCommand())
                        {
                            cmdSub.Transaction = trans;
                            cmdSub.CommandText = "UPDATE Subscribers SET AccountNo = @acc WHERE Id = @subId;";
                            AddParam(cmdSub, "@acc", user.Login.Trim());
                            AddParam(cmdSub, "@subId", user.SubscriberId.Value);
                            cmdSub.ExecuteNonQuery();
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

        public void SetActive(int userId, bool isActive)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE Users SET IsActive = @isActive WHERE Id = @id;";
                AddParam(cmd, "@id", userId);
                AddParam(cmd, "@isActive", isActive ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

public void DeleteUser(int userId)
        {
            using (var conn = _database.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM Users WHERE Id = @id;";
                var p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = userId;
                cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
        }
        private static User MapUser(IDataRecord reader)
        {
            string initPwd = null;
            try
            {
                int ord = reader.GetOrdinal("InitialPassword");
                if (!reader.IsDBNull(ord))
                    initPwd = reader.GetString(ord);
            }
            catch {}

            return new User
            {
                Id = Convert.ToInt32(reader["Id"]),
                Login = reader["Login"].ToString(),
                PasswordHash = reader["PasswordHash"].ToString(),
                Salt = reader["Salt"].ToString(),
                InitialPassword = initPwd,
                FullName = reader["FullName"].ToString(),
                RoleId = Convert.ToInt32(reader["RoleId"]),
                RoleName = reader["RoleName"].ToString(),
                IsActive = Convert.ToInt32(reader["IsActive"]) == 1,
                SubscriberId = reader["SubscriberId"] is DBNull ? (int?)null : Convert.ToInt32(reader["SubscriberId"])
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
