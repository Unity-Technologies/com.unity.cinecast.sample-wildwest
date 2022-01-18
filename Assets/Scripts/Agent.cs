using System;
using System.Collections;
using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;

public class Agent : MonoBehaviour
{
    [Header("General Settings:")]
    [SerializeField] private AgentType agentType;
    public Transform mainMenuTransform;
    public Transform sessionStartTransform;
    public TrailRenderer boostTrail;

    [Header("Awareness Settings:")]
    [SerializeField] private float visibilityDistance;
    [SerializeField] private float catchRadius;
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("Movement Settings:")]
    [SerializeField] private float walkSpeed;
    [SerializeField] private float runSpeed;
    [SerializeField] private float turnSpeed;
    [SerializeField] private float stoppingDistance;
    [SerializeField] private float wanderDurationMin;
    [SerializeField] private float wanderDurationMax;
    [SerializeField] private float speedBoost;
    [SerializeField] private float speedBoostDuration;

    private bool initialised;
    private CharacterController characterController;
    private Animator animator;

    private Agent[] opponents;
    private Agent visibleOpponents;

    private Vector3 desiredDirection;
    private float nextWanderDirectionChangeTime;
    private float nextFleeDirectionChangeTime;
    private float speedBoostEndTime;

    private AgentState agentState;
    
    public float CurrentSpeed { get; private set; }
    public string CurrentTrigger { get; private set; }
    public string AgentId { get; set; }

    private void Awake()
    {
        initialised = false;
        characterController = gameObject.GetComponent<CharacterController>();
        animator = gameObject.GetComponentInChildren<Animator>();
        
        //Using the gameObject name as the identifier for each agent!
        AgentId = gameObject.name;
        CurrentSpeed = 0f;
    }
    public void StartAgent()
    {
        desiredDirection = GetRandomDirection();
        initialised = true;

        //Filling the opponents list by looping through all agents and finding the ones of the opposite type
        opponents = agentType != AgentType.Seeker ? new Agent[] { DemoManager.Instance.seeker} : DemoManager.Instance.hiders.ToArray();
    }

    public void StopAgent()
    {
        initialised = false;
    }

    //Used to reset the animator for scrubbing and pausing in playback
    public void ResetAnimator()
    {
        animator.Rebind();
        animator.Update(0f);
    }

    private void Update()
    {
        CurrentTrigger = "";
        if (!initialised || agentState == AgentState.Caught)
        {
            return;
        }
        
        switch (agentState)
        {
            case AgentState.Wandering:
                Wander();
                break;

            case AgentState.Fleeing:
                Flee();
                break;

            case AgentState.Hunting:
                Hunt();
                break;
            case AgentState.Caught:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        MoveTowards(desiredDirection);
    }

    private void Wander()
    {
        bool obstacleIsInTheWay = ObstacleInSight(desiredDirection, stoppingDistance);

        //Choosing new direction after two seconds or if something is in the way
        if (Time.time > nextWanderDirectionChangeTime || obstacleIsInTheWay)
        {
            nextWanderDirectionChangeTime = Time.time + Random.Range(wanderDurationMin, wanderDurationMax);
            desiredDirection = obstacleIsInTheWay ? FindSafeDirection() : GetRandomDirection();
        }

        visibleOpponents = GetClosestVisibleOpponent();

        if (visibleOpponents != null)
        {
            agentState = agentType == AgentType.Hider ? AgentState.Fleeing : AgentState.Hunting;
        }
    }

    private void Flee()
    {
        CinecastManager.Instance.StartFleeingPOI(AgentId);

        float stopFleeingRadius = visibilityDistance * 1.5f;
        if (Vector3.Distance(transform.position, visibleOpponents.transform.position) > stopFleeingRadius)
        {
            agentState = AgentState.Wandering;
            return;
        }

        if (Time.time > nextFleeDirectionChangeTime)
        {
            nextFleeDirectionChangeTime = Time.time + 1.5f;
            Vector3 directionAwayFromOpponent = (transform.position - visibleOpponents.transform.position).normalized;

            // Find good direction to flee in
            Vector3 bestFleeDirection = directionAwayFromOpponent;
            float bestDirectionScore = -1;
            const int numIterations = 10;
            for (int i = 0; i < numIterations; i++)
            {
                Vector3 randomDirection = GetRandomDirection();
                if (ObstacleInSight(randomDirection, visibilityDistance)) continue;
                float directionScore = Vector3.Dot(randomDirection, directionAwayFromOpponent);
                if (!(directionScore > bestDirectionScore)) continue;
                bestDirectionScore = directionScore;
                bestFleeDirection = randomDirection;
            }
            desiredDirection = bestFleeDirection;
        }

        if (ObstacleInSight(desiredDirection, stoppingDistance))
        {
            agentState = AgentState.Wandering;
        }
    }


    private void Hunt()
    {
        CinecastManager.Instance.StartHuntingPOI(AgentId);

        visibleOpponents = GetClosestVisibleOpponent();
        if (visibleOpponents == null)
        {
            agentState = AgentState.Wandering;
            return;
        }
        
        desiredDirection = (visibleOpponents.transform.position + visibleOpponents.transform.forward * visibilityDistance - transform.position).normalized;

        if (Vector3.Distance(visibleOpponents.transform.position, transform.position) < catchRadius)
        {
            SetAnimationTrigger("Attack");
            visibleOpponents.Catch();
            visibleOpponents = null;
            desiredDirection = Vector3.Cross(desiredDirection, Vector3.up) * Mathf.Sign(Random.value - 0.5f);
        }

        if (ObstacleInSight(desiredDirection, stoppingDistance))
        {
            agentState = AgentState.Wandering;
        }
    }

    private void MoveTowards(Vector3 direction)
    {
        direction.y = 0f;

        //Change speed depending on condition i.e someone is hunting me right now!
        CurrentSpeed = visibleOpponents == null ? walkSpeed : runSpeed;
        bool hasBoost = Time.time < speedBoostEndTime;
        CurrentSpeed = hasBoost ? speedBoost : CurrentSpeed;
        animator.SetFloat("Speed", CurrentSpeed);
        //Rotate towards / away from the target
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);

        //slow down when not facing target
        float speedModifier = Vector3.Dot(transform.forward, desiredDirection) < 0 ? 0 : 1;

        //Move towards / away from target
        characterController.Move(transform.forward * CurrentSpeed * speedModifier * Time.deltaTime);
        boostTrail.gameObject.SetActive(hasBoost);
    }

    //Iterating a maximum of ten times to try and find a safe direction by firing raycasts in random directions
    private Vector3 FindSafeDirection()
    {
        const int numIterations = 10;

        for (int i = 0; i < numIterations; i++)
        {
            Vector3 randomDirection = GetRandomDirection();
            if (!ObstacleInSight(randomDirection, visibilityDistance))
            {
                return randomDirection;
            }
        }

        return GetRandomDirection();
    }

    private bool ObstacleInSight(Vector3 lookDirection, float viewDistance)
    {
        Ray ray = new Ray(transform.position + transform.up * 0.5f, lookDirection);
        return Physics.Raycast(ray, viewDistance, obstacleLayerMask);

    }

    private void SetAnimationTrigger(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName)) return;
        animator.SetTrigger(triggerName);
        CurrentTrigger = triggerName;
    }

    public Agent GetClosestVisibleOpponent()
    {
        float closestDistance = visibilityDistance;
        Agent closestOpponent = null;

        for (int i = 0; i < opponents.Length; i++)
        {
            Vector3 offsetToOpponent = opponents[i].transform.position - transform.position;
            float distanceToOpponent = offsetToOpponent.magnitude;

            // Find closest opponent
            if (!(distanceToOpponent < closestDistance)) continue;
            Color debugCol = Color.red;
            Vector3 directionToOpponent = offsetToOpponent.normalized;
            // Can't see behind ourself
            if (Vector3.Dot(directionToOpponent, transform.forward) > 0.2f)
            {
                debugCol = Color.yellow;
                RaycastHit hit;
                Ray ray = new Ray(transform.position + Vector3.up * 0.5f, directionToOpponent);
                // Make sure opponent not hidden behind a wall
                if (Physics.Raycast(ray, out hit, visibilityDistance))
                {
                    Agent opponent;
                    if (hit.collider.gameObject.TryGetComponent<Agent>(out opponent))
                    {
                        if (opponent.agentState != AgentState.Caught)
                        {
                            debugCol = Color.green;
                            closestDistance = distanceToOpponent;
                            closestOpponent = opponent;
                        }
                    }
                }
            }
            Debug.DrawLine(transform.position, opponents[i].transform.position, debugCol);
        }

        return closestOpponent;

    }

    private void Catch()
    {
        agentState = AgentState.Caught;
        SetAnimationTrigger("Death");
        CurrentSpeed = 0;
        DemoManager.Instance.ActiveHiders.Remove(this);

        // Don't want seeker to get stuck on hider after catching hider
        GetComponent<CapsuleCollider>().enabled = false;
        GetComponent<CharacterController>().enabled = false;
        boostTrail.gameObject.SetActive(false);
    }

    public void PlaybackFrame(AgentData agentData)
    {
        gameObject.transform.position = agentData.position;
        gameObject.transform.rotation = agentData.rotation;
        animator.SetFloat("Speed", agentData.speed);
        bool hasBoost = Mathf.Approximately(agentData.speed, speedBoost);
        boostTrail.gameObject.SetActive(hasBoost);


        SetAnimationTrigger(agentData.trigger);
        if (CurrentTrigger == "Death")
        {
            DemoManager.Instance.ActiveHiders.Remove(this);
        }
    }

    public void Intervene()
    {
        speedBoostEndTime = Time.time + speedBoostDuration;
    }

    private Vector3 GetRandomDirection()
    {
        float randomAngle = Random.value * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
    }


    void OnDrawGizmos()
    {

        if (agentType == AgentType.Seeker)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, visibilityDistance);
        }

    }

    public AgentData GetAgentData()
    {
        AgentData agentData = new AgentData
        {
            id = AgentId,
            position = transform.position,
            rotation = transform.rotation,
            speed = CurrentSpeed,
            trigger = CurrentTrigger
        };
        return agentData;
    }
}

public enum AgentType
{
    Seeker,
    Hider
}

public enum AgentState
{
    Wandering,
    Hunting,
    Fleeing,
    Caught
}
