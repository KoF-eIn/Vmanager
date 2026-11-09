using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VodokanalManager.Services
{
    public interface ICsvExportService
    {
        void ExportToCsv<T>(string filePath, IEnumerable<T> items, Func<T, string[]> rowSelector, string[] headers);
    }

    public class CsvExportService : ICsvExportService
    {
        public void ExportToCsv<T>(string filePath, IEnumerable<T> items, Func<T, string[]> rowSelector, string[] headers)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу экспорта не указан.", nameof(filePath));

            if (items == null) throw new ArgumentNullException(nameof(items));
            if (rowSelector == null) throw new ArgumentNullException(nameof(rowSelector));

            var sb = new StringBuilder();

if (headers != null && headers.Length > 0)
            {
                sb.AppendLine(string.Join(";", EscapeRow(headers)));
            }

foreach (var item in items)
            {
                string[] values = rowSelector(item);
                sb.AppendLine(string.Join(";", EscapeRow(values)));
            }

File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        private static string[] EscapeRow(string[] row)
        {
            var result = new string[row.Length];
            for (int i = 0; i < row.Length; i++)
            {
                string val = row[i] ?? string.Empty;
                if (val.Contains(";") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
                {
                    result[i] = "\"" + val.Replace("\"", "\"\"") + "\"";
                }
                else
                {
                    result[i] = val;
                }
            }
            return result;
        }
    }
}
