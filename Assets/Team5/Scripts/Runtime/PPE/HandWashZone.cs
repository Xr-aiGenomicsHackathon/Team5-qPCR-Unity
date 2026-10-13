using System.Collections.Generic;
using UnityEngine;

namespace Team5.qPCR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HandWashZone : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        private readonly Dictionary<PPEHandProbe, int> overlaps = new Dictionary<PPEHandProbe, int>();

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var probe = other.GetComponentInParent<PPEHandProbe>();
            if (probe == null) return;
            overlaps.TryGetValue(probe, out var count);
            overlaps[probe] = count + 1;
            lesson?.SetHandUnderWater(probe.Side, true);
        }

        private void OnTriggerExit(Collider other)
        {
            var probe = other.GetComponentInParent<PPEHandProbe>();
            if (probe == null || !overlaps.TryGetValue(probe, out var count)) return;
            if (count > 1)
            {
                overlaps[probe] = count - 1;
                return;
            }
            overlaps.Remove(probe);
            lesson?.SetHandUnderWater(probe.Side, false);
        }

        private void OnDisable()
        {
            foreach (var probe in overlaps.Keys)
                if (probe != null) lesson?.SetHandUnderWater(probe.Side, false);
            overlaps.Clear();
        }

        private void Reset()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(0.40f, 0.25f, 0.30f);
        }
    }
}
