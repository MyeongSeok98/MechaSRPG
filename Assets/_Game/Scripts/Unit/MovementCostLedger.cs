using System;
using System.Globalization;

namespace MechaXCOM
{
    // Prepaid fractional AP is equivalent to ceil(total movement cost), without
    // repricing earlier steps when damage changes the cost of the next cell.
    public sealed class MovementCostLedger
    {
        private const double Epsilon = 1e-9;
        public double PrepaidAP { get; private set; }
        public long PaidAP { get; private set; }
        public void ResetForTurn() { PrepaidAP = 0; PaidAP = 0; }
        private static bool ValidRate(float rate) => rate > 0 && !float.IsNaN(rate) && !float.IsInfinity(rate);
        // Interpret the authored float's shortest round-trip decimal (e.g.1.4),
        // rather than its binary expansion, before testing integer AP boundaries.
        internal static double RateValue(float rate) => double.Parse(rate.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        private static double SnapPositiveInteger(double value)
        {
            double nearest = Math.Round(value);
            return nearest >= 1 && Math.Abs(value - nearest) <= Epsilon ? nearest : value;
        }
        public double GetAdditionalAPCost(int cells, float rate)
        {
            if (cells < 0 || !ValidRate(rate)) return double.PositiveInfinity;
            if (cells == 0) return 0;
            double needed = cells / RateValue(rate) - PrepaidAP;
            // Only existing prepaid credit may absorb a near-zero subtraction error.
            // A positive first step must cost >=1 even at an enormous authored rate.
            if (needed <= 0 || (PrepaidAP > 0 && needed <= Epsilon)) return 0;
            return Math.Ceiling(SnapPositiveInteger(needed));
        }
        public bool CanAfford(int cells, float rate, int availableAP) => availableAP >= 0 &&
            GetAdditionalAPCost(cells, rate) <= availableAP;
        public int GetAffordableCells(float rate, int availableAP)
        {
            if (!ValidRate(rate) || availableAP < 0) return 0;
            double count = Math.Floor(SnapPositiveInteger((availableAP + PrepaidAP) * RateValue(rate)));
            int result = count >= int.MaxValue ? int.MaxValue : (int)Math.Max(0, count);
            if (result > 0 && !CanAfford(result, rate, availableAP)) result--;
            return result;
        }
        public bool TryCommitStep(float rate, int availableAP, out int charge)
        {
            charge = 0;
            double required = GetAdditionalAPCost(1, rate);
            if (availableAP < 0 || required > availableAP) return false;
            charge = (int)required;
            PrepaidAP = Math.Max(0, PrepaidAP + charge - 1d / RateValue(rate));
            if (PrepaidAP < Epsilon) PrepaidAP = 0;
            PaidAP += charge;
            return true;
        }
    }
}
