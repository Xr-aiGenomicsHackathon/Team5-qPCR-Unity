using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    public sealed class MeltCurveView : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float visibility;
        [SerializeField, Min(1f)] private float lineThickness = 3f;

        public void SetVisible(bool value)
        {
            visibility = value ? 1f : 0f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (visibility <= 0f) return;
            var rect = GetPixelAdjustedRect();
            Vector2? previous = null;
            const int points = 48;
            for (var index = 0; index <= points; index++)
            {
                var x = index / (float)points;
                var peak = Mathf.Exp(-Mathf.Pow((x - .63f) / .09f, 2f));
                var point = new Vector2(Mathf.Lerp(rect.xMin + 14f, rect.xMax - 8f, x),
                    Mathf.Lerp(rect.yMin + 14f, rect.yMax - 8f, .08f + .82f * peak));
                if (previous.HasValue) AddLine(vh, previous.Value, point);
                previous = point;
            }
        }

        private void AddLine(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * lineThickness * .5f;
            var start = vh.currentVertCount;
            var curveColor = new Color(.45f, .18f, .76f, 1f);
            vh.AddVert(a - normal, curveColor, Vector2.zero);
            vh.AddVert(a + normal, curveColor, Vector2.zero);
            vh.AddVert(b + normal, curveColor, Vector2.zero);
            vh.AddVert(b - normal, curveColor, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
