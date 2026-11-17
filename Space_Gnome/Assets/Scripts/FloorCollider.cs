using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
//using System.Diagnostics;
using UnityEngine;

public class FloorCollider : MonoBehaviour
{
    public GameObject player;
    public GnomeMovement m_gnomeMovement;
    public bool isStanding;

    [SerializeField] Rigidbody floorColliderRB;

    // 2026: GnomeMovement now decides standing / landing itself by casting the gnome's capsule
    // (see GnomeMovement.HandleGround / FallStep). These contact callbacks used to do it and are
    // kept only for reference; they no longer change state unless this is ticked.
    [SerializeField] bool driveStandingFromContacts = false;

    private void Awake()
    {
        // When not used for standing, this box (with its own non-kinematic Rigidbody) only got in
        // the way: it pressed against the platform and the gnome's own collider and pushed him.
        if (driveStandingFromContacts) return;
        foreach (var c in GetComponents<Collider>()) c.enabled = false;
        if (floorColliderRB != null) floorColliderRB.isKinematic = true;
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (!driveStandingFromContacts) return;
        if (collision.gameObject.CompareTag("Floor"))
        {

            isStanding = true;
            Debug.Log("Started standing...");
            m_gnomeMovement.ResetStates();
          // m_gnomeMovement.gravity = m_gnomeMovement.defaultGravity;
        }
        //else { isStanding = false; }
    }
    public void OnCollisionExit(Collision collision)
    {
        if (!driveStandingFromContacts) return;
        if (collision.gameObject.CompareTag("Floor"))
        {
            if (m_gnomeMovement.player1Controls.Player.Jump.triggered /*&& Toolbox.instance.m_gnomeMovement.jumpingAllowed*/)
            {
                isStanding = false;
                Debug.Log("Started Jumping...");
                
                m_gnomeMovement.isJumping = true;
                m_gnomeMovement.isIdle = false;
                m_gnomeMovement.isFallingIdle = false;
                m_gnomeMovement.isWalking = false;
                m_gnomeMovement.isSprinting = false;
                m_gnomeMovement.isDancing = false;

            }
            else if (m_gnomeMovement.isJumping==false)
            {
                isStanding = false;
                Debug.Log("Started FallingIdle...");
                
                m_gnomeMovement.FallingIdle();
                m_gnomeMovement.isFallingIdle = true;
                m_gnomeMovement.isIdle = false;
                m_gnomeMovement.isJumping = false;
                m_gnomeMovement.isWalking = false;
                m_gnomeMovement.isSprinting = false;
                m_gnomeMovement.isDancing = false;
            }
            else { isStanding = false; }

        }



    }
    private void Update()
    {
        if (!driveStandingFromContacts) return; // box is off; GnomeMovement handles the ground
        if (player.transform.position.y < gameObject.transform.position.y)
        {
            transform.Translate(((player.transform.position.y - transform.position.y) * Vector3.up), Space.Self);
            Debug.Log("Updated Floor Collider Y position.");
            Toolbox.instance.m_gnomeMovement.playerRB.velocity = Vector3.zero;
            Debug.Log("Set Player RB Velocity to (0,0,0).");
            floorColliderRB.velocity = Vector3.zero;
            Debug.Log("Set velocity to zero.");
        }


        

    }

    

}
