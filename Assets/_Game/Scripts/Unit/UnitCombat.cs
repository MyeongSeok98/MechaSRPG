using UnityEngine;
using System.Collections.Generic;

namespace MechaXCOM
{
    public enum AttackType { Ranged, Melee }
    public struct AttackStats
    {
        public int BaseHitChance, Range, APCost;
        public AttackType Type;
        public WeaponSlot Slot;
        public string DisplayName;
        public int MinDamage, MaxDamage;
        public float GripMultiplier;
        public WeaponData Weapon;
    }
    public class UnitCombat : MonoBehaviour
    {
        public bool TryGetAttack(WeaponSlot slot, out AttackStats stats, out string reason)
        {
            stats = default; reason = null;
            var loadout = GetComponent<UnitWeaponLoadout>();
            if (loadout)
            {
                if (!loadout.IsValid(out reason)) return false;
                if (slot != WeaponSlot.RightHand && slot != WeaponSlot.LeftHand)
                { reason = "장착 설정에서는 손 슬롯을 선택해야 합니다."; return false; }
                var weapon = slot == WeaponSlot.RightHand ? loadout.RightWeapon : loadout.LeftWeapon;
                var grip = slot == WeaponSlot.RightHand ? loadout.RightGrip : loadout.LeftGrip;
                if (!weapon) { reason = "선택한 손에 무기가 없습니다."; return false; }
                var unit = GetComponent<BattleUnit>();
                if (unit && unit.IsInitialized && unit.Health)
                {
                    bool leftDestroyed = unit.Health.GetCurrentHP(TargetBodyPart.LeftArm) == 0;
                    bool rightDestroyed = unit.Health.GetCurrentHP(TargetBodyPart.RightArm) == 0;
                    if (grip == GripMode.TwoHands && (leftDestroyed || rightDestroyed))
                    { reason = "양손 파지 무기는 양쪽 팔이 모두 필요합니다."; return false; }
                    if (slot == WeaponSlot.LeftHand ? leftDestroyed : rightDestroyed)
                    { reason = "파괴된 팔의 무기는 사용할 수 없습니다."; return false; }
                }
                stats = new AttackStats
                {
                    Type = weapon.AttackType, Slot = slot, DisplayName = weapon.DisplayName, Weapon = weapon,
                    MinDamage = weapon.MinDamage, MaxDamage = weapon.MaxDamage, BaseHitChance = weapon.BaseHitChance,
                    Range = weapon.Range, APCost = weapon.APCost,
                    GripMultiplier = weapon.GripRequirement == GripRequirement.Either && grip == GripMode.TwoHands
                        ? weapon.TwoHandHitMultiplier : 1f
                };
                return true;
            }
            reason = "Unit Weapon Loadout에 무기를 지정해야 합니다.";
            return false;
        }
        public IReadOnlyList<WeaponSlot> GetAttackSlots(AttackType type)
        {
            var slots = new List<WeaponSlot>(2);
            if (type != AttackType.Ranged && type != AttackType.Melee) return slots;
            foreach (var slot in new[] { WeaponSlot.RightHand, WeaponSlot.LeftHand })
                if (TryGetAttack(slot, out var attack, out _) && attack.Type == type) slots.Add(slot);
            return slots;
        }
        public AttackStats GetAttack(AttackType type)
        {
            var slots = GetAttackSlots(type);
            return slots.Count > 0 && TryGetAttack(slots[0], out var stats, out _) ? stats : default;
        }
    }
}
