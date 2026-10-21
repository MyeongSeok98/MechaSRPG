using UnityEngine;

namespace MechaXCOM
{
    public class UnitVision : MonoBehaviour
    {
        [SerializeField, Min(0)] private int detectionRange = 10;
        public int DetectionRange => Mathf.Max(0, detectionRange);
        public event System.Action Changed;

        public void Configure(int cells)
        {
            detectionRange = Mathf.Max(0, cells);
            Changed?.Invoke();
        }
        private void OnEnable() => Changed?.Invoke();
        private void OnDisable() => Changed?.Invoke();
    }
}
