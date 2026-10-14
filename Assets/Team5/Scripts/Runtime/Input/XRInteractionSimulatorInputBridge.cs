using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
#endif

namespace Team5.qPCR
{
    /// <summary>
    /// Compatibility bridge for the XRI 3.3.2 Interaction Simulator in Unity 6000.3.
    /// Its immediate InputState.Change calls do not reach the existing simulated
    /// controller and HMD devices in this editor combination, while queued state events do.
    /// Active only for Editor/Windows simulated devices; never alters real OpenXR input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XRInteractionSimulatorInputBridge : MonoBehaviour
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        private static readonly BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo LeftStateField =
            typeof(XRInteractionSimulator).GetField("m_LeftControllerState", PrivateInstance);
        private static readonly FieldInfo RightStateField =
            typeof(XRInteractionSimulator).GetField("m_RightControllerState", PrivateInstance);
        private static readonly FieldInfo HmdStateField =
            typeof(XRInteractionSimulator).GetField("m_HMDState", PrivateInstance);
        private static readonly FieldInfo LeftDeviceField =
            typeof(SimulatedDeviceLifecycleManager).GetField("m_LeftControllerDevice", PrivateInstance);
        private static readonly FieldInfo RightDeviceField =
            typeof(SimulatedDeviceLifecycleManager).GetField("m_RightControllerDevice", PrivateInstance);
        private static readonly FieldInfo HmdDeviceField =
            typeof(SimulatedDeviceLifecycleManager).GetField("m_HMDDevice", PrivateInstance);

        [SerializeField] private XRInteractionSimulator simulator;

        public void Configure(XRInteractionSimulator value) => simulator = value;

        private void Awake()
        {
            if (simulator == null)
                simulator = GetComponent<XRInteractionSimulator>();
        }

        private void LateUpdate()
        {
            if (simulator == null || !simulator.isActiveAndEnabled ||
                LeftStateField == null || RightStateField == null ||
                LeftDeviceField == null || RightDeviceField == null)
                return;

            var manager = simulator.deviceLifecycleManager;
            if (manager == null || !manager.isActiveAndEnabled)
                return;

            QueueControllerState(
                LeftDeviceField.GetValue(manager) as InputDevice,
                (XRSimulatedControllerState)LeftStateField.GetValue(simulator));
            QueueControllerState(
                RightDeviceField.GetValue(manager) as InputDevice,
                (XRSimulatedControllerState)RightStateField.GetValue(simulator));
            if (HmdStateField != null && HmdDeviceField != null)
            {
                var device = HmdDeviceField.GetValue(manager) as InputDevice;
                if (device != null && device.added)
                    InputSystem.QueueStateEvent(device, (XRSimulatedHMDState)HmdStateField.GetValue(simulator));
            }
        }

        private static void QueueControllerState(InputDevice device, XRSimulatedControllerState state)
        {
            if (device != null && device.added)
                InputSystem.QueueStateEvent(device, state);
        }
#else
        private void Awake() => enabled = false;
#endif
    }
}
