// Space Gnome: report what the Input System sees, to find out why the gamepad stops working.
// Menu: Space Gnome > Input Diagnostics (use while in Play mode, ideally while it is broken)
// Writes Logs/SpaceGnome_InputDiagnostics.txt and the Console.

using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGnomeTools
{
    public static class InputDiagnostics
    {
        // The project's Input System settings (Assets/InputSystem.inputsettings.asset) use the defaults:
        //   Background Behavior = Reset And Disable Non-Background Devices: gamepads are switched off
        //     while the Editor/app window does not have focus (e.g. after clicking into another program).
        //   Play Mode Input Behavior = Pointers And Keyboards Respect Game View Focus.
        // This makes gamepad input always reach the game in the Editor. Same as changing the two
        // fields in Project Settings > Input System Package.
        [MenuItem("Space Gnome/Input: Keep Gamepad Active")]
        public static void KeepGamepadActive()
        {
            var s = InputSystem.settings;
            string before = s.backgroundBehavior + " / " + s.editorInputBehaviorInPlayMode;
            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            string after = s.backgroundBehavior + " / " + s.editorInputBehaviorInPlayMode;
            Debug.Log("[SpaceGnome Input] settings " + before + " -> " + after + " (" + AssetDatabase.GetAssetPath(s) + ")");
            EditorUtility.DisplayDialog("Keep gamepad active",
                "Background behavior / Play mode input:\n" + before + "\n->\n" + after +
                "\n\nSaved to " + AssetDatabase.GetAssetPath(s) + ".", "OK");
        }

        [MenuItem("Space Gnome/Input Diagnostics")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Space Gnome input diagnostics, " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Play mode: " + EditorApplication.isPlaying + "   app focused: " + Application.isFocused + "   timeScale: " + Time.timeScale);

            var s = InputSystem.settings;
            sb.AppendLine("Settings: backgroundBehavior=" + s.backgroundBehavior +
                          "  editorInputBehaviorInPlayMode=" + s.editorInputBehaviorInPlayMode +
                          "  updateMode=" + s.updateMode);

            sb.AppendLine("");
            sb.AppendLine("Devices:");
            foreach (var d in InputSystem.devices)
                sb.AppendLine("  " + d.layout + " '" + d.displayName + "'  enabled=" + d.enabled +
                              "  canRunInBackground=" + d.canRunInBackground +
                              "  lastUpdate=" + d.lastUpdateTime.ToString("0.00") + "  path=" + d.path);
            sb.AppendLine("Disabled devices: " + string.Join(", ", InputSystem.disconnectedDevices.Select(d => d.displayName)));

            var pad = Gamepad.current;
            sb.AppendLine("Gamepad.current: " + (pad == null ? "NONE" :
                pad.displayName + "  leftStick=" + pad.leftStick.ReadValue() + "  rightStick=" + pad.rightStick.ReadValue() +
                "  south=" + pad.buttonSouth.isPressed));

            sb.AppendLine("");
            if (EditorApplication.isPlaying)
            {
                foreach (var gm in Object.FindObjectsOfType<GnomeMovement>(true))
                {
                    var c = gm.player1Controls;
                    sb.AppendLine("GnomeMovement on '" + gm.name + "' active=" + gm.isActiveAndEnabled);
                    if (c == null) { sb.AppendLine("  player1Controls is null"); continue; }
                    sb.AppendLine("  Player map enabled=" + c.Player.enabled +
                                  "  devices filter=" + (c.devices.HasValue ? string.Join(",", c.devices.Value.Select(d => d.displayName)) : "(all)") +
                                  "  bindingMask=" + (c.bindingMask.HasValue ? c.bindingMask.Value.groups : "(none)"));
                    foreach (var name in new[] { "Move", "Look", "Jump" })
                    {
                        var a = c.asset.FindAction("Player/" + name);
                        sb.AppendLine("  " + name + ": enabled=" + a.enabled + " phase=" + a.phase +
                                      " activeControl=" + (a.activeControl != null ? a.activeControl.path : "none") +
                                      " controls=" + string.Join(" ", a.controls.Select(x => x.path)));
                    }
                }
                var ui = Object.FindObjectOfType<UI_Manager>();
                sb.AppendLine("UI_Manager state: " + (ui != null ? ui.State.ToString() : "not found"));
                var es = UnityEngine.EventSystems.EventSystem.current;
                sb.AppendLine("EventSystem selected: " + (es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "none"));
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SpaceGnome_InputDiagnostics.txt", sb.ToString());
            Debug.Log("[SpaceGnome Input]\n" + sb);
        }
    }
}
