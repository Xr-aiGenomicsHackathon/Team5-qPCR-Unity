using System.Collections.Generic;
using UnityEngine;

namespace Team5.qPCR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HandDryerZone : PPEHandDetectionZone
    {
        [SerializeField] private AudioSource dryerAudio;

        protected override void Awake()
        {
            base.Awake();
            if (dryerAudio != null) dryerAudio.loop = true;
        }

        protected override void Update()
        {
            base.Update();
            var shouldPlay = lesson != null &&
                             lesson.IsLessonActive && !lesson.IsWaterRunning &&
                             lesson.CurrentStep == PPELessonStep.DryHands &&
                             LeftDetected && RightDetected;
            if (dryerAudio == null) return;
            if (shouldPlay && !dryerAudio.isPlaying) dryerAudio.Play();
            if (!shouldPlay && dryerAudio.isPlaying) dryerAudio.Stop();
        }

        protected override void Report(PPEHandSide side, bool value) => lesson?.SetHandInDryer(side, value);
        protected override void ClearDetection()
        {
            base.ClearDetection();
            if (dryerAudio != null) dryerAudio.Stop();
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
