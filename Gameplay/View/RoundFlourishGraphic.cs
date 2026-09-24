using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 라운드 텍스트 양옆에 테이블 로고와 어울리는 대칭형 가지 장식을 그린다.
    /// 별도 이미지 에셋 없이 RectTransform 크기에 맞춰 확장되는 uGUI 그래픽이다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class RoundFlourishGraphic : MaskableGraphic
    {
        [SerializeField, Min(0.5f)] private float lineThickness = 2.2f;
        [SerializeField, Min(1f)] private float leafLength = 8f;
        [SerializeField, Min(1f)] private float leafWidth = 3f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float halfWidth = rect.width * 0.5f;
            float gap = Mathf.Min(105f, halfWidth * 0.46f);
            float extent = Mathf.Max(gap + 8f, halfWidth - 10f);
            Color tint = color;

            Vector2 inner = new Vector2(-gap, -2f);
            Vector2 control = new Vector2(-(gap + extent) * 0.5f, -25f);
            Vector2 outer = new Vector2(-extent, -8f);
            var left = new List<Vector2>(13);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float inverse = 1f - t;
                left.Add(inverse * inverse * inner
                    + 2f * inverse * t * control
                    + t * t * outer);
            }

            AddBranch(vh, left, tint);

            var right = new List<Vector2>(left.Count);
            for (int i = 0; i < left.Count; i++)
                right.Add(new Vector2(-left[i].x, left[i].y));
            AddBranch(vh, right, tint);

            AddLeaf(vh, left[3], new Vector2(-0.45f, 0.89f), tint);
            AddLeaf(vh, left[6], new Vector2(-0.20f, 0.98f), tint);
            AddLeaf(vh, left[9], new Vector2(-0.65f, 0.76f), tint);
            AddLeaf(vh, right[3], new Vector2(0.45f, 0.89f), tint);
            AddLeaf(vh, right[6], new Vector2(0.20f, 0.98f), tint);
            AddLeaf(vh, right[9], new Vector2(0.65f, 0.76f), tint);

            AddDiamond(vh, left[0] + Vector2.left * 8f, 3f, tint);
            AddDiamond(vh, right[0] + Vector2.right * 8f, 3f, tint);
        }

        private void AddBranch(VertexHelper vh, IReadOnlyList<Vector2> points, Color tint)
        {
            for (int i = 0; i < points.Count - 1; i++)
                AddSegment(vh, points[i], points[i + 1], lineThickness, tint);
        }

        private static void AddSegment(
            VertexHelper vh, Vector2 from, Vector2 to, float thickness, Color tint)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(from - normal, tint, Vector2.zero);
            vh.AddVert(from + normal, tint, Vector2.zero);
            vh.AddVert(to + normal, tint, Vector2.zero);
            vh.AddVert(to - normal, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddLeaf(VertexHelper vh, Vector2 center, Vector2 direction, Color tint)
        {
            direction.Normalize();
            Vector2 normal = new Vector2(-direction.y, direction.x);
            int start = vh.currentVertCount;
            vh.AddVert(center - direction * (leafLength * 0.35f), tint, Vector2.zero);
            vh.AddVert(center + normal * leafWidth, tint, Vector2.zero);
            vh.AddVert(center + direction * (leafLength * 0.65f), tint, Vector2.zero);
            vh.AddVert(center - normal * leafWidth, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddDiamond(VertexHelper vh, Vector2 center, float size, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center + Vector2.left * size, tint, Vector2.zero);
            vh.AddVert(center + Vector2.up * size, tint, Vector2.zero);
            vh.AddVert(center + Vector2.right * size, tint, Vector2.zero);
            vh.AddVert(center + Vector2.down * size, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
