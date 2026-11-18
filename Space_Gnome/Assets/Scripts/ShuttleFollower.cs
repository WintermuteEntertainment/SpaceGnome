using UnityEngine;

// Keeps the wounded space shuttle below the gnome as he falls (2026-11-18), so the junk looks
// like it is spilling out of it. Put this on the shuttle model in the scene (NOT under the
// Spawner FirePoint: the Spawner moves that every frame to a random spot 66-99 below the gnome).
//
// Place the shuttle where you want it relative to the gnome in the scene; that offset is kept.
// Height is locked to the gnome (he falls ~39/s, so any lag would let him catch it); sideways it
// follows smoothly, so it drifts a little when he moves.
public class ShuttleFollower : MonoBehaviour
{
    [Tooltip("Empty = the gnome (Toolbox m_gnomeMovement).")]
    public Transform target;
    [Tooltip("Use the offset it has from the gnome when Play starts. Untick to use 'offset' below.")]
    public bool keepSceneOffset = true;
    public Vector3 offset = new Vector3(0f, -80f, 0f);
    [Tooltip("Seconds to catch up sideways (0 = rigid).")]
    public float sideSmoothing = 0.6f;
    [Tooltip("Slow tumble, degrees per second on each axis (0 = none). For later: the 'wounded' look.")]
    public Vector3 spin = Vector3.zero;

    Vector3 sideVelocity;

    void Start()
    {
        if (target == null && Toolbox.instance.m_gnomeMovement != null) target = Toolbox.instance.m_gnomeMovement.transform;
        // 2026-11-18: Target was set to the Player 3 root, which never moves (only its child GNOME 1
        // falls), so the shuttle stood still. Follow the object that actually has GnomeMovement.
        if (target != null)
        {
            var gm = target.GetComponentInChildren<GnomeMovement>(true);
            if (gm != null && gm.transform != target)
            {
                Debug.Log("ShuttleFollower: target '" + target.name + "' doesn't move; following '" + gm.name + "' inside it.");
                target = gm.transform;
            }
        }
        if (target != null && keepSceneOffset) offset = transform.position - target.position;
        Debug.Log("ShuttleFollower: following '" + (target != null ? target.name : "nothing") + "' at offset " + offset + ".");
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = target.position + offset;
        Vector3 pos = transform.position;
        Vector3 side = Vector3.SmoothDamp(new Vector3(pos.x, 0f, pos.z), new Vector3(goal.x, 0f, goal.z), ref sideVelocity, sideSmoothing);
        transform.position = new Vector3(side.x, goal.y, side.z);
        if (spin != Vector3.zero) transform.Rotate(spin * Time.deltaTime, Space.World);
    }
}
