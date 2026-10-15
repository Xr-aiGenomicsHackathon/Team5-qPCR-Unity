using TMPro;
using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>Warns about room-scale leaning; does not move or freeze the tracked head.</summary>
    public sealed class HeadBoundaryWarning : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private TMP_Text warning;
        [SerializeField] private LayerMask solidLayers;
        [SerializeField] private float headRadius = .10f;
        [SerializeField] private DoorwayAccessGate doorwayGate;
        private readonly Collider[] hits = new Collider[16];
        private Vector3 lastAccessibleHeadPosition;
        private bool hasAccessibleHeadPosition;
        public bool IsPenetrating { get; private set; }
        private void LateUpdate()
        {
            IsPenetrating = false;
            var crossedClosedDoor = false;
            if (head != null)
            {
                var count = Physics.OverlapSphereNonAlloc(head.position, headRadius, hits, solidLayers, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                    if (hits[i] != null && hits[i].enabled && !hits[i].transform.IsChildOf(transform)) { IsPenetrating = true; break; }
                if (hasAccessibleHeadPosition && doorwayGate != null)
                    crossedClosedDoor = !doorwayGate.AllowsTeleport(lastAccessibleHeadPosition, head.position, headRadius);
                IsPenetrating |= crossedClosedDoor;
                if (!IsPenetrating)
                {
                    lastAccessibleHeadPosition = head.position;
                    hasAccessibleHeadPosition = true;
                }
            }
            if (fade != null) fade.alpha = Mathf.MoveTowards(fade.alpha, IsPenetrating ? .9f : 0f, Time.unscaledDeltaTime * 6f);
            if (warning != null) warning.text = crossedClosedDoor
                ? "Door closed — return to the preparation area"
                : IsPenetrating ? "Room boundary — lean back into the laboratory" : "";
        }
        private void OnDisable() { IsPenetrating = false; hasAccessibleHeadPosition = false; if (fade != null) fade.alpha = 0; }
    }
}
