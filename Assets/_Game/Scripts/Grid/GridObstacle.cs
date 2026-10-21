using UnityEngine;

namespace MechaXCOM
{
    [RequireComponent(typeof(BoxCollider))]
    public class GridObstacle : MonoBehaviour
    {
        [SerializeField, Tooltip("Enable for buildings. Disable for ravines or water that only block movement.")]
        private bool blocksShots = true;
        public bool BlocksShots => blocksShots;
        public BoxCollider Collider => GetComponent<BoxCollider>();
    }
}
