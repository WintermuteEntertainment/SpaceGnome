using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CamController : MonoBehaviour
{
    SGInput player1Controls;

    [SerializeField] GameObject mainCamera;
    [SerializeField] GameObject fallingCamera;
    [SerializeField] GameObject vCam1;
    [SerializeField] GameObject vCam2;

    public GnomeMovement m_gnomeMovement;
    public FloorCollider m_floorCollider;

    [Tooltip("On: landed camera (vCam1, front three-quarter view for dancing) while standing, " +
             "falling camera (vCam2, above) in the air. Off: vCam2 for everything.")]
    [SerializeField] bool useStandingCamera = true;

    [Header("Air camera distance (2026-11-18)")]
    [Tooltip("vCam2 offset from the gnome in the air, world space (it never rotates). The scene value was (0, 14, -4); Tim: too close.")]
    [SerializeField] Vector3 airOffsetFar = new Vector3(0f, 19f, -8f);
    [Tooltip("Close-follow view, toggled with gamepad Select / keyboard V.")]
    [SerializeField] Vector3 airOffsetClose = new Vector3(0f, 9f, -7f);
    [SerializeField] bool startClose = false;
    [Tooltip("Seconds to glide between the two views.")]
    [SerializeField] float viewBlendSeconds = 0.4f;

    InputAction viewToggle;
    bool closeView;
    Cinemachine.CinemachineTransposer airTransposer;

    private void Awake()
    {
        player1Controls = new SGInput();
        closeView = startClose;
        viewToggle = new InputAction("CameraView", InputActionType.Button);
        viewToggle.AddBinding("<Gamepad>/select");
        viewToggle.AddBinding("<Keyboard>/v");
        viewToggle.performed += ctx =>
        {
            closeView = !closeView;
            Debug.Log("Camera view: " + (closeView ? "close " + airOffsetClose : "far " + airOffsetFar) + ".");
        };
    }

    void UpdateAirDistance()
    {
        if (airTransposer == null && vCam2 != null)
        {
            var vc = vCam2.GetComponent<Cinemachine.CinemachineVirtualCamera>();
            if (vc != null) airTransposer = vc.GetCinemachineComponent<Cinemachine.CinemachineTransposer>();
            if (airTransposer == null) return;
            airTransposer.m_FollowOffset = closeView ? airOffsetClose : airOffsetFar; // no glide on the first frame
        }
        if (airTransposer == null) return;
        Vector3 goal = closeView ? airOffsetClose : airOffsetFar;
        float k = viewBlendSeconds <= 0f ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime / viewBlendSeconds * 3f);
        airTransposer.m_FollowOffset = Vector3.Lerp(airTransposer.m_FollowOffset, goal, k);
    }

    private void Update()
    {
        UpdateAirDistance();
        // Airborne (falling OR jumping) uses the falling camera. 2023 checked isFallingIdle only,
        // so during a jump the close standing camera (vCam1) kept filming and lost the gnome.
        if (!useStandingCamera || m_gnomeMovement.isFallingIdle || (m_floorCollider != null && !m_floorCollider.isStanding))
        {
            if (mainCamera != null)
            {
                // Falling is filmed by vCam2 through the Main Camera's Cinemachine brain.
                // (2023: this switched to the gnome-parented Falling Camera and turned the
                // Main Camera off, which the check at the end of Update undid every frame.)
                if (fallingCamera != null) fallingCamera.SetActive(false);
                vCam2.SetActive(true);
                mainCamera.SetActive(true);
                vCam1.SetActive(false);
            }
           
        }
        else if (m_floorCollider.isStanding) 
        {
            if (fallingCamera != null)
            {
                mainCamera.SetActive(true);
                vCam1.SetActive(true);
                fallingCamera.SetActive(false);
                vCam2.SetActive(false);
            }
          
        }
        if (!vCam1.activeInHierarchy && !vCam2.activeInHierarchy)
        {
            vCam1.SetActive(true);
        }
        if (!mainCamera.activeInHierarchy)
        {
            mainCamera.SetActive(true);
        }
    }

    void OnEnable()
    {
        player1Controls?.Player.Enable();
        viewToggle?.Enable();
    }
    void OnDisable()
    {
        player1Controls?.Player.Disable();
        viewToggle?.Disable();
    }
    void OnDestroy()
    {
        viewToggle?.Dispose();
    }
}
