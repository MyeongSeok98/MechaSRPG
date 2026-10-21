using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechaXCOM.Tests
{
    public class BattleCameraOrbitTests
    {
        private GameObject root;
        private Camera camera;
        private BattleCameraOrbit orbit;
        private Keyboard keyboard;
        private Mouse mouse;
        private Vector3 center;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            root = new GameObject("Temporary Camera Map");
            var grid = root.AddComponent<GridManager>();
            grid.transform.position = new Vector3(3, 2, -5);
            grid.Configure(20, 12, 2);
            center = new Vector3(23, 2, 7);
            camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = center + new Vector3(14, 28, -42);
            camera.transform.rotation = Quaternion.Euler(35, -40, 0);
            camera.orthographic = true;
            camera.orthographicSize = 17;
            orbit = camera.gameObject.AddComponent<BattleCameraOrbit>();
            orbit.Initialize(grid);
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseWheelZoomsBothWaysWithoutMovingCameraAndStopsWithoutInput()
        {
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) });
            yield return null;
            Assert.That(camera.orthographicSize, Is.LessThan(17), "Wheel up must zoom in.");
            float zoomedIn = camera.orthographicSize;
            yield return null;
            yield return null;
            Assert.That(camera.orthographicSize, Is.EqualTo(zoomedIn), "A wheel tick must not keep zooming.");
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
            yield return null;
            Assert.That(camera.orthographicSize, Is.GreaterThan(zoomedIn), "Wheel down must zoom out.");
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
        }

        [UnityTest]
        public IEnumerator WheelZoomRespectsInspectorLimitsAndSurvivesFocusAndOrbit()
        {
#if UNITY_EDITOR
            var settings = new SerializedObject(orbit);
            settings.FindProperty("zoomSpeed").floatValue = 2;
            settings.FindProperty("minZoomSize").floatValue = 8;
            settings.FindProperty("maxZoomSize").floatValue = 24;
            settings.ApplyModifiedPropertiesWithoutUndo();
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) });
            yield return null;
            Assert.That(camera.orthographicSize, Is.EqualTo(15).Within(.001f));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 12000) });
            yield return null;
            Assert.That(camera.orthographicSize, Is.EqualTo(8));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -12000) });
            yield return null;
            Assert.That(camera.orthographicSize, Is.EqualTo(24));
            var point = center + new Vector3(2, 0, 3);
            Assert.That(orbit.TryFocus(point), Is.True);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(camera.WorldToViewportPoint(point).x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(camera.WorldToViewportPoint(point).y, Is.EqualTo(.5f).Within(.001f));
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            for (int i = 0; i < 4; i++) { orbit.RotateRight(); yield return new WaitForSecondsRealtime(.6f); }
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.05f));
            Assert.That(camera.orthographicSize, Is.EqualTo(24));
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator StartupPreservesAuthoredPoseAndZoom()
        {
            yield return null;
            Assert.That(Vector3.Distance(camera.transform.position, center + new Vector3(14, 28, -42)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(35, -40, 0)), Is.LessThan(0.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(17));
        }

        [UnityTest]
        public IEnumerator QEKeysRotateRelativeToCurrentViewAndReturnAfterFourTurns()
        {
            var keys = new[] { Key.Q, Key.E, Key.E, Key.E, Key.E, Key.E };
            for (int i = 0; i < keys.Length; i++)
            {
                var offset = camera.transform.position - center;
                var initialRotation = camera.transform.rotation;
                float degrees = keys[i] == Key.Q ? 90 : -90;
                var expected = center + Quaternion.AngleAxis(degrees, Vector3.up) * offset;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.AngleAxis(degrees, Vector3.up) * initialRotation), Is.LessThan(0.05f));
                Assert.That(camera.orthographicSize, Is.EqualTo(17));
            }
        }

        [UnityTest]
        public IEnumerator FourTurnsInEitherDirectionRestoreInitialPositionRotationAndZoom()
        {
            var initialPosition = camera.transform.position;
            var initialRotation = camera.transform.rotation;
            float initialZoom = camera.orthographicSize;
            foreach (var key in new[] { Key.Q, Key.E })
            {
                for (int i = 0; i < 4; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSecondsRealtime(0.6f);
                }
                Assert.That(Vector3.Distance(camera.transform.position, initialPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, initialRotation), Is.LessThan(0.05f));
                Assert.That(camera.orthographicSize, Is.EqualTo(initialZoom));
            }
        }

        [UnityTest]
        public IEnumerator OrbitIsSmoothAndIgnoresInputUntilRotationFinishes()
        {
            var startPosition = camera.transform.position;
            var startRotation = camera.transform.rotation;
            orbit.RotateLeft();
            Assert.That(camera.transform.position, Is.EqualTo(startPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(startRotation));
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Vector3.Distance(camera.transform.position, startPosition), Is.GreaterThan(0.1f));
            var offset = camera.transform.position - center;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.EqualTo(new Vector2(14, -42).magnitude).Within(0.01f));
            Assert.That(offset.y, Is.EqualTo(28).Within(0.001f));
            var expected = center + Quaternion.AngleAxis(-90, Vector3.up) * (startPosition - center);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.01f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Vector3.Distance(camera.transform.position, startPosition), Is.LessThan(0.01f),
                "A new rotation must be accepted after the previous rotation finishes.");
        }

        [UnityTest]
        public IEnumerator ShortestArcCrossesNorthAndUsesInspectorDuration()
        {
#if UNITY_EDITOR
            var settings = new SerializedObject(orbit);
            settings.FindProperty("rotationDuration").floatValue = 0.8f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            float radius = new Vector2(14, -42).magnitude;
            float yaw = 350 * Mathf.Deg2Rad;
            camera.transform.position = center + new Vector3(Mathf.Sin(yaw) * radius, 28, Mathf.Cos(yaw) * radius);
            orbit.RotateRight();
            yield return new WaitForSecondsRealtime(0.3f);
            var offset = camera.transform.position - center;
            Assert.That(offset.x, Is.GreaterThan(0));
            Assert.That(offset.z, Is.GreaterThan(1), "350 to 80 degrees must pass North.");
            yield return new WaitForSecondsRealtime(0.3f);
            offset = camera.transform.position - center;
            float currentYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(currentYaw, 80)), Is.GreaterThan(1),
                "At 0.6 seconds the configured 0.8 second orbit must still be in progress.");
            yield return new WaitForSecondsRealtime(0.3f);
            float endYaw = 80 * Mathf.Deg2Rad;
            Assert.That(Vector3.Distance(camera.transform.position, center + new Vector3(Mathf.Sin(endYaw) * radius, 28, Mathf.Cos(endYaw) * radius)), Is.LessThan(0.01f));
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator DisabledComponentStopsAtCurrentPose()
        {
            orbit.RotateLeft();
            yield return new WaitForSecondsRealtime(0.15f);
            orbit.enabled = false;
            var position = camera.transform.position;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(camera.transform.position, Is.EqualTo(position));
            orbit.enabled = true;
            yield return null;
            Assert.That(camera.transform.position, Is.EqualTo(position));
        }

        [UnityTest]
        public IEnumerator WASDPansOnGroundRelativeToCurrentViewAndStopsOnRelease()
        {
            var rotation = camera.transform.rotation;
            float cameraHeight = camera.transform.position.y;
            foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D })
            {
                var start = camera.transform.position;
                var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                var expected = key == Key.W ? forward : key == Key.S ? -forward : key == Key.D ? right : -right;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return new WaitForSecondsRealtime(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                var movement = camera.transform.position - start;
                Assert.That(movement.magnitude, Is.GreaterThan(0.1f));
                Assert.That(Vector3.Angle(movement, expected), Is.LessThan(0.1f));
                Assert.That(camera.transform.position.y, Is.EqualTo(cameraHeight).Within(0.001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.05f));
                Assert.That(camera.orthographicSize, Is.EqualTo(17));
                var stopped = camera.transform.position;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(camera.transform.position, Is.EqualTo(stopped));
            }
            orbit.RotateRight(); yield return new WaitForSecondsRealtime(0.6f);
            var afterRotation = camera.transform.position;
            var rotatedForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSecondsRealtime(0.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(Vector3.Angle(camera.transform.position - afterRotation, rotatedForward), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator PanningMovesOrbitPivotAndFourTurnsReturnToPannedPose()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSecondsRealtime(0.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var displacement = position - (center + new Vector3(14, 28, -42));
            Assert.That(displacement.magnitude, Is.GreaterThan(0.1f));
            orbit.RotateRight(); yield return new WaitForSecondsRealtime(0.6f);
            var expected = center + displacement + Quaternion.AngleAxis(90, Vector3.up) * new Vector3(14, 28, -42);
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.01f));
            for (int i = 0; i < 3; i++) { orbit.RotateRight(); yield return new WaitForSecondsRealtime(0.6f); }
            Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator ArrowKeysNoLongerRotateCamera()
        {
            var position = camera.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.RightArrow, Key.DownArrow, Key.LeftArrow));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(camera.transform.position, Is.EqualTo(position));
        }
    }
}
