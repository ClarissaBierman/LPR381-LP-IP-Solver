using LPR381_Project.IO;
using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381_Project.Algorithms
{
	internal class CuttingPlane : IAlgorithm
	{
		public string Name => "Cutting Plane Algorithm";

		private const double Tolerance = 1e-6;
		private const int MaxCuts = 100;

		public Solution Solve(LPModel model, OutputWriter output)
		{
			output.AppendHeader(Name);
			output.AppendLine("Solving LP relaxation via Primal Simplex:");

			var relaxation = new PrimalSimplex().Solve(model, output);

			if (relaxation.Status == SolveStatus.Unbounded)
			{
				output.AppendLine("LP relaxation is unbounded - cutting plane cannot proceed.");
				return relaxation;
			}
			if (relaxation.Status == SolveStatus.Infeasible)
			{
				output.AppendLine("LP relaxation is infeasible.");
				return relaxation;
			}

			var cf = CanonicalFormBuilder.Build(model);
			cf.Tableau = relaxation.FinalTableau;
			cf.BasicVariableIndices = relaxation.BasicVariableIndices;

			output.AppendHeader($"{Name} — Adding Gomory Cuts");

			var integerColumns = GetIntegerColumns(model, cf);

			if (integerColumns.Count == 0)
			{
				output.AppendLine("Note: no variables are restricted to Integer/Binary - returning the LP relaxation optimum.");
				return relaxation;
			}

			int cutCount = 0;
			var summaryLines = new List<string>();

			while (true)
			{
				int fracRow = FindFractionalRow(cf, integerColumns, out double fracValue);
				if (fracRow == -1)
				{
					summaryLines.Add("All integer-restricted variables are integral - optimal integer solution found.");
					break;
				}

				cutCount++;
				if (cutCount > MaxCuts)
				{
					output.AppendHeader("Cutting Plane Summary");
					foreach (var l in summaryLines) output.AppendLine(l);
					output.AppendLine("Maximum number of cuts exceeded - stopping (possible cycling).");
					return new Solution { Status = SolveStatus.Infeasible };
				}

				int basicCol = cf.BasicVariableIndices[fracRow];
				string basicLabel = cf.ColumnLabels[basicCol];
				summaryLines.Add($"Cut {cutCount}: source row is basic variable {basicLabel} = {fracValue:0.000} (fractional)");

				output.AppendHeader($"Cut {cutCount}: source row = {basicLabel} = {fracValue:0.000}");

				AddGomoryCut(cf, fracRow);
				PrintTableau(output, cf, $"After adding cut {cutCount} (dual-infeasible RHS expected)");

				var dualStatus = DualOptimize(cf);
				if (dualStatus == SolveStatus.Infeasible)
				{
					summaryLines.Add($"Cut {cutCount}: led to primal infeasibility - problem has no integer-feasible solution.");
					output.AppendHeader("Cutting Plane Summary");
					foreach (var l in summaryLines) output.AppendLine(l);
					return new Solution { Status = SolveStatus.Infeasible };
				}

				PrintTableau(output, cf, $"After re-optimizing (cut {cutCount})");
			}

			output.AppendHeader("Cutting Plane Summary");
			foreach (var l in summaryLines) output.AppendLine(l);

			var solution = ExtractSolution(model, cf, output);
			return solution;
		}

		// Dual simplex - restores feasibility after a cut row is added
		private static SolveStatus DualOptimize(CanonicalForm cf)
		{
			int rows = cf.Tableau.GetLength(0);
			int cols = cf.Tableau.GetLength(1);
			int rhsCol = cols - 1;

			while (true)
			{
				int leaveRow = -1;
				double mostNegative = -Tolerance;
				for (int i = 1; i < rows; i++)
				{
					if (cf.Tableau[i, rhsCol] < mostNegative)
					{
						mostNegative = cf.Tableau[i, rhsCol];
						leaveRow = i;
					}
				}

				if (leaveRow == -1) break; // primal-feasible again

				int enterCol = -1;
				double bestRatio = double.PositiveInfinity;
				for (int j = 0; j < rhsCol; j++)
				{
					if (cf.ArtificialColumns.Contains(j)) continue; // never let an artificial re-enter the basis

					double a = cf.Tableau[leaveRow, j];
					if (a < -Tolerance)
					{
						double ratio = cf.Tableau[0, j] / (-a);
						if (ratio < bestRatio - Tolerance ||
							(ratio < bestRatio + Tolerance && (enterCol == -1 || j < enterCol)))
						{
							bestRatio = ratio;
							enterCol = j;
						}
					}
				}

				if (enterCol == -1) return SolveStatus.Infeasible;

				Pivot(cf, leaveRow, enterCol);
				cf.BasicVariableIndices[leaveRow - 1] = enterCol;
			}

			return SolveStatus.Optimal;
		}

		private static void Pivot(CanonicalForm cf, int row, int col)
		{
			int rows = cf.Tableau.GetLength(0);
			int cols = cf.Tableau.GetLength(1);

			double pivotVal = cf.Tableau[row, col];
			for (int j = 0; j < cols; j++)
				cf.Tableau[row, j] /= pivotVal;

			for (int i = 0; i < rows; i++)
			{
				if (i == row) continue;
				double factor = cf.Tableau[i, col];
				if (Math.Abs(factor) < 1e-12) continue;
				for (int j = 0; j < cols; j++)
					cf.Tableau[i, j] -= factor * cf.Tableau[row, j];
			}
		}

		// Gomory fractional cut
		private static void AddGomoryCut(CanonicalForm cf, int sourceRow)
		{
			int oldRows = cf.Tableau.GetLength(0);
			int oldCols = cf.Tableau.GetLength(1);
			int oldRhsCol = oldCols - 1;

			int newRows = oldRows + 1;
			int newCols = oldCols + 1;
			int newRhsCol = newCols - 1;
			int newCutCol = oldRhsCol; // the new column slots in right where the old RHS used to be

			var newTableau = new double[newRows, newCols];

			for (int i = 0; i < oldRows; i++)
			{
				for (int j = 0; j < oldRhsCol; j++)
					newTableau[i, j] = cf.Tableau[i, j];
				newTableau[i, newCutCol] = 0d;
				newTableau[i, newRhsCol] = cf.Tableau[i, oldRhsCol];
			}

			double b = cf.Tableau[sourceRow, oldRhsCol];
			double f0 = FractionalPart(b);

			for (int j = 0; j < oldRhsCol; j++)
				newTableau[newRows - 1, j] = -FractionalPart(cf.Tableau[sourceRow, j]);
			newTableau[newRows - 1, newCutCol] = 1d;
			newTableau[newRows - 1, newRhsCol] = -f0;

			cf.Tableau = newTableau;

			var newLabels = new string[cf.ColumnLabels.Length + 1];
			Array.Copy(cf.ColumnLabels, newLabels, cf.ColumnLabels.Length);
			newLabels[newLabels.Length - 1] = $"g{CountGomoryLabels(cf) + 1}";
			cf.ColumnLabels = newLabels;

			var newBasic = new int[cf.BasicVariableIndices.Length + 1];
			Array.Copy(cf.BasicVariableIndices, newBasic, cf.BasicVariableIndices.Length);
			newBasic[newBasic.Length - 1] = newCutCol;
			cf.BasicVariableIndices = newBasic;
		}

		private static int CountGomoryLabels(CanonicalForm cf) => cf.ColumnLabels.Count(l => l.StartsWith("g"));

		private static double FractionalPart(double v)
		{
			double f = v - Math.Floor(v);
			// Guard against floating noise landing just above 0 or just below 1.
			if (f < Tolerance || f > 1 - Tolerance) return 0d;
			return f;
		}

		// Helpers
		private static List<(int OriginalIndex, int Column)> GetIntegerColumns(LPModel model, CanonicalForm cf)
		{
			var list = new List<(int, int)>();
			for (int j = 0; j < model.VariableCount; j++)
			{
				if (model.SignRestrictions[j] == VariableType.Integer || model.SignRestrictions[j] == VariableType.Binary)
					list.Add((j, cf.VariableMaps[j].Components[0].ColumnIndex));
			}
			return list;
		}

		/// First basic row whose basic variable is an integer-restricted column with a fractional value.
		private static int FindFractionalRow(CanonicalForm cf, List<(int OriginalIndex, int Column)> integerColumns, out double value)
		{
			int rhsCol = cf.Tableau.GetLength(1) - 1;
			var intCols = new HashSet<int>(integerColumns.Select(t => t.Column));

			for (int i = 0; i < cf.BasicVariableIndices.Length; i++)
			{
				int basicCol = cf.BasicVariableIndices[i];
				if (!intCols.Contains(basicCol)) continue;

				double v = cf.Tableau[i + 1, rhsCol];
				double frac = FractionalPart(v);
				if (frac > Tolerance)
				{
					value = v;
					return i + 1;
				}
			}

			value = 0;
			return -1;
		}

		private static void PrintTableau(OutputWriter output, CanonicalForm cf, string label)
		{
			int rows = cf.Tableau.GetLength(0);
			int cols = cf.Tableau.GetLength(1);

			var columnHeaders = new string[cols];
			Array.Copy(cf.ColumnLabels, columnHeaders, cf.ColumnLabels.Length);
			columnHeaders[cols - 1] = "RHS";

			var rowLabels = new string[rows];
			rowLabels[0] = "z";
			for (int i = 0; i < cf.BasicVariableIndices.Length; i++)
				rowLabels[i + 1] = cf.ColumnLabels[cf.BasicVariableIndices[i]];

			output.AppendLine();
			output.AppendLine(label + ":");
			output.AppendTable(cf.Tableau, columnHeaders, rowLabels);
		}

		private static Solution ExtractSolution(LPModel model, CanonicalForm cf, OutputWriter output)
		{
			int rhsCol = cf.Tableau.GetLength(1) - 1;
			int totalCols = cf.Tableau.GetLength(1) - 1; // excluding RHS

			var colValues = new double[totalCols];
			for (int i = 0; i < cf.BasicVariableIndices.Length; i++)
			{
				int col = cf.BasicVariableIndices[i];
				if (col < totalCols)
					colValues[col] = cf.Tableau[i + 1, rhsCol];
			}

			var originalValues = new double[cf.OriginalVariableCount];
			for (int j = 0; j < cf.OriginalVariableCount; j++)
			{
				double v = 0d;
				foreach (var (colIndex, sign) in cf.VariableMaps[j].Components)
					v += sign * colValues[colIndex];
				originalValues[j] = v;
			}

			double objRaw = cf.Tableau[0, rhsCol];
			double objective = cf.OriginalWasMinimization ? -objRaw : objRaw;

			output.AppendLine();
			output.AppendLine($"OPTIMAL (integer): Objective = {OutputWriter.Round3(objective):0.000}");
			for (int j = 0; j < originalValues.Length; j++)
				output.AppendLine($"x{j + 1} = {OutputWriter.Round3(originalValues[j]):0.000}");

			return new Solution
			{
				Status = SolveStatus.Optimal,
				ObjectiveValue = objective,
				VariableValues = originalValues,
				FinalTableau = cf.Tableau,
				BasicVariableIndices = cf.BasicVariableIndices
			};
		}
	}
}
