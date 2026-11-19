using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

// Hitch logger (2026-11-19). Tim: audio crackle in Editor AND build, plus performance hiccups.
// Crackle that follows the game (not the output device) usually means frame spikes long enough to
// starve the audio. This watches frame times and, when a frame is much slower than normal, logs
// ONE line naming the engine systems that took the time that frame, e.g.
//   HITCH 142 ms (normal 7 ms): Physics.Simulate 96.1 | Mesh.Bake Scaled Mesh PhysX CollisionData 88.0 | ...
// Uses Unity's built-in profiler markers (no Profiler window needed; works in the Editor and in
// Development builds). Added at runtime by UI_Manager. Remove once the cause is found.
public class HitchLogger : MonoBehaviour
{
    [Tooltip("A frame counts as a hitch when it is at least this many ms...")]
    public float minHitchMs = 33f;
    [Tooltip("...and at least this many times the recent normal frame time.")]
    public float hitchFactor = 2.5f;
    [Tooltip("At most one hitch line per this many seconds (so the log itself can't cause hitches).")]
    public float logCooldown = 0.5f;
    public int topEntries = 6;

    // Markers matched by substring against everything the profiler offers.
    static readonly string[] Wanted =
    {
        "Physics.Simulate", "Physics.Processing", "Physics.SyncColliderTransform", "Physics.SyncRigidbodyTransform",
        "Bake Scaled Mesh", "Bake Mesh", "Physics.BakeCollisionMeshes",
        "GC.Collect", // (not GC.Alloc: it fires per allocation, and recording it would add overhead)
        "ScriptRunBehaviourUpdate", "ScriptRunBehaviourLateUpdate", "ScriptRunBehaviourFixedUpdate", "ScriptRunDelayedDynamicFrameRate",
        "Animators.Update", "Director.ProcessFrame",
        "ParticleSystem.Update", "ParticleSystem.EndUpdateAll",
        "Instantiate", "Loading.ReadObject", "Canvas.SendWillRenderCanvases", "UIEvents.WillRenderCanvases",
        "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForGfxCommandsFromMainThread", "WaitForTargetFPS",
        "DebugLogHandler", "Debug.Log", "Logging", "StackTraceUtility",
        "Audio.Update", "AudioManager.Update",
    };

    readonly List<(string name, ProfilerRecorder rec)> recorders = new List<(string, ProfilerRecorder)>();
    ProfilerRecorder gcAllocInFrame;
    float normalMs = 16f, lastLog = -99f;
    int frames;

    void OnEnable()
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        var seen = new HashSet<string>();
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (seen.Contains(d.Name) || !Wanted.Any(w => d.Name.Contains(w))) continue;
            seen.Add(d.Name);
            recorders.Add((d.Name, new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default)));
        }
        gcAllocInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        Debug.Log("HitchLogger: watching " + recorders.Count + " markers (" + string.Join(", ", recorders.Select(r => r.name).Take(40)) + ").");
    }

    void OnDisable()
    {
        foreach (var r in recorders) r.rec.Dispose();
        recorders.Clear();
        gcAllocInFrame.Dispose();
    }

    void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        frames++;
        if (frames < 30) { normalMs = Mathf.Lerp(normalMs, ms, 0.2f); return; } // settle after load

        bool hitch = ms >= minHitchMs && ms >= normalMs * hitchFactor;
        if (!hitch) { normalMs = Mathf.Lerp(normalMs, ms, 0.02f); return; }
        if (Time.unscaledTime - lastLog < logCooldown) return;
        lastLog = Time.unscaledTime;

        // Each recorder holds the PREVIOUS frame's total (ns) for its marker: the frame that just took `ms`.
        var top = recorders
            .Select(r => (r.name, ms: r.rec.Valid && r.rec.Count > 0 ? r.rec.LastValue / 1e6 : 0.0))
            .Where(x => x.ms >= 0.5)
            .OrderByDescending(x => x.ms)
            .Take(topEntries);
        var sb = new StringBuilder();
        sb.Append("HITCH ").Append(ms.ToString("0")).Append(" ms (normal ").Append(normalMs.ToString("0.0")).Append(" ms, t ")
          .Append(Time.time.ToString("0.0")).Append("): ");
        sb.Append(string.Join(" | ", top.Select(x => x.name + " " + x.ms.ToString("0.0"))));
        if (gcAllocInFrame.Valid) sb.Append(" | GC alloc ").Append((gcAllocInFrame.LastValue / 1024f).ToString("0")).Append(" KB");
        var gm = Toolbox.instance != null ? Toolbox.instance.m_gnomeMovement : null;
        if (gm != null)
            sb.Append(" | gnome ").Append(gm.m_floorCollider != null && gm.m_floorCollider.isStanding ? "standing" : "airborne")
              .Append(gm.CurrentGround != null ? " on " + gm.CurrentGround.name : "");
        Debug.LogWarning(sb.ToString());
    }
}
