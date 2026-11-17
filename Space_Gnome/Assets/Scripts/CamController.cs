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

    private void Awake()
    {
        player1Controls = new SGInput();
    }

    private void Update()
    {
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
        player1Controls.Player.Enable();

    }
    void OnDisable()
    {
        player1Controls.Player.Disable();
    }
}
