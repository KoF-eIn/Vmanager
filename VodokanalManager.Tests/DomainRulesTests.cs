using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VodokanalManager.Models;

namespace VodokanalManager.Tests
{
    [TestClass]
    public class DomainRulesTests
    {
        [TestMethod]
        public void VolumeConversion_M3ToLitersAndBack_PreservesExactValue()
        {
            decimal m3 = 135.420m;
            long liters = (long)Math.Round(m3 * 1000m);

            Assert.AreEqual(135420L, liters);

            decimal backM3 = liters / 1000m;
            Assert.AreEqual(m3, backM3);
        }

        [TestMethod]
        public void ChargeCalculation_WithIntegerRounding_ProducesAccurateKopecks()
        {
            // Формула: (расход в литрах * тариф в копейках за м3 + 500) / 1000
            // Пример 1: расход 8 400 л (8.400 м3), тариф 38.60 руб/м3 (3860 коп)
            // 8400 * 3860 = 32424000
            // (32424000 + 500) / 1000 = 32424 коп = 324.24 руб.
            long consumptionL = 8400L;
            int tariffPriceKop = 3860;

            long amountKop = (consumptionL * tariffPriceKop + 500) / 1000;
            Assert.AreEqual(32424L, amountKop);
            Assert.AreEqual(324.24m, amountKop / 100m);

            // Пример 2: проверка округления (+500 / 1000)
            long consumptionL2 = 3L; // 0.003 м3
            int tariffPriceKop2 = 3860;
            // 3 * 3860 = 11580
            // (11580 + 500) / 1000 = 12 коп.
            long amountKop2 = (consumptionL2 * tariffPriceKop2 + 500) / 1000;
            Assert.AreEqual(12L, amountKop2);
        }

        [TestMethod]
        public void TariffSelection_PicksNewestTariffNotExceedingPeriod()
        {
            var tariffs = new List<Tariff>
            {
                new Tariff { Id = 1, ServiceTypeId = 1, ValidFrom = new DateTime(2025, 1, 1), PriceKopPerM3 = 3860 },
                new Tariff { Id = 2, ServiceTypeId = 1, ValidFrom = new DateTime(2025, 7, 1), PriceKopPerM3 = 4210 },
                new Tariff { Id = 3, ServiceTypeId = 1, ValidFrom = new DateTime(2026, 1, 1), PriceKopPerM3 = 4530 }
            };

            // Для периода май 2025 должен выбраться январский тариф
            var periodMay2025 = new DateTime(2025, 5, 1);
            var selectedMay = tariffs
                .Where(t => t.ServiceTypeId == 1 && t.ValidFrom <= periodMay2025)
                .OrderByDescending(t => t.ValidFrom)
                .FirstOrDefault();

            Assert.IsNotNull(selectedMay);
            Assert.AreEqual(3860, selectedMay.PriceKopPerM3);

            // Для периода ноябрь 2025 должен выбраться июльский тариф
            var periodNov2025 = new DateTime(2025, 11, 1);
            var selectedNov = tariffs
                .Where(t => t.ServiceTypeId == 1 && t.ValidFrom <= periodNov2025)
                .OrderByDescending(t => t.ValidFrom)
                .FirstOrDefault();

            Assert.IsNotNull(selectedNov);
            Assert.AreEqual(4210, selectedNov.PriceKopPerM3);

            // Для периода январь 2026 должен выбраться тариф 2026 года
            var periodJan2026 = new DateTime(2026, 1, 1);
            var selectedJan = tariffs
                .Where(t => t.ServiceTypeId == 1 && t.ValidFrom <= periodJan2026)
                .OrderByDescending(t => t.ValidFrom)
                .FirstOrDefault();

            Assert.IsNotNull(selectedJan);
            Assert.AreEqual(4530, selectedJan.PriceKopPerM3);
        }

        [TestMethod]
        public void MeterReading_Validation_RejectsLesserReading()
        {
            long previousReadingL = 120000L; // 120.000 м3
            long invalidNewReadingL = 119500L; // 119.500 м3

            bool isValid = invalidNewReadingL >= previousReadingL;
            Assert.IsFalse(isValid, "Новое показание не может быть меньше предыдущего.");

            long validNewReadingL = 128400L;
            Assert.IsTrue(validNewReadingL >= previousReadingL);
        }

        [TestMethod]
        public void DebtCalculation_CalculatesBalanceAccurately()
        {
            long totalChargedKop = 105400L; // 1054.00 руб.
            long totalPaidKop = 80000L;      // 800.00 руб.

            long debtKop = totalChargedKop - totalPaidKop;
            Assert.AreEqual(25400L, debtKop);
            Assert.AreEqual(254.00m, debtKop / 100m);
        }
    }
}
