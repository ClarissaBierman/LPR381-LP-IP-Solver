using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Modles
{
    public class VariableMap
    {
        public int OriginalIndex { get; set; }

        
        public List<(int ColumnIndex, double Sign)> Components { get; } = new List<(int ColumnIndex, double Sign)> ();
    }
}
