using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MechaXCOM
{
    public class SelectedUnitInfoPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Text unitName;
        [SerializeField] private Text pilotName;
        [SerializeField] private Image portrait;
        [SerializeField] private Text apText;
        [SerializeField] private Text availability;
        [SerializeField] private Text state;
        [SerializeField] private RectTransform statusContent;
        [SerializeField] private ScrollRect statusScroll;
        [SerializeField] private LayoutElement statusHeight;
        [SerializeField] private UnitStatusRowUI rowPrefab;
        [SerializeField] private float maximumStatusHeight = 234;
        private BattleDebugUI source;
        private TurnManager turns;
        private Font bodyFont;
        private readonly List<UnitStatusPart> parts = new List<UnitStatusPart>();
        private readonly List<UnitStatusRowUI> rows = new List<UnitStatusRowUI>();
        private readonly Vector3[] corners = new Vector3[4];
        public Rect GUIRect
        {
            get
            {
                panelRoot.GetWorldCorners(corners);
                return new Rect(corners[0].x, Screen.height - corners[1].y, corners[2].x - corners[0].x, corners[1].y - corners[0].y);
            }
        }
        public BattleUnit DisplayedUnit { get; private set; }
        public bool IsVisible => isActiveAndEnabled && panelRoot && panelRoot.gameObject.activeSelf;
        public string APText => apText.text;
        public string AvailabilityText => availability.text;
        public string StateText => state.text;
        public void Initialize(BattleDebugUI debug, TurnManager manager)
        { source = debug; turns = manager; Hide(); }
        private void Awake()
        {
            // Dynamic OS font retains Hangul names while numeric resources use the authored Oxanium asset.
            bodyFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            if (bodyFont)
                foreach (var text in GetComponentsInChildren<Text>(true))
                    if (!text.font || text.font.name != "Oxanium-Regular") text.font = bodyFont;
            Hide();
        }
        private void LateUpdate()
        {
            if (statusScroll) statusScroll.enabled = !(turns && turns.IsPresentingTurn);
            if (!source) return; // Allows the view to be previewed with an explicitly supplied health source.
            if (!source.isActiveAndEnabled || source.DetailedDebugVisible || !source.DisplayedUnit) { Hide(); return; }
            ShowUnit(source.DisplayedUnit, source.DisplayedUnit.Health, source.PilotPortrait);
        }
        public void ShowUnit(BattleUnit unit, IUnitStatusSource statusSource, Sprite defaultPortrait = null)
        {
            if (!unit || statusSource == null) { Hide(); return; }
            bool changed = DisplayedUnit != unit;
            DisplayedUnit = unit; panelRoot.gameObject.SetActive(true);
            unitName.text = UnitInspectionText.Name(unit);
            pilotName.text = unit.PilotData ? unit.PilotData.DisplayName : "Pilot unassigned";
            portrait.sprite = defaultPortrait ? defaultPortrait : unit.PilotData ? unit.PilotData.DefaultPortrait : null;
            portrait.enabled = portrait.sprite;
            apText.text = $"AP {unit.ActionPoints.CurrentPoints}/{unit.ActionPoints.MaxPoints}";
            bool active = unit.Health && unit.Health.CanFight;
            availability.text = active ? "ACTIVE" : "INACTIVE";
            availability.color = active ? new Color32(136, 216, 153, 255) : new Color32(233, 170, 81, 255);
            state.text = StateOf(unit);
            statusSource.GetStatusParts(parts);
            int previousCount = 0; foreach (var row in rows) if (row.gameObject.activeSelf) previousCount++;
            while (rows.Count < parts.Count)
            {
                var row = Instantiate(rowPrefab, statusContent); rows.Add(row);
                if (bodyFont) row.GetComponentInChildren<Text>().font = bodyFont;
            }
            for (int index = 0; index < rows.Count; index++)
            {
                rows[index].gameObject.SetActive(index < parts.Count);
                if (index < parts.Count) rows[index].Show(parts[index]);
            }
            statusHeight.preferredHeight = Mathf.Min(maximumStatusHeight, parts.Count * 30 + Mathf.Max(0, parts.Count - 1) * 4);
            if (changed || previousCount != parts.Count)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
                statusScroll.verticalNormalizedPosition = 1;
            }
        }
        private string StateOf(BattleUnit unit)
        {
            if (unit.Health.State != MechState.Active) return unit.Health.State.ToString().ToUpperInvariant();
            if (unit.Movement && unit.Movement.IsMoving) return "MOVING";
            if (unit.HasPendingFacing) return "FACING";
            if (unit.Overwatch && unit.Overwatch.IsActive) return "OVERWATCH";
            if (unit.Team == UnitTeam.Enemy) return "INSPECT ONLY";
            if (turns && turns.ActiveTeam != unit.Team) return "WAITING";
            return unit.ActionPoints.CurrentPoints > 0 ? "READY" : "NO AP";
        }
        public bool ContainsScreenPoint(Vector2 point) => IsVisible && RectTransformUtility.RectangleContainsScreenPoint(panelRoot, point);
        private void Hide() { DisplayedUnit = null; if (panelRoot) panelRoot.gameObject.SetActive(false); }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (bodyFont) Destroy(bodyFont); }
    }
}
