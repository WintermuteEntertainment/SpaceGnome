using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public AudioClip[] audioClips;
    public AudioSource audioSource;
    public AudioSource audioSource2;
    public AudioSource audioSource3;
    public AudioSource audioSource4;

    public AudioListener audioListener;

    [SerializeField] bool musicStarted;

    private void Awake()
    {
        audioListener = audioListener.GetComponent<AudioListener>();
    }
    // 2026-11-19 (build audio "corrupted", worse as the round went on): the four layers are each a
    // 25.043 s loop (48 kHz), but 2023 started layers 2-4 at exactly 25 / 50 / 75 s with
    // PlayDelayed, so each entered 43 / 86 / 129 ms out of step with layer 1, and PlayDelayed also
    // snaps to the audio buffer (which differs between Editor and build). Now each layer is
    // scheduled on the audio clock exactly one, two and three loop-lengths after layer 1.
    [Tooltip("Seconds of lead time before the first layer starts, so all four can be scheduled precisely.")]
    [SerializeField] double scheduleLead = 0.2;

    // 2026-11-19: the crackle/static turned out to be Tim's PC audio setup (it happens on the Windows
    // volume slider too), not the game. Kept as a mix control at the original level. Note: every clip
    // imports with Normalize on, so all four layers at 0.7 peak at 1.41 and clip very briefly
    // (~160 samples per 25 s loop, on drum hits); 0.5 keeps them just under full level.
    [Tooltip("Volume applied to every music layer at start. 0.7 = the prefab's original level " +
             "(all four together clip very slightly); 0.5 keeps them under full level.")]
    [SerializeField, Range(0f, 1f)] float layerVolume = 0.7f;

    void Start()
    {
        var sources = new[] { audioSource, audioSource2, audioSource3, audioSource4 };
        foreach (var s in sources) if (s != null) s.volume = layerVolume;
        if (audioSource == null || audioSource.clip == null) { Debug.LogWarning("MusicPlayer: first layer has no clip."); return; }
        double loop = (double)audioSource.clip.samples / audioSource.clip.frequency;
        double start = AudioSettings.dspTime + scheduleLead;
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null || sources[i].clip == null) continue;
            // Compare durations, not sample counts: "Optimize Sample Rate" can import each layer at a
            // different rate (so different sample counts) while the length in seconds is identical.
            double len = (double)sources[i].clip.samples / sources[i].clip.frequency;
            if (System.Math.Abs(len - loop) > 0.002)
                Debug.LogWarning("MusicPlayer: layer " + (i + 1) + " ('" + sources[i].clip.name + "') is a different length from layer 1; it will drift.");
            sources[i].PlayScheduled(start + i * loop);
        }
        Debug.Log("MusicPlayer: layers scheduled every " + loop.ToString("0.000") + " s (loop length).");
    }

}

