using UnityEngine;

namespace MechaXCOM
{
    [RequireComponent(typeof(BoxCollider))]
    public class CoverObject : MonoBehaviour
    {
        public enum CoverHeight { Half, Full }
        [SerializeField] private CoverHeight coverType = CoverHeight.Half;
        public CoverType Type => coverType == CoverHeight.Full ? CoverType.Full : CoverType.Half;
        public BoxCollider Collider => GetComponent<BoxCollider>();
        public void Configure(CoverType type) =>
            coverType = type == CoverType.Full ? CoverHeight.Full : CoverHeight.Half;
    }
}

