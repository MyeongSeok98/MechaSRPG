using UnityEngine;

namespace MechaXCOM
{
    public partial class PlayerInputController
    {
        private CoverObject coverTarget;
        private CoverTargetUI coverUI;
        public CoverAttackResult LastCoverAttackResult { get; private set; }
        public bool IsCoverAttackSelected => ActionMode == UnitActionMode.Attack && SelectedWeaponSlot.HasValue && selected &&
            selected.Combat && selected.Combat.TryGetAttack(SelectedWeaponSlot.Value, out var attack, out _) &&
            attack.Type == AttackType.Ranged && attack.Weapon.RangedCategory == RangedWeaponCategory.Explosive && !attack.Weapon.IsIndirect;
        public CoverObject TargetCover => IsCoverAttackSelected && coverTarget && combat &&
            combat.IsCoverTargetAvailable(coverTarget) ? coverTarget : null;
        public CoverAttackPreview CurrentCoverPreview => TargetCover && combat
            ? combat.GetCoverAttackPreview(selected, TargetCover, SelectedWeaponSlot.Value) : default;
        private void ClearCoverAim() => coverTarget = null;
        private void HandleBattleTerrainChanged()
        {
            if (!ReferenceEquals(coverTarget, null) && !TargetCover) ClearCoverAim();
            if (selected && visualizer) RefreshRange();
        }
        public bool TrySelectCover(CoverObject value)
        {
            if (!CanChooseAction || !IsCoverAttackSelected || !combat) return false;
            target = null;
            var preview = combat.GetCoverAttackPreview(selected, value, SelectedWeaponSlot.Value);
            if (!preview.CanAttack)
            { ClearCoverAim(); InputMessage = preview.FailureReason; RefreshRange(); return false; }
            coverTarget = value; SelectedBodyPart = TargetBodyPart.Whole;
            InputMessage = null; ResetClickSequence(); RefreshRange(); return true;
        }
        public CoverAttackResult TryFireCoverSelected()
        {
            if (!CanChooseAction || !IsCoverAttackSelected || !TargetCover || !combat)
                return new CoverAttackResult { FailureReason = "공격할 엄폐물을 선택하세요." };
            var result = combat.TryAttackCover(selected, TargetCover, SelectedWeaponSlot.Value);
            if (result.WasExecuted)
            {
                LastCoverAttackResult = result; LastAttackResult = default; LastIndirectAttackResult = default;
                InputMessage = $"{result.Target.name}: 피해 {result.DamageDealt} / " +
                    (result.WasDestroyed ? "엄폐물 파괴" : $"남은 HP {result.HPAfter}");
                ActionMode = UnitActionMode.Menu; target = null; ClearCoverAim(); ResetClickSequence(); RefreshRange();
            }
            else InputMessage = result.FailureReason;
            return result;
        }
        public void CancelCoverAim()
        { ClearCoverAim(); target = null; ActionMode = UnitActionMode.Menu; InputMessage = null; RefreshRange(); }
    }
}
