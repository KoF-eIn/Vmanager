using System;

namespace VodokanalManager.Data
{
    public class SqliteDialect : IDialect
    {
        public string CaseInsensitiveLike(string column, string paramName)
        {
            return $"LOWERU({column}) LIKE LOWERU({paramName})";
        }

        public string GetLastInsertedIdQuery => "SELECT last_insert_rowid();";

        public string LimitRows(int count) => $"LIMIT {count}";

        public string FormatDate(DateTime date) => date.ToString("yyyy-MM-dd");
    }
}
