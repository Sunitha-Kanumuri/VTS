using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.Library.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime startOfDay(this DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, 0);
        }

        public static DateTime endOfDay(this DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, 23, 59, 59, 999);
        }
    }
}
