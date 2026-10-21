using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MechaXCOM
{
    public class UnitHealth : MonoBehaviour
    {
        [FormerlySerializedAs("maxHealth")]
        [SerializeField, Min(1)] private int bodyMaxHP = 20;
        [SerializeField, Min(1)] private int cockpitMaxHP = 6;
        [SerializeField, Min(1)] private int headMaxHP = 6;
        [SerializeField, Min(1)] private int leftArmMaxHP = 8;
        [SerializeField, Min(1)] private int rightArmMaxHP = 8;
        [SerializeField, Min(1)] private int leftLegMaxHP = 10;
        [SerializeField, Min(1)] private int rightLegMaxHP = 10;
        [SerializeField, Range(0, 100)] private int destructionChance = 25;

        // Seven entries, indexed from Torso through RightLeg. Whole is only a targeting alias.
        private readonly int[] currentHP = new int[7];
        private bool initialized;
        private Func<int> destructionRollSource;
        public MechState State { get; private set; } = MechState.Active;
        public bool IsPresent => initialized && (State == MechState.Active || State == MechState.Critical);
        public bool CanFight => initialized && State == MechState.Active;
        public bool PilotEjected => State == MechState.Ejected;
        public event Action StateChanged;
        public event Action<TargetBodyPart> PartDestroyed;

        public int MajorPartsDestroyed
        {
            get
            {
                if (!initialized) return 0;
                int count = 0;
                for (int index = (int)TargetBodyPart.Head - 1; index < currentHP.Length; index++)
                    if (currentHP[index] == 0) count++;
                return count;
            }
        }

        public int GetCurrentHP(TargetBodyPart part)
        {
            part = BodyTargetRules.Normalize(part);
            return IsValidPart(part) ? currentHP[(int)part - 1] : 0;
        }

        public int GetMaxHP(TargetBodyPart part)
        {
            switch (BodyTargetRules.Normalize(part))
            {
                case TargetBodyPart.Torso: return bodyMaxHP;
                case TargetBodyPart.Cockpit: return cockpitMaxHP;
                case TargetBodyPart.Head: return headMaxHP;
                case TargetBodyPart.LeftArm: return leftArmMaxHP;
                case TargetBodyPart.RightArm: return rightArmMaxHP;
                case TargetBodyPart.LeftLeg: return leftLegMaxHP;
                case TargetBodyPart.RightLeg: return rightLegMaxHP;
                default: return 0;
            }
        }

        public void ConfigurePartHP(TargetBodyPart part, int maximum)
        {
            maximum = Mathf.Max(1, maximum);
            switch (BodyTargetRules.Normalize(part))
            {
                case TargetBodyPart.Torso: bodyMaxHP = maximum; break;
                case TargetBodyPart.Cockpit: cockpitMaxHP = maximum; break;
                case TargetBodyPart.Head: headMaxHP = maximum; break;
                case TargetBodyPart.LeftArm: leftArmMaxHP = maximum; break;
                case TargetBodyPart.RightArm: rightArmMaxHP = maximum; break;
                case TargetBodyPart.LeftLeg: leftLegMaxHP = maximum; break;
                case TargetBodyPart.RightLeg: rightLegMaxHP = maximum; break;
            }
        }

        public void ConfigureDestructionChance(int percent) => destructionChance = Mathf.Clamp(percent, 0, 100);
        public void SetDestructionRollSource(Func<int> source) => destructionRollSource = source;

        public void Initialize()
        {
            for (int index = 0; index < currentHP.Length; index++)
                currentHP[index] = GetMaxHP((TargetBodyPart)(index + 1));
            State = MechState.Active;
            initialized = true;
        }

        public PartDamageResult ApplyDamage(TargetBodyPart part, int damage)
        {
            part = BodyTargetRules.Normalize(part);
            var result = new PartDamageResult
            {
                Part = part, PartHPAfter = GetCurrentHP(part), StateBefore = State, StateAfter = State
            };
            if (!IsPresent || damage <= 0 || !IsValidPart(part)) return result;

            int index = (int)part - 1;
            result.Applied = true;
            result.DamageDealt = Math.Min(damage, currentHP[index]);
            currentHP[index] -= result.DamageDealt;
            result.PartHPAfter = currentHP[index];

            if (GetCurrentHP(TargetBodyPart.Torso) == 0 || GetCurrentHP(TargetBodyPart.Cockpit) == 0 || MajorPartsDestroyed >= 4)
                State = MechState.Destroyed;
            else if (State == MechState.Critical || MajorPartsDestroyed >= 3)
            {
                result.DestructionChecked = true;
                int roll = destructionRollSource != null ? destructionRollSource() : UnityEngine.Random.Range(0, 100);
                State = roll < destructionChance ? MechState.Destroyed : MechState.Critical;
            }

            result.StateAfter = State;
            if (result.StateBefore != State) StateChanged?.Invoke();
            if (result.DamageDealt > 0 && result.PartHPAfter == 0) PartDestroyed?.Invoke(part);
            return result;
        }

        public bool MarkEjected()
        {
            if (!IsPresent) return false;
            State = MechState.Ejected;
            StateChanged?.Invoke();
            return true;
        }

        private static bool IsValidPart(TargetBodyPart part) => part >= TargetBodyPart.Torso && part <= TargetBodyPart.RightLeg;

        private void OnValidate()
        {
            bodyMaxHP = Mathf.Max(1, bodyMaxHP);
            cockpitMaxHP = Mathf.Max(1, cockpitMaxHP);
            headMaxHP = Mathf.Max(1, headMaxHP);
            leftArmMaxHP = Mathf.Max(1, leftArmMaxHP);
            rightArmMaxHP = Mathf.Max(1, rightArmMaxHP);
            leftLegMaxHP = Mathf.Max(1, leftLegMaxHP);
            rightLegMaxHP = Mathf.Max(1, rightLegMaxHP);
            destructionChance = Mathf.Clamp(destructionChance, 0, 100);
        }
    }
}
