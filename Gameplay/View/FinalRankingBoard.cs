using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 매치 종료 순위표. 카드 한 장이 사람 한 명이고, <b>왼쪽부터 1등</b>이다.
    ///
    /// 텍스트 한 덩어리였을 때의 문제는 글자 크기가 아니라 <b>구분</b>이었다 — 15pt 네 줄이
    /// 54px 상자 하나에 들어가 있어 등수·사람·잔액이 눈에 갈라지지 않았다. 그래서 카드로
    /// 쪼개고, 사람은 이름이 아니라 <b>초상화와 색</b>으로 알아보게 했다. 닉네임이 아직
    /// 네트워크로 넘어오지 않아 이름은 "플레이어 1"에 머무는데, 정작 사람들은 매치 내내
    /// 서로를 토끼·여우·악어로 부르고 있었다.
    ///
    /// 왕관·비석은 여기 붙이지 않는다. 매치 종료는 이번 판의 승패가 아니라 <b>전체 성적</b>을
    /// 보는 화면이고, 표식은 라운드 판정에서 쓰던 것이라 같은 화면에 두면 방금 진 판을
    /// 말하는지 매치 전체를 말하는지 흐려진다. 등수는 숫자와 색이 이미 말하고 있다.
    ///
    /// 순위는 여기서 계산하지 않는다. 이미 등수대로 정렬된 것을 받아 그리기만 한다 —
    /// 잔액은 좌석에 귀속되고 정렬 규칙(파산자 자동 최하위)은
    /// <see cref="RoundSettlement.RankOrder"/>가 쥐고 있다.
    /// </summary>
    public class FinalRankingBoard : MonoBehaviour
    {
        /// <summary>
        /// 한 사람의 성적. 등수는 배열 순서로 정해지므로 여기 담지 않는다 —
        /// 두 곳에 등수가 있으면 어긋날 수 있다.
        /// </summary>
        public struct Entry
        {
            public Sprite Portrait;
            public Color Accent;      // 캐릭터 색. 머니바 라벨과 같은 색이라 눈이 이어진다
            public string Name;       // 토끼 / 여우 / 악어
            public string Subtitle;   // 지금 맡고 있는 역할, 파산이면 "파산"
            public bool IsLocal;
            public int Balance;
            public bool Bankrupt;
        }

        /// <summary>
        /// 카드 한 장의 부품. 등수마다 색이 다른데 그건 씬에 박아 두고(등수는 자리로
        /// 고정이니까), 실행 중에는 사람에 따라 달라지는 것만 갈아 끼운다.
        /// </summary>
        [System.Serializable]
        public class Row
        {
            public GameObject Root;
            [Tooltip("카드 바탕. 테두리와 배경 모두 실행 중에 그 사람의 캐릭터 색으로 칠해진다.")]
            public RoundedPanelGraphic Frame;
            [Tooltip("카드 왼쪽 끝의 색 띠. 캐릭터 색을 진하게 칠해 어두운 바탕과 짝을 이룬다.")]
            public Image AccentBar;
            [Tooltip("등수 배지의 동그란 바탕. 이것도 캐릭터 색을 따른다.")]
            public RoundedPanelGraphic BadgeFrame;
            public TextMeshProUGUI Rank;
            public Image Portrait;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Subtitle;
            public TextMeshProUGUI Balance;
        }

        [Tooltip("1등부터 왼쪽 순서대로. 자리가 등수를 뜻하므로 순서를 바꾸면 안 된다.")]
        [SerializeField] private Row[] rows = new Row[3];

        [Header("색")]
        [Tooltip("파산자의 잔액 숫자. 카드 색은 캐릭터 색을 지키고, 파산은 숫자와 '파산' 글자로만 알린다.")]
        [SerializeField] private Color bankruptColor = new Color(0.91f, 0.42f, 0.38f, 1f);

        [Tooltip("카드 테두리 두께(px). RoundedPanelGraphic이 색과 두께를 함께 받으므로 여기서 쥐고 있는다.")]
        [SerializeField, Min(0f)] private float borderThickness = 2.5f;

        [Tooltip("'나' 표시 색. 역할 옆에 작은 글자로 붙는다.")]
        [SerializeField] private Color localTagColor = new Color(1f, 0.85f, 0.55f, 1f);

        // 카드 바탕은 그 사람의 색을 어둡게 누른 것이다. 캐릭터 색을 그대로 칠하면
        // 흰 글자가 묻히고, 무채색으로 두면 머니바와 짝이 안 맞는다.
        //
        // 검정과 섞는 비율이라 값이 작을수록 어둡다. 배지는 카드 위에 얹히므로
        // 한 단계 밝게 잡아야 동그라미가 보인다.
        private const float CardTint = 0.17f;
        private const float BadgeTint = 0.30f;

        // 배지 숫자는 흰색에 그 사람의 색을 살짝 섞는다. 순색으로 쓰면 어두운 배지
        // 위에서 읽히지 않는다.
        private const float RankTextTint = 0.35f;

        /// <summary>
        /// 순위표를 그리고 화면에 올린다. <paramref name="ranked"/>는 1등부터 정렬돼 있어야 한다.
        /// 사람이 셋보다 적으면 남는 칸은 꺼진다(자리가 빈 세션도 순위는 성립한다).
        /// </summary>
        public void Show(IReadOnlyList<Entry> ranked)
        {
            gameObject.SetActive(true);
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                if (row == null || row.Root == null) continue;

                bool has = ranked != null && i < ranked.Count;
                row.Root.SetActive(has);
                if (has) Apply(row, i, ranked[i]);
            }
        }

        /// <summary>순위표를 내린다. 라운드 판정으로 돌아갈 때 부른다.</summary>
        public void Hide() => gameObject.SetActive(false);

        private void Apply(Row row, int index, Entry entry)
        {
            // 카드 전체를 그 사람의 색으로 칠한다. 오른쪽 위 머니바 카드와 같은 색이라,
            // 순위표의 한 칸과 머니바의 한 칸이 같은 사람이라는 것이 색만으로 이어진다.
            //
            // 등수는 색이 아니라 <b>번호와 자리</b>가 말한다(왼쪽부터 1등). 금·은·동을
            // 쓰면 사람 색과 싸우게 되는데, 이 화면에서 먼저 찾는 것은 "내 칸"이다.
            if (row.Rank != null)
            {
                row.Rank.text = (index + 1).ToString();
                row.Rank.color = Color.Lerp(Color.white, entry.Accent, RankTextTint);
            }

            if (row.Frame != null)
            {
                row.Frame.SetBorder(entry.Accent, borderThickness);
                row.Frame.SetBackground(Darken(entry.Accent, CardTint));
            }

            if (row.BadgeFrame != null)
            {
                row.BadgeFrame.SetBorder(entry.Accent, 2f);
                row.BadgeFrame.SetBackground(Darken(entry.Accent, BadgeTint));
            }

            if (row.AccentBar != null) row.AccentBar.color = entry.Accent;

            if (row.Portrait != null)
            {
                row.Portrait.sprite = entry.Portrait;
                row.Portrait.enabled = entry.Portrait != null;
            }

            if (row.Name != null)
            {
                row.Name.text = entry.Name;
                row.Name.color = entry.Accent;
            }

            if (row.Subtitle != null)
                row.Subtitle.text = entry.IsLocal
                    ? entry.Subtitle + "   <b>" + Tag(localTagColor,
                        ZooJackText.Get("Common.LocalPlayer", "나")) + "</b>"
                    : entry.Subtitle;

            if (row.Balance == null) return;

            // 머니바와 같은 표기(1,900 + 금화). 같은 숫자가 두 곳에서 다르게 보이면
            // 눈이 한 번 멈춘다. 금화는 글자색에 물들지 않으므로(TMP는 tint를 따로 켜야 한다)
            // 파산해서 숫자가 붉어져도 동전은 노란색 그대로다.
            row.Balance.text = entry.Balance.ToString("N0") + " <sprite index=0>";
            row.Balance.color = entry.Bankrupt ? bankruptColor : Color.white;
        }

        private static string Tag(Color color, string text) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        // 알파는 그대로 두고 밝기만 누른다. 알파까지 섞으면 카드가 투명해져
        // 뒤의 테이블이 비쳐 글자가 읽히지 않는다.
        private static Color Darken(Color color, float amount) =>
            new Color(color.r * amount, color.g * amount, color.b * amount, 1f);
    }
}
