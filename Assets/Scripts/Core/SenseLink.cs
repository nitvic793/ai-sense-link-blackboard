using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Sense Link structure
    /// </summary>
    public class SenseLink
    {
        public Vector3 LastKnownTargetPosition { set; get; }

        public bool InPursuit { set; get; }

    }
}
