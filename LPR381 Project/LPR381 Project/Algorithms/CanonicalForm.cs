using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Algorithms
{
    public class CanonicalForm
    {
       
        /// Row 0 = objective row, rows 1..m = constraints.
        /// Columns 0..(TotalVariableCount-1) = variables, last column = RHS.
       
        public double[,] Tableau { get; set; } = new double[0, 0];

        /// Display label for every column except the RHS column (e.g. "x1", "s2", "a1").
        public string[] ColumnLabels { get; set; } = System.Array.Empty<string>();

        /// Column index of the basic variable for each constraint row (index 0 = row 1 of Tableau).
        public int[] BasicVariableIndices { get; set; } = System.Array.Empty<int>();

        /// Initial basic variables, used to locate the inverse basis matrix B^-1 in the final tableau.
        public int[] InitialBasicVariableIndices { get; set; } = System.Array.Empty<int>();

        /// True if any artificial-variable column exists (needed to check feasibility at the end)
        public List<int> ArtificialColumns { get; set; } = new List<int>();

        /// How to translate expanded tableau columns back to the original input-file variables.
        public List<VariableMap> VariableMaps { get; set; } = new List<VariableMap>();

        /// Number of decision variables as entered in the input file (before splitting/substitution).
        public int OriginalVariableCount { get; set; }

        /// True if the original model was "min" (objective was negated internally to solve as max).
        public bool OriginalWasMinimization { get; set; }

        /// Big-M penalty value used for artificial variables
        public double BigM { get; set; } = 1_000_000d;
    }
}
