using UnityEngine;
using UnityEngine.UI;

namespace Team5.qPCR
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AmplificationGraphView : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField, Min(1f)] private float lineThickness = 3f;
        public float Progress => progress;
        public const float Threshold = .20f;
        public static float Signal(float cycle, bool positiveControl) =>
            .02f + .88f / (1f + Mathf.Exp(-.65f * (cycle - (positiveControl ? 20.1f : 26.1f))));
        public static float Cq(bool positiveControl) =>
            (positiveControl ? 20.1f : 26.1f) - Mathf.Log(.88f / (Threshold - .02f) - 1f) / .65f;

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            DrawAxes(vh);
            DrawCurve(vh, new Color(.30f,.76f,1f,1f), true, true);
            DrawCurve(vh, new Color(.32f,.95f,.57f,1f), false, true);
            DrawCurve(vh, new Color(1f,.70f,.32f,1f), false, false);
        }

        private void DrawAxes(VertexHelper vh)
        {
            var rect = GetPixelAdjustedRect();
            for (var i = 1; i <= 4; i++)
            {
                var y = Mathf.Lerp(rect.yMin+18f,rect.yMax-10f,i/4f);
                AddLine(vh,new Vector2(rect.xMin+18f,y),new Vector2(rect.xMax-8f,y),new Color(.22f,.29f,.38f,.6f));
            }
            AddLine(vh, new Vector2(rect.xMin + 18f, rect.yMin + 18f), new Vector2(rect.xMin + 18f, rect.yMax - 10f), new Color(.80f,.85f,.92f,1f));
            AddLine(vh, new Vector2(rect.xMin + 18f, rect.yMin + 18f), new Vector2(rect.xMax - 8f, rect.yMin + 18f), new Color(.80f,.85f,.92f,1f));
            var thresholdY = Mathf.Lerp(rect.yMin+18f,rect.yMax-10f,Threshold);
            for(var i=0;i<24;i+=2)
                AddLine(vh,new Vector2(Mathf.Lerp(rect.xMin+18f,rect.xMax-8f,i/24f),thresholdY),
                    new Vector2(Mathf.Lerp(rect.xMin+18f,rect.xMax-8f,(i+1)/24f),thresholdY),new Color(1f,.88f,.45f,1f));
        }

        private void DrawCurve(VertexHelper vh, Color curveColor, bool positiveControl, bool amplify)
        {
            var rect = GetPixelAdjustedRect();
            const int points = 35;
            var visible = Mathf.FloorToInt(points * progress);
            Vector2? previous = null;
            for (var index = 0; index <= visible; index++)
            {
                var x = index / (float)points;
                var y = amplify
                    ? Signal(index, positiveControl)
                    : .02f + Mathf.Sin(index * 1.7f) * 0.003f;
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
