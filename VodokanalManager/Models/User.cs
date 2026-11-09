using System;

namespace VodokanalManager.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }
        public string InitialPassword { get; set; }
        public string FullName { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; } = true;
        public int? SubscriberId { get; set; }

        public bool IsAdmin => RoleId == 1;
        public bool IsOperator => RoleId == 2;
        public bool IsCashier => RoleId == 3;
        public bool IsSubscriber => RoleId == 4;

        public string StatusText => IsActive ? "Активен" : "Заблокирован";
    }
}
