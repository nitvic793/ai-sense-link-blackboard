using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Scripts.Core
{
    class Blackboard
    {
        private Dictionary<string, object> blackboardData = new Dictionary<string, object>();

        public T Get<T>(string key)
        {
            if (!blackboardData.ContainsKey(key)) return default(T);
            return (T)blackboardData[key];
        }

        public object Get(string key)
        {
            if (!blackboardData.ContainsKey(key)) return null;
            return blackboardData[key];
        }

        public void Set<T>(string key, T value)
        {
            if (!blackboardData.ContainsKey(key))
            {
                blackboardData.Add(key, value);
            }
            else
            {
                blackboardData[key] = value;
            }            
        }
    }
}
