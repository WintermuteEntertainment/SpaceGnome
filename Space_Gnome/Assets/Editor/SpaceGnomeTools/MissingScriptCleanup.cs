// Space Gnome: remove "missing script" component slots.
//
// They are leftovers from packages removed in 2023 (ProBuilder components on the Spawner boxes'
// Cube / Wall / Floor objects, HDRP camera/light data). They do nothing, flood the Console with
// "The referenced script ... is missing!" every time Play starts, and Unity refuses to save a
// prefab that contains one (2026-11-17: "Error while saving Prefab 'SpawnerParent 2'", which
// blocked Make Space Junk Landable from updating the Spawner lists).
//
// Cleans every prefab under Assets/Prefabs and the open scene(s), then saves. Everything is in
// git, so the removed slots can be recovered if ever needed.
//
// Menu: Space Gnome > Remove Missing Scripts

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceGnomeTools
{
    public static class MissingScriptCleanup
    {
        [MenuItem("Space Gnome/Remove Missing Scripts")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Remove missing scripts", "Stop Play mode first.", "OK"); return; }
            var report = new StringBuilder();
            int prefabsChanged = 0, total = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = Strip(root);
                    if (n > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabsChanged++;
                        total += n;
                        report.AppendLine(path + ": removed " + n);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                int n = 0;
                foreach (var go in scene.GetRootGameObjects()) n += Strip(go);
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.AppendLine("Scene " + scene.name + ": removed " + n);
                    total += n;
                }
            }

            AssetDatabase.SaveAssets();
            string summary = "Removed " + total + " missing-script slots (" + prefabsChanged + " prefabs).\n\n" + report;
            Debug.Log("[SpaceGnome Cleanup] " + summary);
            EditorUtility.DisplayDialog("Remove missing scripts", summary, "OK");
        }

        static int Strip(GameObject root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (count == 0) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject) && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)
                    && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) != null)
                    continue; // lives in a nested prefab: cleaned when that prefab itself is processed
                Undo.RegisterCompleteObjectUndo(t.gameObject, "Remove missing scripts");
                n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            return n;
        }
    }
}
