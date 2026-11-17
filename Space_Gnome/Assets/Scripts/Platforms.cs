using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
[System.Serializable]

public class Platforms : MonoBehaviour
{
    [SerializeField] GameObject player;

    [SerializeField] GameObject platformPrefab;

    [SerializeField] Transform platformObjectsParent;

    public GameObject[] spawnablePlatforms;

    GameObject clone;

   public SGInput player1Controls;

    [SerializeField] float platformSpawnDistance;

    [SerializeField] Vector3 platformSpawnOffset;

    private void Awake()
    {

        clone = null;
        player1Controls = new SGInput();
        player1Controls.Player.FirePlatform.performed += ctx => Fire();
    }

    public void Fire()
    {
        // 2026 fix: the 2023 line was Instantiate(clone = prefab, ...), which stored the PREFAB in
        // `clone`, so SetActive and the 90-unit move below were applied to the prefab asset (its
        // file drifted to y = -26000) while the new platform stayed where it appeared. It also
        // assumed exactly 5 entries. Same spawn maths as intended, applied to the new platform:
        // start at player - platformSpawnOffset, then platformSpawnDistance further down.
        if (spawnablePlatforms == null || spawnablePlatforms.Length == 0) return;
        var prefab = spawnablePlatforms[Random.Range(0, spawnablePlatforms.Length)];
        if (prefab == null) return;
        clone = Instantiate(prefab, player.transform.position - platformSpawnOffset, player.transform.rotation, platformObjectsParent);
        clone.SetActive(true);
        clone.transform.Translate(Vector3.down * platformSpawnDistance, Space.Self);
        Debug.Log("Spawned platform at " + clone.transform.position + ".");
    }
    //private void Update()
    //{
    //    if (player1Controls.Player.Fire.triggered)
    //    {
    //        Fire();
    //    }
    //}                                                     //Possibly cause of double platform spawning!
    void OnEnable()
    {
        player1Controls.Player.Enable();
    }
    void OnDisable()
    {
        player1Controls.Player.Disable();
    }
}
