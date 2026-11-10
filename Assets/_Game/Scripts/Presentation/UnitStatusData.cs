using System.Collections.Generic;
using UnityEngine;

namespace MechaXCOM
{
    public interface IUnitStatusSource
    {
        void GetStatusParts(List<UnitStatusPart> destination);
    }

    public readonly struct UnitStatusPart
    {
        public string Id { get; }
        public string Label { get; }
        public int CurrentHP { get; }
        public int MaxHP { get; }
        public float Fraction => MaxHP > 0 ? Mathf.Clamp01((float)CurrentHP / MaxHP) : 0;
        public UnitStatusPart(string id, string label, int current, int maximum)
        { Id = id; Label = label; MaxHP = Mathf.Max(0, maximum); CurrentHP = Mathf.Clamp(current, 0, MaxHP); }
    }
}
