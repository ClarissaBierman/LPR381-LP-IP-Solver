using LPR381_Project.IO;
using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381_Project.Algorithms
{

    internal class BranchAndBoundSimplex : IAlgorithm
    {
        public string Name => "Branch & Bound Simplex Algorithm";

        private const double Tolerance = 1e-6;

        private class BBNode
        {
            public int Id;
            public int? ParentId;
            public string BranchDescription;
            public LPModel Model;
        }

        private int _nextNodeId = 1;

        public Solution Solve(LPModel model, OutputWriter output)
        {
            output.AppendHeader(Name);

            if (!model.IsIntegerModel)
            {
                output.AppendLine("Note: no variables are restricted to Integer/Binary in this model - " +
                                   "Branch & Bound will just return the LP relaxation's optimal solution.");
            }

            var integerVarIndices = new List<int>();
            for (int j = 0; j < model.VariableCount; j++)
            {
                if (model.SignRestrictions[j] == VariableType.Integer || model.SignRestrictions[j] == VariableType.Binary)
                    integerVarIndices.Add(j);
            }

            var stack = new Stack<BBNode>();
            stack.Push(new BBNode { Id = _nextNodeId++, ParentId = null, BranchDescription = "Root (LP Relaxation)", Model = model });

            Solution best = null;
            int bestNodeId = -1;
            var summaryLines = new List<string>();

            while (stack.Count > 0)
            {
                var node = stack.Pop();

                output.AppendHeader($"Node {node.Id} [{node.BranchDescription}] (parent: {(node.ParentId?.ToString() ?? "-")})");

                var primal = new PrimalSimplex();
                Solution result = primal.Solve(node.Model, output);

                if (result.Status == SolveStatus.Infeasible)
                {
                    summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: fathomed (infeasible)");
                    continue;
                }

                if (result.Status == SolveStatus.Unbounded)
                {
                    summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: fathomed (unbounded)");
                    continue;
                }

                if (best != null
                    && !IsBetter(result.ObjectiveValue, best.ObjectiveValue, model.Objective)
                    && Math.Abs(result.ObjectiveValue - best.ObjectiveValue) > Tolerance)
                {
                    summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: fathomed (bound {result.ObjectiveValue:0.000} " +
                                      $"cannot beat incumbent {best.ObjectiveValue:0.000})");
                    continue;
                }

                int fracIdx = FindFractionalIntegerVariable(result, integerVarIndices);

                if (fracIdx == -1)
                {
                    summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: integer-feasible candidate, obj = {result.ObjectiveValue:0.000}");
                    if (best == null || IsBetter(result.ObjectiveValue, best.ObjectiveValue, model.Objective))
                    {
                        best = result;
                        bestNodeId = node.Id;
                        summaryLines.Add($"  >>> New best candidate (Node {node.Id})");
                    }
                    continue;
                }

                double val = result.VariableValues[fracIdx];
                double floorVal = Math.Floor(val);
                double ceilVal = Math.Ceiling(val);
                string varName = $"x{fracIdx + 1}";

                summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: branching on {varName} = {val:0.000} " +
                                  $"-> {varName} <= {floorVal}   and   {varName} >= {ceilVal}");

                var childA = new BBNode
                {
                    Id = _nextNodeId++,
                    ParentId = node.Id,
                    BranchDescription = $"{varName} <= {floorVal}",
                    Model = CloneWithExtraConstraint(node.Model, fracIdx, RelationType.LessOrEqual, floorVal)
                };
                var childB = new BBNode
                {
                    Id = _nextNodeId++,
                    ParentId = node.Id,
                    BranchDescription = $"{varName} >= {ceilVal}",
                    Model = CloneWithExtraConstraint(node.Model, fracIdx, RelationType.GreaterOrEqual, ceilVal)
                };

                stack.Push(childA);
                stack.Push(childB);
            }

            output.AppendHeader("Branch & Bound Summary");
            foreach (var line in summaryLines) output.AppendLine(line);
            output.AppendLine();

            if (best != null)
            {
                output.AppendLine($"BEST CANDIDATE: Node {bestNodeId}, Objective = {OutputWriter.Round3(best.ObjectiveValue):0.000}");
                for (int j = 0; j < best.VariableValues.Length; j++)
                    output.AppendLine($"x{j + 1} = {OutputWriter.Round3(best.VariableValues[j]):0.000}");
                return best;
            }

            output.AppendLine("No integer-feasible solution found.");
            return new Solution { Status = SolveStatus.Infeasible };
        }

        private static int FindFractionalIntegerVariable(Solution result, List<int> integerVarIndices)
        {
            foreach (int idx in integerVarIndices)
            {
                double val = result.VariableValues[idx];
                if (Math.Abs(val - Math.Round(val)) > Tolerance)
                    return idx;
            }
            return -1;
        }

        private static bool IsBetter(double candidateObj, double incumbentObj, ObjectiveType objective)
        {
            return objective == ObjectiveType.Max
                ? candidateObj > incumbentObj + Tolerance
                : candidateObj < incumbentObj - Tolerance;
        }

        private static LPModel CloneWithExtraConstraint(LPModel source, int varIndex, RelationType relation, double rhs)
        {
            var clone = new LPModel
            {
                Objective = source.Objective,
                ObjectiveCoefficients = (double[])source.ObjectiveCoefficients.Clone(),
                SignRestrictions = (VariableType[])source.SignRestrictions.Clone(),
                Constraints = source.Constraints.Select(c => new Constraint
                {
                    Coefficients = (double[])c.Coefficients.Clone(),
                    Relation = c.Relation,
                    Rhs = c.Rhs
                }).ToList()
            };

            var boundCoeffs = new double[clone.VariableCount];
            boundCoeffs[varIndex] = 1d;
            clone.Constraints.Add(new Constraint
            {
                Coefficients = boundCoeffs,
                Relation = relation,
                Rhs = rhs
            });

            return clone;
        }
    }
}