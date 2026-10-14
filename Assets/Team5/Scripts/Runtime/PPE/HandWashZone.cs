using System.Collections.Generic;
using UnityEngine;

namespace Team5.qPCR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HandWashZone : PPEHandDetectionZone
    {
        protected override void Report(PPEHandSide side, bool value) => lesson?.SetHandUnderWater(side, value);

        private void Reset()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(0.40f, 0.26f, 0.30f);
        }
    }
}
