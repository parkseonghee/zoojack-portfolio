using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>추가 이미지 없이 원형 링과 선택 부채꼴을 그리는 uGUI 그래픽.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EmotionWheelArcGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float innerRadius = 84f;
        [SerializeField, Min(0f)] private float outerRadius = 220f;
        [SerializeField] private float startAngle = 0f;
        [SerializeField] private float endAngle = 360f;
        [SerializeField, Range(1, 128)] private int segments = 64;

        public void Configure(float inner, float outer, float start, float end, int resolution)
        {
            innerRadius = Mathf.Max(0f, inner);
            outerRadius = Mathf.Max(innerRadius, outer);
            startAngle = start;
            endAngle = end;
            segments = Mathf.Clamp(resolution, 1, 128);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float span = endAngle - startAngle;
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(span) / 360f * segments));
            var tint = color;

            for (int i = 0; i <= stepCount; i++)
            {
                float t = i / (float)stepCount;
                float radians = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                vh.AddVert(direction * innerRadius, tint, Vector2.zero);
                vh.AddVert(direction * outerRadius, tint, Vector2.zero);
            }

            for (int i = 0; i < stepCount; i++)
            {
                int first = i * 2;
                vh.AddTriangle(first, first + 3, first + 1);
                vh.AddTriangle(first, first + 2, first + 3);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            innerRadius = Mathf.Max(0f, innerRadius);
            outerRadius = Mathf.Max(innerRadius, outerRadius);
            SetVerticesDirty();
        }
#endif
    }
}
