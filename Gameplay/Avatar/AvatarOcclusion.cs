using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 캐릭터가 카드·칩을 가릴 때 반투명하게 만드는 판정(순수 계산).
    ///
    /// 캐릭터는 TableArea의 마지막 자식이라 카드·칩보다 항상 위에 그려진다.
    /// 그래서 겹치면 무조건 캐릭터가 가리는 쪽이고, 겹침만 보면 충분하다.
    /// </summary>
    public static class AvatarOcclusion
    {
        /// <summary>가릴 때의 투명도. 실루엣은 보이되 카드는 읽히는 값.</summary>
        public const float FadedAlpha = 0.35f;

        /// <summary>투명해지고 돌아오는 속도. 클수록 즉각적이다.</summary>
        public const float FadeSpeed = 12f;

        // GetWorldCorners가 배열을 요구하므로 매 프레임 할당하지 않도록 재사용한다.
        // UI는 메인 스레드에서만 도니 공유해도 안전하다.
        private static readonly Vector3[] CornerBuffer = new Vector3[4];

        /// <summary>
        /// RectTransform의 월드 기준 AABB. 캔버스가 같으면 그대로 비교할 수 있다.
        /// <paramref name="scale"/>로 판정 상자를 줄인다(캐릭터 이미지의 투명 여백 보정).
        /// </summary>
        public static Rect WorldRect(RectTransform rect, Vector2 scale)
        {
            if (rect == null) return Rect.zero;

            rect.GetWorldCorners(CornerBuffer);

            float minX = CornerBuffer[0].x, maxX = CornerBuffer[0].x;
            float minY = CornerBuffer[0].y, maxY = CornerBuffer[0].y;
            for (int i = 1; i < 4; i++)
            {
                var c = CornerBuffer[i];
                if (c.x < minX) minX = c.x; else if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y; else if (c.y > maxY) maxY = c.y;
            }

            // 회전한 카드도 네 모서리를 감싸는 상자로 잡히므로 조금 넉넉하게 판정된다.
            var result = new Rect(minX, minY, maxX - minX, maxY - minY);
            return scale == Vector2.one ? result : Shrink(result, scale);
        }

        /// <summary>중심을 유지한 채 크기만 줄인다.</summary>
        public static Rect Shrink(Rect rect, Vector2 scale)
        {
            Vector2 center = rect.center;
            float w = rect.width * Mathf.Max(0f, scale.x);
            float h = rect.height * Mathf.Max(0f, scale.y);
            return new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
        }

        /// <summary>두 AABB가 겹치는지. 변만 스치는 경우는 겹치지 않은 것으로 본다.</summary>
        public static bool Overlaps(Rect a, Rect b)
        {
            if (a.width <= 0f || a.height <= 0f || b.width <= 0f || b.height <= 0f) return false;
            return a.xMin < b.xMax && a.xMax > b.xMin
                && a.yMin < b.yMax && a.yMax > b.yMin;
        }

        /// <summary>
        /// UGUI에서 <paramref name="earlier"/>가 <paramref name="later"/>보다 먼저 그려지는지.
        /// 즉 later가 earlier를 덮는지.
        ///
        /// 같은 캔버스 안에서 uGUI는 계층을 위에서 아래로 훑으며 그리므로, 부모는 자식보다
        /// 먼저 그려지고 형제끼리는 인덱스가 작은 쪽이 먼저 그려진다. 갈라지는 지점의
        /// 형제 인덱스만 비교하면 된다.
        ///
        /// 캔버스가 다르거나 sortingOrder를 쓰면 이 계산은 맞지 않는다.
        /// </summary>
        public static bool DrawsBefore(Transform earlier, Transform later)
        {
            if (earlier == null || later == null || earlier == later) return false;

            // 루트까지의 경로를 각각 쌓는다.
            var a = new System.Collections.Generic.List<Transform>();
            for (var t = earlier; t != null; t = t.parent) a.Add(t);
            var b = new System.Collections.Generic.List<Transform>();
            for (var t = later; t != null; t = t.parent) b.Add(t);

            // 뒤에서부터(루트부터) 같이 내려가며 갈라지는 지점을 찾는다.
            int i = a.Count - 1, j = b.Count - 1;
            if (a[i] != b[j]) return false; // 서로 다른 계층 — 비교 불가

            while (i > 0 && j > 0 && a[i - 1] == b[j - 1]) { i--; j--; }

            // 한쪽이 다른 쪽의 조상이면, 조상이 먼저 그려진다.
            if (i == 0) return true;   // earlier가 later의 조상
            if (j == 0) return false;  // later가 earlier의 조상

            return a[i - 1].GetSiblingIndex() < b[j - 1].GetSiblingIndex();
        }

        /// <summary>
        /// 투명도를 목표값 쪽으로 한 프레임 옮긴다. 프레임률에 무관한 지수 감쇠라
        /// 60fps든 144fps든 같은 시간에 같은 만큼 사라진다.
        /// </summary>
        public static float StepAlpha(float current, float target, float deltaTime)
        {
            if (deltaTime <= 0f) return current;

            float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-FadeSpeed * deltaTime));

            // 지수 감쇠는 목표에 영원히 닿지 않는다. 눈에 안 보이는 차이는 붙여버린다.
            return Mathf.Abs(target - next) < 0.002f ? target : next;
        }
    }
}
