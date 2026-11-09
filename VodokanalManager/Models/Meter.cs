using System;

namespace VodokanalManager.Models
{
    public class Meter
    {
        public int Id { get; set; }
        public int SubscriberId { get; set; }
        public string SubscriberAccountNo { get; set; }
        public string SubscriberName { get; set; }
        public int ServiceTypeId { get; set; }
        public string ServiceTypeName { get; set; }
        public string SerialNo { get; set; }
        public DateTime InstallDate { get; set; } = DateTime.Today;
        public bool IsActive { get; set; } = true;

        public long LastReadingL { get; set; }
        public decimal LastReadingM3 => LastReadingL / 1000m;
        public DateTime? LastReadingPeriod { get; set; }

        public string StatusText => IsActive ? "В эксплуатации" : "Выведен из эксплуатации";

        public override string ToString() => $"{SerialNo} ({ServiceTypeName})";
    }

    public class MeterReading
    {
        public int Id { get; set; }
        public int MeterId { get; set; }
        public string MeterSerialNo { get; set; }
        public int SubscriberId { get; set; }
        public string SubscriberAccountNo { get; set; }
        public string SubscriberName { get; set; }
        public int ServiceTypeId { get; set; }
        public string ServiceTypeName { get; set; }

        public DateTime Period { get; set; }
        public long ValueL { get; set; }
        public decimal ValueM3
        {
            get => ValueL / 1000m;
            set => ValueL = (long)Math.Round(value * 1000m);
        }

        public long? PrevValueL { get; set; }
        public decimal? PrevValueM3 => PrevValueL.HasValue ? PrevValueL.Value / 1000m : (decimal?)null;

        public long ConsumptionL { get; set; }
        public decimal ConsumptionM3 => ConsumptionL / 1000m;

        public int PriceKopPerM3 { get; set; }
        public decimal PriceRubPerM3 => PriceKopPerM3 / 100m;

        public long AmountKop { get; set; }
        public decimal AmountRub => AmountKop / 100m;

        public bool IsInitialReading { get; set; }

        public int? UserId { get; set; }
        public string UserName { get; set; }
    }
}
