using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Modles
{
    public enum RelationType
    {
        LessOrEqual,    // <=
        GreaterOrEqual, // >=
        Equal           // =
    }
     public class Constraint
    {
        public double[] Coefficients { get; set; } = System.Array.Empty<double>();
        public RelationType Relation { get; set; }
        public double Rhs { get; set; }

        public override string ToString()
        {
            string relSymbol;
            switch (Relation)
            {
                case RelationType.LessOrEqual:
                    relSymbol = "<=";
                    break;
                case RelationType.GreaterOrEqual:
                    relSymbol = ">=";
                    break;
                case RelationType.Equal:
                    relSymbol = "=";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Relation), Relation, "Unknown relation type");
            }
            return string.Join(" ", Coefficients) + $" {relSymbol} {Rhs}";
        }
    }
}
