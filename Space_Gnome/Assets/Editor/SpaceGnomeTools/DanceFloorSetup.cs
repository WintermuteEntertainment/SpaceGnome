// Space Gnome: separate player-spawned dance floors from the level asteroid platforms (2026-11-18).
//
// 1. Creates a placeholder dance floor (rectangular cube, BoxCollider, tag Floor, DanceFloor
//    component) at Assets/Prefabs/DanceFloors/DanceFloor_Placeholder.prefab, with its own simple
//    URP Lit material. Skipped if it already exists (never overwritten).
// 2. In Player 3.prefab, on GNOME 1 (the object with GnomeMovement + Platforms):
//    - adds LevelObjects if missing and fills its list from the old shared Platforms list
//      (Platform, Platform 1-4 = Tim's asteroid platforms), only if its list is empty;
//    - sets Platforms.danceFloorPrefabs to the placeholder, only if that list is empty.
// 3. Adds HazardTint to Asteroid.prefab (the harmful one) if missing. To undo the tint, untick
//    or remove that component.
//
// Menu: Space Gnome > Set Up Dance Floors + Level Rocks

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpaceGnomeTools
{
    public static class DanceFloorSetup
    {
        const string Folder = "Assets/Prefabs/DanceFloors";
        const string FloorPath = Folder + "/DanceFloor_Placeholder.prefab";
        const string MatPath = Folder + "/DanceFloor_Placeholder.mat";
        const string PlayerPath = "Assets/Prefabs/Player 3.prefab";
        const string AsteroidPath = "Assets/Prefabs/Asteroid.prefab";

        [MenuItem("Space Gnome/Set Up Dance Floors + Level Rocks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Dance floors", "Stop Play mode first.", "OK"); return; }
            var report = new StringBuilder();

            var floor = MakePlaceholder(report);
            SetUpPlayer(floor, report);
            TintAsteroid(report);

            AssetDatabase.SaveAssets();
            Debug.Log("[SpaceGnome DanceFloors] " + report);
            EditorUtility.DisplayDialog("Dance floors + level rocks", report.ToString(), "OK");
        }

        static GameObject MakePlaceholder(StringBuilder report)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPath);
            if (existing != null) { report.AppendLine("Placeholder dance floor already exists, left as is."); return existing; }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "DanceFloors");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.85f, 0.25f, 0.75f, 1f)); // loud magenta: obviously a placeholder
                mat.SetFloat("_Smoothness", 0.6f);
                AssetDatabase.CreateAsset(mat, MatPath);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // comes with a BoxCollider
            go.name = "DanceFloor_Placeholder";
            go.tag = "Floor";
            go.transform.localScale = new Vector3(14f, 1.2f, 10f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<DanceFloor>().kind = "Placeholder";

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, FloorPath);
            Object.DestroyImmediate(go);
            report.AppendLine("Created " + FloorPath + " (14 x 1.2 x 10, BoxCollider, tag Floor).");
            return prefab;
        }

        static void SetUpPlayer(GameObject floor, StringBuilder report)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var platforms = root.GetComponentsInChildren<Platforms>(true)
                                    .Where(p => p.GetComponent<GnomeMovement>() != null).ToList();
                if (platforms.Count != 1)
                {
                    report.AppendLine("Player 3: expected 1 Platforms on the gnome, found " + platforms.Count + ". Nothing changed there.");
                    return;
                }
                var p = platforms[0];
                bool changed = false;

                var level = p.GetComponent<LevelObjects>();
                if (level == null) { level = p.gameObject.AddComponent<LevelObjects>(); changed = true; report.AppendLine("Player 3: added LevelObjects to " + p.name + "."); }
                if (level.levelPrefabs == null || level.levelPrefabs.Length == 0)
                {
                    var rocks = (p.spawnablePlatforms ?? new GameObject[0]).Where(x => x != null).ToArray();
                    level.levelPrefabs = rocks;
                    changed = true;
                    report.AppendLine("Player 3: level rocks = " + string.Join(", ", rocks.Select(x => x.name)) + ".");
                    foreach (var r in rocks)
                    {
                        bool col = r.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger);
                        if (!col) report.AppendLine("   WARNING: " + r.name + " has no enabled collider (gnome falls through it).");
                    }
                }
                else report.AppendLine("Player 3: LevelObjects list already set, left as is.");

                if (p.danceFloorPrefabs == null || p.danceFloorPrefabs.Length == 0)
                {
                    p.danceFloorPrefabs = new[] { floor };
                    changed = true;
                    report.AppendLine("Player 3: dance floors = " + floor.name + ".");
                }
                else report.AppendLine("Player 3: dance floor list already set, left as is.");

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void TintAsteroid(StringBuilder report)
        {
            var root = PrefabUtility.LoadPrefabContents(AsteroidPath);
            try
            {
                if (root.GetComponent<HazardTint>() != null) { report.AppendLine("Asteroid: HazardTint already there."); return; }
                root.AddComponent<HazardTint>();
                PrefabUtility.SaveAsPrefabAsset(root, AsteroidPath);
                report.AppendLine("Asteroid: added HazardTint (red). Untick or remove it to undo.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
