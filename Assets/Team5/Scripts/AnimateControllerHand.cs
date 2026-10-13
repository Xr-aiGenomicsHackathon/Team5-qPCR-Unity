using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class AnimateControllerHand : MonoBehaviour
{
    public InputActionReference gripAction;
    public InputActionReference triggerAction;

    private Animator handAnimator;

    private void Awake()
    {
        handAnimator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (gripAction?.action != null)
            handAnimator.SetFloat("Grip",
                gripAction.action.ReadValue<float>(), 0.08f, Time.deltaTime);

        if (triggerAction?.action != null)
            handAnimator.SetFloat("Trigger",
                triggerAction.action.ReadValue<float>(), 0.08f, Time.deltaTime);
    }
}