using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Team5.qPCR
{
    public sealed class PlateDropRecovery : MonoBehaviour
    {
        [SerializeField] private Transform homeAnchor;
        [SerializeField] private InstrumentController instrument;
        [SerializeField] private XRGrabInteractable grab;
        [SerializeField] private float recoveryHeight = -0.15f;

        public void Configure(Transform home, InstrumentController instrumentController, XRGrabInteractable interactable)
        {
            Configure(home, instrumentController, interactable, recoveryHeight);
        }

        public void Configure(Transform home, InstrumentController instrumentController, XRGrabInteractable interactable,
            float minimumHeight)
        {
            homeAnchor = home;
            instrument = instrumentController;
            grab = interactable;
            recoveryHeight = minimumHeight;
        }

        private void Update()
        {
            if (transform.position.y >= recoveryHeight || homeAnchor == null || (instrument != null && instrument.IsPlateInserted))
                return;
            if (grab != null && grab.isSelected) return;
            transform.SetParent(homeAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }
            GetComponent<PlateController>()?.SetA1Aligned(false);
            instrument?.MarkPlateRemoved();
        }
    }
}
