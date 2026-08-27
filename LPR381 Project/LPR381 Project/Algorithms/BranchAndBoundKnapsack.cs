using LPR381_Project.IO;
using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381_Project.Algorithms
{
    internal class BranchAndBoundKnapsack : IAlgorithm
    {
        public string Name => "Branch & Bound Knapsack Algorithm";

        private const double Tolerance = 1e-6;

        private class KnapNode
        {
            public int Id;
            public int? ParentId;
            public string BranchDescription;

            /// Decision so far, indexed by ORIGINAL variable index. -1 = undecided, 0/1 = fixed.
            public int[] Fixed;

            /// How many items (in ratio order) have been decided.
            public int Level;
        }

        public Solution Solve(LPModel model, OutputWriter output)
        {
            output.AppendHeader(Name);

            int n = model.VariableCount;

            if (model.Constraints.Count == 0)
            {
                output.AppendLine("Model has no constraints - nothing to branch on.");
                return new Solution { Status = SolveStatus.Infeasible };
            }

            bool allBinary = model.SignRestrictions.All(v => v == VariableType.Binary);
            if (!allBinary)
            {
                output.AppendLine("Note: this solver is designed for 0/1 Knapsack problems (all variables 'bin'). " +
                                   "Non-binary variables here will still be branched as 0/1 - treat results with " +
                                   "caution if that isn't what the model intends.");
            }

            // Internally maximize: negate profits if the model is "min" and flip the objective back at the end.
            double minToMaxSign = model.Objective == ObjectiveType.Min ? -1d : 1d;
            var profit = model.ObjectiveCoefficients.Select(c => c * minToMaxSign).ToArray();

            // Primary constraint used for the fractional (Dantzig) bound: first "<=" constraint.
            int primaryIdx = model.Constraints.FindIndex(c => c.Relation == RelationType.LessOrEqual);
            if (primaryIdx == -1)
            {
                output.AppendLine("No '<=' capacity constraint found - cannot compute a Knapsack bound for this model.");
                return new Solution { Status = SolveStatus.Infeasible };
            }
            var primary = model.Constraints[primaryIdx];

            output.AppendLine($"Primary capacity constraint: constraint #{primaryIdx + 1}, capacity = {primary.Rhs:0.000}");

            // Decide branching/bounding order by profit/weight ratio, descending.
            // Items with zero or negative weight in the primary constraint are pushed to the front
            // (they never hurt the bound) to keep the ratio sort well-defined.
            var order = Enumerable.Range(0, n)
                .OrderByDescending(j =>
                {
                    double w = primary.Coefficients[j];
                    return Math.Abs(w) < Tolerance ? double.PositiveInfinity : profit[j] / w;
                })
                .ToArray();

            var stack = new Stack<KnapNode>();
            var rootFixed = Enumerable.Repeat(-1, n).ToArray();
            stack.Push(new KnapNode { Id = 1, ParentId = null, BranchDescription = "Root (all free)", Fixed = rootFixed, Level = 0 });
            int nextId = 2;

            double bestProfit = double.NegativeInfinity;
            int[] bestFixed = null;
            int bestNodeId = -1;
            var summaryLines = new List<string>();

            while (stack.Count > 0)
            {
                var node = stack.Pop();

                // Feasibility check across ALL constraints, given the items fixed to 1 so far
                // (free/undecided items are assumed 0 for this check - the least they could contribute).
                if (!IsPartiallyFeasible(model, node.Fixed))
                {
                    summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: fathomed (infeasible - fixed items alone violate a constraint)");
                    continue;
                }

                double currentProfit = 0d;
                for (int j = 0; j < n; j++)
                    if (node.Fixed[j] == 1) currentProfit += profit[j];

                double bound = ComputeBound(profit, primary, order, node.Fixed, currentProfit);

                if (bestFixed != null && bound <= bestProfit + Tolerance)
                {
                    summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: fathomed (bound {bound:0.000} " +
                                      $"cannot beat incumbent {bestProfit:0.000})");
                    continue;
                }

                if (node.Level == n)
                {
                    // Leaf: every item decided - integer-feasible by construction.
                    summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: complete candidate, profit = {currentProfit:0.000}");
                    if (currentProfit > bestProfit + Tolerance)
                    {
                        bestProfit = currentProfit;
                        bestFixed = (int[])node.Fixed.Clone();
                        bestNodeId = node.Id;
                        summaryLines.Add($"  >>> New best candidate (Node {node.Id})");
                    }
                    continue;
                }

                int itemIdx = order[node.Level];
                summaryLines.Add($"Node {node.Id} [{node.BranchDescription}]: bound = {bound:0.000}, branching on x{itemIdx + 1} " +
                                  $"(profit={profit[itemIdx]:0.000}, weight={primary.Coefficients[itemIdx]:0.000})");

                var fixedExclude = (int[])node.Fixed.Clone();
                fixedExclude[itemIdx] = 0;
                var childExclude = new KnapNode
                {
                    Id = nextId++,
                    ParentId = node.Id,
                    BranchDescription = $"x{itemIdx + 1} = 0",
                    Fixed = fixedExclude,
                    Level = node.Level + 1
                };

                var fixedInclude = (int[])node.Fixed.Clone();
                fixedInclude[itemIdx] = 1;
                var childInclude = new KnapNode
                {
                    Id = nextId++,
                    ParentId = node.Id,
                    BranchDescription = $"x{itemIdx + 1} = 1",
                    Fixed = fixedInclude,
                    Level = node.Level + 1
                };

                // Push "exclude" first, "include" second, so "include" (the more promising
                // branch by ratio order) is popped and explored first - finds good
                // incumbents earlier, which improves pruning.
                stack.Push(childExclude);
                stack.Push(childInclude);
            }

            output.AppendHeader("Branch & Bound Knapsack Summary");
            foreach (var line in summaryLines) output.AppendLine(line);
            output.AppendLine();

            if (bestFixed == null)
            {
                output.AppendLine("No integer-feasible solution found.");
                return new Solution { Status = SolveStatus.Infeasible };
            }

            double trueObjective = minToMaxSign * bestProfit; // undo the max-transform for "min" models
            output.AppendLine($"BEST CANDIDATE: Node {bestNodeId}, Objective = {OutputWriter.Round3(trueObjective):0.000}");
            for (int j = 0; j < n; j++)
                output.AppendLine($"x{j + 1} = {bestFixed[j]}.000");

            return new Solution
            {
                Status = SolveStatus.Optimal,
                ObjectiveValue = trueObjective,
                VariableValues = bestFixed.Select(v => (double)v).ToArray()
            };
        }

        /// Checks every constraint assuming undecided (-1) items contribute 0 -
        /// i.e. can the items already fixed to 1 possibly still lead to a feasible leaf?
        private static bool IsPartiallyFeasible(LPModel model, int[] fixedVals)
        {
            foreach (var c in model.Constraints)
            {
                double lhs = 0d;
                for (int j = 0; j < fixedVals.Length; j++)
                    if (fixedVals[j] == 1) lhs += c.Coefficients[j];

                switch (c.Relation)
                {
                    case RelationType.LessOrEqual:
                        if (lhs > c.Rhs + Tolerance) return false;
                        break;
                    case RelationType.Equal:
                        // Can only truly verify once every variable is decided; only reject
                        // early if the fixed-1 items alone already overshoot.
                        if (lhs > c.Rhs + Tolerance) return false;
                        break;
                    case RelationType.GreaterOrEqual:
                        // Free variables could still push this up later, so fixed-1 items
                        // alone can't make a ">=" constraint infeasible - nothing to check here.
                        break;
                }
            }
            return true;
        }

        /// Classic Dantzig fractional-knapsack bound: fill remaining capacity greedily
        /// by ratio order, taking one item fractionally at the break point.
        private static double ComputeBound(double[] profit, Constraint primary, int[] order, int[] fixedVals, double currentProfit)
        {
            double usedCapacity = 0d;
            for (int j = 0; j < fixedVals.Length; j++)
                if (fixedVals[j] == 1) usedCapacity += primary.Coefficients[j];

            double remainingCapacity = primary.Rhs - usedCapacity;
            double bound = currentProfit;

            foreach (int j in order)
            {
                if (fixedVals[j] != -1) continue; // already decided, skip

                double w = primary.Coefficients[j];

                if (w <= Tolerance)
                {
                    // Free item with (near) zero or negative weight: always worth including
                    // in the bound if it has positive profit, doesn't consume capacity.
                    if (profit[j] > 0) bound += profit[j];
                    continue;
                }

                if (w <= remainingCapacity + Tolerance)
                {
                    bound += profit[j];
                    remainingCapacity -= w;
                }
                else
                {
                    double fraction = remainingCapacity / w;
                    if (fraction > 0) bound += fraction * profit[j];
                    remainingCapacity = 0;
                    // Once capacity is used up, no later (lower-ratio) item can add anything more.
                    break;
                }
            }

            return bound;
        }
    }
}