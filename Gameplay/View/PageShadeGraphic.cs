using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 넘어가는 종이에 지는 그늘. <b>접히는 쪽이 짙고 바깥으로 갈수록 옅어진다.</b>
    ///
    /// 그림 한 장으로 덮으면 종이가 아니라 회색 판때기가 세워진 것처럼 보인다 —
    /// 실제로 그랬다. 종이는 책등 쪽이 먼저 어두워지므로 그 기울기가 있어야
    /// "넘어가는 중"으로 읽힌다.
    ///
    /// 텍스처를 쓰지 않고 꼭짓점 색만으로 만든다. 그림 파일이 하나도 늘지 않고,
    /// 짙기를 바꿀 때 색만 다시 칠하면 된다.
    /// </summary>
    /// <remarks>
    /// <see cref="CanvasRenderer"/>를 여기서 직접 요구한다. <see cref="Graphic"/>에 붙은
    /// 같은 요구는 물려받아지지 않아서, 이것이 없으면 부품이 조용히 빠진 채 만들어지고
    /// 아무것도 그려지지 않는다 — 값은 다 맞는데 화면만 비어 실제로 한참 헤맸다.
    /// </remarks>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PageShadeGraphic : MaskableGraphic
    {
        [Tooltip("접히는 쪽(책등)이 오른쪽인지. 좌우로 뒤집어 놓은 뒷면은 반대가 된다.")]
        [SerializeField] private bool hingeOnRight;

        [Range(0f, 1f)]
        [Tooltip("가장 짙은 쪽의 짙기. 실행 중에 넘어가는 각도가 정한다.")]
        [SerializeField] private float strength;

        [Range(0f, 1f)]
        [Tooltip("바깥 끝에 남는 몫. 0이면 끝이 완전히 투명해진다.")]
        [SerializeField] private float farRatio = 0.12f;

        /// <summary>도구가 만들 때 접히는 방향을 정해 준다.</summary>
        public void SetHinge(bool onRight)
        {
            hingeOnRight = onRight;
            SetVerticesDirty();
        }

        /// <summary>짙기를 바꾼다. 0이면 아무것도 그리지 않는다.</summary>
        public void SetStrength(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(strength, amount)) return;
            strength = amount;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (strength <= 0.002f) return;   // 눕혀 놓은 종이에는 그늘이 없다

            Rect rect = GetPixelAdjustedRect();
            Color32 near = Shade(strength);
            Color32 far = Shade(strength * farRatio);
            Color32 left = hingeOnRight ? far : near;
            Color32 right = hingeOnRight ? near : far;

            var vertex = UIVertex.simpleVert;

            vertex.position = new Vector3(rect.xMin, rect.yMin);
            vertex.color = left;
            vh.AddVert(vertex);

            vertex.position = new Vector3(rect.xMin, rect.yMax);
            vertex.color = left;
            vh.AddVert(vertex);

            vertex.position = new Vector3(rect.xMax, rect.yMax);
            vertex.color = right;
            vh.AddVert(vertex);

            vertex.position = new Vector3(rect.xMax, rect.yMin);
            vertex.color = right;
            vh.AddVert(vertex);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private Color32 Shade(float alpha)
        {
            Color tint = color;
            tint.a = Mathf.Clamp01(alpha);
            return tint;
        }
    }
}
