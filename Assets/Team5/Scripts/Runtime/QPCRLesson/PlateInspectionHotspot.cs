using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    public sealed class PlateInspectionHotspot : MonoBehaviour
    {
        private static readonly Color CurrentBlue = new Color(0.12f, 0.42f, 0.92f, 1f);
        private static readonly Color CompleteGreen = new Color(0.16f, 0.72f, 0.38f, 1f);
        private static readonly Color AttentionAmber = new Color(1f, 0.66f, 0.12f, 1f);
        private static readonly Color ErrorRed = new Color(0.94f, 0.18f, 0.22f, 1f);

        [SerializeField] private PlateInspectionKind kind;
        [SerializeField] private PlateInspectionController controller;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private Renderer[] feedbackRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private GameObject floatingLabel;

        private bool isComplete;

        public PlateInspectionKind Kind => kind;

        public void Configure(PlateInspectionKind inspectionKind, PlateInspectionController inspectionController,
            XRSimpleInteractable simpleInteractable, Collider collider, Renderer[] renderers, GameObject label)
        {
            kind = inspectionKind;
            controller = inspectionController;
            interactable = simpleInteractable;
            targetCollider = collider;
            feedbackRenderers = renderers ?? System.Array.Empty<Renderer>();
            floatingLabel = label;
        }

        private void Awake()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
            if (targetCollider == null)
                targetCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            interactable?.selectEntered.AddListener(OnSelected);
            interactable?.hoverEntered.AddListener(OnHoverEntered);
            interactable?.hoverExited.AddListener(OnHoverExited);
        }

        private void OnDisable()
        {
            interactable?.selectEntered.RemoveListener(OnSelected);
            interactable?.hoverEntered.RemoveListener(OnHoverEntered);
            interactable?.hoverExited.RemoveListener(OnHoverExited);
        }

        public void RefreshAvailability(PlateInspectionKind current, bool completed)
        {
            isComplete = completed;
            var isCurrent = current == kind && !completed;
            // Plate ID, A1, seal and bubbles are visual laboratory checks. The
            // learner follows the pointer, observes the plate and confirms the
            // finding on the lesson card instead of physically touching labels.
            if (targetCollider != null)
                targetCollider.enabled = false;
            if (interactable != null)
                interactable.enabled = false;
            if (floatingLabel != null)
                floatingLabel.SetActive(isCurrent || completed);
            ApplyColor(completed ? CompleteGreen : isCurrent ? CurrentBlue : AttentionAmber);
        }

        public void MarkComplete()
        {
            isComplete = true;
            ApplyColor(CompleteGreen);
        }

        public void ResetHotspot()
        {
            isComplete = false;
            ApplyColor(AttentionAmber);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            var inputInteractor = args.interactorObject as XRBaseInputInteractor;
            var result = controller == null
                ? InspectionAttemptResult.Rejected(InspectionAttemptOutcome.LessonNotReady, "Plate inspection is not configured.")
                : controller.TryInspect(kind, args.interactorObject as IXRSelectInteractor);

            if (result.IsSuccess)
            {
                inputInteractor?.SendHapticImpulse(0.32f, 0.06f);
                ApplyColor(CompleteGreen);
            }
            else if (result.Outcome != InspectionAttemptOutcome.Throttled)
            {
                inputInteractor?.SendHapticImpulse(0.72f, 0.12f);
                StopAllCoroutines();
                StartCoroutine(FlashError());
            }
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!isComplete)
                ApplyColor(CurrentBlue);
            (args.interactorObject as XRBaseInputInteractor)?.SendHapticImpulse(0.08f, 0.025f);
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (!isComplete)
                ApplyColor(controller != null && controller.CurrentInspection == kind ? CurrentBlue : AttentionAmber);
        }

        private IEnumerator FlashError()
        {
            ApplyColor(ErrorRed);
            yield return new WaitForSecondsRealtime(0.55f);
            ApplyColor(isComplete ? CompleteGreen : CurrentBlue);
        }

        private void ApplyColor(Color color)
        {
            foreach (var target in feedbackRenderers)
            {
                if (target == null)
                    continue;
                if (target is LineRenderer line)
                {
                    line.startColor = color;
                    line.endColor = color;
                    continue;
                }

                var block = new MaterialPropertyBlock();
                target.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                block.SetColor("_EmissionColor", color * 0.35f);
                target.SetPropertyBlock(block);
            }
        }
    }
}
