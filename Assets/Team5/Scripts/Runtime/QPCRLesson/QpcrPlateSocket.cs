using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Team5.qPCR
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class QpcrPlateSocket : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private QpcrDrawerController drawer;
        [SerializeField] private XRGrabInteractable plateGrab;
        [SerializeField] private Transform plate;
        [SerializeField] private Rigidbody plateBody;
        [SerializeField] private Transform attachTransform;
        [SerializeField] private Renderer guidanceRenderer;
        [SerializeField] private QpcrMachineAudio machineAudio;
        [SerializeField, Range(5f, 45f)] private float orientationTolerance = 24f;

        private bool plateInside;
        private IXRSelectInteractor lastHoldingInteractor;

        public bool IsPlateSeated { get; private set; }

        public void Configure(QpcrLessonController lessonController, QpcrDrawerController drawerController,
            XRGrabInteractable grab, Transform plateTransform, Rigidbody body, Transform attachment,
            Renderer guide, QpcrMachineAudio audio)
        {
            lesson = lessonController;
            drawer = drawerController;
            plateGrab = grab;
            plate = plateTransform;
            plateBody = body;
            attachTransform = attachment;
            guidanceRenderer = guide;
            machineAudio = audio;
        }

        private void Awake()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            if (plateGrab != null)
            {
                plateGrab.selectEntered.AddListener(OnPlateGrabbed);
                plateGrab.selectExited.AddListener(OnPlateReleased);
            }
            if (lesson != null)
                lesson.StateChanged += OnStateChanged;
        }

        private void Start() => ResetSocket();

        private void OnDisable()
        {
            if (plateGrab != null)
            {
                plateGrab.selectEntered.RemoveListener(OnPlateGrabbed);
                plateGrab.selectExited.RemoveListener(OnPlateReleased);
            }
            if (lesson != null)
                lesson.StateChanged -= OnStateChanged;
        }

        public bool IsOrientationValid(Transform candidate)
        {
            return candidate != null && attachTransform != null &&
                Quaternion.Angle(candidate.rotation, attachTransform.rotation) <= orientationTolerance;
        }

        public void ResetSocket()
        {
            IsPlateSeated = false;
            plateInside = false;
            SetGuideColor(new Color(0.12f, 0.42f, 0.92f, 0.65f));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (plate != null && (other.transform == plate || other.transform.IsChildOf(plate)))
            {
                plateInside = true;
                UpdateGuideForPose();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (plate != null && (other.transform == plate || other.transform.IsChildOf(plate)))
            {
                plateInside = true;
                UpdateGuideForPose();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (plate != null && (other.transform == plate || other.transform.IsChildOf(plate)))
            {
                plateInside = false;
                SetGuideColor(new Color(0.12f, 0.42f, 0.92f, 0.65f));
            }
        }

        private void OnPlateGrabbed(SelectEnterEventArgs args)
        {
            lastHoldingInteractor = args.interactorObject as IXRSelectInteractor;
        }

        private void OnPlateReleased(SelectExitEventArgs args)
        {
            if (lesson == null || lesson.CurrentState != QpcrLessonState.LoadPlate)
                return;
            if (drawer == null || !drawer.IsOpen || drawer.IsMoving)
            {
                Reject("Open the instrument drawer before loading the plate.");
                return;
            }
            if (!plateInside)
            {
                Reject("Move the plate fully into the highlighted thermal-block socket before releasing it.");
                return;
            }
            if (!IsOrientationValid(plate))
            {
                Reject("A1 is facing the wrong direction. Rotate the plate until the A1 corner matches the socket marker.");
                StartCoroutine(FlashGuideError());
                return;
            }

            SeatPlate();
        }

        private void SeatPlate()
        {
            if (plate == null || attachTransform == null)
                return;
            plate.SetParent(attachTransform, false);
            plate.localPosition = Vector3.zero;
            plate.localRotation = Quaternion.identity;
            if (plateBody != null)
            {
                if (!plateBody.isKinematic)
                {
                    plateBody.linearVelocity = Vector3.zero;
                    plateBody.angularVelocity = Vector3.zero;
                }
                plateBody.isKinematic = true;
            }
            if (plateGrab != null)
                plateGrab.enabled = false;
            IsPlateSeated = true;
            SetGuideColor(new Color(0.16f, 0.72f, 0.38f, 0.8f));
            machineAudio?.PlayButton();
            (lastHoldingInteractor as XRBaseInputInteractor)?.SendHapticImpulse(0.48f, 0.1f);
            lesson.TryComplete(QpcrLessonState.LoadPlate);
        }

        private void Reject(string message)
        {
            lesson?.ReportRejectedAction(message);
            machineAudio?.PlayError();
            (lastHoldingInteractor as XRBaseInputInteractor)?.SendHapticImpulse(0.72f, 0.12f);
        }

        private IEnumerator FlashGuideError()
        {
            SetGuideColor(new Color(0.94f, 0.18f, 0.22f, 0.9f));
            yield return new WaitForSecondsRealtime(1f);
            UpdateGuideForPose();
        }

        private void UpdateGuideForPose()
        {
            SetGuideColor(IsOrientationValid(plate)
                ? new Color(0.16f, 0.72f, 0.38f, 0.8f)
                : new Color(1f, 0.66f, 0.12f, 0.8f));
        }

        private void SetGuideColor(Color color)
        {
            if (guidanceRenderer == null)
                return;
            var block = new MaterialPropertyBlock();
            guidanceRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 0.25f);
            guidanceRenderer.SetPropertyBlock(block);
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            if (state == QpcrLessonState.NotStarted)
                ResetSocket();
        }
    }
}
