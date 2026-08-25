using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Modles
{
    public enum VariableType
    {
        Positive,   // "+"  -> x >= 0
        Negative,   // "-"  -> x <= 0
        Urs,        // "urs" -> unrestricted in sign
        Integer,    // "int" -> integer, otherwise unrestricted in sign
        Binary      // "bin" -> integer, 0 or 1
    }
}
