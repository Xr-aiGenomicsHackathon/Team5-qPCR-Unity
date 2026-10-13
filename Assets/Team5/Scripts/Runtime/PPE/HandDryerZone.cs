using System.Collections.Generic;
using UnityEngine;

namespace Team5.qPCR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HandDryerZone : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private AudioSource dryerAudio;
        private readonly Dictionary<PPEHandProbe, int> overlaps = new Dictionary<PPEHandProbe, int>();

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (dryerAudio != null) dryerAudio.loop = true;
        }

        private void Update()
        {
            var shouldPlay = lesson != null &&
                             lesson.CurrentStep == PPELessonStep.DryHands &&
                             overlaps.Count > 0;
            if (dryerAudio == null) return;
            if (shouldPlay && !dryerAudio.isPlaying) dryerAudio.Play();
            if (!shouldPlay && dryerAudio.isPlaying) dryerAudio.Stop();
        }

        private void OnTriggerEnter(Collider other)
        {
            var probe = other.GetComponentInParent<PPEHandProbe>();
            if (probe == null) return;
            overlaps.TryGetValue(probe, out var count);
            overlaps[probe] = count + 1;
            lesson?.SetHandInDryer(probe.Side, true);
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
            lesson?.SetHandInDryer(probe.Side, false);
        }

        private void OnDisable()
        {
            foreach (var probe in overlaps.Keys)
                if (probe != null) lesson?.SetHandInDryer(probe.Side, false);
            overlaps.Clear();
            dryerAudio?.Stop();
        }

        private void Reset()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(0.38f, 0.25f, 0.30f);
        }
    }
}
