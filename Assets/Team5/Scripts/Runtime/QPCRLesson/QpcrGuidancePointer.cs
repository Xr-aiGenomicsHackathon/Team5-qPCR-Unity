using System;
using UnityEngine;

namespace Team5.qPCR
{
    [Serializable]
    public sealed class QpcrPointerTarget
    {
        public QpcrLessonState State;
        public Transform Target;
    }

    public sealed class QpcrGuidancePointer : MonoBehaviour
    {
        [SerializeField] private QpcrLessonController lesson;
        [SerializeField] private LineRenderer beam;
        [SerializeField] private Transform marker;
        [SerializeField] private QpcrPointerTarget[] targets = Array.Empty<QpcrPointerTarget>();
        [SerializeField, Min(.05f)] private float beamHeight = .09f;
        [SerializeField, Min(0f)] private float targetOffset = .015f;
        [SerializeField, Min(.1f)] private float pulseSpeed = 5f;
        [SerializeField, Range(0f, .5f)] private float pulseAmount = .18f;

        private Transform activeTarget;
        private Vector3 markerBaseScale = Vector3.one;

        public Transform ActiveTarget => activeTarget;

        public void Configure(QpcrLessonController lessonController, LineRenderer pointerBeam,
            Transform pointerMarker, QpcrPointerTarget[] stepTargets)
        {
            lesson = lessonController;
            beam = pointerBeam;
            marker = pointerMarker;
            targets = stepTargets ?? Array.Empty<QpcrPointerTarget>();
        }

        private void Awake()
        {
            if (beam != null)
            {
                beam.useWorldSpace = true;
                beam.positionCount = 2;
            }

            if (marker != null)
                markerBaseScale = marker.localScale;
        }

        private void OnEnable()
        {
            if (lesson != null)
                lesson.StateChanged += OnStateChanged;
        }

        private void Start() => OnStateChanged(lesson == null
            ? QpcrLessonState.NotStarted
            : lesson.CurrentState);

        private void OnDisable()
        {
            if (lesson != null)
                lesson.StateChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (activeTarget == null)
                return;

            var end = activeTarget.position + Vector3.up * targetOffset;
            if (activeTarget is RectTransform rect)
                end = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax, 0f));
            var start = end + Vector3.up * beamHeight;

            if (beam != null)
            {
                beam.SetPosition(0, start);
                beam.SetPosition(1, end);
            }

            if (marker != null)
            {
                marker.position = end;
                var pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
                marker.localScale = markerBaseScale * pulse;
            }
        }

        private void OnStateChanged(QpcrLessonState state)
        {
            activeTarget = FindTarget(state);
            var visible = activeTarget != null && state is not
                (QpcrLessonState.NotStarted or QpcrLessonState.Complete);
            if (beam != null)
                beam.gameObject.SetActive(visible);
            if (marker != null)
                marker.gameObject.SetActive(visible);
        }

        private Transform FindTarget(QpcrLessonState state)
        {
            foreach (var item in targets)
                if (item != null && item.State == state)
                    return item.Target;
            return null;
        }
    }
}
