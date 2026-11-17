using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Asteroids : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Asteroid"))
        {
            // 2026: no damage for bumping into an asteroid while standing on a platform; it still
            // hurts in the air, or when it comes down on his head (contact in the top third of him).
            var gm = Toolbox.instance.m_gnomeMovement;
            bool standing = gm != null && gm.m_floorCollider != null && gm.m_floorCollider.isStanding;
            if (standing)
            {
                var body = GetComponent<Collider>();
                float headLine = body != null ? body.bounds.max.y - body.bounds.size.y / 3f : transform.position.y;
                bool fromAbove = collision.contactCount > 0 && collision.GetContact(0).point.y >= headLine;
                if (!fromAbove) return;
            }

            Toolbox.instance.m_coins.hP -= 1;
            Debug.Log("Hit an asteroid.");
            Toolbox.instance.m_audio.tookDamage.Play();
        }
    }
   
}
