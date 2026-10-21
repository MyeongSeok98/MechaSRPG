using UnityEngine;

namespace MechaXCOM
{
    public class BodyTargetArt : ScriptableObject
    {
        [SerializeField] private Texture2D[] images;
        public Texture2D GetImage(TargetBodyPart part)
        {
            int index = (int)part;
            return images != null && index >= 0 && index < images.Length ? images[index] : null;
        }
    }
}
