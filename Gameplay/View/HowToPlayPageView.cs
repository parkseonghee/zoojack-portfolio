using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 설명서의 종이 한 장. <see cref="HowToPlayPage"/> 하나를 받아 그린다.
    ///
    /// 여섯 장이 돌아가며 쓰인다 — 펼쳐 놓은 왼쪽·오른쪽, 그리고 넘어가는 장의
    /// 앞뒤 두 장씩. 어느 쪽을 그릴지는 <see cref="HowToPlayPanel"/>이 정한다.
    ///
    /// <b>덩어리는 실행 중에 만든다.</b> 쪽마다 덩어리 수와 종류가 다르므로 씬에
    /// 미리 깔아 둘 수 없다. 본보기 셋(제목·그림 줄·글)을 복제해 쌓고, 남는 것은
    /// 지우지 않고 꺼 둔다.
    ///
    /// <b>파일 이름은 클래스 이름과 같아야 한다.</b> MonoBehaviour를 다른 파일에 얹으면
    /// 컴파일은 되지만 씬에 저장되지 않는다(기록 화면에서 실제로 겪었다).
    /// </summary>
    public class HowToPlayPageView : MonoBehaviour
    {
        [Tooltip("덩어리가 위에서 아래로 쌓이는 곳. VerticalLayoutGroup이 자리를 잡는다.")]
        [SerializeField] private RectTransform content;

        [Header("본보기 — 모두 꺼 둔다")]
        [SerializeField] private TextMeshProUGUI headingTemplate;

        [SerializeField] private HowToPlayRowView rowTemplate;
        [SerializeField] private TextMeshProUGUI captionTemplate;

        [Header("종이")]
        [Tooltip("바깥쪽 아래 구석의 쪽수.")]
        [SerializeField] private TextMeshProUGUI pageNumber;

        [Tooltip("넘어가는 동안 종이에 지는 그늘. 세워질수록 짙어진다.")]
        [SerializeField] private PageShadeGraphic shade;

        [Tooltip("그림 줄의 기본 높이(px). 덩어리가 따로 정하지 않으면 이 값을 쓴다.")]
        [SerializeField] private float defaultRowHeight = 150f;

        private readonly List<TextMeshProUGUI> headings = new List<TextMeshProUGUI>();
        private readonly List<HowToPlayRowView> rows = new List<HowToPlayRowView>();
        private readonly List<TextMeshProUGUI> captions = new List<TextMeshProUGUI>();

        private void Awake()
        {
            if (headingTemplate != null) headingTemplate.gameObject.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (captionTemplate != null) captionTemplate.gameObject.SetActive(false);
        }

        /// <summary>
        /// 이 장에 <paramref name="page"/>를 그린다. <paramref name="number"/>는
        /// 사람이 읽는 쪽수(1부터)이고, 0 이하면 쪽수를 감춘다 — 없는 쪽이라는 뜻이다.
        /// </summary>
        public void Apply(HowToPlayPage page, int number, CardSpriteRegistry cards)
        {
            bool exists = number > 0;

            // 있는 쪽인지부터 가른다. 쪽 수가 홀수면 마지막 오른쪽이 빈 종이로 남는데,
            // 그 자리에 쪽수를 적으면 없는 쪽이 있는 것처럼 보인다.
            if (pageNumber != null)
            {
                pageNumber.gameObject.SetActive(exists);
                if (exists) pageNumber.text = number.ToString();
            }

            HideAll();
            if (!exists || page?.Blocks == null) return;

            int usedHeadings = 0, usedRows = 0, usedCaptions = 0;

            for (int i = 0; i < page.Blocks.Length; i++)
            {
                HowToPlayBlock block = page.Blocks[i];
                if (block == null) continue;

                switch (block.Kind)
                {
                    case HowToPlayBlockKind.Heading:
                    {
                        var view = Grow(headings, headingTemplate, usedHeadings++);
                        if (view == null) break;
                        view.text = block.LocalizedText;
                        Place(view.transform, i);
                        break;
                    }

                    case HowToPlayBlockKind.Caption:
                    {
                        var view = Grow(captions, captionTemplate, usedCaptions++);
                        if (view == null) break;
                        view.text = block.LocalizedText;
                        Place(view.transform, i);
                        break;
                    }

                    default:
                    {
                        var view = Grow(rows, rowTemplate, usedRows++);
                        if (view == null) break;
                        view.Apply(block, cards, defaultRowHeight);
                        Place(view.transform, i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 그늘의 짙기(0~1). 넘어가는 장이 세워질수록 1에 가까워진다 —
        /// 이것이 없으면 종이가 접히는 것이 아니라 가로로 눌리는 것처럼만 보인다.
        /// </summary>
        public void SetShade(float amount) => shade?.SetStrength(amount);

        // ── 덩어리 만들기 ────────────────────────────────────────────

        private void HideAll()
        {
            foreach (var view in headings) view.gameObject.SetActive(false);
            foreach (var view in rows) view.gameObject.SetActive(false);
            foreach (var view in captions) view.gameObject.SetActive(false);
        }

        /// <summary>
        /// 덩어리를 인스펙터에 적은 순서대로 세운다. 종류별로 따로 쌓아 두므로
        /// 이것이 없으면 제목이 전부 위로, 그림이 전부 아래로 몰린다.
        /// </summary>
        private static void Place(Transform view, int order)
        {
            view.gameObject.SetActive(true);
            view.SetSiblingIndex(order);
        }

        private T Grow<T>(List<T> pool, T template, int index) where T : Component
        {
            if (template == null || content == null) return null;

            while (pool.Count <= index)
            {
                T made = Object.Instantiate(template, content);
                made.name = template.name + "_" + pool.Count;
                made.gameObject.SetActive(false);
                pool.Add(made);
            }

            return pool[index];
        }
    }
}
