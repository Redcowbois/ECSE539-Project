using TMPro;
using UnityEngine;
using System;

public class PlayerManager : MonoBehaviour
{
    public int playerLife = 2;
    public float timeSinceLastCollision = -1;
    public double invisTime = 10f;
    public bool isInvis = false;

    public bool hasTreasure;


    [SerializeField] private Camera cam;
    [SerializeField] Canvas canvas;

    [SerializeField] TextMeshProUGUI lifeText;
    [SerializeField] TextMeshProUGUI invisText;
    [SerializeField] TextMeshProUGUI endText;
    [SerializeField] TextMeshProUGUI treasureText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // check i frame after collision
        if (timeSinceLastCollision > 0)
        {
            timeSinceLastCollision -= Time.deltaTime;
        } else if (timeSinceLastCollision < 0) 
        {
             timeSinceLastCollision = -1;
        }

        // update invis timer if invis
        if (isInvis) { 
            invisTime -= Time.deltaTime;

            if (invisTime <= 0) 
            { 
                isInvis = false;
                GameManager.Instance.isPlayerInvis = isInvis;
                invisTime = 0;
            }

            invisText.text = "Invis Time Left: " + Math.Round(invisTime, 2) + "s";
        }

        //active invis mode if possible
        if (Input.GetKeyDown(KeyCode.Space) && invisTime > 0)
        {
            isInvis = !isInvis;
            GameManager.Instance.isPlayerInvis = isInvis;
        }
    }

    public void hitPlayer()
    {
        playerLife--;
        lifeText.text = "Life: " + playerLife;

        if (playerLife == 0)
        {
            endText.text = "You lose!";
            endText.color = Color.red;
            endText.enabled = true;
            endText.gameObject.SetActive(true);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // detect collision with ogre or thrown rock
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log("Collision with enemy detected"); 

            if (timeSinceLastCollision <= 0)
            {
                hitPlayer();
                timeSinceLastCollision = 2;
            }
        }

        //check if treasure acquired
        if (collision.gameObject.CompareTag("Cave"))
        {
            Debug.Log("Collision with cave detected");

            hasTreasure = true;
            treasureText.gameObject.SetActive(true) ;

            if (collision.gameObject.name == "Cave1")
            {
                GameManager.Instance.isTreasure1Taken = true;
            }
            else if (collision.gameObject.name == "Cave2")
            {
                GameManager.Instance.isTreasure2Taken = true;
            }
        }

        // check if completed game
        if (collision.gameObject.name == "Spawn")
        {
            Debug.Log("Collision with spawn detected");

            if (hasTreasure) {
                endText.text = "You win!";
                endText.color = Color.green;
                endText.gameObject.SetActive(true);
            }
        }
    }
}
