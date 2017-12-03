using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class HostileBehavior : MonoBehaviour
{
    private enum PatrolPoint { A, B };

    public Guid InstanceID = Guid.NewGuid();

    public Transform target = null;

    public bool IsPatrol = true;

    public float PursueSpeed = 10F;

    public int EnemyCoolDownTime = 100;

    public Transform PatrolPointA = null;
    public Transform PatrolPointB = null;
    private Transform currentPatrolTarget = null;
    private PatrolPoint currentPatrolPoint;
    private Transform targetProxy = null;

    public Blackboard blackboard = new Blackboard();

    private List<Task> tasks = new List<Task>();

    private Dictionary<string, SenseLink> senseLinks = new Dictionary<string, SenseLink>();

    private CancellationTokenSource cancellationToken = new CancellationTokenSource();

    private Text exclamation;

    private bool isTargetInVisionCone = false;

    private const long taskUpdatePeriod = 100;

    private NavMeshAgent navMeshAgent;

    void Start()
    {
        blackboard.Set("IsTargetInVisionCone", false);
        blackboard.Set("SuspicionMeter", 0);
        blackboard.Set("InPursuit", false);
        blackboard.Set("CoolDownTime", 0);
        currentPatrolTarget = PatrolPointB;
        currentPatrolPoint = PatrolPoint.B;
        navMeshAgent = transform.GetComponent<NavMeshAgent>();
        StartIntelligenceTasks();
    }

    void Update()
    {
        UpdateLastKnownTargetPosition();
        Arbiter();
    }

    private void Arbiter()
    {
        if (blackboard.Get<bool>("InPursuit"))
        {
            Pursue();
        }
        else if (IsPatrol)
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (Vector3.Distance(currentPatrolTarget.transform.position, transform.position) > 1F)
        {
            navMeshAgent.destination = currentPatrolTarget.position;
        }
        else
        {
            if (currentPatrolPoint == PatrolPoint.B)
            {
                currentPatrolPoint = PatrolPoint.A;
                currentPatrolTarget = PatrolPointA;
            }
            else
            {
                currentPatrolPoint = PatrolPoint.B;
                currentPatrolTarget = PatrolPointB;
            }
        }
    }

    private void Pursue()
    {
        navMeshAgent.destination = targetProxy.position;
    }

    private void StartIntelligenceTasks()
    {
        tasks.Add(new Task(async () =>
        {
            while (true)
            {
                cancellationToken.Token.ThrowIfCancellationRequested();
                lock (blackboard)
                {
                    DoSystemsWork();
                }
                await Task.Delay(TimeSpan.FromMilliseconds(taskUpdatePeriod), cancellationToken.Token);
            }
        }, cancellationToken.Token, TaskCreationOptions.LongRunning));

        tasks.Add(new Task(async () =>
        {
            while (true)
            {
                cancellationToken.Token.ThrowIfCancellationRequested();
                lock (blackboard)
                {
                    DoSenseLinkWork();
                }
                await Task.Delay(TimeSpan.FromMilliseconds(taskUpdatePeriod), cancellationToken.Token);
            }
        }, cancellationToken.Token, TaskCreationOptions.LongRunning));

        foreach (var task in tasks)
        {
            task.Start();
        }
    }

    private void DoSystemsWork()
    {
        blackboard.Set("IsTargetInVisionCone", isTargetInVisionCone);
        CheckPursue();
        HandleSuspicion();
        UpdatePursueTarget();
        UpdateCoolDown();
    }

    private void UpdatePursueTarget()
    {
        var suspicionValue = blackboard.Get<int>("SuspicionMeter");
        if (suspicionValue == 100)
        {
            targetProxy = target; //Pursue Target
            blackboard.Set("InPursuit", true);
        }
    }

    private void CheckPursue()
    {
        if(blackboard.Get<int>("CoolDownTime") == 0)
        {
            blackboard.Set("InPursuit", false);
            targetProxy = null;
        }
    }

    void UpdateCoolDown()
    {
        if (blackboard.Get<int>("SuspicionMeter") > 0)
        {
            blackboard.Set("CoolDownTime", EnemyCoolDownTime);
        }
        else
        {
            Debug.Log("Pursue Cool Down: " + blackboard.Get<int>("CoolDownTime"));
            var coolDown = blackboard.Get<int>("CoolDownTime");
            coolDown--;
            if (coolDown <= 0) coolDown = 0;
            blackboard.Set("CoolDownTime", coolDown);
        }
    }

    private void HandleSuspicion()
    {
        if (isTargetInVisionCone)
        {
            var suspicionValue = blackboard.Get<int>("SuspicionMeter");
            if (suspicionValue >= 100)
            {
                suspicionValue = 100; //Full Suspicion
            }
            else
            {
                suspicionValue += 10; //Ramp up suspicion
            }

            blackboard.Set("SuspicionMeter", suspicionValue);
        }
        else
        {
            var suspicionValue = blackboard.Get<int>("SuspicionMeter");
            if (suspicionValue > 0)
            {
                suspicionValue -= 10; //Ramp down suspicion
            }
            else
            {
                blackboard.Set("SuspicionMeter", 0);
            }

            blackboard.Set("SuspicionMeter", suspicionValue);
        }
    }

    private void UpdateLastKnownTargetPosition()
    {
        if (isTargetInVisionCone)
        {
            blackboard.Set("LastKnownPosition", target.transform.position);
        }
    }

    private void DoSenseLinkWork()
    {
        if (blackboard.Get<bool>("InPursuit"))
        {
            NotifyLinksWithinRange();
        }
    }

    private void NotifyLinksWithinRange()
    {

    }

    private void OnDestroy()
    {
        cancellationToken.Cancel();
    }

    void Awake()
    {
        exclamation = this.transform.Find("ExCanvas/ExclamationText").GetComponent<Text>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.name == "Player")
        {
            if (HasRaycastHitPlayer(other.transform)) //Only allow visibility if a ray cast hits the player object directly.
            {
               // targetProxy = other.transform;
                target = other.transform;
                isTargetInVisionCone = true;
                exclamation.enabled = true;
            }
        }
    }

    bool HasRaycastHitPlayer(Transform other)
    {
        RaycastHit hit;
        Physics.Linecast(transform.position, other.position, out hit);
        return (hit.transform.name == "Player");
        Ray ray = new Ray(transform.position, (other.position - transform.position).normalized);
        Physics.Raycast(ray, out hit);
        if (hit.transform.name == "Player") return true;
        ray = new Ray(transform.position, (transform.position - other.position).normalized);
        Physics.Raycast(ray, out hit);
        return (hit.transform.name == "Player");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.name == "Player")
        {
            target = null;
            exclamation.enabled = false;
            isTargetInVisionCone = false;
        }
    }

}
