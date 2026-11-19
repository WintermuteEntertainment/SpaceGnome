using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Asteroid : MonoBehaviour
{
    public Transform rotationCenter;
    public GnomeMovement m_gnomeMovement;
    [Tooltip("2023 value: degrees around the Earth PER FRAME. Now applied per second at 60 fps " +
             "(rotationIncrements x 60), so the speed no longer depends on the frame rate.")]
    [SerializeField] float rotationIncrements;
    [SerializeField] GameObject earth;
    [SerializeField] private float asteroidFallSpeed;

    // 2026-11-19 (performance): each asteroid was a DYNAMIC Rigidbody with gravity and no drag, so it
    // kept accelerating forever, AND this script teleported it every frame, which forces a physics
    // update per asteroid per frame. Its movement is entirely scripted, so the Rigidbody is now
    // kinematic with gravity off. It still hits the gnome (his Rigidbody is dynamic), so the damage
    // in Asteroids.cs is unchanged.
    [Header("Falling (2026-11-19)")]
    [Tooltip("Downward acceleration (units/s per second). 9.81 = Unity's default gravity, what they had as physics objects.")]
    [SerializeField] float gravity = 9.81f;
    [Tooltip("Top falling speed (units/s). The gnome falls ~39/s, so above that they pull away below him. " +
             "As physics objects they had no limit and kept speeding up forever.")]
    [SerializeField] float maxFallSpeed = 55f;
    float fallVelocity;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void Update()
    {
        if (earth == null || m_gnomeMovement == null || rotationCenter == null) return;
        float fall = m_gnomeMovement.fallSpeed * m_gnomeMovement.gravity;
        // 2023 axis was fall * dt * rotationCenter.position: the direction of rotationCenter's
        // position (flipped when fall is negative). dt only scaled its length, which RotateAround ignores.
        Vector3 axis = rotationCenter.position * Mathf.Sign(fall);
        if (axis.sqrMagnitude > 0.0001f)
            transform.RotateAround(earth.transform.position, axis, rotationIncrements * 60f * Time.deltaTime);
        transform.Translate(asteroidFallSpeed * fall * Time.deltaTime * Vector3.down);

        // Gravity, scripted (Tim: they just floated once they stopped being physics objects).
        fallVelocity = Mathf.Min(fallVelocity + gravity * Time.deltaTime, maxFallSpeed);
        transform.position += Vector3.down * (fallVelocity * Time.deltaTime);
    }
}
