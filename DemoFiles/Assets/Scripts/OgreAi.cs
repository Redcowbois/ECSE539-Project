using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class OgreAi : MonoBehaviour
{
    
    OgreBehaviors behaviors;
    GameObject player;
    public TextMeshProUGUI planText;
    public string currentPlan = "";
    public bool executingStep = false;

    public bool isTreasureTaken = false;
    public bool isPlayerSeen = false;
    public bool isIdle = true;
    public bool isAtBase = true;
    public bool hasRock = false;
    public bool isAtCenter = false;

    void Start()
    {
        behaviors = gameObject.GetComponent<OgreBehaviors>();
        player = behaviors.player;
        StartCoroutine(OgreAiCoroutine());
        StartCoroutine(OgrePlanExecutorCoroutine());
    }

    private void Update()
    {
        if (behaviors.checkIfPlayerVisible())
        {
            isPlayerSeen = true;
        }
        else
        {
            isPlayerSeen = false;
        }

        if (GameManager.Instance.isTreasureTaken(behaviors.ogreId))
        {
            isTreasureTaken = true;
        }
    }

    // makes the plan
    private IEnumerator OgreAiCoroutine()
    {
        while (true)
        {
            // skip if already has a plan or executing step
            if (executingStep || currentPlan.Length > 2)
            {
                yield return null;
                continue;
            } else
            {
                CompoundTask root = TreasureDomain.BuildDomain(out WorldState world, GameManager.Instance.shrooms, GameManager.Instance.boulders, isPlayerSeen, isTreasureTaken, isIdle, isAtBase, hasRock, isAtCenter);
                Debug.Log(isPlayerSeen + " " + isTreasureTaken + " " + isIdle + " " + isAtBase + " " + hasRock + " " + GameManager.Instance.shrooms.Count + " " + GameManager.Instance.boulders.Count);

                var planner = new BacktrackingHTNPlanner();
                var plan = planner.Plan(root, world);


                if (plan == null)
                {
                    Debug.LogError("NO VALID PLAN FOUND");
                }
                else
                {
                    string overallPlan = "";
                    foreach (var step in plan)
                        if (step.Name != "")
                        {
                            overallPlan += step.Name + ";";
                        }
                    Debug.Log("PLAN: " + overallPlan);

                
                    currentPlan = (overallPlan != ";") ? overallPlan : "";
                    planText.text = gameObject.name + ": " + currentPlan;
                }

                yield return null;
            }

        }
    }

    // executes the plan
    private IEnumerator OgrePlanExecutorCoroutine()
    {
        while (true)
        {
            //skip if no plan or executing step
            if (currentPlan == "" || currentPlan == ";" || executingStep)
            {
                yield return null;
                continue;
            } else
            {
                int splitIndex = currentPlan.IndexOf(';');
                string currentAction = currentPlan.Substring(0, splitIndex);
                currentPlan = currentPlan.Substring(splitIndex + 1);

                executingStep = true;
                Debug.Log("executing action: " + currentAction);
                Debug.Log("plan without cur action: " + currentPlan);
                switch (currentAction)
                {
                    case "jump":
                        behaviors.StartJumpingSequence();
                        break;

                    case "spin":
                        behaviors.StartSpinningSequence();
                        break;

                    case "pathToShroom":
                        behaviors.StartShroomNavigation();
                        break;

                    case "goHome":
                        behaviors.ReturnToCave();
                        break;

                    case "waitAround":
                        behaviors.StartWaitingAround();
                        break;

                    case "chasePlayer":
                        behaviors.StartChasePlayer();
                        break;

                    case "pathToRock":
                        behaviors.StartRockPath();
                        break;

                    case "throwRock":
                        behaviors.StartRockThrow();
                        break;

                    case "pathToCenter":
                        behaviors.StartPathToCenter();
                        break;

                    default:
                        Debug.LogError("unknown action");
                        executingStep = false;
                        break;
                }


                yield return null;
            }

        }
    }
}
