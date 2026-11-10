using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public partial class CombatSystem
    {
        private Func<int, int, int> indirectPartCountRollSource = RollDamage;
        private Func<int, int> indirectPartIndexRollSource = count => UnityEngine.Random.Range(0, count);
        public void SetIndirectPartCountRollSource(Func<int, int, int> source) => indirectPartCountRollSource = source ?? RollDamage;
        public void SetIndirectPartIndexRollSource(Func<int, int> source) => indirectPartIndexRollSource = source ?? (count => UnityEngine.Random.Range(0, count));

        private bool TryGetIndirectConfig(BattleUnit attacker, WeaponSlot slot, out AttackStats config, out string reason)
            => TryGetExplosiveConfig(attacker, slot, true, out config, out reason);
        public IndirectAttackPreview GetIndirectPreview(BattleUnit attacker, Vector2Int center, WeaponSlot slot)
        {
            var preview = new IndirectAttackPreview { Center = center, VisibleTargets = Array.Empty<BattleUnit>(), VisibleCovers = Array.Empty<CoverObject>() };
            if (!TryGetIndirectConfig(attacker, slot, out var config, out var reason))
            { preview.FailureReason = reason; return preview; }
            var weapon = config.Weapon;
            preview.WeaponName = weapon.DisplayName; preview.Radius = weapon.BlastRadius;
            preview.MinHitParts = weapon.MinHitParts; preview.MaxHitParts = weapon.MaxHitParts;
            preview.MinDamage = config.MinDamage; preview.MaxDamage = config.MaxDamage; preview.APCost = config.APCost;
            var ammo = attacker.Ammo.GetStatus(slot);
            preview.AmmoLoaded = ammo.Loaded; preview.MagazineCapacity = ammo.Capacity; preview.SpareMagazines = ammo.SpareMagazines;
            if (!CanAimIndirectAt(attacker.CurrentGridPosition, center, weapon, out reason))
            { preview.FailureReason = reason; return preview; }
            var known = new List<BattleUnit>();
            foreach (var unit in CollectIndirectTargets(center, weapon.BlastRadius))
                if (KnownIndirectTarget(attacker, unit)) known.Add(unit);
            preview.VisibleTargets = known.AsReadOnly();
            preview.VisibleCovers = CollectBlastCovers(center, weapon).AsReadOnly();
            preview.CoverDamageMultiplier = weapon.CoverDamageMultiplier;
            preview.MinCoverDamage = CombatMath.CalculateCoverDamage(config.MinDamage, weapon.CoverDamageMultiplier);
            preview.MaxCoverDamage = CombatMath.CalculateCoverDamage(config.MaxDamage, weapon.CoverDamageMultiplier);
            preview.CanAttack = true; return preview;
        }
        // Geometry only, also used for hypothetical AI firing positions before movement.
        internal bool CanAimIndirectAt(Vector2Int from, Vector2Int center, WeaponData weapon, out string reason)
        {
            reason = null;
            if (!grid.TryGetCell(center, out _) || CombatMath.GetGridDistance(from, center) > weapon.Range)
                reason = "공격 사거리 안의 맵 좌표를 선택하세요.";
            else if (!weapon.HasTrait(WeaponTrait.IndirectOverBuildings) && !grid.HasClearIndirectLaunchPath(from, center))
                reason = "큰 건물이 곡사 발사 경로를 막고 있습니다.";
            return reason == null;
        }
        public List<GridCell> GetIndirectBlastCells(Vector2Int center, int radius)
        {
            var cells = new List<GridCell>();
            if (!grid || radius < 1 || !grid.TryGetCell(center, out _)) return cells;
            foreach (var cell in grid.GetCells())
            {
                if (CombatMath.GetGridDistance(center, cell.Position) <= radius && grid.HasClearBlast(center, cell.Position)) cells.Add(cell);
            }
            return cells;
        }
        public List<GridCell> GetIndirectPreviewCells(BattleUnit attacker, Vector2Int center, WeaponSlot slot)
        {
            var preview = GetIndirectPreview(attacker, center, slot);
            if (!preview.CanAttack) return new List<GridCell>();
            var cells = GetIndirectBlastCells(center, preview.Radius);
            var included = new HashSet<GridCell>(cells);
            foreach (var target in preview.VisibleCovers)
                foreach (var cell in grid.GetObstacleCells(target.Collider))
                    if (CombatMath.GetGridDistance(center, cell.Position) <= preview.Radius &&
                        grid.HasClearBlastToObstacle(center, cell.Position, target.Collider) && included.Add(cell))
                        cells.Add(cell);
            return cells;
        }
        private List<BattleUnit> CollectIndirectTargets(Vector2Int center, int radius)
        {
            var targets = new List<BattleUnit>();
            foreach (var cell in GetIndirectBlastCells(center, radius))
            {
                var unit = cell.OccupyingUnit;
                if (unit && unit.isActiveAndEnabled && unit.IsAlive && unit.Health.isActiveAndEnabled && unit.CurrentGridPosition == cell.Position)
                    targets.Add(unit);
            }
            return targets;
        }
        private bool KnownIndirectTarget(BattleUnit attacker, BattleUnit target) => target.Team == attacker.Team ||
            (turns.Visibility && turns.Visibility.IsVisibleTo(attacker.Team, target));

        public IndirectAttackResult TryIndirectAttack(BattleUnit attacker, Vector2Int center, WeaponSlot slot)
        {
            var preview = GetIndirectPreview(attacker, center, slot);
            var result = new IndirectAttackResult { Center = center, Targets = Array.Empty<IndirectUnitDamage>(),
                Covers = Array.Empty<CoverAttackResult>(), FailureReason = preview.FailureReason };
            if (!preview.CanAttack) return result;
            // No callbacks or randomness between validation and the one-shot payments.
            if (!attacker.ActionPoints.TrySpend(preview.APCost) || !attacker.Ammo.TryConsumeShot(slot))
            { result.FailureReason = "발사 자원을 사용할 수 없습니다."; return result; }
            var visibleResults = new List<IndirectUnitDamage>();
            var coverResults = new List<CoverAttackResult>();
            turns.BeginDamageResolution();
            try
            {
                // Freeze membership/visibility before RNG or any damage opens a wall.
                var targets = new List<(BattleUnit unit, bool known, bool show, Vector3 position)>();
                foreach (var unit in CollectIndirectTargets(center, preview.Radius))
                    targets.Add((unit, KnownIndirectTarget(attacker, unit), CanShowFeedback(unit), FeedbackPosition(unit.gameObject)));
                var coverTargets = new List<CoverObject>(preview.VisibleCovers);
                var payloads = new List<(BattleUnit unit, bool known, bool show, Vector3 position, List<PartDamage> hits)>();
                var coverPayloads = new List<(CoverObject target, CoverDurability health, Vector3 position, int damage)>();
                var feedback = new List<CombatFeedbackEntry>();
                foreach (var info in targets)
                {
                    var unit = info.unit; var candidates = new List<TargetBodyPart>();
                    for (int i = (int)TargetBodyPart.Torso; i <= (int)TargetBodyPart.RightLeg; i++)
                    {
                        var part = (TargetBodyPart)i;
                        if (part != TargetBodyPart.Cockpit && unit.Health.GetCurrentHP(part) > 0) candidates.Add(part);
                    }
                    int count = Math.Min(candidates.Count, Mathf.Clamp(indirectPartCountRollSource(preview.MinHitParts, preview.MaxHitParts), preview.MinHitParts, preview.MaxHitParts));
                    var hits = new List<PartDamage>();
                    for (int i = 0; i < count; i++)
                    {
                        int index = Mathf.Clamp(indirectPartIndexRollSource(candidates.Count), 0, candidates.Count - 1);
                        var part = candidates[index]; candidates.RemoveAt(index);
                        hits.Add(new PartDamage(part, Mathf.Clamp(damageRollSource(preview.MinDamage, preview.MaxDamage), preview.MinDamage, preview.MaxDamage)));
                    }
                    payloads.Add((unit, info.known, info.show, info.position, hits));
                }
                foreach (var target in coverTargets)
                {
                    int raw = Mathf.Clamp(damageRollSource(preview.MinDamage, preview.MaxDamage), preview.MinDamage, preview.MaxDamage);
                    var health = target.GetComponent<CoverDurability>();
                    coverPayloads.Add((target, health, FeedbackPosition(target.gameObject), CombatMath.CalculateCoverDamage(raw, health.Data ? 1f : preview.CoverDamageMultiplier)));
                }
                foreach (var payload in payloads)
                {
                    var damage = payload.unit.Health.ApplyDamageBatch(payload.hits);
                    if (payload.known) visibleResults.Add(new IndirectUnitDamage { Target = payload.unit, Parts = Array.AsReadOnly(damage) });
                    if (payload.show) feedback.Add(new CombatFeedbackEntry(payload.position, CombatFeedbackText.ForParts(damage)));
                }
                foreach (var payload in coverPayloads)
                {
                    int damage = payload.health.ApplyDamage(payload.damage);
                    coverResults.Add(new CoverAttackResult { WasExecuted = true, Target = payload.target, DamageDealt = damage,
                        HPAfter = payload.health.CurrentHP, WasDestroyed = payload.health.IsDestroyed });
                    feedback.Add(new CombatFeedbackEntry(payload.position, CombatFeedbackText.ForCover(coverResults[coverResults.Count - 1])));
                }
                PublishFeedback(feedback);
            }
            finally { turns.EndDamageResolution(); }
            result.WasExecuted = true; result.FailureReason = null; result.Targets = visibleResults.AsReadOnly();
            result.Covers = coverResults.AsReadOnly(); return result;
        }
    }
}
