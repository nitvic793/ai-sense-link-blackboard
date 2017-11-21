using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class HostileBehavior : MonoBehaviour
{
    public Guid InstanceID = Guid.NewGuid();

    public Transform target = null;

    private Blackboard blackboard = new Blackboard();

    private List<Task> tasks = new List<Task>();

    private Dictionary<string, SenseLink> senseLinks = new Dictionary<string, SenseLink>();

    private CancellationTokenSource cancellationToken = new CancellationTokenSource();

    private const long taskUpdatePeriod = 1000;

    // Use this for initialization
    void Start()
    {
        blackboard.Set("IsCloseToTarget", false);
        StartIntelligenceTasks();
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(blackboard.Get<bool>("IsCloseToTarget"));
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
        blackboard.Set("IsCloseToTarget", true);
    }

    private void DoSenseLinkWork()
    {

    }

    private void OnDestroy()
    {
        cancellationToken.Cancel();
    }
}
