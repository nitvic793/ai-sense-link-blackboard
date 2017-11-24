using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class HostileBehavior : MonoBehaviour
{
    public Guid InstanceID = Guid.NewGuid();

    public Transform target = null;

    public Blackboard blackboard = new Blackboard();

    private List<Task> tasks = new List<Task>();

    private Dictionary<string, SenseLink> senseLinks = new Dictionary<string, SenseLink>();

    private CancellationTokenSource cancellationToken = new CancellationTokenSource();

    private Text exclamation;

    private bool isTargetInVisionCone = false;

    private const long taskUpdatePeriod = 100;

    void Start()
    {
        blackboard.Set("IsTargetInVisionCone", false);
        blackboard.Set("SuspicionMeter", 0);
        StartIntelligenceTasks();
    }

    void Update()
    {
        UpdateLastKnownTargetPosition();
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
        Debug.Log("Suspicion: " + blackboard.Get<int>("SuspicionMeter"));
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
        target = other.transform;
        isTargetInVisionCone = true;
        exclamation.enabled = true;
    }

    void OnTriggerExit(Collider other)
    {
        target = null;
        exclamation.enabled = false;
        isTargetInVisionCone = false;
    }

}
