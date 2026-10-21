using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class UnitMechaVisual : MonoBehaviour
    {
        // Serialized so an authored prefab preview is reused at battle start.
        [SerializeField, HideInInspector] private GameObject modelInstance;
        [SerializeField, HideInInspector] private GameObject appliedModelPrefab;
        public GameObject ModelInstance => modelInstance;

        public void Initialize(GameObject modelPrefab, float heightOffset)
        {
            if (!isActiveAndEnabled || !modelPrefab) return;
            var visual = transform.Find("Visual");
            if (!visual)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(transform, false);
                visual.localPosition = Vector3.down * heightOffset;
            }
            if (!modelInstance || appliedModelPrefab != modelPrefab)
            {
                if (modelInstance)
                {
                    modelInstance.SetActive(false);
                    if (Application.isPlaying) Destroy(modelInstance); else DestroyImmediate(modelInstance);
                }
                modelInstance = Instantiate(modelPrefab, visual, false);
                modelInstance.name = "Model";
                appliedModelPrefab = modelPrefab;
            }
            // The root collider remains the selection/logic collider.
            var primitive = GetComponent<Renderer>();
            if (primitive) primitive.enabled = false;
        }
    }
}
