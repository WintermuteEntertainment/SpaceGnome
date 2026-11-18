// Space Gnome: make every piece of space junk the Spawner drops land-able (Tim, 2026-11-17).
//
// Checked before writing this (prefab + variant overrides):
//   already land-able (Floor tag + solid collider): Cactus, Mushroom_02, Pylon, Tire Variant,
//     Donut Variant, PocketWatch
//   CopCar Variant: solid colliders, no Floor tag            -> tag Floor
//   Palm Tree:      no collider, no tag (gnome passed through) -> tag Floor + box collider
//   Rocks2.fbx:     spawner list entry was the raw model      -> new prefab with tag + collider,
//                   swapped into the Spawner lists in the open scene
// Re-running is safe.
//
// Menu: Space Gnome > Make Space Junk Landable

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceGnomeTools
{
    public static class JunkLandableFix
    {
        const string FloorTag = "Floor";
        static readonly string[] Prefabs =
        {
            "Assets/Prefabs/Environment/Obstacles/CopCar Variant.prefab",
            "Assets/Prefabs/Environment/Obstacles/Palm Tree.prefab",
        };
        // Every junk prefab the Spawner drops. Their Rigidbodies had gravity on, so each one fell
        // away under the gnome (the log showed him "re-landing" on a Donut every 0.3 s, a few
        // units lower each time). Kinematic = stays where it was spawned, still collides.
        static readonly string[] AllJunk =
        {
            "Assets/Prefabs/Environment/Obstacles/Cactus.prefab",
            "Assets/Prefabs/Environment/Obstacles/Mushroom_02.prefab",
            "Assets/Prefabs/Environment/Obstacles/Pylon.prefab",
            "Assets/Prefabs/Environment/Obstacles/Tire Variant.prefab",
            "Assets/Prefabs/Environment/Obstacles/Donut Variant.prefab",
            "Assets/Prefabs/Environment/Obstacles/PocketWatch.prefab",
            "Assets/Prefabs/Environment/Obstacles/CopCar Variant.prefab",
            "Assets/Prefabs/Environment/Obstacles/Palm Tree.prefab",
        };
        const string RocksModel = "Assets/Models/Rocks/Rocks2.fbx";
        const string RocksPrefab = "Assets/Prefabs/Environment/Obstacles/Rocks2 (landable).prefab";

        [MenuItem("Space Gnome/Make Space Junk Landable")]
        public static void Run()
        {
            var report = new StringBuilder();

            foreach (var path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { report.AppendLine("Not found: " + path); continue; }
                try
                {
                    MakeLandable(root, report, path);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            foreach (var path in AllJunk)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { report.AppendLine("Not found: " + path); continue; }
                try
                {
                    if (Freeze(root, report, path)) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // Rocks2: build a land-able prefab from the raw model.
            GameObject rocksPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RocksPrefab);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RocksModel);
            if (rocksPrefab == null && model != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                MakeLandable(inst, report, RocksModel);
                rocksPrefab = PrefabUtility.SaveAsPrefabAsset(inst, RocksPrefab);
                Object.DestroyImmediate(inst);
                report.AppendLine("Created " + RocksPrefab);
            }

            // Swap the raw model for the prefab in every Spawner list in the open scene(s).
            int swapped = 0;
            if (rocksPrefab != null && model != null)
            {
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var scene = SceneManager.GetSceneAt(s);
                    foreach (var go in scene.GetRootGameObjects())
                        foreach (var sp in go.GetComponentsInChildren<Spawner>(true))
                        {
                            if (sp.objects == null) continue;
                            bool changed = false;
                            for (int i = 0; i < sp.objects.Length; i++)
                                if (sp.objects[i] == model) { Undo.RecordObject(sp, "Landable rocks"); sp.objects[i] = rocksPrefab; changed = true; swapped++; }
                            if (changed) { PrefabUtility.RecordPrefabInstancePropertyModifications(sp); EditorSceneManager.MarkSceneDirty(scene); }
                        }
                    if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                }
            }
            report.AppendLine("Spawner entries switched from Rocks2.fbx to the land-able prefab: " + swapped);

            AssetDatabase.SaveAssets();
            Debug.Log("[SpaceGnome Junk]\n" + report);
            EditorUtility.DisplayDialog("Make space junk landable", report.ToString(), "OK");
        }

        // Kinematic, no gravity: the junk stays where it spawned.
        static bool Freeze(GameObject root, StringBuilder report, string label)
        {
            bool changed = false;
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb.isKinematic && !rb.useGravity) continue;
                rb.isKinematic = true;
                rb.useGravity = false;
                changed = true;
            }
            report.AppendLine(label + (changed ? ": now stays put (kinematic, no gravity)" : ": already stays put"));
            return changed;
        }

        static void MakeLandable(GameObject root, StringBuilder report, string label)
        {
            if (root.tag != FloorTag) { root.tag = FloorTag; report.AppendLine(label + ": tag -> Floor"); }

            bool hasSolid = false;
            foreach (var c in root.GetComponents<Collider>()) if (!c.isTrigger && c.enabled) hasSolid = true;
            if (hasSolid) { report.AppendLine(label + ": already has a solid collider on the root"); return; }

            // Box around all its renderers, in the root's local space.
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { report.AppendLine(label + ": no renderers, no collider added"); return; }
            Bounds world = renderers[0].bounds;
            foreach (var r in renderers) world.Encapsulate(r.bounds);
            var box = root.AddComponent<BoxCollider>();
            Vector3 ls = root.transform.lossyScale;
            box.center = root.transform.InverseTransformPoint(world.center);
            box.size = new Vector3(world.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                                   world.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                                   world.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            report.AppendLine(label + ": added box collider " + box.size.ToString("0.00"));
        }
    }
}
