using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public static class IndirectAttackText
    {
        public static string Result(IndirectAttackResult result)
        {
            if (!result.WasExecuted) return result.FailureReason;
            int count = result.Targets == null ? 0 : result.Targets.Count;
            var lines = new List<string> { $"포격 완료 · 확인된 피격 {count}기" };
            if (result.Targets != null)
                foreach (var unit in result.Targets)
                {
                    var parts = new List<string>();
                    foreach (var hit in unit.Parts) parts.Add($"{BodyTargetRules.Label(hit.Part)} {hit.DamageDealt}");
                    lines.Add($"{unit.Target.name}: {string.Join(", ", parts)}");
                }
            if (result.Covers != null)
                foreach (var cover in result.Covers)
                    lines.Add($"{cover.Target.name}: 엄폐물 피해 {cover.DamageDealt}" + (cover.WasDestroyed ? " / 파괴" : $" / HP {cover.HPAfter}"));
            return string.Join("\n", lines);
        }
    }
    [DisallowMultipleComponent]
    public class IndirectTargetUI : MonoBehaviour
    {
        private PlayerInputController input;
        public void Initialize(PlayerInputController value) => input = value;
        private float Scale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .25f, 1f);
        public bool IsVisible => isActiveAndEnabled && input && input.CanChooseAction && input.IsIndirectAttackSelected && input.SelectedImpactCell.HasValue;
        public Rect PanelRect => new Rect(Screen.width - 316 * Scale, (Screen.height - 360 * Scale) / 2, 300 * Scale, 360 * Scale);
        public Rect FireButtonRect => new Rect(PanelRect.x + 12 * Scale, PanelRect.y + 310 * Scale, 170 * Scale, 34 * Scale);
        public Rect CancelButtonRect => new Rect(PanelRect.x + 190 * Scale, PanelRect.y + 310 * Scale, 98 * Scale, 34 * Scale);
        public string KnownTargetsText
        {
            get
            {
                var targets = input ? input.CurrentIndirectPreview.VisibleTargets : null;
                if (targets == null || targets.Count == 0) return "확인된 대상 없음 · 좌표 포격 가능";
                var names = new List<string>(); foreach (var unit in targets) names.Add(unit.name);
                return "확인된 대상: " + string.Join(", ", names);
            }
        }
        public string FriendlyWarningText
        {
            get
            {
                if (!input || !input.SelectedUnit) return string.Empty;
                var targets = input.CurrentIndirectPreview.VisibleTargets; int count = 0;
                if (targets != null) foreach (var unit in targets) if (unit.Team == input.SelectedUnit.Team) count++;
                return count > 0 ? $"주의: 아군 {count}기 피해 예상 (자신 포함)" : string.Empty;
            }
        }
        public bool ContainsScreenPoint(Vector2 point) => IsVisible && PanelRect.Contains(new Vector2(point.x, Screen.height - point.y));
        public string TerrainDamageText
        {
            get
            {
                if (!input) return string.Empty;
                var preview = input.CurrentIndirectPreview; int facilities = 0, covers = 0;
                if (preview.VisibleCovers != null)
                    foreach (var target in preview.VisibleCovers)
                        if (target && target.GetComponent<CoverDurability>().Data) facilities++; else covers++;
                var lines = new List<string>();
                if (facilities > 0) lines.Add($"시설 {facilities}개 / 피해 {preview.MinDamage}-{preview.MaxDamage}");
                if (covers > 0) lines.Add($"엄폐물 {covers}개 / 피해 {preview.MinCoverDamage}-{preview.MaxCoverDamage}");
                return lines.Count == 0 ? "시설·엄폐물 0개" : string.Join("\n", lines);
            }
        }
        public void HandleScreenClick(Vector2 point)
        {
            if (!ContainsScreenPoint(point)) return;
            var p = new Vector2(point.x, Screen.height - point.y);
            if (CancelButtonRect.Contains(p)) input.CancelIndirectAim();
            else if (FireButtonRect.Contains(p) && input.CurrentIndirectPreview.CanAttack) input.TryFireIndirectSelected();
        }
        private void OnGUI()
        {
            if (!IsVisible) return;
            var preview = input.CurrentIndirectPreview; var panel = PanelRect;
            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * Scale), wordWrap = true };
            var title = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(18 * Scale) };
            var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * Scale) };
            int depth = GUI.depth; GUI.depth = -10; GUI.Box(panel, GUIContent.none);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 10 * Scale, 276 * Scale, 32 * Scale), "곡사 좌표 공격", title);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 45 * Scale, 276 * Scale, 50 * Scale),
                $"{preview.WeaponName}\n좌표 {input.SelectedImpactCell.Value} / 폭발 반경 {preview.Radius}칸", label);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 100 * Scale, 276 * Scale, 55 * Scale),
                $"부위당 피해 {preview.MinDamage}-{preview.MaxDamage}\n기체당 {preview.MinHitParts}-{preview.MaxHitParts}부위 · 확정 피격", label);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 158 * Scale, 276 * Scale, 50 * Scale),
                $"AP {preview.APCost} / 탄약 {preview.AmmoLoaded}/{preview.MagazineCapacity}\n{(preview.CanAttack ? KnownTargetsText : preview.FailureReason)}", label);
            ResourceTypography.Label(new Rect(panel.x + 12 * Scale, panel.y + 218 * Scale, 276 * Scale, 75 * Scale),
                $"{TerrainDamageText}\n{FriendlyWarningText}", label);
            bool enabled = GUI.enabled; GUI.enabled = enabled && preview.CanAttack; GUI.Button(FireButtonRect, "Fire / 포격 확정", button);
            GUI.enabled = enabled; GUI.Button(CancelButtonRect, "취소", button); GUI.depth = depth;
        }
    }
}

