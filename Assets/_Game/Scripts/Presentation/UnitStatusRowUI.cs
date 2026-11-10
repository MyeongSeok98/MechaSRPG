using UnityEngine;
using UnityEngine.UI;

namespace MechaXCOM
{
    public class UnitStatusRowUI : MonoBehaviour
    {
        [SerializeField] private Text partLabel;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;
        public string PartId { get; private set; }
        public string HealthText => healthText.text;
        public float HealthFraction => healthFill.fillAmount;
        public void Show(UnitStatusPart part)
        {
            PartId = part.Id; partLabel.text = part.Label;
            healthText.text = $"{part.CurrentHP}/{part.MaxHP}";
            healthFill.fillAmount = part.Fraction;
            var color = part.CurrentHP <= 0 ? new Color32(208, 81, 71, 255) :
                part.Fraction < .5f ? new Color32(225, 171, 68, 255) : new Color32(117, 194, 140, 255);
            healthFill.color = color; healthText.color = part.CurrentHP <= 0 ? color : new Color32(232, 237, 228, 255);
        }
    }
}
