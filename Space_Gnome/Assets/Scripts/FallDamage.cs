using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallDamage : MonoBehaviour
{
    public float height;
    public float landingHeight;
    float fallDistance;

    int fallDamage;
    [SerializeField] int fallDamageThreshHoldMinimum;
    [SerializeField] int fallDamageThreshHold1;
    [SerializeField] int fallDamageThreshHold2;
    [SerializeField] int fallDamageThreshHold3;
    [SerializeField] int fallDamageThreshHold4;
    
    [SerializeField] int fallDamage1;
    [SerializeField] int fallDamage2;
    [SerializeField] int fallDamage3;
    [SerializeField] int fallDamage4;
    [SerializeField] int fallDamage5;

    [SerializeField] int boostPenalty1;
    [SerializeField] int boostPenalty2;
    [SerializeField] int boostPenalty3;
    [SerializeField] int boostPenalty4;
    [SerializeField] int boostPenalty5;

    // 2026 rework. Fall distance = highest point since the gnome last stood (or since the game
    // started) minus where he lands. GnomeMovement calls OnLeftGround / OnLanded when he really
    // leaves or lands on a platform. The 2023 version used physics contacts: "height" started at 0
    // so the first landing counted as a fall from y = 0, and a contact while falling THROUGH a
    // platform still charged damage.
    bool airborne = true;

    // For the HUD (2026-11-18): distance fallen so far since he last stood, and where damage starts.
    public bool Airborne => airborne;
    public float CurrentFall => airborne ? Mathf.Max(0f, height - transform.position.y) : 0f;
    public float SafeFall => fallDamageThreshHoldMinimum;
    public float LastFall => fallDistance;

    private void Start()
    {
        height = transform.position.y;
    }

    private void Update()
    {
        if (airborne && transform.position.y > height) height = transform.position.y; // track the peak
    }

    public void OnLeftGround(float y)
    {
        airborne = true;
        height = y;
    }

    public void OnLanded(float y)
    {
        if (!airborne) return;
        airborne = false;
        landingHeight = y;
        fallDistance = height - landingHeight;
        Debug.Log("Landed. Fell " + fallDistance.ToString("0.0") + " (from " + height.ToString("0.0") + " to " + landingHeight.ToString("0.0") + ").");
        FallDamageHPDrain();
    }
    public void FallDamageHPDrain()
    {
            if (fallDistance < fallDamageThreshHoldMinimum)
            {
                fallDamage = 0;                             //Set fall damage based on fall distance.
                Debug.Log("Took 0 fall damage.");
            }
            else if (fallDistance > fallDamageThreshHoldMinimum && fallDistance < fallDamageThreshHold1)
            {
                fallDamage = fallDamage1; 
                Toolbox.instance.m_coins.hP -= fallDamage1;
                Toolbox.instance.m_playerManager.boost -= boostPenalty1;
                //Play sounds and PE and subtract boost penalty and points loss.
                Debug.Log("Took fallDamage1");
               Toolbox.instance.m_audio.hardFall1.PlayOneShot(Toolbox.instance.m_audio.audioClip1);
            }
            else if (fallDistance > fallDamageThreshHold1 && fallDistance < fallDamageThreshHold2)
            {
                fallDamage = fallDamage2;
                Toolbox.instance.m_coins.hP -= fallDamage2;
                Toolbox.instance.m_playerManager.boost -= boostPenalty2;
                Debug.Log("Took fallDamage2");
                Toolbox.instance.m_audio.hardFall2.PlayOneShot(Toolbox.instance.m_audio.audioClip2);
            }
            else if (fallDistance > fallDamageThreshHold2 && fallDistance < fallDamageThreshHold3)
            {
                fallDamage = fallDamage3;
                Toolbox.instance.m_coins.hP -= fallDamage3;
                Toolbox.instance.m_playerManager.boost -= boostPenalty3;
                Debug.Log("Took fallDamage3");
            }
            else if (fallDistance > fallDamageThreshHold3 && fallDistance < fallDamageThreshHold4)
            {
                fallDamage = fallDamage4;
                Toolbox.instance.m_coins.hP -= fallDamage4;
                Toolbox.instance.m_playerManager.boost -= boostPenalty4;
                Debug.Log("Took fallDamage4");
            }
            else if (fallDistance > fallDamageThreshHold4)
            {
                fallDamage = fallDamage5;
                Toolbox.instance.m_coins.hP -= fallDamage5;
                Toolbox.instance.m_playerManager.boost -= boostPenalty5;
                Debug.Log("Took fallDamage5");
            }
    }
}
