using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LPR381_Project.Modles;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.IO
{
    internal class InputParser
    {
        public static LPModel Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Input file not found: {filePath}");

            var lines = File.ReadAllLines(filePath)
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToArray();

            if (lines.Length < 3)
                throw new FormatException("Input file must have an objective line, at least one constraint line, and a sign-restriction line.");

            var model = new LPModel();
            model.Constraints = new List<Constraint>();

            // ---- Line 1: objective ----
            var objTokens = lines[0].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (objTokens.Length < 2)
                throw new FormatException("Objective line must contain direction (max/min) followed by coefficients.");

            string direction = objTokens[0].Trim().ToLowerInvariant();

            switch (direction)
            {
                case "max":
                    model.Objective = ObjectiveType.Max;
                    break;
                case "min":
                    model.Objective = ObjectiveType.Min;
                    break;
                default:
                    throw new FormatException($"Unknown objective direction: {objTokens[0]}");
            }

            model.ObjectiveCoefficients = objTokens.Skip(1)
                                                    .Select(ParseSignedNumber)
                                                    .ToArray();

            int varCount = model.ObjectiveCoefficients.Length;

            // ---- Middle lines: constraints ----
            var constraintLines = lines.Skip(1).Take(lines.Length - 2);

            foreach (var line in constraintLines)
            {
                var tokens = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length < varCount + 1)                 
                    throw new FormatException(
                        $"Constraint must have {varCount} coefficient(s) followed by a relation and RHS. Line: {line}");

                var coeffs = tokens.Take(varCount)
                                    .Select(ParseSignedNumber)
                                    .ToArray();

                string remainder = string.Concat(tokens.Skip(varCount));
                var (relation, rhs) = ParseRelationAndRhs(remainder);

                model.Constraints.Add(new Constraint
                {
                    Coefficients = coeffs,
                    Relation = relation,
                    Rhs = rhs
                });
            }

            // ---- Last line: sign restrictions ----
            var signTokens = lines[lines.Length - 1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (signTokens.Length != varCount)                     
                throw new FormatException(
                    $"Expected {varCount} sign restriction(s), but found {signTokens.Length}.");

            model.SignRestrictions = signTokens.Select(ParseVariableType).ToArray();

            Validate(model);
            return model;
        }

      
        private static double ParseSignedNumber(string token)
        {
            return double.Parse(token, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

      
        private static (RelationType Relation, double Rhs) ParseRelationAndRhs(string token)
        {
            string op;
            RelationType relation;

            if (token.StartsWith("<="))
            {
                op = "<=";
                relation = RelationType.LessOrEqual;
            }
            else if (token.StartsWith(">="))
            {
                op = ">=";
                relation = RelationType.GreaterOrEqual;
            }
            else if (token.StartsWith("="))
            {
                op = "=";
                relation = RelationType.Equal;
            }
            else
            {
                throw new FormatException($"Unknown relation operator in constraint: '{token}'");
            }

            string rhsPart = token.Substring(op.Length);
            if (!double.TryParse(rhsPart, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double rhs))
                throw new FormatException($"Could not parse right-hand-side value from: '{token}'");

            return (relation, rhs);
        }

        private static VariableType ParseVariableType(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new FormatException("Sign restriction token cannot be null or empty.");

            string normalized = token.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "+":
                    return VariableType.Positive;
                case "-":
                    return VariableType.Negative;
                case "urs":
                    return VariableType.Urs;
                case "int":
                    return VariableType.Integer;
                case "bin":
                    return VariableType.Binary;
                default:
                    throw new FormatException($"Unknown sign restriction: {token}");
            }
        }

        private static void Validate(LPModel model)
        {
            int n = model.VariableCount;
            foreach (var c in model.Constraints)
            {
                if (c.Coefficients.Length != n)
                    throw new FormatException("A constraint's coefficient count does not match the number of decision variables.");
            }
            if (model.SignRestrictions.Length != n)
                throw new FormatException("The sign-restriction line does not match the number of decision variables.");
        }
    }
}
