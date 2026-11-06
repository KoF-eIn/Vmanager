using System;
using System.Data.SQLite;
using System.Globalization;

namespace VodokanalManager.Data
{
    [SQLiteFunction(Name = "LOWERU", Arguments = 1, FuncType = FunctionType.Scalar)]
    public class LoweruFunction : SQLiteFunction
    {
        public override object Invoke(object[] args)
        {
            if (args == null || args.Length == 0 || args[0] == null || args[0] is DBNull)
                return string.Empty;

            return args[0].ToString().ToLowerInvariant();
        }
    }

    [SQLiteFunction(Name = "RU_CI", FuncType = FunctionType.Collation)]
    public class RuCiCollation : SQLiteFunction
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        public override int Compare(string param1, string param2)
        {
            return string.Compare(param1, param2, RussianCulture, CompareOptions.IgnoreCase);
        }
    }
}
