// Space Gnome recovery: move the project onto URP (Universal Render Pipeline).
//
// What it does, in order:
//   1. Creates a URP pipeline asset + Universal Renderer in Assets/Settings/URP
//      and makes it the active pipeline (Graphics settings and every Quality level).
//   2. Converts materials to "Universal Render Pipeline/Lit":
//        - materials whose shader is missing (the old HDRP Lit / HDRP Shader Graph ones),
//          reading their saved HDRP property values straight from the .mat file;
//        - built-in "Standard" and "Standard (Specular setup)" materials.
//      Everything else (skyboxes, Unlit, TextMesh Pro, UI) is left alone and listed.
//   3. Puts GYM_03 in the build scene list (the October 2023 project's list had only GYM_03).
//
// Materials are converted in place, so their GUIDs and every scene/prefab reference stay valid.
// Every decision is written to Logs/SpaceGnome_URPConversion.txt next to the project.
//
// Run from the menu: Space Gnome > Convert Project to URP
// Or headless:       -executeMethod SpaceGnomeTools.URPConversion.RunBatch

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceGnomeTools
{
    public static class URPConversion
    {
        const string SettingsFolder = "Assets/Settings/URP";
        const string PipelinePath = SettingsFolder + "/SpaceGnome_URP.asset";
        const string RendererPath = SettingsFolder + "/SpaceGnome_URP_Renderer.asset";
        const string BuildScene = "Assets/Scenes/GYM_03.unity";
        const string UrpLit = "Universal Render Pipeline/Lit";
        const string MissingShader = "Hidden/InternalErrorShader";

        static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Space Gnome/Convert Project to URP")]
        public static void RunFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Convert to URP",
                "This switches the project to URP and converts materials in place.\n\n" +
                "The project is in git, so every change can be reviewed or reverted.", "Convert", "Cancel"))
                return;
            Run();
            EditorUtility.DisplayDialog("Convert to URP", "Done. See Logs/SpaceGnome_URPConversion.txt.", "OK");
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); Log("FAILED: " + e); WriteReport(); EditorApplication.Exit(1); }
        }

        static void Run()
        {
            Report.Clear();
            Log("Space Gnome URP conversion, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Log("Unity " + Application.unityVersion);

            var pipeline = SetupPipeline();
            ConvertMaterials();
            SetBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("Pipeline in use: " + (GraphicsSettings.defaultRenderPipeline ? GraphicsSettings.defaultRenderPipeline.name : "none"));
            WriteReport();
        }

        // ---------------------------------------------------------------- pipeline

        static UniversalRenderPipelineAsset SetupPipeline()
        {
            Log("");
            Log("== Pipeline");
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets/Settings", "URP");

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(renderer);

                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, PipelinePath);
                Log("Created " + PipelinePath + " and " + RendererPath);
            }
            else
            {
                Log("Using existing " + PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
                Log("Quality level '" + QualitySettings.names[i] + "' -> URP");
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ---------------------------------------------------------------- materials

        static void ConvertMaterials()
        {
            Log("");
            Log("== Materials");
            var lit = Shader.Find(UrpLit);
            if (lit == null) throw new Exception("Shader '" + UrpLit + "' not found; is URP installed?");

            var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int converted = 0, skipped = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string shaderName = mat.shader != null ? mat.shader.name : "(null)";
                bool missing = mat.shader == null || shaderName == MissingShader;
                bool standard = shaderName == "Standard" || shaderName == "Standard (Specular setup)";

                if (!missing && !standard)
                {
                    Log("  keep   " + path + "  [" + shaderName + "]");
                    skipped++;
                    continue;
                }

                var saved = SavedProps.Read(path);
                ApplyLit(mat, lit, saved, standard, out string notes);
                EditorUtility.SetDirty(mat);
                converted++;
                Log("  URP    " + path + "  [was " + (missing ? "missing shader (HDRP era)" : shaderName) + "]" + notes);
            }
            AssetDatabase.SaveAssets();
            Log("Converted " + converted + ", left alone " + skipped);
        }

        static void ApplyLit(Material mat, Shader lit, SavedProps p, bool fromStandard, out string notes)
        {
            var n = new List<string>();
            mat.shader = lit;

            // Base colour map and tint
            var baseTex = p.FirstTexture("_BaseColorMap", "_MainTex", "_BASE_COLOR_MAP", "_ColorMap");
            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex.Texture);
                mat.SetTextureScale("_BaseMap", baseTex.Scale);
                mat.SetTextureOffset("_BaseMap", baseTex.Offset);
                n.Add("base=" + baseTex.Name);
            }
            var tint = p.FirstColor("_BaseColor", "_Color", "_BASE_COLOR");
            if (tint.HasValue) mat.SetColor("_BaseColor", tint.Value);

            // Normal map
            var normal = p.FirstTexture("_NormalMap", "_BumpMap", "_BUMP_MAP");
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal.Texture);
                mat.SetFloat("_BumpScale", p.FirstFloat("_NormalScale", "_BumpScale", "_BUMP_MAP_STRENGTH") ?? 1f);
                n.Add("normal=" + normal.Name);
            }

            // Metallic / smoothness / occlusion.
            // HDRP's mask map packs metallic (R), occlusion (G) and smoothness (A); URP Lit reads
            // metallic from R and smoothness from A of _MetallicGlossMap, and occlusion from G of
            // _OcclusionMap, so the same texture goes into both slots.
            var mask = p.FirstTexture("_MaskMap");
            var metalMap = mask ?? p.FirstTexture("_MetallicGlossMap", "_METALNESS_MAP");
            if (metalMap != null)
            {
                mat.SetTexture("_MetallicGlossMap", metalMap.Texture);
                mat.SetFloat("_Smoothness", p.FirstFloat("_SmoothnessRemapMax", "_GlossMapScale") ?? 1f);
                n.Add("metal=" + metalMap.Name);
            }
            else
            {
                mat.SetFloat("_Smoothness", p.FirstFloat("_Smoothness", "_Glossiness") ?? 0.5f);
            }
            mat.SetFloat("_Metallic", p.FirstFloat("_Metallic") ?? 0f);
            var occlusion = mask ?? p.FirstTexture("_OcclusionMap");
            if (occlusion != null)
            {
                mat.SetTexture("_OcclusionMap", occlusion.Texture);
                mat.SetFloat("_OcclusionStrength", p.FirstFloat("_OcclusionStrength") ?? 1f);
            }

            // Emission. HDRP stores the picked colour as _EmissiveColorLDR; _EmissiveColor can be
            // in physical units (very large), so it is only used when the LDR value is absent.
            var emisTex = p.FirstTexture("_EmissiveColorMap", "_EmissionMap", "_EMISSION_COLOR_MAP");
            var emisCol = p.FirstColor("_EmissiveColorLDR", "_EmissionColor") ?? p.FirstColor("_EmissiveColor");
            bool emissive = emisTex != null || (emisCol.HasValue && emisCol.Value.maxColorComponent > 0.001f);
            if (emissive)
            {
                if (emisTex != null) mat.SetTexture("_EmissionMap", emisTex.Texture);
                var c = emisCol ?? Color.white;
                if (c.maxColorComponent > 16f) c = c / c.maxColorComponent;
                mat.SetColor("_EmissionColor", c);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                n.Add("emission");
            }

            // Surface type and alpha clipping
            bool transparent, clip;
            float cutoff;
            if (fromStandard)
            {
                float mode = p.FirstFloat("_Mode") ?? 0f; // 0 opaque, 1 cutout, 2 fade, 3 transparent
                transparent = mode >= 2f;
                clip = Mathf.Approximately(mode, 1f);
                cutoff = p.FirstFloat("_Cutoff") ?? 0.5f;
            }
            else
            {
                transparent = (p.FirstFloat("_SurfaceType") ?? 0f) >= 1f;
                clip = (p.FirstFloat("_AlphaCutoffEnable") ?? 0f) >= 1f;
                cutoff = p.FirstFloat("_AlphaCutoff", "_Cutoff") ?? 0.5f;
            }
            mat.SetFloat("_Surface", transparent ? 1f : 0f);
            mat.SetFloat("_Blend", 0f); // alpha blend
            mat.SetFloat("_AlphaClip", clip ? 1f : 0f);
            mat.SetFloat("_Cutoff", cutoff);
            if (transparent) n.Add("transparent");
            if (clip) n.Add("cutout " + cutoff.ToString("0.##", CultureInfo.InvariantCulture));

            // Double-sided (HDRP) -> cull off
            bool twoSided = (p.FirstFloat("_DoubleSidedEnable") ?? 0f) >= 1f;
            mat.SetFloat("_Cull", twoSided ? 0f : 2f);
            if (twoSided) n.Add("double-sided");

            mat.renderQueue = -1;
            BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords);

            foreach (var miss in p.UnresolvedTextures) n.Add("UNRESOLVED texture " + miss);
            notes = n.Count > 0 ? "  (" + string.Join(", ", n) + ")" : "";
        }

        // ---------------------------------------------------------------- build scenes

        static void SetBuildScenes()
        {
            Log("");
            Log("== Build scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildScene) == null)
            {
                Log("  " + BuildScene + " not found, build list left as is");
                return;
            }
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == BuildScene))
            {
                Log("  " + BuildScene + " already listed");
                return;
            }
            list.Insert(0, new EditorBuildSettingsScene(BuildScene, true));
            EditorBuildSettings.scenes = list.ToArray();
            Log("  added " + BuildScene);
        }

        // ---------------------------------------------------------------- logging

        static void Log(string line)
        {
            Report.AppendLine(line);
            Debug.Log("[SpaceGnome URP] " + line);
        }

        static void WriteReport()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SpaceGnome_URPConversion.txt", Report.ToString());
        }

        // ---------------------------------------------------------------- saved .mat values

        // Reads the property values serialized in a .mat file. A material whose shader is missing
        // still carries these on disk, which is the only reliable place to read the HDRP values.
        class SavedProps
        {
            public class Tex { public string Name; public Texture Texture; public Vector2 Scale; public Vector2 Offset; }

            readonly Dictionary<string, Tex> textures = new Dictionary<string, Tex>();
            readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
            public readonly List<string> UnresolvedTextures = new List<string>();

            static readonly Regex TexRx = new Regex(
                @"- (?<n>\w+):\s*\r?\n\s*m_Texture: \{fileID: (?<f>-?\d+)(?:, guid: (?<g>[0-9a-f]{32}), type: \d+)?\}\s*\r?\n\s*m_Scale: \{x: (?<sx>[-\d.eE+]+), y: (?<sy>[-\d.eE+]+)\}\s*\r?\n\s*m_Offset: \{x: (?<ox>[-\d.eE+]+), y: (?<oy>[-\d.eE+]+)\}");
            static readonly Regex FloatRx = new Regex(@"^\s*- (?<n>\w+): (?<v>[-\d.eE+]+)\s*$", RegexOptions.Multiline);
            static readonly Regex ColorRx = new Regex(
                @"^\s*- (?<n>\w+): \{r: (?<r>[-\d.eE+]+), g: (?<g>[-\d.eE+]+), b: (?<b>[-\d.eE+]+), a: (?<a>[-\d.eE+]+)\}", RegexOptions.Multiline);

            static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

            public static SavedProps Read(string assetPath)
            {
                var p = new SavedProps();
                string text = File.ReadAllText(assetPath);

                int texStart = text.IndexOf("m_TexEnvs:", StringComparison.Ordinal);
                int floatStart = text.IndexOf("m_Floats:", StringComparison.Ordinal);
                int colorStart = text.IndexOf("m_Colors:", StringComparison.Ordinal);
                string texBlock = Slice(text, texStart, floatStart, colorStart);
                string floatBlock = Slice(text, floatStart, colorStart, texStart);
                string colorBlock = Slice(text, colorStart, texStart, floatStart);

                foreach (Match m in TexRx.Matches(texBlock))
                {
                    var g = m.Groups["g"].Value;
                    if (string.IsNullOrEmpty(g) || m.Groups["f"].Value == "0") continue;
                    var texPath = AssetDatabase.GUIDToAssetPath(g);
                    var tex = string.IsNullOrEmpty(texPath) ? null : AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                    if (tex == null) { p.UnresolvedTextures.Add(m.Groups["n"].Value + " " + g); continue; }
                    p.textures[m.Groups["n"].Value] = new Tex
                    {
                        Name = Path.GetFileName(texPath),
                        Texture = tex,
                        Scale = new Vector2(F(m.Groups["sx"].Value), F(m.Groups["sy"].Value)),
                        Offset = new Vector2(F(m.Groups["ox"].Value), F(m.Groups["oy"].Value)),
                    };
                }
                foreach (Match m in FloatRx.Matches(floatBlock))
                    p.floats[m.Groups["n"].Value] = F(m.Groups["v"].Value);
                foreach (Match m in ColorRx.Matches(colorBlock))
                    p.colors[m.Groups["n"].Value] = new Color(F(m.Groups["r"].Value), F(m.Groups["g"].Value), F(m.Groups["b"].Value), F(m.Groups["a"].Value));
                return p;
            }

            // Text from `start` up to the nearest of the other section starts after it.
            static string Slice(string text, int start, int a, int b)
            {
                if (start < 0) return "";
                int end = text.Length;
                if (a > start) end = Math.Min(end, a);
                if (b > start) end = Math.Min(end, b);
                return text.Substring(start, end - start);
            }

            public Tex FirstTexture(params string[] names)
            {
                foreach (var n in names) if (textures.TryGetValue(n, out var t)) return t;
                return null;
            }
            public float? FirstFloat(params string[] names)
            {
                foreach (var n in names) if (floats.TryGetValue(n, out var v)) return v;
                return null;
            }
            public Color? FirstColor(params string[] names)
            {
                foreach (var n in names) if (colors.TryGetValue(n, out var c)) return c;
                return null;
            }
        }
    }
}
