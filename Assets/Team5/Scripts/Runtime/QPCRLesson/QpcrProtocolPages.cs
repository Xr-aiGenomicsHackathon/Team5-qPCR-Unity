using TMPro;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    public sealed class QpcrProtocolPages : MonoBehaviour
    {
        [SerializeField] private GameObject[] fieldObjects;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private QpcrTouchscreenController touchscreen;
        [SerializeField] private GameObject keypad;
        [SerializeField] private GameObject reviewPanel;
        [SerializeField] private TMP_Text reviewText;
        [SerializeField] private Button validate;
        public int Page { get; private set; }
        private void OnEnable() => ShowPage(0);
        public void Next() => ShowPage(Page + 1);
        public void Previous() => ShowPage(Page - 1);
        public void ShowPage(int page)
        {
            if (reviewPanel != null) reviewPanel.SetActive(false);
            if (keypad != null) keypad.SetActive(true);
            if (previous != null) previous.gameObject.SetActive(true);
            if (next != null) next.gameObject.SetActive(true);
            if (validate != null) validate.gameObject.SetActive(true);
            Page = Mathf.Clamp(page, 0, 3);
            if (fieldObjects != null)
                for (var i = 0; i < fieldObjects.Length; i++) fieldObjects[i]?.SetActive(i / 4 == Page);
            if (pageLabel != null) pageLabel.text = $"Protocol {Page + 1} / 4";
            if (previous != null) previous.interactable = Page > 0;
            if (next != null) next.interactable = Page < 3;
        }
        public void ShowParameter(QpcrProtocolParameter parameter)
        {
            var index = System.Array.FindIndex(fieldObjects, go => go != null && go.name == "Field_" + parameter);
            ShowPage(Mathf.Max(0, index) / 4);
        }
        public void ShowReview()
        {
            if (reviewPanel == null || touchscreen == null) return;
            foreach (var go in fieldObjects) go?.SetActive(false);
            if (keypad != null) keypad.SetActive(false);
            previous?.gameObject.SetActive(false); next?.gameObject.SetActive(false); validate?.gameObject.SetActive(false);
            var text = new StringBuilder("REVIEW THIS TRAINING PROTOCOL\n");
            foreach (var value in touchscreen.Values)
                text.AppendLine(QpcrProtocolValidator.LabelFor(value.Key) + ": " + value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " " + QpcrProtocolValidator.UnitFor(value.Key));
            if (reviewText != null) reviewText.text = text.ToString();
            reviewPanel.SetActive(true);
        }
    }
}
