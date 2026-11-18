using UnityEngine;

// Tumble-and-settle for space junk (2026-11-18). Story: it all fell out of Space Gnome's shuttle,
// so it drifts, spins and wobbles. Tim wanted the 2023 "bouncing around" feel back without losing
// the ability to land and dance on it (2026-11-17 made the junk kinematic and still for that).
//
// - Drifts slowly (sinks much slower than the gnome falls, ~39/s), and every few seconds gets a
//   new "bump" direction it eases into, so it looks knocked about.
// - Spins around its vertical axis and wobbles a little on the others (kept small so the top
//   stays land-able).
// - Settles while the gnome stands on it: motion and spin ease to a stop. It must be fully still
//   under him; moving ground under him makes him lose and re-find it, and each landing resets the
//   dance combo.
// - Picks up again a moment after he leaves.
//
// Added at runtime by the Spawner to every junk object, so no prefab needs editing.
public class JunkMotion : MonoBehaviour
{
    [Tooltip("Horizontal drift speed range (units/s).")]
    public Vector2 driftSpeed = new Vector2(1f, 4f);
    [Tooltip("Sinking speed range (units/s). The gnome falls ~39/s.")]
    public Vector2 sinkSpeed = new Vector2(1f, 5f);
    [Tooltip("Seconds between bumps (new drift direction).")]
    public Vector2 bumpEvery = new Vector2(2f, 5f);
    [Tooltip("Spin around the vertical axis (degrees/s, random sign).")]
    public Vector2 spinSpeed = new Vector2(10f, 60f);
    [Tooltip("Wobble angle on the tilt axes (degrees). Keep small so he can still land on top.")]
    public float wobbleDegrees = 12f;
    [Tooltip("Seconds to come to a stop once he lands on it.")]
    public float settleSeconds = 0.35f;
    [Tooltip("Seconds after he leaves before it starts moving again, and how long it takes to get going.")]
    public float resumeDelay = 1.5f, resumeSeconds = 1f;

    Rigidbody rb;
    Vector3 velocity, targetVelocity;
    float spin, nextBump, wobblePhaseX, wobblePhaseZ, wobbleRate;
    float wobbleTime;
    float activity = 1f; // 1 = full motion, 0 = settled
    float leftAt = -999f;
    bool wasStood;
    Quaternion baseRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) { rb.isKinematic = true; } // scripted motion, never gravity
        baseRotation = transform.rotation;
        spin = Random.Range(spinSpeed.x, spinSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
        wobblePhaseX = Random.Range(0f, 10f);
        wobblePhaseZ = Random.Range(0f, 10f);
        wobbleRate = Random.Range(0.4f, 0.9f);
        Bump();
        velocity = targetVelocity;
    }

    void Bump()
    {
        Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(driftSpeed.x, driftSpeed.y);
        targetVelocity = new Vector3(dir.x, -Random.Range(sinkSpeed.x, sinkSpeed.y), dir.y);
        nextBump = Time.time + Random.Range(bumpEvery.x, bumpEvery.y);
    }

    bool GnomeOnMe()
    {
        var gm = Toolbox.instance != null ? Toolbox.instance.m_gnomeMovement : null;
        if (gm == null || gm.m_floorCollider == null || !gm.m_floorCollider.isStanding) return false;
        var g = gm.CurrentGround;
        return g != null && g.transform.IsChildOf(transform);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool stood = GnomeOnMe();
        if (wasStood && !stood) leftAt = Time.time;
        wasStood = stood;

        if (stood) activity = Mathf.MoveTowards(activity, 0f, dt / Mathf.Max(0.01f, settleSeconds));
        else if (Time.time - leftAt > resumeDelay) activity = Mathf.MoveTowards(activity, 1f, dt / Mathf.Max(0.01f, resumeSeconds));
        if (stood && activity < 0.05f) activity = 0f; // fully still under him

        if (activity <= 0f) return;

        if (Time.time >= nextBump) Bump();
        velocity = Vector3.Lerp(velocity, targetVelocity, dt * 0.8f);
        wobbleTime += dt * activity;
        Quaternion yaw = Quaternion.Euler(0f, spin * dt * activity, 0f);
        baseRotation = yaw * baseRotation;
        Quaternion wobble = Quaternion.Euler(
            Mathf.Sin((wobbleTime + wobblePhaseX) * wobbleRate * 2f) * wobbleDegrees,
            0f,
            Mathf.Sin((wobbleTime + wobblePhaseZ) * wobbleRate * 1.7f) * wobbleDegrees);

        // Position stops the instant he lands (sinking even 1 unit under him would lose his
        // ground); only the spin and wobble ease out over settleSeconds.
        Vector3 pos = transform.position + (stood ? Vector3.zero : velocity * activity * dt);
        Quaternion rot = wobble * baseRotation;
        if (rb != null) { rb.MovePosition(pos); rb.MoveRotation(rot); }
        else { transform.SetPositionAndRotation(pos, rot); }
    }
}
