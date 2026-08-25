using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LPR381_Project.Modles;
using LPR381_Project.IO;

namespace LPR381_Project.Algorithms
{
    internal interface IAlgorithm
    {
        string Name { get; }
        Solution Solve(LPModel model, OutputWriter output);
    }

    public class Solution
    {
        public SolveStatus Status { get; set; }
        public double ObjectiveValue { get; set; }
        public double[] VariableValues { get; set; } = System.Array.Empty<double>();

        /// Final simplex tableau, for sensitivity analysis to operate on.
        public double[,] FinalTableau { get; set; }
        public int[] BasicVariableIndices { get; set; }
    }

    public enum SolveStatus { Optimal, Infeasible, Unbounded }
}
