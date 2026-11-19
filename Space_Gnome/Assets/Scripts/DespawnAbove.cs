using UnityEngine;

// Removes a Spawner-made object (junk, coin, asteroid) once the gnome has fallen well past it
// (2026-11-19). The Spawner never removed anything (its limits are 1,000,000), so every piece of
// junk, every coin and every asteroid stayed for the whole round, each still moving and doing
// physics, and the game got heavier the longer you played. Same rule as the level rocks
// (LevelObjects.removeAbove): gone once it's this far above him, never while he's standing on it.
public class DespawnAbove : MonoBehaviour
{
    [Tooltip("Removed once it is this many units above the gnome.")]
    public float removeAbove = 250f;

    void Update()
    {
        var gm = Toolbox.instance != null ? Toolbox.instance.m_gnomeMovement : null;
        if (gm == null) return;
        if (transform.position.y - gm.transform.position.y <= removeAbove) return;
        if (gm.CurrentGround != null && gm.CurrentGround.transform.IsChildOf(transform)) return; // he's on it
        Destroy(gameObject);
    }
}
