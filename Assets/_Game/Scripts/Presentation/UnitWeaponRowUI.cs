using UnityEngine;
using UnityEngine.UI;

namespace MechaXCOM
{
    public class UnitWeaponRowUI : MonoBehaviour
    {
        [SerializeField] private Text slotLabel, weaponName, resources, status, reserve;
        private string resourceText;
        private Font bodyFont, displayFont;
        public WeaponSlot Slot { get; private set; }
        public bool IsDisabled { get; private set; }
        public string StatusText => status.text;
        public string ResourceText => resourceText;
        public Color NameColor => weaponName.color;
        public void ApplyBodyFont(Font font, Font display = null)
        { bodyFont = font; displayFont = display; slotLabel.font = weaponName.font = status.font = font; }
        public void Show(UnitWeaponInfo info)
        {
            Slot = info.Slot; IsDisabled = info.IsDisabled;
            slotLabel.text = info.SlotLabel; weaponName.text = info.Name;
            resourceText = info.ResourceText;
            resources.text = info.AmmoText; status.text = info.StatusText;
            if (bodyFont)
            {
                UnitInfoFont.Apply(slotLabel, displayFont, bodyFont);
                UnitInfoFont.Apply(weaponName, displayFont, bodyFont);
                UnitInfoFont.Apply(status, displayFont, bodyFont);
            }
            reserve.text = info.ReserveText;
            reserve.enabled = info.StatusText.Length == 0;
            status.enabled = info.StatusText.Length > 0;
            Color red = new Color32(255, 100, 90, 255), amber = new Color32(245, 185, 85, 255);
            weaponName.color = info.IsDisabled ? red : new Color32(232, 237, 228, 255);
            slotLabel.color = info.IsDisabled ? red : new Color32(65, 222, 225, 255);
            reserve.color = resources.color = status.color = info.IsDisabled ? red : info.ResourceShortage ? amber : new Color32(212, 225, 211, 255);
        }
    }
}
