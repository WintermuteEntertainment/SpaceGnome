// Space Gnome recovery: camera setup for GYM_03.
//
// vCam2 (in the air): follows and frames the gnome from above, world-space offset, so it does
//   not spin with him. 2023 version followed a marker ~15 units off to the side with no Look At
//   and a 15-degree lens, so the gnome sat outside the frame.
// vCam1 (landed): behind and above the gnome, turning with him (third-person), dances still visible.
// Blends (Main Camera Blends asset):
//   vCam1 -> vCam2 (jump / step off): CUT, so the camera is above him from the first frame.
//     2023: a 2-second ease, which dragged the view down through the platform while he left frame.
//   vCam2 -> vCam1 (landing): short ease, LandingBlendSeconds.
// Tune the numbers below (or the vCam Inspectors); re-running re-applies them.
//
// Menu: Space Gnome > Set Up Cameras (open scene)

using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceGnomeTools
{
    public static class FallingCameraSetup
    {
        // In the air (vCam2)
        static readonly Vector3 AirOffset = new Vector3(0f, 14f, -4f); // world space: above, slightly behind
        const float AirFieldOfView = 35f;
        const float AirPositionDamping = 0.3f;  // sideways lag, seconds (no vertical lag)
        const float AirAimDamping = 0.2f;

        // Landed (vCam1): offset in the gnome's own frame (x right, y up, z forward).
        // Behind and above him, so stick-forward is screen-forward (11-17: front view made Tim
        // "drive him backwards"). Was (2.5, 2.2, 7), in front.
        static readonly Vector3 LandedOffset = new Vector3(0f, 3f, -8f);
        const float LandedFieldOfView = 35f;
        const float LandedPositionDamping = 0.5f;
        const float LandedYawDamping = 0f;       // the CameraOrbit pivot already eases its heading
        const float LandedAimDamping = 0.3f;

        const float DeadZone = 0.05f;   // fraction of screen he may drift before the aim reacts
        const float SoftZone = 0.6f;    // he is kept inside this central fraction of the screen
        const float LandingBlendSeconds = 0.6f;
        const string BlendsPath = "Assets/Main Camera Blends.asset";

        [MenuItem("Space Gnome/Set Up Cameras (open scene)")]
        public static void Run()
        {
            CinemachineVirtualCamera air = null, landed = null;
            Transform target = null, fallingCamera = null;

            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    foreach (var v in root.GetComponentsInChildren<CinemachineVirtualCamera>(true))
                    {
                        if (v.name == "vCam2") air = v;
                        if (v.name == "vCam1") landed = v;
                    }
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "CameraLookAtTarget" && t.parent != null && t.parent.name == "GNOME 1") target = t;
                        if (t.name == "Falling Camera") fallingCamera = t;
                    }
                }

            if (air == null || landed == null || target == null)
            {
                EditorUtility.DisplayDialog("Cameras",
                    "Could not find " + (air == null ? "vCam2" : landed == null ? "vCam1" : "GNOME 1/CameraLookAtTarget") +
                    " in the open scene. Open GYM_03 and try again.", "OK");
                return;
            }

            // Landed camera follows an orbit pivot (CameraOrbit) that the right stick turns while the
            // gnome stands still, and that settles behind him when he moves.
            var pivot = EnsureOrbitPivot(target, air.gameObject.scene);

            Configure(air, target, target, CinemachineTransposer.BindingMode.WorldSpace, AirOffset,
                      AirPositionDamping, 0f, 0f, AirFieldOfView, AirAimDamping);
            Configure(landed, pivot, target, CinemachineTransposer.BindingMode.LockToTargetWithWorldUp, LandedOffset,
                      LandedPositionDamping, LandedPositionDamping, LandedYawDamping, LandedFieldOfView, LandedAimDamping);

            if (fallingCamera != null && fallingCamera.gameObject.activeSelf)
            {
                Undo.RecordObject(fallingCamera.gameObject, "Falling camera off");
                fallingCamera.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fallingCamera.gameObject);
            }

            string blends = SetBlends();

            EditorSceneManager.MarkSceneDirty(air.gameObject.scene);
            EditorSceneManager.SaveScene(air.gameObject.scene);
            Debug.Log("[SpaceGnome Camera] vCam2 air " + AirOffset + ", vCam1 landed " + LandedOffset + ". " + blends);
            EditorUtility.DisplayDialog("Cameras",
                "vCam2 (air) and vCam1 (landed) now follow and frame the gnome.\n" + blends + "\nScene saved. Press Play to try it.", "OK");
        }

        static Transform EnsureOrbitPivot(Transform target, Scene scene)
        {
            CameraOrbit orbit = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                orbit = root.GetComponentInChildren<CameraOrbit>(true);
                if (orbit != null) break;
            }
            if (orbit == null)
            {
                var go = new GameObject("CameraOrbitPivot");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Camera orbit pivot");
                orbit = go.AddComponent<CameraOrbit>();
            }
            Undo.RecordObject(orbit, "Camera orbit pivot");
            orbit.target = target;
            orbit.gnome = target.GetComponentInParent<GnomeMovement>();
            orbit.transform.position = target.position;
            orbit.transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            EditorUtility.SetDirty(orbit);
            return orbit.transform;
        }

        static void Configure(CinemachineVirtualCamera vcam, Transform follow, Transform lookAt, CinemachineTransposer.BindingMode binding,
                              Vector3 offset, float xzDamping, float yDamping, float yawDamping, float fov, float aimDamping)
        {
            Undo.RecordObject(vcam, "Camera setup");
            vcam.Follow = follow;
            vcam.LookAt = lookAt;
            var lens = vcam.m_Lens;
            lens.FieldOfView = fov;
            vcam.m_Lens = lens;

            var body = vcam.GetCinemachineComponent<CinemachineTransposer>();
            if (body == null || body.GetType() != typeof(CinemachineTransposer))
                body = vcam.AddCinemachineComponent<CinemachineTransposer>();
            Undo.RecordObject(body, "Camera body");
            body.m_BindingMode = binding;
            body.m_FollowOffset = offset;
            body.m_XDamping = xzDamping;
            body.m_YDamping = yDamping;
            body.m_ZDamping = xzDamping;
            body.m_YawDamping = yawDamping;

            var aim = vcam.GetCinemachineComponent<CinemachineComposer>();
            if (aim == null || aim.GetType() != typeof(CinemachineComposer))
                aim = vcam.AddCinemachineComponent<CinemachineComposer>();
            Undo.RecordObject(aim, "Camera aim");
            aim.m_TrackedObjectOffset = Vector3.zero;
            aim.m_ScreenX = 0.5f;
            aim.m_ScreenY = 0.5f;
            aim.m_DeadZoneWidth = DeadZone;
            aim.m_DeadZoneHeight = DeadZone;
            aim.m_SoftZoneWidth = SoftZone;
            aim.m_SoftZoneHeight = SoftZone;
            aim.m_HorizontalDamping = aimDamping;
            aim.m_VerticalDamping = aimDamping;

            PrefabUtility.RecordPrefabInstancePropertyModifications(vcam);
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            PrefabUtility.RecordPrefabInstancePropertyModifications(aim);
        }

        static string SetBlends()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(BlendsPath);
            if (settings == null) return "Blend asset not found at " + BlendsPath + "; blends unchanged.";
            Undo.RecordObject(settings, "Camera blends");
            settings.m_CustomBlends = new[]
            {
                new CinemachineBlenderSettings.CustomBlend
                {
                    m_From = "vCam1", m_To = "vCam2",
                    m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f),
                },
                new CinemachineBlenderSettings.CustomBlend
                {
                    m_From = "vCam2", m_To = "vCam1",
                    m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, LandingBlendSeconds),
                },
            };
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return "Jump: cut to the air camera. Landing: " + LandingBlendSeconds + " s blend to the landed camera.";
        }
    }
}
