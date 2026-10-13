using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class SoapDispenserController : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Transform movingButton;
        [SerializeField] private float pressDistance = 0.012f;
        [SerializeField] private GameObject foamVisual;
        [SerializeField] private AudioSource soapAudio;

        private Vector3 restPosition;

        private void Awake()
        {
            if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
            if (movingButton == null) movingButton = transform;
            restPosition = movingButton.localPosition;
            if (foamVisual != null) foamVisual.SetActive(false);
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
            var accepted = lesson != null && lesson.TryUseSoap();
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(
                accepted ? 0.32f : 0.72f, accepted ? 0.06f : 0.12f);
            StopAllCoroutines();
            StartCoroutine(AnimatePress());
            if (!accepted) return;
            if (foamVisual != null) foamVisual.SetActive(true);
            soapAudio?.Play();
        }

        private IEnumerator AnimatePress()
        {
            var duration = 0.18f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                movingButton.localPosition = restPosition + Vector3.down *
                    (Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * pressDistance);
                yield return null;
            }
            movingButton.localPosition = restPosition;
        }

        private void OnLessonReset()
        {
            StopAllCoroutines();
            if (movingButton != null) movingButton.localPosition = restPosition;
            if (foamVisual != null) foamVisual.SetActive(false);
        }
    }
}
