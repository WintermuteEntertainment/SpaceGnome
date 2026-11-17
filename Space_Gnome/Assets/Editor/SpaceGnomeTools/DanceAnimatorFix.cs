// Space Gnome: make the breakdance animations reachable.
//
// Gnome_Controller only reached the dance moves through Idle -> Dancing State -> Ready -> move,
// which needs isDancing and then isDanceReady. GnomeMovement's dance methods call ResetStates()
// (clearing both) before setting the move's flag, and the line that set isDanceReady was commented
// out, so the Animator never left Idle even though the combos (and score) worked in code.
//
// This adds, on the Base Layer:
//   Any State -> <move>   when the move's flag is true (no exit time, short blend, no self-restart)
//   <move> -> Idle State  when the flag is false and isIdle is true (if not already there)
// and turns off Apply Root Motion on the gnome's Animator in Player 3 (movement is driven by
// code; root motion from the clips fought it and could make running stall or dances drift).
// Re-running is safe: it replaces its own transitions instead of adding duplicates.
//
// Menu: Space Gnome > Fix Dance Animations

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SpaceGnomeTools
{
    public static class DanceAnimatorFix
    {
        const string ControllerPath = "Assets/Animation/Gnome_Controller.controller";
        const string PlayerPrefab = "Assets/Prefabs/Player 3.prefab";
        const float BlendSeconds = 0.15f;

        // Base Layer state name -> bool parameter GnomeMovement sets for it.
        // Second Ready pose (Y + B held), Tim's pick 2026-11-17.
        const string Ready2State = "Ready 2";
        const string Ready2Param = "isDanceReady2";
        const string Ready2Fbx = "Assets/Animation/Animations/Dances/Gnome-TPose-NoBones@Breakdance Footwork To Idle.fbx";
        const string Ready1Fbx = "Assets/Animation/Animations/Dances/Gnome-TPose-NoBones@Breakdance Ready.fbx";

        static readonly Dictionary<string, string> Moves = new Dictionary<string, string>
        {
            { "Ready", "isDanceReady" },          // Ready pose while Y is held (dance tiers, 11-17)
            { Ready2State, Ready2Param },         // Ready pose while Y + B are held
            { "BreakDance1990", "BreakDance1990" },
            { "Headspin", "Headspin" },
            { "Uprock", "Uprock" },
            { "RockDancing", "RockDancing" },
            { "Flair", "isFlair" },
            { "HipHop Dancing", "isHipHopDancing" },
            { "Silly Dancing", "isSillyDancing" },
            { "Breakdance Ending 1", "isBreakDanceEnding1" },
        };

        [MenuItem("Space Gnome/Fix Dance Animations")]
        public static void Run()
        {
            var report = new StringBuilder();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) { EditorUtility.DisplayDialog("Dance animations", "Not found: " + ControllerPath, "OK"); return; }

            Undo.RecordObject(ctrl, "Dance transitions");
            var sm = ctrl.layers[0].stateMachine;
            Undo.RecordObject(sm, "Dance transitions");

            EnsureReady2(ctrl, sm, report);

            AnimatorState idle = null;
            foreach (var cs in sm.states) if (cs.state.name.StartsWith("Idle State")) idle = cs.state;

            foreach (var cs in sm.states)
            {
                var state = cs.state;
                if (!Moves.TryGetValue(state.name, out string param)) continue;

                // Replace any Any State transitions into this state (keeps re-runs idempotent).
                foreach (var t in sm.anyStateTransitions)
                    if (t.destinationState == state) sm.RemoveAnyStateTransition(t);

                var any = sm.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, param);
                any.hasExitTime = false;
                any.duration = BlendSeconds;
                any.canTransitionToSelf = false;
                report.AppendLine("Any State -> " + state.name + "  if " + param);

                if (idle != null)
                {
                    bool hasIdleExit = false;
                    foreach (var t in state.transitions) if (t.destinationState == idle) hasIdleExit = true;
                    if (!hasIdleExit)
                    {
                        Undo.RecordObject(state, "Dance exit");
                        var back = state.AddTransition(idle);
                        back.AddCondition(AnimatorConditionMode.IfNot, 0f, param);
                        back.AddCondition(AnimatorConditionMode.If, 0f, "isIdle");
                        back.hasExitTime = false;
                        back.duration = 0.25f;
                        report.AppendLine("   " + state.name + " -> Idle State  if !" + param + " & isIdle (added)");
                    }
                }
            }
            EditorUtility.SetDirty(ctrl);
            EditorUtility.SetDirty(sm);
            AssetDatabase.SaveAssets();

            // Root motion off on the gnome.
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                foreach (var anim in root.GetComponentsInChildren<Animator>(true))
                {
                    if (anim.runtimeAnimatorController == null || anim.runtimeAnimatorController.name != ctrl.name) continue;
                    report.AppendLine("Apply Root Motion on " + anim.name + ": " + anim.applyRootMotion + " -> False");
                    anim.applyRootMotion = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            Debug.Log("[SpaceGnome Dance]\n" + report);
            EditorUtility.DisplayDialog("Dance animations", report.ToString(), "OK");
        }

        // Import the Footwork To Idle animation (its FBX had no clip settings saved) with the same
        // clip settings as the first Ready pose, and add a "Ready 2" state + isDanceReady2 parameter.
        static void EnsureReady2(AnimatorController ctrl, AnimatorStateMachine sm, StringBuilder report)
        {
            var importer = AssetImporter.GetAtPath(Ready2Fbx) as ModelImporter;
            if (importer == null) { report.AppendLine("Ready 2: FBX not found at " + Ready2Fbx); return; }

            if (importer.clipAnimations == null || importer.clipAnimations.Length == 0)
            {
                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) { report.AppendLine("Ready 2: the FBX contains no animation take"); return; }

                var ready1 = AssetImporter.GetAtPath(Ready1Fbx) as ModelImporter;
                var template = ready1 != null && ready1.clipAnimations.Length > 0 ? ready1.clipAnimations[0] : null;
                foreach (var c in clips)
                {
                    c.loopTime = true; // held pose: loop while the buttons are held
                    if (template != null)
                    {
                        c.loopPose = template.loopPose;
                        c.lockRootRotation = template.lockRootRotation;
                        c.lockRootHeightY = template.lockRootHeightY;
                        c.lockRootPositionXZ = template.lockRootPositionXZ;
                        c.keepOriginalOrientation = template.keepOriginalOrientation;
                        c.keepOriginalPositionY = template.keepOriginalPositionY;
                        c.keepOriginalPositionXZ = template.keepOriginalPositionXZ;
                        c.heightFromFeet = template.heightFromFeet;
                        c.cycleOffset = template.cycleOffset;
                    }
                }
                importer.clipAnimations = clips;
                if (ready1 != null && ready1.sourceAvatar != null) importer.sourceAvatar = ready1.sourceAvatar;
                importer.SaveAndReimport();
                report.AppendLine("Ready 2: imported " + clips.Length + " clip(s) from Breakdance Footwork To Idle (looping)");
            }

            AnimationClip clip = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Ready2Fbx))
                if (a is AnimationClip ac && !ac.name.StartsWith("__preview__")) { clip = ac; break; }
            if (clip == null) { report.AppendLine("Ready 2: no clip found after import"); return; }

            bool hasParam = false;
            foreach (var p in ctrl.parameters) if (p.name == Ready2Param) hasParam = true;
            if (!hasParam) { ctrl.AddParameter(Ready2Param, AnimatorControllerParameterType.Bool); report.AppendLine("Added parameter " + Ready2Param); }

            AnimatorState ready2 = null, ready1State = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name == Ready2State) ready2 = cs.state;
                if (cs.state.name == "Ready") ready1State = cs.state;
            }
            if (ready2 == null)
            {
                Vector3 pos = new Vector3(500f, 300f, 0f);
                foreach (var cs in sm.states) if (cs.state == ready1State) pos = cs.position + new Vector3(0f, 80f, 0f);
                ready2 = sm.AddState(Ready2State, pos);
                report.AppendLine("Added state " + Ready2State);
            }
            Undo.RecordObject(ready2, "Ready 2 clip");
            ready2.motion = clip;
            EditorUtility.SetDirty(ready2);
            report.AppendLine(Ready2State + " plays clip '" + clip.name + "' (" + clip.length.ToString("0.00") + " s)");
        }
    }
}
