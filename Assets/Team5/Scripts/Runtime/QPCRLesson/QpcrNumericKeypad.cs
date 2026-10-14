using UnityEngine;

namespace Team5.qPCR
{
    public sealed class QpcrNumericKeypad : MonoBehaviour
    {
        [SerializeField] private QpcrTouchscreenController touchscreen;

        public void Configure(QpcrTouchscreenController controller) => touchscreen = controller;
        public void Press0() => touchscreen?.AppendCharacter("0");
        public void Press1() => touchscreen?.AppendCharacter("1");
        public void Press2() => touchscreen?.AppendCharacter("2");
        public void Press3() => touchscreen?.AppendCharacter("3");
        public void Press4() => touchscreen?.AppendCharacter("4");
        public void Press5() => touchscreen?.AppendCharacter("5");
        public void Press6() => touchscreen?.AppendCharacter("6");
        public void Press7() => touchscreen?.AppendCharacter("7");
        public void Press8() => touchscreen?.AppendCharacter("8");
        public void Press9() => touchscreen?.AppendCharacter("9");
        public void PressDecimal() => touchscreen?.AppendCharacter(".");
        public void PressBackspace() => touchscreen?.Backspace();
        public void PressConfirm() => touchscreen?.ConfirmCurrentValue();
    }
}
