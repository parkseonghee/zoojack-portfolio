using System.Collections.Generic;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 카드 영역의 중앙을 기준으로 현재 카드 수만큼 슬롯을 균등 배치합니다.
    /// 영역 위치와 카드 크기, 간격은 씬/인스펙터 설정을 그대로 사용합니다.
    /// </summary>
    public class AdaptiveHandLayout : MonoBehaviour
    {
        [SerializeField] private CardView[] cardViews;
        [SerializeField] private Vector2 cardSize = new Vector2(126f, 176f);
        [SerializeField, Min(0f)] private float preferredSpacing = 16f;
        [SerializeField, Min(1f)] private float maximumWidth = 560f;

        public IReadOnlyList<CardView> CardViews => cardViews;
        public int Capacity => cardViews?.Length ?? 0;

        public void Configure(
            CardView[] views,
            Vector2 size,
            float spacing,
            float maxWidth)
        {
            cardViews = views;
            cardSize = size;
            preferredSpacing = Mathf.Max(0f, spacing);
            maximumWidth = Mathf.Max(1f, maxWidth);
        }

        public void Arrange(int visibleCount)
        {
            if (cardViews == null || cardViews.Length == 0) return;

            int count = Mathf.Clamp(visibleCount, 0, cardViews.Length);
            if (count == 0) return;

            float spacing = preferredSpacing;
            float desiredWidth = cardSize.x * count + spacing * (count - 1);
            if (count > 1 && desiredWidth > maximumWidth)
                spacing = Mathf.Max(0f, (maximumWidth - cardSize.x * count) / (count - 1));

            float step = cardSize.x + spacing;
            float startX = -step * (count - 1) * 0.5f;

            for (int i = 0; i < cardViews.Length; i++)
            {
                CardView view = cardViews[i];
                if (view == null) continue;

                RectTransform rect = view.GetComponent<RectTransform>();
                if (rect == null) continue;

                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = cardSize;

                // 자리는 카드에게 맡긴다. 딜러가 준 카드는 위에서 내려오는 중이라
                // 여기서 매 프레임 제자리로 되돌리면 내려오는 모습이 보이지 않는다.
                if (i < count)
                    view.PlaceAt(new Vector2(startX + step * i, 0f));
            }
        }
    }
}
