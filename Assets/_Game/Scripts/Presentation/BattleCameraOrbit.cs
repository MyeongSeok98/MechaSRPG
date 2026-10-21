using UnityEngine;
using UnityEngine.InputSystem;

namespace MechaXCOM
{
    [RequireComponent(typeof(Camera))]
    public class BattleCameraOrbit : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float rotationDuration = 0.45f;
        [SerializeField, Min(0.01f)] private float focusDuration = 0.35f;
        [SerializeField, Min(0)] private float panSpeed = 12f;
        [SerializeField, Min(0), Tooltip("Orthographic size change per mouse wheel notch (120 scroll units).")]
        private float zoomSpeed = 60f;
        [SerializeField, Min(0.01f)] private float minZoomSize = 5f;
        [SerializeField, Min(0.01f)] private float maxZoomSize = 60f;
        private Camera battleCamera;
        private Vector3 center;
        private float radius, height, startYaw, targetYaw, elapsed;
        private Quaternion startRotation;
        private Vector3 focusStart, focusDestination, focusPoint;
        private bool initialized, rotating, focusing;

        public void Initialize(GridManager grid)
        {
            if (initialized || !grid) return;
            battleCamera = GetComponent<Camera>();
            center = grid.transform.position + new Vector3(grid.Width * grid.CellSize / 2f, 0, grid.Height * grid.CellSize / 2f);
            var offset = transform.position - center;
            radius = new Vector2(offset.x, offset.z).magnitude;
            height = offset.y;
            initialized = true;
        }

        public void RotateLeft() => BeginRotation(-90f);
        public void RotateRight() => BeginRotation(90f);

        public bool TryFocus(Vector3 point)
        {
            if (!initialized || !isActiveAndEnabled || rotating || focusing || Mathf.Abs(transform.forward.y) < 0.001f)
                return false;
            float distance = (point.y - transform.position.y) / transform.forward.y;
            if (distance <= 0) return false;
            focusStart = transform.position;
            focusDestination = point - transform.forward * distance;
            focusPoint = point;
            elapsed = 0;
            focusing = true;
            return true;
        }

        private void BeginRotation(float degrees)
        {
            if (!initialized || !isActiveAndEnabled || rotating || focusing) return;
            var offset = transform.position - center;
            radius = new Vector2(offset.x, offset.z).magnitude;
            height = offset.y;
            if (radius < 0.001f) return;
            startYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            targetYaw = startYaw + degrees;
            startRotation = transform.rotation;
            elapsed = 0;
            rotating = true;
        }

        private void Update()
        {
            if (!initialized) return;
            var mouse = Mouse.current;
            if (mouse != null && battleCamera.orthographic)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0)
                {
                    float minimum = Mathf.Max(.01f, minZoomSize);
                    battleCamera.orthographicSize = Mathf.Clamp(
                        battleCamera.orthographicSize - scroll / 120f * Mathf.Max(0, zoomSpeed),
                        minimum, Mathf.Max(minimum, maxZoomSize));
                }
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.qKey.wasPressedThisFrame) RotateRight();
            else if (keyboard.eKey.wasPressedThisFrame) RotateLeft();
            if (rotating || focusing) return;
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            var displacement = (right * input.x + forward * input.y) * Mathf.Max(0, panSpeed) * Time.unscaledDeltaTime;
            transform.position += displacement;
            center += displacement;
        }

        private void LateUpdate()
        {
            if (focusing)
            {
                elapsed += Time.unscaledDeltaTime;
                float focusProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, focusDuration));
                transform.position = Vector3.Lerp(focusStart, focusDestination, Mathf.SmoothStep(0, 1, focusProgress));
                if (focusProgress >= 1)
                {
                    center = focusPoint;
                    focusing = false;
                }
                return;
            }
            if (!rotating) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, rotationDuration));
            float eased = Mathf.SmoothStep(0, 1, progress);
            float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, eased);
            float yaw = currentYaw * Mathf.Deg2Rad;
            transform.position = center + new Vector3(Mathf.Sin(yaw) * radius, height, Mathf.Cos(yaw) * radius);
            transform.rotation = Quaternion.AngleAxis(currentYaw - startYaw, Vector3.up) * startRotation;
            if (progress >= 1) rotating = false;
        }

        private void OnDisable() { rotating = false; focusing = false; }
    }
}
