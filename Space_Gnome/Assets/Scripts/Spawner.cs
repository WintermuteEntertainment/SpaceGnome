using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("Game Objects")]
    public GameObject objectInstantiator;   //This game object assigned in Inspector.
    [SerializeField] GameObject playerPrefab;
    public GameObject[] objects; //Array of game objects to be instantiated from.
    private GameObject spawnedObject; //Most recently spawned object.
    private GameObject spawnedAsteroid; //Most recently spawned asteroid.
    //[SerializeField] GameObject launchplatform;
    [SerializeField] GameObject asteroidPrefab;
    private GameObject clonedAsteroid;

    //[Header("Fire Speed")]
    //[SerializeField] int fireSpeed;  //Speed at which instanstiated objects are fired at from firePoint.

    [Header ("Transforms")]
    [SerializeField] Transform firePoint; //Transform and direction to fire cloned object from/in.
    [SerializeField] Transform playerSpawnTransform; //Currently unused. Initial player spawn in level.
    [SerializeField] Transform spawnedObjectParentTransform; //Parent game object that cloned objects are placed under in Heiarchy.
    [SerializeField] Transform spawnedSpawnerParentTransform; //Parent object in heiarchy of cloned Spawners.
    [SerializeField] Transform rotationCenter; //Central point for objects to rotate around.
    [SerializeField] Transform asteroidFirePoint;  //Transform and direction to instantiate asteroids from/in.

    [SerializeField] bool isColliding; //Used to detect if player is colliding with the gameobject this script is attached to.
    [SerializeField] bool spawningAllowed; //True when object count is less than max object count.
    [SerializeField] bool isSpawned; //Currently unused.  Starts in Awake function as false. True immiedietly after object spawned if one was spawned this frame.
                                     //Reset to false on SpawnObject() call.

    [Header("Number of Objects Instantiated")]
    public int objectCount;                 //Current number of objects that have been instantiated.
    public int asteroidCount;               //Current number of instantiated Asteroids.
    [Header("Max Objects Allowed")]
    public int maxObjectCount;              //Maximum number of objects that can be instantiated at once.
    public int maxAsteroids;                //Maximum number of asteroids that can be instantiated at once.
    [Header("Max Spawners Allowed")]
    [SerializeField] int maxSpawnerCount;

    [Header("Random Distance Range From Player")]       //Minimum and maximum distances from player at which objects can be instantiated for each axis.
    public float xDistanceFromPlayerMin;
    public float xDistanceFromPlayerMax;
    public float yDistanceFromPlayerMin;
    public float yDistanceFromPlayerMax;
    public float zDistanceFromPlayerMin;
    public float zDistanceFromPlayerMax;

    [Header("Spawned Objects Spawn Distance")]
    [SerializeField] float spawnerSpawnDistance; //Y distance from player newly cloned Spawners are cloned at.
    [SerializeField] float asteroidSpawnDistance; //Y distance from player newly cloned asteroids are cloned at.

    Vector3 randomPos;


    private void Awake()
    {
        isSpawned = false; //Currently unused.
        spawningAllowed = true;
    }

    [System.Obsolete]
    private void Update()
    {

        if (!spawningAllowed)
        {
            return;
            isSpawned = false; //Still unused.
        }
        else
        { if (isColliding) { SpawnObject(); SpawnAsteroid(); } }

        if (objectCount <= maxObjectCount) { spawningAllowed = true; }
        else if (objectCount > maxObjectCount) { spawningAllowed = false; spawnedObject.SetActive(false); }

        randomPos.x = Random.Range(xDistanceFromPlayerMin, xDistanceFromPlayerMax);
        randomPos.y = Random.Range(yDistanceFromPlayerMin, yDistanceFromPlayerMax);
        randomPos.z = Random.Range(zDistanceFromPlayerMin, zDistanceFromPlayerMax);

        Vector3 position = new Vector3(randomPos.x, randomPos.y, randomPos.z);
        firePoint.transform.position = playerPrefab.transform.position + position; ;
        objectInstantiator = gameObject;

    }

    [System.Obsolete]
    private void FixedUpdate()
    {
        Vector3 asteroidPosition = new Vector3(randomPos.x, randomPos.y, randomPos.z);
        asteroidFirePoint.transform.position = playerPrefab.transform.position + asteroidPosition;
        if (clonedAsteroid != null) { clonedAsteroid.transform.RotateAround(rotationCenter.position, Random.Range(0, 361)); }

        if (playerPrefab.transform.position.y < objectInstantiator.transform.position.y) { 
                objectInstantiator.transform.Translate((objectInstantiator.transform.position.y - playerPrefab.transform.position.y - 1) * Time.deltaTime * Vector3.down);
        }
    }
    void SpawnObject()
    {
        // 2026: skip empty entries (a deleted prefab leaves a hole in the list, e.g. entry 18 after
        // "Rocks2 (landable)" was removed); Instantiate(null) throws.
        GameObject pick = null;
        for (int tries = 0; tries < 10 && pick == null; tries++) pick = objects[Random.Range(0, objects.Length)];
        if (pick == null) { isColliding = false; return; }
        objectCount += 1;
        // Keep the NEW object, not the prefab (2023 stored the prefab here, so the later
        // spawnedObject.SetActive(false) switched off the prefab asset itself).
        spawnedObject = Instantiate(pick, firePoint.position, firePoint.rotation, spawnedObjectParentTransform);
        // 2026-11-18: tumble-and-settle (drift, spin, wobble; still while the gnome stands on it).
        // Junk only (tag Floor): the same lists also hold Coin 1-3 and the hazard Asteroid, which
        // keep their own physics.
        if (spawnedObject.CompareTag("Floor") && spawnedObject.GetComponent<JunkMotion>() == null)
            spawnedObject.AddComponent<JunkMotion>();
        // Count coins for "coins recovered %" at game over (2026-11-18).
        if ((pick.CompareTag("Coin1") || pick.CompareTag("Coin2") || pick.CompareTag("Coin3")) && Toolbox.instance.m_coins != null)
            Toolbox.instance.m_coins.coinsSpawned++;


        if (spawnedObject != null) { if (spawnedObject.activeInHierarchy) { isSpawned = true; Debug.Log("Instantaited Object: " + spawnedObject.name); } }
        else { isSpawned = false; }

        isColliding = false;
        isSpawned = true;
    }

    [System.Obsolete]
    void SpawnAsteroid()
    {
        int i;
        asteroidCount += 1;
        for (i = 0; i <= maxAsteroids; i++) ;
        if (asteroidCount <= maxAsteroids) 
        {
            GameObject clone = Instantiate(asteroidPrefab, asteroidFirePoint.position, asteroidFirePoint.transform.rotation, rotationCenter);
            Debug.Log ("Spawned asteroid " + i + ".");
            clone.transform.RotateAround(rotationCenter.transform.position, Random.Range(0, 361));
            clone.transform.Translate(0, asteroidSpawnDistance, 0);
            if (clonedAsteroid != null) { clonedAsteroid = clone; }
        }
        else if (asteroidCount > maxAsteroids && (clonedAsteroid != null)) { Destroy(clonedAsteroid); }
        else if (asteroidCount == 0) 
        {
            GameObject clone = Instantiate(asteroidPrefab, asteroidFirePoint.position, asteroidFirePoint.transform.rotation, rotationCenter);
            Debug.Log("Max Asteroid count reached. Spawned asteroid " + i + ".");
            clone.transform.RotateAround(rotationCenter.transform.position, Random.Range(0, 361));
            clone.transform.Translate(0, asteroidSpawnDistance, 0);
            if (clonedAsteroid != null) { clonedAsteroid = clone; }
        }
        else if (clonedAsteroid == null)
        {
            return;
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Instantiate(objectInstantiator, spawnedSpawnerParentTransform, true);
            isColliding = true;
        }
        // 2026: never switch off things the gnome can stand on (level / spawned platforms, land-able
        // junk). The spawner boxes track the gnome's height, so they used to hit the platform he
        // was about to land on and make it vanish.
        else if (!collision.gameObject.CompareTag("Floor")) { collision.gameObject.SetActive(false); }
    }
    public void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            int i;
            //Destroy(launchplatform);

            isColliding = true;
            GameObject clone = Instantiate(objectInstantiator, spawnedSpawnerParentTransform, true);
            //objectInstantiator.SetActive(false);
            Destroy(objectInstantiator);        //BIG CHANGE...CAUSING PROBLEMS?
            clone.transform.Translate(new Vector3(0, spawnerSpawnDistance, 0), Space.World);

            for (i = 0; i <= maxSpawnerCount; i++) ;
            if (i <= maxSpawnerCount) { objectInstantiator.SetActive(true); }
             //else if (i> maxSpawnerCount) { Destroy(spawnedSpawnerParentTransform.GetChild(transform.childCount -1).gameObject); }
            //^---Would be good to destroy old spawners rather than simply set to inactive.
        }
    }
}
