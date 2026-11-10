using System;
using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent, RequireComponent(typeof(CoverObject))]
    public class CoverDurability : MonoBehaviour
    {
        [SerializeField, Min(1), InspectorName("최대 HP")] private int maxHP = 1000;
        [SerializeField, InspectorName("오브젝트 데이터 (선택)")] private DestructibleObjectData objectData;
        private bool initialized;
        private int initialMaxHP, currentHP;
        public int MaxHP { get { Initialize(); return initialMaxHP; } }
        public int CurrentHP { get { Initialize(); return currentHP; } }
        public bool IsDestroyed => CurrentHP == 0;
        public DestructibleObjectData Data => objectData;
        public bool ConfigureData(DestructibleObjectData data)
        {
            if (initialized || !data || !data.IsValid(out _)) return false;
            objectData = data; return true;
        }
        public event Action Changed;
        public event Action Destroyed;
        public void Initialize()
        {
            if (initialized) return;
            initialized = true; initialMaxHP = Mathf.Max(1, objectData ? objectData.MaxHP : maxHP); currentHP = initialMaxHP;
        }
        public int ApplyDamage(int amount)
        {
            Initialize();
            if (amount <= 0 || currentHP == 0 || !isActiveAndEnabled) return 0;
            int dealt = Math.Min(amount, currentHP); currentHP -= dealt;
            if (currentHP == 0)
            {
                DisableParts();
                gameObject.SetActive(false);
                GetComponent<CoverObject>().NotifyChanged();
                Destroyed?.Invoke();
            }
            Changed?.Invoke(); return dealt;
        }
        private void DisableParts()
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
        private void OnEnable() { if (initialized && currentHP == 0) DisableParts(); }
        private void OnValidate() => maxHP = Mathf.Max(1, maxHP);
    }
}
