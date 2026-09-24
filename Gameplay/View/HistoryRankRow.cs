using TMPro;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 기록 화면 '랭킹' 칸의 한 줄. 끝난 매치 하나의 순위를 <b>왼쪽부터 1등</b>으로 담는다.
    /// 매치가 끝날 때마다 이 줄이 아래로 하나씩 쌓인다.
    ///
    /// <see cref="HistoryRoundRow"/>와 마찬가지로 부품을 스스로 들고 있고, 그래서
    /// 파일 이름이 클래스 이름과 같아야 한다.
    /// </summary>
    public class HistoryRankRow : MonoBehaviour
    {
        [Tooltip("'1번째 게임'.")]
        [SerializeField] private TextMeshProUGUI title;

        [Tooltip("1등부터 왼쪽 순서대로. 자리가 등수를 뜻하므로 순서를 바꾸면 안 된다.")]
        [SerializeField] private HistoryCard[] cards = new HistoryCard[3];

        [Header("색")]
        [Tooltip("파산한 사람의 잔액 숫자. 순위표(FinalRankingBoard)와 같은 색이다.")]
        [SerializeField] private Color bankruptColor = new Color(0.91f, 0.42f, 0.38f, 1f);

        public void Apply(MatchHistory.RankEntry entry, System.Func<CharacterId, Sprite> portraits)
        {
            if (title != null)
                title.text = ZooJackText.Get(
                    "History.Rank.Title", "{0}번째 게임", entry.MatchNumber);
            if (cards == null || entry.Ranked == null) return;

            for (int i = 0; i < cards.Length; i++)
            {
                bool has = i < entry.Ranked.Length;
                if (cards[i]?.Frame != null) cards[i].Frame.gameObject.SetActive(has);
                if (!has) continue;

                var standing = entry.Ranked[i];

                // 등수를 역할 앞에 붙인다. 이 화면에는 등수 배지를 둘 자리가 없고(줄이
                // 여러 판 쌓이므로 낮아야 한다), 왼쪽부터 1등이라는 규칙만으로는
                // 줄이 여러 개 겹쳐 보일 때 몇 등인지 세어야 한다.
                //
                // 결과가 붙더라도 직군명을 지우지 않는다. 같은 캐릭터도 매치마다
                // 끝날 때의 직군이 달라질 수 있으므로 랭킹 기록에 함께 남겨야 한다.
                // 항복이 가장 먼저다. 판이 왜 여기서 끝났는지를 말하는 것이 그것이고,
                // 접고 나간 사람은 어차피 꼴찌라 '승리'와 겹칠 일이 없다.
                string result = standing.Surrendered
                    ? ResultSuffix(ZooJackText.Get("History.Result.Surrender", "항복"))
                    : i == 0
                        ? ResultSuffix(ZooJackText.Get("History.Result.Win", "승리"))
                        : standing.Bankrupt
                            ? ResultSuffix(ZooJackText.Get("History.Result.Bankrupt", "파산"))
                            : string.Empty;
                string label = ZooJackText.Get(
                    "History.Rank.PositionRole", "{0}위 · {1}", i + 1,
                    HistoryRoundRow.RoleLabel(standing.Role)) + result;

                cards[i].Apply(
                    standing.Character,
                    portraits?.Invoke(standing.Character),
                    label,
                    standing.Balance.ToString("N0") + " <sprite index=0>",
                    standing.Bankrupt ? bankruptColor : Color.white);
            }
        }

        private static string ResultSuffix(string result) => ZooJackText.Get(
            "History.Rank.ResultSuffix", " · {0}", result);
    }
}
