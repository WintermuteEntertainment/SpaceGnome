using UnityEngine;

// Pivot for the landed camera (vCam1 follows this, not the gnome).
// Sits on the gnome every frame. Its heading (yaw):
//   - standing still (idle or dancing): turned by the right stick, so the camera orbits the gnome
//     and you can see him from any side; the gnome does not turn (GnomeMovement.InOrbitMode).
//   - walking / running / airborne: eases back to the gnome's own heading, so the camera settles
//     behind him again.
// Added and wired by Space Gnome > Set Up Cameras.
public class CameraOrbit : MonoBehaviour
{
    public GnomeMovement gnome;
    public Transform target;                  // point on the gnome to sit on (CameraLookAtTarget)

    [Tooltip("Orbit speed at full right-stick deflection (degrees/s).")]
    public float orbitSpeedDegrees = 150f;
    [Tooltip("How fast the camera swings back behind him once he moves (degrees/s).")]
    public float returnSpeedDegrees = 240f;
    [Tooltip("Right-stick deflection ignored before orbiting starts (0-1).")]
    public float deadZone = 0.2f;

    float yaw;

    public float Yaw => yaw;

    void Start()
    {
        if (gnome != null) yaw = gnome.transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (gnome == null) return;
        Transform anchor = target != null ? target : gnome.transform;
        transform.position = anchor.position;

        if (gnome.InOrbitMode)
        {
            float x = gnome.look.x;
            if (Mathf.Abs(x) > deadZone) yaw += x * orbitSpeedDegrees * Time.deltaTime;
        }
        else
        {
            yaw = Mathf.MoveTowardsAngle(yaw, gnome.transform.eulerAngles.y, returnSpeedDegrees * Time.deltaTime);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
