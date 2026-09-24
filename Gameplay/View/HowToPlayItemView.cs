using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 그림 줄에 놓인 것 하나 — 카드 한 장이거나, 아무 그림이거나, <c>→</c> 같은 기호.
    ///
    /// 너비는 그림의 <b>원래 비율</b>에서 뽑는다. 카드마다 크기가 다르지 않으므로
    /// 높이만 맞추면 줄이 저절로 가지런해진다.
    /// </summary>
    public class HowToPlayItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI symbol;
        [SerializeField] private LayoutElement layout;

        /// <summary>기호가 적어도 차지하는 폭. 줄 높이에 곱한다.</summary>
        private const float SymbolMinWidthRatio = 0.55f;

        /// <summary>글 양옆에 두는 여백. 줄 높이에 곱한다.</summary>
        private const float SymbolPadRatio = 0.16f;

        public void Apply(HowToPlayItem item, CardSpriteRegistry cards, float rowHeight)
        {
            float height = rowHeight * Mathf.Max(0.1f, item?.Scale ?? 1f);

            if (item != null && item.Kind == HowToPlayItemKind.Symbol)
            {
                if (icon != null) icon.enabled = false;

                float width = height * Mathf.Max(SymbolMinWidthRatio, item.MinWidth);
                if (symbol != null)
                {
                    string symbolText = string.IsNullOrWhiteSpace(item.TextKey)
                        ? item.Symbol
                        : ZooJackText.Get(item.TextKey, item.Symbol);
                    symbol.gameObject.SetActive(true);
                    symbol.text = symbolText;
                    symbol.fontSize = height * 0.42f;

                    // 여기 오는 것이 화살표 한 글자만은 아니다 — "플레이어 B" 같은 말도
                    // 온다. 폭을 높이에서만 뽑으면 그런 글은 잘리거나 두 줄로 접힌다.
                    width = Mathf.Max(width,
                        symbol.GetPreferredValues(symbolText).x + height * SymbolPadRatio);
                }

                Size(height, width);
                return;
            }

            if (symbol != null) symbol.gameObject.SetActive(false);

            Sprite sprite = Resolve(item, cards);
            if (icon != null)
            {
                icon.enabled = sprite != null;
                icon.sprite = sprite;
            }

            // 그림이 없으면 자리도 차지하지 않는다 — 빈 네모가 줄에 끼면
            // 무엇을 빠뜨렸는지가 아니라 그것도 설명의 일부인 것처럼 보인다.
            if (sprite == null) { Size(0f, 0f); return; }

            float aspect = sprite.rect.height <= 0f ? 1f : sprite.rect.width / sprite.rect.height;
            Size(height, height * aspect);
        }

        private static Sprite Resolve(HowToPlayItem item, CardSpriteRegistry cards)
        {
            if (item == null) return null;

            switch (item.Kind)
            {
                case HowToPlayItemKind.Card:
                    return cards == null
                        ? null
                        : cards.GetSprite(new BlackjackCard { Rank = item.Rank, Suit = item.Suit });

                case HowToPlayItemKind.CardBack:
                    return cards == null ? null : cards.CardBack;

                default:
                    return item.Image;
            }
        }

        private void Size(float height, float width)
        {
            if (layout == null) return;
            layout.preferredHeight = height;
            layout.preferredWidth = width;
        }
    }
}
