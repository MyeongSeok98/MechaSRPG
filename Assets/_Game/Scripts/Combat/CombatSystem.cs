using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public class CombatSystem : MonoBehaviour
    {
        private GridManager grid;
        private CoverSystem cover;
        private TurnManager turns;
        private OverwatchSystem overwatch;
        public void ConfigureOverwatch(OverwatchSystem system) => overwatch = system;
        private Func<int> rollSource = () => UnityEngine.Random.Range(0, 100);
        private Func<int, int, int> damageRollSource = RollDamage;
        public void SetDamageRollSource(Func<int, int, int> source) => damageRollSource = source ?? RollDamage;
        // Both bounds are inclusive without overflowing when maximum == int.MaxValue.
        private static int RollDamage(int minimum, int maximum) => UnityEngine.Random.Range(minimum - 1, maximum) + 1;
        public void Initialize(GridManager grid, CoverSystem cover, TurnManager turns)
        { this.grid = grid; this.cover = cover; this.turns = turns; }
        public void SetRollSource(Func<int> rollSource) =>
            this.rollSource = rollSource ?? (() => UnityEngine.Random.Range(0, 100));
        public List<GridCell> GetAttackRangeCells(BattleUnit attacker, AttackType type = AttackType.Ranged)
        {
            return TryDefaultSlot(attacker, type, out var slot) ? GetWeaponRangeCells(attacker, slot) : new List<GridCell>();
        }
        private static bool TryDefaultSlot(BattleUnit attacker, AttackType type, out WeaponSlot slot)
        {
            slot = default;
            if (!attacker || !attacker.Combat) return false;
            var slots = attacker.Combat.GetAttackSlots(type);
            if (slots.Count == 0) return false;
            slot = slots[0]; return true;
        }
        public List<GridCell> GetWeaponRangeCells(BattleUnit attacker, WeaponSlot slot)
        {
            var result = new List<GridCell>();
            if (!isActiveAndEnabled || !grid || !turns || !attacker || !turns.CanAct(attacker) ||
                !attacker.Combat || !attacker.Combat.isActiveAndEnabled || !attacker.ActionPoints ||
                !attacker.Combat.TryGetAttack(slot, out var config, out _) || config.MinDamage <= 0 || config.Range <= 0 ||
                !attacker.ActionPoints.isActiveAndEnabled || !attacker.ActionPoints.CanSpend(config.APCost) ||
                !grid.TryGetCell(attacker.CurrentGridPosition, out var source) || source.OccupyingUnit != attacker) return result;
            foreach (var cell in grid.GetCells())
                if (cell != source && (!cell.Occupied || cell.OccupyingUnit.Team != attacker.Team) &&
                    CombatMath.IsInRange(source.Position, cell.Position, config.Range) &&
                    (config.Type == AttackType.Melee ? grid.HasMeleeReach(source.Position, cell.Position, config.Range)
                        : grid.HasClearShot(source.Position, cell.Position))) result.Add(cell);
            return result;
        }
        public AttackPreview GetPreview(BattleUnit attacker, BattleUnit target, AttackType type = AttackType.Ranged,
            TargetBodyPart part = TargetBodyPart.Whole)
        {
            return TryDefaultSlot(attacker, type, out var slot) ? GetWeaponPreview(attacker, target, slot, part) :
                Invalid(new AttackPreview { Type = type }, "No usable weapon for this attack type.");
        }
        public AttackPreview GetWeaponPreview(BattleUnit attacker, BattleUnit target, WeaponSlot slot,
            TargetBodyPart part = TargetBodyPart.Whole)
            => BuildWeaponPreview(attacker, target, slot, part, false);
        private AttackPreview BuildWeaponPreview(BattleUnit attacker, BattleUnit target, WeaponSlot slot,
            TargetBodyPart part, bool reaction)
        {
            var preview = new AttackPreview { WeaponSlot = slot, ReactionMultiplier = reaction ? OverwatchSystem.ReactionHitMultiplier : 1f };
            if (reaction && (!overwatch || !overwatch.CanResolveShot(attacker, target, slot)))
                return Invalid(preview, "No authorized overwatch reaction.");
            if (!attacker || !attacker.Combat || !attacker.Combat.isActiveAndEnabled)
                return Invalid(preview, "Attacker components are unavailable.");
            if (!attacker.Combat.TryGetAttack(slot, out var config, out var reason)) return Invalid(preview, reason);
            var type = config.Type;
            preview.Type = type;
            preview.WeaponName = config.DisplayName;
            preview.TargetPart = type == AttackType.Melee ? TargetBodyPart.Torso : BodyTargetRules.Normalize(part);
            if (preview.TargetPart < TargetBodyPart.Torso || preview.TargetPart > TargetBodyPart.RightLeg)
                return Invalid(preview, "Invalid target part.");
            if (!isActiveAndEnabled || !grid || !cover || !turns)
                return Invalid(preview, "Combat is unavailable.");
            if (!attacker || !target || attacker == target || !attacker.IsAlive || !target.IsAlive ||
                !attacker.isActiveAndEnabled || !target.isActiveAndEnabled)
                return Invalid(preview, "Select a living enemy.");
            preview.PartHP = target.Health.GetCurrentHP(preview.TargetPart);
            preview.PartMaxHP = target.Health.GetMaxHP(preview.TargetPart);
            preview.TargetState = target.Health.State;
            if (attacker.Team == target.Team)
                return Invalid(preview, "Cannot attack the same team.");
            if (!grid.TryGetCell(attacker.CurrentGridPosition, out var source) || source.OccupyingUnit != attacker ||
                !grid.TryGetCell(target.CurrentGridPosition, out var destination) || destination.OccupyingUnit != target ||
                source == destination)
                return Invalid(preview, "Unit is not registered on this grid.");
            if (!attacker.Combat || !attacker.ActionPoints || !attacker.Combat.isActiveAndEnabled ||
                !attacker.ActionPoints.isActiveAndEnabled)
                return Invalid(preview, "Attacker components are unavailable.");
            if (config.MinDamage <= 0 || config.MaxDamage < config.MinDamage || config.Range <= 0)
                return Invalid(preview, "Attack damage and range must be positive.");
            long distance = CombatMath.GetGridDistance(attacker.CurrentGridPosition, target.CurrentGridPosition);
            preview.Distance = distance;
            preview.Direction = CombatMath.GetIncomingDirection(attacker.CurrentGridPosition, target.CurrentGridPosition);
            preview.Cover = destination.GetCover(preview.Direction);
            preview.CoverMultiplier = type == AttackType.Melee ? 1 : cover.GetAttackCoverMultiplier(destination, preview.Direction);
            preview.PartMultiplier = type == AttackType.Melee ? 1 : BodyTargetRules.HitMultiplier(preview.TargetPart);
            preview.HeadMultiplier = attacker.HeadHitMultiplier;
            preview.GripMultiplier = config.GripMultiplier;
            preview.DistanceMultiplier = type == AttackType.Ranged && config.Weapon ? config.Weapon.GetDistanceHitMultiplier(distance) : 1f;
            preview.HitChance = CombatMath.CalculateAttackHitChance(config.BaseHitChance,
                preview.PartMultiplier, preview.CoverMultiplier, preview.HeadMultiplier, preview.GripMultiplier,
                preview.DistanceMultiplier, preview.ReactionMultiplier);
            preview.MinDamage = config.MinDamage;
            preview.MaxDamage = config.MaxDamage;
            preview.APCost = reaction ? 0 : config.APCost;
            if (!reaction && !turns.CanAct(attacker)) return Invalid(preview, "Cannot attack outside this team's turn, while moving or after battle ends.");
            if (!CombatMath.IsInRange(attacker.CurrentGridPosition, target.CurrentGridPosition, config.Range))
                return Invalid(preview, "Target is out of range.");
            if (type == AttackType.Melee && !grid.HasMeleeReach(source.Position, destination.Position, config.Range))
                return Invalid(preview, "Target is not reachable for melee.");
            if (type == AttackType.Ranged && !grid.HasClearShot(attacker.CurrentGridPosition, target.CurrentGridPosition))
            {
                preview.BlockedByBuilding = true;
                return Invalid(preview, "Shot blocked by building.");
            }
            if (!reaction && !attacker.ActionPoints.CanSpend(config.APCost)) return Invalid(preview, "Not enough AP.");
            if (!reaction && (!turns.Visibility || !turns.Visibility.IsVisibleTo(attacker.Team, target)))
                return Invalid(preview, "Target is not detected.");
            preview.CanAttack = true;
            return preview;
        }
        private static AttackPreview Invalid(AttackPreview preview, string reason)
        { preview.CanAttack = false; preview.FailureReason = reason; return preview; }
        public AttackResult TryAttack(BattleUnit attacker, BattleUnit target, AttackType type = AttackType.Ranged,
            TargetBodyPart part = TargetBodyPart.Whole)
        {
            return TryDefaultSlot(attacker, type, out var slot) ? TryAttackWithWeapon(attacker, target, slot, part) :
                new AttackResult { FailureReason = "No usable weapon for this attack type." };
        }
        public AttackResult TryAttackWithWeapon(BattleUnit attacker, BattleUnit target, WeaponSlot slot,
            TargetBodyPart part = TargetBodyPart.Whole)
        {
            var preview = GetWeaponPreview(attacker, target, slot, part);
            if (!preview.CanAttack) return new AttackResult { FailureReason = preview.FailureReason };
            if (!attacker.ActionPoints.TrySpend(preview.APCost))
                return new AttackResult { FailureReason = "Not enough AP." };
            return ExecuteAttack(target, preview);
        }
        internal AttackResult TryOverwatchAttack(BattleUnit attacker, BattleUnit target, WeaponSlot slot)
        {
            var preview = BuildWeaponPreview(attacker, target, slot, TargetBodyPart.Torso, true);
            if (!preview.CanAttack) return new AttackResult { FailureReason = preview.FailureReason };
            if (!overwatch || !overwatch.CanResolveShot(attacker, target, slot) || !attacker.Overwatch.TryConsume())
                return new AttackResult { FailureReason = "Overwatch is no longer available." };
            return ExecuteAttack(target, preview);
        }
        private AttackResult ExecuteAttack(BattleUnit target, AttackPreview preview)
        {
            bool hit = CombatMath.IsHit(preview.HitChance, rollSource());
            var damage = hit ? target.Health.ApplyDamage(preview.TargetPart,
                Mathf.Clamp(damageRollSource(preview.MinDamage, preview.MaxDamage), preview.MinDamage, preview.MaxDamage)) :
                new PartDamageResult { PartHPAfter = preview.PartHP, StateAfter = preview.TargetState };
            return new AttackResult
            {
                WasExecuted = true, Hit = hit, HitChance = preview.HitChance,
                DamageDealt = damage.DamageDealt, TargetPart = preview.TargetPart,
                TargetPartHPAfter = damage.PartHPAfter, TargetStateAfter = damage.StateAfter,
                DestructionChecked = damage.DestructionChecked
            };
        }
    }
}
