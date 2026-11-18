using UnityEngine;

// A streamed level rock (added by LevelObjects).
//  - Juice (2026-11-18, replaces the cyan glow, which didn't read and tinted the rock texture):
//      pop-in   : grows in with an overshoot when the gnome comes within revealDistance
//      breathing: slow, slight pulse
//      squish   : a springy wobble when he lands on it
//    All of it scales X and Z only, so the top surface never moves under him (moving ground makes
//    him lose and re-find it, and each landing resets the dance combo).
//  - Watchdog: if anything other than LevelObjects disables or destroys it, logs a warning with
//    the call stack (added while chasing "level platforms vanish when I land").
public class LevelPlatform : MonoBehaviour
{
    [Tooltip("Pops in when the gnome is this close (it is created ~450 below him, out of sight).")]
    public float revealDistance = 260f;
    public float popSeconds = 0.45f;
    [Tooltip("Breathing size swing (0.025 = +/-2.5%).")]
    public float breatheAmount = 0.025f;
    [Tooltip("Breaths per second.")]
    public float breatheSpeed = 0.35f;
    [Tooltip("Landing squish strength (0.08 = 8% wider at the peak).")]
    public float squishAmount = 0.08f;
    public float squishSeconds = 0.5f;

    [HideInInspector] public bool removalExpected; // set by LevelObjects before it removes this

    Vector3 baseScale;
    float phase, revealedAt = -1f, squishAt = -99f;
    bool wasStoodOn;

    void Start()
    {
        baseScale = transform.localScale;
        phase = Random.Range(0f, Mathf.PI * 2f);
        transform.localScale = new Vector3(0.001f, baseScale.y, 0.001f); // hidden flat until revealed
    }

    Vector3 placedAt;
    bool placedKnown, movedLogged;

    void Update()
    {
        if (!removalExpected) Juice(); // LevelObjects shrinks it away itself once removal starts

        // Watchdog: report once if something moves a level platform away from where it was placed.
        if (!placedKnown) { placedAt = transform.position; placedKnown = true; }
        else if (!movedLogged && (transform.position - placedAt).sqrMagnitude > 1f)
        {
            movedLogged = true;
            Debug.LogWarning("Level platform '" + name + "' MOVED from " + placedAt + " to " + transform.position +
                             " (parent '" + (transform.parent != null ? transform.parent.name : "none") + "').");
        }
    }

    void Juice()
    {
        var gm = Toolbox.instance != null ? Toolbox.instance.m_gnomeMovement : null;

        if (revealedAt < 0f)
        {
            if (gm == null || Mathf.Abs(gm.transform.position.y - transform.position.y) > revealDistance) return;
            revealedAt = Time.time;
        }

        bool stood = gm != null && gm.CurrentGround != null && gm.CurrentGround.transform.IsChildOf(transform);
        if (stood && !wasStoodOn) squishAt = Time.time;
        wasStoodOn = stood;

        float pop = Mathf.Clamp01((Time.time - revealedAt) / Mathf.Max(0.01f, popSeconds));
        float popScale = OutBack(pop);
        float breathe = 1f + Mathf.Sin(Time.time * breatheSpeed * Mathf.PI * 2f + phase) * breatheAmount;
        float t = (Time.time - squishAt) / Mathf.Max(0.01f, squishSeconds);
        float squish = t < 1f ? 1f + squishAmount * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) : 1f; // damped wobble

        float k = popScale * breathe * squish;
        transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z * k);
    }

    static float OutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
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

    void OnDisable()
    {
        if (!removalExpected && gameObject.scene.isLoaded && Application.isPlaying)
            Debug.LogWarning("Level platform '" + name + "' at " + transform.position + " was DISABLED unexpectedly. Caller:\n" + System.Environment.StackTrace);
    }

    void OnDestroy()
    {
        if (!removalExpected && gameObject.scene.isLoaded && Application.isPlaying)
            Debug.LogWarning("Level platform '" + name + "' at " + transform.position + " was DESTROYED unexpectedly. Caller:\n" + System.Environment.StackTrace);
    }
}
