using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class WallColour : MonoBehaviour
{
    public Rigidbody rB;
    public Collider wallCollision;
    public Material wallMaterial;
    public MeshRenderer meshRenderer;

    private void Start()
    {
    
    }
    private void Update()
    {
    
    }
    void OnTriggerEnter(Collision wallCollision)
    {
        wallMaterial.DOColor(Color.blue, 10);
        if (wallCollision.gameObject)
        {
            
        }
        
    }
}
