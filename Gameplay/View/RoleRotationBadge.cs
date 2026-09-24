using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// "여기서 자리가 돈다"를 뜻하는 동그란 화살표. 그림 파일 없이 그린다.
    ///
    /// <b>왜 스프라이트를 쓰지 않는가.</b> 프로젝트의 UI 도형은 전부 코드로 그린다
    /// (<see cref="RoundedPanelGraphic"/>·<see cref="EmotionWheelArcGraphic"/> 등).
    /// 그림 파일을 하나 더 들이면 색과 굵기를 화면마다 다시 맞춰야 하고, 해상도가
    /// 바뀔 때 이 표식만 흐려진다.
    ///
    /// 굵기와 지름은 <see cref="RectTransform"/> 크기에 비례하므로, 줄 높이가 바뀌어도
    /// 도구에서 숫자를 다시 재지 않아도 된다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoleRotationBadge : MaskableGraphic
    {
        [Tooltip("고리 굵기. 반지름에 곱한다.")]
        [SerializeField, Range(0.05f, 0.5f)] private float thicknessRatio = 0.22f;

        [Tooltip("고리가 그려지기 시작하는 각도(도). 화살촉이 설 자리를 비워 둔다.")]
        [SerializeField] private float startAngle = 120f;

        [Tooltip("고리가 끝나는 각도. 시작보다 작으면 시계 방향으로 돈다 — " +
                 "자리가 도는 방향(딜러 → A → B)과 눈이 같은 쪽으로 움직인다.")]
        [SerializeField] private float endAngle = -150f;

        [Tooltip("화살촉 크기. 고리 굵기에 곱한다.")]
        [SerializeField, Range(1f, 3f)] private float headRatio = 2.1f;

        [SerializeField, Range(4, 64)] private int segments = 40;

        /// <summary>바깥 반지름. 화살촉이 고리보다 넓으므로 그만큼 안으로 들여 그린다.</summary>
        private float Radius
        {
            get
            {
                Rect rect = rectTransform.rect;
                float half = Mathf.Min(rect.width, rect.height) * 0.5f;
                return half / (1f + thicknessRatio * (headRatio - 1f) * 0.5f);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float radius = Radius;
            if (radius <= 0f) return;

            float thickness = radius * thicknessRatio;
            float inner = radius - thickness * 0.5f;
            float outer = radius + thickness * 0.5f;
            Color32 tint = color;

            int steps = Mathf.Max(1,
                Mathf.CeilToInt(Mathf.Abs(endAngle - startAngle) / 360f * segments));

            for (int i = 0; i <= steps; i++)
            {
                float radians = Mathf.Lerp(startAngle, endAngle, i / (float)steps) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                vh.AddVert(direction * inner, tint, Vector2.zero);
                vh.AddVert(direction * outer, tint, Vector2.zero);
            }

            for (int i = 0; i < steps; i++)
            {
                int first = i * 2;
                vh.AddTriangle(first, first + 3, first + 1);
                vh.AddTriangle(first, first + 2, first + 3);
            }

            AddHead(vh, tint, radius, thickness);
        }

        /// <summary>
        /// 고리가 끝나는 자리에 세우는 화살촉. 고리의 접선 방향을 향해야 '돈다'로 읽힌다 —
        /// 바깥이나 안쪽을 향하면 그냥 삼각형 하나가 붙은 고리다.
        /// </summary>
        private void AddHead(VertexHelper vh, Color32 tint, float radius, float thickness)
        {
            float radians = endAngle * Mathf.Deg2Rad;
            var outward = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            // 도는 방향의 접선. 각도가 줄어드는 쪽으로 그리므로 접선도 그쪽이다.
            var tangent = new Vector2(outward.y, -outward.x);
            if (endAngle > startAngle) tangent = -tangent;

            float half = thickness * headRatio * 0.5f;
            Vector2 baseCenter = outward * radius;
            Vector2 tip = baseCenter + tangent * (half * 1.5f);

            int start = vh.currentVertCount;
            vh.AddVert(baseCenter + outward * half, tint, Vector2.zero);
            vh.AddVert(baseCenter - outward * half, tint, Vector2.zero);
            vh.AddVert(tip, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
