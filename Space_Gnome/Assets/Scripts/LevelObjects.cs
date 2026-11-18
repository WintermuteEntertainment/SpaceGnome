using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// In-level asteroid platforms (2026-11-18): world-fixed rocks streamed in layers below the gnome
// as he falls, so running out of boost is survivable. Split out of Platforms.cs, which now only
// handles the dance floors the player spawns. These use their own prefab list (Tim's asteroid
// platforms: Platform, Platform 1-4, with convex MeshColliders and their own scale).
//
// Sits on GNOME 1 next to Platforms; everything is measured from the gnome himself.
public class LevelObjects : MonoBehaviour
{
    [Tooltip("Asteroid platform prefabs streamed into the level. Needs tag Floor and an enabled collider to be land-able.")]
    public GameObject[] levelPrefabs;

    [Tooltip("Vertical distance between layers (randomised +/-15%). Fall damage starts at 150, so ~140 keeps " +
             "hopping down from one rock to the next damage-free.")]
    [SerializeField] float layerSpacing = 140f;
    [Tooltip("Layers are created this far below the gnome (about 11 s of falling at normal speed).")]
    [SerializeField] float lookAhead = 450f;
    [Tooltip("Rocks this far above the gnome are removed.")]
    [SerializeField] float removeAbove = 250f;
    [Tooltip("Extra horizontal distance beyond 'just clear of his path' (min, max). Never placed directly under him.")]
    [SerializeField] Vector2 sideOffset = new Vector2(2f, 35f);
    [Tooltip("Size relative to the prefab. 1 = the prefab's own scale.")]
    [SerializeField] float scale = 1f;
    [Tooltip("Chance a layer gets a second rock.")]
    [SerializeField, Range(0f, 1f)] float secondChance = 0.35f;
    [Tooltip("Random spin around the vertical axis only, so the top stays land-able.")]
    [SerializeField] bool randomYaw = true;
    [Tooltip("Seconds a removed rock takes to shrink away.")]
    [SerializeField] float despawnSeconds = 0.3f;

    readonly List<GameObject> live = new List<GameObject>();
    readonly HashSet<GameObject> warned = new HashSet<GameObject>();
    Transform parent;
    float nextLayerY;
    bool started;

    Transform Gnome => transform;

    void Awake()
    {
        // Own runtime parent at scene root, so the rocks never move with the gnome or a spawner.
        parent = new GameObject("LevelObjects (runtime)").transform;
    }

    void Update()
    {
        if (levelPrefabs == null || levelPrefabs.Length == 0) return;
        float py = Gnome.position.y;
        if (!started) { nextLayerY = py - layerSpacing; started = true; }

        int guard = 0; // never more than a handful per frame, even after a long nose dive
        while (nextLayerY > py - lookAhead && guard++ < 8)
        {
            SpawnLayer(nextLayerY);
            nextLayerY -= layerSpacing * Random.Range(0.85f, 1.15f);
        }
        if (nextLayerY > py) nextLayerY = py - layerSpacing; // he outran the stream (e.g. boosted up)

        var ground = Toolbox.instance.m_gnomeMovement != null ? Toolbox.instance.m_gnomeMovement.CurrentGround : null;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i];
            if (p == null) { live.RemoveAt(i); continue; }
            if (p.transform.position.y - py <= removeAbove) continue;
            if (ground != null && ground.transform.IsChildOf(p.transform)) continue;
            live.RemoveAt(i);
            Despawn(p);
        }
    }

    void SpawnLayer(float y)
    {
        int count = Random.value < secondChance ? 2 : 1;
        float firstAngle = Random.Range(0f, 360f);
        for (int n = 0; n < count; n++)
        {
            var prefab = levelPrefabs[Random.Range(0, levelPrefabs.Length)];
            if (prefab == null) continue;
            WarnIfNotLandable(prefab);

            var p = Instantiate(prefab, parent);
            p.name = prefab.name + " (level)";
            p.SetActive(true);
            p.transform.localScale = prefab.transform.localScale * scale;
            if (randomYaw) p.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * prefab.transform.rotation;

            // Just clear of his straight-down path: half the rock's width plus his body and a margin.
            float halfWidth = 5f;
            var rend = p.GetComponentInChildren<Renderer>();
            if (rend != null) halfWidth = Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.z);
            float clear = halfWidth + 2f;

            float angle = (firstAngle + n * Random.Range(120f, 240f)) * Mathf.Deg2Rad; // second one on another side
            float dist = clear + Random.Range(sideOffset.x, sideOffset.y);
            Vector3 c = Gnome.position;
            p.transform.position = new Vector3(c.x + Mathf.Cos(angle) * dist, y, c.z + Mathf.Sin(angle) * dist);

            Platforms.StripStrayPlatformsComponents(p);
            if (p.GetComponent<LevelPlatform>() == null) p.AddComponent<LevelPlatform>(); // glow + watchdog
            live.Add(p);
        }
    }

    // Once per prefab: say so if a rock can't be landed on (no enabled collider, or not tagged Floor).
    void WarnIfNotLandable(GameObject prefab)
    {
        if (warned.Contains(prefab)) return;
        warned.Add(prefab);
        bool hasCollider = false;
        foreach (var c in prefab.GetComponentsInChildren<Collider>(true))
            if (c.enabled && !c.isTrigger) { hasCollider = true; break; }
        if (!hasCollider)
            Debug.LogWarning("Level prefab '" + prefab.name + "' has no enabled collider, so the gnome falls through it.", prefab);
        if (!prefab.CompareTag("Floor"))
            Debug.LogWarning("Level prefab '" + prefab.name + "' is tagged '" + prefab.tag + "', not Floor, so the gnome can't stand on it.", prefab);
    }

    void Despawn(GameObject p)
    {
        var level = p.GetComponent<LevelPlatform>();
        if (level != null) level.removalExpected = true;
        foreach (var c in p.GetComponentsInChildren<Collider>()) c.enabled = false;
        p.transform.DOKill();
        p.transform.DOScale(Vector3.zero, despawnSeconds).SetEase(Ease.InBack).OnComplete(() => { if (p != null) Destroy(p); });
    }

    void OnDestroy()
    {
        if (parent != null) Destroy(parent.gameObject);
    }
}
