using UnityEngine;

namespace MechaXCOM
{
    public class UnitMobility : MonoBehaviour
    {
        [SerializeField, Min(0)] private int shortMoveBonus = 5;
        [SerializeField, Min(0)] private int mediumMoveBonus = 10;
        [SerializeField, Min(0)] private int longMoveBonus = 15;
        public int MovedCells { get; private set; }
        public int MovementEvasion { get; private set; }

        public void ResetMovement()
        {
            MovedCells = 0;
            MovementEvasion = 0;
        }
        public void RecordCellMoved()
        {
            MovedCells++;
            MovementEvasion = MovedCells >= 5 ? longMoveBonus :
                MovedCells >= 3 ? mediumMoveBonus : shortMoveBonus;
        }
        private void OnValidate()
        {
            shortMoveBonus = Mathf.Max(0, shortMoveBonus);
            mediumMoveBonus = Mathf.Max(0, mediumMoveBonus);
            longMoveBonus = Mathf.Max(0, longMoveBonus);
        }
    }
}
