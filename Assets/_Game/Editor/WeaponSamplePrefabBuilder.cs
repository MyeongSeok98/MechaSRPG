using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MechaXCOM.Editor
{
    public static class WeaponSamplePrefabBuilder
    {
        public const string RiflePath = "Assets/_Game/Prefabs/Weapons/Rifles/BeamRifle.prefab";
        public const string MechaPath = "Assets/_Game/Prefabs/Mecha/Samples/MechaReferenceWithRifle.prefab";
        private const string MaterialFolder = "Assets/_Game/Materials/WeaponSamples";

        public static void InspectModels()
        {
            foreach (var path in new[] { "Assets/_Game/Art/Models/Weapons/Rifles/beam_rifle.obj",
                "Assets/_Game/Art/Models/Mecha/Sample/mecha_reference.obj" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var item in asset.GetComponentsInChildren<Transform>())
                {
                    var renderer = item.GetComponent<Renderer>();
                    Debug.Log(path + " / " + item.name + " / " + (renderer ? renderer.bounds.ToString() : "no renderer"));
                    var filter = item.GetComponent<MeshFilter>();
                    if (filter) Debug.Log("Imported first vertex: " + filter.sharedMesh.vertices[0]);
                }
            }
        }

        [MenuItem("MechaXCOM/Create Rifle And Mecha Sample Prefabs")]
        public static void Build()
        {
            if (File.Exists(RiflePath) || File.Exists(MechaPath))
                throw new InvalidOperationException("Sample prefabs already exist; edit them in Prefab Mode. They will not be overwritten.");
            EnsureFolder(Path.GetDirectoryName(RiflePath).Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(MechaPath).Replace('\\', '/'));
            EnsureFolder(MaterialFolder);
            var temporary = EditorSceneManager.NewPreviewScene();
            try
            {
                var rifle = new GameObject("BeamRifle");
                SceneManager.MoveGameObjectToScene(rifle, temporary);
                var rifleModel = LoadModel("Assets/_Game/Art/Models/Weapons/Rifles/beam_rifle.obj", rifle.transform, "Rifle");
                var grip = SourcePoint(rifleModel.transform, FeatureBounds("Assets/_Game/Art/Models/Weapons/Rifles/beam_rifle.obj", "Main_grip").center);
                rifleModel.transform.position -= grip;
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(rifle.transform, false);
                var muzzleBounds = FeatureBounds("Assets/_Game/Art/Models/Weapons/Rifles/beam_rifle.obj", "Muzzle_front_rim");
                muzzle.position = SourcePoint(rifleModel.transform,
                    new Vector3(muzzleBounds.center.x, muzzleBounds.min.y, muzzleBounds.center.z));
                var rifleAsset = PrefabUtility.SaveAsPrefabAsset(rifle, RiflePath);
                UnityEngine.Object.DestroyImmediate(rifle);

                var mecha = new GameObject("MechaReferenceWithRifle");
                SceneManager.MoveGameObjectToScene(mecha, temporary);
                var mechaModel = LoadModel("Assets/_Game/Art/Models/Mecha/Sample/mecha_reference.obj", mecha.transform, "Mecha");
                var socket = new GameObject("RightHandWeaponSocket").transform;
                socket.SetParent(mechaModel.transform, false);
                socket.position = SourcePoint(mechaModel.transform,
                    FeatureBounds("Assets/_Game/Art/Models/Mecha/Sample/mecha_reference.obj", "R_fist_core").center);
                socket.rotation = Quaternion.identity;
                var heldRifle = (GameObject)PrefabUtility.InstantiatePrefab(rifleAsset, socket);
                heldRifle.transform.localPosition = Vector3.zero;
                heldRifle.transform.localRotation = Quaternion.identity;
                heldRifle.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(mecha, MechaPath);
                UnityEngine.Object.DestroyImmediate(mecha);
                AssetDatabase.SaveAssets();
                Debug.Log("Created BeamRifle and MechaReferenceWithRifle; source models and battle scenes unchanged.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(temporary);
            }
        }

        private static GameObject LoadModel(string path, Transform parent, string materialPrefix)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new InvalidOperationException("Missing model: " + path);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            model.name = "Model";
            // Both authored OBJ files use Z up and -Y forward. Normalize to Unity Y up, +Z forward.
            model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    string name = original ? original.name : "Default";
                    string materialPath = MaterialFolder + "/" + materialPrefix + "_" + name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (!material)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (!shader) throw new InvalidOperationException("URP Lit shader unavailable.");
                        material = new Material(shader) { name = materialPrefix + "_" + name };
                        material.SetColor("_BaseColor", original ? original.color : Color.gray);
                        material.SetFloat("_Smoothness", .25f);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    return material;
                }).ToArray();
            }
            return model;
        }

        private static Vector3 SourcePoint(Transform model, Vector3 objPoint) =>
            model.TransformPoint(new Vector3(-objPoint.x, objPoint.y, objPoint.z));

        private static Bounds FeatureBounds(string path, string name)
        {
            bool inObject = false, found = false;
            var bounds = new Bounds();
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("o ", StringComparison.Ordinal)) inObject = line.Substring(2).Trim() == name;
                if (!inObject || !line.StartsWith("v ", StringComparison.Ordinal)) continue;
                var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var vertex = new Vector3(float.Parse(fields[1], CultureInfo.InvariantCulture),
                    float.Parse(fields[2], CultureInfo.InvariantCulture), float.Parse(fields[3], CultureInfo.InvariantCulture));
                if (!found) { bounds = new Bounds(vertex, Vector3.zero); found = true; }
                else bounds.Encapsulate(vertex);
            }
            if (!found) throw new InvalidOperationException("Source OBJ feature not found: " + name);
            return bounds;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        // Batch-only preview: never opens or saves an authored scene.
        public static void RenderPreviews()
        {
            RenderPreview(MechaPath, "Artifacts/MechaReferenceWithRifle.png");
            RenderPreview(RiflePath, "Artifacts/BeamRifle.png");
        }
        internal static void RenderPreview(string path, string output)
        {
            var temporary = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            var oldTarget = RenderTexture.active;
            bool oldAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.5f, .5f, .5f);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), temporary);
                var renderers = model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
                foreach (var renderer in renderers)
                    Debug.Log("Preview materials: " + path + " " + string.Join(", ", renderer.sharedMaterials.Select(material =>
                        material.name + "=" + material.GetColor("_BaseColor"))));
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var light = new GameObject("Preview Light").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, temporary);
                light.type = LightType.Directional; light.intensity = 2;
                light.transform.rotation = Quaternion.Euler(35, -35, 0);
                var camera = new GameObject("Preview Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, temporary);
                camera.scene = temporary;
                camera.orthographic = true; camera.orthographicSize = bounds.size.magnitude * .55f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.13f, .16f, .2f);
                camera.transform.position = bounds.center + new Vector3(-3, 1.5f, 4) * bounds.size.magnitude;
                camera.transform.LookAt(bounds.center);
                target = new RenderTexture(960, 960, 24); camera.targetTexture = target;
                // The first batch render initializes URP's material buffers; capture the warmed render.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(960, 960, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 960), 0, 0); pixels.Apply();
                File.WriteAllBytes(output, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldTarget;
                ShaderUtil.allowAsyncCompilation = oldAsyncCompilation;
                if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(temporary);
            }
        }
    }
}
