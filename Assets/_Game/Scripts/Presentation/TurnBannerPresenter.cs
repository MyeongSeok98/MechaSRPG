using UnityEngine;

namespace MechaXCOM
{
    [DisallowMultipleComponent]
    public class TurnBannerPresenter : MonoBehaviour
    {
        [SerializeField, InspectorName("적 턴 이미지")] private Texture2D enemyTurnImage;
        [SerializeField, InspectorName("플레이어 턴 이미지")] private Texture2D playerTurnImage;
        [SerializeField, Range(.01f, 1), InspectorName("진입 시간")] private float slideInDuration = .15f;
        [SerializeField, Range(0, 3), InspectorName("중앙 대기 시간")] private float holdDuration = .4f;
        [SerializeField, Range(.01f, 1), InspectorName("퇴장 시간")] private float slideOutDuration = .15f;
        private TurnManager turns;
        private bool subscribed, active, initialized;
        private float startedAt, activeEnter, activeHold, activeExit;
        private int lastShownTurn = -1;
        private UnitTeam lastShownTeam;
        public UnitTeam CurrentTeam { get; private set; }
        public bool IsVisible => active && Ready && Image(CurrentTeam) && turns.OwnsTurnPresentation(this);
        private bool Ready => Application.isPlaying && isActiveAndEnabled && turns && turns.isActiveAndEnabled && !turns.IsBattleOver;
        public Texture2D CurrentTexture => IsVisible ? Image(CurrentTeam) : null;
        public TurnBannerFrame CurrentFrame
        {
            get
            {
                var image = CurrentTexture;
                if (!image) return default;
                float width = Screen.width * .9f, height = width * image.height / Mathf.Max(1, image.width);
                var centered = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
                return TurnBannerAnimation.Evaluate(Time.unscaledTime - startedAt, activeEnter, activeHold, activeExit, centered, Screen.width);
            }
        }
        public void Initialize(TurnManager manager)
        {
            if (initialized && turns == manager) return;
            Unsubscribe(); Cancel(); turns = manager; initialized = true; lastShownTurn = -1;
            if (!enemyTurnImage) enemyTurnImage = Resources.Load<Texture2D>("TurnBanners/EnemyTurn");
            if (!playerTurnImage) playerTurnImage = Resources.Load<Texture2D>("TurnBanners/YourTurn");
            Subscribe(); ShowCurrent();
        }
        private Texture2D Image(UnitTeam team) => team == UnitTeam.Player ? playerTurnImage : enemyTurnImage;
        private void ShowCurrent() { if (turns && turns.TurnNumber > 0) Show(turns.ActiveTeam); }
        private static float Duration(float value, float fallback, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        private void Show(UnitTeam team)
        {
            if (!Ready || (lastShownTurn == turns.TurnNumber && lastShownTeam == team)) return;
            Cancel();
            if (!Image(team)) return;
            activeEnter = Duration(slideInDuration, .15f, .01f, 1);
            activeHold = Duration(holdDuration, .4f, 0, 3);
            activeExit = Duration(slideOutDuration, .15f, .01f, 1);
            CurrentTeam = team; lastShownTurn = turns.TurnNumber; lastShownTeam = team;
            startedAt = Time.unscaledTime; active = true;
            turns.HoldTurnPresentation(this, activeEnter + activeHold + activeExit);
        }
        private void Subscribe()
        { if (!subscribed && turns && isActiveAndEnabled) { turns.TurnStarted += Show; subscribed = true; } }
        private void Unsubscribe()
        { if (subscribed && turns) turns.TurnStarted -= Show; subscribed = false; }
        private void Cancel() { active = false; if (turns) turns.ReleaseTurnPresentation(this); }
        private void Update() { if (active && !IsVisible) Cancel(); }
        private void OnEnable() { Subscribe(); ShowCurrent(); }
        private void OnDisable() { Unsubscribe(); Cancel(); }
        private void OnDestroy() { Unsubscribe(); Cancel(); }
        private void OnGUI()
        {
            if (!IsVisible) return;
            var frame = CurrentFrame; if (!frame.Visible) return;
            var color = GUI.color; int depth = GUI.depth;
            GUI.depth = -1000; var faded = color; faded.a *= frame.Alpha; GUI.color = faded;
            GUI.DrawTexture(frame.Rect, CurrentTexture, ScaleMode.ScaleToFit, true);
            GUI.color = color; GUI.depth = depth;
        }
    }
}
