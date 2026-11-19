using UnityEngine;

// Speed lines (2026-11-18, Tim: make falling feel faster, nose dive included).
// Thin streaks are emitted below and around the gnome and drift upward, so he falls past them.
// Everything scales with his MEASURED fall speed (so a nose dive, ~2.5x, automatically gives more,
// longer, faster lines); during a dive they also turn brighter. None while standing, rising or
// slow. Added at runtime by UI_Manager, which keeps it out of the prefabs. Sprites/Default shader:
// renders in URP and is always included in builds.
public class SpeedLines : MonoBehaviour
{
    [Tooltip("No lines below this fall speed (units/s). Normal fall is ~39.")]
    public float minSpeed = 15f;
    [Tooltip("Lines per second per unit of fall speed above minSpeed.")]
    public float ratePerSpeed = 2.2f;
    [Tooltip("Extra emission multiplier while nose diving.")]
    public float diveRateMultiplier = 1.6f;
    [Tooltip("Upward drift as a fraction of fall speed (adds to the rush past him).")]
    public float upwardFraction = 0.5f;
    [Tooltip("Ring around his fall path where lines appear (min, max radius).")]
    public Vector2 radius = new Vector2(6f, 45f);
    [Tooltip("How far below him they appear (min, max).")]
    public Vector2 below = new Vector2(40f, 140f);
    public float lifetime = 2.5f;
    public float width = 0.12f;
    [Tooltip("Streak length per unit of particle speed.")]
    public float lengthPerSpeed = 0.15f;
    public Color color = new Color(0.8f, 0.92f, 1f, 0.35f);
    public Color diveColor = new Color(1f, 1f, 1f, 0.6f);

    ParticleSystem ps;
    float lastY, speed, owed;
    bool haveLastY;

    void Start()
    {
        var go = new GameObject("SpeedLines (runtime)");
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;
        var emission = ps.emission;
        emission.enabled = false; // emitted by hand below
        var shape = ps.shape;
        shape.enabled = false;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = lengthPerSpeed;
        r.lengthScale = 1f;
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) r.material = new Material(shader);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
    }

    void LateUpdate()
    {
        if (ps == null || Toolbox.instance == null) return;
        var ui = Toolbox.instance.m_uIManager;
        var gm = Toolbox.instance.m_gnomeMovement;
        bool playing = ui != null && ui.State == UI_Manager.MenuState.Playing && gm != null && gm.isActiveAndEnabled;
        if (!playing) { haveLastY = false; speed = 0f; return; }

        float y = gm.transform.position.y;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float raw = haveLastY ? Mathf.Max(0f, (lastY - y) / dt) : 0f;
        speed = Mathf.Lerp(speed, raw, Mathf.Clamp01(dt / 0.15f));
        lastY = y;
        haveLastY = true;

        bool standing = gm.m_floorCollider != null && gm.m_floorCollider.isStanding;
        if (standing || speed < minSpeed) { owed = 0f; return; }

        float rate = (speed - minSpeed) * ratePerSpeed * (gm.IsDiving ? diveRateMultiplier : 1f);
        owed += rate * dt;
        int n = Mathf.Min((int)owed, 60);
        owed -= n;

        var ep = new ParticleSystem.EmitParams();
        Color c = gm.IsDiving ? diveColor : color;
        Vector3 c0 = gm.transform.position;
        for (int i = 0; i < n; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(radius.x, radius.y);
            ep.position = c0 + new Vector3(dir.x, -Random.Range(below.x, below.y), dir.y);
            ep.velocity = Vector3.up * speed * upwardFraction * Random.Range(0.8f, 1.2f);
            ep.startSize = width * Random.Range(0.7f, 1.3f);
            ep.startColor = c;
            ep.startLifetime = lifetime;
            ps.Emit(ep, 1);
        }
    }

    void OnDestroy()
    {
        if (ps != null) Destroy(ps.gameObject);
    }
}
