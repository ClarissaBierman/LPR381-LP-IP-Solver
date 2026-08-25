using LPR381_Project.Modles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Algorithms
{
    public static class CanonicalFormBuilder
    {
        public static CanonicalForm Build(LPModel model, double bigM = 1_000_000d)
        {
            // --Work out how each original variable expands into columns ----
            var variableMaps = new List<VariableMap>();
            var expandedObjective = new List<double>(); 
            var columnLabels = new List<string>();

            // Internally we always MAXIMIZE. If the model is "min", negate the
            // objective coefficients here; ObjectiveValue is negated back to
            // the true (minimized) value once the Solution is built.
            double minToMaxSign = model.Objective == ObjectiveType.Min ? -1d : 1d;

            for (int j = 0; j < model.VariableCount; j++)
            {
                var map = new VariableMap { OriginalIndex = j };
                double c = minToMaxSign * model.ObjectiveCoefficients[j];

                switch (model.SignRestrictions[j])
                {
                    case VariableType.Negative:
                     
                        map.Components.Add((expandedObjective.Count, -1d));
                        expandedObjective.Add(-c);
                        columnLabels.Add($"x{j + 1}");
                        break;

                    case VariableType.Urs:
                       
                        map.Components.Add((expandedObjective.Count, 1d));
                        expandedObjective.Add(c);
                        columnLabels.Add($"x{j + 1}+");
                        map.Components.Add((expandedObjective.Count, -1d));
                        expandedObjective.Add(-c);
                        columnLabels.Add($"x{j + 1}-");
                        break;

                    case VariableType.Positive:
                    case VariableType.Integer:
                    case VariableType.Binary:
                    default:
                    
                        map.Components.Add((expandedObjective.Count, 1d));
                        expandedObjective.Add(c);
                        columnLabels.Add($"x{j + 1}");
                        break;
                }

                variableMaps.Add(map);
            }

            int decisionColumnCount = expandedObjective.Count;

            //  Expand each original constraint's coefficients the same way ----
            var expandedConstraintRows = new List<double[]>();
            var relations = new List<RelationType>();
            var rhsList = new List<double>();

            foreach (var c in model.Constraints)
            {
                var row = new double[decisionColumnCount];
                foreach (var map in variableMaps)
                {
                    double coeff = c.Coefficients[map.OriginalIndex];
                    foreach (var (col, sign) in map.Components)
                        row[col] = sign * coeff;
                }
                expandedConstraintRows.Add(row);
                relations.Add(c.Relation);
                rhsList.Add(c.Rhs);
            }

            // --Binary variables also need an explicit x_j <= 1 row ----
            for (int j = 0; j < model.VariableCount; j++)
            {
                if (model.SignRestrictions[j] != VariableType.Binary) continue;

                var row = new double[decisionColumnCount];
             
                row[variableMaps[j].Components[0].ColumnIndex] = 1d;
                expandedConstraintRows.Add(row);
                relations.Add(RelationType.LessOrEqual);
                rhsList.Add(1d);
            }

            int constraintCount = expandedConstraintRows.Count;

            //  Normalise RHS >= 0, then decide slack/surplus/artificial per row ----
            var extraColumnLabelsPerRow = new string[constraintCount]; 
            var needsArtificial = new bool[constraintCount];
            int slackCounter = 0;

            for (int i = 0; i < constraintCount; i++)
            {
                if (rhsList[i] < 0)
                {
                    for (int j = 0; j < decisionColumnCount; j++)
                        expandedConstraintRows[i][j] *= -1;
                    rhsList[i] *= -1;

                    switch (relations[i])
                    {
                        case RelationType.LessOrEqual:
                            relations[i] = RelationType.GreaterOrEqual;
                            break;
                        case RelationType.GreaterOrEqual:
                            relations[i] = RelationType.LessOrEqual;
                            break;
                        case RelationType.Equal:
                            relations[i] = RelationType.Equal;
                            break;
                    }
                }

                switch (relations[i])
                {
                    case RelationType.LessOrEqual:
                        extraColumnLabelsPerRow[i] = $"s{++slackCounter}";
                        needsArtificial[i] = false;
                        break;
                    case RelationType.GreaterOrEqual:
                        extraColumnLabelsPerRow[i] = $"s{++slackCounter}";
                        needsArtificial[i] = true;
                        break;
                    case RelationType.Equal:
                        extraColumnLabelsPerRow[i] = null;
                        needsArtificial[i] = true;
                        break;
                }
            }

            int slackSurplusColumnCount = slackCounter;
            int artificialColumnCount = 0;
            foreach (bool needs in needsArtificial)
                if (needs) artificialColumnCount++;

            int totalColumns = decisionColumnCount + slackSurplusColumnCount + artificialColumnCount + 1; 
            int rhsColumn = totalColumns - 1;

            var tableau = new double[constraintCount + 1, totalColumns];
            var basicVariableIndices = new int[constraintCount];
            var artificialColumns = new List<int>();
            var artificialColumnSet = new HashSet<int>(); 


            int nextSlackSurplusCol = decisionColumnCount;
            int nextArtificialCol = decisionColumnCount + slackSurplusColumnCount;

            for (int i = 0; i < constraintCount; i++)
            {
                for (int j = 0; j < decisionColumnCount; j++)
                    tableau[i + 1, j] = expandedConstraintRows[i][j];

                switch (relations[i])
                {
                    case RelationType.LessOrEqual:
                        {
                            int col = nextSlackSurplusCol++;
                            tableau[i + 1, col] = 1d;
                            columnLabels.EnsureIndex(col, extraColumnLabelsPerRow[i]);
                            basicVariableIndices[i] = col; 
                            break;
                        }
                    case RelationType.GreaterOrEqual:
                        {
                            int surplusCol = nextSlackSurplusCol++;
                            tableau[i + 1, surplusCol] = -1d;
                            columnLabels.EnsureIndex(surplusCol, extraColumnLabelsPerRow[i]);

                            int artCol = nextArtificialCol++;
                            tableau[i + 1, artCol] = 1d;
                            columnLabels.EnsureIndex(artCol, $"a{artificialColumns.Count + 1}");
                            artificialColumns.Add(artCol);
                            artificialColumnSet.Add(artCol);
                            basicVariableIndices[i] = artCol; 
                            break;
                        }
                    case RelationType.Equal:
                    default:
                        {
                            int artCol = nextArtificialCol++;
                            tableau[i + 1, artCol] = 1d;
                            columnLabels.EnsureIndex(artCol, $"a{artificialColumns.Count + 1}");
                            artificialColumns.Add(artCol);
                            artificialColumnSet.Add(artCol);
                            basicVariableIndices[i] = artCol; 
                            break;
                        }
                }

                tableau[i + 1, rhsColumn] = rhsList[i];
            }

            // -- Row 0 (objective): -c_j for decision vars, +M for artificials --
            for (int j = 0; j < decisionColumnCount; j++)
                tableau[0, j] = -expandedObjective[j];
            foreach (int artCol in artificialColumns)
                tableau[0, artCol] = bigM;

         
            for (int i = 0; i < constraintCount; i++)
            {
                if (!artificialColumnSet.Contains(basicVariableIndices[i])) continue;
                for (int j = 0; j < totalColumns; j++)
                    tableau[0, j] -= bigM * tableau[i + 1, j];
            }

            return new CanonicalForm
            {
                Tableau = tableau,
                ColumnLabels = columnLabels.ToArray(),
                BasicVariableIndices = basicVariableIndices,
                InitialBasicVariableIndices = (int[])basicVariableIndices.Clone(),
                ArtificialColumns = artificialColumns,
                VariableMaps = variableMaps,
                OriginalVariableCount = model.VariableCount,
                OriginalWasMinimization = model.Objective == ObjectiveType.Min,
                BigM = bigM
            };
        }
    }

    internal static class ListExtensions
    {
        public static void EnsureIndex(this List<string> list, int index, string value)
        {
            while (list.Count <= index) list.Add("");
            list[index] = value;
        }
    }
}
