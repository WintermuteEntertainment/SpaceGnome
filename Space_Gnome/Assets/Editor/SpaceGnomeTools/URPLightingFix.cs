// Space Gnome recovery: bring HDRP-era lighting into URP's range.
//
// HDRP lights use physical units (the GYM_03 suns are 100,000 lux) and HDRP lit the scene from a
// sky volume. URP expects intensities around 1 and lights the scene from the skybox material.
//
// Menu: Space Gnome > Fix Lighting for URP (open scene + prefabs)
//   1. Lights above 8 (HDRP scale) in the open scene(s) and in every prefab under Assets are
//      divided by 100,000, the GYM_03 sun's value, so the brightest light becomes 1 and the
//      others keep their HDRP brightness relative to it.
//   2. Skybox = SpaceSkies "Pink_4K_Resolution" (the Skybox_1 set the HDRP sky profile used),
//      ambient light from the skybox, and cameras clear to the skybox.
// Everything it changes is listed in Logs/SpaceGnome_LightingFix.txt.

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceGnomeTools
{
    public static class URPLightingFix
    {
        const float HdrpThreshold = 8f;
        const float HdrpSunLux = 100000f;
        const string SkyboxPath = "Assets/SpaceSkies Free/Skybox_1/Pink_4K_Resolution.mat";

        static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Space Gnome/Fix Lighting for URP (open scene + prefabs)")]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog("Fix lighting for URP",
                "Rescales HDRP-scale light intensities (open scenes and prefabs) and sets the Pink space skybox.\n\n" +
                "The open scene and changed prefabs are saved. Everything is in git.", "Fix", "Cancel"))
                return;

            Report.Clear();
            Log("Space Gnome lighting fix, " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            Log("");
            Log("== Lights in open scenes");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                        if (Rescale(light, scene.name)) EditorSceneManager.MarkSceneDirty(scene);
            }

            Log("");
            Log("== Lights in prefabs");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponentsInChildren<Light>(true).Length == 0) continue;

                var contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var light in contents.GetComponentsInChildren<Light>(true))
                    changed |= Rescale(light, path);
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Log("");
            Log("== Sky and ambient");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (sky == null)
            {
                Log("  skybox material not found at " + SkyboxPath + "; sky left as is");
            }
            else
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
                DynamicGI.UpdateEnvironment();
                Log("  skybox = " + SkyboxPath + ", ambient from skybox");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneAt(i));
            }

            Log("");
            Log("== Cameras in open scenes");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (cam.clearFlags != CameraClearFlags.Skybox)
                        {
                            Undo.RecordObject(cam, "Camera clears to skybox");
                            Log("  " + Path(cam.transform) + ": clear " + cam.clearFlags + " -> Skybox");
                            cam.clearFlags = CameraClearFlags.Skybox;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                            EditorSceneManager.MarkSceneDirty(scene);
                        }
                        else Log("  " + Path(cam.transform) + ": already clears to skybox");
                    }
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SpaceGnome_LightingFix.txt", Report.ToString());
            EditorUtility.DisplayDialog("Fix lighting for URP", "Done. See Logs/SpaceGnome_LightingFix.txt.", "OK");
        }

        static bool Rescale(Light light, string where)
        {
            if (light.intensity <= HdrpThreshold)
            {
                Log("  keep   " + where + " / " + Path(light.transform) + "  " + light.type + " " + light.intensity);
                return false;
            }
            float before = light.intensity;
            Undo.RecordObject(light, "Rescale HDRP light");
            light.intensity = before / HdrpSunLux * 1f;
            if (light.intensity < 0.001f) light.intensity = 0.001f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            Log("  scale  " + where + " / " + Path(light.transform) + "  " + light.type + " " + before + " -> " + light.intensity.ToString("0.####"));
            return true;
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        static void Log(string line)
        {
            Report.AppendLine(line);
            Debug.Log("[SpaceGnome Lighting] " + line);
        }
    }
}
