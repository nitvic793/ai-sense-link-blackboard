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

    public PersonalityEnum pers;

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

    public Dictionary<HostileBehavior, SenseLink> senseLinks = new Dictionary<HostileBehavior, SenseLink>();

    public Dictionary<HostileBehavior, bool> lineOfSightLink = new Dictionary<HostileBehavior, bool>();

    private CancellationTokenSource cancellationToken = new CancellationTokenSource();

    private Text exclamation;

    private bool isTargetInVisionCone = false;

    private const long taskUpdatePeriod = 100;

    private NavMeshAgent navMeshAgent;

    private Vector3 startPosition;

    public enum EnemyType { Archer, Brawler, Normal };

    public enum Personality { Aggressive, Passive };

    public EnemyType enemyType;

    public Personality personalityType;

    private float attackTime = 0F;

    void Start()
    {
        blackboard.Set(Constants.IsTargetInVisionCone, false);
        blackboard.Set(Constants.SuspicionMeter, 0);
        blackboard.Set(Constants.InPursuit, false);
        blackboard.Set(Constants.IsAttacking, false);
        blackboard.Set(Constants.CoolDownTime, 0);
        blackboard.Set(Constants.MyPosition, transform.position);
        blackboard.Set(Constants.MyName, name);
        currentPatrolTarget = PatrolPointB;
        currentPatrolPoint = PatrolPoint.B;
        navMeshAgent = transform.GetComponent<NavMeshAgent>();
        startPosition = transform.position;
        target = GameObject.Find("Player").transform;
        StartIntelligenceTasks();
        StartCoroutine(Constants.UpdateLineOfSightLinks);
    }


    void Update()
    {
        UpdateSensors();
        Arbiter();
    }

    /// <summary>
    /// The Arbiter of the system. Checks the blackboard for the state of the system and make decisions.
    /// </summary>
    private void Arbiter()
    {
        navMeshAgent.isStopped = false;
        if (blackboard.Get<bool>(Constants.IsAttacking))
        {
            navMeshAgent.isStopped = true;
            Attack();
        }
        else if (blackboard.Get<bool>(Constants.InPursuit))
        {
            Pursue();
        }
        else if (blackboard.Get<bool>(Constants.CheckPosition))
        {
            CheckPosition();
        }
        else if (blackboard.Get<bool>(Constants.HeardSound))
        {
            Debug.Log("I heard a sound!");
            CheckSound();
        }
        else if (IsPatrol && blackboard.Get<int>(Constants.SuspicionMeter) == 0)
        {
            Patrol();
        }
        else
        {
            navMeshAgent.isStopped = true; //Wait
        }        
    }

    /// <summary>
    /// Inflict damage on the player if within range. 
    /// </summary>
    private void Attack()
    {
        if (!IsWithinRange(target.position))
        {
            blackboard.Set(Constants.IsAttacking, false);
            return;
        }

        //Face the player object.
        Vector3 targetDir = target.position - transform.position;
        float step = 1F * Time.deltaTime;
        Vector3 newDir = Vector3.RotateTowards(transform.forward, targetDir, step, 0.0F);
        Debug.DrawRay(transform.position, newDir, Color.red);
        transform.rotation = Quaternion.LookRotation(newDir);
        var player = target.GetComponent<Player>();
        attackTime += Time.deltaTime;
        //End Face the player
        if (attackTime > 0.25f)
        {
            attackTime = 0F;
            player.InflictDamage(2F);
        }
    }

    /// <summary>
    /// Check location provided by sense links
    /// </summary>
    private void CheckPosition()
    {
        var lastPosition = blackboard.Get<Vector3>(Constants.LastKnownPosition);
        if (Vector3.Distance(lastPosition, transform.position) > 1F)
        {
            navMeshAgent.destination = lastPosition;
        }
        else
        {
            blackboard.Set(Constants.CheckPosition, false);
        }
    }

    private void CheckSound()
    {
        var lastPosition = blackboard.Get<Vector3>(Constants.SoundPosition);
        if (Vector3.Distance(lastPosition, transform.position) > 2F)
        {
            navMeshAgent.destination = lastPosition;
        }
        else
        {
            blackboard.Set(Constants.HeardSound, false);
        }
    }

    private void Patrol()
    {
        if (Vector3.Distance(currentPatrolTarget.transform.position, transform.position) > 10F)
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

    private void GoBackToStartPosition()
    {
        navMeshAgent.destination = startPosition;
    }

    private void Pursue()
    {
        navMeshAgent.destination = targetProxy.position;
        blackboard.Set(Constants.CheckPosition, false);
        navMeshAgent.speed = PursueSpeed;
    }

    private void StartIntelligenceTasks()
    {
        AddTask(DoSystemsWork);
        AddTask(DoSenseLinkWork);
        foreach (var task in tasks)
        {
            task.Start();
        }
    }

    private void AddTask(Action action, long updatePeriod = taskUpdatePeriod)
    {
        tasks.Add(new Task(async () =>
        {
            while (true)
            {
                cancellationToken.Token.ThrowIfCancellationRequested();
                lock (blackboard)
                {
                    action();
                }
                await Task.Delay(TimeSpan.FromMilliseconds(taskUpdatePeriod), cancellationToken.Token);
            }
        }, cancellationToken.Token, TaskCreationOptions.LongRunning));
    }

    private void DoSystemsWork()
    {
        blackboard.Set(Constants.IsTargetInVisionCone, isTargetInVisionCone);
        CheckPursue();
        HandleSuspicion();
        UpdatePursueTarget();
        UpdateAttack();
        UpdateCoolDown();
    }

    private void UpdateAttack()
    {
        var playerPosition = blackboard.Get<Vector3>(Constants.LastKnownPosition);
        if (IsWithinRange(playerPosition))
        {
            blackboard.Set(Constants.IsAttacking, true);
        }
        else
        {
            blackboard.Set(Constants.IsAttacking, false);
        }
    }

    private bool IsWithinRange(Vector3 position)
    {
        var myPosition = blackboard.Get<Vector3>(Constants.MyPosition);
        return (Vector3.Distance(myPosition, position) < 3.5F);
    }

    private void UpdatePursueTarget()
    {
        var suspicionValue = blackboard.Get<int>(Constants.SuspicionMeter);
        if (suspicionValue == 100)
        {
            targetProxy = target; //Pursue Target
            blackboard.Set(Constants.InPursuit, true);
        }
    }

    private void CheckPursue()
    {
        if (blackboard.Get<int>(Constants.CoolDownTime) == 0)
        {
            blackboard.Set(Constants.InPursuit, false);
            targetProxy = null;
        }
        switch (pers)
        {
            case PersonalityEnum.aggressive:
                PursueSpeed = 10F;
                break;
            case PersonalityEnum.passive:
                PursueSpeed = 4F;
                break;
            default:
                PursueSpeed = 6F;
                break;
        }
    }

    void UpdateCoolDown()
    {
        if (blackboard.Get<int>(Constants.SuspicionMeter) > 0 && blackboard.Get<bool>(Constants.InPursuit))
        {
            blackboard.Set(Constants.CoolDownTime, EnemyCoolDownTime);
        }
        else
        {
            var coolDown = blackboard.Get<int>(Constants.CoolDownTime);
            coolDown--;
            if (coolDown <= 0) coolDown = 0;
            blackboard.Set(Constants.CoolDownTime, coolDown);
        }
    }

    private void HandleSuspicion()
    {
        if (isTargetInVisionCone)
        {
            var suspicionValue = blackboard.Get<int>(Constants.SuspicionMeter);
            if (suspicionValue >= 100)
            {
                suspicionValue = 100; //Full Suspicion
            }
            else
            {
                suspicionValue += 10; //Ramp up suspicion
            }

            blackboard.Set(Constants.SuspicionMeter, suspicionValue);
        }
        else
        {
            var suspicionValue = blackboard.Get<int>(Constants.SuspicionMeter);
            if (suspicionValue > 0)
            {
                suspicionValue -= 10; //Ramp down suspicion
            }
            else
            {
                blackboard.Set(Constants.SuspicionMeter, 0);
            }

            blackboard.Set(Constants.SuspicionMeter, suspicionValue);
        }
    }

    private void UpdateSensors()
    {
        blackboard.Set(Constants.MyPosition, transform.position);
        if (isTargetInVisionCone)
        {
            blackboard.Set(Constants.LastKnownPosition, target.transform.position);
        }

        var player = GameObject.Find("Player");
        blackboard.Set(Constants.HaveDirectLosWithPlayer, HasRaycastHitPlayer(player.transform));
    }

    private void DoSenseLinkWork()
    {
        if (blackboard.Get<bool>(Constants.InPursuit))
        {
            NotifyLinksWithinRange();
        }
        else
        {
            if (blackboard.Get<bool>(Constants.CheckSenseLinks))
            {
                blackboard.Set(Constants.CheckSenseLinks, false);
                var comrade = blackboard.Get<HostileBehavior>(Constants.LastComradeLinkWrite);
                lock (senseLinks)
                {
                    SenseLink link = blackboard.Get<SenseLink>(Constants.SenseLink);
                    blackboard.Set(Constants.LastKnownPosition, link.LastKnownTargetPosition);
                    blackboard.Set(Constants.CheckPosition, true);
                }
            }
        }
    }

    private void NotifyLinksWithinRange()
    {
        foreach (var link in senseLinks)
        {
            lock (link.Key.blackboard)
            {
                var comradePosition = link.Key.blackboard.Get<Vector3>(Constants.MyPosition);
                var comrade = link.Key;
                if (Vector3.Distance(blackboard.Get<Vector3>(Constants.MyPosition), comradePosition) < 40 && lineOfSightLink[link.Key])
                {
                    link.Value.InPursuit = true;
                    link.Value.LastKnownTargetPosition = blackboard.Get<Vector3>(Constants.LastKnownPosition);
                    link.Key.blackboard.Set(Constants.CheckSenseLinks, true);
                    link.Key.blackboard.Set(Constants.LastComradeLinkWrite, link.Key);
                    link.Key.blackboard.Set(Constants.SenseLink, link.Value);
                }
            }
        }
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

    private void OnTriggerStay(Collider other)
    {
        if (other.name == "Player")
        {
            if (HasRaycastHitPlayer(other.transform)) //Only allow visibility if a ray cast hits the player object directly.
            {
                // targetProxy = other.transform;
                isTargetInVisionCone = true;
                exclamation.enabled = true;
            }
        }
    }

    private IEnumerator UpdateLineOfSightLinks()
    {
        while (true)
        {
            foreach (var link in senseLinks)
            {
                if (lineOfSightLink.ContainsKey(link.Key))
                {
                    lineOfSightLink[link.Key] = HasRaycastLink(link.Key.transform);
                }
                else
                {
                    lineOfSightLink.Add(link.Key, HasRaycastLink(link.Key.transform));
                }
            }
            yield return new WaitForSeconds(.1f);
        }
    }

    bool HasRaycastHitPlayer(Transform other)
    {
        RaycastHit hit;
        Physics.Linecast(transform.position, other.position, out hit);
        return (hit.transform?.name == "Player");
        Ray ray = new Ray(transform.position, (other.position - transform.position).normalized);
        Physics.Raycast(ray, out hit);
        if (hit.transform.name == "Player") return true;
        ray = new Ray(transform.position, (transform.position - other.position).normalized);
        Physics.Raycast(ray, out hit);
        return (hit.transform.name == "Player");
    }

    bool HasRaycastLink(Transform other)
    {
        RaycastHit hit;
        Physics.Linecast(transform.position, other.position, out hit);
        return (hit.transform?.name == other.name);
        Ray ray = new Ray(transform.position, (other.position - transform.position).normalized);
        Physics.Raycast(ray, out hit);
        if (hit.transform.name == other.name) return true;
        ray = new Ray(transform.position, (transform.position - other.position).normalized);
        Physics.Raycast(ray, out hit);
        return (hit.transform.name == other.name);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.name == "Player")
        {
            exclamation.enabled = false;
            isTargetInVisionCone = false;
        }
    }

}
