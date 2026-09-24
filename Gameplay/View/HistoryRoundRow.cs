using TMPro;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 기록 화면 '라운드' 칸의 한 줄. 라운드 하나에서 셋의 금액이 어떻게 움직였고,
    /// 딜러가 조작했는지, 겉보기 패자가 고발했는지를 담는다.
    ///
    /// <b>부품을 스스로 들고 있다.</b> 줄은 실행 중에 이 오브젝트를 복제해 만드는데
    /// (<see cref="HistoryPanel"/>), 참조를 패널 쪽에 두면 복제본마다 이름으로 다시 찾아야 한다.
    /// 여기 두면 <see cref="Object.Instantiate(Object)"/>가 배선까지 함께 복제해 준다.
    ///
    /// <b>파일 이름은 클래스 이름과 같아야 한다.</b> 다른 줄 종류와 한 파일에 묶어 두었더니
    /// 컴파일은 되고 실행도 됐지만 씬에 저장되지 않았다 — 씬을 다시 열면 컴포넌트 참조가
    /// 끊긴 채 "references runtime script in scene file"만 남았다.
    /// </summary>
    public class HistoryRoundRow : MonoBehaviour
    {
        [Tooltip("'라운드 3'.")]
        [SerializeField] private TextMeshProUGUI title;

        [Tooltip("토끼 · 악어 · 여우 순서. 자리는 사람에 고정이라 역할이 돌아도 칸은 " +
                 "그대로다 — 머니바와 같은 규칙이다(CharacterIdentity.SeatOrder).")]
        [SerializeField] private HistoryCard[] cards = new HistoryCard[3];

        [Tooltip("카드와 같은 순서. 그 사람이 낸 뇌물이 그 사람 카드 바로 아래 붙는다.")]
        [SerializeField] private TextMeshProUGUI[] bribes = new TextMeshProUGUI[3];

        [Tooltip("이 라운드부터 배역이 바뀌었다는 표식. 4·7라운드에만 켜진다.")]
        [SerializeField] private GameObject rotationBadge;

        [SerializeField] private TextMeshProUGUI manipulation;
        [SerializeField] private TextMeshProUGUI accusation;

        [Header("색")]
        [Tooltip("금액이 늘었을 때. 머니바의 +N 배지와 같은 색이다.")]
        [SerializeField] private Color gainColor = new Color(0.42f, 0.82f, 0.44f, 1f);

        [SerializeField] private Color lossColor = new Color(0.91f, 0.38f, 0.33f, 1f);

        [Tooltip("변화가 없을 때(±0).")]
        [SerializeField] private Color flatColor = new Color(0.78f, 0.75f, 0.68f, 1f);

        [Tooltip("조작했을 때, 그리고 고발했을 때 — 눈에 걸려야 하는 쪽.")]
        [SerializeField] private Color alertColor = new Color(1f, 0.66f, 0.32f, 1f);

        [Tooltip("딜러가 조작하지 않았을 때(공정). 이것도 알고 싶은 결과라 회색으로 두지 않는다.")]
        [SerializeField] private Color fairColor = new Color(0.40f, 0.85f, 0.72f, 1f);

        [Tooltip("고발이 빗나갔을 때. 고발자가 무고 벌금을 문 라운드다.")]
        [SerializeField] private Color failColor = new Color(0.95f, 0.44f, 0.40f, 1f);

        [Tooltip("승복 — 아무 일도 일어나지 않은 라운드.")]
        [SerializeField] private Color calmColor = new Color(0.72f, 0.70f, 0.64f, 1f);

        [Tooltip("뇌물이 딜러에게 실제로 넘어갔을 때. 금화와 같은 계열이다.")]
        [SerializeField] private Color bribeTakenColor = new Color(0.95f, 0.80f, 0.42f, 1f);

        [Tooltip("뇌물을 내지 않았거나, 냈지만 돌려받았을 때.")]
        [SerializeField] private Color bribeIdleColor = new Color(0.55f, 0.53f, 0.48f, 1f);

        /// <param name="rolesJustRotated">
        /// 이 라운드가 <b>바뀐 배역으로 시작</b>하는지. 카드가 사람에 고정이라 교대는
        /// 이름표만 바꾸는데, 그것만으로는 왜 달라졌는지 알 수 없어 표식을 하나 세운다.
        /// </param>
        public void Apply(
            MatchHistory.RoundEntry entry,
            System.Func<CharacterId, Sprite> portraits,
            bool rolesJustRotated = false)
        {
            if (title != null)
                title.text = ZooJackText.Get("History.Round.Title", "라운드 {0}", entry.Round);
            if (rotationBadge != null) rotationBadge.SetActive(rolesJustRotated);

            MatchHistory.Change[] seated = BySeat(entry.Changes);

            if (cards != null && seated != null)
                for (int i = 0; i < cards.Length; i++)
                {
                    bool has = i < seated.Length && seated[i].Character != CharacterId.None;
                    if (cards[i]?.Frame != null) cards[i].Frame.gameObject.SetActive(has);
                    if (bribes != null && i < bribes.Length && bribes[i] != null)
                        bribes[i].gameObject.SetActive(has);
                    if (!has) continue;

                    var change = seated[i];
                    cards[i].Apply(
                        change.Character,
                        portraits?.Invoke(change.Character),
                        RoleLabel(change.Role),
                        DeltaText(change.Delta),
                        change.Delta > 0 ? gainColor : change.Delta < 0 ? lossColor : flatColor);

                    // 뇌물은 카드 밑에 붙인다. 한 줄에 "A는 얼마, B는 얼마"로 몰아 적으면
                    // 위 카드에 이미 있는 이름을 다시 읽어야 누가 낸 것인지 이어진다.
                    if (bribes != null && i < bribes.Length && bribes[i] != null)
                    {
                        bribes[i].text = BribeText(change, out Color bribeColor);
                        bribes[i].color = bribeColor;
                    }
                }

            if (manipulation != null)
            {
                manipulation.text = entry.Manipulated
                    ? ZooJackText.Get("History.Manipulation.Cheated", "조작 여부:  <b>조작함</b>")
                    : ZooJackText.Get("History.Manipulation.Fair", "조작 여부:  <b>공정</b>");
                manipulation.color = entry.Manipulated ? alertColor : fairColor;
            }

            if (accusation == null) return;
            accusation.text = ZooJackText.Get(
                "History.Accusation.Label", "고발 여부:  <b>{0}</b>", AccusationText(entry));
            accusation.color = AccusationColor(entry);
        }

        /// <summary>
        /// 사람 차례로 늘어놓는다. 들어오는 것은 <b>역할 차례</b>(A·딜러·B)라 3라운드마다
        /// 같은 칸의 주인이 바뀐다 — 그러면 매치 내내 한 사람의 돈을 눈으로 따라가던 것이
        /// 4라운드에서 끊긴다. 칸은 사람에 고정이고 도는 것은 이름표뿐이다.
        ///
        /// 동물이 정해지지 않은 자리(빈자리)는 남는 칸에 순서대로 채운다.
        /// </summary>
        private static MatchHistory.Change[] BySeat(MatchHistory.Change[] changes)
        {
            if (changes == null) return null;

            var seats = CharacterIdentity.SeatOrder;
            var seated = new MatchHistory.Change[seats.Length];
            var taken = new bool[seats.Length];
            var placed = new bool[changes.Length];

            for (int i = 0; i < changes.Length; i++)
            {
                int seat = CharacterIdentity.SeatIndex(changes[i].Character);
                if (seat < 0 || taken[seat]) continue;
                seated[seat] = changes[i];
                taken[seat] = true;
                placed[i] = true;
            }

            int next = 0;
            for (int i = 0; i < changes.Length; i++)
            {
                if (placed[i]) continue;
                while (next < taken.Length && taken[next]) next++;
                if (next >= taken.Length) break;
                seated[next] = changes[i];
                taken[next] = true;
            }

            return seated;
        }

        /// <summary>
        /// 고발 칸의 색. 셋 다 다른 일이라 셋 다 다른 색이다 — 고발이 맞았으면 눈에 걸리는
        /// 색, 빗나갔으면 붉은 색(고발자가 무고 벌금을 물었다), 승복은 차분한 색.
        /// </summary>
        private Color AccusationColor(MatchHistory.RoundEntry entry)
        {
            if (entry.Accusation != AccusationChoice.Accuse) return calmColor;
            return entry.AccusationResult == AccusationResult.Failed ? failColor : alertColor;
        }

        /// <summary>
        /// 뇌물 한 줄. <b>낸 금액과 뺏긴 금액은 다르다</b> — 조작이 발각되거나 딜러가
        /// 공정하게 굴리면 전액 돌아온다. 낸 금액만 적으면 잔액 변화와 어긋나 보이므로
        /// 돌아온 경우에는 '반납'을 붙이고 색을 죽인다.
        /// </summary>
        private string BribeText(MatchHistory.Change change, out Color color)
        {
            if (change.Role == PlayerRole.Dealer)
            {
                // 딜러 자리에는 '받은 것'이 아니라 '실제로 챙긴 것'이 적힌다.
                color = change.Bribe > 0 ? bribeTakenColor : bribeIdleColor;
                return change.Bribe > 0
                    ? ZooJackText.Get("History.Bribe.DealerKept", "뇌물 챙김 {0}", Coins(change.Bribe))
                    : ZooJackText.Get("History.Bribe.DealerNone", "뇌물 챙김 없음");
            }

            if (change.Bribe <= 0)
            {
                color = bribeIdleColor;
                return ZooJackText.Get("History.Bribe.None", "뇌물 없음");
            }

            color = change.BribeKept > 0 ? bribeTakenColor : bribeIdleColor;
            return change.BribeKept > 0
                ? ZooJackText.Get("History.Bribe.Paid", "뇌물 {0}", Coins(change.Bribe))
                : ZooJackText.Get("History.Bribe.Returned", "뇌물 {0} · 반납", Coins(change.Bribe));
        }

        private static string Coins(int amount) => amount.ToString("N0") + " <sprite index=0>";

        /// <summary>
        /// 고발 결과 한마디. 고발했으면 맞았는지까지 붙인다 — 고발했다는 사실만으로는
        /// 그 라운드에 무슨 일이 있었는지 알 수 없다.
        /// </summary>
        private static string AccusationText(MatchHistory.RoundEntry entry) => entry.Accusation switch
        {
            AccusationChoice.Accuse => entry.AccusationResult switch
            {
                AccusationResult.Success => ZooJackText.Get("History.Accusation.Success", "고발 · 적중"),
                AccusationResult.Failed  => ZooJackText.Get("History.Accusation.Failed", "고발 · 실패"),
                _                        => ZooJackText.Get("History.Accusation.Accused", "고발")
            },
            AccusationChoice.AcceptResult => ZooJackText.Get("History.Accusation.Accepted", "승복"),
            _                             => ZooJackText.Get("Common.None", "-")
        };

        /// <summary>부호를 반드시 붙인다. 0도 '±0'으로 적어 빈칸과 구별한다.</summary>
        internal static string DeltaText(int delta) =>
            (delta > 0 ? "+" : delta < 0 ? "-" : "±")
            + Mathf.Abs(delta).ToString("N0") + " <sprite index=0>";

        internal static string RoleLabel(PlayerRole role) => role == PlayerRole.None
            ? ZooJackText.Get("Lobby.Card.Free", "빈자리")
            : ZooJackText.RoleName(role);
    }
}
