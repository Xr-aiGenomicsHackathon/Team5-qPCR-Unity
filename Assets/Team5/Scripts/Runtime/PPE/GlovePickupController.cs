using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class GlovePickupController : MonoBehaviour
    {
        [SerializeField] private PPELessonController lesson;
        [SerializeField] private HandAppearanceController handAppearance;
        [SerializeField] private PPEHandSide gloveSide;
        [SerializeField] private XRSimpleInteractable interactable;
        [Tooltip("Assign the matching Left Controller or Right Controller root for reliable hand checking.")]
        [SerializeField] private Transform expectedControllerRoot;
        [SerializeField] private Renderer[] pickupRenderers = Array.Empty<Renderer>();
        [SerializeField] private Collider[] pickupColliders = Array.Empty<Collider>();
        [SerializeField] private AudioSource gloveAudio;

        private void Awake()
        {
            if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
            if (pickupRenderers == null || pickupRenderers.Length == 0)
                pickupRenderers = GetComponentsInChildren<Renderer>(true);
            if (pickupColliders == null || pickupColliders.Length == 0)
                pickupColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            interactable?.selectEntered.AddListener(OnSelected);
            if (lesson != null) lesson.ResetRequested += ResetPickup;
        }

        private void OnDisable()
        {
            interactable?.selectEntered.RemoveListener(OnSelected);
            if (lesson != null) lesson.ResetRequested -= ResetPickup;
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            var inputInteractor = args.interactorObject as XRBaseInputInteractor;
            if (!InteractorMatchesExpectedHand(args.interactorObject?.transform))
            {
                lesson?.ReportRejectedAction($"Use your {gloveSide.ToString().ToLowerInvariant()} hand for this glove.");
                inputInteractor?.SendHapticImpulse(0.75f, 0.12f);
                return;
            }

            var accepted = lesson != null && lesson.TryWearGlove(gloveSide);
            inputInteractor?.SendHapticImpulse(accepted ? 0.4f : 0.75f, accepted ? 0.08f : 0.12f);
            if (!accepted) return;

            handAppearance?.SetGloved(gloveSide, true);
            if (gloveAudio != null) gloveAudio.Play();
            SetPickupVisible(false);
        }

        private bool InteractorMatchesExpectedHand(Transform interactorTransform)
        {
            if (interactorTransform == null) return false;
            if (expectedControllerRoot != null)
                return interactorTransform == expectedControllerRoot ||
                       interactorTransform.IsChildOf(expectedControllerRoot);

            for (var current = interactorTransform; current != null; current = current.parent)
            {
                var containsLeft = current.name.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0;
                var containsRight = current.name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0;
                if (containsLeft) return gloveSide == PPEHandSide.Left;
                if (containsRight) return gloveSide == PPEHandSide.Right;
            }
            return false;
        }

        private void SetPickupVisible(bool visible)
        {
            if (pickupRenderers != null)
                foreach (var item in pickupRenderers) if (item != null) item.enabled = visible;
            if (pickupColliders != null)
                foreach (var item in pickupColliders) if (item != null) item.enabled = visible;
            if (interactable != null) interactable.enabled = visible;
        }

        public void ResetPickup()
        {
            SetPickupVisible(true);
            handAppearance?.ResetHands();
        }
    }
}
