using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    public sealed class AmplificationGraphView : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField, Min(1f)] private float lineThickness = 3f;

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            DrawAxes(vh);
            DrawCurve(vh, new Color(0.12f, 0.42f, 0.92f, 1f), 0.42f, 0.18f, true);
            DrawCurve(vh, new Color(0.16f, 0.72f, 0.38f, 1f), 0.56f, 0.12f, true);
            DrawCurve(vh, new Color(0.94f, 0.50f, 0.18f, 1f), 0.9f, 0.035f, false);
        }

        private void DrawAxes(VertexHelper vh)
        {
            var rect = GetPixelAdjustedRect();
            AddLine(vh, new Vector2(rect.xMin + 18f, rect.yMin + 18f), new Vector2(rect.xMin + 18f, rect.yMax - 10f), new Color(.18f,.23f,.30f,1f));
            AddLine(vh, new Vector2(rect.xMin + 18f, rect.yMin + 18f), new Vector2(rect.xMax - 8f, rect.yMin + 18f), new Color(.18f,.23f,.30f,1f));
        }

        private void DrawCurve(VertexHelper vh, Color curveColor, float midpoint, float baseline, bool amplify)
        {
            var rect = GetPixelAdjustedRect();
            const int points = 35;
            var visible = Mathf.Max(1, Mathf.CeilToInt(points * progress));
            Vector2? previous = null;
            for (var index = 0; index <= visible; index++)
            {
                var x = index / (float)points;
                var y = amplify
                    ? baseline + 0.72f / (1f + Mathf.Exp(-18f * (x - midpoint)))
                    : baseline + Mathf.Sin(index * 1.7f) * 0.006f;
                var point = new Vector2(Mathf.Lerp(rect.xMin + 18f, rect.xMax - 8f, x),
                    Mathf.Lerp(rect.yMin + 18f, rect.yMax - 10f, Mathf.Clamp01(y)));
                if (previous.HasValue)
                    AddLine(vh, previous.Value, point, curveColor);
                previous = point;
            }
        }

        private void AddLine(VertexHelper vh, Vector2 a, Vector2 b, Color lineColor)
        {
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * lineThickness * 0.5f;
            var start = vh.currentVertCount;
            vh.AddVert(a - normal, lineColor, Vector2.zero);
            vh.AddVert(a + normal, lineColor, Vector2.zero);
            vh.AddVert(b + normal, lineColor, Vector2.zero);
            vh.AddVert(b - normal, lineColor, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
