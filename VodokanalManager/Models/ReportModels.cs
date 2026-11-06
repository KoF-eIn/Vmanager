using System;

namespace VodokanalManager.Models
{
    public class DebtorReportItem
    {
        public int Rank { get; set; }
        public int SubscriberId { get; set; }
        public string AccountNo { get; set; }
        public string Name { get; set; }
        public string SubscriberTypeName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public long DebtKop { get; set; }
        public decimal DebtRub => DebtKop / 100m;
        public long TotalChargedKop { get; set; }
        public decimal TotalChargedRub => TotalChargedKop / 100m;
        public long TotalPaidKop { get; set; }
        public decimal TotalPaidRub => TotalPaidKop / 100m;
        public DateTime? LastPaymentDate { get; set; }

        public string LastPaymentDateText => LastPaymentDate.HasValue ? LastPaymentDate.Value.ToString("dd.MM.yyyy") : "Не поступало";
    }

    public class SuspiciousConsumptionItem
    {
        public int ReadingId { get; set; }
        public string AccountNo { get; set; }
        public string SubscriberName { get; set; }
        public string ServiceTypeName { get; set; }
        public string MeterSerialNo { get; set; }
        public DateTime Period { get; set; }
        public string PeriodText => Period.ToString("MM.yyyy");
        public long ConsumptionL { get; set; }
        public decimal ConsumptionM3 => ConsumptionL / 1000m;
        public decimal AvgPrev3MonthsM3 { get; set; }
        public double Ratio { get; set; }
        public string Note { get; set; }
    }

    public class MonthlyConsumptionItem
    {
        public string PeriodKey { get; set; }
        public string PeriodName { get; set; }
        public decimal ColdWaterM3 { get; set; }
        public decimal HotWaterM3 { get; set; }
        public decimal TotalM3 => ColdWaterM3 + HotWaterM3;
    }

    public class PaymentSummaryItem
    {
        public string PaymentMethodName { get; set; }
        public int PaymentsCount { get; set; }
        public long TotalAmountKop { get; set; }
        public decimal TotalAmountRub => TotalAmountKop / 100m;
    }
}
