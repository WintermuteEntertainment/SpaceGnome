using UnityEngine;

// Red tint on harmful objects (2026-11-18), so the damaging Asteroid reads differently from the
// rocks the gnome can land on. Fully reversible: untick this component, or remove it from
// Asteroid.prefab, and the original materials come back. Works on material instances, so the
// shared Asteroid material asset is never changed.
public class HazardTint : MonoBehaviour
{
    [Tooltip("Multiplied into the base colour (URP Lit _BaseColor, or _Color on other shaders).")]
    // 2026-11-18: first values (1, 0.35, 0.3) + glow 0.35 were far too red (Tim); now a faint warm cast.
    public Color tint = new Color(1f, 0.82f, 0.78f, 1f);
    [Tooltip("Faint red glow so it still reads in shadow. Black = no glow.")]
    [ColorUsage(false, true)] public Color glow = new Color(0.05f, 0.004f, 0f, 1f);

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColor = Shader.PropertyToID("_Color");
    static readonly int Emission = Shader.PropertyToID("_EmissionColor");

    Renderer[] renderers;
    Material[][] originals;

    void OnEnable()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originals = new Material[renderers.Length][];
        for (int r = 0; r < renderers.Length; r++)
        {
            if (renderers[r] is ParticleSystemRenderer) continue;
            originals[r] = renderers[r].sharedMaterials;
            var mats = renderers[r].materials; // instances
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (m.HasProperty(BaseColor)) m.SetColor(BaseColor, m.GetColor(BaseColor) * tint);
                else if (m.HasProperty(LegacyColor)) m.SetColor(LegacyColor, m.GetColor(LegacyColor) * tint);
                if (glow.maxColorComponent > 0f && m.HasProperty(Emission))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor(Emission, glow);
                }
            }
            renderers[r].materials = mats;
        }
    }

    void OnDisable()
    {
        if (renderers == null) return;
        for (int r = 0; r < renderers.Length; r++)
        {
            if (renderers[r] == null || originals[r] == null) continue;
            foreach (var m in renderers[r].materials) Destroy(m);
            renderers[r].sharedMaterials = originals[r];
        }
    }
}
