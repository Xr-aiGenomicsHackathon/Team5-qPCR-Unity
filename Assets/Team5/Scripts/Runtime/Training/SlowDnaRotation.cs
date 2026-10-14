using UnityEngine;
namespace Team5.qPCR
{
    public sealed class SlowDnaRotation : MonoBehaviour
    {
        private void Update() => transform.Rotate(Vector3.up, 4f * Time.deltaTime, Space.Self);
    }
}
