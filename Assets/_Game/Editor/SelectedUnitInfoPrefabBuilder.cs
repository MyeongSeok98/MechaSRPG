using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MechaXCOM.EditorTools
{
    public static class SelectedUnitInfoPrefabBuilder
    {
        public const string PanelPath = "Assets/_Game/Prefabs/UI/SelectedUnitInfoPanel.prefab";
        public const string RowPath = "Assets/_Game/Prefabs/UI/UnitStatusRow.prefab";
        private static Font BodyFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Font NumberFont => Resources.Load<Font>("Oxanium-Regular") ?? BodyFont;

        [MenuItem("MechaXCOM/Create Selected Unit Info Panel Prefabs")]
        public static void Build()
        {
            if (File.Exists(PanelPath)) throw new System.InvalidOperationException("선택 기체 정보 패널이 이미 있습니다. 기존 에셋은 덮어쓰지 않습니다.");
            var frame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Panels/UnitInfoFrame_Olive.png");
            if (!frame) throw new System.InvalidOperationException("9-slice 프레임을 찾을 수 없습니다.");
            var row = Node("Unit Status Row", null); Height(row, 30);
            var horizontal = row.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 8;
            horizontal.childControlWidth = horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = false;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            var label = Label("Part Label", row.transform, 14, 30); Width(label.gameObject, 58);
            var track = Image("HP Track", row.transform, new Color32(22, 28, 25, 220));
            var trackLayout = track.gameObject.AddComponent<LayoutElement>(); trackLayout.minWidth = 36; trackLayout.flexibleWidth = 1; trackLayout.preferredHeight = 9;
            var fill = Image("HP Fill", track.transform, Color.white); Stretch(fill.rectTransform, 1);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 1;
            var hp = Label("HP Value", row.transform, 14, 30, true); hp.alignment = TextAnchor.MiddleRight; Width(hp.gameObject, 92);
            var rowView = row.AddComponent<UnitStatusRowUI>(); Set(rowView, "partLabel", label); Set(rowView, "healthText", hp); Set(rowView, "healthFill", fill);
            var rowAsset = PrefabUtility.SaveAsPrefabAsset(row, RowPath).GetComponent<UnitStatusRowUI>(); Object.DestroyImmediate(row);

            var canvasRoot = Node("Selected Unit Info Panel", null);
            var canvas = canvasRoot.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = canvasRoot.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            canvasRoot.AddComponent<GraphicRaycaster>();
            var panel = Image("Panel", canvasRoot.transform, Color.white); panel.sprite = frame; panel.type = UnityEngine.UI.Image.Type.Sliced;
            var rect = panel.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(16, -16); rect.sizeDelta = new Vector2(350, 584);
            var vertical = Vertical(panel.gameObject, 10); vertical.padding = new RectOffset(24, 24, 44, 32);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = Node("Identity", panel.transform); Height(header, 50); Vertical(header, 2);
            var name = Label("Unit Name", header.transform, 24, 30); name.fontStyle = FontStyle.Bold; name.text = "CAVALIER";
            var pilot = Label("Pilot Name", header.transform, 14, 18); pilot.color = new Color32(180, 195, 178, 255);
            var summary = Node("Portrait and AP", panel.transform); Height(summary, 166);
            var summaryLayout = summary.AddComponent<HorizontalLayoutGroup>(); summaryLayout.spacing = 12;
            summaryLayout.childControlWidth = summaryLayout.childControlHeight = true;
            summaryLayout.childForceExpandWidth = summaryLayout.childForceExpandHeight = false;
            var portraitFrame = Image("Portrait Frame", summary.transform, new Color32(118, 133, 119, 230)); Width(portraitFrame.gameObject, 130); Height(portraitFrame.gameObject, 164);
            var portrait = Image("Portrait", portraitFrame.transform, Color.white); Stretch(portrait.rectTransform, 2); portrait.preserveAspect = true; portrait.raycastTarget = false;
            var stats = Node("Action State", summary.transform); var statsLayout = stats.AddComponent<LayoutElement>(); statsLayout.flexibleWidth = 1;
            var statsVertical = Vertical(stats, 3); statsVertical.childAlignment = TextAnchor.MiddleLeft;
            var ap = Label("AP", stats.transform, 27, 46, true); ap.text = "AP 5/5";
            var conditionCaption = Label("Condition Caption", stats.transform, 11, 14); conditionCaption.text = "CONDITION"; conditionCaption.color = Color.gray;
            var available = Label("Availability", stats.transform, 19, 26); available.fontStyle = FontStyle.Bold;
            var stateCaption = Label("State Caption", stats.transform, 11, 14); stateCaption.text = "STATE"; stateCaption.color = Color.gray;
            var state = Label("State", stats.transform, 17, 28);

            var section = Image("Status Header", panel.transform, new Color32(78, 95, 99, 220)); Height(section.gameObject, 30);
            var sectionText = Label("Status Title", section.transform, 18, 30); Stretch(sectionText.rectTransform, 5); sectionText.text = "UNIT STATUS"; sectionText.fontStyle = FontStyle.Bold;
            var scrollRoot = Node("Status List", panel.transform); var statusHeight = scrollRoot.AddComponent<LayoutElement>(); statusHeight.preferredHeight = 234;
            var scroll = scrollRoot.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.inertia = false;
            scroll.scrollSensitivity = 24; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Image("Viewport", scrollRoot.transform, Color.clear); Stretch(viewport.rectTransform, 0); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node("Parts", viewport.transform); var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(.5f, 1);
            contentRect.sizeDelta = Vector2.zero; Vertical(content, 4);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform; scroll.content = contentRect;
            var view = canvasRoot.AddComponent<SelectedUnitInfoPanel>();
            Set(view, "panelRoot", rect); Set(view, "unitName", name); Set(view, "pilotName", pilot); Set(view, "portrait", portrait);
            Set(view, "apText", ap); Set(view, "availability", available); Set(view, "state", state); Set(view, "statusContent", contentRect);
            Set(view, "statusScroll", scroll); Set(view, "statusHeight", statusHeight); Set(view, "rowPrefab", rowAsset);
            PrefabUtility.SaveAsPrefabAsset(canvasRoot, PanelPath); Object.DestroyImmediate(canvasRoot); AssetDatabase.SaveAssets();
        }
        private static GameObject Node(string name, Transform parent)
        { var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5; if (parent) obj.transform.SetParent(parent, false); return obj; }
        private static Image Image(string name, Transform parent, Color color)
        { var image = Node(name, parent).AddComponent<Image>(); image.color = color; return image; }
        private static Text Label(string name, Transform parent, int size, float height, bool numeric = false)
        {
            var text = Node(name, parent).AddComponent<Text>(); text.font = numeric ? NumberFont : BodyFont; text.fontSize = size;
            text.color = new Color32(232, 237, 228, 255); text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            Height(text.gameObject, height); return text;
        }
        private static VerticalLayoutGroup Vertical(GameObject obj, int spacing)
        {
            var layout = obj.AddComponent<VerticalLayoutGroup>(); layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return layout;
        }
        private static void Height(GameObject obj, float height) => (obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>()).preferredHeight = height;
        private static void Width(GameObject obj, float width)
        { var layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>(); layout.minWidth = layout.preferredWidth = width; layout.flexibleWidth = 0; }
        private static void Stretch(RectTransform rect, float padding)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * padding; rect.offsetMax = Vector2.one * -padding; }
        private static void Set(Object target, string field, Object value)
        { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
