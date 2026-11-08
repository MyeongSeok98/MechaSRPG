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
        public bool IsProvidingCover => isActiveAndEnabled && Collider && Collider.enabled &&
            (!TryGetComponent<CoverDurability>(out var health) || !health.IsDestroyed);
        public event System.Action Changed;
        public void Configure(CoverType type) =>
            coverType = type == CoverType.Full ? CoverHeight.Full : CoverHeight.Half;
        internal void NotifyChanged() => Changed?.Invoke();
        private void OnEnable() => NotifyChanged();
        private void OnDisable()
        { if (!TryGetComponent<CoverDurability>(out var health) || !health.IsDestroyed) NotifyChanged(); }
    }
}

