using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381_Project.Modles
{
    public enum ObjectiveType { Max, Min }
    public class LPModel
    {
        public ObjectiveType Objective { get; set; }

        /// Objective function coefficients, in decision-variable order.
        public double[] ObjectiveCoefficients { get; set; } = System.Array.Empty<double>();

        public List<Constraint> Constraints { get; set; } = new List<Constraint>();

        /// Sign restriction per decision variable, same order as above.
        public VariableType[] SignRestrictions { get; set; } = System.Array.Empty<VariableType>();

        public int VariableCount => ObjectiveCoefficients.Length;

        /// True if any variable is Integer or Binary (an IP, not a pure LP).
        public bool IsIntegerModel
        {
            get
            {
                foreach (var v in SignRestrictions)
                    if (v == VariableType.Integer || v == VariableType.Binary)
                        return true;
                return false;
            }
        }
    }
}
