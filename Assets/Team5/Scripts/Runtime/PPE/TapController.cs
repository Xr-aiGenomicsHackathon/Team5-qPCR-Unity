using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class TapController : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private ParticleSystem waterParticles;
        [SerializeField] private AudioSource waterAudio;
        [SerializeField] private AudioSource clickAudio;
        [SerializeField] private Transform movingHandle;
        [SerializeField] private Vector3 onLocalEulerOffset = new Vector3(0f, 0f, -35f);

        private Quaternion handleOffRotation;

        private void Awake()
        {
            if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
            if (movingHandle == null) movingHandle = transform;
            handleOffRotation = movingHandle.localRotation;
            ApplyWaterState(false);
        }

        private void OnEnable()
        {
            interactable?.selectEntered.AddListener(OnSelected);
            if (lesson != null) lesson.ResetRequested += OnLessonReset;
        }

        private void OnDisable()
        {
            interactable?.selectEntered.RemoveListener(OnSelected);
            if (lesson != null) lesson.ResetRequested -= OnLessonReset;
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            if (lesson == null) return;
            var desiredState = !lesson.IsWaterRunning;
            var accepted = lesson.TrySetTap(desiredState);
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(
                accepted ? 0.35f : 0.75f, accepted ? 0.06f : 0.12f);
            if (!accepted) return;
            ApplyWaterState(desiredState);
            clickAudio?.Play();
        }

        private void ApplyWaterState(bool running)
        {
            if (waterParticles != null)
            {
                if (running && !waterParticles.isPlaying) waterParticles.Play();
                if (!running) waterParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (waterAudio != null)
            {
                waterAudio.loop = true;
                if (running && !waterAudio.isPlaying) waterAudio.Play();
                if (!running) waterAudio.Stop();
            }

            if (movingHandle != null)
                movingHandle.localRotation = running
                    ? handleOffRotation * Quaternion.Euler(onLocalEulerOffset)
                    : handleOffRotation;
        }

        private void OnLessonReset()
        {
            ApplyWaterState(false);
        }
    }
}
