using LPR381_Project.IO;
using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Algorithms
{


    internal class PrimalSimplex : IAlgorithm
    {
        private const double Epsilon = 1e-9;

        public string Name => "Primal Simplex Algorithm";

        public Solution Solve(LPModel model, OutputWriter output)
        {
            var canon = CanonicalFormBuilder.Build(model);

            output.AppendHeader($"{Name} — Canonical Form");
            WriteTableau(output, canon, "Initial Tableau (Canonical Form)");

            output.AppendHeader($"{Name} — Iterations");

            int iteration = 0;
            const int maxIterations = 1000;
            bool stoppedByLimit = false;

            while (true)
            {
                int enteringCol = ChooseEnteringColumn(canon);
                if (enteringCol == -1)
                    break;

                int leavingRow = ChooseLeavingRow(canon, enteringCol);
                if (leavingRow == -1)
                {
                    output.AppendLine();
                    output.AppendLine($"Unbounded: column '{canon.ColumnLabels[enteringCol]}' has no positive ratio to limit it.");
                    return new Solution { Status = SolveStatus.Unbounded };
                }

                string enteringName = canon.ColumnLabels[enteringCol];
                string leavingName = canon.ColumnLabels[canon.BasicVariableIndices[leavingRow - 1]];

                Pivot(canon, leavingRow, enteringCol);
                canon.BasicVariableIndices[leavingRow - 1] = enteringCol;

                iteration++;
                WriteTableau(output, canon,
                    $"Iteration {iteration} (entering: {enteringName}, leaving: {leavingName})");

                if (iteration >= maxIterations)
                {
                    output.AppendLine();
                    output.AppendLine("Stopped after reaching the maximum iteration safety limit.");
                    stoppedByLimit = true;
                    break;
                }
            }

            if (stoppedByLimit)
            {
                return new Solution { Status = SolveStatus.Unbounded };
            }

            int rhsCol = canon.Tableau.GetLength(1) - 1;

            bool changed = true;
            int safety = 0;
            while (changed && safety < 100)
            {
                changed = false;
                for (int r = 0; r < canon.BasicVariableIndices.Length; r++)
                {
                    int basicCol = canon.BasicVariableIndices[r];
                    if (canon.ArtificialColumns.Contains(basicCol)
                        && Math.Abs(canon.Tableau[r + 1, rhsCol]) <= Epsilon)
                    {
                        if (TryDriveOutArtificial(canon, r, rhsCol))
                        {
                            changed = true;
                            safety++;
                        }
                    }
                }
            }

            for (int r = 0; r < canon.BasicVariableIndices.Length; r++)
            {
                int basicCol = canon.BasicVariableIndices[r];
                if (canon.ArtificialColumns.Contains(basicCol)
                    && canon.Tableau[r + 1, rhsCol] > Epsilon)
                {
                    output.AppendLine();
                    output.AppendLine("Infeasible: an artificial variable remains in the basis at a positive value.");
                    return new Solution { Status = SolveStatus.Infeasible };
                }
            }

            var solution = BuildSolution(canon, rhsCol);
            output.AppendLine();
            output.AppendLine($"Optimal solution found in {iteration} iteration(s).");
            output.AppendLine($"Objective value: {OutputWriter.Round3(solution.ObjectiveValue):0.000}");
            for (int j = 0; j < solution.VariableValues.Length; j++)
                output.AppendLine($"x{j + 1} = {OutputWriter.Round3(solution.VariableValues[j]):0.000}");

            return solution;
        }

        private static int ChooseEnteringColumn(CanonicalForm canon)
        {
         
            int cols = canon.Tableau.GetLength(1) - 1;
            int bestCol = -1;
            double mostNegative = -Epsilon;

            for (int j = 0; j < cols; j++)
            {
                if (canon.Tableau[0, j] < mostNegative)
                {
                    mostNegative = canon.Tableau[0, j];
                    bestCol = j;
                }
            }

            return bestCol;
        }

        private static int ChooseLeavingRow(CanonicalForm canon, int enteringCol)
        {
            int rows = canon.Tableau.GetLength(0) - 1;
            int rhsCol = canon.Tableau.GetLength(1) - 1;

            int bestRow = -1;
            double bestRatio = double.PositiveInfinity;

            for (int r = 1; r <= rows; r++)
            {
                double coeff = canon.Tableau[r, enteringCol];
                if (coeff <= Epsilon) continue;

                double ratio = canon.Tableau[r, rhsCol] / coeff;
                if (ratio < bestRatio - Epsilon)
                {
                    bestRatio = ratio;
                    bestRow = r;
                }
                else if (Math.Abs(ratio - bestRatio) <= Epsilon && bestRow != -1)
                {
                    if (canon.BasicVariableIndices[r - 1] < canon.BasicVariableIndices[bestRow - 1])
                        bestRow = r;
                }
            }

            return bestRow;
        }

        private static void Pivot(CanonicalForm canon, int pivotRow, int pivotCol)
        {
            int rows = canon.Tableau.GetLength(0);
            int cols = canon.Tableau.GetLength(1);

            double pivotValue = canon.Tableau[pivotRow, pivotCol];
            if (Math.Abs(pivotValue) < Epsilon)
                throw new InvalidOperationException("Pivot element is numerically zero.");

            for (int j = 0; j < cols; j++)
                canon.Tableau[pivotRow, j] /= pivotValue;

            for (int r = 0; r < rows; r++)
            {
                if (r == pivotRow) continue;
                double factor = canon.Tableau[r, pivotCol];
                if (Math.Abs(factor) < Epsilon) continue;
                for (int j = 0; j < cols; j++)
                    canon.Tableau[r, j] -= factor * canon.Tableau[pivotRow, j];
            }
        }

        private static bool TryDriveOutArtificial(CanonicalForm canon, int basicRowIndex, int rhsCol)
        {
            int row = basicRowIndex + 1;
            int artificialCol = canon.BasicVariableIndices[basicRowIndex];

            for (int j = 0; j < canon.Tableau.GetLength(1) - 1; j++)
            {
                if (j == artificialCol) continue;
                if (canon.ArtificialColumns.Contains(j)) continue;
                if (Math.Abs(canon.Tableau[row, j]) <= Epsilon) continue;

                bool isBasic = canon.BasicVariableIndices.Contains(j);
                if (isBasic) continue;

                Pivot(canon, row, j);
                canon.BasicVariableIndices[basicRowIndex] = j;
                return true;
            }
            return false;
        }

        private static Solution BuildSolution(CanonicalForm canon, int rhsCol)
        {
            var columnValues = new double[rhsCol];
            for (int r = 0; r < canon.BasicVariableIndices.Length; r++)
                columnValues[canon.BasicVariableIndices[r]] = canon.Tableau[r + 1, rhsCol];

            var original = new double[canon.OriginalVariableCount];
            foreach (var map in canon.VariableMaps)
            {
                double value = 0;
                foreach (var (col, sign) in map.Components)
                    value += sign * columnValues[col];
                original[map.OriginalIndex] = value;
            }

            double objective = canon.Tableau[0, rhsCol];
            if (canon.OriginalWasMinimization) objective = -objective;

            return new Solution
            {
                Status = SolveStatus.Optimal,
                ObjectiveValue = objective,
                VariableValues = original,
                FinalTableau = canon.Tableau,
                BasicVariableIndices = canon.BasicVariableIndices
            };
        }

        private static void WriteTableau(OutputWriter output, CanonicalForm canon, string title)
        {
            output.AppendLine();
            output.AppendLine(title);

            int cols = canon.Tableau.GetLength(1);
            var headers = new string[cols];
            Array.Copy(canon.ColumnLabels, headers, canon.ColumnLabels.Length);
            headers[cols - 1] = "RHS";

            int rows = canon.Tableau.GetLength(0);
            var rowLabels = new string[rows];
            rowLabels[0] = "z";
            for (int r = 1; r < rows; r++)
                rowLabels[r] = canon.ColumnLabels[canon.BasicVariableIndices[r - 1]];

            output.AppendTable(canon.Tableau, headers, rowLabels);
        }
    }
}