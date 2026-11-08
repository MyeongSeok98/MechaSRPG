using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public partial class CombatSystem
    {
        public event Action TerrainChanged;
        private void HandleTerrainChanged()
        {
            if (turns && turns.Visibility) turns.Visibility.Refresh();
            TerrainChanged?.Invoke();
        }
        private void OnDestroy() { if (cover) cover.Changed -= HandleTerrainChanged; }
        private bool TryGetExplosiveConfig(BattleUnit attacker, WeaponSlot slot, bool indirect, out AttackStats config, out string reason)
        {
            config = default; reason = null;
            if (!isActiveAndEnabled || !grid || !turns || !turns.isActiveAndEnabled || !attacker ||
                !attacker.isActiveAndEnabled || !attacker.IsAlive || !attacker.Health.isActiveAndEnabled ||
                !attacker.Combat || !attacker.Combat.isActiveAndEnabled || !attacker.ActionPoints || !attacker.ActionPoints.isActiveAndEnabled ||
                !attacker.CanTakeAction || !turns.CanAct(attacker) ||
                !grid.TryGetCell(attacker.CurrentGridPosition, out var source) || source.OccupyingUnit != attacker)
                reason = "현재 폭발 공격을 할 수 없습니다.";
            else if (!attacker.Combat.TryGetAttack(slot, out config, out reason)) { }
            else if (config.Type != AttackType.Ranged || config.Weapon.RangedCategory != RangedWeaponCategory.Explosive || config.Weapon.IsIndirect != indirect)
                reason = indirect ? "곡사 폭발무기를 선택하세요." : "엄폐물을 공격할 직사 폭발무기를 선택하세요.";
            else if (!attacker.ActionPoints.CanSpend(config.APCost)) reason = "발사 AP가 부족합니다.";
            else if (!attacker.Ammo || !attacker.Ammo.isActiveAndEnabled || !attacker.Ammo.CanFire(slot)) reason = "발사할 탄약이 없습니다.";
            return reason == null;
        }
        private bool CanDamageCover(CoverObject target, out CoverDurability health)
        {
            health = null;
            return cover && cover.Grid == grid && target && target.IsProvidingCover &&
                target.TryGetComponent(out health) && health.isActiveAndEnabled && !health.IsDestroyed;
        }
        public CoverAttackPreview GetCoverAttackPreview(BattleUnit attacker, CoverObject target, WeaponSlot slot)
        {
            var preview = new CoverAttackPreview { Target = target };
            if (!TryGetExplosiveConfig(attacker, slot, false, out var config, out var reason))
            { preview.FailureReason = reason; return preview; }
            if (!CanDamageCover(target, out var health) || !ContainsCover(target))
            { preview.FailureReason = "이 전장에 등록된 파괴 가능한 엄폐물을 선택하세요."; return preview; }
            preview.WeaponName = config.DisplayName; preview.CurrentHP = health.CurrentHP; preview.MaxHP = health.MaxHP;
            preview.MinDamage = CombatMath.CalculateCoverDamage(config.MinDamage, config.Weapon.CoverDamageMultiplier);
            preview.MaxDamage = CombatMath.CalculateCoverDamage(config.MaxDamage, config.Weapon.CoverDamageMultiplier);
            preview.APCost = config.APCost;
            var ammo = attacker.Ammo.GetStatus(slot); preview.AmmoLoaded = ammo.Loaded; preview.MagazineCapacity = ammo.Capacity;
            if (preview.MaxDamage <= 0) { preview.FailureReason = "이 무기는 엄폐물 피해가 없습니다."; return preview; }
            long nearest = long.MaxValue;
            foreach (var cell in grid.GetObstacleCells(target.Collider))
            {
                long distance = CombatMath.GetGridDistance(attacker.CurrentGridPosition, cell.Position);
                if (distance > config.Range || distance >= nearest ||
                    !grid.HasClearShotToObstacle(attacker.CurrentGridPosition, cell.Position, target.Collider)) continue;
                nearest = distance; preview.ImpactCell = cell.Position;
            }
            if (nearest == long.MaxValue) { preview.FailureReason = "사거리 밖이거나 건물이 엄폐물 사선을 막고 있습니다."; return preview; }
            preview.CanAttack = true; return preview;
        }
        private bool ContainsCover(CoverObject value)
        { foreach (var item in cover.RegisteredCovers) if (item == value) return true; return false; }
        internal bool IsCoverTargetAvailable(CoverObject value) => CanDamageCover(value, out _) && ContainsCover(value);
        public CoverAttackResult TryAttackCover(BattleUnit attacker, CoverObject target, WeaponSlot slot)
        {
            var preview = GetCoverAttackPreview(attacker, target, slot);
            var result = new CoverAttackResult { Target = target, FailureReason = preview.FailureReason };
            if (!preview.CanAttack || !attacker.Combat.TryGetAttack(slot, out var config, out _)) return result;
            if (!attacker.ActionPoints.TrySpend(config.APCost) || !attacker.Ammo.TryConsumeShot(slot))
            { result.FailureReason = "발사 자원을 사용할 수 없습니다."; return result; }
            turns.BeginDamageResolution();
            try
            {
                var position = FeedbackPosition(target.gameObject);
                int raw = Mathf.Clamp(damageRollSource(config.MinDamage, config.MaxDamage), config.MinDamage, config.MaxDamage);
                var health = target.GetComponent<CoverDurability>();
                result.DamageDealt = health.ApplyDamage(CombatMath.CalculateCoverDamage(raw, config.Weapon.CoverDamageMultiplier));
                result.HPAfter = health.CurrentHP; result.WasDestroyed = health.IsDestroyed;
                PublishFeedback(new List<CombatFeedbackEntry> { new CombatFeedbackEntry(position, CombatFeedbackText.ForCover(result)) });
            }
            finally { turns.EndDamageResolution(); }
            result.WasExecuted = true; result.FailureReason = null; return result;
        }
        private List<CoverObject> CollectBlastCovers(Vector2Int center, WeaponData weapon)
        {
            var result = new List<CoverObject>();
            if (!cover || cover.Grid != grid || weapon.CoverDamageMultiplier <= 0) return result;
            foreach (var target in cover.RegisteredCovers)
            {
                if (!CanDamageCover(target, out _)) continue;
                foreach (var cell in grid.GetObstacleCells(target.Collider))
                    if (CombatMath.GetGridDistance(center, cell.Position) <= weapon.BlastRadius &&
                        grid.HasClearBlastToObstacle(center, cell.Position, target.Collider))
                    { result.Add(target); break; }
            }
            return result;
        }
        private void AddCoverRangeCells(BattleUnit attacker, WeaponSlot slot, AttackStats config, List<GridCell> result)
        {
            if (config.Type != AttackType.Ranged || config.Weapon.IsIndirect ||
                config.Weapon.RangedCategory != RangedWeaponCategory.Explosive || config.Weapon.CoverDamageMultiplier <= 0 ||
                !cover || cover.Grid != grid) return;
            var existing = new HashSet<GridCell>(result);
            foreach (var target in cover.RegisteredCovers)
            {
                if (!GetCoverAttackPreview(attacker, target, slot).CanAttack) continue;
                foreach (var cell in grid.GetObstacleCells(target.Collider))
                    if (CombatMath.GetGridDistance(attacker.CurrentGridPosition, cell.Position) <= config.Range &&
                        grid.HasClearShotToObstacle(attacker.CurrentGridPosition, cell.Position, target.Collider) && existing.Add(cell))
                        result.Add(cell);
            }
        }
    }
}
