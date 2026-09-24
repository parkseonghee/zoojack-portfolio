using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 로컬 핫시트 3인 블랙잭 게임 컨트롤러.
    /// RoundEngine을 직접 호출하여 씬 UI를 구동한다.
    /// </summary>
    public class GameDirector : MonoBehaviour, MatchSurrender.IHost
    {
        // ── 카드 이미지 ───────────────────────────────────────────────
        // 관점 기반 슬롯: 보는 사람 자신의 손패는 하단(앞면), 상대 손패는 상단(뒷면).
        [Header("Card Views - Self (하단, 앞면)")]
        [Tooltip("손패 슬롯을 왼쪽부터 순서대로 넣는다. 칸 수는 RoundEngine.DoubledMaxCardsPerHand까지 " +
                 "쓸 수 있고, 비워 둔 칸은 그냥 그리지 않는다.")]
        [SerializeField] internal CardView[] cardsSelf = new CardView[RoundEngine.MaxCardsPerHand];
        [SerializeField] internal AdaptiveHandLayout handLayoutA;

        [Header("Card Views - Opponent (상단, 뒷면)")]
        [SerializeField] internal CardView[] cardsOpp = new CardView[RoundEngine.MaxCardsPerHand];
        [SerializeField] internal AdaptiveHandLayout handLayoutB;

        [Header("Bet Chips (테이블 베팅 자리)")]
        [SerializeField] internal BetChipStackView chipsA;
        [SerializeField] internal BetChipStackView chipsB;

        [Header("Avatars (테이블 위 캐릭터)")]
        [SerializeField] internal AvatarStage avatarStage;

        [Header("Emotions")]
        [SerializeField] internal EmotionWheelController emotionWheel;

        [Header("Card Views - Dealer Candidates")]
        [Tooltip("딜러가 고르는 후보 카드. 칸 수는 BlackjackDeckService.DefaultCandidateCount와 맞춘다.")]
        [SerializeField] internal CardView[] cardsDealer =
            new CardView[BlackjackDeckService.DefaultCandidateCount];

        // ── 항상 보이는 상태 텍스트 ──────────────────────────────────
        [Header("Status Bar")]
        [SerializeField] internal TextMeshProUGUI txtPhase;
        [SerializeField] internal TextMeshProUGUI txtPhaseAction;
        [SerializeField] internal TextMeshProUGUI txtRound;

        [Header("Money Bar")]
        [SerializeField] internal MoneyCard[] moneyCards = new MoneyCard[3];

        // ── 베팅 금액 표시 ────────────────────────────────────────────
        // 각자 건 금액은 자기 칩 더미 아래에, 두 베팅의 합계는 화면 위 가운데에 상시로 띄운다.
        [Header("베팅 금액 표시")]
        [Tooltip("칩 더미 아래에 붙는 금액. 더미 크기에 맞춰 자리를 다시 잡는다.")]
        [SerializeField] internal TextMeshProUGUI txtBetAmountA;
        [SerializeField] internal TextMeshProUGUI txtBetAmountB;

        [Tooltip("두 베팅의 합계 배지. 타이머 카드 아래 가운데.")]
        [SerializeField] internal GameObject betTotalCard;
        [SerializeField] internal TextMeshProUGUI txtBetTotalLabel;
        [FormerlySerializedAs("txtBetTotal")]
        [SerializeField] internal TextMeshProUGUI txtBetTotalAmount;

        // ── 캐릭터 그림 원본 ──────────────────────────────────────────
        // 캐릭터는 사람에게 붙고 매치 내내 고정이다. 역할이 교대되면 이 그림들이
        // 역할 자리 사이를 옮겨 다닌다.
        //
        // 초상화(원형 아이콘)와 아바타(전신)는 서로 다른 그림이므로 따로 들고 있어야 한다.
        // 하나로 합치면 머니바에 전신이 들어가거나 테이블에 동그란 아이콘이 서게 된다.
        [Header("캐릭터 초상화 (머니바 원형 아이콘)")]
        [SerializeField] internal Sprite portraitRabbit;
        [SerializeField] internal Sprite portraitFox;
        [SerializeField] internal Sprite portraitCroc;

        [Header("캐릭터 아바타 (테이블 전신)")]
        [SerializeField] internal Sprite avatarRabbit;
        [SerializeField] internal Sprite avatarFox;
        [SerializeField] internal Sprite avatarCroc;

        // ── 패널 루트 ─────────────────────────────────────────────────
        [Header("UI Panels")]
        [SerializeField] internal GameObject panelCover;
        [SerializeField] internal GameObject panelWaiting;
        [SerializeField] internal GameObject panelBribe;
        [SerializeField] internal GameObject panelBet;
        [SerializeField] internal GameObject panelDealer;
        [SerializeField] internal GameObject panelDecision;
        [SerializeField] internal GameObject panelResult;
        [SerializeField] internal GameObject panelAccusation;
        [SerializeField] internal GameObject panelFinal;

        [Header("Role Reveal")]
        [SerializeField] internal RoleRevealView roleReveal;

        // ── Cover 패널 ────────────────────────────────────────────────
        [Header("Cover Panel")]
        [SerializeField] internal TextMeshProUGUI txtCoverMsg;
        [SerializeField] internal Button btnReveal;
        [SerializeField] internal WaitingOverlayView waitingOverlay;

        // ── Waiting 패널 ─────────────────────────────────────────────
        [Header("Waiting Panel")]
        [SerializeField] internal Button btnStart;

        // ── Bribe 패널 ────────────────────────────────────────────────
        [Header("Bribe Panel")]
        [SerializeField] internal TextMeshProUGUI txtBribeTitle;
        [SerializeField] internal TextMeshProUGUI txtBribeSummary;   // 뇌물·남은 잔액 요약
        [Tooltip("뇌물 프리셋 칩. 금액은 BribeSelector.Presets가 정하고, 여기 넣은 순서가 그 순서다.")]
        [SerializeField] internal Button[] btnBribePresets = new Button[4];
        [SerializeField] internal Button btnBribeMinus;            // 직접입력 스테퍼 −
        [SerializeField] internal Button btnBribePlus;             // 직접입력 스테퍼 +
        [SerializeField] internal TextMeshProUGUI txtBribeAmount;  // 스테퍼 현재값
        [SerializeField] internal Button btnBribeConfirm;

        // ── Bet 패널 ──────────────────────────────────────────────────
        [Header("Bet Panel")]
        [SerializeField] internal TextMeshProUGUI txtBetTitle;
        [SerializeField] internal TextMeshProUGUI txtBetSelected;
        [SerializeField] internal TMP_SpriteAsset betTokenGreenSpriteAsset;
        [SerializeField] internal TMP_SpriteAsset betTokenBlueSpriteAsset;
        [SerializeField] internal TMP_SpriteAsset betTokenRedSpriteAsset;
        [SerializeField] internal TMP_SpriteAsset betTokenBlackSpriteAsset;
        [SerializeField] internal TextMeshProUGUI txtBetSummary;
        [SerializeField] internal Slider sliderBet;
        [Tooltip("판돈 프리셋 칩. 금액은 RoundSettlement.BetPresets가 정하고, 여기 넣은 순서가 그 순서다.")]
        [SerializeField] internal Button[] btnBetPresets = new Button[4];
        [SerializeField] internal Button btnBetConfirm;

        // ── Dealer 패널 ───────────────────────────────────────────────
        [Header("Dealer Panel")]
        [SerializeField] internal TextMeshProUGUI txtDealerTimer;
        [SerializeField] internal TextMeshProUGUI txtBribeStatusTitle;
        [SerializeField] internal TextMeshProUGUI txtBribePlayerA;
        [FormerlySerializedAs("txtDealerBribes")]
        [SerializeField] internal TextMeshProUGUI txtBribeAmountA;
        [SerializeField] internal TextMeshProUGUI txtBribeDivider;
        [SerializeField] internal TextMeshProUGUI txtBribePlayerB;
        [SerializeField] internal TextMeshProUGUI txtBribeAmountB;
        [SerializeField] internal TextMeshProUGUI txtDealerScores;
        [SerializeField] internal TextMeshProUGUI txtDealerSelection;
        [SerializeField] internal HourglassLoadingGraphic dealerTimerHourglass;
        [SerializeField] internal Button btnDealerConfirm;
        [SerializeField] internal Button[] btnCandidates =
            new Button[BlackjackDeckService.DefaultCandidateCount];
        [SerializeField] internal TextMeshProUGUI[] txtCandidates =
            new TextMeshProUGUI[BlackjackDeckService.DefaultCandidateCount];

        // ── Decision 패널 ─────────────────────────────────────────────
        [Header("Decision Panel")]
        [SerializeField] internal TextMeshProUGUI txtDecisionHand;
        [SerializeField] internal TextMeshProUGUI txtDecisionScore;
        [SerializeField] internal GameObject sharedScoreCards;
        [SerializeField] internal TextMeshProUGUI txtDecisionScoreA;
        [SerializeField] internal TextMeshProUGUI txtDecisionScoreB;
        [SerializeField] internal Button btnHit;
        [SerializeField] internal Button btnStand;
        [SerializeField] internal Button btnDie;   // 초기 2장에서만 활성화
        [SerializeField] internal Button btnDoubleDown; // 초기 2장 + 양쪽이 2배 판돈을 감당할 때만

        // ── Result 패널 ───────────────────────────────────────────────
        [Header("Result Panel")]
        [SerializeField] internal TextMeshProUGUI txtResultA;
        [SerializeField] internal TextMeshProUGUI txtResultB;
        [SerializeField] internal TextMeshProUGUI txtResultOutcome;

        [Tooltip("결과 카드의 설명 한 줄. 비긴 판에는 '승자가 결정되었습니다'가 거짓말이 " +
                 "되므로 그때만 다른 글로 바꾼다. 씬에 적힌 글이 승부가 갈린 판의 기본값이다.")]
        [SerializeField] internal TextMeshProUGUI txtResultDescription;
        [SerializeField] internal Image resultMarkerA;
        [SerializeField] internal Image resultMarkerB;

        [Tooltip("승패 표식. 색 틴트가 아니라 그림 자체로 구분한다 — 왕관=승자, 비석=패자.")]
        [SerializeField] internal Sprite spriteCrown;
        [SerializeField] internal Sprite spriteTombstone;

        [Tooltip("표식의 높이(px). 폭은 원본 비율을 지켜 자동으로 계산된다. " +
                 "결과 패널과 최종 판정이 이 값 하나를 함께 쓰므로 두 화면의 표식이 항상 같은 크기다.")]
        [SerializeField] internal float markerHeight = 172f;

        [SerializeField] internal Button btnContinue;

        // ── Accusation 패널 ───────────────────────────────────────────
        // 카드 두 장 중 하나를 고르는 화면이다. 카드 전체가 버튼이라 어디를 눌러도
        // 선택되고, 설명은 카드 안에 들어 있어 따로 읽을 안내문이 없다.
        [Header("Accusation Panel")]
        [SerializeField] internal TextMeshProUGUI txtAccuseHeadline;
        [SerializeField] internal TextMeshProUGUI txtAccuseSubline;
        [SerializeField] internal ChoiceCardView cardAccuse;
        [SerializeField] internal ChoiceCardView cardAccept;
        [SerializeField] internal Button btnAccuse;
        [SerializeField] internal Button btnAccept;

        /// <summary>
        /// 선택지 카드 한 장. 이름 / 태그 / 설명 세 칸으로 이루어진다.
        /// 태그는 비워 두면 알약째로 사라지므로 카드마다 개수가 달라도 된다.
        /// </summary>
        [System.Serializable]
        internal class ChoiceCardView
        {
            public TextMeshProUGUI Name;
            public TextMeshProUGUI[] Tags = new TextMeshProUGUI[2];
            public TextMeshProUGUI Body;
        }

        // ── Final 패널 ────────────────────────────────────────────────
        [Header("Final Panel")]
        [SerializeField] internal TextMeshProUGUI txtFinalWinner;
        [SerializeField] internal TextMeshProUGUI txtFinalReason;
        [Tooltip("최종 판정 큐브. 세 사람의 초상화를 붙인 정육면체가 굴러 승자를 가린다.")]
        [SerializeField] internal FinalJudgmentCube finalCube;
        // NetworkGameDirector와의 호출 호환성만 유지한다. 직렬화 대상이 아니므로
        // Inspector에는 제거된 상세 정산 UI 슬롯이 다시 생기지 않는다.
        internal GameObject finalRoundDetails => null;
        [SerializeField] internal GameObject finalOutcomeGroup;
        [Tooltip("최종 판정의 승패 표식. A·B는 결과 패널과 같은 카드 더미 위 자리를 쓴다.")]
        [SerializeField] internal Image finalMarkerA;
        [SerializeField] internal Image finalMarkerB;

        [SerializeField] internal Button btnNextRound;

        // ── 매치 종료 화면 ────────────────────────────────────────────
        //
        // 최종 판정과 매치 종료는 같은 PanelFinal을 쓰지만 카드는 서로 다른 것을 쓴다.
        // 하나를 돌려 쓰면 글자를 매번 갈아 끼워야 하고(머리말·제목·버튼 라벨), 그 갈아
        // 끼우기를 한 군데라도 빠뜨리면 매치가 끝난 화면에 "다음 라운드"가 남는다.
        // 실제로 그런 상태였다.
        [Header("Match Over")]
        [Tooltip("매치 종료 순위표. 테이블 위쪽 띠에 가로로 선다. " +
                 "ZooJack/게임/최종 화면 만들기 메뉴로 만든다.")]
        [SerializeField] internal FinalRankingBoard finalRanking;

        [Tooltip("매치 종료 전용 카드. 최종 판정 카드(FinalVictoryCard)와 같은 규격이지만 " +
                 "다른 오브젝트다. 둘은 동시에 뜨지 않는다.")]
        [SerializeField] internal GameObject matchOverCard;
        [SerializeField] internal TextMeshProUGUI txtMatchOverTitle;
        [SerializeField] internal TextMeshProUGUI txtMatchOverReason;

        [Tooltip("다시 플레이. 네트워크에서는 전원이 눌러야 방 로비로 돌아가고, " +
                 "핫시트에서는 그 자리에서 새 매치를 연다.")]
        [SerializeField] internal Button btnRestartMatch;

        [Tooltip("나가기. 합의 없이 혼자서도 나간다 — 한 명이 자리를 비워 재대결이 " +
                 "성립하지 않을 때 남은 사람이 갇히지 않게 하는 유일한 출구다.")]
        [SerializeField] internal Button btnLeaveMatch;

        // ── 조작 피드백 비네트 ────────────────────────────────────────
        [Header("Vignette")]
        [SerializeField] internal VignetteEffect vignette;

        [Header("알림")]
        [Tooltip("화면 한가운데 잠깐 떴다 사라지는 알림. 버튼 없는 소식에 쓴다.")]
        [SerializeField] internal ToastView toast;

        /// <summary>
        /// 알림이 화면에 머무는 시간(초). 핫시트에서 딜러에게 화면을 넘기는 시점도
        /// 이 값을 따르므로, 여기만 바꾸면 알림과 화면 전환이 함께 움직인다.
        /// </summary>
        internal const float ToastSeconds = 1f;

        [Header("UI Animation")]
        [SerializeField] internal DealerCardArranger.AnimationSettings dealerCardAnimation
            = new DealerCardArranger.AnimationSettings();

        // ── 게임 로직 ─────────────────────────────────────────────────
        private RoundEngine roundEngine;
        private RoundContext roundContext;
        private RoleAssignmentService roleAssignment;
        private List<PlayerSeat> players;

        private DealerCardArranger arranger;
        private int dealerCandVersion;
        private JudgmentSuspense suspense;
        private int finalJudgmentVersion;

        private GamePhase phase = GamePhase.WaitingForPlayers;
        private int roundIndex;
        private Dictionary<string, int> bribes = new Dictionary<string, int>();
        private HashSet<string> bribesDone = new HashSet<string>();
        private HashSet<string> betsDone = new HashSet<string>();
        private MatchOutcome outcome;
        private string apparentWinnerId;
        private FinalJudgmentData lastJudgment;
        private float dealerElapsed;
        private const float DealerTimeLimit = RoundSettlement.DealerDecisionSeconds; // 15초
        private float playerDecisionElapsed;
        private const float PlayerDecisionTimeLimit = RoundSettlement.PlayerDecisionSeconds;
        private float accusationElapsed;
        private const float AccusationTimeLimit = RoundSettlement.AccusationSeconds;
        private float bribeElapsed;
        private const float BribeTimeLimit = RoundSettlement.BribeSelectionSeconds;
        private float betElapsed;
        private const float BetTimeLimit = RoundSettlement.BetSelectionSeconds;
        private BribeSelector bribeSelector; // 뇌물 프리셋 칩 + 스테퍼 UI 로직
        /// <summary>네트워크 디렉터와 똑같이 하는 화면 조작. 두 모드가 이 한 벌을 공유한다.</summary>
        private GamePanelPresenter panels;

        // ── 화면 조각 ────────────────────────────────────────
        // 셋 다 어느 패널에도 속하지 않고 항상 떠 있는 묶음이라, 패널 흐름과 떼어 따로 둔다.
        // 위젯 참조는 여전히 이쪽의 [SerializeField]다 — 씬 배선을 건드리지 않기 위해
        // 옮긴 것은 로직뿐이고, 참조는 생성자로 넘긴다.
        //
        // 처음 쓰일 때 만든다. 네트워크 모드에서는 Start가 돌지 않는데
        // (NetworkGameDirector가 ui.enabled를 false로 둔다) 네트워크 디렉터도 이 셋을 부른다.
        private MoneyBarView money;
        private ResultMarkerView markers;
        private BetChipsView betChips;

        /// <summary>머니바. 사람 고정 칸에 잔액과 정산 예고를 그린다.</summary>
        internal MoneyBarView Money =>
            money ?? (money = new MoneyBarView(moneyCards, PortraitFor));

        /// <summary>왕관·비석 표식. 결과 패널과 최종 판정 무대가 함께 쓴다.</summary>
        internal ResultMarkerView Markers =>
            markers ?? (markers = new ResultMarkerView(
                resultMarkerA, resultMarkerB, finalMarkerA, finalMarkerB,
                spriteCrown, spriteTombstone, markerHeight));

        /// <summary>테이블 위 칩 더미·금액·총합 배지와 판돈 패널의 고른 칩.</summary>
        internal BetChipsView BetChips =>
            betChips ?? (betChips = new BetChipsView(
                chipsA, chipsB, txtBetAmountA, txtBetAmountB,
                betTotalCard, txtBetTotalLabel, txtBetTotalAmount, txtBetSelected,
                betTokenGreenSpriteAsset, betTokenBlueSpriteAsset,
                betTokenRedSpriteAsset, betTokenBlackSpriteAsset,
                () => matchEnded));
        private BetPresetRow betPresets;     // 판돈 프리셋 칩 UI 로직
        private int selectedBet;
        private int bribePanelBalance;       // 뇌물 패널을 연 좌석의 잔액(뇌물 + 판돈의 예산)
        private bool tieRedealInProgress;
        private bool revealRolesOnStart = true;

        // 이번 결과 공개의 연출을 이미 시작했는지. 결과 패널을 떠날 때 풀린다.
        private bool resultPresented;

        /// <summary>
        /// 결과 패널을 떠났다고 알린다. 다음 라운드의 결과 연출이 다시 걸리게 된다.
        ///
        /// <b>두 디렉터가 각자의 ShowOnly에서 반드시 불러야 한다.</b> 핫시트와 네트워크는
        /// ShowOnly를 따로 갖고 있어서, 한쪽만 부르면 그쪽 모드에서 잠금이 영영 풀리지 않는다.
        /// 그러면 둘째 라운드부터 <see cref="PresentRoundResult"/>가 통째로 건너뛰어져
        /// 지난 라운드의 왕관·비석이 카드가 뒤집히기도 전에 그대로 떠 있는다.
        /// (실제로 네트워크 빌드에서 그랬다.)
        /// </summary>
        internal void ClearRoundResultPresentation() => resultPresented = false;

        // 매치 종료 사유(카운트다운 줄을 뺀 본문). 남은 시간을 매 프레임 다시 쓰려면
        // 원문을 들고 있어야 한다 — 화면 글자에서 되읽으면 카운트다운이 겹쳐 쌓인다.
        private string matchOverReason = string.Empty;

        // 매치 종료 소리를 이미 냈는지. matchEnded와 달리 화면 갱신으로 풀리지 않는다.
        private bool matchOverAnnounced;

        // 항복으로 끝났다면 누가 접었는지. None이면 항복이 아닌 사유로 끝난 것이다.
        private PlayerRole surrenderedRole = PlayerRole.None;

        // ── 시드머니(잔액) ────────────────────────────────────────────
        // 각 좌석은 시드머니로 시작한다. 판돈/뇌물/벌금 정산이 잔액에 반영된다.
        private const int SeedMoney = RoundSettlement.SeedMoney;
        private const int MaxRounds = RoundSettlement.MaxRounds;
        private readonly Dictionary<string, int> balances = new Dictionary<string, int>();
        private readonly Dictionary<string, int> bets = new Dictionary<string, int>(); // 좌석별 이번 라운드 판돈 베팅

        /// <summary>
        /// 이번 라운드 판돈 S. StartDeal에서 두 베팅의 최소 매칭으로 확정되고, 더블다운하면 2배가 된다.
        /// 정산이 이 값 하나를 보므로(다이 비용·배신·고발 뒤집기) 여기만 바꾸면 전부 따라온다.
        /// </summary>
        private int roundStake;
        private string bribingPid; // 현재 뇌물/베팅 슬라이더를 조작 중인 좌석 id
        private bool matchEnded;
        private PlayerRole decisionRole; // who is making hit/stand choice
        private PlayerRole currentViewer = PlayerRole.PlayerA; // 하단(자기)에 표시할 관점(핫시트)
        private System.Action onRevealed;
        private bool emotionWheelWired;

        /// <summary>
        /// 다음 감정표현을 낼 수 있는 시각.
        ///
        /// <b>핫시트에도 쿨타임이 필요하다.</b> 네트워크 쪽은 호스트가 초당 한 번으로
        /// 끊어 주지만 이 화면에는 그런 심판이 없어, 예전에는 E를 누르는 만큼 그대로
        /// 나갔다. 숫자는 네트워크와 같은 것을 쓴다 — 같은 게임인데 화면에 따라
        /// 다른 속도로 놀리게 되면 그건 규칙이 아니라 사고다.
        /// </summary>
        private float nextEmotionTime;
        private bool leavingGameScene;

        // ── Unity Lifecycle ───────────────────────────────────────────

        private void Start()
        {
            BuildPlayers();
            panels = new GamePanelPresenter(this);
            arranger = panels.BuildArranger();
            suspense = new JudgmentSuspense(
                txtFinalWinner, txtFinalReason, vignette,
                finalCube, null, finalOutcomeGroup);
            suspense.OnCandidateSettled = Markers.RefreshFinal;
            Markers.RefreshFinal(FinalWinner.None);
            WireButtons();
            WireEmotionWheel();
            ShowOnly(panelWaiting);
            RefreshStatus();

            ChatLog.AddSystem(ZooJackText.Get(
                "Chat.Guide.GameStarted",
                "게임이 시작되었습니다."));

            // 항복은 환경설정 화면 안에 있고 그 화면은 이쪽을 모른다. 여기서 손을 든다.
            // 네트워크 모드에서는 이 Start 자체가 돌지 않으므로(NetworkGameDirector가
            // 막는다) 자리를 두고 다투지 않는다.
            MatchSurrender.SetHost(this);
        }

        /// <summary>버튼 배열의 RectTransform 배열. 비어 있는 자리는 그대로 비운다.</summary>
        internal static RectTransform[] RectsOf(IReadOnlyList<Button> buttons)
        {
            if (buttons == null) return System.Array.Empty<RectTransform>();
            var rects = new RectTransform[buttons.Count];
            for (int i = 0; i < buttons.Count; i++) rects[i] = RectOf(buttons[i]);
            return rects;
        }

        private static RectTransform RectOf(Component c) => c == null ? null : c.GetComponent<RectTransform>();

        private void Update()
        {
            // 핫시트에선 지금 화면을 보는 사람이 자기 캐릭터를 조종한다.
            // currentViewer가 바뀌는 지점이 여러 군데라 매 프레임 맞춰 두는 편이 안전하다
            // (SetLocalRole은 값이 같으면 즉시 반환한다).
            if (avatarStage != null) avatarStage.SetLocalRole(currentViewer);
            RefreshHotseatBribeStatusCard();
            UpdateTurnTimer();
        }

        private void WireEmotionWheel()
        {
            if (emotionWheel == null)
                emotionWheel = EmotionWheelController.FindInScene();
            if (emotionWheel == null || emotionWheelWired) return;

            emotionWheel.EmotionSelected += OnEmotionSelected;
            emotionWheel.SetOpenGuard(CanOpenEmotionWheel);
            emotionWheelWired = true;
        }

        private bool CanOpenEmotionWheel()
        {
            if (!isActiveAndEnabled || leavingGameScene) return false;
            if (currentViewer == PlayerRole.None || avatarStage == null) return false;
            if (!avatarStage.IsVisible(currentViewer)) return false;
            if (Time.unscaledTime < nextEmotionTime) return false;
            return !IsTextInputFocused();
        }

        internal static bool IsTextInputFocused()
        {
            var selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            if (selected == null) return false;

            var tmpInput = selected.GetComponentInParent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused) return true;

            var legacyInput = selected.GetComponentInParent<InputField>();
            return legacyInput != null && legacyInput.isFocused;
        }

        private void OnEmotionSelected(EmotionId emotionId)
        {
            if (!CanOpenEmotionWheel()) return;

            nextEmotionTime = Time.unscaledTime + FusionGameState.EmotionCooldownSeconds;
            avatarStage.ShowEmotion(currentViewer, emotionId);
        }

        private void RefreshHotseatBribeStatusCard()
        {
            int bribeA = 0;
            int bribeB = 0;
            if (roleAssignment != null)
            {
                var playerA = roleAssignment.GetPlayerA();
                var playerB = roleAssignment.GetPlayerB();
                if (playerA != null) bribes.TryGetValue(playerA.PlayerId, out bribeA);
                if (playerB != null) bribes.TryGetValue(playerB.PlayerId, out bribeB);
            }

            SetBribeStatusCard(currentViewer, bribeA, bribeB);
        }

        internal void SetBribeStatusCard(PlayerRole viewer, int bribeA, int bribeB)
        {
            bool revealA = viewer == PlayerRole.Dealer || viewer == PlayerRole.PlayerA;
            bool revealB = viewer == PlayerRole.Dealer || viewer == PlayerRole.PlayerB;
            if (txtBribeStatusTitle != null)
                txtBribeStatusTitle.text = ZooJackText.Get(
                    "Game.Bribe.StatusTitle", "딜러에게 제출한 뇌물");
            if (txtBribePlayerA != null) txtBribePlayerA.text = ZooJackText.RoleName(PlayerRole.PlayerA);
            if (txtBribeAmountA != null)
                txtBribeAmountA.text = $"<b>{BribeStatusAmount(revealA, bribeA)}</b>";
            if (txtBribeDivider != null) txtBribeDivider.text = "│";
            if (txtBribePlayerB != null) txtBribePlayerB.text = ZooJackText.RoleName(PlayerRole.PlayerB);
            if (txtBribeAmountB != null)
                txtBribeAmountB.text = $"<b>{BribeStatusAmount(revealB, bribeB)}</b>";

            Transform cardRoot = txtBribeStatusTitle != null
                ? txtBribeStatusTitle.transform.parent
                : txtBribeAmountA != null ? txtBribeAmountA.transform.parent : null;
            if (cardRoot != null) cardRoot.gameObject.SetActive(true);
        }

        private static string BribeStatusAmount(bool revealed, int amount) =>
            revealed ? $"{amount:N0} <sprite index=0>" : "?";

        /// <summary>
        /// 시계가 도는 단계를 한곳에서 처리한다. 단계마다 주인·제한시간·시간 초과 시
        /// 할 일이 다르지만, (주인, 남은 초, 전체 길이)로 정규화하면 표시는 한 벌로 끝난다.
        ///
        /// 핫시트는 화면을 넘겨 가며 보므로 <b>패널이 실제로 떠 있을 때만</b> 시계를 돌린다.
        /// 넘기는 중(커버 화면)에 시간이 깎이면 아무도 보지 못한 채 자동 처리된다.
        /// </summary>
        private void UpdateTurnTimer()
        {
            PlayerRole owner = PlayerRole.None;
            float limit = 0f, elapsed = 0f;
            System.Action onTimeout = null;

            if (phase == GamePhase.BribeSelection && IsShowing(panelBribe))
            {
                // 핫시트는 한 사람씩 돌아가며 고르므로 시계 주인도 지금 화면을 보는 사람 하나다.
                // 시간이 다 되면 지금 고른 값을 그대로 확정한다 — 화면 앞에 사람이 있는 모드라
                // 기본값으로 되돌릴 이유가 없다.
                bribeElapsed += Time.deltaTime;
                owner = currentViewer;
                limit = BribeTimeLimit;
                elapsed = bribeElapsed;
                onTimeout = ConfirmBribe;
            }
            else if (phase == GamePhase.BetSelection && IsShowing(panelBet))
            {
                betElapsed += Time.deltaTime;
                owner = currentViewer;
                limit = BetTimeLimit;
                elapsed = betElapsed;
                onTimeout = ConfirmBet;
            }
            else if (phase == GamePhase.DealerCardDistribution && IsShowing(panelDealer))
            {
                dealerElapsed += Time.deltaTime;
                owner = PlayerRole.Dealer;
                limit = DealerTimeLimit;
                elapsed = dealerElapsed;
                onTimeout = () => arranger?.ConfirmCenter(OnCandidatePicked);
            }
            else if (phase == GamePhase.PlayerDecision && IsShowing(panelDecision))
            {
                playerDecisionElapsed += Time.deltaTime;
                owner = decisionRole;
                limit = PlayerDecisionTimeLimit;
                elapsed = playerDecisionElapsed;
                onTimeout = OnStand;
            }
            else if (phase == GamePhase.Accusation && IsShowing(panelAccusation))
            {
                accusationElapsed += Time.deltaTime;
                owner = outcome == MatchOutcome.PlayerAWin ? PlayerRole.PlayerB : PlayerRole.PlayerA;
                limit = AccusationTimeLimit;
                elapsed = accusationElapsed;
                onTimeout = () => OnAccusation(AccusationChoice.AcceptResult);   // 자동 승복
            }

            bool running = owner != PlayerRole.None && limit > 0f;
            float left = running ? Mathf.Max(0f, limit - elapsed) : 0f;

            RefreshAvatarPlates(phase, running ? owner : PlayerRole.None, left);
            RefreshReadyBadges();
            SetTurnTimerVisible(running);
            TurnCountdownSound.Update(running, left);
            if (!running) return;

            arranger?.StyleTimer(txtDealerTimer, left);
            if (dealerTimerHourglass != null)
                dealerTimerHourglass.SandProgress = 1f - Mathf.Clamp01(left / limit);

            // 표시를 끝낸 뒤에 넘긴다. 시간 초과 처리는 단계와 패널을 바꾸므로 마지막이어야 한다.
            if (elapsed >= limit) onTimeout?.Invoke();
        }

        internal static bool IsShowing(GameObject panel) => panel != null && panel.activeSelf;

        // ── 초기화 ───────────────────────────────────────────────────

        private void BuildPlayers()
        {
            players = new List<PlayerSeat>
            {
                new PlayerSeat { PlayerId = HotseatSeats.Dealer,
                                 DisplayName = HotseatSeats.DealerDisplayName,  IsConnected = true, IsReady = true },
                new PlayerSeat { PlayerId = HotseatSeats.PlayerA,
                                 DisplayName = HotseatSeats.PlayerADisplayName, IsConnected = true, IsReady = true },
                new PlayerSeat { PlayerId = HotseatSeats.PlayerB,
                                 DisplayName = HotseatSeats.PlayerBDisplayName, IsConnected = true, IsReady = true }
            };
            roleAssignment = new RoleAssignmentService();
            roleAssignment.AssignRoles(players);

            // 캐릭터를 좌석에 고정한다. 배역 직후의 역할로 한 번만 정하므로,
            // 이후 3라운드마다 역할이 돌아도 사람과 캐릭터가 함께 움직인다.
            foreach (var seat in players)
                seat.Character = CharacterIdentity.FromInitialRole(seat.Role);

            roundEngine = new RoundEngine();

            // 모든 좌석에 시드머니 지급
            balances.Clear();
            foreach (var seat in players) balances[seat.PlayerId] = SeedMoney;
        }

        private void WireButtons()
        {
            if (btnReveal != null) btnReveal.onClick.AddListener(OnReveal);
            if (btnStart  != null) btnStart.onClick.AddListener(OnStart);

            if (sliderBet != null)
            {
                DisableDirectionalNavigation(sliderBet);
                // 씬에 저장된 Slider 값이 초기 기본 판돈을 덮지 않게, 이벤트 연결 전에
                // 공용 기본값(50)을 10원 단위 슬라이더 값(5)으로 동기화한다.
                sliderBet.wholeNumbers = true;
                sliderBet.minValue = RoundSettlement.BetToSteps(RoundSettlement.MinBet);
                sliderBet.maxValue = RoundSettlement.BetToSteps(RoundSettlement.SeedMoney);
                sliderBet.SetValueWithoutNotify(RoundSettlement.BetToSteps(RoundSettlement.DefaultBet));
                sliderBet.onValueChanged.AddListener(OnBetSliderChanged);
            }
            betPresets = new BetPresetRow(btnBetPresets, sliderBet);
            bribeSelector = new BribeSelector(
                btnBribePresets, btnBribeMinus, btnBribePlus, txtBribeAmount, RefreshBribeSummary);
            if (btnBribeConfirm != null) btnBribeConfirm.onClick.AddListener(ConfirmBribe);
            if (btnBetConfirm != null) btnBetConfirm.onClick.AddListener(ConfirmBet);

            for (int i = 0; i < btnCandidates.Length; i++)
            {
                int index = i; // 클로저가 루프 변수를 붙잡지 않도록 자리마다 복사한다
                if (btnCandidates[i] != null)
                    btnCandidates[i].onClick.AddListener(() => OnDealerCardClicked(index));
            }
            if (btnDealerConfirm != null) btnDealerConfirm.onClick.AddListener(ConfirmDealerSelection);

            if (btnHit   != null) btnHit.onClick.AddListener(OnHit);
            if (btnStand != null) btnStand.onClick.AddListener(OnStand);
            if (btnDie   != null) btnDie.onClick.AddListener(OnDie);
            if (btnDoubleDown != null) btnDoubleDown.onClick.AddListener(OnDoubleDown);

            if (btnRestartMatch != null) btnRestartMatch.onClick.AddListener(OnRestartMatch);
            if (btnLeaveMatch   != null) btnLeaveMatch.onClick.AddListener(OnLeaveMatch);

            if (btnContinue  != null) btnContinue.onClick.AddListener(OnResultContinue);
            if (btnAccuse    != null) btnAccuse.onClick.AddListener(() => OnAccusation(AccusationChoice.Accuse));
            if (btnAccept    != null) btnAccept.onClick.AddListener(() => OnAccusation(AccusationChoice.AcceptResult));
            if (btnNextRound != null) btnNextRound.onClick.AddListener(OnNextRound);

            WireUiSounds();
        }

        /// <summary>
        /// 화면을 만질 때 나는 소리를 전부 여기서 붙인다.
        ///
        /// <b>목록이 여기 하나뿐인 이유.</b> 네트워크로 들어오면 <c>NetworkGameDirector.Awake</c>가
        /// 이 디렉터를 꺼 버려서(<c>ui.enabled = false</c>) 위쪽 <see cref="WireButtons"/>는 돌지 않는다.
        /// 두 디렉터가 각자 목록을 들고 있으면 한쪽에만 버튼을 추가했을 때 핫시트에서는 소리가 나고
        /// 빌드에서는 안 나는, 눈으로는 못 잡는 차이가 생긴다. 그래서 위젯 필드를 가진 이쪽에
        /// 목록을 두고 네트워크 디렉터는 이 함수를 부르기만 한다.
        ///
        /// 코인·칩 버튼과 카드는 각자 제 소리가 있으므로 여기 넣지 않는다. 화면을 걷어내는
        /// 버튼은 <see cref="PanelPeekToggle"/>이 스스로 낸다.
        /// </summary>
        internal void WireUiSounds()
        {
            GameAudio.AttachClick(
                btnBribeMinus, btnBribePlus, btnBribeConfirm,   // 뇌물: − · + · 제출하기
                btnBetConfirm,                                  // 판돈: 제출하기
                btnDealerConfirm,                               // 딜러: 결정
                btnHit, btnStand, btnDoubleDown, btnDie,        // 행동 선택 네 가지
                btnContinue,                                    // 라운드 결과: 계속
                btnAccuse, btnAccept,                           // 고발 · 승복
                btnNextRound);                                  // 최종 판정: 다음 라운드

            // 판돈 슬라이더는 손을 뗄 때 칩 소리를 낸다(끄는 내내가 아니라).
            SliderSettleSound.Attach(sliderBet);
        }

        /// <summary>캐릭터 이동 WASD/방향키가 판돈 슬라이더 값을 바꾸지 않게 한다.</summary>
        internal static void DisableDirectionalNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        // ── 게임 흐름 ─────────────────────────────────────────────────

        private void OnStart()
        {
            roundIndex++;
            bribes.Clear();
            bets.Clear();
            bribesDone.Clear();
            betsDone.Clear();
            roundStake = 0;
            tieRedealInProgress = false;
            roundContext = null;
            outcome = MatchOutcome.None;
            apparentWinnerId = string.Empty;
            lastJudgment = null;
            phase = revealRolesOnStart ? GamePhase.RoleAssignment : GamePhase.BribeSelection;
            ClearAllSlots();
            BetChips.Show(0, 0, roundIndex, phase); // 아직 아무도 베팅하지 않았으므로 칩을 치운다
            RefreshStatus();

            ShowCover(ZooJackText.Get("Game.Cover.PrivateTurn", "{0}의 차례\n다른 플레이어는 화면을 보지 마세요!",
                ZooJackText.RoleName(PlayerRole.PlayerA)), () =>
            {
                if (revealRolesOnStart)
                    ShowRoleReveal(PlayerRole.PlayerA, () =>
                    {
                        phase = GamePhase.BribeSelection;
                        RefreshStatus();
                        ShowBribePanel(PlayerRole.PlayerA);
                    });
                else
                    ShowBribePanel(PlayerRole.PlayerA);
            });
        }

        private void ShowBribePanel(PlayerRole role)
        {
            // 좌석 id는 역할에서 실시간으로 가져온다(역할 교대를 켜도 그대로 동작한다).
            currentViewer = role;
            Money.HighlightLocal(CharacterForRole(role));
            if (txtPhaseAction != null) txtPhaseAction.text = PhaseActionKo(phase);
            bribingPid = (role == PlayerRole.PlayerA
                ? roleAssignment.GetPlayerA() : roleAssignment.GetPlayerB()).PlayerId;
            bribePanelBalance = Bal(bribingPid);

            // 시계는 패널을 여는 순간부터 돈다. 커버 화면을 넘기는 동안 깎이면
            // 화면을 받은 사람이 이미 줄어든 시간을 보게 된다.
            bribeElapsed = 0f;

            // 뇌물은 첫 딜 전에 단독으로 확정한다. 최소 판돈을 낼 여지는 남긴다.
            bribeSelector?.Begin(RoundSettlement.MaxBribeFor(bribePanelBalance));
            RefreshBribeSummary();

            string name = ZooJackText.RoleName(role);
            if (txtBribeTitle != null)
                txtBribeTitle.text = ZooJackText.Get("Game.Bribe.Title", "{0} · 뇌물 결정", name);
            ShowOnly(panelBribe);
        }

        private void RefreshBribeSummary() =>
            panels.RefreshBribeSummary(bribeSelector != null ? bribeSelector.Value : 0, bribePanelBalance);

        private void ShowBetPanel(PlayerRole role)
        {
            currentViewer = role;
            Money.HighlightLocal(CharacterForRole(role));
            phase = GamePhase.BetSelection;
            if (txtPhaseAction != null) txtPhaseAction.text = PhaseActionKo(phase);
            bribingPid = (role == PlayerRole.PlayerA
                ? roleAssignment.GetPlayerA() : roleAssignment.GetPlayerB()).PlayerId;
            bribePanelBalance = Bal(bribingPid);
            selectedBet = RoundSettlement.DefaultBet;
            betElapsed = 0f;
            RefreshBetLimit();

            string name = ZooJackText.RoleName(role);
            if (txtBetTitle != null)
                txtBetTitle.text = ZooJackText.Get("Game.Bet.Title", "{0} · 판돈 결정", name);
            // 직전 딜러 관점에서 앞면이었던 상대 카드를 플레이어 관점으로 즉시 갱신한다.
            RefreshCardSlots();
            BlackjackHand handA = roundContext?.GetHand(PlayerRole.PlayerA);
            BlackjackHand handB = roundContext?.GetHand(PlayerRole.PlayerB);
            RefreshDecisionScoreCards(
                role,
                handA?.Cards, BlackjackScoreCalculator.Calculate(handA),
                handB?.Cards, BlackjackScoreCalculator.Calculate(handB));
            ShowOnly(panelBet);
        }

        private void RefreshBetLimit()
        {
            bribes.TryGetValue(bribingPid, out int bribe);
            selectedBet = panels.ApplyBetLimit(selectedBet, bribe, bribePanelBalance, betPresets);
        }

        private void RefreshBetSummary(int bribe) =>
            panels.RefreshBetSummary(selectedBet, bribe, bribePanelBalance);

        private void OnBetSliderChanged(float raw)
        {
            selectedBet = RoundSettlement.StepsToBet(Mathf.RoundToInt(raw));
            // 씬이 켜지며 Slider가 초기화되는 순간에는 아직 판돈을 낼 플레이어가 없다.
            // null 키로 Dictionary를 조회하면 실제 판돈 단계 전에도 예외가 난다.
            int bribe = 0;
            if (!string.IsNullOrEmpty(bribingPid))
                bribes.TryGetValue(bribingPid, out bribe);
            RefreshBetSummary(bribe);
        }

        private void ConfirmBribe()
        {
            int bribe = bribeSelector != null ? bribeSelector.Value : 0;
            bribes[bribingPid] = bribe;
            bribesDone.Add(bribingPid);

            if (bribesDone.Count < 2)
            {
                currentViewer = PlayerRole.PlayerB;
                if (txtPhaseAction != null) txtPhaseAction.text = PhaseActionKo(phase);
                ShowCover(ZooJackText.Get("Game.Cover.PrivateTurn", "{0}의 차례\n다른 플레이어는 화면을 보지 마세요!",
                    ZooJackText.RoleName(PlayerRole.PlayerB)), () =>
                {
                    if (revealRolesOnStart)
                    {
                        phase = GamePhase.RoleAssignment;
                        RefreshStatus();
                        ShowRoleReveal(PlayerRole.PlayerB, () =>
                        {
                            phase = GamePhase.BribeSelection;
                            RefreshStatus();
                            ShowBribePanel(PlayerRole.PlayerB);
                        });
                    }
                    else
                        ShowBribePanel(PlayerRole.PlayerB);
                });
            }
            else
            {
                StartInitialDeal();
            }
        }

        private void StartInitialDeal()
        {
            string aId = roleAssignment.GetPlayerA().PlayerId;
            string bId = roleAssignment.GetPlayerB().PlayerId;

            roundContext = roundEngine.CreateContext(aId, bId);
            roundEngine.GenerateNextInitialCandidateSet(roundContext);
            phase = GamePhase.DealerCardDistribution;
            currentViewer = PlayerRole.Dealer;
            RefreshStatus();
            ShowDealerPanel();
        }

        private void BeginBetSelection()
        {
            phase = GamePhase.BetSelection;
            currentViewer = PlayerRole.PlayerA;
            RefreshStatus();
            ShowCover(ZooJackText.Get("Game.Cover.BetTurn", "{0}의 차례\n카드를 확인하고 판돈을 선택하세요.",
                ZooJackText.RoleName(PlayerRole.PlayerA)), () =>
            {
                ShowBetPanel(PlayerRole.PlayerA);
            });
        }

        private void ConfirmBet()
        {
            bribes.TryGetValue(bribingPid, out int bribe);
            bets[bribingPid] = Mathf.Min(selectedBet, RoundSettlement.MaxBetFor(bribePanelBalance, bribe));
            betsDone.Add(bribingPid);

            if (betsDone.Count < 2)
            {
                currentViewer = PlayerRole.PlayerB;
                ShowCover(ZooJackText.Get("Game.Cover.BetTurn", "{0}의 차례\n카드를 확인하고 판돈을 선택하세요.",
                    ZooJackText.RoleName(PlayerRole.PlayerB)), () =>
                {
                    ShowBetPanel(PlayerRole.PlayerB);
                });
                return;
            }

            string aId = roleAssignment.GetPlayerA().PlayerId;
            string bId = roleAssignment.GetPlayerB().PlayerId;
            bets.TryGetValue(aId, out int betA);
            bets.TryGetValue(bId, out int betB);
            roundStake = RoundSettlement.ResolveStake(betA, betB);
            BetChips.Show(betA, betB, roundIndex, phase);
            ShowDecisionPanel(PlayerRole.PlayerA);
        }

        private void ShowDealerPanel()
        {
            dealerElapsed = 0f;
            ShowCover(ZooJackText.Get("Game.Cover.DealerCards", "딜러의 차례\n카드 후보를 선택하세요"), () =>
            {
                void OpenDealerPanel()
                {
                    phase = GamePhase.DealerCardDistribution;
                    RefreshStatus();
                    RefreshDealerPanel();
                    ShowOnly(panelDealer);
                }

                if (revealRolesOnStart)
                {
                    phase = GamePhase.RoleAssignment;
                    RefreshStatus();
                    ShowRoleReveal(PlayerRole.Dealer, () =>
                    {
                        revealRolesOnStart = false;
                        OpenDealerPanel();
                    });
                }
                else
                {
                    OpenDealerPanel();
                }
            });
        }

        private void RefreshDealerPanel()
        {
            var set = roundContext?.CurrentCandidateSet;
            if (set == null) return;

            currentViewer = PlayerRole.Dealer;
            RefreshCardSlots();

            string target = ZooJackText.RoleName(set.TargetPlayerRole);
            if (txtDealerSelection != null)
                txtDealerSelection.text = ZooJackText.Get(
                    "Game.Dealer.Selection", "{0}에게 줄 카드", target);

            // 딜러는 양쪽 패를 모두 알고 있으므로 배분 중에도 현재 점수를 함께 보여 준다.
            RefreshRevealedScoreCards(
                roundContext.PlayerAHand?.Cards, roundEngine.GetScore(roundContext, PlayerRole.PlayerA),
                roundContext.PlayerBHand?.Cards, roundEngine.GetScore(roundContext, PlayerRole.PlayerB));

            arranger.Setup(set.Candidates, ++dealerCandVersion,
                set.TargetPlayerRole == PlayerRole.PlayerA);
            if (btnDealerConfirm != null) btnDealerConfirm.interactable = true;
            RefreshDealerSelection(arranger.CurrentCenter);
        }

        private void OnDealerCardClicked(int index)
        {
            arranger?.HandleClick(index, RefreshDealerSelection);
        }

        private void ConfirmDealerSelection()
        {
            panels.LockDealerConfirmButton();
            arranger?.ConfirmCenter(OnCandidatePicked);
        }

        private void RefreshDealerSelection(int index)
        {
            var set = roundContext?.CurrentCandidateSet;
            if (set == null) return;

            panels.ShowDealerCandidateScore(
                set.Candidates, index, set.TargetPlayerRole,
                roundContext.GetHand(set.TargetPlayerRole).Cards);
        }

        private void OnCandidatePicked(int idx)
        {
            var set = roundContext?.CurrentCandidateSet;
            if (set == null) return;

            if (idx >= set.Candidates.Count) idx = 0;

            var savedStep = set.DealStep;
            var savedRole = set.TargetPlayerRole;

            var choice = new DealerCardChoice
            {
                TargetPlayerRole = savedRole,
                CandidateSetId   = set.CandidateSetId,
                ChosenCandidateIndex = idx
            };

            string dealerId = roleAssignment.GetDealer().PlayerId;
            roundEngine.ApplyDealerCardChoice(roundContext, choice, dealerId, dealerElapsed);
            ShowApprovedAction(PlayerRole.Dealer, PlayerActionType.SubmitDealerCardChoice);

            ClearDealerSlots();
            RefreshCardSlots();
            RefreshStatus();

            if (savedStep == BlackjackDealStep.InitialDeal)
            {
                if (!roundContext.IsInitialDealComplete)
                {
                    roundEngine.GenerateNextInitialCandidateSet(roundContext);
                    dealerElapsed = 0f;
                    RefreshDealerPanel(); // 딜러 패널 유지, 다음 카드
                }
                else
                {
                    if (tieRedealInProgress)
                    {
                        tieRedealInProgress = false;
                        ShowDecisionPanel(PlayerRole.PlayerA);
                    }
                    else
                    {
                        BeginBetSelection();
                    }
                }
            }
            else
            {
                // Hit 카드 지급 후 해당 플레이어에게 돌아감.
                // 더블다운한 손은 이 한 장으로 끝이므로 여기서 자동 스탠드된다.
                roundEngine.ApplyDoubleDownAutoStand(roundContext, savedRole);
                var score = roundEngine.GetScore(roundContext, savedRole);
                if (score.IsBust || roundContext.HasStood(savedRole)) AdvanceDecision();
                else                                                  ShowDecisionPanel(savedRole);
            }
        }

        private void ShowDecisionPanel(PlayerRole role)
        {
            phase = GamePhase.PlayerDecision;
            decisionRole = role;
            currentViewer = role;
            playerDecisionElapsed = 0f;
            arranger?.StyleTimer(txtDealerTimer, PlayerDecisionTimeLimit);
            if (dealerTimerHourglass != null) dealerTimerHourglass.SandProgress = 0f;
            RefreshStatus();

            string name = ZooJackText.RoleName(role);
            ShowCover(ZooJackText.Get("Game.Cover.DecisionTurn", "{0}의 차례\n행동을 선택해 주세요.", name), () =>
            {
                RefreshCardSlots();
                RefreshDecisionPanel(role);
                ShowOnly(panelDecision);
            });
        }

        private void RefreshDecisionPanel(PlayerRole role)
        {
            var score = roundEngine.GetScore(roundContext, role);
            var handA = roundContext.GetHand(PlayerRole.PlayerA);
            var handB = roundContext.GetHand(PlayerRole.PlayerB);
            var scoreA = roundEngine.GetScore(roundContext, PlayerRole.PlayerA);
            var scoreB = roundEngine.GetScore(roundContext, PlayerRole.PlayerB);

            string playerName = ZooJackText.RoleName(role);
            if (txtDecisionHand != null)
                txtDecisionHand.text = ZooJackText.Get(
                    "Game.Decision.Turn",
                    "<b>{0}의 차례</b>\n<size=17><color={1}>행동을 선택하세요.</color></size>",
                    playerName, ZooJackPalette.TaupeTag);
            if (txtDecisionScore != null)
                txtDecisionScore.gameObject.SetActive(false);

            RefreshDecisionScoreCards(role, handA?.Cards, scoreA, handB?.Cards, scoreB);

            int handCount = roundContext.GetHand(role)?.Cards.Count ?? 0;
            if (btnHit   != null)
                btnHit.interactable = !score.IsBust && handCount < RoundEngine.MaxCardsFor(roundContext, role);
            if (btnStand != null) btnStand.interactable  = true;
            if (btnDie   != null) btnDie.interactable = roundEngine.CanDie(roundContext, role);
            if (btnDoubleDown != null) btnDoubleDown.interactable = CanDoubleDownNow(role);
        }

        private void OnHit()
        {
            if ((roundContext.GetHand(decisionRole)?.Cards.Count ?? 0)
                >= RoundEngine.MaxCardsFor(roundContext, decisionRole))
                return;

            roundEngine.GenerateHitCandidateSet(roundContext, decisionRole);
            ShowApprovedAction(decisionRole, PlayerActionType.RequestHit);
            phase = GamePhase.DealerCardDistribution;
            currentViewer = PlayerRole.Dealer;
            dealerElapsed = 0f;
            RefreshStatus();

            ShowCover(ZooJackText.Get("Game.Cover.DealerHit", "딜러의 차례\n히트 카드 후보를 선택하세요"), () =>
            {
                RefreshDealerPanel();
                ShowOnly(panelDealer);
            });
        }

        private void OnStand()
        {
            roundEngine.ApplyStand(roundContext, decisionRole);
            ShowApprovedAction(decisionRole, PlayerActionType.RequestStand);
            AdvanceDecision();
        }

        private void AdvanceDecision()
        {
            if (roundEngine.IsBothDecisionDone(roundContext))
            {
                FinishRound();
                return;
            }

            var sA = roundEngine.GetScore(roundContext, PlayerRole.PlayerA);
            var sB = roundEngine.GetScore(roundContext, PlayerRole.PlayerB);

            bool aNeedsMore = !roundContext.PlayerAStood && !sA.IsBust;
            bool bNeedsMore = !roundContext.PlayerBStood && !sB.IsBust;

            if (aNeedsMore) ShowDecisionPanel(PlayerRole.PlayerA);
            else if (bNeedsMore) ShowDecisionPanel(PlayerRole.PlayerB);
            else FinishRound();
        }

        private void FinishRound()
        {
            phase = GamePhase.BlackjackResultCalculation;
            var result = roundEngine.CalculateResult(roundContext);
            outcome = result.Outcome;
            RefreshCardSlots();
            RefreshStatus();

            if (outcome == MatchOutcome.Tie)
            {
                ShowTieResult();
                return;
            }

            RevealOutcome(result.Outcome);
        }

        /// <summary>
        /// 선언하는 <b>본인</b>이 더블다운 후의 판돈을 감당할 수 있는지.
        ///
        /// 상대는 보지 않는다. 상대가 2S를 못 내면 마이너스로 떨어져 파산하고 매치가
        /// 끝나는데, 잔액이 얇은 상대를 밀어내는 것도 더블다운을 거는 이유다.
        /// </summary>
        private bool CanAffordDoubleDown(PlayerRole role)
        {
            if (roundContext == null || roundStake <= 0) return false;

            var seat = role == PlayerRole.PlayerA
                ? roleAssignment.GetPlayerA()
                : roleAssignment.GetPlayerB();
            if (seat == null) return false;

            bribes.TryGetValue(seat.PlayerId, out int bribe);
            int stakeAfter = RoundSettlement.StakeAfterDoubleDown(
                roundStake, roundContext.AnyoneDoubled);

            return RoundSettlement.CanAffordStake(Bal(seat.PlayerId), bribe, stakeAfter);
        }

        private bool CanDoubleDownNow(PlayerRole role) =>
            roundEngine.CanDoubleDown(roundContext, role) && CanAffordDoubleDown(role);

        /// <summary>
        /// 더블다운. 선언한 쪽만 자기 판돈을 올리므로 상대에게 물어볼 것이 없다 —
        /// 그 자리에서 성립시키고, 상대에게는 알림만 띄운다.
        /// </summary>
        private void OnDoubleDown()
        {
            if (!CanDoubleDownNow(decisionRole)) return;

            var declarer = decisionRole;

            // 판돈을 올리는 것은 이번 라운드에 한 번뿐이다. 둘 다 걸어도 2배에서 멈춘다 —
            // 각자 자기 몫을 2배로 올리면 매칭된 판돈은 어차피 2배가 되고,
            // 두 번 곱하면 4배가 되어 선언 시점의 잔액 검증(2S 기준)이 무의미해진다.
            bool firstThisRound = !roundContext.AnyoneDoubled;
            roundEngine.ApplyDoubleDown(roundContext, declarer);
            if (firstThisRound) roundStake = RoundSettlement.DoubledStake(roundStake);
            // 테이블 위 칩도 선언한 쪽만 2배로 다시 쌓는다.
            // 숫자가 그대로면 "2배가 됐다"는 말만 있고 눈에 보이는 증거가 없다.
            RefreshBetChips();

            // 카드 한 장을 받는다. 배분은 기존 히트 경로(딜러 후보 3장 → 선택)를 그대로 탄다.
            // 카드가 들어온 뒤의 자동 Stand는 OnCandidatePicked가 처리한다.
            roundEngine.GenerateHitCandidateSet(roundContext, declarer);
            phase = GamePhase.DealerCardDistribution;
            currentViewer = PlayerRole.Dealer;
            dealerElapsed = 0f;
            RefreshStatus();

            // 알림을 읽는 동안 결정 화면이 그대로 떠 있으므로 버튼을 잠근다.
            // 이미 더블다운이 성립했는데 히트를 한 번 더 누를 수 있으면 안 된다.
            SetDecisionButtonsInteractable(false);

            ShowApprovedAction(declarer, PlayerActionType.RequestDoubleDown);

            // 알림을 읽을 시간을 준 뒤 딜러에게 화면을 넘긴다. 커버를 먼저 띄우면
            // 알림이 커버 뒤에 깔려 아무도 못 읽는다.
            DOVirtual.DelayedCall(ToastSeconds, () =>
            {
                currentViewer = PlayerRole.Dealer;
                ShowDealerPanel();
            }, ignoreTimeScale: true).SetLink(gameObject);
        }

        private void SetDecisionButtonsInteractable(bool on)
        {
            if (btnHit != null) btnHit.interactable = on;
            if (btnStand != null) btnStand.interactable = on;
            if (btnDie != null) btnDie.interactable = on;
            if (btnDoubleDown != null) btnDoubleDown.interactable = on;
        }

        private void OnDie()
        {
            if (!roundEngine.CanDie(roundContext, decisionRole)) return;

            roundEngine.ApplyDie(roundContext, decisionRole);
            ShowApprovedAction(decisionRole, PlayerActionType.RequestDie);
            // 승부를 건너뛰고 곧바로 결과 공개로 합류한다. 다이한 쪽이 겉보기 패자가 되므로
            // 이후 고발 흐름은 기존 경로가 그대로 처리한다.
            RevealOutcome(RoundEngine.OutcomeAfterDie(decisionRole));
        }

        // 겉보기 승자를 확정하고 결과 패널을 띄운다.
        // 블랙잭 승부와 다이가 공유하는 출구다(다이는 승부만 건너뛰고 여기로 합류한다).
        private void RevealOutcome(MatchOutcome result)
        {
            outcome = result;
            bool aWon = outcome == MatchOutcome.PlayerAWin;
            apparentWinnerId = (aWon ? roleAssignment.GetPlayerA() : roleAssignment.GetPlayerB()).PlayerId;

            phase = GamePhase.ResultReveal;
            RefreshStatus();

            // 카드부터 그린다. 여기서 덮여 있던 상대 손패가 한 장씩 열리기 시작하고,
            // 아래의 결과 발표는 그것이 끝날 때까지 기다린다.
            RefreshCardSlots();
            RefreshRevealedScoreCards(
                roundContext.PlayerAHand?.Cards, roundEngine.GetScore(roundContext, PlayerRole.PlayerA),
                roundContext.PlayerBHand?.Cards, roundEngine.GetScore(roundContext, PlayerRole.PlayerB));
            ShowOnly(panelResult);

            // 지난 판이 비겼다면 글자가 '카드 다시 받기'로 바뀌어 있다. 되돌린다.
            SetContinueLabel(ZooJackText.Get("Common.Continue", "계속"));
            PresentRoundResult(
                ZooJackText.RoundWinner(aWon ? PlayerRole.PlayerA : PlayerRole.PlayerB),
                aWon,
                foldedRole: roundContext.FoldedRole);
        }

        /// <summary>
        /// 비긴 판의 결과 화면. 승패가 갈린 판과 같은 카드를 쓴다 — 양쪽 손패가 한 장씩
        /// 열리고 다 열린 뒤에 문구가 뜬다. 카드를 지우는 것은 '카드 다시 받기'를
        /// 누른 뒤다. 순서를 뒤집으면 무엇 때문에 비겼는지 볼 수 없다.
        /// </summary>
        private void ShowTieResult()
        {
            phase = GamePhase.TieRedeal;
            RefreshStatus();

            // 카드부터 그린다. TieRedeal은 공개 구간이라 여기서 덮여 있던 손패가
            // 한 장씩 열리기 시작하고, 아래의 결과 발표는 그것이 끝날 때까지 기다린다.
            RefreshCardSlots();
            RefreshRevealedScoreCards(
                roundContext.PlayerAHand?.Cards, roundEngine.GetScore(roundContext, PlayerRole.PlayerA),
                roundContext.PlayerBHand?.Cards, roundEngine.GetScore(roundContext, PlayerRole.PlayerB));
            ShowOnly(panelResult);

            // 핫시트에는 기다릴 사람이 없다. 글자만 바꿔 어디로 가는지 알린다.
            SetContinueLabel(ZooJackText.Get("Game.Result.Redeal", "카드 다시 받기"));
            PresentRoundResult(ZooJackText.Get(
                "Game.Result.Tie", "무승부! 서로의 점수가 같습니다"),
                playerAWon: false, tie: true);
        }

        private void SetContinueLabel(string text)
        {
            if (btnContinue == null) return;
            var label = btnContinue.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text;
        }

        /// <summary>비긴 판을 다 봤다. 손패를 지우고 같은 라운드를 다시 나눈다.</summary>
        private void StartTieRedeal()
        {
            tieRedealInProgress = true;
            roundEngine.ResetForTieRedeal(roundContext);
            ClearPlayerSlots();
            roundEngine.GenerateNextInitialCandidateSet(roundContext);

            phase = GamePhase.DealerCardDistribution;
            currentViewer = PlayerRole.Dealer;
            dealerElapsed = 0f;
            RefreshStatus();
            RefreshDealerPanel();
            ShowOnly(panelDealer);
        }

        private void OnResultContinue()
        {
            // 같은 버튼이 두 곳으로 간다 — 비긴 판에서는 카드 재배분, 아니면 고발 단계.
            if (phase == GamePhase.TieRedeal) { StartTieRedeal(); return; }

            bool aWon = outcome == MatchOutcome.PlayerAWin;
            string loserName = ZooJackText.RoleName(aWon ? PlayerRole.PlayerB : PlayerRole.PlayerA);

            phase = GamePhase.Accusation;
            RefreshStatus();
            RefreshCardSlots();
            RefreshRevealedScoreCards(
                roundContext.PlayerAHand?.Cards,
                roundEngine.GetScore(roundContext, PlayerRole.PlayerA),
                roundContext.PlayerBHand?.Cards,
                roundEngine.GetScore(roundContext, PlayerRole.PlayerB));
            ShowCover(ZooJackText.Get(
                "Game.Cover.AccusationTurn", "{0}의 차례\n딜러 고발 여부를 결정하세요", loserName), () =>
            {
                RefreshAccusationCards();
                accusationElapsed = 0f;
                ShowOnly(panelAccusation);
            });
        }

        private void OnAccusation(AccusationChoice choice)
        {
            // 버튼과 시간 초과가 모두 여기로 들어온다. 두 번 정산되면 잔액이 어긋나므로 막는다.
            if (phase != GamePhase.Accusation) return;

            bool aWon = outcome == MatchOutcome.PlayerAWin;
            string aId = roleAssignment.GetPlayerA().PlayerId;
            string bId = roleAssignment.GetPlayerB().PlayerId;
            string dealerId = roleAssignment.GetDealer().PlayerId;
            string loserId  = aWon ? bId : aId;
            string winnerId = aWon ? aId : bId;

            lastJudgment = roundEngine.JudgeAccusation(roundContext, choice, loserId, winnerId);
            phase = GamePhase.FinalJudgment;

            // ── 재화 정산 ────────────────────────────────────────────
            bool manipulated = ManipulationRecordTracker.WasAnyManipulationRecorded(roundContext);
            bribes.TryGetValue(aId, out int bribeA);
            bribes.TryGetValue(bId, out int bribeB);
            // 더블다운이 있었다면 roundStake는 이미 2배다. ResolveStake로 다시 계산하면 안 된다.
            int stake = roundStake;

            var settle = RoundSettlement.Compute(
                choice, lastJudgment.AccusationResult, outcome, manipulated, bribeA, bribeB, stake,
                foldedRole: roundContext.FoldedRole);

            // 배신(딜러 단독 승리)까지 반영된 최종 승자로 표기한다.
            string winnerText = ZooJackText.FinalWinner(settle.ResolvedWinner);

            RefreshFinalPresentation(
                settle.ResolvedWinner, manipulated,
                settle.PlayerADelta, settle.PlayerBDelta, settle.DealerDelta,
                Bal(aId) + settle.PlayerADelta,
                Bal(bId) + settle.PlayerBDelta,
                Bal(dealerId) + settle.DealerDelta);

            // 뇌물은 라운드가 끝난 지금부터 공개다. 넘어간 몫은 정산과 같은 함수로 뽑는다.
            RoundSettlement.BribesKept(
                manipulated, settle.ResolvedWinner,
                choice == AccusationChoice.Accuse
                    && lastJudgment.AccusationResult == AccusationResult.Success,
                outcome, bribeA, bribeB, out int keptFromA, out int keptFromB);
            int keptTotal = keptFromA + keptFromB;

            // 잔액 반영은 두구두구 연출이 끝나 결과가 공개되는 순간으로 미룬다(머니 바 스포일러 방지).
            void ApplySettlement()
            {
                // 기록도 같은 순간에 남긴다. 머니 바만 늦추고 여기를 앞세우면 주사위가
                // 구르는 동안 기록 화면을 열어 답을 먼저 볼 수 있다 — 실제로 그랬다.
                MatchHistory.RecordRound(
                    roundIndex,
                    new MatchHistory.Change(CharacterOf(aId), PlayerRole.PlayerA,
                        settle.PlayerADelta, bribeA, keptFromA),
                    new MatchHistory.Change(CharacterOf(dealerId), PlayerRole.Dealer,
                        settle.DealerDelta, keptTotal, keptTotal),
                    new MatchHistory.Change(CharacterOf(bId), PlayerRole.PlayerB,
                        settle.PlayerBDelta, bribeB, keptFromB),
                    manipulated,
                    choice,
                    lastJudgment.AccusationResult,
                    settle.ResolvedWinner);

                balances[aId]      = Bal(aId)      + settle.PlayerADelta;
                balances[bId]      = Bal(bId)      + settle.PlayerBDelta;
                balances[dealerId] = Bal(dealerId) + settle.DealerDelta;

                // 모델은 여기서 이미 정산 후 값이지만, 머니바는 아직 정산 전 숫자에
                // ±N 배지만 붙인다. 숫자가 실제로 움직이는 건 '다음 라운드'를 누를 때다.
                Money.HoldDelta(CharacterOf(aId),      balances[aId],      settle.PlayerADelta);
                Money.HoldDelta(CharacterOf(bId),      balances[bId],      settle.PlayerBDelta);
                Money.HoldDelta(CharacterOf(dealerId), balances[dealerId], settle.DealerDelta);
                Money.RevealDeltas();   // 이 콜백 자체가 승자 공개 시점이라 곧바로 드러낸다
                // 파산자가 나오면 다음 라운드로 넘어가지 않고 최종 결과로 간다.
                // 라벨은 판정마다 여기서 다시 정한다 — 한 번 바꾼 뒤 되돌리는 자리를
                // 따로 두면, 새 매치를 시작해도 "최종 결과 보기"가 남는 길이 생긴다.
                SetNextRoundLabel(AnyoneBankrupt()
                    ? ZooJackText.Get("Game.Result.ViewFinal", "최종 결과 보기")
                    : ZooJackText.Get("Game.Result.NextRound", "다음 라운드"));
                if (btnNextRound != null) btnNextRound.gameObject.SetActive(true);
            }

            ShowOnly(panelFinal);
            SetFinalStage(true);
            if (btnNextRound != null) btnNextRound.gameObject.SetActive(false);
            // 핫시트에서는 매치 종료 화면이 '최종 결과 보기'를 눌러야 열린다. 이 판정과
            // 같은 순간에 겹치지 않으므로 최종 승리 팡파레를 그대로 낸다.
            suspense.Show(++finalJudgmentVersion, settle.ResolvedWinner, winnerText, settle.ReasonText,
                settle.ResolvedWinner == FinalWinner.Dealer, matchOver: false, onRevealed: ApplySettlement);
            RefreshStatus();
        }

        private void OnNextRound()
        {
            // 예고해 둔 ±N을 숫자에 흘려 넣는다. 라운드가 넘어가는 이 순간이
            // 모두의 잔액 변화를 한 번에 보여 줄 수 있는 유일한 지점이다.
            Money.CommitDeltas();

            // 파산자가 있거나 최대 라운드에 도달하면 매치 종료. 그 뒤로는 이 버튼이
            // 화면에 없다 — 새 매치는 매치 종료 카드의 전용 버튼(OnRestartMatch)이 연다.
            if (AnyoneBankrupt() || roundIndex >= MaxRounds) { ShowMatchOver(); return; }

            // 3라운드마다 역할 교대. roundIndex는 방금 끝낸 라운드 번호다(StartRound에서 증가).
            revealRolesOnStart = RoundSettlement.ShouldRotateRolesAfterRound(roundIndex);
            if (revealRolesOnStart) roleAssignment.RotateDealer();
            phase = GamePhase.WaitingForPlayers;
            RefreshStatus();
            ShowOnly(panelWaiting);
        }

        // ── 매치 종료 / 재시작 ───────────────────────────────────────

        /// <summary>좌석 중 한 명이라도 파산했으면 true.</summary>
        private bool AnyoneBankrupt()
        {
            foreach (var seat in players)
                if (RoundSettlement.IsBankrupt(Bal(seat.PlayerId))) return true;
            return false;
        }

        private void ShowMatchOver()
        {
            // 최종 순위는 정산 후 잔액으로 내므로 머니바도 같은 값이어야 한다.
            Money.CommitDeltas();
            matchEnded = true;
            SetFinalRoundDetailsVisible(false);

            // 잔액은 역할이 아니라 좌석에 귀속되므로, 순위는 좌석 기준 잔액으로 낸다.
            var seats = players.ToArray();
            var bals  = new int[seats.Length];
            int withdrawn = -1;
            CharacterId surrendered = CharacterId.None;
            for (int i = 0; i < seats.Length; i++)
            {
                bals[i] = Bal(seats[i].PlayerId);
                if (surrenderedRole == PlayerRole.None || seats[i].Role != surrenderedRole) continue;
                withdrawn = i;
                surrendered = seats[i].Character;
            }
            // 항복한 사람과 파산자가 아래로 내려간다.
            var order = RoundSettlement.RankOrder(bals, withdrawn);

            var chars  = new CharacterId[order.Length];
            var roles  = new PlayerRole[order.Length];
            var ranked = new int[order.Length];
            for (int rank = 0; rank < order.Length; rank++)
            {
                PlayerSeat seat = seats[order[rank]];
                chars[rank]  = seat.Character;
                roles[rank]  = seat.Role;
                ranked[rank] = bals[order[rank]];
            }

            CharacterId bankrupt = CharacterId.None;
            for (int i = 0; i < seats.Length; i++)
                if (RoundSettlement.IsBankrupt(bals[i])) { bankrupt = seats[i].Character; break; }

            ShowOnly(panelFinal);
            SetFinalStage(false);
            SetFinalRoundDetailsVisible(false);

            // 지난 라운드 판정의 왕관·비석은 낡은 정보다. 매치 종료는 방금 판의 승패가
            // 아니라 전체 성적을 보는 화면이라, 표식이 남아 있으면 어느 쪽을 말하는지 흐려진다.
            Markers.RefreshFinal(FinalWinner.None);
            if (sharedScoreCards != null) sharedScoreCards.SetActive(false);

            ShowMatchOverCard(bankrupt, surrenderedRole);
            ShowFinalRanking(chars, roles, ranked, CharacterForRole(currentViewer), surrendered);

            // 기록 화면의 랭킹에 이 매치를 한 줄 쌓는다(네트워크 쪽 ShowMatchOver와 같은 자리).
            var standings = new MatchHistory.Standing[order.Length];
            for (int rank = 0; rank < order.Length; rank++)
                standings[rank] = new MatchHistory.Standing(
                    chars[rank], roles[rank], ranked[rank],
                    RoundSettlement.IsBankrupt(ranked[rank]),
                    surrendered != CharacterId.None && chars[rank] == surrendered);
            MatchHistory.RecordRanking(standings);
        }

        // ── 항복 ─────────────────────────────────────────────────────

        /// <summary>
        /// 핫시트에서는 지금 화면을 보고 있는 사람이 곧 항복하는 사람이다.
        /// 아직 판이 서지 않았거나 이미 끝났으면 버튼조차 뜨지 않는다.
        /// </summary>
        bool MatchSurrender.IHost.CanSurrender =>
            isActiveAndEnabled
            && !matchEnded
            && phase != GamePhase.WaitingForPlayers
            && currentViewer != PlayerRole.None;

        /// <summary>
        /// 알림이 뜬 뒤 순위표가 올라오기까지 두는 사이(초).
        ///
        /// 둘이 같은 순간에 나오면 "누가 항복했다"는 문장을 읽기도 전에 순위표가 눈을
        /// 끌어간다. 핫시트와 네트워크가 같은 박자를 쓰도록 여기 한 곳에 둔다.
        /// </summary>
        internal const float SurrenderHoldSeconds = 1.5f;

        /// <summary>
        /// 항복. 알림을 먼저 띄우고 <see cref="SurrenderHoldSeconds"/>만큼 두었다가
        /// 순위표를 올린다. 알림은 어떤 패널 위에도 뜨므로(<see cref="ToastView"/>)
        /// 화면이 바뀌어도 남은 시간 동안 그대로 읽힌다.
        /// </summary>
        void MatchSurrender.IHost.Surrender()
        {
            if (matchEnded || surrenderedRole != PlayerRole.None) return;

            surrenderedRole = currentViewer;
            ShowActionToast(
                PlayerActionType.RequestSurrender,
                ActionAnnouncementMapper.SurrenderNotice(surrenderedRole),
                ActionAnnouncementMapper.SurrenderFollowUp);

            // 연출이 도중에 timeScale을 건드려도 같은 길이여야 하므로 실시간으로 센다.
            surrenderHold?.Kill();
            surrenderHold = DOVirtual.DelayedCall(SurrenderHoldSeconds, ShowMatchOver, true)
                .SetLink(gameObject);
        }

        /// <summary>항복 알림을 읽는 사이. 매치가 다시 시작되면 거둔다.</summary>
        private Tween surrenderHold;

        // 매치 종료 카드의 '새 매치 시작'. 한 기기에서 한 사람이 누르므로 준비 게이트가 없다.
        // 네트워크에서는 이 리스너가 붙지 않는다(NetworkGameDirector가 핫시트 Start를 막는다).
        private void OnRestartMatch()
        {
            Money.CommitDeltas();
            ResetMatch();
        }

        // 핫시트에는 세션이 없으므로 씬만 되돌린다. 네트워크 쪽 나가기는
        // NetworkGameDirector가 러너를 내린 뒤 같은 씬을 연다.
        private void OnLeaveMatch()
        {
            leavingGameScene = true;
            emotionWheel?.CancelSelection();
            SceneManager.LoadScene(ZooJackScenes.Lobby);
        }

        // 매치 종료 상태바 문구. 단계 열거형(GamePhase)에는 매치 종료가 없다 — 끝나도
        // 단계는 FinalJudgment에 머무르므로 화면 쪽에서 덮어 준다. 핫시트와 네트워크가
        // 같은 글자를 써야 해서 여기 한 곳에 둔다.
        internal static string MatchOverPhase =>
            ZooJackText.Get("Game.Phase.MatchOver", "매치 종료");
        internal static string MatchOverPhaseAction =>
            ZooJackText.Get("Game.Phase.MatchOverAction", "최종 순위를 확인하세요.");

        /// <summary>
        /// 매치 종료 카드를 올리고 최종 판정 카드를 내린다. 둘은 다른 오브젝트이므로
        /// 서로의 글자를 건드리지 않는다 — 판정 카드는 판정 문구를 그대로 들고 있고,
        /// 다음 판정이 오면 <see cref="JudgmentSuspense"/>가 그대로 다시 띄운다.
        ///
        /// 사유는 <b>한 줄</b>이다. 순위는 위쪽 순위표가 맡는다.
        /// 우승자를 여기 또 적지는 않는다 — 순위표 1등 칸이 이미 금색으로 말하고 있다.
        /// </summary>
        internal void ShowMatchOverCard(CharacterId bankrupt) =>
            ShowMatchOverCard(bankrupt, PlayerRole.None);

        /// <param name="surrendered">
        /// 항복으로 끝났다면 접은 사람의 배역. 사유 중에서 <b>가장 먼저</b> 읽힌다 —
        /// 항복한 판에도 파산자는 있을 수 있지만, 판을 끝낸 것은 항복이다.
        /// </param>
        internal void ShowMatchOverCard(CharacterId bankrupt, PlayerRole surrendered)
        {
            // 소리는 이 매치에 한 번만. matchEnded로는 가릴 수 없다 — 네트워크에서는
            // 화면을 다시 그릴 때마다 ShowOnly가 HideMatchOverScreen을 거치며 그 값을
            // 껐다 켜므로, 갱신마다 "처음"으로 보인다. 그래서 새 매치에서만 풀리는
            // 잠금을 따로 둔다(ResetMatch, 그리고 네트워크는 씬을 다시 연다).
            if (!matchOverAnnounced)
            {
                matchOverAnnounced = true;
                GameAudio.PlayMatchOver();
                ChatLog.AddSystem(ZooJackText.Get(
                    "Chat.Guide.GameEnded",
                    "게임이 종료되었습니다."));
            }

            matchEnded = true;

            // 아래 둘은 화면 갱신을 기다리지 않고 여기서 바로 반영한다 — 이 카드는
            // 두구두구 연출 콜백에서 올라오므로 다음 갱신이 언제 올지 알 수 없다.
            // 상태바·배지는 이후 갱신에서도 matchEnded를 보고 이 상태를 유지한다.

            // 총 베팅 배지(y 278~324)가 순위표(y 290~390)를 뚫고 올라온다.
            if (betTotalCard != null) betTotalCard.SetActive(false);

            // 단계 이름은 아직 "최종 판정"이거나 곧 "라운드 종료"가 된다. 둘 다 틀렸다 —
            // 다음 라운드는 없다.
            if (txtPhase != null) txtPhase.text = MatchOverPhase;
            if (txtPhaseAction != null) txtPhaseAction.text = MatchOverPhaseAction;

            if (finalOutcomeGroup != null) finalOutcomeGroup.SetActive(false);
            ResultCardPop.Play(matchOverCard);

            if (txtMatchOverTitle != null)
                txtMatchOverTitle.text = ZooJackText.Get("Game.MatchOver.Title", "매치 종료");

            matchOverReason =
                surrendered != PlayerRole.None
                    ? ActionAnnouncementMapper.SurrenderNotice(surrendered)
                    : bankrupt != CharacterId.None
                        ? ZooJackText.Get("Game.MatchOver.BankruptReason", "{0} 파산으로 끝났습니다.",
                            CharacterIdentity.NameKo(bankrupt))
                        : ZooJackText.Get("Game.MatchOver.AllRoundsReason", "{0}라운드를 모두 마쳤습니다.",
                            RoundSettlement.MaxRounds);

            if (txtMatchOverReason != null) txtMatchOverReason.text = matchOverReason;
        }

        /// <summary>
        /// 종료 사유 아래에 남은 시간을 붙인다. 숫자만 두지 않고 <b>무슨 일이 일어나는지</b>
        /// 함께 적는다 — 준비를 안 눌렀다는 이유로 갑자기 방에서 튕기면 버그로 읽힌다.
        ///
        /// 핫시트에서는 부르지 않는다(마감이 없다). 그래서 사유는 한 줄로 남는다.
        /// </summary>
        internal void SetMatchOverCountdown(float secondsLeft)
        {
            if (txtMatchOverReason == null) return;

            int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            string color = seconds <= MatchOverUrgentSeconds
                ? ZooJackPalette.UrgentTag : ZooJackPalette.StoneTag;

            txtMatchOverReason.text = matchOverReason + "\n" + ZooJackText.Get(
                "Game.MatchOver.Countdown",
                "<size=14><color={0}>{1}초 후 준비하지 않은 사람은 방에서 나갑니다</color></size>",
                color, seconds);
        }

        /// <summary>남은 시간이 이 값 이하로 떨어지면 붉게 바뀐다.</summary>
        private const int MatchOverUrgentSeconds = 10;

        /// <summary>
        /// 최종 순위표를 올린다. 세 배열은 <b>1등부터 같은 순서</b>로 정렬돼 있어야 한다 —
        /// 정렬 규칙(파산자 자동 최하위)은 <see cref="RoundSettlement.RankOrder"/>에 있고
        /// 핫시트와 네트워크가 그 결과를 그대로 넘긴다.
        ///
        /// 사람을 이름이 아니라 초상화와 색으로 알아보게 한다. 닉네임이 아직 네트워크로
        /// 넘어오지 않아 이름은 "플레이어 1"에 머무는데, 정작 매치 내내 서로를 부르던
        /// 호칭은 토끼·여우·악어다.
        /// </summary>
        /// <param name="surrendered">
        /// 판을 접고 나간 사람. 배역 대신 '항복'을 적는다 — 매치가 왜 여기서 끝났는지가
        /// 그 사람에 대해 알려 주는 것 중 가장 큰 것이다.
        /// </param>
        internal void ShowFinalRanking(
            IReadOnlyList<CharacterId> characters,
            IReadOnlyList<PlayerRole> roles,
            IReadOnlyList<int> balances,
            CharacterId localCharacter,
            CharacterId surrendered = CharacterId.None)
        {
            if (finalRanking == null || characters == null) return;

            var entries = new List<FinalRankingBoard.Entry>(characters.Count);
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterId who = characters[i];
                int balance = i < balances.Count ? balances[i] : 0;
                bool bankrupt = RoundSettlement.IsBankrupt(balance);
                string name = CharacterIdentity.NameKo(who);

                entries.Add(new FinalRankingBoard.Entry
                {
                    Portrait = PortraitFor(who),
                    Accent   = ZooJackPalette.CharacterAccent(who),
                    Name     = string.IsNullOrEmpty(name)
                        ? ZooJackText.Get("Lobby.Card.Free", "빈자리") : name,
                    // 항복·파산이면 역할 대신 그것을 적는다. 역할은 3라운드마다 도니까
                    // 매치가 끝난 시점의 배역은 그 사람에 대해 알려 주는 것이 가장 적다.
                    Subtitle =
                        who != CharacterId.None && who == surrendered
                            ? ZooJackText.Get("History.Result.Surrender", "항복")
                        : bankrupt ? ZooJackText.Get("History.Result.Bankrupt", "파산")
                        : MoneyBarView.RoleLabel(i < roles.Count ? roles[i] : PlayerRole.None),
                    IsLocal  = who != CharacterId.None && who == localCharacter,
                    Balance  = balance,
                    Bankrupt = bankrupt
                });
            }

            finalRanking.Show(entries);
        }

        /// <summary>
        /// 매치 종료 화면(순위표 + 전용 카드)을 통째로 내린다.
        /// <see cref="ShowOnly"/>가 조건 없이 부른다 — 최종 판정도 같은 패널을 쓰므로
        /// 패널 이름으로는 가려낼 수 없고, 매치 종료 쪽에서 그 호출 뒤에 다시 올린다.
        /// </summary>
        internal void HideMatchOverScreen()
        {
            matchEnded = false;
            if (finalRanking != null) finalRanking.Hide();
            ResultCardPop.Hide(matchOverCard);
        }

        /// <summary>매치 종료 화면이 지금 떠 있는지. 상태바 문구와 총 베팅 배지가 이 값을 본다.</summary>
        internal bool IsMatchOverScreenUp => matchEnded;

        private void ResetMatch()
        {
            surrenderHold?.Kill();
            surrenderHold = null;
            matchEnded = false;
            matchOverAnnounced = false;
            surrenderedRole = PlayerRole.None;
            roundIndex = 0;

            // 지난 매치의 라운드 기록은 여기서 비운다(네트워크 쪽은 게임 씬이 다시 열리는
            // 지점에서 같은 일을 한다). 랭킹은 그대로 쌓인다.
            MatchHistory.BeginMatch();
            revealRolesOnStart = true;
            foreach (var seat in players) balances[seat.PlayerId] = SeedMoney;
            phase = GamePhase.WaitingForPlayers;
            RefreshStatus();
            ShowOnly(panelWaiting);
        }

        private void SetNextRoundLabel(string text)
        {
            if (btnNextRound == null) return;
            var label = btnNextRound.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text;
        }

        /// <summary>
        /// 다음 라운드 버튼을 준비 버튼으로 쓴다. 이미 누른 사람에게는 몇 명이 남았는지 보여 준다.
        ///
        /// 눌러도 화면이 그대로면 "안 눌렸나?" 싶어 계속 누르게 된다. 버튼을 잠그고
        /// 인원수를 세어 주는 것이 지금 기다리는 중이라는 유일한 신호다.
        /// </summary>
        internal void SetNextRoundReadyState(bool localReady, int readyCount, int total) =>
            SetReadyState(btnNextRound, localReady, readyCount, total,
                ZooJackText.Get("Game.Result.NextRound", "다음 라운드"));

        /// <summary>
        /// 결과 공개의 '계속' 버튼을 고발 단계 준비 버튼으로 쓴다.
        /// 이 버튼은 결과 카드 안에 있어 카드가 뜨기 전에는 눌릴 수 없다 —
        /// 상대 손패가 다 열리기 전에 넘기는 일이 구조적으로 막힌다.
        /// </summary>
        internal void SetAccusationReadyState(bool localReady, int readyCount, int total) =>
            SetReadyState(btnContinue, localReady, readyCount, total,
                ZooJackText.Get("Game.Result.ToAccusation", "고발 단계로"));

        /// <summary>
        /// 비긴 판에서는 같은 '계속' 버튼이 재배분 준비 버튼이 된다. 가는 곳이 다르므로
        /// 글자도 달라야 한다 — 비긴 판에는 고발할 것이 없다.
        /// </summary>
        internal void SetTieRedealReadyState(bool localReady, int readyCount, int total) =>
            SetReadyState(btnContinue, localReady, readyCount, total,
                ZooJackText.Get("Game.Result.Redeal", "카드 다시 받기"));

        /// <summary>
        /// 매치 종료의 '다시 플레이'를 전원 준비 버튼으로 쓴다. 전원이 누르면 방 로비로
        /// 돌아가 캐릭터부터 다시 고른다. 기다리다 지치면 옆의 나가기로 빠질 수 있다.
        /// </summary>
        internal void SetRematchReadyState(bool localReady, int readyCount, int total) =>
            SetReadyState(btnRestartMatch, localReady, readyCount, total,
                ZooJackText.Get("Game.MatchOver.Rematch", "다시 플레이"));

        // 누른 뒤 화면이 그대로면 "안 눌렸나?" 싶어 계속 누르게 된다.
        // 버튼을 잠그고 인원수를 세어 주는 것이 지금 기다리는 중이라는 유일한 신호다.
        private static void SetReadyState(
            Button button, bool localReady, int readyCount, int total, string idleLabel)
        {
            if (button == null) return;

            button.interactable = !localReady;
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = localReady
                    ? ZooJackText.Get("Game.Ready.WaitingCount", "기다리는 중  {0}/{1}", readyCount, total)
                    : idleLabel;
        }

        private string NameOf(string pid)
        {
            var seat = players.Find(p => p.PlayerId == pid);
            return seat != null ? seat.DisplayName : pid;
        }

        // ── 커버 (핫시트 전환) ────────────────────────────────────────

        /// <summary>화면 한가운데 잠깐 떴다 사라지는 알림. 누를 것이 없는 소식에 쓴다.</summary>
        internal void ShowToast(string title, string body) =>
            toast?.Show(title, body, ToastSeconds);

        /// <summary>
        /// 승인된 공개 행동 알림. 소리를 여기서 함께 낸다.
        ///
        /// 네트워크 디렉터도 자기 경로 끝에서 이 창구를 부른다 — 알림과 소리가
        /// 한 줄에 묶여 있어야 핫시트와 빌드가 갈라지지 않는다.
        /// </summary>
        internal void ShowActionToast(PlayerActionType actionType, string title, string body)
        {
            // 히트·스탠드·더블다운·다이 네 가지에만 소리를 얹는다. 딜러의 카드 배분은
            // 곧바로 카드가 내려오는 소리로 이어져, 여기서 한 번 더 울리면 겹친다.
            if (IsPlayerDecision(actionType)) GameAudio.PlayPlayerAction();
            ShowToast(title, body);
        }

        private static bool IsPlayerDecision(PlayerActionType actionType) =>
            actionType is PlayerActionType.RequestHit
                       or PlayerActionType.RequestStand
                       or PlayerActionType.RequestDoubleDown
                       or PlayerActionType.RequestDie
                       or PlayerActionType.RequestSurrender;

        private void ShowApprovedAction(PlayerRole actorRole, PlayerActionType actionType)
        {
            // 딜러의 카드 선택은 다른 참가자에게는 진행 피드백이지만,
            // 선택을 직접 수행한 딜러 본인에게는 중복 안내이므로 띄우지 않는다.
            if (currentViewer == PlayerRole.Dealer &&
                actorRole == PlayerRole.Dealer &&
                actionType == PlayerActionType.SubmitDealerCardChoice)
                return;

            if (ActionAnnouncementMapper.TryMap(
                    actorRole, actionType, out string actorText, out string actionText))
                ShowActionToast(actionType, actorText, actionText);
        }

        private void ShowCover(string msg, System.Action callback) =>
            ShowCover(msg, ZooJackText.Get(
                "Game.Cover.ContinueDetail", "화면을 확인한 뒤 계속해 주세요."), callback);

        /// <summary>
        /// 화면을 넘겨 줄 때 덮는 커버. 제목 칸은 한 줄짜리라 긴 설명은
        /// <paramref name="detail"/>로 넘겨야 잘리지 않는다.
        /// </summary>
        private void ShowCover(string msg, string detail, System.Action callback)
        {
            onRevealed = callback;
            if (waitingOverlay != null)
                waitingOverlay.Show(msg, detail);
            else if (txtCoverMsg != null)
                txtCoverMsg.text = msg + "\n" + detail;
            if (btnReveal != null) btnReveal.gameObject.SetActive(true);
            ShowOnly(panelCover);
        }

        private void OnReveal()
        {
            var cb = onRevealed;
            onRevealed = null;
            cb?.Invoke();
        }

        // ── UI 헬퍼 ──────────────────────────────────────────────────

        private void ShowOnly(GameObject panel) =>
            panels.ShowOnly(panel, phase == GamePhase.Accusation);

        private int Bal(string pid) => balances.TryGetValue(pid, out int v) ? v : 0;

        // 좌석 id로 그 사람의 캐릭터를 찾는다. 머니바 칸은 캐릭터로 찾으므로 필요하다.
        private CharacterId CharacterOf(string pid)
        {
            if (players == null) return CharacterId.None;

            foreach (var seat in players)
                if (seat.PlayerId == pid) return seat.Character;

            return CharacterId.None;
        }

        private CharacterId CharacterForRole(PlayerRole role)
        {
            if (players == null) return CharacterId.None;
            foreach (var seat in players)
                if (seat.Role == role) return seat.Character;
            return CharacterId.None;
        }

        internal void ShowRoleReveal(PlayerRole role, CharacterId character, System.Action onComplete = null)
        {
            if (roleReveal == null)
            {
                onComplete?.Invoke();
                return;
            }
            roleReveal.Show(role, PortraitFor(character), ZooJackPalette.CharacterAccent(character), onComplete);
        }

        private void ShowRoleReveal(PlayerRole role, System.Action onComplete) =>
            ShowRoleReveal(role, CharacterForRole(role), onComplete);

        /// <summary>
        /// 머니바는 <b>사람 자리</b>다. 좌석마다 자기 칸이 있고 거기서 움직이지 않는다.
        /// 좌석을 순회하므로 역할이 교대돼도 각자의 돈은 자기 칸에 그대로 남는다.
        /// </summary>
        private void RefreshMoney()
        {
            if (players == null) return;

            foreach (var seat in players)
                Money.SetCard(seat.Character, seat.Role, Bal(seat.PlayerId));

            Money.HighlightLocal(CharacterForRole(currentViewer));
        }

        // ── 캐릭터 초상화 ─────────────────────────────────────────────

        /// <summary>머니바 카드에 들어가는 원형 초상화. 없으면 null.</summary>
        internal Sprite PortraitFor(CharacterId id) => id switch
        {
            CharacterId.Rabbit => portraitRabbit,
            CharacterId.Fox    => portraitFox,
            CharacterId.Croc   => portraitCroc,
            _                  => null
        };

        /// <summary>테이블 위를 걸어 다니는 전신 그림. 없으면 null.</summary>
        internal Sprite AvatarSpriteFor(CharacterId id) => id switch
        {
            CharacterId.Rabbit => avatarRabbit,
            CharacterId.Fox    => avatarFox,
            CharacterId.Croc   => avatarCroc,
            _                  => null
        };

        /// <summary>
        /// 테이블 위 아바타 자리마다 <b>지금 그 역할을 맡은 사람</b>의 전신 그림을 건다.
        /// 아바타 자리는 역할에 고정(A는 왼쪽, B는 오른쪽, 딜러는 위)이므로 역할이
        /// 교대되면 그림이 자리 사이를 옮겨 다닌다.
        ///
        /// 머니바는 반대로 사람에 고정이라 여기서 건드리지 않는다 — <see cref="MoneyBarView.SetCard"/> 참고.
        /// </summary>
        // 어느 역할을 어느 캐릭터가 맡고 있는지. 최종 판정 큐브가 초상화를 고를 때 쓴다.
        // 핫시트는 좌석에서, 네트워크는 호스트가 보낸 값에서 들어오는데 둘 다 이 메서드를
        // 지나므로, 여기 한 곳에만 기억해 두면 두 모드가 같은 값을 본다.
        private CharacterId characterInA, characterInB, characterInDealer;

        internal void RefreshCharacterVisuals(CharacterId roleA, CharacterId roleB, CharacterId roleDealer)
        {
            characterInA = roleA;
            characterInB = roleB;
            characterInDealer = roleDealer;

            if (avatarStage == null) return;
            // 테이블은 전신 그림을 쓴다. 초상화(원형)를 넣으면 캐릭터가 동그란 아이콘으로 변한다.
            ApplyAvatarSprite(PlayerRole.PlayerA, roleA);
            ApplyAvatarSprite(PlayerRole.PlayerB, roleB);
            ApplyAvatarSprite(PlayerRole.Dealer,  roleDealer);
        }

        /// <summary>
        /// 캐릭터 머리 위 명패를 갱신한다. 이름은 지금 맡은 직군이고, 타이머와 행동 문구는
        /// <paramref name="timedRole"/> 한 명에게만 붙는다 — 명패의 목적이 "지금 누구를
        /// 기다리고 있고, 그 사람이 무엇을 하는 중이며, 얼마나 남았나"를 남들에게 알리는
        /// 것이기 때문이다.
        /// </summary>
        internal void RefreshAvatarPlates(
            GamePhase currentPhase, PlayerRole timedRole, float secondsLeft) =>
            RefreshAvatarPlates(currentPhase, timedRole, PlayerRole.None, secondsLeft);

        /// <summary>
        /// 시계 주인이 둘인 단계(뇌물·판돈)용. 두 사람이 동시에 고르고 마감도 하나라서
        /// 한 명만 고를 수 있는 <see cref="RefreshAvatarPlates(GamePhase, PlayerRole, float)"/>로는
        /// "지금 누구를 기다리는가"를 표현하지 못한다. 이미 낸 사람은 부르는 쪽에서
        /// None으로 빼 주므로, 남은 사람 머리 위에만 숫자가 남는다.
        /// </summary>
        internal void RefreshAvatarPlates(
            GamePhase currentPhase, PlayerRole timedRole, PlayerRole coTimedRole, float secondsLeft)
        {
            if (avatarStage == null) return;

            // 상단 타이머 카드와 같은 변환을 써야 두 시계가 같은 숫자를 가리킨다.
            int left = TurnClock.Seconds(secondsLeft);
            string action = PlateActionKo(currentPhase);
            ApplyPlate(PlayerRole.PlayerA, timedRole, coTimedRole, left, action);
            ApplyPlate(PlayerRole.PlayerB, timedRole, coTimedRole, left, action);
            ApplyPlate(PlayerRole.Dealer,  timedRole, coTimedRole, left, action);
        }

        // 시계를 쥔 사람이 아니면 초도 행동 문구도 숨긴다. 전원에게 띄우면 어느 것이
        // 지금 도는 시계이고 누구를 기다리는 중인지 알 수 없다.
        private void ApplyPlate(PlayerRole role, PlayerRole timedRole, PlayerRole coTimedRole,
            int secondsLeft, string action)
        {
            bool waitingOnThisSeat = role == timedRole || role == coTimedRole;
            // 이름을 지었으면 이름만, 아직이라면(또는 핫시트라면) 배역으로 부른다.
            avatarStage.SetPlate(role, PlayerNames.Label(role, RoleKo(role)),
                waitingOnThisSeat ? secondsLeft : -1,
                waitingOnThisSeat ? action : null);
        }

        /// <summary>
        /// 낼 것을 낸 사람 발밑에 <see cref="AvatarStage.ReadyBadge"/>를 붙인다.
        ///
        /// <b>둘이 동시에 고르는 단계에서만 뜻이 있다.</b> 뇌물과 판돈은 A·B가 같은 마감을
        /// 두고 함께 고르므로 "나는 냈고 쟤는 아직"이 성립한다. 딜러 카드 배분처럼 한 사람
        /// 차례인 단계에서는 나머지가 <b>아직 안 낸 것이 아니라 낼 것이 없는 것</b>이라,
        /// 거기까지 붙이면 지금 누구를 기다리는지가 오히려 흐려진다.
        /// </summary>
        private void RefreshReadyBadges()
        {
            if (roleAssignment == null) { ClearReadyBadges(); return; }

            bool bribeStage = phase == GamePhase.BribeSelection;
            bool betStage   = phase == GamePhase.BetSelection;

            SetReadyBadge(PlayerRole.PlayerA,
                HasSubmitted(roleAssignment.GetPlayerA(), bribeStage, betStage));
            SetReadyBadge(PlayerRole.PlayerB,
                HasSubmitted(roleAssignment.GetPlayerB(), bribeStage, betStage));
            SetReadyBadge(PlayerRole.Dealer, false);
        }

        private bool HasSubmitted(PlayerSeat seat, bool bribeStage, bool betStage) =>
            seat != null
            && (bribeStage ? bribes.ContainsKey(seat.PlayerId)
              : betStage   ? bets.ContainsKey(seat.PlayerId)
                           : false);

        private void ClearReadyBadges()
        {
            SetReadyBadge(PlayerRole.PlayerA, false);
            SetReadyBadge(PlayerRole.PlayerB, false);
            SetReadyBadge(PlayerRole.Dealer, false);
        }

        /// <summary>
        /// 준비 표시 하나를 켜고 끈다. 네트워크 쪽도 이 길로 들어온다 —
        /// 무엇을 냈는지는 저마다 알지만, 어디에 어떻게 그릴지는 여기만 안다.
        /// </summary>
        internal void SetReadyBadge(PlayerRole role, bool ready) =>
            avatarStage?.SetReady(role, ready);

        /// <summary>
        /// 명패 위 행동 문구. 시계를 쥔 사람 머리 위에만 뜨므로 주어를 적지 않는다 —
        /// 누가 하는 중인지는 문구가 떠 있는 <b>자리</b>가 말한다.
        ///
        /// 시계가 도는 단계에만 값이 있다. 나머지 단계는 기다릴 사람 자체가 없어
        /// <see cref="RefreshAvatarPlates(GamePhase, PlayerRole, PlayerRole, float)"/>가
        /// None을 받으므로 어차피 뜨지 않는다.
        /// </summary>
        private static string PlateActionKo(GamePhase currentPhase) => currentPhase switch
        {
            GamePhase.BribeSelection => ZooJackText.Get("Game.PlateAction.Bribe", "선택하는 중..."),
            GamePhase.BetSelection => ZooJackText.Get("Game.PlateAction.Bet", "베팅하는 중..."),
            GamePhase.DealerCardDistribution => ZooJackText.Get("Game.PlateAction.Deal", "배분하는 중..."),
            GamePhase.PlayerDecision => ZooJackText.Get("Game.PlateAction.Decision", "고민하는 중..."),
            GamePhase.Accusation => ZooJackText.Get("Game.PlateAction.Accusation", "고발하는 중..."),
            _                                => null
        };

        /// <summary>
        /// 상단 턴 타이머 카드를 켜고 끈다. 시계가 도는 단계에서만 보인다.
        /// 어떤 단계에 시계가 있는지는 부르는 쪽이 판단한다.
        /// </summary>
        internal void SetTurnTimerVisible(bool visible)
        {
            if (txtDealerTimer == null) return;

            Transform card = txtDealerTimer.transform.parent;
            if (card == null || card.gameObject.activeSelf == visible) return;
            card.gameObject.SetActive(visible);
        }

        internal static string RoleKo(PlayerRole role) =>
            role == PlayerRole.None ? string.Empty : ZooJackText.RoleName(role);

        // 그림이 준비되지 않은 좌석(매치 시작 전)은 건드리지 않는다.
        // null을 넣으면 SetSprite가 Image를 꺼버려 캐릭터가 통째로 사라진다.
        private void ApplyAvatarSprite(PlayerRole role, CharacterId id)
        {
            var sprite = AvatarSpriteFor(id);
            if (sprite != null) avatarStage.SetSprite(role, sprite);
        }

        /// <summary>
        /// 지금 테이블에 올라와 있는 금액으로 칩을 다시 놓는다. 더블다운을 선언한 쪽만
        /// 2배다. 베팅 자체는 바뀌지 않으므로 배수만 다시 태우면 된다.
        /// </summary>
        private void RefreshBetChips()
        {
            if (roleAssignment == null) return;

            bets.TryGetValue(roleAssignment.GetPlayerA().PlayerId, out int betA);
            bets.TryGetValue(roleAssignment.GetPlayerB().PlayerId, out int betB);

            BetChips.Show(
                RoundSettlement.TableBet(betA, roundContext != null && roundContext.HasDoubled(PlayerRole.PlayerA)),
                RoundSettlement.TableBet(betB, roundContext != null && roundContext.HasDoubled(PlayerRole.PlayerB)),
                roundIndex, phase);
        }

        private void RefreshStatus()
        {
            RefreshMoney();
            BetChips.RefreshTotal(phase);

            // 매치가 끝나면 단계는 아직 FinalJudgment지만 화면은 매치 종료다. 상태바만
            // "최종 판정"으로 남으면 방금 판의 판정을 기다리는 줄 알고 버튼을 찾게 된다.
            if (txtPhase != null) txtPhase.text = matchEnded ? MatchOverPhase : ZooJackText.PhaseName(phase);
            if (txtPhaseAction != null)
                txtPhaseAction.text = matchEnded ? MatchOverPhaseAction : PhaseActionKo(phase);
            if (txtRound != null)
                txtRound.text = ZooJackText.Get(
                    "Game.Round.Counter", "라운드 {0} / {1}", roundIndex, MaxRounds);
            RefreshCharacterVisualsFromSeats();
        }

        // 좌석의 현재 역할로 '이 역할을 맡은 사람의 캐릭터'를 찾아 그린다.
        // roleAssignment.RotateDealer()가 역할을 돌리면 여기서 자동으로 따라온다.
        private void RefreshCharacterVisualsFromSeats()
        {
            if (players == null) return;

            CharacterId a = CharacterId.None, b = CharacterId.None, d = CharacterId.None;
            foreach (var seat in players)
            {
                if (seat.Role == PlayerRole.PlayerA) a = seat.Character;
                else if (seat.Role == PlayerRole.PlayerB) b = seat.Character;
                else if (seat.Role == PlayerRole.Dealer) d = seat.Character;
            }
            RefreshCharacterVisuals(a, b, d);
        }

        private string PhaseActionKo(GamePhase currentPhase) => currentPhase switch
        {
            GamePhase.WaitingForPlayers => ZooJackText.Get(
                "Game.Action.Waiting.Hotseat", "게임 시작을 기다려 주세요."),
            GamePhase.RoleAssignment => ZooJackText.Get(
                "Game.Action.RoleAssignment", "배정된 역할을 확인해 주세요."),
            GamePhase.BribeSelection => ZooJackText.Get(
                "Game.Action.Bribe.Hotseat", "{0}는 딜러에게 줄 뇌물을 결정하세요.",
                currentViewer == PlayerRole.PlayerB ? ZooJackText.RoleName(PlayerRole.PlayerB) : ZooJackText.RoleName(PlayerRole.PlayerA)),
            GamePhase.BetSelection => ZooJackText.Get(
                "Game.Action.Bet.Hotseat", "{0}는 카드를 확인하고 판돈을 결정하세요.",
                currentViewer == PlayerRole.PlayerB ? ZooJackText.RoleName(PlayerRole.PlayerB) : ZooJackText.RoleName(PlayerRole.PlayerA)),
            GamePhase.SystemResultGeneration => ZooJackText.Get(
                "Game.Action.ResultGeneration", "게임 결과를 생성하고 있습니다."),
            GamePhase.CardCandidateGeneration => ZooJackText.Get(
                "Game.Action.CardCandidates", "딜러의 카드 후보를 준비하고 있습니다."),
            GamePhase.DealerCardDistribution => ZooJackText.Get(
                "Game.Action.Dealer.Local", "딜러는 배분할 카드를 선택하세요."),
            GamePhase.PlayerDecision => ZooJackText.Get(
                "Game.Action.Player.Hotseat", "{0}는 히트 또는 스탠드를 선택하세요.",
                currentViewer == PlayerRole.PlayerB ? ZooJackText.RoleName(PlayerRole.PlayerB) : ZooJackText.RoleName(PlayerRole.PlayerA)),
            GamePhase.BlackjackResultCalculation => ZooJackText.Get(
                "Game.Action.ResultCalculation", "카드 결과를 계산하고 있습니다."),
            GamePhase.DealerDecision => ZooJackText.Get(
                "Game.Action.DealerDecision.Local", "딜러는 결과를 확인하고 결정하세요."),
            GamePhase.ResultReveal => ZooJackText.Get(
                "Game.Action.ResultReveal", "공개된 라운드 결과를 확인하세요."),
            GamePhase.Accusation => ZooJackText.Get(
                "Game.Action.Accusation.Hotseat", "패배한 플레이어는 고발 여부를 결정하세요."),
            GamePhase.FinalJudgment => ZooJackText.Get(
                "Game.Action.FinalJudgment", "최종 판정 결과를 확인하세요."),
            GamePhase.TieRedeal => ZooJackText.Get(
                "Game.Action.TieRedeal.Hotseat", "카드를 다시 배분하고 있습니다."),
            GamePhase.RoundEnd => ZooJackText.Get(
                "Game.Action.RoundEnd", "다음 라운드를 준비해 주세요."),
            _                                    => string.Empty
        };

        internal static string DealerCardValue(BlackjackCard card)
        {
            if (card == null) return "-";
            if (card.Rank == BlackjackCardRank.Ace) return "1 / 11";
            return card.Rank >= BlackjackCardRank.Jack ? "10" : ((int)card.Rank).ToString();
        }

        private void RefreshCardSlots()
        {
            if (roundContext == null) return;
            var aCards = roundContext.PlayerAHand?.Cards;
            var bCards = roundContext.PlayerBHand?.Cards;
            bool reveal = IsRevealPhase(phase);
            bool dealerView = currentViewer == PlayerRole.Dealer;
            int aFaceUp = reveal || dealerView || currentViewer == PlayerRole.PlayerA ? AllFaceUp : UpcardOnly;
            int bFaceUp = reveal || dealerView || currentViewer == PlayerRole.PlayerB ? AllFaceUp : UpcardOnly;

            RenderFixedRoles(aCards, aFaceUp, bCards, bFaceUp);
        }

        // 무승부 안내(TieRedeal)도 공개 구간이다. 무엇 때문에 비겼는지 양쪽 손패를 보여줘야
        // "왜 갑자기 다시 나눠주지?"가 되지 않는다. 무승부에는 고발이 없으므로 공개해도 손해가 없다.
        internal static bool IsRevealPhase(GamePhase p) =>
            p == GamePhase.ResultReveal || p == GamePhase.Accusation ||
            p == GamePhase.FinalJudgment || p == GamePhase.RoundEnd ||
            p == GamePhase.TieRedeal;

        // 앞면으로 보여줄 장수. 자기 손패는 전부, 상대 손패는 업카드(첫 장) 1장만.
        internal const int AllFaceUp  = int.MaxValue;
        internal const int UpcardOnly = 1;

        /// <summary>상대 손패 중 앞면으로 보여줄 장수. 공개 페이즈면 전부, 아니면 업카드 1장.</summary>
        internal static int OppFaceUpCount(bool reveal) => reveal ? AllFaceUp : UpcardOnly;

        // 결과 공개 연출. 마지막으로 그려 달라고 받은 내용을 들고 있어야, 한 장이 더
        // 열릴 때마다 스스로 다시 그릴 수 있다 — 디렉터는 화면이 바뀔 때만 그리기 때문이다.
        private IReadOnlyList<BlackjackCard> lastACards, lastBCards;
        private int lastAFaceUp, lastBFaceUp;
        private HandRevealSequence handReveal;

        private HandRevealSequence HandReveal =>
            handReveal ?? (handReveal = new HandRevealSequence(OnRevealStep));

        /// <summary>
        /// 한 장이 더 열릴 때마다 손패<b>와 점수 카드를 함께</b> 다시 그린다.
        ///
        /// 점수 카드를 빼면 안 된다. 점수를 가릴지 말지는 <see cref="IsHandHidden"/>가
        /// "아직 덮인 카드가 남았는가"로 정하는데, 그 답이 바뀌는 순간이 바로 여기다.
        /// 디렉터는 네트워크 상태가 바뀔 때만 화면을 그리므로, 이 연출이 스스로 부르지
        /// 않으면 카드는 다 뒤집혔는데 점수 카드만 "2 + ?"에 멈춰 있는다.
        /// 다음에 아무나 버튼을 눌러 화면이 갱신되면 그제야 풀려서, 가끔씩 나는 것처럼 보였다.
        /// </summary>
        private void OnRevealStep()
        {
            DrawCachedHands();
            RefreshCachedScoreCards();
        }

        /// <summary>
        /// 마지막으로 그려 달라고 받은 손패로 점수 카드를 다시 그린다.
        /// 점수는 그 손패에서 직접 계산한다 — 디렉터를 거치지 않고 부를 수 있어야
        /// 연출 도중에도 쓸 수 있다.
        /// </summary>
        private void RefreshCachedScoreCards() =>
            RefreshRevealedScoreCards(
                lastACards, BlackjackScoreCalculator.Calculate(
                    new BlackjackHand { Cards = AsList(lastACards) }),
                lastBCards, BlackjackScoreCalculator.Calculate(
                    new BlackjackHand { Cards = AsList(lastBCards) }));

        // 물리 슬롯은 플레이어 역할에 고정한다. A는 항상 왼쪽, B는 항상 오른쪽이다.
        // 뒷면은 CardView.ShowFaceDown()이 레지스트리의 Card_Back(빨강) 스프라이트를 사용한다.
        internal void RenderFixedRoles(
            IReadOnlyList<BlackjackCard> aCards, int aFaceUp,
            IReadOnlyList<BlackjackCard> bCards, int bFaceUp)
        {
            lastACards = aCards; lastAFaceUp = aFaceUp;
            lastBCards = bCards; lastBFaceUp = bFaceUp;
            DrawCachedHands();
        }

        // 앞면으로 보여 줄 장수를 연출에 한 번 물어보고 그린다. 공개 순간에는 요청보다
        // 적은 수가 돌아오고, 그 수가 시간에 따라 한 장씩 올라간다.
        private void DrawCachedHands()
        {
            int aShown = HandReveal.Submit(PlayerRole.PlayerA, lastACards?.Count ?? 0, lastAFaceUp);
            int bShown = HandReveal.Submit(PlayerRole.PlayerB, lastBCards?.Count ?? 0, lastBFaceUp);

            DrawHand(lastACards, aShown, handLayoutA, cardsSelf);
            DrawHand(lastBCards, bShown, handLayoutB, cardsOpp);
        }

        /// <summary>
        /// 이 손에 아직 덮여 있는 카드가 남았는지. 점수를 숨길지 정하는 데 쓴다.
        /// 읽기만 한다 — 여기서 <see cref="HandRevealSequence.Submit"/>을 부르면
        /// 물어보는 것만으로 공개가 시작되어 버린다.
        /// </summary>
        private bool IsHandHidden(PlayerRole role, IReadOnlyList<BlackjackCard> cards) =>
            HandReveal.Shown(role) < (cards?.Count ?? 0);

        private void OnDestroy()
        {
            MatchSurrender.ClearHost(this);
            surrenderHold?.Kill();

            if (emotionWheel != null && emotionWheelWired)
            {
                emotionWheel.EmotionSelected -= OnEmotionSelected;
                emotionWheel.SetOpenGuard(null);
                emotionWheelWired = false;
            }
            handReveal?.StopAll();
            markerReveal?.Kill();
        }

        internal void RefreshDecisionScoreCards(
            PlayerRole activeRole,
            IReadOnlyList<BlackjackCard> aCards, BlackjackScore scoreA,
            IReadOnlyList<BlackjackCard> bCards, BlackjackScore scoreB)
        {
            if (txtDecisionScoreA != null)
                txtDecisionScoreA.text = DecisionScoreCardText(
                    "A", activeRole == PlayerRole.PlayerA, aCards, scoreA);
            if (txtDecisionScoreB != null)
                txtDecisionScoreB.text = DecisionScoreCardText(
                    "B", activeRole == PlayerRole.PlayerB, bCards, scoreB);
        }

        /// <summary>
        /// 양쪽 점수를 모두 공개한다. 단, <b>아직 덮여 있는 카드가 남은 손은 감춘 채로 둔다</b> —
        /// 카드를 한 장씩 뒤집는 동안 합계가 먼저 떠 있으면 뒤집을 이유가 없어진다.
        /// 마지막 장이 열리는 순간 이 칸도 함께 진짜 숫자로 바뀐다.
        /// </summary>
        internal void RefreshRevealedScoreCards(
            IReadOnlyList<BlackjackCard> aCards, BlackjackScore scoreA,
            IReadOnlyList<BlackjackCard> bCards, BlackjackScore scoreB)
        {
            if (txtDecisionScoreA != null)
                txtDecisionScoreA.text = DecisionScoreCardText(
                    "A", !IsHandHidden(PlayerRole.PlayerA, aCards), aCards, scoreA);
            if (txtDecisionScoreB != null)
                txtDecisionScoreB.text = DecisionScoreCardText(
                    "B", !IsHandHidden(PlayerRole.PlayerB, bCards), bCards, scoreB);
        }

        /// <summary>
        /// 카드를 다 읽은 뒤 판정이 올라오기까지의 뜸(초).
        /// 이 사이에 보는 사람이 직접 점수를 견주고 누가 이겼는지 짐작한다.
        /// </summary>
        private const float JudgmentDelay = 1f;

        // 표식을 늦게 붙이는 예약. 화면이 먼저 넘어가면 취소해야 하므로 들고 있는다.
        private Tween markerReveal;

        /// <summary>
        /// 라운드 결과를 알린다. 순서는 <b>카드 → 점수 → (한 박) → 판정</b>이다.
        ///
        /// 판정이란 결과 문구와 왕관·비석을 말하며, 둘은 <b>같은 순간에</b> 올라온다.
        /// 문구가 먼저 뜨고 표식이 뒤따르면 같은 사실을 두 번 말하는 꼴이 되어,
        /// 정작 판정이 내려지는 한 순간의 무게가 둘로 쪼개진다.
        ///
        /// 카드가 다 열린 뒤에 뜸을 두는 이유는 그 사이에 보는 사람이 직접 점수를
        /// 견주게 하기 위해서다. 답을 먼저 말해 버리면 카드를 한 장씩 뒤집은 것이
        /// 아무 의미가 없다 — 최종 판정에서 주사위보다 먼저 답을 흘리지 않는 것과 같다.
        /// </summary>
        /// <param name="tie">
        /// 비긴 판인지. 왕관도 비석도 세우지 않고 소리도 팡파레가 아니다 — 이긴 사람이
        /// 없는데 한쪽에 왕관이 서면 그 판을 이겼다고 읽는다.
        /// </param>
        internal void PresentRoundResult(
            string outcomeText,
            bool playerAWon,
            bool tie = false,
            PlayerRole foldedRole = PlayerRole.None)
        {
            // 이 연출은 한 결과에 한 번만 시작한다. 화면 갱신은 남이 준비 버튼을 누를
            // 때마다 다시 도는데, 그때마다 여기 들어오면 결과 카드가 다시 숨겨지고
            // 판정이 1초씩 미뤄져 카드가 깜빡인다.
            //
            // 잠금을 푸는 곳은 <see cref="ClearRoundResultPresentation"/> 하나뿐이다.
            if (resultPresented) return;
            resultPresented = true;

            if (txtResultOutcome != null) txtResultOutcome.text = outcomeText;
            SetResultDescription(tie, foldedRole);

            // 결과 카드는 따로 참조를 두지 않고 승자 문구의 부모를 쓴다.
            // 배선을 하나 더 늘리면 그만큼 끊어질 곳이 늘어난다.
            GameObject resultCard = txtResultOutcome != null
                ? txtResultOutcome.transform.parent.gameObject
                : null;

            // 판정 — 문구와 표식이 함께 올라온다. "계속" 버튼도 이 카드 안에 있으므로
            // 이때부터 넘길 수 있다. 판정을 보기 전에 지나쳐 버릴 길이 없어진다.
            void Judge()
            {
                // 겉보기 승리를 알리는 순간. 이 함수는 resultPresented 잠금 뒤에 예약되므로
                // 한 라운드에 한 번만 돈다 — 화면을 몇 번 다시 그리든 소리는 한 번이다.
                //
                // 비긴 판에는 팡파레를 울리지 않는다. 아무도 이기지 않았는데 승리 나팔이
                // 나면 결과를 잘못 읽는다. 알림 소리만 짧게 낸다.
                if (tie) GameAudio.PlayPlayerAction();
                else GameAudio.PlayApparentWin();

                ResultCardPop.Play(resultCard);

                // 표식만 내린다. 여기는 markerReveal이 부르는 자리라, 그것을 함께
                // 죽이는 HideResultMarkers를 쓰면 자기 트윈을 안에서 끊게 된다.
                if (tie) Markers.HideResult();
                else Markers.RefreshResult(playerAWon);
            }

            // 카드를 다 읽은 시점. 점수만 진짜 숫자로 바꾸고 판정은 한 박 미룬다.
            void CardsRead()
            {
                RefreshCachedScoreCards();
                markerReveal?.Kill();
                markerReveal = DOVirtual.DelayedCall(JudgmentDelay, Judge, true);
            }

            HideResultMarkers();   // 예약된 판정까지 함께 취소한다
            ResultCardPop.Hide(resultCard);

            if (!HandReveal.IsRunning) { CardsRead(); return; }
            HandReveal.WhenDone(CardsRead);
        }

        /// <summary>
        /// 결과 카드의 설명 줄을 판에 맞게 바꾼다.
        ///
        /// 승부가 갈린 판의 글은 <b>씬에 적힌 것</b>을 그대로 쓴다. 여기에 다시 적어 두면
        /// 씬에서 문구를 다듬어도 코드가 덮어써 버린다.
        /// </summary>
        private void SetResultDescription(bool tie, PlayerRole foldedRole)
        {
            if (txtResultDescription == null) return;

            if (resultDescriptionDefault == null)
                resultDescriptionDefault = txtResultDescription.text;

            txtResultDescription.text = tie
                ? ZooJackText.Get("Game.Result.Description.Tie", TieDescription)
                : foldedRole != PlayerRole.None
                    ? ZooJackText.Get("Game.Result.Description.Die", "{0}가 다이했습니다.",
                        ZooJackText.RoleName(foldedRole))
                    : ZooJackText.Get("Game.Result.Description.Decided", resultDescriptionDefault);
        }

        private const string TieDescription =
            "승자가 없습니다. 뇌물과 조작 기록은 그대로 두고 카드만 다시 나눕니다.";

        /// <summary>씬에 적혀 있던 설명. 처음 그릴 때 한 번만 붙잡는다.</summary>
        private string resultDescriptionDefault;

        private static List<BlackjackCard> AsList(IReadOnlyList<BlackjackCard> cards) =>
            cards == null ? new List<BlackjackCard>() : new List<BlackjackCard>(cards);

        /// <summary>
        /// 왕관·비석을 모두 내린다. 아직 승자를 말할 때가 아닐 때 쓴다.
        ///
        /// <see cref="ApplyMarker"/>로는 감출 수 없다 — 그쪽은 <c>isWinner</c>가 거짓이면
        /// <b>비석을 보여 주는</b> 것이지 표식을 내리는 것이 아니다. 여기서 그걸 감추기로
        /// 착각해서, 카드가 뒤집히기도 전에 양쪽에 비석이 서 있었다.
        ///
        /// 예약해 둔 판정도 함께 취소한다. 남겨 두면 방금 감춘 표식이 잠시 뒤 혼자 올라온다.
        /// </summary>
        internal void HideResultMarkers()
        {
            markerReveal?.Kill();
            markerReveal = null;
            Markers.HideResult();
        }

        /// <summary>
        /// 대기 화면 위에 양쪽 점수 합계 박스를 띄운다.
        /// 딜러는 양쪽 전체 점수를 보고, 일반 플레이어는 자기 점수만 전체 공개한다.
        /// 상대 점수는 첫 공개 카드만 표시하고 나머지는 숨긴다.
        ///
        /// <see cref="ShowOnly"/>가 보이는 패널을 기준으로 박스를 꺼버리므로,
        /// 반드시 대기 화면을 띄운 <b>뒤에</b> 호출해야 한다.
        /// </summary>
        internal void ShowScoreCardsWhileWaiting(
            PlayerRole viewerRole,
            IReadOnlyList<BlackjackCard> aCards, BlackjackScore scoreA,
            IReadOnlyList<BlackjackCard> bCards, BlackjackScore scoreB)
        {
            if (viewerRole == PlayerRole.Dealer)
                RefreshRevealedScoreCards(aCards, scoreA, bCards, scoreB);
            else
                RefreshDecisionScoreCards(viewerRole, aCards, scoreA, bCards, scoreB);

            if (sharedScoreCards != null) sharedScoreCards.SetActive(true);
        }

        // ── 고발 선택지 ──────────────────────────────────────────────

        /// <summary>
        /// 고발 화면의 카드 두 장을 채운다. 문구는 규칙(<see cref="RoundSettlement"/>)이
        /// 들고 있으므로 여기서는 어느 칸에 넣을지만 정한다.
        /// </summary>
        internal void RefreshAccusationCards()
        {
            if (txtAccuseHeadline != null)
                txtAccuseHeadline.text = ZooJackText.Get(
                    "Game.Accusation.Headline", "딜러를 믿으시겠습니까?");
            if (txtAccuseSubline != null)
                txtAccuseSubline.text = ZooJackText.Get(
                    "Game.Accusation.Subline", "선택한 뒤에 진실이 밝혀집니다.");

            FillChoiceCard(cardAccuse, ZooJackText.Get("Game.Accusation.Accuse", "고발"),
                RoundSettlement.AccuseCardBody(),
                RoundSettlement.AccuseTagEffect(), RoundSettlement.AccuseTagCost());

            FillChoiceCard(cardAccept, ZooJackText.Get("Game.Accusation.Accept", "승복"),
                RoundSettlement.AcceptCardBody(),
                RoundSettlement.AcceptTagEffect());
        }

        private static void FillChoiceCard(
            ChoiceCardView card, string name, string body, params string[] tags)
        {
            if (card == null) return;
            if (card.Name != null) card.Name.text = name;
            if (card.Body != null) card.Body.text = body;
            if (card.Tags == null) return;

            for (int i = 0; i < card.Tags.Length; i++)
            {
                var label = card.Tags[i];
                if (label == null) continue;

                string text = i < tags.Length ? tags[i] : null;
                // 글자가 아니라 알약(배경)째로 켜고 끈다. 글자만 지우면 빈 알약이 남는다.
                var pill = label.transform.parent != null ? label.transform.parent.gameObject : label.gameObject;
                bool show = !string.IsNullOrEmpty(text);
                if (pill.activeSelf != show) pill.SetActive(show);
                if (show) label.text = text;
            }
        }

        internal void RefreshFinalPresentation(
            FinalWinner winner,
            bool wasManipulated,
            int deltaA,
            int deltaB,
            int deltaDealer,
            int balanceA,
            int balanceB,
            int balanceDealer)
        {
            // PanelFinal에서 상세 정산 UI를 제거했으므로 갱신할 별도 뷰가 없다.
            // 호출부는 로컬/네트워크 흐름이 공유하므로 호환성을 위해 메서드는 유지한다.
        }

        internal void SetFinalRoundDetailsVisible(bool visible)
        {
            // 상세 정산 그룹은 현재 PanelFinal 구성에서 사용하지 않는다.
        }

        /// <summary>
        /// 최종 판정 큐브를 올리거나 내린다. 올라간 동안 화면이 어두워지고
        /// 초상화 큐브가 구른다.
        ///
        /// 매치 종료 화면은 같은 패널을 쓰지만 큐브를 쓰지 않는다 — 그쪽은 순위표라
        /// 가려낼 승자가 없다.
        ///
        /// 초상화는 <b>지금 그 역할을 맡은 사람</b>의 것이다. 역할은 3라운드마다
        /// 교대되므로 큐브 면도 그때 같이 바뀐다.
        /// </summary>
        internal void SetFinalStage(bool on)
        {
            if (finalCube == null) return;
            if (!on) { finalCube.End(); return; }

            finalCube.Begin(
                PortraitFor(characterInA),
                PortraitFor(characterInDealer),
                PortraitFor(characterInB));
        }

        private static string DecisionScoreCardText(
            string roleLabel, bool showFullScore,
            IReadOnlyList<BlackjackCard> cards, BlackjackScore score)
        {
            if (!showFullScore)
            {
                string upcard = cards != null && cards.Count > 0
                    ? DealerCardValue(cards[0])
                    : "-";
                return ZooJackText.Get(
                    "Game.Score.HiddenOpponent",
                    "<size=15><color={2}>{0} 점수</color></size>\n<size=34><b>{1} + ?</b></size>\n<size=14><color={3}>상대 패</color></size>",
                    roleLabel, upcard, ZooJackPalette.TaupeTag, ZooJackPalette.SlateTag);
            }

            string value = score.IsBust
                ? ZooJackText.Get("Game.Score.Bust", "버스트") : score.BestValue.ToString();
            string kind = score.IsBust
                ? ZooJackText.Get("Game.Score.Bust", "버스트")
                : score.IsSoft ? ZooJackText.Get("Game.Score.Soft", "소프트")
                : ZooJackText.Get("Game.Score.Hard", "하드");
            string color = score.IsBust ? ZooJackPalette.BustTag
                : score.IsSoft ? ZooJackPalette.SoftTag : ZooJackPalette.HardTag;
            return ZooJackText.Get(
                "Game.Score.Full",
                "<size=15><color={4}>{0} 점수</color></size>\n<size=38><b>{1}</b></size>\n<size=15><color={2}><b>{3}</b></color></size>",
                roleLabel, value, color, kind, ZooJackPalette.TaupeTag);
        }

        private static void DrawHand(
            IReadOnlyList<BlackjackCard> cards, int faceUpCount,
            AdaptiveHandLayout layout, IReadOnlyList<CardView> slots)
        {
            int visibleCount = Mathf.Min(cards?.Count ?? 0, RoundEngine.MaxCardsPerHand);
            layout?.Arrange(visibleCount);

            if (slots == null) return;
            for (int i = 0; i < slots.Count; i++) DrawSlot(slots[i], cards, i, faceUpCount);
        }

        /// <summary>슬롯 배열을 통째로 감춘다. 씬 연결이 빈 자리는 건너뛴다.</summary>
        private static void HideAll(IReadOnlyList<CardView> slots)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Count; i++) slots[i]?.Hide();
        }

        private static void DrawSlot(
            CardView view, IReadOnlyList<BlackjackCard> cards, int idx, int faceUpCount)
        {
            if (view == null) return;
            if (cards != null && idx < cards.Count)
            {
                if (idx < faceUpCount) view.ShowCard(cards[idx]);
                else                   view.ShowFaceDown();
            }
            else view.Hide();
        }

        private void ClearDealerSlots() => HideAll(cardsDealer);

        private void ClearPlayerSlots()
        {
            HideAll(cardsSelf);
            HideAll(cardsOpp);
        }

        private void ClearAllSlots()
        {
            ClearPlayerSlots();
            ClearDealerSlots();
        }

    }
}
