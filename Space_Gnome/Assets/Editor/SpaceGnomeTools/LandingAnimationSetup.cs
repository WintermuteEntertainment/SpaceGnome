// Space Gnome: landing animations (2026-11-18, Tim's clip picks).
//
// Adds to Gnome_Controller, Base Layer:
//   triggers  Land, LandHard  (GnomeMovement.PlayLandingAnimation sets one when he lands)
//   state     "Land"       = Jumping Down   (normal landing)
//   state     "Land Hard"  = Fall Flat      (landing that cost HP; movement locked 1.2 s in code)
//   Any State -> Land / Land Hard on the trigger (no exit time, quick blend)
//   Land      -> Idle at 85% of the clip; straight to Walking / Run / Jumping / Falling Idle if those start
//   Land Hard -> Idle at 95%; to Falling Idle if the ground goes; to Walking / Run only after 60%
// Re-running is safe: it replaces its own states' transitions instead of adding duplicates.
// If a clip starts too early (Jumping Down includes the jump off before the landing), set
// "Cycle Offset" on that state in the Animator window, e.g. 0.4 to start 40% in.
//
// Menu: Space Gnome > Add Landing Animations

using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SpaceGnomeTools
{
    public static class LandingAnimationSetup
    {
        const string ControllerPath = "Assets/Animation/Gnome_Controller.controller";
        const string SoftFbx = "Assets/Animation/Animations/Dances/Gnome-TPose-NoBones@Jumping Down.fbx";
        const string HardFbx = "Assets/Animation/Animations/Dances/Gnome-TPose-NoBones@Fall Flat.fbx";

        [MenuItem("Space Gnome/Add Landing Animations")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Landing animations", "Stop Play mode first.", "OK"); return; }
            var report = new StringBuilder();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) { EditorUtility.DisplayDialog("Landing animations", "Not found: " + ControllerPath, "OK"); return; }
            var soft = Clip(SoftFbx, report);
            var hard = Clip(HardFbx, report);
            if (soft == null || hard == null) { EditorUtility.DisplayDialog("Landing animations", report.ToString(), "OK"); return; }

            Undo.RecordObject(ctrl, "Landing animations");
            var sm = ctrl.layers[0].stateMachine;
            Undo.RecordObject(sm, "Landing animations");

            foreach (var p in new[] { "Land", "LandHard" })
                if (!ctrl.parameters.Any(x => x.name == p)) { ctrl.AddParameter(p, AnimatorControllerParameterType.Trigger); report.AppendLine("Added trigger " + p); }

            AnimatorState Find(string prefix) => sm.states.Select(s => s.state).FirstOrDefault(s => s.name.StartsWith(prefix));
            var idle = Find("Idle State");
            var walk = Find("Walking State");
            var run = Find("Run State");
            var jump = Find("Jumping State");
            var fallIdle = Find("Falling Idle State");
            if (idle == null) { EditorUtility.DisplayDialog("Landing animations", "No 'Idle State' on the Base Layer; nothing changed.", "OK"); return; }

            var land = State(sm, "Land", soft, new Vector3(250, 450, 0), report);
            var landHard = State(sm, "Land Hard", hard, new Vector3(250, 520, 0), report);

            AnyTo(sm, land, "Land");
            AnyTo(sm, landHard, "LandHard");

            Clear(land); Clear(landHard);
            Exit(land, idle, 0.85f, 0.2f, null);
            Exit(land, walk, null, 0.15f, "isWalking");
            Exit(land, run, null, 0.15f, "isRunning");
            Exit(land, jump, null, 0.1f, "isJumping");
            Exit(land, fallIdle, null, 0.15f, "isFallingIdle");
            Exit(landHard, idle, 0.95f, 0.3f, null);
            Exit(landHard, fallIdle, null, 0.2f, "isFallingIdle");
            Exit(landHard, walk, 0.6f, 0.2f, "isWalking");
            Exit(landHard, run, 0.6f, 0.2f, "isRunning");
            report.AppendLine("Any State -> Land (Jumping Down) / Land Hard (Fall Flat); exits to Idle, Walking, Run, Jumping, Falling Idle.");

            EditorUtility.SetDirty(ctrl);
            EditorUtility.SetDirty(sm);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpaceGnome Landing]\n" + report);
            EditorUtility.DisplayDialog("Landing animations", report.ToString(), "OK");
        }

        static AnimationClip Clip(string fbx, StringBuilder report)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            report.AppendLine(clip != null ? "Clip: " + clip.name + " (" + clip.length.ToString("0.00") + " s, loop " + clip.isLooping + ")" : "Clip NOT found in " + fbx);
            return clip;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos, StringBuilder report)
        {
            var s = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == name);
            if (s == null) { s = sm.AddState(name, pos); report.AppendLine("Added state " + name); }
            s.motion = clip;
            s.writeDefaultValues = true;
            return s;
        }

        static void AnyTo(AnimatorStateMachine sm, AnimatorState dst, string trigger)
        {
            foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == dst).ToArray()) sm.RemoveAnyStateTransition(t);
            var any = sm.AddAnyStateTransition(dst);
            any.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            any.hasExitTime = false;
            any.duration = 0.08f;
            any.canTransitionToSelf = false;
        }

        static void Clear(AnimatorState s)
        {
            Undo.RecordObject(s, "Landing exits");
            foreach (var t in s.transitions.ToArray()) s.RemoveTransition(t);
        }

        static void Exit(AnimatorState from, AnimatorState to, float? exitTime, float blend, string ifBool)
        {
            if (to == null) return;
            var t = from.AddTransition(to);
            t.hasExitTime = exitTime.HasValue;
            if (exitTime.HasValue) t.exitTime = exitTime.Value;
            t.duration = blend;
            if (ifBool != null) t.AddCondition(AnimatorConditionMode.If, 0f, ifBool);
        }
    }
}
