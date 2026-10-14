using System.Collections.Generic;
using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>Counts colliders, not trigger messages. Stay repairs detection after resets.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public abstract class PPEHandDetectionZone : MonoBehaviour
    {
        [SerializeField] protected PPELessonController lesson;
        private readonly Dictionary<Collider, PPEHandProbe> contacts = new Dictionary<Collider, PPEHandProbe>();
        private readonly List<Collider> stale = new List<Collider>();
        public bool LeftDetected { get; private set; }
        public bool RightDetected { get; private set; }
        protected virtual void Awake() => GetComponent<BoxCollider>().isTrigger = true;
        protected virtual void OnEnable() { if (lesson != null) lesson.ResetRequested += ClearDetection; }
        protected virtual void OnDisable()
        {
            if (lesson != null) lesson.ResetRequested -= ClearDetection;
            ClearDetection();
        }
        private void OnTriggerEnter(Collider other) => Observe(other);
        private void OnTriggerStay(Collider other) => Observe(other);
        private void OnTriggerExit(Collider other) { contacts.Remove(other); Publish(); }
        private void Observe(Collider other)
        {
            var probe = other.GetComponentInParent<PPEHandProbe>();
            if (probe == null || !probe.isActiveAndEnabled) return;
            contacts[other] = probe;
            Publish();
        }
        protected virtual void Update()
        {
            stale.Clear();
            foreach (var pair in contacts)
                if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy ||
                    pair.Value == null || !pair.Value.isActiveAndEnabled) stale.Add(pair.Key);
            foreach (var collider in stale) contacts.Remove(collider);
            if (stale.Count > 0) Publish();
        }
        protected virtual void ClearDetection()
        {
            contacts.Clear(); LeftDetected = false; RightDetected = false;
            Report(PPEHandSide.Left, false); Report(PPEHandSide.Right, false);
        }
        private void Publish()
        {
            LeftDetected = false; RightDetected = false;
            foreach (var probe in contacts.Values)
                if (probe != null && probe.isActiveAndEnabled)
                { if (probe.Side == PPEHandSide.Left) LeftDetected = true; else RightDetected = true; }
            Report(PPEHandSide.Left, LeftDetected); Report(PPEHandSide.Right, RightDetected);
        }
        protected abstract void Report(PPEHandSide side, bool detected);
    }
}
