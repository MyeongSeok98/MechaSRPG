using UnityEngine;

namespace MechaXCOM
{
    // A partial camera viewport does not clear the rest of the backbuffer.
    // Repaint those gutters behind IMGUI, including after an overlay has disappeared.
    [DisallowMultipleComponent]
    public class BattleViewportBackground : MonoBehaviour
    {
        private Camera battleCamera;
        public void Initialize(Camera camera) => battleCamera = camera;

        private void OnGUI()
        {
            // IMGUI uses this value to order this component's subsequent GUI events.
            GUI.depth = 10000;
            if (!battleCamera || !battleCamera.isActiveAndEnabled || battleCamera.targetTexture ||
                Event.current.type != EventType.Repaint) return;
            var viewport = battleCamera.pixelRect;
            float left = Mathf.Clamp(viewport.xMin, 0, Screen.width);
            float right = Mathf.Clamp(viewport.xMax, left, Screen.width);
            float top = Mathf.Clamp(Screen.height - viewport.yMax, 0, Screen.height);
            float bottom = Mathf.Clamp(Screen.height - viewport.yMin, top, Screen.height);
            var color = GUI.color;
            var matrix = GUI.matrix;
            GUI.color = Color.black; GUI.matrix = Matrix4x4.identity;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, top), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, bottom, Screen.width, Screen.height - bottom), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, top, left, bottom - top), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(right, top, Screen.width - right, bottom - top), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = color;
        }
    }
}
