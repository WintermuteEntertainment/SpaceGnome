using UnityEngine;

// Marks a player-spawned dance floor (2026-11-18), as opposed to a level asteroid platform
// (LevelObjects / LevelPlatform). Rectangular, BoxCollider, tag Floor. Holds per-floor
// settings so each kind (cardboard, coloured squares, ice block, carpet...) can differ later.
public class DanceFloor : MonoBehaviour
{
    [Tooltip("Shown in logs, e.g. 'Cardboard', 'Carpet'.")]
    public string kind = "Placeholder";
}
