using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 기록 화면. 칸이 둘이다 — <b>라운드</b>는 지금 매치의 라운드별 금액 변화·조작·고발을,
    /// <b>랭킹</b>은 이 세션에서 끝난 매치들의 순위를 아래로 쌓아 보여 준다.
    ///
    /// <b>값을 들고 있지 않다.</b> 주인은 <see cref="MatchHistory"/>이고 여기는 그것을 그린다.
    /// 그래서 매치가 끝나 씬을 오가도 랭킹이 남고, 이 화면을 한 번도 열지 않아도 기록은 쌓인다.
    /// <see cref="SettingsPanel"/>과 같은 구조다.
    ///
    /// <b>줄은 실행 중에 만든다.</b> 라운드는 아홉 판까지, 랭킹은 끝이 없으므로 씬에 미리
    /// 깔아 둘 수 없다. 도구가 만들어 둔 본보기 줄을 복제해 쓴다.
    ///
    /// 배선은 <c>ZooJack/게임/기록 화면 만들기</c> 메뉴가 해 준다.
    /// </summary>
    public class HistoryPanel : MonoBehaviour
    {
        [Header("열고 닫기")]
        [Tooltip("기록 내용을 담은 오브젝트. '기록' 버튼은 이 바깥에 있어야 다시 열 수 있다.")]
        [SerializeField] private GameObject window;

        [Tooltip("화면을 덮는 어두운 판. 여기를 눌러도 닫힌다.")]
        [SerializeField] private Button scrim;

        [SerializeField] private Button btnOpen;
        [SerializeField] private Button btnClose;

        [Header("칸 고르기")]
        [SerializeField] private Button btnRoundTab;
        [SerializeField] private Button btnRankTab;
        [SerializeField] private RoundedPanelGraphic roundTabFrame;
        [SerializeField] private RoundedPanelGraphic rankTabFrame;
        [SerializeField] private TextMeshProUGUI roundTabLabel;
        [SerializeField] private TextMeshProUGUI rankTabLabel;

        [Header("라운드 칸")]
        [Tooltip("줄이 쌓이는 곳. VerticalLayoutGroup이 자리를 잡아 준다.")]
        [SerializeField] private RectTransform roundContent;

        [Tooltip("복제해 쓸 본보기 줄. 꺼져 있어야 한다 — 켜 두면 빈 줄 하나가 늘 보인다.")]
        [SerializeField] private HistoryRoundRow roundTemplate;

        [Tooltip("기록이 없을 때 대신 뜨는 글자.")]
        [SerializeField] private GameObject roundEmpty;

        [SerializeField] private ScrollRect roundScroll;

        [Header("랭킹 칸")]
        [SerializeField] private RectTransform rankContent;
        [SerializeField] private HistoryRankRow rankTemplate;
        [SerializeField] private GameObject rankEmpty;
        [SerializeField] private ScrollRect rankScroll;

        [Header("초상화")]
        [Tooltip("머니바와 같은 원형 초상화. 도구가 GameDirector에서 그대로 복사해 넣는다.")]
        [SerializeField] private Sprite portraitRabbit;

        [SerializeField] private Sprite portraitFox;
        [SerializeField] private Sprite portraitCroc;

        [Header("칸 색")]
        [Tooltip("고른 칸. 카드와 같은 색이라 칸과 내용이 한 장으로 이어져 보인다.")]
        [SerializeField] private Color tabActiveFill = new Color(0.106f, 0.122f, 0.133f, 0.99f);

        [SerializeField] private Color tabIdleFill = new Color(0.06f, 0.07f, 0.08f, 0.92f);
        [SerializeField] private Color tabActiveText = new Color(0.949f, 0.910f, 0.800f, 1f);
        [SerializeField] private Color tabIdleText = new Color(0.55f, 0.53f, 0.48f, 1f);

        /// <summary>어느 칸을 보고 있는지. 창을 닫고 다시 열어도 그 칸을 그대로 유지한다.</summary>
        private bool showingRanks;

        /// <summary>마지막으로 그린 기록의 번호. 이것이 그대로면 다시 그릴 것이 없다.</summary>
        private int drawnVersion = -1;

        // 복제해 둔 줄. 매번 지우고 새로 만들지 않고 남는 것만 껐다 — 기록은 한 줄씩
        // 늘어나므로, 열 때마다 아홉 줄을 다시 만들면 그때마다 화면이 한 번 멈춘다.
        private readonly List<HistoryRoundRow> roundRows = new List<HistoryRoundRow>();
        private readonly List<HistoryRankRow> rankRows = new List<HistoryRankRow>();

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            if (btnOpen != null) btnOpen.onClick.AddListener(Open);
            if (btnClose != null) btnClose.onClick.AddListener(Close);
            if (scrim != null) scrim.onClick.AddListener(Close);

            if (btnRoundTab != null) btnRoundTab.onClick.AddListener(() => SelectTab(false));
            if (btnRankTab != null) btnRankTab.onClick.AddListener(() => SelectTab(true));

            // 본보기는 절대 켜지 않는다. 이것이 켜져 있으면 기록이 없어도 빈 줄이 하나 보이고,
            // 복제본에 함께 실려 줄마다 유령이 하나씩 붙는다.
            if (roundTemplate != null) roundTemplate.gameObject.SetActive(false);
            if (rankTemplate != null) rankTemplate.gameObject.SetActive(false);

            if (window != null) window.SetActive(false);
        }

        // ── 열고 닫기 ────────────────────────────────────────────────

        public void Open()
        {
            GameAudio.PlayClick();

            // 열 때마다 확인한다. 창이 닫혀 있는 동안에도 기록은 쌓이므로,
            // 그때마다 그리면 아무도 보지 않는 줄을 만드는 데 시간을 쓴다.
            Redraw(force: false);
            ApplyTab();

            if (window != null) window.SetActive(true);
            transform.SetAsLastSibling();   // 어느 패널이 떠 있든 그 위에
        }

        public void Close()
        {
            GameAudio.PlayClick();
            if (window != null) window.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void SelectTab(bool ranks)
        {
            GameAudio.PlayClick();
            showingRanks = ranks;
            ApplyTab();
        }

        private void ApplyTab()
        {
            if (roundScroll != null) roundScroll.gameObject.SetActive(!showingRanks);
            if (rankScroll != null) rankScroll.gameObject.SetActive(showingRanks);

            Paint(roundTabFrame, roundTabLabel, !showingRanks);
            Paint(rankTabFrame, rankTabLabel, showingRanks);
        }

        private void Paint(RoundedPanelGraphic frame, TextMeshProUGUI label, bool active)
        {
            // 고른 칸만 카드와 같은 색으로 채운다. 글자 굵기까지 바꾸는 이유는 색만으로는
            // 어느 칸이 열려 있는지 한눈에 갈라지지 않아서다.
            if (frame != null) frame.SetBackground(active ? tabActiveFill : tabIdleFill);
            if (label == null) return;
            label.color = active ? tabActiveText : tabIdleText;
            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }

        // ── 그리기 ───────────────────────────────────────────────────

        /// <summary>
        /// 창이 열려 있는 동안 기록이 바뀌면(남이 라운드를 끝내면) 그때 다시 그린다.
        /// 매 프레임 도는 대신 번호만 비교하므로 값이 그대로면 아무 일도 하지 않는다.
        /// </summary>
        private void Update()
        {
            if (IsOpen) Redraw(force: false);
        }

        private void Redraw(bool force)
        {
            if (!force && drawnVersion == MatchHistory.Version) return;
            drawnVersion = MatchHistory.Version;

            DrawRounds();
            DrawRanks();
        }

        /// <summary>
        /// 라운드 줄을 쌓는다.
        ///
        /// <b>자리가 바뀐 채로 시작하는 라운드에 표식을 붙인다.</b> 줄 안의 카드는 사람에
        /// 고정이라(<see cref="HistoryRoundRow"/>) 교대는 카드의 이름표만 바꾼다. 그 이름표가
        /// 달라진 <b>바로 그 줄</b>에 표식이 서야 "여기서부터 배역이 다르다"로 읽힌다 —
        /// 3라운드에 붙이면 아직 안 바뀐 줄을 가리키게 된다(4·7라운드가 맞다).
        ///
        /// 그래서 조건도 '이 라운드 <i>직전에</i> 돌았는가'다. 그 라운드가 기록에 있다는
        /// 것 자체가 교대가 실제로 일어났다는 증거라, 따로 확인할 것이 없다.
        /// </summary>
        private void DrawRounds()
        {
            var entries = MatchHistory.Rounds;
            if (roundEmpty != null) roundEmpty.SetActive(entries.Count == 0);
            if (roundContent == null || roundTemplate == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                HistoryRoundRow row = Grow(roundRows, roundTemplate, roundContent, i);
                if (row == null) continue;

                bool rotated =
                    RoundSettlement.ShouldRotateRolesAfterRound(entries[i].Round - 1);

                row.gameObject.SetActive(true);
                row.Apply(entries[i], PortraitFor, rotated);
            }

            for (int i = entries.Count; i < roundRows.Count; i++)
                roundRows[i].gameObject.SetActive(false);
        }

        private void DrawRanks()
        {
            var entries = MatchHistory.Ranks;
            if (rankEmpty != null) rankEmpty.SetActive(entries.Count == 0);
            if (rankContent == null || rankTemplate == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                HistoryRankRow row = Grow(rankRows, rankTemplate, rankContent, i);
                if (row == null) continue;
                row.gameObject.SetActive(true);
                row.Apply(entries[i], PortraitFor);
            }

            for (int i = entries.Count; i < rankRows.Count; i++)
                rankRows[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// <paramref name="index"/>번째 줄을 내놓는다. 아직 없으면 본보기를 복제해 만든다.
        /// 한 번 만든 줄은 지우지 않고 다시 쓴다.
        /// </summary>
        private static T Grow<T>(List<T> pool, T template, RectTransform parent, int index)
            where T : Component
        {
            while (pool.Count <= index)
            {
                T made = Object.Instantiate(template, parent);
                made.name = template.name + "_" + pool.Count;
                pool.Add(made);
            }
            return pool[index];
        }

        private Sprite PortraitFor(CharacterId id) => id switch
        {
            CharacterId.Rabbit => portraitRabbit,
            CharacterId.Fox    => portraitFox,
            CharacterId.Croc   => portraitCroc,
            _                  => null
        };
    }
}
