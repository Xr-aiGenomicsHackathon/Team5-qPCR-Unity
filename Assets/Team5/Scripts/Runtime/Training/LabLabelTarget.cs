using UnityEngine;

namespace Team5.qPCR
{
    [DisallowMultipleComponent]
    public sealed class LabLabelTarget : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea(1, 3)] private string purpose;
        [SerializeField] private Transform labelAnchor;
        public string DisplayName => displayName;
        public string Purpose => purpose;
        private Renderer[] visibleRenderers;
        private void Awake() => visibleRenderers = GetComponentsInChildren<Renderer>(true);
        public bool IsAvailable
        {
            get
            {
                if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(displayName)) return false;
                // Pickups can hide their meshes without disabling the logical root or proxy.
                if (visibleRenderers == null || visibleRenderers.Length == 0) return true;
                foreach (var renderer in visibleRenderers)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) return true;
                return false;
            }
        }
        public Vector3 LabelPosition(Vector3 hitPoint) => labelAnchor != null ? labelAnchor.position : hitPoint;
        public void Configure(string objectName, string objectPurpose, Transform anchor = null)
        { displayName = objectName; purpose = objectPurpose; labelAnchor = anchor; }
    }
}
