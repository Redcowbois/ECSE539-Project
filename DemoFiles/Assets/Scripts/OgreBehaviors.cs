using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.Android;
using UnityEngine.Rendering;
using static UnityEngine.GraphicsBuffer;

[RequireComponent(typeof(Rigidbody))]
public class OgreBehaviors : MonoBehaviour
{
    [Header("Jumping Settings")]
    public float jumpForce = 10f;
    public float jumpDuration = 3f;
    public float jumpInterval = 0.5f;

    [Header("Spinning Settings")]
    public float spinSpeed = 180f;
    public float spinDuration = 2f;

    [Header("Throw Settings")]
    public float throwForce = 15f;
    public float throwUpwardForce = 5f;
    public float distanceAmplifier = 2f;
    public float minForce = 5f;               
    public float maxForce = 30f;
    public float upwardForceMultiplier = 0.5f; 

    [Header("Other Settings")]
    [SerializeField] public GameObject player;
    [SerializeField] GameObject rockPoint;
    [SerializeField] private GameObject obstacleParent;
    [SerializeField] private GameObject center;
    public float visionRange = 20f;

    private Rigidbody rb;
    private NavMeshAgent agent;
    private OgreAi ogreAi;
    private Vector3 lastSeenPlayerPosition;
    public bool isLookingAtPlayer;

    private bool isJumping = false;
    private bool isSpinning = false;
    private bool isChasingPlayer = false;

    private bool isGoingToShroom = false;
    GameObject currentShroom = null;
    Coroutine shroomCheckCoroutine = null;

    private bool isThrowingRock = false;
    GameObject currentRock = null;

    public GameObject basePosition;
    public int ogreId;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        ogreAi = GetComponent<OgreAi>();
    }


    public bool checkIfPlayerVisible()
    {
        Vector3 directionToPlayer = player.transform.position - gameObject.transform.position + new Vector3(0, 1, 0);

        return Physics.Linecast(gameObject.transform.position + new Vector3(0, 1, 0), player.transform.position, out RaycastHit hit)
            && !GameManager.Instance.isPlayerInvis
            && hit.transform == player.transform
            && Vector3.Angle(transform.forward, directionToPlayer) < 120 / 2f
            && directionToPlayer.magnitude < visionRange;
    }

    private void Update()
    {

        if (checkIfPlayerVisible())
        {
            lastSeenPlayerPosition = player.transform.position;
            isLookingAtPlayer = true;
        }
        else
        {
            isLookingAtPlayer = false;
        }

        if (GameManager.Instance.isTreasureTaken(ogreId)) {
            visionRange = visionRange * 2;
        
        }
    }


    // check all other behaviors are off
    public bool isIdle()
    {
        return !(isJumping || isSpinning || isGoingToShroom || isChasingPlayer || isThrowingRock);
    }


    public void StartPathToCenter()
    {
        if (NavMesh.SamplePosition(center.transform.position, out NavMeshHit hit2, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit2.position);
        }

        StartCoroutine(PathToCenterCoroutine());
    }

    public IEnumerator PathToCenterCoroutine()
    {
        while (true)
        {
            while (agent.pathPending)
            {
                yield return null;
            }

            // wait for agent to get there
            agent.stoppingDistance += 2;
            while (agent.remainingDistance > agent.stoppingDistance)
            {
                if (isLookingAtPlayer)
                {
                    agent.stoppingDistance -= 2;
                    CompleteAction();
                    yield break;
                }

                yield return null;
            }

            agent.stoppingDistance -= 2;
            ogreAi.isAtCenter = true;
            CompleteAction();
            yield break;
        }
    }

    public void StartRockPath()
    {
        if (isIdle())
        {
            Debug.Log("Starting rock throw sequence");

            var boulders = GameManager.Instance.boulders;
            GameObject nearestRock = null;
            float shortestDistanceSqr = Mathf.Infinity;
            Vector3 currentPos = transform.position;

            foreach (GameObject rock in boulders)
            {
                if (rock == null) continue;

                Vector3 directionToTarget = rock.transform.position - currentPos;
                float dSqrToTarget = directionToTarget.sqrMagnitude;

                if (dSqrToTarget < shortestDistanceSqr)
                {
                    shortestDistanceSqr = dSqrToTarget;
                    nearestRock = rock;
                }
            }

             
            Debug.Log("Pathfinding to nearest rock: " + nearestRock);
            if (nearestRock != null)
            {
                StartCoroutine(CheckIfRockThere(nearestRock));  
                isThrowingRock = true;
                Vector3 direction = (nearestRock.transform.position - agent.transform.position).normalized;
                Vector3 searchPos = agent.transform.position + direction * 1.0f; // search 1m toward obstacle

                NavMeshHit hit;
                if (NavMesh.SamplePosition(searchPos, out hit, 1f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                ogreAi.isAtBase = false;
                ogreAi.isAtCenter = false;
                StartCoroutine(MoveTowardObstacleCoroutine(nearestRock.transform));
            }
        }
    }

    private IEnumerator MoveTowardObstacleCoroutine(Transform obstacle)
    {
        ogreAi.isAtBase = false;
        Debug.Log("Moving towards rock");
        while (true)
        {
            Vector3 direction = (obstacle.position - transform.position).normalized;

            Vector3 searchPos = transform.position + direction * 1f;

            NavMeshHit hit;

            if (NavMesh.SamplePosition(searchPos, out hit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // If no navmesh in front (rare), break
                Debug.Log("No more nav mesh");
                yield break;
            }

            if (currentRock != null)
            {
                Debug.Log("Picking up rock");
                GameManager.Instance.boulders.Remove(currentRock);
                currentRock.transform.SetParent(rockPoint.transform, true);
                currentRock.transform.position = rockPoint.transform.position;

                ogreAi.hasRock = true;
                CompleteAction();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator CheckIfRockThere(GameObject rock)
    {
        while (true)
        {
            if (rock.transform.parent != obstacleParent.transform)
            {
                currentRock = null;
                agent.ResetPath();
                CompleteAction();
                yield break;
            }

            yield return null;
        }
    }

    public void StartRockThrow()
    {
        StartCoroutine(FlyingRockCoroutine());
    }

    private IEnumerator FlyingRockCoroutine()
    {
        // Parent the rock to the spawn point temporarily
        currentRock.transform.SetParent(rockPoint.transform, true);

        // Ensure there's exactly one Rigidbody
        Rigidbody rockRB = currentRock.GetComponent<Rigidbody>();
        if (rockRB == null) rockRB = currentRock.AddComponent<Rigidbody>();

        Collider rockCol = currentRock.GetComponent<BoxCollider>();
        if (rockCol == null) rockCol = currentRock.AddComponent<BoxCollider>();

        rockRB.isKinematic = false;
        rockRB.useGravity = true;

        // Compute direction and distance to player
        Vector3 direction = (lastSeenPlayerPosition - currentRock.transform.position).normalized;
        float distance = Vector3.Distance(lastSeenPlayerPosition, currentRock.transform.position);

        // Scale the throw force
        float distanceAmplifier = 2f;      // tweak as needed
        float scaledForce = Mathf.Clamp(throwForce * Mathf.Sqrt(distance / distanceAmplifier), minForce, maxForce);

        // Optionally scale upward force with distance
        float upwardForce = throwUpwardForce + distance * upwardForceMultiplier;

        // Detach the rock to throw it
        currentRock.transform.SetParent(obstacleParent.transform, true);

        // Add upright component if needed
        if (currentRock.GetComponent<RockUpright>() == null)
            currentRock.AddComponent<RockUpright>();

        // Apply force
        rockRB.AddForce(direction * scaledForce + Vector3.up * upwardForce, ForceMode.Impulse);

        // Reset state
        currentRock = null;
        isThrowingRock = false;

        ogreAi.hasRock = false;
        CompleteAction();

        yield return null;
    }



    //player chase 
    public void StartChasePlayer()
    {
        if (isIdle())
        {
            Debug.Log("Start chase player");
            isChasingPlayer = true;
            ogreAi.isAtBase = false;
            ogreAi.isAtCenter = false;
            StartCoroutine(ChaseCoroutine());
        }
    }

    private IEnumerator ChaseCoroutine()
    {
        //Debug.Log("ray "+Physics.Linecast(gameObject.transform.position + new Vector3(0, 1, 0), player.transform.position, out RaycastHit hit3));
        //Debug.DrawLine(gameObject.transform.position + new Vector3(0, 1, 0), player.transform.position, Color.magenta, 10000f);
        //Debug.Log("invis " + !GameManager.Instance.isPlayerInvis);
        //Debug.Log("hit player " + (hit3.transform == player.transform));
        //Debug.Log("hit player: " + hit3.transform);
        //Debug.Log("distance " + ((gameObject.transform.position - player.transform.position).magnitude < 20));
        //Debug.Log("distance: " + (gameObject.transform.position - player.transform.position).magnitude);
        // if within sight line and 20m and not invis

        while (isChasingPlayer)
        {
            if (checkIfPlayerVisible())
            {
                if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit hit2, 2f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit2.position);
                }
            } 
            else
            {
                agent.ResetPath();
                isChasingPlayer = false;
                CompleteAction();
                yield break;
            }
            
            yield return null;
        }
    }


    // shroom eating behavior
    public void StartShroomNavigation()
    {
        if (isIdle())
        {
            int shroomNo = Random.Range(0, GameManager.Instance.shrooms.Count);
            GameObject shroom = GameManager.Instance.shrooms[shroomNo];

            isGoingToShroom = true;
            currentShroom = shroom;
            ogreAi.isAtBase = false;
            agent.SetDestination(shroom.transform.position);

            shroomCheckCoroutine = StartCoroutine(CheckIfShroomUneaten(shroom));
        }  
    }

    private IEnumerator CheckIfShroomUneaten(GameObject shroom)
    {
        while (true)
        {
            if (!shroom.activeSelf)
            {
                currentShroom = null;
                agent.ResetPath();
                CompleteAction();
                yield break;
            }

            if (isLookingAtPlayer)
            {
                isGoingToShroom = false;
                currentShroom = null;
                CompleteAction();
                yield break;
            }
            else if (ogreAi.isTreasureTaken)
            {
                isGoingToShroom = false;
                currentShroom = null;
                CompleteAction();
                yield break;
            }

            yield return null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        //when eat shroom
        if (other.gameObject == currentShroom)
        {
            isGoingToShroom = false;
            GameManager.Instance.shrooms.Remove(currentShroom);
            currentShroom.SetActive(false);
            currentShroom=null;
            Debug.Log("At shroom");
            StopCoroutine(shroomCheckCoroutine);
            shroomCheckCoroutine = null;
            CompleteAction();
        }
    }

    public void ReturnToCave()
    {
        if (isIdle())
        {
            agent.SetDestination(basePosition.transform.position);
            StartCoroutine(HasReturnedToCave());
        }
    }

    public IEnumerator HasReturnedToCave()
    {
        // wait for path computation
        while (agent.pathPending) {
            yield return null;
        }

        // wait for agent to get there
        while (agent.remainingDistance > agent.stoppingDistance)
        {
            if (isLookingAtPlayer)
            {
                CompleteAction();
                yield break;
            }
            else if (ogreAi.isTreasureTaken)
            {
                CompleteAction();
                yield break;
            }
            else
            {
                yield return null;
            }
        }

        if (!isLookingAtPlayer)
        {
            Debug.Log("Returned to cave");
            ogreAi.isAtBase = true;
        } 
        CompleteAction();
    }

    public void StartWaitingAround()
    {
        if (isIdle())
        {
            StartCoroutine(WaitingAroundCoroutine());
        }
    }

    public IEnumerator WaitingAroundCoroutine()
    {
        yield return new WaitForSeconds(2);

        CompleteAction();
    }


    // jumping code
    public void StartJumpingSequence()
    {
        if (isIdle())
        {
            StartCoroutine(JumpCoroutine());
        }
    }

    private IEnumerator JumpCoroutine()
    {
        Debug.Log("Ogre is starting its jump sequence!");
        agent.enabled = false;
        isJumping = true;
        float startTime = Time.time;

        // The overall behavior will still last for jumpDuration
        while (Time.time < startTime + jumpDuration)
        {
            // Only jump if the Ogre is currently on the ground
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f))
            {
                if (isLookingAtPlayer)
                {
                    agent.enabled = true;
                    isJumping = false;
                    CompleteAction();
                    yield break;
                }

                //Debug.Log("trying to jump");
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }

            // Wait until the next frame before checking again
            yield return new WaitUntil(() => Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f)); ;
        }

        Debug.Log("Ogre has finished jumping.");
        agent.enabled = true;
        isJumping = false;
        CompleteAction();
    }

    // spinning code
    public void StartSpinningSequence()
    {
        if (isIdle())
        {
            StartCoroutine(SpinCoroutine());
        }
    }

    private IEnumerator SpinCoroutine()
    {
        Debug.Log("Ogre is starting to spin!");

        isSpinning = true;
        float startTime = Time.time;

        while (Time.time < startTime + spinDuration)
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);

            if (isLookingAtPlayer)
            {
                isSpinning = false;
                CompleteAction();
                yield break;
            }

            yield return null;
        }

        Debug.Log("Ogre has finished spinning.");
        isSpinning = false;
        CompleteAction();
    }
    private void OnCollisionEnter(Collision collision)
    {
        // found rock to throw
        if (collision.gameObject.CompareTag("Rock") && isThrowingRock)
        {
            currentRock = collision.gameObject;
        }
    }

    private void CompleteAction()
    {
        ogreAi.executingStep = false;
    }
}