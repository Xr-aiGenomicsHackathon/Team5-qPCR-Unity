using UnityEngine;

namespace Team5.qPCR
{
    public sealed class QpcrWorldSpacePanel : MonoBehaviour
    {
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private bool yawOnly = true;

        private void LateUpdate()
        {
            if (!faceCamera || Camera.main == null)
                return;
            var direction = transform.position - Camera.main.transform.position;
            if (yawOnly)
                direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
