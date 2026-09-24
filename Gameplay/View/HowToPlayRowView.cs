using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 설명서의 그림 줄 하나 — 가로로 늘어선 것들과, 그 아래 한 줄 설명.
    ///
    /// <b>부품을 스스로 들고 있다.</b> 줄은 실행 중에 복제해 만드는데
    /// (<see cref="HowToPlayPageView"/>), 참조를 쪽 쪽에 두면 복제본마다 이름으로
    /// 다시 찾아야 한다. 여기 두면 복제가 배선까지 함께 옮겨 준다.
    /// </summary>
    public class HowToPlayRowView : MonoBehaviour
    {
        [Tooltip("것들이 늘어서는 곳. HorizontalLayoutGroup이 자리를 잡는다.")]
        [SerializeField] private RectTransform items;

        [Tooltip("줄 높이를 정한다. 그림 크기는 여기서 나온다.")]
        [SerializeField] private LayoutElement itemsLayout;

        [Tooltip("복제해 쓸 본보기. 꺼져 있어야 한다.")]
        [SerializeField] private HowToPlayItemView itemTemplate;

        [Tooltip("줄 아래 한 줄 설명. 비면 사라진다.")]
        [SerializeField] private TextMeshProUGUI caption;

        private readonly List<HowToPlayItemView> pool = new List<HowToPlayItemView>();

        private void Awake()
        {
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        }

        public void Apply(HowToPlayBlock block, CardSpriteRegistry cards, float defaultHeight)
        {
            float height = block != null && block.Height > 0f ? block.Height : defaultHeight;
            if (itemsLayout != null) itemsLayout.preferredHeight = height;

            var list = block?.Items;
            int count = list?.Length ?? 0;

            for (int i = 0; i < count; i++)
            {
                HowToPlayItemView view = Grow(i);
                if (view == null) continue;
                view.gameObject.SetActive(true);
                view.Apply(list[i], cards, height);
            }

            for (int i = count; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);

            if (items != null) items.gameObject.SetActive(count > 0);

            if (caption == null) return;
            string localized = block?.LocalizedText ?? string.Empty;
            bool hasText = !string.IsNullOrWhiteSpace(localized);
            caption.gameObject.SetActive(hasText);
            if (hasText) caption.text = localized;
        }

        /// <summary>
        /// <paramref name="index"/>번째 것을 내놓는다. 아직 없으면 본보기를 복제한다.
        /// 한 번 만든 것은 지우지 않고 다시 쓴다 — 쪽을 넘길 때마다 만들고 부수면
        /// 넘어가는 도중에 화면이 한 번씩 걸린다.
        /// </summary>
        private HowToPlayItemView Grow(int index)
        {
            if (itemTemplate == null || items == null) return null;

            while (pool.Count <= index)
            {
                var made = Object.Instantiate(itemTemplate, items);
                made.name = itemTemplate.name + "_" + pool.Count;
                pool.Add(made);
            }

            return pool[index];
        }
    }
}
