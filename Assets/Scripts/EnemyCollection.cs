using Assets.Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCollection : MonoBehaviour
{
    private Dictionary<HashSet<string>, SenseLink> enemyDictionary;
    void Start()
    {
        
        enemyDictionary = new Dictionary<HashSet<string>, SenseLink>(HashSet<string>.CreateSetComparer());
        var enemies = GetComponentsInChildren<HostileBehavior>();
        foreach (var enemy in enemies)
        {
            foreach (var e in enemies)
            {
                if (enemy != e)
                {
                    var set = new HashSet<string>(new[] { e.name, enemy.name });
                    var link = new SenseLink();
                    if (!enemyDictionary.ContainsKey(set))
                    {
                        enemyDictionary.Add(set, link);
                        e.senseLinks.Add(enemy, link);
                        enemy.senseLinks.Add(e, link);
                    }                    
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
