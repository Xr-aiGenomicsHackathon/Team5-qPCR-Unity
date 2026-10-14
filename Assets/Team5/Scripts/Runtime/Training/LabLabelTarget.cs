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
        public bool IsAvailable => isActiveAndEnabled && !string.IsNullOrWhiteSpace(displayName);
        public Vector3 LabelPosition(Vector3 hitPoint) => labelAnchor != null ? labelAnchor.position : hitPoint;
        public void Configure(string objectName, string objectPurpose, Transform anchor = null)
        { displayName = objectName; purpose = objectPurpose; labelAnchor = anchor; }
    }
}
