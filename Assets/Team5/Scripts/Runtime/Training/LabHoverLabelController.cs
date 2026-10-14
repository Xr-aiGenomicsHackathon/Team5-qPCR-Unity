using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    [Serializable]
    public sealed class LabLabelView
    {
        public GameObject Panel;
        public TMP_Text Title;
        public TMP_Text Purpose;
    }

    public sealed class LabHoverLabelController : MonoBehaviour
    {
        [SerializeField] private NearFarInteractor[] pointers;
        [SerializeField] private Transform[] fallbackAim;
        [SerializeField] private Transform rig;
        [SerializeField] private LabLabelView[] views;
        [SerializeField] private Transform[] identificationDots;
        [SerializeField] private PPELessonController preparation;
        [SerializeField] private LayerMask labelMask = ~0;
        [SerializeField, Min(0)] private float dwellSeconds = .2f;
        [SerializeField, Min(.1f)] private float maximumDistance = 3f;
        private readonly RaycastHit[] hits = new RaycastHit[128];
        private readonly LabLabelTarget[] candidates = new LabLabelTarget[2];
        private readonly LabLabelTarget[] visible = new LabLabelTarget[2];
        private readonly float[] dwell = new float[2];
        public LabLabelTarget VisibleTarget(int hand) => hand >= 0 && hand < 2 ? visible[hand] : null;
        private void Awake()
        {
            var layer = LayerMask.NameToLayer("LabLabelsOnly");
            if (layer >= 0) for (var other = 0; other < 32; other++) Physics.IgnoreLayerCollision(layer, other, true);
        }
        private void LateUpdate()
        {
            if (preparation != null && preparation.IsLessonActive &&
                (preparation.CurrentStep == PPELessonStep.WetHands || preparation.CurrentStep == PPELessonStep.RinseHands ||
                 preparation.CurrentStep == PPELessonStep.RubHands || preparation.CurrentStep == PPELessonStep.DryHands))
            { Hide(0); Hide(1); return; }
            for (var hand = 0; hand < 2; hand++)
            {
                var pointer = pointers != null && hand < pointers.Length ? pointers[hand] : null;
                var aim = pointer != null && pointer.farInteractionCaster != null ? pointer.curveOrigin :
                    fallbackAim != null && hand < fallbackAim.Length ? fallbackAim[hand] : null;
                if (aim == null || !aim.gameObject.activeInHierarchy || (pointer != null && !pointer.isActiveAndEnabled))
                { Hide(hand); continue; }
                var overUi = pointer != null && pointer.TryGetCurrentUIRaycastResult(out var uiHit) && uiHit.gameObject != null && uiHit.gameObject.activeInHierarchy;
                UpdatePointer(hand, new Ray(aim.position, aim.forward), overUi, Time.unscaledDeltaTime);
            }
        }
        public void UpdatePointer(int hand, Ray ray, bool overUi, float delta)
        {
            if (hand < 0 || hand >= 2) return;
            if (overUi) { Hide(hand); return; }
            var target = FindTarget(ray, out var hitPoint);
            if (target == null || !target.IsAvailable) { Hide(hand); return; }
            if (candidates[hand] != target)
            {
                Hide(hand); candidates[hand] = target; dwell[hand] = 0;
            }
            dwell[hand] += Mathf.Max(0, delta);
            if (dwell[hand] < dwellSeconds) return;
            visible[hand] = target;
            if (identificationDots != null && hand < identificationDots.Length && identificationDots[hand] != null)
            {
                var dot = identificationDots[hand]; dot.gameObject.SetActive(true);
                dot.position = hitPoint - ray.direction.normalized * .006f;
            }
            if (views == null || hand >= views.Length || views[hand] == null || views[hand].Panel == null) return;
            var view = views[hand];
            view.Panel.SetActive(true);
            if (view.Title != null) { view.Title.richText = false; view.Title.text = target.DisplayName; }
            if (view.Purpose != null) { view.Purpose.richText = false; view.Purpose.text = target.Purpose; }
            view.Panel.transform.position = target.LabelPosition(hitPoint) + Vector3.up * .12f;
            if (Camera.main != null)
            {
                var direction = view.Panel.transform.position - Camera.main.transform.position;
                if (direction.sqrMagnitude > .001f) view.Panel.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                view.Panel.transform.localScale = Vector3.one * (.00055f * Mathf.Clamp(direction.magnitude, 1f, 1.6f));
                // Put each hand's card beside its target, keeping two nearby cards apart.
                var rect = view.Panel.GetComponent<RectTransform>();
                var width = rect != null ? rect.rect.width * view.Panel.transform.lossyScale.x : .34f;
                view.Panel.transform.position += Camera.main.transform.right * ((hand == 0 ? -1f : 1f) * (width * .55f + .02f));
            }
        }
        public LabLabelTarget FindTarget(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = ray.origin;
            var count = Physics.RaycastNonAlloc(ray, hits, maximumDistance, labelMask, QueryTriggerInteraction.Collide);
            var nearest = float.PositiveInfinity;
            LabLabelTarget result = null;
            for (var index = 0; index < count; index++)
            {
                var hit = hits[index]; var collider = hit.collider;
                if (collider == null || (rig != null && collider.transform.IsChildOf(rig))) continue;
                var proxy = collider.GetComponent<LabLabelProxy>();
                // Handwashing/selection triggers must not mask the real model.
                if (collider.isTrigger && proxy == null) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance; hitPoint = hit.point;
                result = proxy != null ? proxy.Target : collider.GetComponentInParent<LabLabelTarget>();
            }
            return result != null && result.IsAvailable ? result : null;
        }
        private void Hide(int hand)
        {
            candidates[hand] = null; visible[hand] = null; dwell[hand] = 0;
            if (views != null && hand < views.Length && views[hand]?.Panel != null) views[hand].Panel.SetActive(false);
            if (identificationDots != null && hand < identificationDots.Length && identificationDots[hand] != null)
                identificationDots[hand].gameObject.SetActive(false);
        }
        private void OnDisable() { Hide(0); Hide(1); }
    }
}
