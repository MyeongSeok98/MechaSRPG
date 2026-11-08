using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public struct IndirectAttackPreview
    {
        public bool CanAttack;
        public string FailureReason, WeaponName;
        public Vector2Int Center;
        public int Radius, MinHitParts, MaxHitParts, MinDamage, MaxDamage, APCost;
        public int AmmoLoaded, MagazineCapacity, SpareMagazines;
        // Ownership or pre-shot team visibility only. Never contains an unseen enemy.
        public IReadOnlyList<BattleUnit> VisibleTargets;
        public IReadOnlyList<CoverObject> VisibleCovers;
        public int MinCoverDamage, MaxCoverDamage;
        public float CoverDamageMultiplier;
    }
    public struct IndirectUnitDamage
    {
        public BattleUnit Target;
        public IReadOnlyList<PartDamageResult> Parts;
    }
    public struct IndirectAttackResult
    {
        public bool WasExecuted;
        public string FailureReason;
        public Vector2Int Center;
        // Player-facing results only contain units known before this shot.
        public IReadOnlyList<IndirectUnitDamage> Targets;
        public IReadOnlyList<CoverAttackResult> Covers;
    }
}
