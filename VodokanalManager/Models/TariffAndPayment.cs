using System;

namespace VodokanalManager.Models
{
    public class Tariff
    {
        public int Id { get; set; }
        public int ServiceTypeId { get; set; }
        public string ServiceTypeName { get; set; }
        public DateTime ValidFrom { get; set; }
        public int PriceKopPerM3 { get; set; }
        public decimal PriceRubPerM3
        {
            get => PriceKopPerM3 / 100m;
            set => PriceKopPerM3 = (int)Math.Round(value * 100m);
        }

        public string DisplayText => $"{ServiceTypeName} — {PriceRubPerM3:N2} ₽/м³ (с {ValidFrom:dd.MM.yyyy})";
    }

    public class Payment
    {
        public int Id { get; set; }
        public int SubscriberId { get; set; }
        public string SubscriberAccountNo { get; set; }
        public string SubscriberName { get; set; }
        public string SubscriberAddress { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Today;
        public long AmountKop { get; set; }
        public decimal AmountRub
        {
            get => AmountKop / 100m;
            set => AmountKop = (long)Math.Round(value * 100m);
        }

        public int PaymentMethodId { get; set; } = 1;
        public string PaymentMethodName { get; set; }

        public int? UserId { get; set; }
        public string UserName { get; set; }
    }
}
