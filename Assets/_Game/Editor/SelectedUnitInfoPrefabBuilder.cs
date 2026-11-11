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
        public const string WeaponRowPath = "Assets/_Game/Prefabs/UI/UnitWeaponRow.prefab";
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
            var fill = Image("HP Fill", track.transform, Color.white); Stretch(fill.rectTransform, 0);
            fill.sprite = SquareHealthSprite();
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
            ApplyCompactWeaponLayout();
        }
        [MenuItem("MechaXCOM/Apply Compact Unit Info Layout")]
        public static void ApplyCompactWeaponLayout()
        {
            var row = PrefabUtility.LoadPrefabContents(RowPath);
            Height(row, 26);
            foreach (var text in row.GetComponentsInChildren<Text>()) { text.fontSize = 16; Height(text.gameObject, 26); }
            Width(row.transform.Find("Part Label").gameObject, 66);
            Width(row.transform.Find("HP Value").gameObject, 104);
            Height(row.transform.Find("HP Track").gameObject, 10);
            ConfigureSquareHealthFill(row);
            PrefabUtility.SaveAsPrefabAsset(row, RowPath); PrefabUtility.UnloadPrefabContents(row);

            UnitWeaponRowUI weaponAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponRowPath)?.GetComponent<UnitWeaponRowUI>();
            if (!weaponAsset)
            {
                var weapon = Node("Unit Weapon Row", null); Height(weapon, 38); Vertical(weapon, 0);
                var top = Node("Weapon", weapon.transform); Height(top, 21);
                var h = top.AddComponent<HorizontalLayoutGroup>(); h.spacing = 6; h.childControlWidth = h.childControlHeight = true;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
                var slot = Label("Slot", top.transform, 16, 21); Width(slot.gameObject, 34); slot.fontStyle = FontStyle.Bold;
                var name = Label("Name", top.transform, 18, 21); name.fontStyle = FontStyle.Bold;
                name.GetComponent<LayoutElement>().flexibleWidth = 1;
                var bottom = Node("Resources", weapon.transform); Height(bottom, 17);
                var b = bottom.AddComponent<HorizontalLayoutGroup>(); b.spacing = 4; b.childControlWidth = b.childControlHeight = true;
                b.childForceExpandWidth = b.childForceExpandHeight = false;
                var resources = Label("Ammo", bottom.transform, 13, 17, true); resources.GetComponent<LayoutElement>().flexibleWidth = 1;
                var status = Label("Status", bottom.transform, 13, 17); Width(status.gameObject, 64); status.alignment = TextAnchor.MiddleRight;
                var view = weapon.AddComponent<UnitWeaponRowUI>(); Set(view, "slotLabel", slot); Set(view, "weaponName", name);
                Set(view, "resources", resources); Set(view, "status", status);
                weaponAsset = PrefabUtility.SaveAsPrefabAsset(weapon, WeaponRowPath).GetComponent<UnitWeaponRowUI>(); Object.DestroyImmediate(weapon);
            }
            ApplySingleLineWeaponRows();
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var panel = root.transform.Find("Panel"); var rect = (RectTransform)panel;
                rect.sizeDelta = new Vector2(390, rect.sizeDelta.y);
                var layout = panel.GetComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.padding = new RectOffset(24, 24, 40, 36);
                Height(panel.Find("Identity").gameObject, 54);
                ResizeText(panel, "Identity/Unit Name", 28, 33); ResizeText(panel, "Identity/Pilot Name", 17, 19);
                Height(panel.Find("Portrait and AP").gameObject, 148);
                var portrait = panel.Find("Portrait and AP/Portrait Frame").gameObject; Width(portrait, 118); Height(portrait, 148);
                ResizeText(panel, "Portrait and AP/Action State/AP", 32, 42);
                ResizeText(panel, "Portrait and AP/Action State/Availability", 22, 28);
                ResizeText(panel, "Portrait and AP/Action State/State", 19, 26);
                Height(panel.Find("Status Header").gameObject, 26); ResizeText(panel, "Status Header/Status Title", 20, 26);
                panel.Find("Status List/Viewport/Parts").GetComponent<VerticalLayoutGroup>().spacing = 1;
                var view = root.GetComponent<SelectedUnitInfoPanel>(); var serialized = new SerializedObject(view);
                serialized.FindProperty("maximumStatusHeight").floatValue = 188;
                serialized.FindProperty("statusRowHeight").floatValue = 26; serialized.FindProperty("statusRowSpacing").floatValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var header = panel.Find("Weapon Header");
                if (!header)
                {
                    var image = Image("Weapon Header", panel, new Color32(78, 95, 99, 220)); Height(image.gameObject, 26); header = image.transform;
                    var title = Label("Weapon Title", header, 20, 26); Stretch(title.rectTransform, 5); title.text = "WEAPON LOADOUT"; title.fontStyle = FontStyle.Bold;
                }
                var content = panel.Find("Weapons");
                if (!content) { var obj = Node("Weapons", panel); Vertical(obj, 2); content = obj.transform; }
                Set(view, "weaponHeader", header.gameObject); Set(view, "weaponContent", content.GetComponent<RectTransform>()); Set(view, "weaponRowPrefab", weaponAsset);
                // Keep horizontal inset without clipping 20px section titles vertically.
                foreach (var title in new[] { panel.Find("Status Header/Status Title"), header.Find("Weapon Title") })
                {
                    var titleRect = title.GetComponent<RectTransform>();
                    titleRect.offsetMin = new Vector2(5, 0); titleRect.offsetMax = new Vector2(-5, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        private static void ResizeText(Transform root, string path, int size, float height)
        { var text = root.Find(path).GetComponent<Text>(); text.fontSize = size; Height(text.gameObject, height); }
        [MenuItem("MechaXCOM/Apply Stencilia Unit Info Font")]
        public static void ApplyStenciliaFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Art/UI/Fonts/Stencilia/Stencilia-Bold.ttf");
            if (!font) throw new System.InvalidOperationException("Stencilia-Bold.ttf 파일이 필요합니다.");
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                Set(root.GetComponent<SelectedUnitInfoPanel>(), "displayFont", font);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("MechaXCOM/Apply Saira and Oxanium Unit Info Fonts")]
        public static void ApplySairaOxaniumFonts()
        {
            var titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Art/UI/Fonts/SairaStencil/SairaStencil-SemiBold.ttf");
            var contentFont = Resources.Load<Font>("Oxanium-Regular");
            if (!titleFont || !contentFont) throw new System.InvalidOperationException("Saira Stencil과 Oxanium 폰트가 필요합니다.");
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var view = root.GetComponent<SelectedUnitInfoPanel>();
                Set(view, "displayFont", titleFont); Set(view, "contentFont", contentFont);
                var data = new SerializedObject(view); var titles = data.FindProperty("sectionTitles"); titles.arraySize = 2;
                titles.GetArrayElementAtIndex(0).objectReferenceValue = root.transform.Find("Panel/Status Header/Status Title").GetComponent<Text>();
                titles.GetArrayElementAtIndex(1).objectReferenceValue = root.transform.Find("Panel/Weapon Header/Weapon Title").GetComponent<Text>();
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    bool title = text == root.transform.Find("Panel/Identity/Unit Name").GetComponent<Text>() ||
                        text == root.transform.Find("Panel/Identity/Pilot Name").GetComponent<Text>() || text.name == "Status Title" || text.name == "Weapon Title";
                    text.font = title ? titleFont : contentFont;
                    if (title)
                    {
                        text.fontStyle = FontStyle.Normal;
                        // Saira has a taller line box than its visible glyphs; avoid dropping the whole line.
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("MechaXCOM/Apply Single Line Weapon Rows")]
        public static void ApplySingleLineWeaponRows()
        {
            var row = PrefabUtility.LoadPrefabContents(WeaponRowPath);
            try
            {
                for (int i = row.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(row.transform.GetChild(i).gameObject);
                var oldLayout = row.GetComponent<VerticalLayoutGroup>(); if (oldLayout) Object.DestroyImmediate(oldLayout);
                Height(row, 28);
                var h = row.GetComponent<HorizontalLayoutGroup>() ?? row.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 6; h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
                var badge = Node("Slot Badge", row.transform); Width(badge, 26); Height(badge, 22);
                var slot = Label("Slot", badge.transform, 15, 22); Stretch(slot.rectTransform, 0); slot.alignment = TextAnchor.MiddleCenter; slot.fontStyle = FontStyle.Bold;
                slot.rectTransform.anchoredPosition = new Vector2(0, 2); // Optical centering for the font's baseline.
                foreach (var side in new[] { "Top", "Bottom", "Left", "Right" })
                {
                    var edge = Image(side, badge.transform, new Color32(45, 190, 195, 255)); edge.raycastTarget = false;
                    var r = edge.rectTransform; Stretch(r, 0);
                    // Keep the full stroke inside the badge; 2px survives reduced Canvas scale.
                    if (side == "Top" || side == "Bottom")
                    {
                        float y = side == "Top" ? 1 : 0;
                        r.anchorMin = new Vector2(0, y); r.anchorMax = new Vector2(1, y);
                        r.pivot = new Vector2(.5f, y); r.sizeDelta = new Vector2(0, 2);
                    }
                    else
                    {
                        float x = side == "Right" ? 1 : 0;
                        r.anchorMin = new Vector2(x, 0); r.anchorMax = new Vector2(x, 1);
                        r.pivot = new Vector2(x, .5f); r.sizeDelta = new Vector2(2, 0);
                    }
                }
                var name = Label("Name", row.transform, 15, 24); name.fontStyle = FontStyle.Bold;
                name.horizontalOverflow = HorizontalWrapMode.Overflow; name.resizeTextForBestFit = true; name.resizeTextMinSize = 10; name.resizeTextMaxSize = 15;
                name.GetComponent<LayoutElement>().flexibleWidth = 1; name.GetComponent<LayoutElement>().minWidth = 50;
                WeaponDivider(row.transform, "Ammo Divider");
                var ammo = Label("Ammo", row.transform, 11, 24, true); Width(ammo.gameObject, 75); ammo.alignment = TextAnchor.MiddleCenter;
                WeaponDivider(row.transform, "Reserve Divider");
                var resourceEnd = Node("Reserve or Status", row.transform); Width(resourceEnd, 68); Height(resourceEnd, 24);
                var reserve = Label("Reserve", resourceEnd.transform, 11, 24, true); Stretch(reserve.rectTransform, 0); reserve.alignment = TextAnchor.MiddleRight;
                var status = Label("Status", resourceEnd.transform, 12, 24); Stretch(status.rectTransform, 0); status.alignment = TextAnchor.MiddleRight;
                var view = row.GetComponent<UnitWeaponRowUI>(); Set(view, "slotLabel", slot); Set(view, "weaponName", name);
                Set(view, "resources", ammo); Set(view, "reserve", reserve); Set(view, "status", status);
                PrefabUtility.SaveAsPrefabAsset(row, WeaponRowPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(row); }
            AssetDatabase.SaveAssets();
        }
        private static void WeaponDivider(Transform parent, string name)
        {
            var divider = Image(name, parent, new Color32(150, 165, 153, 220)); divider.raycastTarget = false;
            Width(divider.gameObject, 1); Height(divider.gameObject, 18);
        }
        [MenuItem("MechaXCOM/Apply Square Health Bars")]
        public static void ApplySquareHealthBars()
        {
            var row = PrefabUtility.LoadPrefabContents(RowPath);
            try { ConfigureSquareHealthFill(row); PrefabUtility.SaveAsPrefabAsset(row, RowPath); }
            finally { PrefabUtility.UnloadPrefabContents(row); }
            AssetDatabase.SaveAssets();
        }
        private static void ConfigureSquareHealthFill(GameObject row)
        {
            var fill = row.transform.Find("HP Track/HP Fill").GetComponent<Image>();
            fill.sprite = SquareHealthSprite(); Stretch(fill.rectTransform, 0);
        }
        private static Sprite SquareHealthSprite()
        {
            const string path = "Assets/_Game/Art/UI/Panels/HealthBarFill.png";
            if (!File.Exists(path))
            {
                // An opaque sprite keeps Image.Filled working at partial HP without rounded transparent edges.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
