using System;
using System.Data;

namespace VodokanalManager.Data
{
    public interface IDialect
    {
        string CaseInsensitiveLike(string column, string paramName);
        string GetLastInsertedIdQuery { get; }
        string LimitRows(int count);
        string FormatDate(DateTime date);
    }

    public interface IDatabase
    {
        IDbConnection CreateConnection();
        string ConnectionString { get; }
        string DatabasePath { get; }
        IDialect Dialect { get; }
        void SetDatabasePath(string newPath);
        void Reconnect();
    }
}
