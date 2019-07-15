using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.Library.Extensions
{
    public static class StringExtensions
    {
        public static bool Equals(this string compare, string compareTo)
        {
            return compare.ToLower() == compareTo.ToLower();
        }

        public static bool Contains(this string data, string subString)
        {
            return data.ToLower().Contains(subString.ToLower());
        }
    }
}
