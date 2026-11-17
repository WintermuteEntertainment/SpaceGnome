// Space Gnome: slow the game down for players who did not make it.
//
// Every falling thing moves by GnomeMovement.fallSpeed * gravity: the gnome, the asteroids
// (Asteroid.cs) and the Earth (Earth.cs). Halving fallSpeed halves the whole fall evenly.
// Horizontal steering uses playerSpeed / defaultPlayerSpeed, sprint uses runSpeed.
//
// Menu: Space Gnome > Apply Gentler Pace (Player 3 prefab)
// Writes the values below into the Player 3 prefab, which GYM_03 uses. Edit them here or
// in the GnomeMovement Inspector afterwards.

using UnityEditor;
using UnityEngine;

namespace SpaceGnomeTools
{
    public static class PaceTuning
    {
        const string PrefabPath = "Assets/Prefabs/Player 3.prefab";

        //                                    2023 value / history
        // 11-17 round 3: "up a little bit more across the board, just a touch" -> about +12%.
        const float FallSpeed = -4f;          // -7 -> -3.5 -> -4   (fall ~39 units/s)
        const float PlayerSpeed = 9f;         // 10 -> 6 -> 8 -> 9
        const int DefaultPlayerSpeed = 9;     // 10 -> 6 -> 8 -> 9
        const int RunSpeed = 23;              // 30 -> 15 -> 20 -> 23
        // Jump (GnomeMovement 2026 fields). Peak height of a free jump = speed^2 / (2 * gravity).
        const float BaseJumpSpeed = 24f;      // 12 -> 16 -> 18 (peak 3.6) -> 24 (peak 6.4; 11-17 "should be higher")
        const float JumpGravity = 45f;        // 30 -> 40 -> 45
        const float BoostLiftSpeed = 16f;     // 10 -> 14 -> 16

        // Platforms (Platforms.cs on GNOME 1). A new platform ends this far below the gnome, minus 9.
        const float PlatformSpawnDistance = 40f; // 90 (ended 81 below; 11-17 "too far") -> 40 (31 below, ~0.9 s of fall)

        // Fall damage (FallDamage.cs). Distance fallen since he last stood: below Minimum = no damage,
        // then 1..5 HP. At ~34 units/s, 150 units is about 4.4 s of falling.
        static readonly int[] FallThresholds = { 150, 400, 700, 1000, 1300 }; // 100, 300, 500, 700, 900

        [MenuItem("Space Gnome/Apply Gentler Pace (Player 3 prefab)")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var gm = root.GetComponentInChildren<GnomeMovement>(true);
                if (gm == null)
                {
                    EditorUtility.DisplayDialog("Gentler pace", "No GnomeMovement in " + PrefabPath, "OK");
                    return;
                }
                var so = new SerializedObject(gm);
                string report = "";
                report += Set(so, "fallSpeed", FallSpeed);
                report += Set(so, "playerSpeed", PlayerSpeed);
                report += Set(so, "defaultPlayerSpeed", DefaultPlayerSpeed);
                report += Set(so, "runSpeed", RunSpeed);
                report += Set(so, "baseJumpSpeed", BaseJumpSpeed);
                report += Set(so, "jumpGravity", JumpGravity);
                report += Set(so, "boostLiftSpeed", BoostLiftSpeed);
                so.ApplyModifiedPropertiesWithoutUndo();

                var platforms = root.GetComponentInChildren<Platforms>(true);
                if (platforms != null)
                {
                    var ps = new SerializedObject(platforms);
                    report += Set(ps, "platformSpawnDistance", PlatformSpawnDistance);
                    ps.ApplyModifiedPropertiesWithoutUndo();
                }
                else report += "Platforms: not found\n";

                var fall = root.GetComponentInChildren<FallDamage>(true);
                if (fall != null)
                {
                    var fs = new SerializedObject(fall);
                    string[] names = { "fallDamageThreshHoldMinimum", "fallDamageThreshHold1", "fallDamageThreshHold2", "fallDamageThreshHold3", "fallDamageThreshHold4" };
                    for (int i = 0; i < names.Length; i++) report += Set(fs, names[i], FallThresholds[i]);
                    fs.ApplyModifiedPropertiesWithoutUndo();
                }
                else report += "FallDamage: not found\n";

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[SpaceGnome Pace] " + PrefabPath + "\n" + report);
                EditorUtility.DisplayDialog("Gentler pace", "Player 3 prefab updated:\n\n" + report, "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static string Set(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p == null) return name + ": not found\n";
            string before;
            if (p.propertyType == SerializedPropertyType.Integer) { before = p.intValue.ToString(); p.intValue = Mathf.RoundToInt(value); }
            else { before = p.floatValue.ToString(); p.floatValue = value; }
            return name + ": " + before + " -> " + value + "\n";
        }
    }
}
