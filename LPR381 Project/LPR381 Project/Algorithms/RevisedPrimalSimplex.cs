using LPR381_Project.IO;
using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Algorithms
{
    internal class RevisedPrimalSimplex : IAlgorithm
    {
        public string Name => "Revised Primal Simplex Algorithm";

        public Solution Solve(LPModel model, OutputWriter output)
        {
            output.AppendHeader($"{Name} — Canonical Form");
           

            output.AppendHeader($"{Name} — Product Form / Price Out Iterations");
          

            return new Solution { Status = SolveStatus.Optimal };
        }
    }
}
