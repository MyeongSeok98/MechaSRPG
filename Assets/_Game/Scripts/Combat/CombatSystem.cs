using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public partial class CombatSystem : MonoBehaviour
    {
        private GridManager grid;
        private CoverSystem cover;
        private TurnManager turns;
        private OverwatchSystem overwatch;
        public void ConfigureOverwatch(OverwatchSystem system) => overwatch = system;
        private Func<int> rollSource = () => UnityEngine.Random.Range(0, 100);
        private Func<int, int, int> damageRollSource = RollDamage;
        private Func<int, int> additionalPartIndexRollSource = maximum => UnityEngine.Random.Range(0, maximum);
        // Optional legacy test hook. Its return is snapped to the authored discrete levels.
        private Func<float, float, float> additionalDamageRatioRollSource;
        private Func<int, int> additionalDamageStepRollSource = count => UnityEngine.Random.Range(0, count);
        public void SetAdditionalPartIndexRollSource(Func<int, int> source) =>
            additionalPartIndexRollSource = source ?? (maximum => UnityEngine.Random.Range(0, maximum));
        public void SetAdditionalDamageRatioRollSource(Func<float, float, float> source) =>
            additionalDamageRatioRollSource = source;
        public void SetAdditionalDamageStepRollSource(Func<int, int> source) =>
            additionalDamageStepRollSource = source ?? (count => UnityEngine.Random.Range(0, count));
        public void SetDamageRollSource(Func<int, int, int> source) => damageRollSource = source ?? RollDamage;
        private static float RollCritical() => UnityEngine.Random.Range(0, 10000) / 100f;
        private Func<float> criticalRollSource = RollCritical;
        public void SetCriticalRollSource(Func<float> source) => criticalRollSource = source ?? RollCritical;
        // Both bounds are inclusive without overflowing when maximum == int.MaxValue.
        private static int RollDamage(int minimum, int maximum) => UnityEngine.Random.Range(minimum - 1, maximum) + 1;
        public void Initialize(GridManager grid, CoverSystem cover, TurnManager turns)
        {
            if (this.cover) this.cover.Changed -= HandleTerrainChanged;
            this.grid = grid; this.cover = cover; this.turns = turns;
            if (cover) cover.Changed += HandleTerrainChanged;
        }
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
                (config.Weapon.UsesAmmo && (!attacker.Ammo || !attacker.Ammo.CanFire(slot))) ||
                !attacker.ActionPoints.isActiveAndEnabled || !attacker.ActionPoints.CanSpend(config.APCost) ||
                !grid.TryGetCell(attacker.CurrentGridPosition, out var source) || source.OccupyingUnit != attacker) return result;
            foreach (var cell in grid.GetCells())
                if (config.Weapon.IsIndirect ? CanAimIndirectAt(source.Position, cell.Position, config.Weapon, out _) :
                    cell != source && (!cell.Occupied || cell.OccupyingUnit.Team != attacker.Team) &&
                    CombatMath.IsInRange(source.Position, cell.Position, config.Range) &&
                    (config.Type == AttackType.Melee ? grid.HasMeleeReach(source.Position, cell.Position, config.Range)
                        : grid.HasClearShot(source.Position, cell.Position))) result.Add(cell);
            AddCoverRangeCells(attacker, slot, config, result);
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
            var preview = new AttackPreview { WeaponSlot = slot, ArmorMultiplier = 1f,
                ReactionMultiplier = reaction ? OverwatchSystem.ReactionHitMultiplier : 1f };
            if (reaction && (!overwatch || !overwatch.CanPreviewDecision(attacker, target, slot)))
                return Invalid(preview, "No authorized overwatch reaction.");
            if (!attacker || !attacker.Combat || !attacker.Combat.isActiveAndEnabled)
                return Invalid(preview, "Attacker components are unavailable.");
            if (!attacker.Combat.TryGetAttack(slot, out var config, out var reason)) return Invalid(preview, reason);
            var type = config.Type;
            preview.Type = type;
            preview.WeaponName = config.DisplayName;
            if (config.Weapon.IsIndirect) return Invalid(preview, "곡사무기는 맵 좌표를 선택해 발사하세요.");
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
            preview.MechaMultiplier = CombatMath.CalculateMechaHitMultiplier(attacker.MechaAccuracy, target.MechaEvasion);
            preview.PilotHitMultiplier = CombatMath.CalculatePilotHitMultiplier(type,
                type == AttackType.Melee ? attacker.PilotMelee : attacker.PilotShooting, target.PilotEvasion);
            preview.PilotDamageMultiplier = CombatMath.CalculatePilotDamageMultiplier(type, attacker.PilotMelee);
            preview.HitChance = CombatMath.CalculateAttackHitChance(config.BaseHitChance,
                preview.PartMultiplier, preview.CoverMultiplier, preview.HeadMultiplier, preview.GripMultiplier,
                preview.DistanceMultiplier, preview.ReactionMultiplier, preview.MechaMultiplier, preview.PilotHitMultiplier);
            preview.MinDamage = config.MinDamage;
            preview.MaxDamage = config.MaxDamage;
            if (config.Weapon.HasAdditionalPartDamage)
            {
                preview.AdditionalHitPartCount = config.Weapon.AdditionalHitPartCount;
                preview.AdditionalDamageMinRatio = config.Weapon.AdditionalDamageMinRatio;
                preview.AdditionalDamageMaxRatio = config.Weapon.AdditionalDamageMaxRatio;
                preview.AdditionalDamageStepPercent = config.Weapon.AdditionalDamageStepPercent;
            }
            preview.CriticalChance = CombatMath.CalculateCriticalChance(attacker.PilotTechnique, target.PilotTechnique);
            preview.CriticalDamageMultiplier = CombatMath.BaseCriticalDamageMultiplier;
            preview.TargetArmorType = target.ArmorType;
            preview.ArmorMultiplier = target.GetDamageTakenMultiplier(type, config.Weapon.RangedCategory);
            preview.APCost = reaction ? 0 : config.APCost;
            preview.UsesAmmo = config.Weapon.UsesAmmo;
            preview.UsesEnergyPack = preview.UsesAmmo && config.Weapon.RangedCategory == RangedWeaponCategory.Beam;
            if (preview.UsesAmmo && attacker.Ammo)
            {
                var ammo = attacker.Ammo.GetStatus(slot);
                preview.AmmoLoaded = ammo.Loaded; preview.MagazineCapacity = ammo.Capacity; preview.SpareMagazines = ammo.SpareMagazines;
            }
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
            if (preview.UsesAmmo && (!attacker.Ammo || !attacker.Ammo.CanFire(slot)))
                return Invalid(preview, "Out of ammo. Reload this weapon.");
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
            // No callbacks occur between the preview, AP payment and ammo payment.
            if (preview.UsesAmmo && !attacker.Ammo.TryConsumeShot(slot))
                return new AttackResult { FailureReason = "Weapon ammo is unavailable." };
            return ExecuteAttack(target, preview);
        }
        public bool CanReloadWithWeapon(BattleUnit unit, WeaponSlot slot, out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled || !grid || !turns || !unit || !unit.isActiveAndEnabled || !unit.IsAlive ||
                !unit.Health.isActiveAndEnabled || !unit.Combat || !unit.Combat.isActiveAndEnabled ||
                !unit.ActionPoints || !unit.ActionPoints.isActiveAndEnabled || !unit.Ammo || !unit.Ammo.isActiveAndEnabled ||
                !turns.CanAct(unit) || !grid.TryGetCell(unit.CurrentGridPosition, out var cell) || cell.OccupyingUnit != unit)
                reason = "Cannot reload while this unit cannot act.";
            else if (!unit.Combat.TryGetAttack(slot, out _, out reason)) { }
            else if (!unit.Ammo.CanReload(slot)) reason = "Reload requires a partially empty magazine and a spare.";
            else if (!unit.ActionPoints.CanSpend(unit.Ammo.GetStatus(slot).ReloadAPCost)) reason = "Not enough AP to reload.";
            return reason == null;
        }
        public bool TryReloadWithWeapon(BattleUnit unit, WeaponSlot slot)
        {
            if (!CanReloadWithWeapon(unit, slot, out _)) return false;
            int cost = unit.Ammo.GetStatus(slot).ReloadAPCost;
            if (!unit.ActionPoints.TrySpend(cost)) return false;
            return unit.Ammo.TryReload(slot);
        }
        internal AttackPreview GetOverwatchPreview(BattleUnit attacker, BattleUnit target, WeaponSlot slot, TargetBodyPart part)
            => BuildWeaponPreview(attacker, target, slot, part, true);
        internal AttackResult TryOverwatchAttack(BattleUnit attacker, BattleUnit target, WeaponSlot slot, TargetBodyPart part)
        {
            if (!overwatch || !overwatch.CanResolveShot(attacker, target, slot, part))
                return new AttackResult { FailureReason = "No authorized overwatch fire decision." };
            var preview = GetOverwatchPreview(attacker, target, slot, part);
            if (!preview.CanAttack) return new AttackResult { FailureReason = preview.FailureReason };
            if (!overwatch.CanResolveShot(attacker, target, slot, part) || !attacker.Overwatch.TryConsumeTarget(target))
                return new AttackResult { FailureReason = "Overwatch is no longer available." };
            if (preview.UsesAmmo && !attacker.Ammo.TryConsumeShot(slot))
                return new AttackResult { FailureReason = "Weapon ammo is unavailable." };
            return ExecuteAttack(target, preview);
        }
        private AttackResult ResolveAttack(BattleUnit target, AttackPreview preview)
        {
            bool hit = CombatMath.IsHit(preview.HitChance, rollSource());
            bool critical = hit && preview.CriticalChance > 0f &&
                CombatMath.IsCritical(preview.CriticalChance, criticalRollSource());
            var damage = new PartDamageResult { PartHPAfter = preview.PartHP, StateAfter = preview.TargetState };
            PartDamageResult[] additional = null;
            if (hit)
            {
                int directDamage = CombatMath.CalculateModifiedAttackDamage(
                    Mathf.Clamp(damageRollSource(preview.MinDamage, preview.MaxDamage), preview.MinDamage, preview.MaxDamage),
                    critical, preview.ArmorMultiplier, preview.PilotDamageMultiplier);
                if (preview.AdditionalHitPartCount > 0)
                {
                    var hits = new List<PartDamage> { new PartDamage(preview.TargetPart, directDamage) };
                    var candidates = new List<TargetBodyPart>();
                    for (int i = (int)TargetBodyPart.Torso; i <= (int)TargetBodyPart.RightLeg; i++)
                    {
                        var part = (TargetBodyPart)i;
                        if (part != preview.TargetPart && part != TargetBodyPart.Cockpit && target.Health.GetCurrentHP(part) > 0)
                            candidates.Add(part);
                    }
                    int count = Math.Min(preview.AdditionalHitPartCount, candidates.Count);
                    for (int i = 0; i < count; i++)
                    {
                        int index = Mathf.Clamp(additionalPartIndexRollSource(candidates.Count), 0, candidates.Count - 1);
                        var part = candidates[index]; candidates.RemoveAt(index);
                        int percent = RollAdditionalDamagePercent(preview);
                        int extraDamage = (int)Math.Min(int.MaxValue, Math.Floor(directDamage * (decimal)percent / 100m + .5m));
                        hits.Add(new PartDamage(part, extraDamage));
                    }
                    var results = target.Health.ApplyDamageBatch(hits);
                    damage = results[0]; additional = new PartDamageResult[results.Length - 1];
                    Array.Copy(results, 1, additional, 0, additional.Length);
                }
                else damage = target.Health.ApplyDamage(preview.TargetPart, directDamage);
            }
            return new AttackResult
            {
                WasExecuted = true, Hit = hit, HitChance = preview.HitChance,
                IsCritical = critical, CriticalChance = preview.CriticalChance,
                DamageDealt = damage.DamageDealt, TargetPart = preview.TargetPart,
                TargetPartHPAfter = damage.PartHPAfter, TargetStateAfter = damage.StateAfter,
                DestructionChecked = damage.DestructionChecked, AdditionalPartDamage = additional
            };
        }
        private int RollAdditionalDamagePercent(AttackPreview preview)
        {
            int step = preview.AdditionalDamageStepPercent;
            int first = Mathf.RoundToInt(preview.AdditionalDamageMinRatio * 100f / step);
            int last = Mathf.RoundToInt(preview.AdditionalDamageMaxRatio * 100f / step);
            int levels = last - first + 1;
            int index;
            if (additionalDamageRatioRollSource != null)
            {
                float ratio = additionalDamageRatioRollSource(preview.AdditionalDamageMinRatio, preview.AdditionalDamageMaxRatio);
                ratio = float.IsNaN(ratio) ? preview.AdditionalDamageMinRatio :
                    Mathf.Clamp(ratio, preview.AdditionalDamageMinRatio, preview.AdditionalDamageMaxRatio);
                index = Mathf.RoundToInt(ratio * 100f / step) - first;
            }
            else index = additionalDamageStepRollSource(levels);
            // Each integer index is equally likely in normal play, including both ends.
            return (first + Mathf.Clamp(index, 0, levels - 1)) * step;
        }
    }
}
