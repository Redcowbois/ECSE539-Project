using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public bool isTreasure1Taken = false;
    public bool isTreasure2Taken = false;
    public bool isPlayerInvis = true;
    public List<GameObject> shrooms;
    public List<GameObject> boulders;

    public GameObject player;
    public GameObject ogre1;
    public GameObject ogre2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); 
        }
        else
        {
            Instance = this; 
            DontDestroyOnLoad(gameObject);
        }
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public bool isTreasureTaken(int treasureId)
    {
        if (treasureId == 1)
        {
            return isTreasure1Taken;
        } else
        {
            return isTreasure2Taken;
        }
    }
}
