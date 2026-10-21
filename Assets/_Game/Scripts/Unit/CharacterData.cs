using UnityEngine;

namespace MechaXCOM
{
    // 파일럿의 초기 정의만 저장한다. 성장한 능력치와 전투 중 상태는 별도 책임이다.
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "MechaXCOM/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [SerializeField, InspectorName("캐릭터 ID")] private string characterId;
        [SerializeField, InspectorName("이름")] private string displayName;
        [SerializeField, InspectorName("설명"), TextArea(3, 6)] private string description;
        [SerializeField, InspectorName("초기 레벨"), Min(1)] private int initialLevel = 1;
        [SerializeField, InspectorName("사격"), Min(0)] private int shooting;
        [SerializeField, InspectorName("근접"), Min(0)] private int melee;
        [SerializeField, InspectorName("기량"), Min(0)] private int technique;
        [SerializeField, InspectorName("회피"), Min(0)] private int evasion;

        public string CharacterId => characterId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public int InitialLevel => initialLevel;
        public int Shooting => shooting;
        public int Melee => melee;
        public int Technique => technique;
        public int Evasion => evasion;

        public bool IsValid(out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(characterId)) reason = "캐릭터 ID를 입력하세요.";
            else if (initialLevel < 1) reason = "초기 레벨은 1 이상이어야 합니다.";
            else if (shooting < 0 || melee < 0 || technique < 0 || evasion < 0)
                reason = "기본 능력치는 0 이상이어야 합니다.";
            return reason == null;
        }

        private void OnValidate()
        {
            initialLevel = Mathf.Max(1, initialLevel);
            shooting = Mathf.Max(0, shooting);
            melee = Mathf.Max(0, melee);
            technique = Mathf.Max(0, technique);
            evasion = Mathf.Max(0, evasion);
        }
    }
}
