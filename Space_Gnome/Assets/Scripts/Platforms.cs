using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
[System.Serializable]

// Player-spawned platforms (FirePlatform: X / P).
// 2026: fixed spawning (2023 moved the prefab asset instead of the new platform), added a boost
// cost, a spawn effect (pop-in + particle burst) and cleanup of old platforms.
public class Platforms : MonoBehaviour
{
    [SerializeField] GameObject player;

    [SerializeField] GameObject platformPrefab;

    [SerializeField] Transform platformObjectsParent;

    public GameObject[] spawnablePlatforms;

    GameObject clone;

   public SGInput player1Controls;

    [SerializeField] float platformSpawnDistance;

    [SerializeField] Vector3 platformSpawnOffset;

    [Header("Placement (2026)")]
    [Tooltip("A spawned platform appears this far below the gnome (its top, roughly). " +
             "Replaces the 2023 maths, which measured from PlatformFirePoint (273 below the gnome) " +
             "and so put platforms ~300+ units down.")]
    [SerializeField] float spawnBelowGnome = 30f;

    // Everything is measured from the gnome himself: this component sits on GNOME 1. The 'player'
    // field points at PlatformFirePoint, a child 273 units BELOW the gnome; measuring level
    // platforms from it removed each one just as he reached it (2026-11-17 bug).
    Transform Gnome => transform;

    [Header("Cost (2026)")]
    [Tooltip("Boost spent per platform. Start boost is 10000 and each basic dance move gives 1000, " +
             "so 1500 = about 6 platforms from a full start, or one per 1.5 dance moves after that.")]
    [SerializeField] float boostCost = 1500f;
    [Tooltip("Allow spawning while standing on a platform (normally only in the air, to break a fall).")]
    [SerializeField] bool allowWhileStanding = false;

    [Header("Cleanup (2026)")]
    [Tooltip("At most this many player platforms exist; the oldest goes first.")]
    [SerializeField] int maxPlatforms = 5;
    [Tooltip("A platform older than this is removed (0 = never by age).")]
    [SerializeField] float lifetimeSeconds = 30f;
    [Tooltip("Seconds a removed platform takes to shrink away.")]
    [SerializeField] float despawnSeconds = 0.3f;

    [Header("Spawn effect (2026)")]
    [Tooltip("Seconds the platform takes to pop in to full size.")]
    [SerializeField] float popInSeconds = 0.35f;
    [Tooltip("Optional particle prefab to play at the platform. Empty = a simple built-in burst.")]
    [SerializeField] GameObject spawnEffectPrefab;
    [SerializeField] Color burstColor = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField] int burstParticles = 90;

    readonly List<GameObject> spawned = new List<GameObject>();
    readonly Dictionary<GameObject, float> spawnTime = new Dictionary<GameObject, float>();
    static Material burstMaterial;

    [Header("Static platforms (2026)")]
    [Tooltip("Stream world-fixed platforms below the gnome as he falls, so running out of boost is survivable.")]
    [SerializeField] bool streamStaticPlatforms = true;
    [Tooltip("Vertical distance between layers (randomised +/-15%). Fall damage starts at 150, so ~140 keeps " +
             "hopping down from one static platform to the next damage-free.")]
    [SerializeField] float layerSpacing = 140f;
    [Tooltip("Layers are created this far below the gnome (about 11 s of falling at normal speed).")]
    [SerializeField] float lookAhead = 450f;
    [Tooltip("Static platforms this far above the gnome are removed.")]
    [SerializeField] float removeAbove = 250f;
    [Tooltip("Extra horizontal distance beyond 'just clear of his path' (min, max). Never placed directly under him.")]
    [SerializeField] Vector2 sideOffset = new Vector2(2f, 35f);
    [Tooltip("Size relative to the platform prefab (player-spawned ones are 1).")]
    [SerializeField] float staticScale = 0.6f;
    [Tooltip("Chance a layer gets a second platform.")]
    [SerializeField, Range(0f, 1f)] float secondPlatformChance = 0.35f;

    readonly List<GameObject> staticPlatforms = new List<GameObject>();
    float nextLayerY;
    bool streamStarted;

    private void Awake()
    {

        clone = null;
        player1Controls = new SGInput();
        player1Controls.Player.FirePlatform.performed += ctx => Fire();
    }

    public void Fire()
    {
        if (spawnablePlatforms == null || spawnablePlatforms.Length == 0) return;
        var prefab = spawnablePlatforms[Random.Range(0, spawnablePlatforms.Length)];
        if (prefab == null) return;

        var gm = Toolbox.instance.m_gnomeMovement;
        if (!allowWhileStanding && gm != null && gm.m_floorCollider != null && gm.m_floorCollider.isStanding) return;

        var pm = Toolbox.instance.m_playerManager;
        if (pm != null)
        {
            if (pm.boost < boostCost)
            {
                Debug.Log("Not enough boost for a platform (" + pm.boost.ToString("0") + " / " + boostCost + ").");
                pm.FlashBoostWarning(); // so a refused press is visible, not silent
                return;
            }
            pm.boost -= boostCost;
        }

        // spawnBelowGnome under the gnome, facing his way.
        Vector3 at = Gnome.position + Vector3.down * spawnBelowGnome;
        clone = Instantiate(prefab, at, Quaternion.Euler(0f, Gnome.eulerAngles.y, 0f), platformObjectsParent);
        clone.SetActive(true);
        StripStrayPlatformsComponents(clone);
        Debug.Log("Spawned platform at " + clone.transform.position + " for " + boostCost + " boost.");

        PopIn(clone);
        Burst(clone);

        spawned.Add(clone);
        spawnTime[clone] = Time.time;
        Cleanup();
    }

    private void Update()
    {
        if (spawned.Count > 0) Cleanup();
        if (streamStaticPlatforms) StreamStaticPlatforms();
    }

    // Keep layers of world-fixed platforms in a band below the gnome, wherever he has drifted.
    void StreamStaticPlatforms()
    {
        if (spawnablePlatforms == null || spawnablePlatforms.Length == 0) return;
        float py = Gnome.position.y;
        if (!streamStarted) { nextLayerY = py - layerSpacing; streamStarted = true; }

        int guard = 0; // never more than a handful per frame, even after a long nose dive
        while (nextLayerY > py - lookAhead && guard++ < 8)
        {
            SpawnStaticLayer(nextLayerY);
            nextLayerY -= layerSpacing * Random.Range(0.85f, 1.15f);
        }
        if (nextLayerY > py) nextLayerY = py - layerSpacing; // he outran the stream (e.g. boosted up)

        var ground = Toolbox.instance.m_gnomeMovement != null ? Toolbox.instance.m_gnomeMovement.CurrentGround : null;
        for (int i = staticPlatforms.Count - 1; i >= 0; i--)
        {
            var p = staticPlatforms[i];
            if (p == null) { staticPlatforms.RemoveAt(i); continue; }
            if (p.transform.position.y - py <= removeAbove) continue;
            if (ground != null && ground.transform.IsChildOf(p.transform)) continue;
            staticPlatforms.RemoveAt(i);
            Despawn(p);
        }
    }

    // Every Platform prefab carries a stray, unconfigured copy of this script (Platform 1 has two).
    // Each copy creates and enables its own SGInput (all gamepad bindings) in Awake/OnEnable, so
    // streaming dozens of platforms meant dozens of extra input copies; the gamepad stopped
    // responding when level platforms arrived. Remove them from every new platform.
    static void StripStrayPlatformsComponents(GameObject p)
    {
        foreach (var extra in p.GetComponentsInChildren<Platforms>(true))
        {
            extra.enabled = false;   // OnDisable now: disables its input copy immediately
            Destroy(extra);
        }
    }

    void SpawnStaticLayer(float y)
    {
        int count = Random.value < secondPlatformChance ? 2 : 1;
        float firstAngle = Random.Range(0f, 360f);
        for (int n = 0; n < count; n++)
        {
            var prefab = spawnablePlatforms[Random.Range(0, spawnablePlatforms.Length)];
            if (prefab == null) continue;
            var p = Instantiate(prefab, platformObjectsParent);
            p.name = prefab.name + " (static)";
            p.SetActive(true);
            p.transform.localScale = prefab.transform.localScale * staticScale;
            p.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // Just clear of his straight-down path: half the platform's width plus his body and a margin.
            float halfWidth = 5f;
            var rend = p.GetComponentInChildren<Renderer>();
            if (rend != null) halfWidth = Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.z);
            float clear = halfWidth + 2f;

            float angle = (firstAngle + n * Random.Range(120f, 240f)) * Mathf.Deg2Rad; // second one on another side
            float dist = clear + Random.Range(sideOffset.x, sideOffset.y);
            Vector3 c = Gnome.position;
            p.transform.position = new Vector3(c.x + Mathf.Cos(angle) * dist, y, c.z + Mathf.Sin(angle) * dist);
            StripStrayPlatformsComponents(p);
            p.AddComponent<LevelPlatform>(); // glow + watchdog
            staticPlatforms.Add(p);
        }
    }

    // Remove platforms beyond maxPlatforms (oldest first) or older than lifetimeSeconds,
    // but never the one the gnome is standing on.
    void Cleanup()
    {
        spawned.RemoveAll(p => p == null);
        var ground = Toolbox.instance.m_gnomeMovement != null ? Toolbox.instance.m_gnomeMovement.CurrentGround : null;

        for (int i = 0; i < spawned.Count; i++)
        {
            var p = spawned[i];
            bool tooMany = spawned.Count > maxPlatforms;
            bool tooOld = lifetimeSeconds > 0f && spawnTime.TryGetValue(p, out float born) && Time.time - born > lifetimeSeconds;
            if (!tooMany && !tooOld) continue;
            if (ground != null && ground.transform.IsChildOf(p.transform)) continue; // he's standing on it

            spawned.RemoveAt(i);
            spawnTime.Remove(p);
            Debug.Log("Removed platform '" + p.name + "' (" + (tooMany ? "over the limit of " + maxPlatforms : "older than " + lifetimeSeconds + " s") + ").");
            Despawn(p);
            i--;
        }
    }

    void Despawn(GameObject p)
    {
        var level = p.GetComponent<LevelPlatform>();
        if (level != null) level.removalExpected = true;
        foreach (var c in p.GetComponentsInChildren<Collider>()) c.enabled = false; // nothing lands on a vanishing platform
        p.transform.DOKill();
        p.transform.DOScale(Vector3.zero, despawnSeconds).SetEase(Ease.InBack).OnComplete(() => { if (p != null) Destroy(p); });
    }

    void PopIn(GameObject p)
    {
        Vector3 full = p.transform.localScale;
        p.transform.localScale = full * 0.01f;
        p.transform.DOScale(full, popInSeconds).SetEase(Ease.OutBack);
    }

    void Burst(GameObject p)
    {
        Vector3 pos = p.transform.position;
        if (spawnEffectPrefab != null)
        {
            var fx = Instantiate(spawnEffectPrefab, pos, Quaternion.identity);
            Destroy(fx, 5f);
            return;
        }

        // Simple ring burst sized to the platform. Uses the always-available Sprites/Default shader,
        // so it renders in URP and survives builds. Swap in a real effect via spawnEffectPrefab.
        float radius = 5f;
        var rend = p.GetComponentInChildren<Renderer>();
        if (rend != null) radius = Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.z);

        var go = new GameObject("PlatformSpawnBurst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.3f, radius * 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.03f, radius * 0.08f);
        main.startColor = burstColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = burstParticles * 2;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstParticles) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.15f;
        shape.rotation = new Vector3(90f, 0f, 0f); // circle lies flat, like the platform

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        if (burstMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) burstMaterial = new Material(shader);
        }
        var psr = go.GetComponent<ParticleSystemRenderer>();
        if (burstMaterial != null) psr.material = burstMaterial;

        ps.Play();
        Destroy(go, 3f);
    }

    //private void Update()
    //{
    //    if (player1Controls.Player.Fire.triggered)
    //    {
    //        Fire();
    //    }
    //}                                                     //Possibly cause of double platform spawning!
    void OnEnable()
    {
        player1Controls.Player.Enable();
    }
    void OnDisable()
    {
        player1Controls.Player.Disable();
    }
    void OnDestroy()
    {
        player1Controls?.Dispose(); // free the input copy (stray copies on platforms included)
    }
}
