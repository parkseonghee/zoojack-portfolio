using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 기록 화면의 한 줄에 들어가는 사람 카드 하나 — 초상화·역할·금액.
    ///
    /// 오른쪽 위 머니바 카드와 같은 모습으로 만든다. 같은 사람을 두 곳에서 다르게 그리면
    /// 눈이 이어지지 않아 "이 칸이 나인가"를 매번 다시 찾게 된다.
    ///
    /// MonoBehaviour가 아니다 — <see cref="HistoryRoundRow"/>와 <see cref="HistoryRankRow"/>가
    /// 배열로 들고 있는 부품 묶음일 뿐이다.
    /// </summary>
    [System.Serializable]
    public class HistoryCard
    {
        public RoundedPanelGraphic Frame;
        public Image Portrait;

        [Tooltip("역할 이름. 랭킹 줄에서는 '1위 · 딜러'처럼 등수가 앞에 붙는다.")]
        public TextMeshProUGUI RoleLabel;

        [Tooltip("금액. 라운드 줄에서는 변화량(±N), 랭킹 줄에서는 최종 잔액.")]
        public TextMeshProUGUI Amount;

        // 카드 바탕은 그 사람의 색을 어둡게 누른 것이다. 순색을 칠하면 흰 글자가 묻힌다.
        // FinalRankingBoard와 같은 비율을 쓴다.
        private const float CardTint = 0.17f;
        private const float BorderThickness = 2f;

        /// <summary>사람과 글자를 갈아 끼운다. 색은 그 사람의 캐릭터 색을 따른다.</summary>
        public void Apply(CharacterId who, Sprite portrait, string role, string amount, Color amountColor)
        {
            Color accent = ZooJackPalette.CharacterAccent(who);

            if (Frame != null)
            {
                Frame.SetBorder(accent, BorderThickness);
                Frame.SetBackground(new Color(
                    accent.r * CardTint, accent.g * CardTint, accent.b * CardTint, 1f));
            }

            if (Portrait != null)
            {
                Portrait.sprite = portrait;
                Portrait.enabled = portrait != null;
            }

            if (RoleLabel != null)
            {
                RoleLabel.text = role;
                RoleLabel.color = accent;
            }

            if (Amount == null) return;
            Amount.text = amount;
            Amount.color = amountColor;
        }
    }
}
