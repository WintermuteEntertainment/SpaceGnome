using UnityEngine;

// Marks a streamed "level" (static) platform, added by Platforms.SpawnStaticLayer.
//  - Glow: a soft pulsing emission so level platforms read differently from player-spawned ones.
//  - Watchdog: if anything other than the platform streamer disables or destroys it, logs a
//    warning with the call stack (added while chasing "level platforms vanish when I land").
public class LevelPlatform : MonoBehaviour
{
    public Color glowColor = new Color(0.35f, 0.85f, 1f);
    [Tooltip("Emission strength at the middle of the pulse.")]
    public float glowIntensity = 0.6f;
    [Tooltip("How far the glow swings either side of glowIntensity.")]
    public float pulseAmount = 0.25f;
    [Tooltip("Pulses per second.")]
    public float pulseSpeed = 0.5f;

    [HideInInspector] public bool removalExpected; // set by Platforms before it removes this

    Material[] materials;
    float phase;

    void Start()
    {
        phase = Random.Range(0f, Mathf.PI * 2f);
        var renderers = GetComponentsInChildren<Renderer>();
        var list = new System.Collections.Generic.List<Material>();
        foreach (var r in renderers)
            foreach (var m in r.materials) // instances, so the shared platform materials are not changed
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                list.Add(m);
            }
        materials = list.ToArray();
        ApplyGlow();
    }

    Vector3 placedAt;
    bool placedKnown, movedLogged;

    void Update()
    {
        ApplyGlow();

        // Watchdog: report once if something moves a level platform away from where it was placed.
        if (!placedKnown) { placedAt = transform.position; placedKnown = true; }
        else if (!movedLogged && (transform.position - placedAt).sqrMagnitude > 1f)
        {
            movedLogged = true;
            Debug.LogWarning("Level platform '" + name + "' MOVED from " + placedAt + " to " + transform.position +
                             " (parent '" + (transform.parent != null ? transform.parent.name : "none") + "').");
        }
    }

    // Watchdog: physical contact with the gnome, with heights, to tell "landed on top" from "passed into it".
    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        var col = GetComponent<Collider>();
        float top = col != null ? col.bounds.max.y : transform.position.y;
        Debug.Log("Gnome touched level platform '" + name + "': gnome y " + collision.transform.position.y.ToString("0.00") +
                  ", platform top y " + top.ToString("0.00") + ", tag '" + tag + "', colliders " + GetComponents<Collider>().Length + ".");
    }

    void ApplyGlow()
    {
        if (materials == null) return;
        float k = glowIntensity + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f + phase) * pulseAmount;
        Color c = glowColor * Mathf.Max(0f, k);
        foreach (var m in materials) if (m != null) m.SetColor("_EmissionColor", c);
    }

    void OnDisable()
    {
        if (!removalExpected && gameObject.scene.isLoaded && Application.isPlaying)
            Debug.LogWarning("Level platform '" + name + "' at " + transform.position + " was DISABLED unexpectedly. Caller:\n" + System.Environment.StackTrace);
    }

    void OnDestroy()
    {
        if (materials != null) foreach (var m in materials) if (m != null) Destroy(m);
        if (!removalExpected && gameObject.scene.isLoaded && Application.isPlaying)
            Debug.LogWarning("Level platform '" + name + "' at " + transform.position + " was DESTROYED unexpectedly. Caller:\n" + System.Environment.StackTrace);
    }
}
