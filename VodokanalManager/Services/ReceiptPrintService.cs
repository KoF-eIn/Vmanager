using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using VodokanalManager.Models;

namespace VodokanalManager.Services
{
    public interface IReceiptPrintService
    {
        FlowDocument CreateReceiptDocument(Subscriber subscriber, DateTime period, IEnumerable<MeterReading> readings, decimal lastPaymentsSum);
        void PrintReceipt(FlowDocument document, Window owner = null);
    }

    public class ReceiptPrintService : IReceiptPrintService
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        public FlowDocument CreateReceiptDocument(Subscriber subscriber, DateTime period, IEnumerable<MeterReading> readings, decimal lastPaymentsSum)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(40),
                FontFamily = new FontFamily("Segoe UI, Roboto, Arial"),
                FontSize = 12,
                ColumnWidth = 800
            };

            // Шапка документа
            var headerP = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            headerP.Inlines.Add(new Run("МУП «Водоканал-Сервис» г. Канска\n") { FontSize = 16, FontWeight = FontWeights.Bold });
            headerP.Inlines.Add(new Run("Красноярский край, г. Канск, мкр. 4-й Центральный, д. 33 | ИНН 2450012345 | Тел. 8 (39161) 2-15-40\n") { FontSize = 10, Foreground = Brushes.Gray });
            headerP.Inlines.Add(new Run($"ПЛАТЁЖНЫЙ ДОКУМЕНТ (КВИТАНЦИЯ) ЗА {period.ToString("MMMM yyyy", RussianCulture).ToUpper()}\n") { FontSize = 13, FontWeight = FontWeights.SemiBold });
            doc.Blocks.Add(headerP);

            // Блок реквизитов абонента
            var subTable = new Table { Margin = new Thickness(0, 0, 0, 15) };
            subTable.Columns.Add(new TableColumn { Width = new GridLength(200) });
            subTable.Columns.Add(new TableColumn { Width = new GridLength(500) });

            var rowGroupSub = new TableRowGroup();
            AddKeyValueRow(rowGroupSub, "Лицевой счёт:", subscriber.AccountNo, isBoldValue: true);
            AddKeyValueRow(rowGroupSub, "Абонент (ФИО / Наименование):", subscriber.Name);
            AddKeyValueRow(rowGroupSub, "Адрес объекта:", subscriber.Address);
            AddKeyValueRow(rowGroupSub, "Контактный телефон:", string.IsNullOrEmpty(subscriber.Phone) ? "Не указан" : subscriber.Phone);
            AddKeyValueRow(rowGroupSub, "Тип абонента:", subscriber.SubscriberTypeName);
            subTable.RowGroups.Add(rowGroupSub);
            doc.Blocks.Add(subTable);

            // Таблица начислений по приборам учета
            var pChargesTitle = new Paragraph(new Run("Сведения о потреблении и начислениях за расчетный период:") { FontWeight = FontWeights.Bold })
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            doc.Blocks.Add(pChargesTitle);

            var chargesTable = new Table { Margin = new Thickness(0, 0, 0, 15), CellSpacing = 0 };
            chargesTable.Columns.Add(new TableColumn { Width = new GridLength(140) }); // Услуга
            chargesTable.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Прибор
            chargesTable.Columns.Add(new TableColumn { Width = new GridLength(90) });  // Нач. пок.
            chargesTable.Columns.Add(new TableColumn { Width = new GridLength(90) });  // Кон. пок.
            chargesTable.Columns.Add(new TableColumn { Width = new GridLength(80) });  // Расход
            chargesTable.Columns.Add(new TableColumn { Width = new GridLength(85) });  // Тариф
            chargesTable.Columns.Add(new TableColumn { Width = new GridLength(95) });  // Начислено

            var headerRowGroup = new TableRowGroup();
            var headerRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            headerRow.Cells.Add(CreateHeaderCell("Услуга"));
            headerRow.Cells.Add(CreateHeaderCell("№ прибора"));
            headerRow.Cells.Add(CreateHeaderCell("Начальное"));
            headerRow.Cells.Add(CreateHeaderCell("Конечное"));
            headerRow.Cells.Add(CreateHeaderCell("Расход, м³"));
            headerRow.Cells.Add(CreateHeaderCell("Тариф, ₽"));
            headerRow.Cells.Add(CreateHeaderCell("К оплате, ₽"));
            headerRowGroup.Rows.Add(headerRow);
            chargesTable.RowGroups.Add(headerRowGroup);

            var dataRowGroup = new TableRowGroup();
            decimal totalChargedMonth = 0m;

            if (readings != null && readings.Any())
            {
                foreach (var r in readings)
                {
                    var tr = new TableRow();
                    tr.Cells.Add(CreateDataCell(r.ServiceTypeName));
                    tr.Cells.Add(CreateDataCell(r.MeterSerialNo));
                    tr.Cells.Add(CreateDataCell(r.PrevValueM3.HasValue ? r.PrevValueM3.Value.ToString("N3", RussianCulture) : "0,000"));
                    tr.Cells.Add(CreateDataCell(r.ValueM3.ToString("N3", RussianCulture)));
                    tr.Cells.Add(CreateDataCell(r.ConsumptionM3.ToString("N3", RussianCulture), TextAlignment.Right));
                    tr.Cells.Add(CreateDataCell(r.PriceRubPerM3.ToString("N2", RussianCulture), TextAlignment.Right));
                    tr.Cells.Add(CreateDataCell(r.AmountRub.ToString("N2", RussianCulture), TextAlignment.Right, isBold: true));
                    dataRowGroup.Rows.Add(tr);

                    totalChargedMonth += r.AmountRub;
                }
            }
            else
            {
                var trEmpty = new TableRow();
                var cell = new TableCell(new Paragraph(new Run("За выбранный месяц показания приборов не зафиксированы.")) { TextAlignment = TextAlignment.Center })
                {
                    ColumnSpan = 7,
                    Padding = new Thickness(8)
                };
                trEmpty.Cells.Add(cell);
                dataRowGroup.Rows.Add(trEmpty);
            }

            chargesTable.RowGroups.Add(dataRowGroup);
            doc.Blocks.Add(chargesTable);

            // Блок финансового баланса
            var balanceTable = new Table { Margin = new Thickness(0, 0, 0, 20) };
            balanceTable.Columns.Add(new TableColumn { Width = new GridLength(450) });
            balanceTable.Columns.Add(new TableColumn { Width = new GridLength(200) });

            var balanceRows = new TableRowGroup();
            AddKeyValueRow(balanceRows, "Начислено за текущий период:", $"{totalChargedMonth:N2} ₽");
            AddKeyValueRow(balanceRows, "Оплачено за расчетный период:", $"{lastPaymentsSum:N2} ₽");
            AddKeyValueRow(balanceRows, "Текущая задолженность с учётом прошлых периодов:", $"{subscriber.DebtRub:N2} ₽", isBoldValue: true);

            decimal finalToPay = Math.Max(0m, subscriber.DebtRub);
            AddKeyValueRow(balanceRows, "ИТОГО К ОПЛАТЕ:", $"{finalToPay:N2} ₽", isBoldValue: true, fontSize: 14);

            balanceTable.RowGroups.Add(balanceRows);
            doc.Blocks.Add(balanceTable);

            // Нижний колонтитул
            var footerP = new Paragraph
            {
                Margin = new Thickness(0, 15, 0, 0),
                FontSize = 10,
                Foreground = Brushes.Gray,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 0)
            };
            footerP.Inlines.Add(new Run("Оплату можно произвести в кассе МУП «Водоканал-Сервис», через отделения банков и онлайн-банкинг.\n"));
            footerP.Inlines.Add(new Run($"Документ сформирован программным комплексом «Водоканал-Сервис» | Дата печати: {DateTime.Now:dd.MM.yyyy HH:mm}"));
            doc.Blocks.Add(footerP);

            return doc;
        }

        public void PrintReceipt(FlowDocument document, Window owner = null)
        {
            if (document == null) return;

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                printDialog.PrintDocument(paginator, "Квитанция МУП Водоканал");
            }
        }

        private static void AddKeyValueRow(TableRowGroup group, string key, string value, bool isBoldValue = false, double fontSize = 12)
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph(new Run(key) { Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) }))
            {
                Padding = new Thickness(4, 2, 4, 2)
            });

            var valP = new Paragraph(new Run(value) { FontWeight = isBoldValue ? FontWeights.Bold : FontWeights.Normal, FontSize = fontSize });
            row.Cells.Add(new TableCell(valP) { Padding = new Thickness(4, 2, 4, 2) });
            group.Rows.Add(row);
        }

        private static TableCell CreateHeaderCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text) { FontWeight = FontWeights.SemiBold, FontSize = 11 }))
            {
                Padding = new Thickness(6, 6, 6, 6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private static TableCell CreateDataCell(string text, TextAlignment alignment = TextAlignment.Left, bool isBold = false)
        {
            return new TableCell(new Paragraph(new Run(text) { FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal })
            {
                TextAlignment = alignment,
                Margin = new Thickness(0)
            })
            {
                Padding = new Thickness(6, 5, 6, 5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }
    }
}
