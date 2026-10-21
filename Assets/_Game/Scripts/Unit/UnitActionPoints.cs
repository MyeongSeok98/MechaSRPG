using UnityEngine;

namespace MechaXCOM
{
    public class UnitActionPoints : MonoBehaviour
    {
        [SerializeField, Min(0)] private int maxPoints = 5;
        public int MaxPoints => maxPoints;
        public int CurrentPoints { get; private set; }
        public void Configure(int maxPoints) => this.maxPoints = Mathf.Max(0, maxPoints);
        public void ResetForTurn() => CurrentPoints = maxPoints;
        public bool CanSpend(int cost) => cost >= 0 && CurrentPoints >= cost;
        public bool TrySpend(int cost)
        {
            if (!CanSpend(cost)) return false;
            CurrentPoints -= cost;
            return true;
        }
        private void OnValidate() => maxPoints = Mathf.Max(0, maxPoints);
    }
}
