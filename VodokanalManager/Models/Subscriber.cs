using System;

namespace VodokanalManager.Models
{
    public class Subscriber
    {
        public int Id { get; set; }
        public string AccountNo { get; set; }
        public string Name { get; set; }
        public int SubscriberTypeId { get; set; } = 1;
        public string SubscriberTypeName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public bool IsActive { get; set; } = true;

        public long TotalChargedKop { get; set; }
        public long TotalPaidKop { get; set; }
        public long DebtKop { get; set; }

        public decimal TotalChargedRub => TotalChargedKop / 100m;
        public decimal TotalPaidRub => TotalPaidKop / 100m;
        public decimal DebtRub => DebtKop / 100m;

        public string StatusText => IsActive ? "Активен" : "Отключен";
        public bool HasDebt => DebtKop > 0;

        public override string ToString() => $"{AccountNo} — {Name}";
    }
}
