using UnityEngine;

namespace MechaXCOM
{
    public struct CoverAttackPreview
    {
        public bool CanAttack;
        public bool UsesAmmo;
        public AttackType Type;
        public string FailureReason, WeaponName;
        public CoverObject Target;
        public Vector2Int ImpactCell;
        public int CurrentHP, MaxHP, MinDamage, MaxDamage, APCost, AmmoLoaded, MagazineCapacity;
    }
    public struct CoverAttackResult
    {
        public bool WasExecuted, WasDestroyed;
        public string FailureReason;
        public CoverObject Target;
        public int DamageDealt, HPAfter;
    }
}
