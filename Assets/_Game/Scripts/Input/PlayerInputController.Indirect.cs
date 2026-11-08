using System;
using UnityEngine;

namespace MechaXCOM
{
    public partial class PlayerInputController
    {
        private IndirectTargetUI indirectUI;
        public Vector2Int? SelectedImpactCell { get; private set; }
        public IndirectAttackResult LastIndirectAttackResult { get; private set; }
        public bool IsIndirectAttackSelected => ActionMode == UnitActionMode.Attack && SelectedWeaponSlot.HasValue && selected &&
            selected.Combat && selected.Combat.TryGetAttack(SelectedWeaponSlot.Value, out var attack, out _) && attack.Weapon.IsIndirect;
        public IndirectAttackPreview CurrentIndirectPreview => IsIndirectAttackSelected && SelectedImpactCell.HasValue && combat
            ? combat.GetIndirectPreview(selected, SelectedImpactCell.Value, SelectedWeaponSlot.Value) : default;
        private void ClearIndirectAim()
        { SelectedImpactCell = null; if (visualizer) visualizer.ClearBlastCells(); }
        public bool TrySelectImpactCell(Vector2Int center)
        {
            if (!CanChooseAction || !IsIndirectAttackSelected || !combat) return false;
            var preview = combat.GetIndirectPreview(selected, center, SelectedWeaponSlot.Value);
            if (!preview.CanAttack)
            { ClearIndirectAim(); InputMessage = preview.FailureReason; RefreshRange(); return false; }
            SelectedImpactCell = center; target = null; InputMessage = null; ResetClickSequence(); RefreshRange(); return true;
        }
        public IndirectAttackResult TryFireIndirectSelected()
        {
            if (!CanChooseAction || !IsIndirectAttackSelected || !SelectedImpactCell.HasValue || !combat)
                return new IndirectAttackResult { FailureReason = "곡사 공격 좌표를 선택하세요.", Targets = Array.Empty<IndirectUnitDamage>() };
            var result = combat.TryIndirectAttack(selected, SelectedImpactCell.Value, SelectedWeaponSlot.Value);
            if (result.WasExecuted)
            {
                LastIndirectAttackResult = result; LastAttackResult = default;
                InputMessage = IndirectAttackText.Result(result); ActionMode = UnitActionMode.Menu;
                target = null; ClearIndirectAim(); ResetClickSequence(); RefreshRange();
            }
            else InputMessage = result.FailureReason;
            return result;
        }
        public void CancelIndirectAim()
        { ClearIndirectAim(); target = null; ActionMode = UnitActionMode.Menu; InputMessage = null; RefreshRange(); }
    }
}
