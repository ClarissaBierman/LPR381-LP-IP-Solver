using LPR381_Project.Modles;
using LPR381_Project.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381_Project.Algorithms
{
    internal class SensitivityAnalysis
    {
        public static void Run(LPModel model, Solution solution, OutputWriter output)
        {
            if (solution.Status != SolveStatus.Optimal)
            {
                output.AppendLine("Sensitivity analysis requires an optimal solution.");
                return;
            }

            output.AppendHeader("Sensitivity Analysis & Special Cases (Person 4)");

            var canon = CanonicalFormBuilder.Build(model);
            int m = model.Constraints.Count;
            int rhsCol = solution.FinalTableau.GetLength(1) - 1;

            // Extract B^-1
            double[,] B_inv = new double[m, m];
            for (int i = 0; i < m; i++)
            {
                int initialCol = canon.InitialBasicVariableIndices[i];
                for (int r = 0; r < m; r++)
                {
                    B_inv[r, i] = solution.FinalTableau[r + 1, initialCol];
                }
            }

            // Shadow Prices (Dual variables)
            output.AppendHeader("Shadow Prices (Dual Variables)");
            double[] dualVars = new double[m];
            for (int i = 0; i < m; i++)
            {
                int initialCol = canon.InitialBasicVariableIndices[i];
                double zRowVal = solution.FinalTableau[0, initialCol];
                if (canon.ArtificialColumns.Contains(initialCol))
                {
                    dualVars[i] = zRowVal - canon.BigM;
                }
                else
                {
                    dualVars[i] = zRowVal;
                }
                output.AppendLine($"Constraint {i + 1} ({model.Constraints[i].Relation}): Shadow Price = {OutputWriter.Round3(dualVars[i]):0.000}");
            }

            // RHS Ranging
            output.AppendHeader("RHS Ranging");
            for (int i = 0; i < m; i++)
            {
                double currentB = model.Constraints[i].Rhs;
                double minDelta = double.NegativeInfinity;
                double maxDelta = double.PositiveInfinity;

                for (int r = 0; r < m; r++)
                {
                    double binv_ri = B_inv[r, i];
                    double xb_r = solution.FinalTableau[r + 1, rhsCol];

                    if (Math.Abs(binv_ri) > 1e-9)
                    {
                        double limit = -xb_r / binv_ri;
                        if (binv_ri > 0)
                        {
                            if (limit > minDelta) minDelta = limit;
                        }
                        else
                        {
                            if (limit < maxDelta) maxDelta = limit;
                        }
                    }
                }

                double lowerBound = minDelta == double.NegativeInfinity ? double.NegativeInfinity : currentB + minDelta;
                double upperBound = maxDelta == double.PositiveInfinity ? double.PositiveInfinity : currentB + maxDelta;

                string lowerStr = double.IsNegativeInfinity(lowerBound) ? "-Infinity" : OutputWriter.Round3(lowerBound).ToString("0.000");
                string upperStr = double.IsPositiveInfinity(upperBound) ? "+Infinity" : OutputWriter.Round3(upperBound).ToString("0.000");
                output.AppendLine($"Constraint {i + 1} RHS ({currentB}): [{lowerStr}, {upperStr}]");
            }

            // Objective Coefficient Ranging
            output.AppendHeader("Objective Coefficient Ranging");
            for (int j = 0; j < model.VariableCount; j++)
            {
                if (model.SignRestrictions[j] != VariableType.Positive && model.SignRestrictions[j] != VariableType.Integer && model.SignRestrictions[j] != VariableType.Binary)
                {
                    output.AppendLine($"x{j + 1}: Skipping ranging (only supported for simple positive/binary/integer variables currently).");
                    continue;
                }

                int colIndex = canon.VariableMaps[j].Components[0].ColumnIndex;
                double currentC = model.ObjectiveCoefficients[j];
                
                int basicRow = -1;
                for (int r = 0; r < m; r++)
                {
                    if (solution.BasicVariableIndices[r] == colIndex)
                    {
                        basicRow = r + 1;
                        break;
                    }
                }

                if (basicRow == -1) // Non-basic
                {
                    double zMinusC = solution.FinalTableau[0, colIndex];
                    double minC = double.NegativeInfinity;
                    double maxC = currentC + zMinusC;
                    if (model.Objective == ObjectiveType.Min)
                    {
                        maxC = double.PositiveInfinity;
                        minC = currentC - zMinusC; 
                    }
                    string minStr = double.IsNegativeInfinity(minC) ? "-Infinity" : OutputWriter.Round3(minC).ToString("0.000");
                    string maxStr = double.IsPositiveInfinity(maxC) ? "+Infinity" : OutputWriter.Round3(maxC).ToString("0.000");
                    output.AppendLine($"x{j + 1} (Non-Basic): [{minStr}, {maxStr}]");
                }
                else // Basic
                {
                    double minDelta = double.NegativeInfinity;
                    double maxDelta = double.PositiveInfinity;

                    for (int k = 0; k < rhsCol; k++)
                    {
                        if (Array.IndexOf(solution.BasicVariableIndices, k) != -1) continue; 
                        if (canon.ArtificialColumns.Contains(k)) continue; 

                        double zMinusC_k = solution.FinalTableau[0, k];
                        double y_rk = solution.FinalTableau[basicRow, k];

                        if (Math.Abs(y_rk) > 1e-9)
                        {
                            double limit = zMinusC_k / y_rk;
                            if (y_rk > 0)
                            {
                                if (limit < maxDelta) maxDelta = limit;
                            }
                            else
                            {
                                if (limit > minDelta) minDelta = limit;
                            }
                        }
                    }

                    double minC = currentC + minDelta;
                    double maxC = currentC + maxDelta;

                    if (model.Objective == ObjectiveType.Min)
                    {
                        double tempMin = currentC - maxDelta;
                        double tempMax = currentC - minDelta;
                        minC = tempMin;
                        maxC = tempMax;
                    }

                    string minStr = double.IsNegativeInfinity(minC) ? "-Infinity" : OutputWriter.Round3(minC).ToString("0.000");
                    string maxStr = double.IsPositiveInfinity(maxC) ? "+Infinity" : OutputWriter.Round3(maxC).ToString("0.000");
                    output.AppendLine($"x{j + 1} (Basic): [{minStr}, {maxStr}]");
                }
            }

            // Duality
            output.AppendHeader("Duality Verification");
            output.AppendLine("Primal Objective Value: " + OutputWriter.Round3(solution.ObjectiveValue));
            double dualObjective = 0;
            for (int i = 0; i < m; i++)
            {
                dualObjective += dualVars[i] * model.Constraints[i].Rhs;
            }
            if (model.Objective == ObjectiveType.Min) dualObjective = -dualObjective;
            output.AppendLine("Calculated Dual Objective: " + OutputWriter.Round3(dualObjective));
            if (Math.Abs(solution.ObjectiveValue - dualObjective) < 1e-4)
            {
                output.AppendLine("Strong duality holds: Primal == Dual.");
            }
            else
            {
                output.AppendLine("Strong duality check failed.");
            }
            
            output.AppendHeader("Dual Problem Formulation");
            output.AppendLine($"Objective: {(model.Objective == ObjectiveType.Max ? "Min" : "Max")} W = " + 
                string.Join(" + ", model.Constraints.Select((c, i) => $"{c.Rhs} y{i+1}")).Replace("+ -", "- "));
            for (int j = 0; j < model.VariableCount; j++)
            {
                var terms = new List<string>();
                for (int i = 0; i < m; i++)
                {
                    double coeff = model.Constraints[i].Coefficients[j];
                    if (coeff != 0)
                        terms.Add($"{coeff} y{i+1}");
                }
                if (terms.Count == 0) terms.Add("0");
                
                string rel = ">=";
                if (model.Objective == ObjectiveType.Max)
                {
                    if (model.SignRestrictions[j] == VariableType.Positive) rel = ">=";
                    else if (model.SignRestrictions[j] == VariableType.Urs) rel = "=";
                    else rel = "<=";
                }
                else
                {
                    if (model.SignRestrictions[j] == VariableType.Positive) rel = "<=";
                    else if (model.SignRestrictions[j] == VariableType.Urs) rel = "=";
                    else rel = ">=";
                }
                
                output.AppendLine($"Constraint {j+1}: {string.Join(" + ", terms).Replace("+ -", "- ")} {rel} {model.ObjectiveCoefficients[j]}");
            }
            
            output.AppendLine("Dual Variable Restrictions:");
            for (int i = 0; i < m; i++)
            {
                string res = "urs";
                if (model.Objective == ObjectiveType.Max)
                {
                    if (model.Constraints[i].Relation == RelationType.LessOrEqual) res = ">= 0";
                    else if (model.Constraints[i].Relation == RelationType.GreaterOrEqual) res = "<= 0";
                }
                else
                {
                    if (model.Constraints[i].Relation == RelationType.GreaterOrEqual) res = ">= 0";
                    else if (model.Constraints[i].Relation == RelationType.LessOrEqual) res = "<= 0";
                }
                output.AppendLine($"y{i+1} {res}");
            }
            
            // Adding Activities / Constraints Theory
            output.AppendHeader("Adding New Activities or Constraints");
            output.AppendLine("To add a new activity (variable x_new with coefficients c_new and A_new):");
            output.AppendLine("1. Calculate its reduced cost: Z_new - C_new = C_B * B^-1 * A_new - c_new");
            output.AppendLine("2. If Z_new - C_new < 0 (for Max problem), the current basis is no longer optimal. The new activity should enter the basis.");
            output.AppendLine();
            output.AppendLine("To add a new constraint:");
            output.AppendLine("1. Check if the current optimal solution satisfies the new constraint.");
            output.AppendLine("2. If it does, the current solution remains optimal.");
            output.AppendLine("3. If it does not, add the constraint to the final tableau and use the Dual Simplex Method to restore feasibility.");
        }
    }
}
