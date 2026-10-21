using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class TestWeaponOwner : MonoBehaviour
    {
        private readonly List<WeaponData> owned = new List<WeaponData>();
        public void Own(WeaponData weapon) => owned.Add(weapon);
        private void OnDestroy()
        {
            foreach (var weapon in owned)
                if (weapon) { if (Application.isPlaying) Destroy(weapon); else DestroyImmediate(weapon); }
            owned.Clear();
        }
    }
}
