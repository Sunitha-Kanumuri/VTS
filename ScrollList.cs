using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.Types
{
    public class ScrollList<T> where T:class
    {
        public List<T> appointments { get; set; }
        public bool dataEnd { get; set; }
        public bool dataStart { get; set; }
    }
}
