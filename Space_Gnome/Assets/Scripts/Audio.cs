using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Audio : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource audioSource1;
    public AudioSource audioSource2;
    public AudioSource audioSource3;
    public AudioSource audioSource4;
    [Space(10)]
    [Header("------------------------")]
    [Space(10)]
    public AudioListener audioListener;
    [Space(10)]
    [Header("------------------------")]
    [Space(10)]
    public AudioSource pointsCoin;
    public AudioSource timeCoin;
    public AudioSource hPCoin;
    [Space(10)]
    [Header("------------------------")]
    [Space(10)]
    public AudioSource landing;
    public AudioSource jumping;
    public AudioSource tookDamage;
    [Space(10)]
    [Header("------------------------")]
    [Space(10)]
    public AudioSource hardFall1;
    public AudioClip   audioClip1;
    public AudioSource hardFall2;
    public AudioClip   audioClip2;
    public AudioSource hardFall3;
    public AudioClip   audioClip3;    
    public AudioSource hardFall4;
    public AudioClip   audioClip4;    
    public AudioSource hardFall5;
    public AudioClip   audioClip5;

    // 2026-11-19: mix control for sound effects (1 = the prefab's original levels). Added while chasing
    // static that turned out to be Tim's PC audio setup; lower it with MusicPlayer.layerVolume to give
    // the mix headroom (every clip imports with Normalize on, so each effect peaks at full level).
    [Header("Mix (2026)")]
    [Tooltip("Multiplier applied once at start to every sound-effect source's own volume. 1 = unchanged.")]
    [SerializeField, Range(0f, 1f)] float sfxScale = 1f;

    void Start()
    {
        var sfx = new[] { pointsCoin, timeCoin, hPCoin, landing, jumping, tookDamage, hardFall1, hardFall2, hardFall3, hardFall4, hardFall5 };
        var done = new System.Collections.Generic.HashSet<AudioSource>();
        foreach (var s in sfx)
            if (s != null && done.Add(s)) s.volume *= sfxScale; // once per source, even if two fields share one
    }
}
