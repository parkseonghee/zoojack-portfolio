using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 스프라이트 없이 둥근 배경, 이중 테두리와 그림자를 그리는 확장형 uGUI 그래픽이다.
    /// RectTransform 크기를 그대로 따르므로 씬에서 패널을 수정해도 형태가 유지된다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class RoundedPanelGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float cornerRadius = 28f;
        [SerializeField, Range(2, 16)] private int cornerSegments = 8;
        [SerializeField] private Color backgroundColor = new Color(0.025f, 0.032f, 0.03f, 0.985f);

        [Header("Outer Border")]
        [SerializeField, Min(0f)] private float borderThickness = 2.2f;
        [SerializeField] private Color borderColor = new Color(0.72f, 0.48f, 0.18f, 0.95f);

        [Header("Inner Border")]
        [SerializeField, Min(0f)] private float innerBorderInset = 8f;
        [SerializeField, Min(0f)] private float innerBorderThickness = 1f;
        [SerializeField] private Color innerBorderColor = new Color(0.48f, 0.34f, 0.14f, 0.58f);

        [Header("Shadow")]
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.48f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -6f);
        [SerializeField, Min(0f)] private float shadowExpansion = 3f;

        public void Configure(
            Color background,
            Color outerBorder,
            float radius,
            float outerThickness,
            Color innerBorder,
            float innerInset,
            float innerThickness,
            Color shadow,
            Vector2 shadowPosition,
            float shadowSize)
        {
            backgroundColor = background;
            borderColor = outerBorder;
            cornerRadius = Mathf.Max(0f, radius);
            borderThickness = Mathf.Max(0f, outerThickness);
            innerBorderColor = innerBorder;
            innerBorderInset = Mathf.Max(0f, innerInset);
            innerBorderThickness = Mathf.Max(0f, innerThickness);
            shadowColor = shadow;
            shadowOffset = shadowPosition;
            shadowExpansion = Mathf.Max(0f, shadowSize);
            color = Color.white;
            SetVerticesDirty();
        }

        /// <summary>
        /// 바깥 테두리만 갈아 끼운다. 나머지 생김새(배경·모서리·그림자)는 그대로 두고
        /// 선택 상태만 표시할 때 쓴다.
        ///
        /// <see cref="Graphic.color"/>는 전체에 곱해지는 색조라 어둡게만 할 수 있어서
        /// (버텍스 색은 0~1로 잘린다) "선택됨"처럼 <b>도드라져야</b> 하는 상태를 표현하지 못한다.
        ///
        /// 매 프레임 불릴 수 있으므로 값이 실제로 바뀔 때만 메시를 다시 만든다.
        /// </summary>
        public void SetBorder(Color outerBorder, float outerThickness)
        {
            outerThickness = Mathf.Max(0f, outerThickness);
            if (borderColor == outerBorder && Mathf.Approximately(borderThickness, outerThickness)) return;

            borderColor = outerBorder;
            borderThickness = outerThickness;
            SetVerticesDirty();
        }

        /// <summary>
        /// 배경만 갈아 끼운다. <see cref="SetBorder"/>와 짝이다 — 테두리와 배경을 함께
        /// 사람 색으로 칠해야 하는 화면(최종 순위표)에서 둘 다 필요하다.
        ///
        /// <see cref="Graphic.color"/>로는 안 된다. 그쪽은 전체에 곱해지는 색조라
        /// 어둡게만 할 수 있고 테두리까지 함께 물든다.
        ///
        /// 매 프레임 불릴 수 있으므로 값이 실제로 바뀔 때만 메시를 다시 만든다.
        /// </summary>
        public void SetBackground(Color background)
        {
            if (backgroundColor == background) return;

            backgroundColor = background;
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            cornerRadius = Mathf.Max(0f, cornerRadius);
            cornerSegments = Mathf.Clamp(cornerSegments, 2, 16);
            borderThickness = Mathf.Max(0f, borderThickness);
            innerBorderInset = Mathf.Max(0f, innerBorderInset);
            innerBorderThickness = Mathf.Max(0f, innerBorderThickness);
            shadowExpansion = Mathf.Max(0f, shadowExpansion);
            SetVerticesDirty();
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAlignedRect(rectTransform.rect);
            Color tint = color;

            if (shadowColor.a > 0f)
            {
                Rect shadowRect = Expand(rect, shadowExpansion);
                shadowRect.position += shadowOffset;
                AddRoundedFill(vh, shadowRect, cornerRadius + shadowExpansion,
                    shadowColor * tint, cornerSegments);
            }

            AddRoundedFill(vh, rect, cornerRadius, backgroundColor * tint, cornerSegments);

            if (borderThickness > 0f && borderColor.a > 0f)
                AddRoundedRing(vh, rect, cornerRadius, GetPixelAlignedLength(borderThickness),
                    borderColor * tint, cornerSegments);

            if (innerBorderThickness > 0f && innerBorderColor.a > 0f)
            {
                Rect innerRect = GetPixelAlignedRect(Inset(rect, innerBorderInset));
                AddRoundedRing(vh, innerRect, Mathf.Max(0f, cornerRadius - innerBorderInset),
                    GetPixelAlignedLength(innerBorderThickness),
                    innerBorderColor * tint, cornerSegments);
            }
        }

        private Rect GetPixelAlignedRect(Rect rect)
        {
            if (canvas == null)
                return rect;

            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(
                eventCamera, rectTransform.TransformPoint(rect.min));
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(
                eventCamera, rectTransform.TransformPoint(rect.max));

            screenMin = new Vector2(Mathf.Round(screenMin.x), Mathf.Round(screenMin.y));
            screenMax = new Vector2(Mathf.Round(screenMax.x), Mathf.Round(screenMax.y));

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenMin, eventCamera, out Vector2 localMin) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenMax, eventCamera, out Vector2 localMax))
                return rect;

            return Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);
        }

        private float GetPixelAlignedLength(float length)
        {
            if (length <= 0f || canvas == null)
                return length;

            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(
                eventCamera, rectTransform.TransformPoint(Vector2.zero));
            Vector2 right = RectTransformUtility.WorldToScreenPoint(
                eventCamera, rectTransform.TransformPoint(Vector2.right));
            Vector2 up = RectTransformUtility.WorldToScreenPoint(
                eventCamera, rectTransform.TransformPoint(Vector2.up));
            float pixelsPerUnit = ((right - origin).magnitude + (up - origin).magnitude) * 0.5f;

            if (pixelsPerUnit <= Mathf.Epsilon)
                return length;

            float pixelLength = Mathf.Max(1f, Mathf.Round(length * pixelsPerUnit));
            return pixelLength / pixelsPerUnit;
        }

        private static void AddRoundedFill(
            VertexHelper vh, Rect rect, float radius, Color color, int segments)
        {
            var perimeter = BuildPerimeter(rect, radius, segments);
            int center = vh.currentVertCount;
            vh.AddVert(rect.center, color, Vector2.zero);

            for (int i = 0; i < perimeter.Count; i++)
                vh.AddVert(perimeter[i], color, Vector2.zero);

            for (int i = 0; i < perimeter.Count; i++)
            {
                int current = center + 1 + i;
                int next = center + 1 + ((i + 1) % perimeter.Count);
                vh.AddTriangle(center, current, next);
            }
        }

        private static void AddRoundedRing(
            VertexHelper vh, Rect rect, float radius, float thickness, Color color, int segments)
        {
            float safeThickness = Mathf.Min(thickness, Mathf.Min(rect.width, rect.height) * 0.5f);
            Rect innerRect = Inset(rect, safeThickness);
            var outer = BuildPerimeter(rect, radius, segments);
            var inner = BuildPerimeter(innerRect, Mathf.Max(0f, radius - safeThickness), segments);
            int start = vh.currentVertCount;

            for (int i = 0; i < outer.Count; i++)
            {
                vh.AddVert(outer[i], color, Vector2.zero);
                vh.AddVert(inner[i], color, Vector2.zero);
            }

            for (int i = 0; i < outer.Count; i++)
            {
                int next = (i + 1) % outer.Count;
                int outerCurrent = start + i * 2;
                int innerCurrent = outerCurrent + 1;
                int outerNext = start + next * 2;
                int innerNext = outerNext + 1;
                vh.AddTriangle(outerCurrent, outerNext, innerNext);
                vh.AddTriangle(outerCurrent, innerNext, innerCurrent);
            }
        }

        private static List<Vector2> BuildPerimeter(Rect rect, float radius, int segments)
        {
            float safeRadius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            var points = new List<Vector2>((segments + 1) * 4);

            AddArc(points, new Vector2(rect.xMin + safeRadius, rect.yMin + safeRadius),
                safeRadius, 180f, 270f, segments);
            AddArc(points, new Vector2(rect.xMax - safeRadius, rect.yMin + safeRadius),
                safeRadius, 270f, 360f, segments);
            AddArc(points, new Vector2(rect.xMax - safeRadius, rect.yMax - safeRadius),
                safeRadius, 0f, 90f, segments);
            AddArc(points, new Vector2(rect.xMin + safeRadius, rect.yMax - safeRadius),
                safeRadius, 90f, 180f, segments);

            return points;
        }

        private static void AddArc(
            List<Vector2> points, Vector2 center, float radius,
            float startAngle, float endAngle, int segments)
        {
            if (radius <= Mathf.Epsilon)
            {
                // 둥근 외곽선의 안쪽 반지름이 두께 때문에 0이 되더라도 바깥쪽과
                // 같은 정점 수를 유지해야 AddRoundedRing에서 인덱스를 맞출 수 있다.
                for (int i = 0; i <= segments; i++)
                    points.Add(center);
                return;
            }

            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private static Rect Inset(Rect rect, float amount) =>
            new Rect(rect.xMin + amount, rect.yMin + amount,
                Mathf.Max(0f, rect.width - amount * 2f),
                Mathf.Max(0f, rect.height - amount * 2f));

        private static Rect Expand(Rect rect, float amount) =>
            new Rect(rect.xMin - amount, rect.yMin - amount,
                rect.width + amount * 2f, rect.height + amount * 2f);
    }
}
