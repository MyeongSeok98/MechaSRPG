using System;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Editor
{
    public static class PlayerMechaVisualSetup
    {
        private const string PlayerPath = "Assets/_Game/Prefabs/BattleUnit.prefab";

        [MenuItem("MechaXCOM/Apply Player Mecha Visual")]
        public static void Apply()
        {
            var mecha = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponSamplePrefabBuilder.MechaPath);
            if (!mecha) throw new InvalidOperationException("Missing mecha sample prefab.");
            var player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var visual = player.transform.Find("Visual");
                if (visual) throw new InvalidOperationException("Player Visual already exists; edit it in Prefab Mode.");
                visual = new GameObject("Visual").transform;
                visual.SetParent(player.transform, false);
                // BattleUnit's root is at collider height; the model origin is at its feet.
                visual.localPosition = Vector3.down * player.GetComponent<BattleUnit>().HeightOffset;
                PrefabUtility.InstantiatePrefab(mecha, visual);
                player.GetComponent<MeshRenderer>().enabled = false;
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        public static void RenderPreview() =>
            WeaponSamplePrefabBuilder.RenderPreview(PlayerPath, "Artifacts/PlayerMecha.png");
    }
}
