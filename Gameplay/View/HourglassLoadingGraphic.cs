using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 모래의 양만 바꿔도 자연스럽게 흐르는 것처럼 보이는 경량 uGUI 모래시계다.
    /// 별도 스프라이트 없이 프레임, 위·아래 모래와 낙하 줄기를 직접 그린다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class HourglassLoadingGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float sandProgress;
        [SerializeField, Min(1f)] private float frameThickness = 3f;
        [SerializeField] private Color frameColor = new Color(0.82f, 0.62f, 0.30f, 1f);
        [SerializeField] private Color sandColor = new Color(0.96f, 0.70f, 0.27f, 1f);

        public float SandProgress
        {
            get => sandProgress;
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Approximately(sandProgress, next)) return;
                sandProgress = next;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            frameThickness = Mathf.Max(1f, frameThickness);
            raycastTarget = false;
            SetVerticesDirty();
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float size = Mathf.Min(rect.width, rect.height);
            float halfWidth = size * 0.31f;
            float top = size * 0.35f;
            float bottom = -top;
            float neck = size * 0.035f;
            float barExtension = size * 0.06f;

            Vector2 topLeft = new Vector2(-halfWidth, top);
            Vector2 topRight = new Vector2(halfWidth, top);
            Vector2 upperNeck = new Vector2(0f, neck);
            Vector2 lowerNeck = new Vector2(0f, -neck);
            Vector2 bottomLeft = new Vector2(-halfWidth, bottom);
            Vector2 bottomRight = new Vector2(halfWidth, bottom);

            AddLine(vh, topLeft - Vector2.right * barExtension,
                topRight + Vector2.right * barExtension, frameThickness, frameColor);
            AddLine(vh, topLeft, upperNeck, frameThickness, frameColor);
            AddLine(vh, topRight, upperNeck, frameThickness, frameColor);
            AddLine(vh, lowerNeck, bottomLeft, frameThickness, frameColor);
            AddLine(vh, lowerNeck, bottomRight, frameThickness, frameColor);
            AddLine(vh, bottomLeft - Vector2.right * barExtension,
                bottomRight + Vector2.right * barExtension, frameThickness, frameColor);

            float inset = frameThickness * 1.65f;
            float sandHalfWidth = Mathf.Max(1f, halfWidth - inset);
            float topRemaining = 1f - sandProgress;
            float topBaseY = neck + (top - neck - inset) * topRemaining;
            float topSandHalfWidth = sandHalfWidth * Mathf.Sqrt(topRemaining);
            AddTriangle(vh,
                new Vector2(-topSandHalfWidth, topBaseY),
                new Vector2(topSandHalfWidth, topBaseY),
                new Vector2(0f, neck + inset * 0.35f), sandColor);

            float bottomApexY = bottom + inset + (-neck - bottom - inset) * sandProgress;
            float bottomSandHalfWidth = sandHalfWidth * Mathf.Sqrt(sandProgress);
            AddTriangle(vh,
                new Vector2(-bottomSandHalfWidth, bottom + inset),
                new Vector2(bottomSandHalfWidth, bottom + inset),
                new Vector2(0f, bottomApexY), sandColor);

            if (sandProgress > 0.025f && sandProgress < 0.975f)
            {
                float streamEnd = Mathf.Max(bottomApexY + 1f, bottom + inset);
                AddLine(vh, new Vector2(0f, neck - 1f), new Vector2(0f, streamEnd),
                    Mathf.Max(1.5f, frameThickness * 0.55f), sandColor);
            }
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero);
            vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero);
            vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
