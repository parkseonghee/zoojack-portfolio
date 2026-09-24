#if FUSION2 || ZOOJACK_PHOTON_FUSION
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace ZooJack
{
    public class FusionGameState : NetworkBehaviour, ChatRelay.IHost, PlayerNames.IBoard
    {
        public const int MaxPlayers = 3;
        public const int MaxHandCards = 12;
        /// <summary>
        /// 감정표현을 다시 낼 수 있기까지의 시간(초).
        ///
        /// 말풍선이 떠 있다 사라지기까지 1.65초가 걸린다. 이보다 짧게 잡으면 앞 표현이
        /// 지워지기도 전에 다음 것이 덮어써서, 쓰는 사람에게는 쿨타임이 없는 것처럼
        /// 보인다. 그래서 말풍선이 다 사라진 뒤에 다음 차례가 오도록 둔다.
        /// </summary>
        public const float EmotionCooldownSeconds = 2f;
        private const int MaxBribeAmount = RoundSettlement.MaxBribe; // 뇌물 상한 100

        /// <summary>
        /// 화면의 시계가 0을 가리킨 뒤 호스트가 대신 제출하기까지 두는 여유(초).
        ///
        /// 클라이언트는 자기 시계가 0에 닿으면 지금 고른 값을 스스로 낸다. 그 RPC가
        /// 도착하기 전에 호스트가 기본값으로 덮어써 버리면, 화면을 보고 있던 사람이
        /// 고른 금액이 왕복 지연 때문에 버려진다. 그 폭만큼만 기다린다.
        ///
        /// 이것은 사람에게 주는 시간이 아니라 회선에 주는 여유라서 인스펙터로 빼지 않았다.
        /// </summary>
        private const float SubmitGraceSeconds = 1.5f;

        // ── 제한시간 ─────────────────────────────────────────────────
        //
        // <b>전부 초로 받고 틱 환산은 실행 중에 한다.</b> 예전에는 틱 상수로 박아 두었는데,
        // 그러면 틱레이트를 건드리는 순간 화면에 뜬 숫자와 실제 마감이 어긋난다. 실제로
        // 어긋나 있었다 — 고발은 1800틱이라 적어 두고 64Hz에서 28.1초에 끊겼다.
        //
        // 기본값은 <see cref="RoundSettlement"/>의 규칙 상수에서 온다. 그래서 인스펙터를
        // 건드리지 않으면 핫시트와 네트워크가 같은 값으로 돈다.

        [Header("제한시간 · 사람이 정하는 동안")]
        [Tooltip("딜러가 카드 세 장 중 하나를 고를 시간(초). 넘기면 공정 카드(0번)가 자동 선택된다.\n\n" +
                 "Time Bluffing의 기준값이다 — 고민한 시간 자체가 상대에게 정보가 되므로 " +
                 "너무 길게 잡으면 조작 여부가 시간으로 읽힌다.")]
        [Range(3f, 60f)]
        [SerializeField] private float dealerDecisionSeconds = RoundSettlement.DealerDecisionSeconds;

        [Tooltip("히트·스탠드·더블다운·다이를 정할 시간(초). 넘기면 스탠드로 자동 처리된다.")]
        [Range(3f, 60f)]
        [SerializeField] private float playerDecisionSeconds = RoundSettlement.PlayerDecisionSeconds;

        [Tooltip("뇌물 금액을 정할 시간(초). 넘기면 0(안 냄)으로 자동 제출된다.\n\n" +
                 "A와 B가 동시에 고르는 단계다. 마감이 없으면 한 사람만 손을 놓아도 " +
                 "라운드 전체가 멈춘다.")]
        [Range(3f, 60f)]
        [SerializeField] private float bribeSelectionSeconds = RoundSettlement.BribeSelectionSeconds;

        [Tooltip("판돈을 정할 시간(초). 넘기면 최소 판돈으로 자동 제출된다.")]
        [Range(3f, 60f)]
        [SerializeField] private float betSelectionSeconds = RoundSettlement.BetSelectionSeconds;

        [Tooltip("고발 여부를 정할 시간(초). 넘기면 자동 승복한다.\n\n" +
                 "고를 것이 둘뿐이라 다른 단계만큼 길 이유가 없다 — 오래 끌면 " +
                 "나머지 둘이 빈 화면을 보고 앉아 있다.")]
        [Range(3f, 60f)]
        [SerializeField] private float accusationSeconds = RoundSettlement.AccusationSeconds;

        [Header("제한시간 · 화면이 저절로 넘어갈 때")]
        [Tooltip("결과 공개 화면이 저절로 넘어가기까지의 시간(초).\n\n" +
                 "원래는 전원이 '고발 단계로'를 눌러 넘어가고, 이 값은 아무도 누르지 않을 때만 " +
                 "쓰이는 뒷문이다. 상대 손패가 한 장씩 열리는 데만 3초 넘게 걸리고 그 뒤에야 " +
                 "버튼이 나타나므로 넉넉히 잡는다.")]
        [Range(5f, 60f)]
        [SerializeField] private float resultRevealSeconds = 20f;

        [Tooltip("최종 판정 화면이 걷히기까지의 시간(초).\n\n" +
                 "판정 큐브 연출보다 짧으면 결과를 보기도 전에 화면이 걷힌다. 그래서 " +
                 "연출 길이보다 짧은 값은 실행 중에 자동으로 늘려 잡는다 — 여기에 적은 " +
                 "숫자가 하한이 아니라는 뜻이다.")]
        [Range(5f, 40f)]
        [SerializeField] private float finalJudgmentSeconds = 12f;

        [Tooltip("정산을 읽고 '다음 라운드'를 누르기까지의 시간(초). 넘기면 저절로 넘어간다.")]
        [Range(5f, 120f)]
        [SerializeField] private float roundEndSeconds = 30f;

        [Tooltip("매치 종료 순위표가 걷히기까지의 시간(초). 넘기면 저절로 방 로비로 돌아간다.\n\n" +
                 "순위표를 읽고 정할 만큼은 주되, 자리를 비운 사람 때문에 나머지가 오래 " +
                 "붙잡히지 않을 만큼만 잡는다.")]
        [Range(5f, 120f)]
        [SerializeField] private float matchOverSeconds = 30f;

        [Tooltip("'당신은 …입니다' 역할 공개 연출이 떠 있는 시간(초).")]
        [Range(1f, 15f)]
        [SerializeField] private float roleAssignmentSeconds = 2f;

        // 아래는 위 값을 읽는 창구다. 예전에는 public const였고 화면 쪽에서
        // FusionGameState.XxxLimitSeconds처럼 클래스 이름으로 읽었다. 인스펙터 값이 된 뒤로는
        // 인스턴스를 지나야 하므로 프로퍼티로 바꿨다.

        /// <summary>딜러 카드 선택 제한시간 전체 길이(초).</summary>
        public float DealerDecisionLimitSeconds => dealerDecisionSeconds;

        /// <summary>플레이어 결정 제한시간 전체 길이(초).</summary>
        public float PlayerDecisionLimitSeconds => playerDecisionSeconds;

        /// <summary>뇌물 제한시간 전체 길이(초).</summary>
        public float BribeSelectionLimitSeconds => bribeSelectionSeconds;

        /// <summary>판돈 제한시간 전체 길이(초).</summary>
        public float BetSelectionLimitSeconds => betSelectionSeconds;

        private int AccusationDurationTicks => TicksFromSeconds(accusationSeconds);
        private int ResultRevealDurationTicks => TicksFromSeconds(resultRevealSeconds);
        private int RoleAssignmentDurationTicks => TicksFromSeconds(roleAssignmentSeconds);

        /// <summary>
        /// 최종 판정 화면을 얼마나 띄워 둘지(틱).
        ///
        /// <b>연출보다 짧아질 수 없다.</b> 이 마감이 지나면 호스트가 라운드 종료로 넘겨
        /// 판정 화면을 걷어 가므로, 큐브가 아직 구르는 중에 걷히면 결과를 아무도 못 본다.
        /// 연출 길이는 구르는 소리에 맞춰져 있어(<see cref="JudgmentSuspense"/>) 소리를
        /// 갈아 끼우면 함께 움직인다. 그래서 인스펙터 값을 그대로 믿지 않고 여기서 바닥을 깐다 —
        /// 예전에는 주석으로 "소리를 바꾸면 이 값도 올릴 것"이라 적어 두었을 뿐이었다.
        /// </summary>
        private int FinalJudgmentDurationTicks =>
            TicksFromSeconds(Mathf.Max(finalJudgmentSeconds, MinFinalJudgmentSeconds));

        /// <summary>
        /// 최종 판정 화면이 최소한 떠 있어야 하는 시간(초).
        /// 큐브가 서기까지(= 구르는 소리 길이) + 결과 문구가 뜨는 뜸 + 읽을 시간.
        /// 소리가 없으면 SpinSeconds가 0이라 읽을 시간만 남는다.
        /// </summary>
        private static float MinFinalJudgmentSeconds => GameAudio.SpinSeconds + 2.5f;

        // ── Networked 공개 상태 ──────────────────────────────────────
        [Networked] public int ConnectedPlayerCount { get; private set; }
        [Networked] public GamePhase CurrentPhase { get; private set; }
        [Networked] public int RoundIndex { get; private set; }
        [Networked] public int RoleAssignmentVersion { get; private set; }
        /// <summary>
        /// 마지막으로 호스트가 승인한 공개 행동. Version만 변화를 감지하는 용도이며,
        /// 금액이나 카드 후보 번호 같은 비밀 정보는 복제하지 않는다.
        /// </summary>
        [Networked] public PlayerRole LastApprovedActionActor { get; private set; }
        [Networked] public PlayerActionType LastApprovedActionType { get; private set; }
        [Networked] public int ActionAnnouncementVersion { get; private set; }
        [Networked] public int ReadyMask { get; private set; }

        /// <summary>
        /// 라운드가 끝난 뒤 "다음 라운드"를 누른 자리들(슬롯 비트마스크).
        /// 로비의 <see cref="ReadyMask"/>와 따로 두는 이유는 둘의 수명이 다르기 때문이다 —
        /// 로비 준비는 매치가 시작되면 의미가 없어지지만 이쪽은 라운드마다 다시 모은다.
        /// 한 마스크를 돌려 쓰면 로비 화면이 라운드 준비 상태를 비추게 된다.
        /// </summary>
        [Networked] public int NextRoundReadyMask { get; private set; }

        /// <summary>
        /// 결과 공개에서 "고발 단계로"를 누른 좌석 비트마스크.
        /// <see cref="NextRoundReadyMask"/>와 따로 두는 이유는 수명이 달라서다 —
        /// 한 라운드 안에서 이 게이트가 먼저 열리고 그다음에 저 게이트가 열린다.
        /// </summary>
        [Networked] public int ResultReadyMask { get; private set; }

        /// <summary>
        /// 매치 종료 화면에서 "다시 플레이"를 누른 좌석 비트마스크.
        /// 전원이 누르면 방 로비로 되돌아가 캐릭터부터 다시 고른다.
        /// </summary>
        [Networked] public int RematchReadyMask { get; private set; }

        /// <summary>매치 종료 화면의 마감 틱. 0이면 아직 화면이 뜨지 않았거나 이미 끝났다.</summary>
        [Networked] private int MatchOverEndTick { get; set; }

        /// <summary>라운드 종료(다음 라운드 준비)의 마감 틱. 매치가 끝났으면 걸지 않는다.</summary>
        [Networked] private int RoundEndEndTick { get; set; }

        /// <summary>결과 공개(고발 단계로) 제한시간 전체 길이(초). 화면 시계가 눈금을 잡는 데 쓴다.</summary>
        public float ResultRevealLimitSeconds => TicksToSeconds(ResultRevealDurationTicks);

        /// <summary>결과 공개 마감까지 남은 초. 마감이 없으면 0.</summary>
        public float ResultRevealSecondsLeft =>
            ResultRevealEndTick == 0 ? 0f : SecondsUntil(ResultRevealEndTick);

        /// <summary>무승부 결과 마감까지 남은 초. 마감이 없으면 0.</summary>
        public float TieRedealSecondsLeft =>
            TieRedealEndTick == 0 ? 0f : SecondsUntil(TieRedealEndTick);
        // ── 좌석 상태(슬롯 1~3) ──────────────────────────────────────
        //
        // 자리마다 한 벌씩 두던 스물네 개의 [Networked] 프로퍼티를 여덟 개의
        // NetworkArray로 합쳤다. 예전에는 PlayerOneId/PlayerTwoId/PlayerThreeId 처럼
        // 자리 번호가 이름에 박혀 있어서, 인원을 넷으로 늘리려면 프로퍼티 여덟 개와
        // 그것을 갈라 쓰는 삼항 사슬 열다섯 개를 함께 고쳐야 했다. 하나를 빠뜨려도
        // 컴파일은 통과했고, 어긋난 사실은 네 번째 사람이 들어와야 드러났다.
        //
        // 번호 규칙: 바깥에서 슬롯은 1~3이고 0은 "자리 없음"이다(GetSlotForPlayerId가
        // 못 찾으면 0을 준다). 배열은 0부터 세므로 -1 변환은 아래 접근자 안에서만 한다.

        [Networked, Capacity(MaxPlayers)] private NetworkArray<int> PlayerIds { get; }
        [Networked, Capacity(MaxPlayers)] private NetworkArray<PlayerRole> SlotRoles { get; }

        /// <summary>슬롯별 잔액. 좌석(사람)에 귀속되며 역할 교대와 무관하게 유지된다.</summary>
        [Networked, Capacity(MaxPlayers)] private NetworkArray<int> Balances { get; }
        [Networked] public MatchOutcome PublicOutcome { get; private set; }
        [Networked] public int RevealedWinnerPlayerId { get; private set; }
        [Networked] public NetworkBool CanAccuse { get; private set; }
        [Networked] public FinalWinner VisibleFinalWinner { get; private set; }
        [Networked] public float DealerDecisionElapsedTime { get; private set; }
        [Networked] public float PlayerDecisionElapsedTime { get; private set; }
        /// <summary>파산자가 나와 매치가 끝났는지. true면 다음 라운드로 진행하지 않는다.</summary>
        [Networked] public bool MatchOver { get; private set; }

        /// <summary>
        /// 항복으로 매치가 끝났다면 접은 사람의 배역. <see cref="PlayerRole.None"/>이면
        /// 항복이 아닌 사유(파산·최대 라운드)로 끝났거나 아직 끝나지 않은 것이다.
        ///
        /// <b>MatchOver와 따로 두는 이유.</b> 화면이 갈라지는 지점이 다르다 — 항복은 단계와
        /// 무관하게 그 자리에서 순위표로 넘어가야 하는데, MatchOver만 보면 뇌물 단계에서
        /// 끝난 판도 최종 판정 화면을 한 번 거치게 된다. 사유를 적는 데에도 쓴다.
        /// </summary>
        [Networked] public PlayerRole SurrenderedRole { get; private set; }

        /// <summary>
        /// 뇌물·판돈 마감 틱. 두 사람이 같은 마감을 보므로 사람마다 나누지 않는다.
        /// 0이면 그 단계가 아니거나 이미 끝났다는 뜻이라 시계가 서지 않는다.
        /// 경과 시간이 아니라 마감 틱을 복제하는 이유는 고발과 같다 — 매 틱 쓰지 않아도 된다.
        /// </summary>
        [Networked] public int BribeSelectionEndTick { get; private set; }
        [Networked] public int BetSelectionEndTick { get; private set; }

        [Networked] private int ResultRevealEndTick { get; set; }
        [Networked] private int FinalJudgmentEndTick { get; set; }
        [Networked] private int AccusationEndTick { get; set; }
        [Networked] private int RoleAssignmentEndTick { get; set; }

        // ── Networked 카드/턴 공개 상태 (0 = 없음, 1~52 = EncodeCard 값) ──
        [Networked, Capacity(MaxHandCards)] public NetworkArray<byte> HandACards => default;
        [Networked, Capacity(MaxHandCards)] public NetworkArray<byte> HandBCards => default;
        [Networked] public NetworkBool PlayerAStoodNet { get; private set; }
        [Networked] public NetworkBool PlayerBStoodNet { get; private set; }
        [Networked] public PlayerRole DecisionTurn { get; private set; }
        /// <summary>다이로 물러난 플레이어. None이면 아무도 다이하지 않았다.</summary>
        [Networked] public PlayerRole FoldedRole { get; private set; }

        /// <summary>
        /// 더블다운을 건 자리. 둘이 각자 한 번씩 걸 수 있어 자리마다 따로 둔다.
        /// </summary>
        [Networked] public NetworkBool DoubledA { get; private set; }
        [Networked] public NetworkBool DoubledB { get; private set; }

        /// <summary>이 자리가 이번 라운드에 더블다운을 걸었는지.</summary>
        public bool HasDoubled(PlayerRole role) =>
            role == PlayerRole.PlayerA ? (bool)DoubledA
            : role == PlayerRole.PlayerB && (bool)DoubledB;

        // ── 좌석별 캐릭터(동물) ──────────────────────────────────────
        // 역할이 아니라 좌석에 귀속된다. 매치 시작 시 한 번 정해지고 그 뒤로 바뀌지 않으므로
        // 역할이 3라운드마다 교대돼도 사람과 캐릭터는 함께 움직인다.
        /// <summary>
        /// 자리에 앉은 사람이 지은 이름. 캐릭터와 마찬가지로 <b>사람에게 붙어 있어</b>
        /// 역할이 교대돼도 따라가지 않는다.
        ///
        /// 열여섯 칸을 잡는다 — 이름 상한은 열 글자(<see cref="PlayerNames.MaxLength"/>)이고,
        /// 나머지는 여유다. 호스트가 받을 때 다시 다듬으므로 넘치는 이름은 들어오지 않는다.
        /// </summary>
        [Networked, Capacity(MaxPlayers)] private NetworkArray<NetworkString<_16>> SlotNames { get; }

        /// <summary>내 이름을 방에 다시 알려 볼 수 있는 시각(초).</summary>
        private float nextNicknamePush;

        /// <summary>
        /// 이름을 다시 보내기까지 기다리는 시간(초).
        ///
        /// 보내고 나면 <b>돌아온 값이 내 것과 같아질 때까지</b> 다시 보낸다. 방금 앉아서
        /// 호스트가 아직 자리를 모르는 순간이 있기 때문인데, 그동안 매 프레임 보내면
        /// 초당 예순 번이 나간다. 사람이 이름을 고치는 일은 드물어 이 정도면 넉넉하다.
        /// </summary>
        private const float NicknamePushInterval = 0.5f;

        [Networked, Capacity(MaxPlayers)] private NetworkArray<CharacterId> SlotCharacters { get; }

        /// <summary>
        /// 무승부 결과 화면의 마감 틱. 전원이 '카드 다시 받기'를 누르지 않아도 여기서
        /// 넘어간다 — 결과 공개와 같은 게이트의 뒷문이다(<see cref="ResultRevealEndTick"/>).
        /// </summary>
        [Networked] public int TieRedealEndTick { get; private set; }

        /// <summary>
        /// 이번 라운드 판돈 S(두 베팅의 최소 매칭 결과). 정산이 쓰는 값이다. 0이면 미확정.
        /// </summary>
        [Networked] public int Stake { get; private set; }

        /// <summary>
        /// 좌석별로 실제로 건 금액. 테이블 위 칩 표시용이며, 두 베팅이 모두 들어온
        /// 뒤에 한꺼번에 열린다(먼저 낸 쪽 금액이 상대에게 새지 않도록).
        /// 정산 대상은 <see cref="Stake"/>이므로 이 값은 표시 전용이다.
        /// </summary>
        [Networked] public int BetA { get; private set; }
        [Networked] public int BetB { get; private set; }

        /// <summary>
        /// 테이블 위 캐릭터 위치(캔버스 좌표). 슬롯 기준이라 역할이 바뀌어도 좌석을 따라간다.
        /// 클라이언트가 <see cref="Rpc_ReportAvatarPosition"/>으로 올리면 호스트가
        /// 이동 가능 범위로 잘라서 공개한다.
        /// </summary>
        [Networked, Capacity(MaxPlayers)] private NetworkArray<Vector2> AvatarPositions { get; }
        [Networked, Capacity(MaxPlayers)] private NetworkArray<int> EmotionSequences { get; }
        [Networked, Capacity(MaxPlayers)] private NetworkArray<int> EmotionReadyTicks { get; }
        [Networked] public NetworkBool BribeSubmittedA { get; private set; }
        [Networked] public NetworkBool BribeSubmittedB { get; private set; }
        [Networked] public NetworkBool BetSubmittedA { get; private set; }
        [Networked] public NetworkBool BetSubmittedB { get; private set; }

        // ── Host 전용 비밀 상태 (절대 Networked로 선언하지 않는다) ──
        private readonly Dictionary<int, int> secretBribes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> secretBets = new Dictionary<int, int>();
        private readonly HashSet<int> bribeSubmitters = new HashSet<int>();
        private readonly HashSet<int> betSubmitters = new HashSet<int>();
        private readonly HashSet<int> accusationSubmitters = new HashSet<int>();
        private readonly RoundEngine roundEngine = new RoundEngine();
        private RoundContext roundContext;
        private bool tieRedealInProgress;

        public static FusionGameState LocalInstance { get; private set; }
        public event Action<PlayerActionRequest> HostActionAccepted;
        public event Action<FinalJudgmentData> HostFinalJudgmentResolved;
        public event Action<int, EmotionId, int> EmotionBroadcastReceived;

        // ── 로컬 수신 캐시 (targeted RPC로 받은 비공개 정보) ─────────
        // Dealer 클라이언트에서만 채워진다. Version이 바뀌면 UI가 갱신한다.
        public IReadOnlyList<BlackjackCard> LocalDealerCandidates => localDealerCandidates;
        private readonly List<BlackjackCard> localDealerCandidates = new List<BlackjackCard>();
        public PlayerRole LocalDealerTargetRole { get; private set; }
        public BlackjackDealStep LocalDealerDealStep { get; private set; }
        public int LocalDealerBribeA { get; private set; }
        public int LocalDealerBribeB { get; private set; }
        public int LocalDealerCandidateVersion { get; private set; }

        // 최종 판정 브로드캐스트 수신 캐시 (모든 피어)
        public FinalWinner LastFinalWinner { get; private set; }
        public string LastFinalReason { get; private set; } = string.Empty;
        public int LastPlayerADelta { get; private set; }
        public int LastPlayerBDelta { get; private set; }
        public int LastDealerDelta { get; private set; }
        public bool LastRoundWasManipulated { get; private set; }

        /// <summary>
        /// 겉보기 패자가 고발했는지 승복했는지. 판정이 끝난 뒤에만 값이 있다.
        ///
        /// 고발 단계가 도는 <b>동안</b>에는 아무에게도 알리지 않는다 — 누가 무엇을 고를지
        /// 남이 미리 알면 그 단계가 성립하지 않는다. 판정 브로드캐스트에 실어 보내는 것은
        /// 이미 결과가 공개된 뒤라서다(조작 여부와 같은 취급).
        /// </summary>
        public AccusationChoice LastAccusationChoice { get; private set; }

        /// <summary>고발이 맞았는지. 승복했으면 <see cref="AccusationResult.None"/>.</summary>
        public AccusationResult LastAccusationResult { get; private set; }

        /// <summary>
        /// 지난 라운드에 플레이어 A가 딜러에게 낸 뇌물. 판정이 끝난 뒤에만 값이 있다.
        ///
        /// <b>단계가 도는 동안에는 여전히 비밀이다</b> — <see cref="secretBribes"/>는 호스트에만
        /// 있고 딜러에게만 따로 보낸다. 여기로 오는 것은 라운드가 끝나고 결과가 이미 공개된
        /// 뒤의 값이라, 기록 화면에 남겨도 진행 중인 판을 들여다볼 수는 없다.
        /// </summary>
        public int LastBribeA { get; private set; }

        /// <summary>지난 라운드에 플레이어 B가 낸 뇌물. <see cref="LastBribeA"/>와 같은 취급.</summary>
        public int LastBribeB { get; private set; }

        /// <summary>A가 낸 뇌물 중 딜러가 실제로 챙긴 몫. 나머지는 반납된 것이다.</summary>
        public int LastBribeKeptA { get; private set; }

        /// <summary>B가 낸 뇌물 중 딜러가 실제로 챙긴 몫.</summary>
        public int LastBribeKeptB { get; private set; }

        public int FinalJudgmentVersion { get; private set; }

        /// <summary>
        /// 위의 판정 결과가 <b>몇 번째 라운드의 것인지</b>. 아직 아무 판정도 받지 못했으면 -1.
        ///
        /// <b>왜 필요한가.</b> 위 값들은 RPC로 오는데 <see cref="CurrentPhase"/>는 상태 복제로
        /// 온다. 둘은 다른 통로라 단계가 먼저 도착할 수 있고, 그 사이 위 값들은 <b>지난 라운드</b>
        /// 것이다. 그 상태로 화면을 그리면 지난 라운드의 승자가 잠깐 비치고, 더 나쁘게는
        /// <see cref="FinalJudgmentVersion"/>이 이미 틀어 본 번호와 같아서 연출이 "이미 본
        /// 판정"으로 착각한다 — 주사위가 구르기도 전에 결과 뒤처리가 실행됐다.
        ///
        /// 라운드 번호를 함께 실어 보내면 받는 쪽이 "이건 지금 라운드의 판정인가"를 확실히 안다.
        /// </summary>
        public int LastFinalJudgmentRound { get; private set; } = -1;

        public override void Spawned()
        {
            LocalInstance = this;

            // 채팅은 방 로비에서도 게임 씬에서도 이 하나를 지난다. 이 오브젝트가
            // DontDestroyOnLoad라 씬이 바뀌어도 등록이 그대로 살아 있다 —
            // 화면 쪽에서 씬마다 다시 이어 줄 것이 없다.
            ChatRelay.SetHost(this);

            // 명패와 채팅이 "이 자리 사람 이름"을 물어 올 창구. 호스트든 아니든 등록한다 —
            // 이름은 모두가 보는 것이고, 여기 담긴 값은 이미 모두에게 복제돼 있다.
            PlayerNames.SetBoard(this);

            if (!Object.HasStateAuthority) return;

            CurrentPhase = GamePhase.WaitingForPlayers;
            foreach (var player in Runner.ActivePlayers)
                HostRegisterPlayer(player);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ChatRelay.ClearHost(this);
            PlayerNames.ClearBoard(this);

            // 방을 나가면 그 방에서 나눈 이야기도 끝난다. 남겨 두면 다음에 들어간 방의
            // 채팅창이 낯선 사람들의 지난 대화로 시작한다.
            ChatLog.Clear();

            // 세션 종료 시 LocalInstance를 초기화해서 다음 세션에서 오래된 참조가 재사용되지 않도록 함
            if (LocalInstance == this)
                LocalInstance = null;
        }

        /// <summary>
        /// 그림을 그리는 짬에 내 이름이 방에 제대로 걸려 있는지만 살핀다.
        ///
        /// <b>왜 한 번 보내고 끝내지 않는가.</b> 이름이 방에 걸리려면 세 가지가 다 맞아야
        /// 한다 — 내가 자리를 받았고, 호스트가 그 자리를 알고, 내가 이름을 지었을 것.
        /// 이 셋은 순서가 정해져 있지 않다(늦게 들어오기도 하고, 게임 도중에 이름을
        /// 바꾸기도 한다). 그래서 "다르면 보낸다" 하나로 셋을 모두 덮는다.
        /// </summary>
        public override void Render()
        {
            PushLocalNicknameIfNeeded();
        }

        private void PushLocalNicknameIfNeeded()
        {
            if (Runner == null || !Runner.IsRunning) return;

            int slot = GetSlotForPlayerId(Runner.LocalPlayer.PlayerId);
            if (slot == 0) return;   // 아직 자리를 받지 못했다

            string mine = PlayerNames.Sanitize(GameSettings.Nickname);
            if (GetNameForSlot(slot) == mine) return;

            if (Time.unscaledTime < nextNicknamePush) return;
            nextNicknamePush = Time.unscaledTime + NicknamePushInterval;
            Rpc_SetNickname(mine);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (CurrentPhase == GamePhase.RoleAssignment && Runner.Tick >= RoleAssignmentEndTick)
                HostBeginBribeSelection();

            // 뇌물·판돈 마감. 아직 내지 않은 사람 몫을 기본값으로 채우고 그대로 진행한다.
            // 마감 틱을 0으로 닫는 것은 각 처리 안에서 가장 먼저 한다 — 그러지 않으면
            // 다음 단계로 넘어가기 전에 이 검사가 한 번 더 걸린다.
            if (CurrentPhase == GamePhase.BribeSelection && BribeSelectionEndTick != 0
                && Runner.Tick >= BribeSelectionEndTick + TicksFromSeconds(SubmitGraceSeconds))
                HostApplyBribeTimeout();

            if (CurrentPhase == GamePhase.BetSelection && BetSelectionEndTick != 0
                && Runner.Tick >= BetSelectionEndTick + TicksFromSeconds(SubmitGraceSeconds))
                HostApplyBetTimeout();

            // Dealer 결정 타임아웃 — 기본 카드(index 0) 자동 적용
            if (CurrentPhase == GamePhase.DealerCardDistribution)
            {
                DealerDecisionElapsedTime += Runner.DeltaTime;
                if (DealerDecisionElapsedTime >= DealerDecisionLimitSeconds)
                    HostApplyDefaultDealerCardChoice();
            }

            if (CurrentPhase == GamePhase.PlayerDecision)
            {
                PlayerDecisionElapsedTime += Runner.DeltaTime;
                if (PlayerDecisionElapsedTime >= PlayerDecisionLimitSeconds)
                    HostApplyTimedOutPlayerStand();
            }

            // 무승부 결과는 전원이 눌러야 넘어간다(Rpc_RequestTieRedeal). 이 시계는 그
            // 게이트의 뒷문일 뿐이다 — 한 명이 자리를 비우면 나머지가 영원히 갇힌다.
            if (CurrentPhase == GamePhase.TieRedeal && TieRedealEndTick != 0
                && Runner.Tick >= TieRedealEndTick)
                HostStartTieRedeal();

            // 결과 공개는 전원이 눌러야 넘어간다(Rpc_RequestAccusation). 이 시계는 그
            // 게이트의 뒷문일 뿐이다 — 한 명이 자리를 비우면 나머지가 영원히 갇힌다.
            if (CurrentPhase == GamePhase.ResultReveal && ResultRevealEndTick != 0
                && Runner.Tick >= ResultRevealEndTick)
                HostBeginAccusation();

            // 고발 제한 시간 초과 → 자동 승복 처리
            if (CurrentPhase == GamePhase.Accusation && CanAccuse && Runner.Tick >= AccusationEndTick)
                HostResolveAccusation(AccusationChoice.AcceptResult, FindLosingPlayerId());

            if (CurrentPhase == GamePhase.FinalJudgment && Runner.Tick >= FinalJudgmentEndTick)
                CurrentPhase = GamePhase.RoundEnd;

            // 매치 종료 화면이 실제로 올라온 뒤부터 잰다. 파산이 확정되는 순간부터 재면
            // 두구두구 연출에 걸리는 시간을 순위표를 보기도 전에 까먹는다.
            if (MatchOver && CurrentPhase == GamePhase.RoundEnd && MatchOverEndTick == 0)
                MatchOverEndTick = Runner.Tick + TicksFromSeconds(MatchOverLimitSeconds);

            if (MatchOverEndTick != 0 && Runner.Tick >= MatchOverEndTick)
                HostApplyMatchOverTimeout();

            // 다음 라운드 준비도 전원이 눌러야 넘어간다. 매치가 끝났으면 저 위의 마감이
            // 맡으므로 여기서는 걸지 않는다 — 두 시계가 같은 화면에서 겹치면 안 된다.
            if (!MatchOver && CurrentPhase == GamePhase.RoundEnd && RoundEndEndTick == 0)
                RoundEndEndTick = Runner.Tick + TicksFromSeconds(RoundEndLimitSeconds);

            if (RoundEndEndTick != 0 && Runner.Tick >= RoundEndEndTick)
                HostStartNextRound();
        }

        /// <summary>
        /// 매치 종료 마감. 다시 플레이를 누르지 않은 사람을 <b>방에서 내보내고</b>,
        /// 남은 사람은 방 로비로 되돌린다.
        ///
        /// 다시 플레이에 마감을 둔 이유: 이 게이트에는 취소가 없어 한 명이 자리를 비우면
        /// 나머지는 영영 못 넘어간다. 옆에 나가기가 있으니 갇히지는 않지만, 그건 판을
        /// 이어 가고 싶은 사람이 대신 나가는 셈이라 순서가 거꾸로다.
        ///
        /// 호스트는 내보내지 않는다 — 호스트가 나가면 방 자체가 사라져 준비를 마친
        /// 사람들까지 메인 메뉴로 떨어진다. 호스트가 자리를 비우면 나머지는 방 로비에서
        /// 기다리게 되고, 거기서 각자 나가기로 빠질 수 있다.
        /// </summary>
        private void HostApplyMatchOverTimeout()
        {
            MatchOverEndTick = 0;

            int hostId = Runner.LocalPlayer.PlayerId;
            for (int slot = 1; slot <= MaxPlayers; slot++)
            {
                int pid = GetPlayerIdForSlot(slot);
                if (pid == 0 || pid == hostId) continue;
                if ((RematchReadyMask & (1 << (slot - 1))) != 0) continue;

                HostDisconnect(pid);
            }

            // 내보낸 좌석이 있으면 HostUnregisterPlayer가 이미 여기로 되돌렸겠지만,
            // 아무도 내보내지 않은 경우(호스트만 안 눌렀을 때)에도 판은 접어야 한다.
            RematchReadyMask = 0;
            ResetRoundToWaitingForPlayers();
            Debug.Log("[FusionGameState] 매치 종료 제한시간이 지나 방 로비로 돌아갑니다.");
        }

        private void HostDisconnect(int playerId)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (player.PlayerId != playerId) continue;
                Debug.Log($"[FusionGameState] 준비하지 않아 내보냅니다: {playerId}");
                Runner.Disconnect(player);
                return;
            }
        }

        // ── 플레이어 등록/해제 ───────────────────────────────────────

        public void HostRegisterPlayer(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            if (GetSlotForPlayerId(player.PlayerId) != 0) return;
            if (ConnectedPlayerCount >= MaxPlayers)
            {
                Debug.LogWarning($"[FusionGameState] Rejected {player.PlayerId}: room is full.");
                return;
            }

            for (int slot = 1; slot <= MaxPlayers; slot++)
            {
                if (GetPlayerIdForSlot(slot) != 0) continue;
                SetPlayerIdForSlot(slot, player.PlayerId);
                ConnectedPlayerCount++;
                return;
            }

            // 위 인원수 검사를 통과했는데 빈자리가 없다면 셈과 자리가 어긋난 것이다.
            // 예전에는 이때 조용히 3번 자리를 덮어써서 앉아 있던 사람이 사라졌다.
            Debug.LogWarning($"[FusionGameState] {player.PlayerId}를 앉힐 빈자리가 없습니다 " +
                             $"(ConnectedPlayerCount={ConnectedPlayerCount}).");
        }

        public void HostUnregisterPlayer(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            var slot = GetSlotForPlayerId(player.PlayerId);
            if (slot == 0) return;

            ClearSeat(slot);
            ResetRoundToWaitingForPlayers();
        }

        /// <summary>
        /// 접속이 끊긴 좌석을 비운다. 비운 자리가 하나라도 있으면 true.
        ///
        /// 게임 씬에는 <c>OnPlayerLeft</c>를 받는 곳이 없다 — 로비 매니저는 로비 씬에
        /// 있고, 게임 씬의 디렉터는 러너 콜백을 구현하지 않는다. 그래서 호스트가
        /// 사람 수를 세다가 모자라면 이걸 불러 직접 정리한다.
        ///
        /// <b>방은 접지 않는다.</b> 예전에는 한 명이 나가면 세션을 통째로 내려서
        /// 남은 사람까지 메인 메뉴로 떨어졌다. 지금은 그 자리만 비우고 단계를 대기로
        /// 되돌리며, 남은 사람은 같은 방의 로비로 돌아가 다시 채워지기를 기다린다.
        /// </summary>
        public bool HostReleaseMissingSeats()
        {
            if (!Object.HasStateAuthority || Runner == null) return false;

            bool released = false;
            for (int slot = 1; slot <= MaxPlayers; slot++)
            {
                int playerId = GetPlayerIdForSlot(slot);
                if (playerId == 0 || IsStillConnected(playerId)) continue;

                Debug.Log($"[FusionGameState] 접속이 끊긴 좌석을 비웁니다: 슬롯 {slot}(플레이어 {playerId}).");
                ClearSeat(slot);
                released = true;
            }

            // 되돌리기는 마지막에 한 번만. 좌석마다 부르면 두 번째 호출은 이미 대기 단계라
            // 역할·캐릭터를 비우는 가지를 타지 않는다(ResetRoundToWaitingForPlayers 참고).
            if (released) ResetRoundToWaitingForPlayers();
            return released;
        }

        private bool IsStillConnected(int playerId)
        {
            foreach (var player in Runner.ActivePlayers)
                if (player.PlayerId == playerId) return true;
            return false;
        }

        private void ClearSeat(int slot)
        {
            SetPlayerIdForSlot(slot, 0);
            SetRoleForSlot(slot, PlayerRole.None);
            SetCharacterForSlot(slot, CharacterId.None);
            SetNameForSlot(slot, string.Empty);
            ConnectedPlayerCount = Mathf.Max(0, ConnectedPlayerCount - 1);
        }

        // ── RPC ─────────────────────────────────────────────────────

        /// <summary>
        /// 로비에서 캐릭터(=자리)를 고른다. 동물과 역할은 1:1이므로
        /// (<see cref="CharacterIdentity"/>) 고르는 순간 1라운드 배역까지 정해진다.
        ///
        /// 이미 남이 앉은 자리는 <b>뺏지 못하고 거절</b>한다. 로비에서 캐릭터가 테이블 위에
        /// 서 있는 지금은, 뺏기는 쪽이 아무 조작도 하지 않았는데 캐릭터가 사라진다.
        /// 준비를 마친 뒤에도 바꿀 수 없다 — 준비를 취소해야 다시 고를 수 있다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SelectRole(PlayerRole role, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.WaitingForPlayers) return;

            var src = ResolveSource(info);
            var slot = GetSlotForPlayerId(src.PlayerId);
            if (slot == 0) { LogRejected("SelectRole", src, "Unknown slot."); return; }
            if (IsSlotReady(slot)) { LogRejected("SelectRole", src, "Already ready."); return; }

            // None은 "자리를 비운다"는 뜻이다. 고른 카드를 다시 누른 경우로,
            // 자리에서 일어날 뿐 방에서 나가는 것은 아니므로 등록은 그대로 둔다.
            if (role == PlayerRole.None)
            {
                SetRoleForSlot(slot, PlayerRole.None);
                SetCharacterForSlot(slot, CharacterId.None);
                NotifyAccepted(src, PlayerActionType.SelectRole);
                Debug.Log($"[FusionGameState] Player {src.PlayerId} left the seat (slot {slot})");
                return;
            }

            for (int i = 1; i <= 3; i++)
            {
                if (i == slot || GetRoleForSlot(i) != role) continue;
                LogRejected("SelectRole", src, $"Role {role} is taken by slot {i}.");
                return;
            }

            SetSlotSeat(slot, role);
            NotifyAccepted(src, PlayerActionType.SelectRole);
            Debug.Log($"[FusionGameState] Player {src.PlayerId} → role {role} (slot {slot})");
        }

        /// <summary>
        /// 자리를 배정하고 그 자리의 캐릭터·시작 위치까지 한꺼번에 맞춘다.
        /// 셋을 따로 두면 로비에서 캐릭터만 정해지고 위치가 (0,0)에 남아
        /// 남의 화면에서 테이블 한가운데에 서 있게 된다.
        /// </summary>
        private void SetSlotSeat(int slot, PlayerRole role)
        {
            SetRoleForSlot(slot, role);
            SetCharacterForSlot(slot, CharacterIdentity.FromInitialRole(role));
            SetAvatarPosForSlot(slot, AvatarMovement.SpawnPositionFor(role));
        }

        /// <summary>
        /// 캐릭터 위치 보고. 이동은 클라이언트가 먼저 그리고(예측) 호스트가 뒤따라 공개한다.
        /// 승패에 영향이 없는 연출용 상태라 호스트가 되돌리지 않고, 대신 좌표를 이동 범위로
        /// 잘라서 화면 밖으로 나가는 것만 막는다. 거절해도 로그를 남기지 않는다 —
        /// 초당 20회 올라오므로 콘솔이 묻힌다.
        ///
        /// 채널이 Unreliable인 이유: 위치는 50ms 뒤 값이 앞 값을 완전히 덮어쓴다.
        /// 기본값인 Reliable로 두면 잃어버린 패킷을 재전송하느라 뒤따르는 최신 위치까지
        /// 밀리고(head-of-line blocking), 정작 그 재전송 값은 도착하자마자 쓸모가 없다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
        public void Rpc_ReportAvatarPosition(Vector2 position, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            var src = ResolveSource(info);
            int slot = GetSlotForPlayerId(src.PlayerId);
            if (slot == 0) return;

            SetAvatarPosForSlot(slot, AvatarMovement.Clamp(position));
        }

        /// <summary>클라이언트가 선택한 감정을 State Authority에 승인 요청한다.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestEmotion(int emotionId, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            var src = ResolveSource(info);
            if (Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid)
            {
                LogRejected("Emotion", src, "Network session is not running.");
                return;
            }

            int slot = GetSlotForPlayerId(src.PlayerId);
            if (slot == 0 || !IsStillConnected(src.PlayerId))
            {
                LogRejected("Emotion", src, "Player is not connected or seated.");
                return;
            }

            if (emotionId < 0 || emotionId >= EmotionCatalog.WheelSlotCount)
            {
                LogRejected("Emotion", src, $"Invalid emotion id {emotionId}.");
                return;
            }

            int now = (int)Runner.Tick;
            if (now < GetEmotionReadyTick(slot))
            {
                LogRejected("Emotion", src, "Cooldown is active.");
                return;
            }

            int sequence = GetEmotionSequence(slot) + 1;
            SetEmotionSequence(slot, sequence);
            SetEmotionReadyTick(slot, now + TicksFromSeconds(EmotionCooldownSeconds));
            Rpc_BroadcastEmotion(slot, emotionId, sequence);
        }

        /// <summary>승인된 슬롯·감정·순번을 모든 클라이언트에 방송한다.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_BroadcastEmotion(int playerSlot, int emotionId, int sequence)
        {
            if (playerSlot < 1 || playerSlot > MaxPlayers ||
                emotionId < 0 || emotionId >= EmotionCatalog.WheelSlotCount || sequence <= 0)
                return;

            EmotionBroadcastReceived?.Invoke(playerSlot, (EmotionId)emotionId, sequence);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SetReady(bool ready, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.WaitingForPlayers)
            {
                LogRejected("Ready", ResolveSource(info), "Not in WaitingForPlayers phase."); return;
            }
            var src = ResolveSource(info);
            var slot = GetSlotForPlayerId(src.PlayerId);
            if (slot == 0) { LogRejected("Ready", src, "Unknown slot."); return; }

            // 캐릭터를 고르지 않았으면 준비할 수 없다. 이걸 호스트가 막지 않으면
            // 세 명이 모두 준비했는데 자리가 비어 시작 조건이 영원히 성립하지 않는다.
            if (ready && GetRoleForSlot(slot) == PlayerRole.None)
            {
                LogRejected("Ready", src, "No character chosen."); return;
            }

            SetReady(slot, ready);
            NotifyAccepted(src, PlayerActionType.Ready);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestStartGame(RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || src != Runner.LocalPlayer)
            {
                LogRejected("StartGame", src, "Only Host may start."); return;
            }
            if (ConnectedPlayerCount != MaxPlayers || ReadyMask != 0b111)
            {
                LogRejected("StartGame", src, "Needs three ready players."); return;
            }

            HostAssignInitialRoles();
            // 매치 시작 시 세 좌석에 시드머니 지급
            for (int slot = 1; slot <= MaxPlayers; slot++)
                SetBalanceForSlot(slot, RoundSettlement.SeedMoney);
            NotifyAccepted(src, PlayerActionType.RequestStartGame);
            RoundIndex++;
            ResetRoundSecrets();
            HostBeginRoleAssignment();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SubmitBribe(int amount, RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.BribeSelection)
            {
                LogRejected("SubmitBribe", src, "Wrong phase."); return;
            }
            var role = GetRoleForPlayerId(src.PlayerId);
            int balance = GetBalanceForSlot(GetSlotForPlayerId(src.PlayerId));
            if ((role != PlayerRole.PlayerA && role != PlayerRole.PlayerB)
                || amount < 0
                || amount > MaxBribeAmount
                || amount > RoundSettlement.MaxBribeFor(balance))
            {
                LogRejected("SubmitBribe", src, "Invalid role/bribe."); return;
            }
            if (!bribeSubmitters.Add(src.PlayerId))
            {
                LogRejected("SubmitBribe", src, "Already submitted."); return;
            }

            secretBribes[src.PlayerId] = amount;
            if (role == PlayerRole.PlayerA) BribeSubmittedA = true;
            else BribeSubmittedB = true;
            NotifyAccepted(src, PlayerActionType.SubmitBribe, bribeAmount: amount);
            HostSendDealerBribeStatus();

            if (bribeSubmitters.Count == 2)
                HostStartInitialDeal();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SubmitBet(int amount, RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.BetSelection)
            {
                LogRejected("SubmitBet", src, "Wrong phase."); return;
            }

            var role = GetRoleForPlayerId(src.PlayerId);
            int balance = GetBalanceForSlot(GetSlotForPlayerId(src.PlayerId));
            secretBribes.TryGetValue(src.PlayerId, out int bribe);
            if ((role != PlayerRole.PlayerA && role != PlayerRole.PlayerB)
                || amount % 10 != 0
                || !RoundSettlement.IsAffordable(balance, bribe, amount))
            {
                LogRejected("SubmitBet", src, "Invalid role/bet or insufficient balance."); return;
            }
            if (!betSubmitters.Add(src.PlayerId))
            {
                LogRejected("SubmitBet", src, "Already submitted."); return;
            }

            secretBets[src.PlayerId] = amount;
            if (role == PlayerRole.PlayerA) BetSubmittedA = true;
            else BetSubmittedB = true;
            NotifyAccepted(src, PlayerActionType.SubmitBet, betAmount: amount);

            if (betSubmitters.Count == 2)
                HostFinalizeBetsAndStartDecisions();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SubmitDealerCardChoice(int chosenIndex, RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.DealerCardDistribution)
            {
                LogRejected("DealerCardChoice", src, "Wrong phase."); return;
            }
            if (GetRoleForPlayerId(src.PlayerId) != PlayerRole.Dealer)
            {
                LogRejected("DealerCardChoice", src, "Only Dealer."); return;
            }
            if (roundContext?.CurrentCandidateSet == null)
            {
                LogRejected("DealerCardChoice", src, "No active candidate set."); return;
            }
            var candidateSet = roundContext.CurrentCandidateSet;
            if (chosenIndex < 0 || chosenIndex >= candidateSet.Candidates.Count)
            {
                LogRejected("DealerCardChoice", src, "Index out of range."); return;
            }

            var choice = new DealerCardChoice
            {
                TargetPlayerRole = candidateSet.TargetPlayerRole,
                CandidateSetId = candidateSet.CandidateSetId,
                ChosenCandidateIndex = chosenIndex
            };
            HostApplyDealerCardChoice(choice, DealerDecisionElapsedTime);
            NotifyAccepted(src, PlayerActionType.SubmitDealerCardChoice, chosenIndex: chosenIndex);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestHit(RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.PlayerDecision)
            {
                LogRejected("Hit", src, "Wrong phase."); return;
            }
            var role = GetRoleForPlayerId(src.PlayerId);
            if (role != PlayerRole.PlayerA && role != PlayerRole.PlayerB)
            {
                LogRejected("Hit", src, "Only PlayerA or PlayerB."); return;
            }
            if (!IsCurrentDecisionTurn(role))
            {
                LogRejected("Hit", src, "Not your turn."); return;
            }
            var score = roundEngine.GetScore(roundContext, role);
            if (score.IsBust || roundContext.HasStood(role) ||
                roundContext.GetHand(role).Cards.Count >= RoundEngine.MaxCardsFor(roundContext, role))
            {
                LogRejected("Hit", src, "Already bust, stood, or at the hand-size limit."); return;
            }

            NotifyAccepted(src, PlayerActionType.RequestHit);
            HostGenerateHitCandidateSet(role);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestStand(RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.PlayerDecision)
            {
                LogRejected("Stand", src, "Wrong phase."); return;
            }
            var role = GetRoleForPlayerId(src.PlayerId);
            if (role != PlayerRole.PlayerA && role != PlayerRole.PlayerB)
            {
                LogRejected("Stand", src, "Only PlayerA or PlayerB."); return;
            }
            if (!IsCurrentDecisionTurn(role))
            {
                LogRejected("Stand", src, "Not your turn."); return;
            }
            if (roundContext.HasStood(role))
            {
                LogRejected("Stand", src, "Already stood."); return;
            }

            roundEngine.ApplyStand(roundContext, role);
            HostSyncRoundState();
            NotifyAccepted(src, PlayerActionType.RequestStand);
            HostAdvancePlayerDecision();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestDie(RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.PlayerDecision)
            {
                LogRejected("Die", src, "Wrong phase."); return;
            }
            var role = GetRoleForPlayerId(src.PlayerId);
            if (role != PlayerRole.PlayerA && role != PlayerRole.PlayerB)
            {
                LogRejected("Die", src, "Only PlayerA or PlayerB."); return;
            }
            if (!IsCurrentDecisionTurn(role))
            {
                LogRejected("Die", src, "Not your turn."); return;
            }
            if (!roundEngine.CanDie(roundContext, role))
            {
                LogRejected("Die", src, "Cannot die after busting or standing."); return;
            }

            roundEngine.ApplyDie(roundContext, role);
            FoldedRole = role;
            HostSyncRoundState();
            NotifyAccepted(src, PlayerActionType.RequestDie);

            // 승부를 건너뛰고 곧바로 결과 공개로 합류한다. 다이한 쪽이 겉보기 패자가 되므로
            // 이후 고발권/자동 승복/최종 판정은 기존 흐름이 그대로 처리한다.
            Debug.Log($"[FusionGameState] {role} 다이. 상대가 겉보기 승자가 됩니다.");
            HostRevealOutcome(RoundEngine.OutcomeAfterDie(role));
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestDoubleDown(RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.PlayerDecision)
            {
                LogRejected("DoubleDown", src, "Wrong phase."); return;
            }
            var role = GetRoleForPlayerId(src.PlayerId);
            if (role != PlayerRole.PlayerA && role != PlayerRole.PlayerB)
            {
                LogRejected("DoubleDown", src, "Only PlayerA or PlayerB."); return;
            }
            if (!IsCurrentDecisionTurn(role))
            {
                LogRejected("DoubleDown", src, "Not your turn."); return;
            }
            if (!roundEngine.CanDoubleDown(roundContext, role))
            {
                LogRejected("DoubleDown", src, "Double down is only allowed once, on your opening two cards."); return;
            }
            if (!HostCanAffordDoubleDown(role))
            {
                LogRejected("DoubleDown", src, "You cannot cover the doubled stake."); return;
            }

            // 더블다운은 선언한 쪽만 자기 판돈을 올린다. 상대에게 물어볼 것이 없으므로
            // 그 자리에서 성립시킨다. 상대는 알림으로만 알게 된다.
            // 판돈을 올리는 것은 이번 라운드에 한 번뿐이다. 둘 다 걸어도 2배에서 멈춘다 —
            // 각자 자기 몫을 2배로 올리면 매칭된 판돈은 어차피 2배가 되고,
            // 두 번 곱하면 4배가 되어 선언 시점의 잔액 검증(2S 기준)이 무의미해진다.
            bool firstThisRound = !roundContext.AnyoneDoubled;
            roundEngine.ApplyDoubleDown(roundContext, role);
            if (role == PlayerRole.PlayerA) DoubledA = true; else DoubledB = true;

            if (firstThisRound) Stake = RoundSettlement.DoubledStake(Stake);

            HostSyncRoundState();
            NotifyAccepted(src, PlayerActionType.RequestDoubleDown);
            Debug.Log($"[FusionGameState] {role} 더블다운. 판돈이 {Stake}(으)로 2배가 되고 카드 1장만 받습니다.");

            // 카드 1장을 받는다. 배분은 기존 히트 경로(딜러 후보 3장 → 선택)를 그대로 탄다.
            // 카드가 들어온 뒤의 자동 Stand는 HostApplyDealerCardChoice가 처리한다.
            HostGenerateHitCandidateSet(role);
        }

        /// <summary>
        /// A·B 두 좌석이 모두 2배 판돈을 감당할 수 있는지. 더블다운은 상대까지 2S에 끌어들이므로
        /// 선언자만 검사하면 상대가 낼 수 없는 판을 강요하게 된다.
        /// </summary>
        /// <summary>
        /// 선언하는 <b>본인</b>이 더블다운 후의 판돈을 감당할 수 있는지.
        ///
        /// 상대는 보지 않는다. 상대가 못 내면 마이너스로 떨어져 파산하고 매치가 끝나는데,
        /// 그것도 더블다운의 결과다(<see cref="RoundSettlement.CanAffordStake"/> 참고).
        ///
        /// 화면의 버튼도 정확히 같은 값을 계산한다. 그래서 버튼이 켜져 있으면 반드시
        /// 통과한다 — 눌렀는데 아무 일도 없는 화면이 나오지 않는다.
        /// </summary>
        private bool HostCanAffordDoubleDown(PlayerRole role)
        {
            int pid = FindPlayerIdByRole(role);
            secretBribes.TryGetValue(pid, out int bribe);

            return RoundSettlement.CanAffordStake(
                BalanceForRole(role), bribe, StakeAfterDoubleDown);
        }

        /// <summary>이번 라운드에 누구든 더블다운을 걸었는지.</summary>
        public bool AnyoneDoubled => (bool)DoubledA || (bool)DoubledB;

        /// <summary>
        /// 지금 더블다운을 선언하면 판돈이 얼마가 되는지. 호스트의 검증과 화면의 버튼이
        /// 이 값 하나를 함께 본다 — 둘이 다른 셈을 하면 버튼과 결과가 어긋난다.
        /// </summary>
        public int StakeAfterDoubleDown =>
            RoundSettlement.StakeAfterDoubleDown(Stake, AnyoneDoubled);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SubmitAccusation(AccusationChoice choice, RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.Accusation || !CanAccuse)
            {
                LogRejected("Accusation", src, "Wrong phase or accusation closed."); return;
            }
            if (choice == AccusationChoice.None || !IsLosingPlayer(src.PlayerId))
            {
                LogRejected("Accusation", src, "Only the revealed loser can accuse."); return;
            }
            if (!accusationSubmitters.Add(src.PlayerId))
            {
                LogRejected("Accusation", src, "Already submitted."); return;
            }

            NotifyAccepted(src, PlayerActionType.SubmitAccusation, accusationChoice: choice);
            HostResolveAccusation(choice, src.PlayerId);
        }

        /// <summary>
        /// 결과를 다 봤으니 고발 단계로 넘어가자고 알린다.
        ///
        /// <see cref="Rpc_RequestNextRound"/>와 같은 규칙이다 — 앉아 있는 <b>모두가</b>
        /// 눌러야 넘어간다. 결과 공개는 상대 손패가 한 장씩 열리는 구간이라 읽는 데
        /// 걸리는 시간이 사람마다 다르고, 특히 진 사람에게는 여기서 본 것이 고발
        /// 여부를 정하는 유일한 근거다. 먼저 본 사람이 넘겨 버리면 그 근거를 뺏는다.
        ///
        /// 두 번 눌러도 안전하다. 취소는 없다 — 물릴 수 있으면 한 명이 계속 물리는
        /// 것만으로 판을 붙잡을 수 있다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestAccusation(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.ResultReveal) return;

            var requester = ResolveSource(info);
            int slot = GetSlotForPlayerId(requester.PlayerId);
            if (slot == 0) { LogRejected("NextPhase", requester, "Unknown slot."); return; }

            int bit = 1 << (slot - 1);
            if ((ResultReadyMask & bit) == 0)
            {
                ResultReadyMask |= bit;
                NotifyAccepted(requester, PlayerActionType.Ready);
            }

            if (!AllSeatsReady(ResultReadyMask)) return;   // 아직 안 누른 사람이 있다
            HostBeginAccusation();
        }

        /// <summary>
        /// 비긴 판을 다 봤으니 카드를 다시 나눠 달라고 알린다.
        ///
        /// <see cref="Rpc_RequestAccusation"/>과 같은 규칙이다 — 앉아 있는 <b>모두가</b>
        /// 눌러야 넘어간다. 왜 비겼는지는 양쪽 손패를 다 보고서야 알 수 있고, 그것을
        /// 읽는 데 걸리는 시간은 사람마다 다르다. 먼저 본 사람이 넘겨 버리면 나머지는
        /// 카드가 사라진 뒤에 "왜 다시 나눠 주지?"만 남는다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestTieRedeal(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.TieRedeal) return;

            var requester = ResolveSource(info);
            int slot = GetSlotForPlayerId(requester.PlayerId);
            if (slot == 0) { LogRejected("TieRedeal", requester, "Unknown slot."); return; }

            int bit = 1 << (slot - 1);
            if ((ResultReadyMask & bit) == 0)
            {
                ResultReadyMask |= bit;
                NotifyAccepted(requester, PlayerActionType.Ready);
            }

            if (!AllSeatsReady(ResultReadyMask)) return;   // 아직 안 누른 사람이 있다
            HostStartTieRedeal();
        }

        /// <summary>
        /// 손패를 지우고 다시 나눈다. 전원 준비와 마감 시계가 같은 출구를 쓰도록 한 곳에 둔다.
        /// 뇌물과 조작 기록은 그대로 남는다 — 같은 라운드가 이어지는 것이다.
        /// </summary>
        private void HostStartTieRedeal()
        {
            TieRedealEndTick = 0;
            ResultReadyMask = 0;

            tieRedealInProgress = true;
            roundEngine.ResetForTieRedeal(roundContext);
            HostSyncRoundState();
            HostGenerateNextInitialCandidate();
        }

        // 고발 단계를 연다. 전원 준비와 마감 시계가 같은 출구를 쓰도록 한 곳에 둔다 —
        // 두 곳에 흩어져 있으면 한쪽만 고쳐 제한시간이 붙지 않는 경로가 생긴다.
        private void HostBeginAccusation()
        {
            ResultRevealEndTick = 0;
            CurrentPhase = GamePhase.Accusation;
            CanAccuse = true;
            AccusationEndTick = Runner.Tick + AccusationDurationTicks;
        }

        /// <summary>
        /// 매치가 끝난 뒤 다시 하자고 알린다. 전원이 누르면 <b>방 로비로 되돌아간다</b> —
        /// 그 자리에서 새 판을 여는 것이 아니라 캐릭터(=역할)부터 다시 고른다.
        ///
        /// 같은 배역으로 계속 도는 것보다 이쪽이 낫다고 본 이유: 매치가 끝났다는 건
        /// 한 사람이 파산했거나 아홉 판을 다 돌았다는 뜻이라, 다음 판은 새 게임에 가깝다.
        /// 사람이 빠지거나 바뀔 수 있는 지점도 여기뿐이다.
        ///
        /// 실제 씬 전환은 호스트의 <see cref="NetworkGameDirector"/>가 맡는다 —
        /// 단계가 <see cref="GamePhase.WaitingForPlayers"/>로 돌아온 것을 보고 움직인다.
        /// 로비가 게임 씬으로 넘어갈 때와 정확히 같은 방식이다(단계를 보고 씬을 연다).
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestRematch(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || !MatchOver) return;

            var requester = ResolveSource(info);
            int slot = GetSlotForPlayerId(requester.PlayerId);
            if (slot == 0) { LogRejected("Rematch", requester, "Unknown slot."); return; }

            int bit = 1 << (slot - 1);
            if ((RematchReadyMask & bit) == 0)
            {
                RematchReadyMask |= bit;
                NotifyAccepted(requester, PlayerActionType.Ready);
            }

            if (!AllSeatsReady(RematchReadyMask)) return;   // 아직 안 누른 사람이 있다

            RematchReadyMask = 0;
            ResetRoundToWaitingForPlayers();
            Debug.Log("[FusionGameState] 전원이 다시 플레이를 눌러 방 로비로 돌아갑니다.");
        }

        /// <summary>매치 종료 화면의 제한시간(초). 인스펙터의 <c>matchOverSeconds</c>.</summary>
        public float MatchOverLimitSeconds => matchOverSeconds;

        /// <summary>매치 종료 마감까지 남은 초. 마감이 없으면 0.</summary>
        public float MatchOverSecondsLeft =>
            MatchOverEndTick == 0 ? 0f : SecondsUntil(MatchOverEndTick);

        /// <summary>라운드 종료(다음 라운드 준비)의 제한시간(초). 인스펙터의 <c>roundEndSeconds</c>.</summary>
        public float RoundEndLimitSeconds => roundEndSeconds;

        /// <summary>다음 라운드 마감까지 남은 초. 마감이 없으면 0.</summary>
        public float RoundEndSecondsLeft =>
            RoundEndEndTick == 0 ? 0f : SecondsUntil(RoundEndEndTick);

        /// <summary>다시 플레이를 누른 사람 수.</summary>
        public int RematchReadyCount => CountReady(RematchReadyMask);

        /// <summary>로컬 플레이어가 다시 플레이를 눌렀는지.</summary>
        public bool IsLocalReadyForRematch => IsLocalReady(RematchReadyMask);

        /// <summary>
        /// 다음 라운드로 넘어갈 준비가 됐다고 알린다.
        ///
        /// 누른 사람 한 명이 곧바로 판을 넘기지 않는다 — 앉아 있는 <b>모두가</b> 눌러야
        /// 다음 라운드가 열린다. 정산 결과와 딜러의 조작 여부를 확인하는 시간이 사람마다
        /// 다른데, 먼저 본 한 사람이 넘겨 버리면 나머지는 자기 돈이 왜 줄었는지 모른 채
        /// 다음 판에 끌려 들어간다.
        ///
        /// 두 번 눌러도 안전하다. 취소는 없다 — 준비를 물릴 수 있으면 한 명이 계속
        /// 물리는 것만으로 판을 영원히 붙잡을 수 있다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestNextRound(RpcInfo info = default)
        {
            // 매치가 끝났으면(파산자 발생) 다음 라운드로 넘어가지 않는다.
            if (!Object.HasStateAuthority || CurrentPhase != GamePhase.RoundEnd || MatchOver) return;

            var requester = ResolveSource(info);
            int requesterSlot = GetSlotForPlayerId(requester.PlayerId);
            if (requesterSlot == 0)
            {
                LogRejected("NextRound", requester, "Unknown slot."); return;
            }

            int bit = 1 << (requesterSlot - 1);
            if ((NextRoundReadyMask & bit) == 0)
            {
                NextRoundReadyMask |= bit;
                NotifyAccepted(requester, PlayerActionType.Ready);
            }

            if (!AllSeatsReady(NextRoundReadyMask)) return;   // 아직 안 누른 사람이 있다
            HostStartNextRound();
        }

        /// <summary>
        /// 다음 라운드를 연다. 전원 준비와 마감 시계가 같은 출구를 쓰도록 한 곳에 둔다.
        ///
        /// 마감이 지났을 때 <b>내보내지 않고 그냥 진행하는</b> 이유: 이 게임의 다른 모든
        /// 단계(뇌물·판돈·딜러·행동·고발)가 자리를 비운 사람에게 기본값을 넣고 넘어간다.
        /// 라운드 종료만 사람을 쳐내면 규칙이 하나 더 늘고, 무엇보다 셋이 있어야 성립하는
        /// 판에서 한 명을 빼는 것은 판을 끝내는 것과 같다.
        /// (매치 종료는 반대로 내보낸다 — 거기엔 이어 갈 다음 판이 없다.)
        /// </summary>
        private void HostStartNextRound()
        {
            RoundEndEndTick = 0;

            // 3라운드마다 역할 교대(딜러→A→B→딜러). RoundIndex는 방금 끝낸 라운드 번호다.
            bool rolesRotated = RoundSettlement.ShouldRotateRolesAfterRound(RoundIndex);
            if (rolesRotated) HostRotateRoles();

            // A/B 좌석이 최소 판돈을 못 내면 낼 수 있는 베팅이 아예 없다. 마감이 대신
            // 채워 넣는 금액(TimedOutBet = MinBet)조차 IsAffordable을 통과하지 못해
            // 라운드가 성립하지 않으므로, 열지 말고 매치를 종료한다.
            if (!HostContestantsCanPlay())
            {
                MatchOver = true;
                Debug.Log("[FusionGameState] 최소 판돈을 낼 수 없는 좌석이 있어 매치를 종료합니다.");
                return;
            }

            RoundIndex++;
            ResetRoundSecrets();
            if (rolesRotated) HostBeginRoleAssignment();
            else HostBeginBribeSelection();
            Debug.Log($"[FusionGameState] 라운드 {RoundIndex}/{RoundSettlement.MaxRounds} 시작 "
                + $"(딜러 = 슬롯 {FindSlotByRole(PlayerRole.Dealer)}).");
        }

        /// <summary>
        /// 항복. 누구든 자기 자리에서 판을 접을 수 있고, 그 순간 <b>모두의</b> 매치가 끝난다.
        ///
        /// <b>왜 셋 다 끝나는가.</b> 이 게임은 딜러 하나와 플레이어 둘이 있어야 한 판이
        /// 성립한다. 한 자리가 빠지면 남은 둘이 이어 갈 수 있는 규칙이 없으므로,
        /// 항복은 곧 매치 종료다. 자리를 뜨는 것(나가기)과 달리 순위표는 그대로 뜬다 —
        /// 여기까지의 성적은 실제로 겨뤄서 나온 것이라 지울 이유가 없다.
        ///
        /// 되물리지 않는다. 취소를 두면 항복이 협박 카드가 된다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestSurrender(RpcInfo info = default)
        {
            var src = ResolveSource(info);
            if (!Object.HasStateAuthority) return;

            if (CurrentPhase == GamePhase.WaitingForPlayers)
            {
                LogRejected("Surrender", src, "No match in progress."); return;
            }
            if (MatchOver || SurrenderedRole != PlayerRole.None)
            {
                LogRejected("Surrender", src, "Match is already over."); return;
            }

            var role = GetRoleForPlayerId(src.PlayerId);
            if (role == PlayerRole.None)
            {
                LogRejected("Surrender", src, "Unknown seat."); return;
            }

            SurrenderedRole = role;
            MatchOver = true;

            // 굴러가던 시계를 전부 세운다. 단계로 걸린 마감은 단계가 바뀌면 저절로
            // 멎지만, 라운드 종료 마감만은 단계를 보고도 계속 돌아 다음 라운드를 연다.
            CanAccuse = false;
            RoundEndEndTick = 0;
            NextRoundReadyMask = 0;
            ResultReadyMask = 0;

            // 알림은 히트·스탠드와 같은 통로로 보낸다. 받는 쪽이 버전 하나만 보고
            // 한 번씩 띄우는 구조라, 여기 얹으면 항복만 두 번 뜨는 일이 없다.
            HostPublishApprovedAction(role, PlayerActionType.RequestSurrender);

            // 라운드 종료로 옮겨 놓아야 매치 종료 마감(다시 플레이 게이트)이 걸린다.
            CurrentPhase = GamePhase.RoundEnd;
            Debug.Log($"[FusionGameState] {role} 항복. 매치를 종료합니다.");
        }

        // ── 채팅 ─────────────────────────────────────────────────────

        /// <summary>
        /// 앉아 있는 사람만 말한다. 세션이 살아 있고 자리가 있으면 참.
        /// 채팅창은 이 값 하나를 보고 입력칸을 열거나 잠근다.
        /// </summary>
        bool ChatRelay.IHost.CanSend =>
            Object != null && Object.IsValid
            && Runner != null && Runner.IsRunning
            && GetLocalPlayerSlot() != 0;

        void ChatRelay.IHost.Send(string text) => Rpc_SendChat(text);

        /// <summary>
        /// 친 말을 호스트에 올린다.
        ///
        /// <b>왜 곧바로 뿌리지 않는가.</b> 보낸 사람의 화면에만 먼저 띄우면 그 줄이
        /// 남들의 화면에서는 다른 자리에 끼어든다 — 대화의 순서가 사람마다 달라진다.
        /// 호스트를 한 번 거치면 셋이 같은 순서로 읽는다.
        ///
        /// 이름은 실어 보내지 않는다. 누가 보냈는지는 호스트가 자리로 안다 —
        /// 보내는 쪽이 이름을 정하면 남의 이름을 달고 말할 수 있다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SendChat(string message, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            var src = ResolveSource(info);
            int slot = GetSlotForPlayerId(src.PlayerId);
            if (slot == 0)
            {
                LogRejected("Chat", src, "Unknown seat."); return;
            }

            // 보내는 쪽이 이미 다듬어 보내지만 여기서 한 번 더 지난다 — 고쳐 만든
            // 클라이언트는 무엇이든 보낼 수 있고, 믿을 수 있는 것은 호스트뿐이다.
            string text = ChatLog.Sanitize(message);
            if (text.Length == 0) return;

            Rpc_BroadcastChat(slot, text);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_BroadcastChat(int slot, string message) =>
            ChatLog.Add(GetCharacterForSlot(slot), ChatSpeakerName(slot), message);

        /// <summary>
        /// 채팅에 적히는 이름 — "토끼 · 플레이어 A".
        ///
        /// <b>동물만으로는 부족하다.</b> 배역은 3라운드마다 돌기 때문에, 지금 이 말을 한
        /// 토끼가 딜러인지 플레이어인지가 그 말의 뜻을 바꾼다. 반대로 배역만 적으면
        /// 자리가 돌 때 같은 사람이 다른 사람처럼 보인다. 둘 다 있어야 한다.
        ///
        /// 동물을 고르기 전(방에 막 들어온 참)에는 자리 번호로 부른다.
        /// </summary>
        private string ChatSpeakerName(int slot)
        {
            // 이름을 지었으면 그 이름으로만 부른다. 동물은 테이블 위에 그대로 서 있고
            // 채팅 줄의 색도 동물을 따르므로, 글자까지 겹쳐 적을 필요가 없다.
            string name = GetNameForSlot(slot);
            if (string.IsNullOrEmpty(name))
                name = CharacterIdentity.NameKo(GetCharacterForSlot(slot));

            // 이름도 동물도 없는 사람 — 방에 막 들어와 아직 아무것도 고르지 않았다.
            return string.IsNullOrEmpty(name)
                ? ZooJackText.Get("Chat.Speaker.Slot", "플레이어 {0}", slot)
                : name;
        }

        // 고발 선택(또는 타임아웃 자동 승복)을 판정하고 최종 결과를 모든 피어에 브로드캐스트한다.
        private void HostResolveAccusation(AccusationChoice choice, int accuserId)
        {
            CanAccuse = false;
            if (accuserId == 0 || roundContext == null)
            {
                // 정산 없이 끝나는 경로에서도 종료 판정은 반드시 거쳐야 한다.
                // (빠뜨리면 최대 라운드를 넘겨 다음 라운드가 열린다)
                HostEvaluateMatchEnd();
                CurrentPhase = GamePhase.RoundEnd;
                return;
            }

            string accuserPlayerId = $"fusion-{accuserId}";
            string apparentWinnerPlayerId = $"fusion-{RevealedWinnerPlayerId}";
            var judgment = roundEngine.JudgeAccusation(roundContext, choice, accuserPlayerId, apparentWinnerPlayerId);

            // ── 재화 정산(호스트 권한) ──────────────────────────────
            int pidA = FindPlayerIdByRole(PlayerRole.PlayerA);
            int pidB = FindPlayerIdByRole(PlayerRole.PlayerB);
            secretBribes.TryGetValue(pidA, out int bribeA);
            secretBribes.TryGetValue(pidB, out int bribeB);
            int stake = Stake; // HostStartInitialDeal에서 확정된 값(칩 표시와 동일 원천)
            bool manipulated = ManipulationRecordTracker.WasAnyManipulationRecorded(roundContext);

            var settle = RoundSettlement.Compute(
                choice, judgment.AccusationResult, PublicOutcome, manipulated, bribeA, bribeB, stake,
                foldedRole: FoldedRole);

            AddBalanceForRole(PlayerRole.PlayerA, settle.PlayerADelta);
            AddBalanceForRole(PlayerRole.PlayerB, settle.PlayerBDelta);
            AddBalanceForRole(PlayerRole.Dealer,  settle.DealerDelta);

            HostEvaluateMatchEnd();

            // 뇌물은 이제야 공개된다. 정산과 같은 함수로 '실제로 넘어간 몫'을 뽑아
            // 함께 실어 보낸다 — 받는 쪽이 다시 계산하면 규칙이 두 벌이 된다.
            RoundSettlement.BribesKept(
                manipulated, settle.ResolvedWinner,
                choice == AccusationChoice.Accuse
                    && judgment.AccusationResult == AccusationResult.Success,
                PublicOutcome, bribeA, bribeB,
                out int keptFromA, out int keptFromB);

            // 배신(딜러 단독 승리)까지 반영된 최종 승자를 공개한다.
            VisibleFinalWinner = settle.ResolvedWinner;
            HostFinalJudgmentResolved?.Invoke(judgment);
            Rpc_BroadcastFinalJudgment(
                RoundIndex,
                settle.ResolvedWinner,
                settle.ReasonText ?? string.Empty,
                settle.PlayerADelta,
                settle.PlayerBDelta,
                settle.DealerDelta,
                manipulated,
                choice,
                judgment.AccusationResult,
                bribeA, bribeB,
                keptFromA, keptFromB);
            CurrentPhase = GamePhase.FinalJudgment;
            FinalJudgmentEndTick = Runner.Tick + FinalJudgmentDurationTicks;
        }

        // ── 잔액 헬퍼 (슬롯 귀속) ────────────────────────────────────

        private void AddBalanceForRole(PlayerRole role, int delta)
        {
            int slot = FindSlotByRole(role);
            if (slot == 0) return;
            SetBalanceForSlot(slot, GetBalanceForSlot(slot) + delta);
        }

        private int FindSlotByRole(PlayerRole role)
        {
            for (int slot = 1; slot <= MaxPlayers; slot++)
                if (GetRoleForSlot(slot) == role) return slot;
            return 0;
        }

        /// <summary>
        /// 1~3만 자리다. 0(자리 없음)이나 범위 밖은 여기서 걸러진다 — 예전 삼항 사슬이
        /// 마지막 기본값 가지로 하던 일을 이 한 곳이 대신한다.
        /// </summary>
        private static bool IsSeatSlot(int slot) => slot >= 1 && slot <= MaxPlayers;

        private int GetBalanceForSlot(int slot) => IsSeatSlot(slot) ? Balances[slot - 1] : 0;

        private void SetBalanceForSlot(int slot, int value)
        {
            if (IsSeatSlot(slot)) Balances.Set(slot - 1, value);
        }

        /// <summary>역할(A/B/Dealer)에 해당하는 좌석의 현재 잔액. UI 표시에 사용.</summary>
        public int BalanceForRole(PlayerRole role) => GetBalanceForSlot(FindSlotByRole(role));

        /// <summary>로컬 플레이어 좌석의 현재 잔액.</summary>
        public int LocalBalance => GetBalanceForSlot(GetLocalPlayerSlot());

        /// <summary>
        /// 매치 종료 판정. 한 라운드가 끝날 때마다 호출한다.
        /// 종료 사유는 두 가지 — 파산자 발생, 또는 최대 라운드 도달.
        /// MatchOver가 켜지면 Rpc_RequestNextRound가 다음 라운드를 열지 않는다.
        /// </summary>
        private void HostEvaluateMatchEnd()
        {
            for (int slot = 1; slot <= MaxPlayers; slot++)
                if (RoundSettlement.IsBankrupt(GetBalanceForSlot(slot))) { MatchOver = true; return; }

            if (RoundIndex >= RoundSettlement.MaxRounds) MatchOver = true;
        }

        /// <summary>
        /// 이번 라운드에 판돈을 걸어야 하는 A·B 좌석이 최소 판돈을 낼 수 있는지.
        /// 한쪽이라도 못 내면 뇌물 제출이 성립하지 않으므로 라운드를 열 수 없다.
        /// </summary>
        private bool HostContestantsCanPlay() =>
            !RoundSettlement.IsBankrupt(BalanceForRole(PlayerRole.PlayerA))
            && !RoundSettlement.IsBankrupt(BalanceForRole(PlayerRole.PlayerB));

        // 역할 교대: RoleRotationPeriod(3)라운드마다 한 번. 도는 방향은 규칙 쪽
        // (RoundSettlement.NextRole)이 정한다 — 화면이 그 방향을 말로 적는 곳도 같은
        // 함수를 본다.
        private void HostRotateRoles()
        {
            for (int slot = 1; slot <= 3; slot++)
                SetRoleForSlot(slot, RoundSettlement.NextRole(GetRoleForSlot(slot)));
        }

        // ── Host 전용 게임 흐름 메서드 ───────────────────────────────

        private void HostBeginRoleAssignment()
        {
            RoleAssignmentVersion++;
            CurrentPhase = GamePhase.RoleAssignment;
            RoleAssignmentEndTick = Runner.Tick + RoleAssignmentDurationTicks;
        }

        private void HostBeginBribeSelection()
        {
            CurrentPhase = GamePhase.BribeSelection;
            BribeSelectionEndTick = (int)Runner.Tick
                + TicksFromSeconds(bribeSelectionSeconds);
        }

        private void HostStartInitialDeal()
        {
            BribeSelectionEndTick = 0;   // 둘 다 냈다. 시계를 내린다.

            int pidA = FindPlayerIdByRole(PlayerRole.PlayerA);
            int pidB = FindPlayerIdByRole(PlayerRole.PlayerB);

            roundContext = roundEngine.CreateContext($"fusion-{pidA}", $"fusion-{pidB}");
            HostGenerateNextInitialCandidate();
        }

        private void HostBeginBetSelection()
        {
            CurrentPhase = GamePhase.BetSelection;
            BetSelectionEndTick = (int)Runner.Tick
                + TicksFromSeconds(betSelectionSeconds);
            HostSendPublicSnapshot();
        }

        // ── 뇌물·판돈 마감 ────────────────────────────────────────────
        //
        // 두 단계 모두 "제출 수가 2에 도달할 때"만 진행하므로, 한 사람이 응답을 멈추면
        // 나머지 둘이 끝없이 기다린다. 마감이 오면 빠진 사람 몫을 기본값으로 채워
        // 제출 수를 강제로 2로 만든 뒤 평소와 같은 경로로 넘긴다.
        //
        // 채워 넣기가 실패해도(좌석이 비었다면) 진행은 그대로 한다. 여기서 되돌아가면
        // 막으려던 무한 대기가 다시 생긴다.

        private void HostApplyBribeTimeout()
        {
            BribeSelectionEndTick = 0;
            HostAutoSubmitBribe(PlayerRole.PlayerA);
            HostAutoSubmitBribe(PlayerRole.PlayerB);
            HostSendDealerBribeStatus();
            HostStartInitialDeal();
        }

        private void HostAutoSubmitBribe(PlayerRole role)
        {
            int pid = FindPlayerIdByRole(role);
            if (pid == 0 || !bribeSubmitters.Add(pid)) return;

            secretBribes[pid] = RoundSettlement.TimedOutBribe;
            if (role == PlayerRole.PlayerA) BribeSubmittedA = true;
            else BribeSubmittedB = true;

            // 금액은 공지에 실리지 않는다(역할 + 행동 종류뿐). 시간 초과로 채웠다는 사실이
            // 뇌물 액수를 노출시키면 안 되므로, 평소 제출과 똑같은 모양으로만 알린다.
            HostPublishApprovedAction(role, PlayerActionType.SubmitBribe);
            Debug.Log($"[FusionGameState] {role} 뇌물 선택 시간 초과. "
                + $"뇌물 {RoundSettlement.TimedOutBribe}(으)로 자동 제출합니다.");
        }

        private void HostApplyBetTimeout()
        {
            BetSelectionEndTick = 0;
            HostAutoSubmitBet(PlayerRole.PlayerA);
            HostAutoSubmitBet(PlayerRole.PlayerB);
            HostFinalizeBetsAndStartDecisions();
        }

        private void HostAutoSubmitBet(PlayerRole role)
        {
            int pid = FindPlayerIdByRole(role);
            if (pid == 0 || !betSubmitters.Add(pid)) return;

            secretBets[pid] = RoundSettlement.TimedOutBet;
            if (role == PlayerRole.PlayerA) BetSubmittedA = true;
            else BetSubmittedB = true;

            HostPublishApprovedAction(role, PlayerActionType.SubmitBet);
            Debug.Log($"[FusionGameState] {role} 판돈 선택 시간 초과. "
                + $"최소 판돈 {RoundSettlement.TimedOutBet}(으)로 자동 제출합니다.");
        }

        private void HostFinalizeBetsAndStartDecisions()
        {
            BetSelectionEndTick = 0;   // 둘 다 냈다. 시계를 내린다.

            int pidA = FindPlayerIdByRole(PlayerRole.PlayerA);
            int pidB = FindPlayerIdByRole(PlayerRole.PlayerB);
            secretBets.TryGetValue(pidA, out int betA);
            secretBets.TryGetValue(pidB, out int betB);

            // 두 제출이 모두 끝난 이 시점에만 공개 Networked 값으로 복사한다.
            BetA = betA;
            BetB = betB;
            Stake = RoundSettlement.ResolveStake(betA, betB);
            HostAdvancePlayerDecision();
        }

        private void HostGenerateNextInitialCandidate()
        {
            var candidateSet = roundEngine.GenerateNextInitialCandidateSet(roundContext);
            CurrentPhase = GamePhase.CardCandidateGeneration;
            DealerDecisionElapsedTime = 0f;

            // Dealer 전용 Snapshot 업데이트
            HostSendDealerSnapshot(candidateSet);
            CurrentPhase = GamePhase.DealerCardDistribution;
        }

        private void HostApplyDealerCardChoice(DealerCardChoice choice, float decisionTime)
        {
            bool wasInitialDeal = roundContext?.CurrentCandidateSet?.DealStep == BlackjackDealStep.InitialDeal;
            string dealerPlayerId = $"fusion-{FindPlayerIdByRole(PlayerRole.Dealer)}";
            roundEngine.ApplyDealerCardChoice(roundContext, choice, dealerPlayerId, decisionTime);
            HostPublishApprovedAction(PlayerRole.Dealer, PlayerActionType.SubmitDealerCardChoice);

            // 더블다운한 손은 이 한 장으로 끝이다. 카드가 들어온 직후 스탠드시켜야
            // HostAdvancePlayerDecision이 곧바로 상대 차례 또는 결과 계산으로 넘어간다.
            roundEngine.ApplyDoubleDownAutoStand(roundContext, choice.TargetPlayerRole);
            HostSyncRoundState();

            if (!roundContext.IsInitialDealComplete)
            {
                // 초기 딜 계속
                HostGenerateNextInitialCandidate();
            }
            else
            {
                if (wasInitialDeal && !tieRedealInProgress)
                {
                    // 첫 두 장을 모두 확인한 뒤에야 각 플레이어가 판돈을 선택한다.
                    HostBeginBetSelection();
                }
                else
                {
                    // 무승부 재딜은 기존 뇌물·판돈을 유지하고 다시 베팅하지 않는다.
                    tieRedealInProgress = false;
                    HostAdvancePlayerDecision();
                }
            }
        }

        // 타임아웃 시 기본 카드(index 0) 자동 선택
        private void HostApplyDefaultDealerCardChoice()
        {
            var candidateSet = roundContext?.CurrentCandidateSet;
            if (candidateSet == null) return;

            var choice = new DealerCardChoice
            {
                TargetPlayerRole = candidateSet.TargetPlayerRole,
                CandidateSetId = candidateSet.CandidateSetId,
                ChosenCandidateIndex = 0
            };
            HostApplyDealerCardChoice(choice, DealerDecisionElapsedTime);
            Debug.Log("[FusionGameState] Dealer decision timed out. Default card (index 0) applied.");
        }

        private void HostApplyTimedOutPlayerStand()
        {
            PlayerRole role = DecisionTurn;
            if (role != PlayerRole.PlayerA && role != PlayerRole.PlayerB)
            {
                HostAdvancePlayerDecision();
                return;
            }

            if (!roundContext.HasStood(role))
            {
                roundEngine.ApplyStand(roundContext, role);
                HostSyncRoundState();
            }

            Debug.Log($"[FusionGameState] {role} decision timed out. Stand applied.");
            HostAdvancePlayerDecision();
        }

        private void HostGenerateHitCandidateSet(PlayerRole role)
        {
            roundEngine.GenerateHitCandidateSet(roundContext, role);
            CurrentPhase = GamePhase.CardCandidateGeneration;
            DealerDecisionElapsedTime = 0f;
            HostSendDealerSnapshot(roundContext.CurrentCandidateSet);
            CurrentPhase = GamePhase.DealerCardDistribution;
        }

        // Stand 처리 후 다음 결정 단계 또는 결과 계산으로 진행
        private void HostAdvancePlayerDecision()
        {
            PlayerDecisionElapsedTime = 0f;

            if (roundEngine.IsBothDecisionDone(roundContext))
            {
                HostCalculateAndRevealResult();
                return;
            }
            CurrentPhase = GamePhase.PlayerDecision;
            HostSendPublicSnapshot();
        }

        private void HostCalculateAndRevealResult()
        {
            CurrentPhase = GamePhase.BlackjackResultCalculation;
            var result = roundEngine.CalculateResult(roundContext);
            PublicOutcome = result.Outcome;

            if (result.Outcome == MatchOutcome.Tie)
            {
                // 무승부 → 안내를 띄우고 잠시 멈춘 뒤 재딜한다.
                //
                // 여기서 곧바로 ResetForTieRedeal + HostGenerateNextInitialCandidate를 부르면
                // 두 가지가 한꺼번에 망가진다.
                //  1) 후자가 같은 틱 안에서 CurrentPhase를 DealerCardDistribution으로 덮어쓴다.
                //     [Networked] 값은 틱 끝에 한 번 복제되므로 클라이언트는 TieRedeal을
                //     아예 보지 못하고, "무승부!" 화면이 단 한 프레임도 뜨지 않는다.
                //  2) 손패를 지운 뒤라 무엇이 비겼는지 볼 수 없다.
                //
                // 그래서 페이즈만 세우고 손패는 그대로 둔 채 게이트를 연다.
                // 실제 재딜은 전원이 누르거나(Rpc_RequestTieRedeal) 마감이 지난 뒤다.
                //
                // 준비 표시를 비우는 것을 잊으면 안 된다. 이 게이트는 결과 공개와 같은
                // 마스크를 쓰는데, 무승부 재딜은 <b>같은 라운드 안에서</b> 일어나므로
                // 라운드마다 도는 초기화(ResetRoundSecrets)가 사이에 끼지 않는다.
                ResultReadyMask = 0;
                CurrentPhase = GamePhase.TieRedeal;
                TieRedealEndTick = Runner.Tick + ResultRevealDurationTicks;
                HostSendPublicSnapshot();
                return;
            }

            HostRevealOutcome(result.Outcome);
        }

        // 겉보기 승자를 확정하고 결과 공개 단계로 넘어간다.
        // 블랙잭 승부와 다이가 공유하는 출구다(다이는 승부만 건너뛰고 여기로 합류한다).
        private void HostRevealOutcome(MatchOutcome outcome)
        {
            PublicOutcome = outcome;
            RevealedWinnerPlayerId = outcome == MatchOutcome.PlayerAWin
                ? FindPlayerIdByRole(PlayerRole.PlayerA)
                : FindPlayerIdByRole(PlayerRole.PlayerB);

            CurrentPhase = GamePhase.ResultReveal;
            ResultRevealEndTick = Runner.Tick + ResultRevealDurationTicks;
            CanAccuse = false;
            HostSendPublicSnapshot();
        }

        // ── 스냅샷 전송 ──────────────────────────────────────────────

        private void HostSendDealerBribeStatus()
        {
            var dealerRef = FindPlayerRefById(FindPlayerIdByRole(PlayerRole.Dealer));
            if (dealerRef == PlayerRef.None) return;

            secretBribes.TryGetValue(FindPlayerIdByRole(PlayerRole.PlayerA), out int bribeA);
            secretBribes.TryGetValue(FindPlayerIdByRole(PlayerRole.PlayerB), out int bribeB);
            Rpc_SendDealerBribeStatus(dealerRef, bribeA, bribeB);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_SendDealerBribeStatus(
            [RpcTarget] PlayerRef dealer, int bribeA, int bribeB)
        {
            LocalDealerBribeA = bribeA;
            LocalDealerBribeB = bribeB;
        }

        private void HostSendDealerSnapshot(CardCandidateSet candidateSet)
        {
            if (roundContext == null || candidateSet == null) return;

            var dealerPlayerId = FindPlayerIdByRole(PlayerRole.Dealer);
            var dealerRef = FindPlayerRefById(dealerPlayerId);
            if (dealerRef == PlayerRef.None)
            {
                Debug.LogWarning("[FusionGameState] Dealer PlayerRef를 찾지 못해 카드 후보를 전송할 수 없습니다.");
                return;
            }

            secretBribes.TryGetValue(FindPlayerIdByRole(PlayerRole.PlayerA), out int bribeA);
            secretBribes.TryGetValue(FindPlayerIdByRole(PlayerRole.PlayerB), out int bribeB);

            var cands = candidateSet.Candidates;
            Rpc_SendDealerCandidates(
                dealerRef,
                cands.Count > 0 ? EncodeCard(cands[0]) : (byte)0,
                cands.Count > 1 ? EncodeCard(cands[1]) : (byte)0,
                cands.Count > 2 ? EncodeCard(cands[2]) : (byte)0,
                candidateSet.TargetPlayerRole,
                candidateSet.DealStep,
                bribeA,
                bribeB);
        }

        // Dealer 피어에게만 전송되는 비공개 카드 후보 + 뇌물 정보 (targeted RPC)
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_SendDealerCandidates(
            [RpcTarget] PlayerRef dealer,
            byte card0, byte card1, byte card2,
            PlayerRole targetRole, BlackjackDealStep dealStep,
            int bribeA, int bribeB)
        {
            localDealerCandidates.Clear();
            var c0 = DecodeCard(card0); if (c0 != null) localDealerCandidates.Add(c0);
            var c1 = DecodeCard(card1); if (c1 != null) localDealerCandidates.Add(c1);
            var c2 = DecodeCard(card2); if (c2 != null) localDealerCandidates.Add(c2);
            LocalDealerTargetRole = targetRole;
            LocalDealerDealStep = dealStep;
            LocalDealerBribeA = bribeA;
            LocalDealerBribeB = bribeB;
            LocalDealerCandidateVersion++;
        }

        // 최종 판정 결과(승자 + 사유)를 모든 피어에 전송
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_BroadcastFinalJudgment(
            int round,
            FinalWinner winner,
            string reason,
            int playerADelta,
            int playerBDelta,
            int dealerDelta,
            bool wasManipulated,
            AccusationChoice accusation,
            AccusationResult accusationResult,
            int bribeA, int bribeB,
            int bribeKeptA, int bribeKeptB)
        {
            LastFinalWinner = winner;
            LastFinalReason = reason;
            LastPlayerADelta = playerADelta;
            LastPlayerBDelta = playerBDelta;
            LastDealerDelta = dealerDelta;
            LastRoundWasManipulated = wasManipulated;
            LastAccusationChoice = accusation;
            LastAccusationResult = accusationResult;
            LastBribeA = bribeA;
            LastBribeB = bribeB;
            LastBribeKeptA = bribeKeptA;
            LastBribeKeptB = bribeKeptB;
            FinalJudgmentVersion++;

            // 이 판정이 몇 번째 라운드 것인지는 호스트가 실어 보낸다. 받는 쪽의 RoundIndex를
            // 읽지 않는 이유는 그것도 복제 타이밍에 달려 있어서다 — 판정과 라운드 번호가
            // 어긋나면 이 값의 존재 이유가 사라진다.
            LastFinalJudgmentRound = round;
        }

        private void HostSendPublicSnapshot()
        {
            PublicRoundSnapshot snapshot = new PublicRoundSnapshot
            {
                Phase = CurrentPhase,
                RoundIndex = RoundIndex,
                DealerDecisionElapsedTime = DealerDecisionElapsedTime,
                PublicOutcome = PublicOutcome,
                ApparentWinnerPlayerId = RevealedWinnerPlayerId == 0 ? string.Empty : $"fusion-{RevealedWinnerPlayerId}",
                CanAccuse = CanAccuse
            };
            Debug.Log($"[FusionGameState] Public snapshot: Phase={snapshot.Phase}, Outcome={snapshot.PublicOutcome}");
        }

        // ── 카드 상태 동기화 ─────────────────────────────────────────

        // Host의 roundContext 상태를 Networked 프로퍼티로 복사해 모든 피어에 공개한다.
        private void HostSyncRoundState()
        {
            if (roundContext == null) return;

            SyncHand(HandACards, roundContext.PlayerAHand);
            SyncHand(HandBCards, roundContext.PlayerBHand);
            PlayerAStoodNet = roundContext.PlayerAStood;
            PlayerBStoodNet = roundContext.PlayerBStood;

            var scoreA = roundEngine.GetScore(roundContext, PlayerRole.PlayerA);
            var scoreB = roundEngine.GetScore(roundContext, PlayerRole.PlayerB);
            bool aDone = roundContext.PlayerAStood || scoreA.IsBust;
            bool bDone = roundContext.PlayerBStood || scoreB.IsBust;
            DecisionTurn = !aDone ? PlayerRole.PlayerA : !bDone ? PlayerRole.PlayerB : PlayerRole.None;
        }

        private void SyncHand(NetworkArray<byte> target, BlackjackHand hand)
        {
            for (int i = 0; i < MaxHandCards; i++)
                target.Set(i, i < hand.Cards.Count ? EncodeCard(hand.Cards[i]) : (byte)0);
        }

        private void ClearNetworkedRoundState()
        {
            for (int i = 0; i < MaxHandCards; i++)
            {
                HandACards.Set(i, 0);
                HandBCards.Set(i, 0);
            }
            PlayerAStoodNet = false;
            PlayerBStoodNet = false;
            DecisionTurn = PlayerRole.None;
            FoldedRole = PlayerRole.None;
            DoubledA = false;
            DoubledB = false;
            Stake = 0;
            BetA = 0;
            BetB = 0;
            BribeSubmittedA = false;
            BribeSubmittedB = false;
            BetSubmittedA = false;
            BetSubmittedB = false;
        }

        // 카드 1장을 byte 하나로 인코딩: 0 = 없음, 1~52 = suit*13 + rank
        public static byte EncodeCard(BlackjackCard card) =>
            card == null ? (byte)0 : (byte)((int)card.Suit * 13 + (int)card.Rank);

        public static BlackjackCard DecodeCard(byte value) =>
            value == 0
                ? null
                : new BlackjackCard
                {
                    Rank = (BlackjackCardRank)(((value - 1) % 13) + 1),
                    Suit = (BlackjackCardSuit)((value - 1) / 13)
                };

        // 클라이언트가 Networked 배열에서 패를 읽는다.
        public List<BlackjackCard> GetHandCards(PlayerRole role)
        {
            var source = role == PlayerRole.PlayerA ? HandACards : HandBCards;
            var cards = new List<BlackjackCard>();
            for (int i = 0; i < MaxHandCards; i++)
            {
                var card = DecodeCard(source[i]);
                if (card == null) break;
                cards.Add(card);
            }
            return cards;
        }

        // ── 결정 순서 제어 ───────────────────────────────────────────

        // PlayerDecision 단계에서 현재 차례인지 확인한다.
        // PlayerA가 먼저, PlayerA가 Stand/Bust 이후 PlayerB 순서다.
        private bool IsCurrentDecisionTurn(PlayerRole role)
        {
            if (role == PlayerRole.PlayerA)
                return !roundContext.PlayerAStood && !roundEngine.GetScore(roundContext, PlayerRole.PlayerA).IsBust;
            if (role == PlayerRole.PlayerB)
            {
                var scoreA = roundEngine.GetScore(roundContext, PlayerRole.PlayerA);
                bool playerADone = roundContext.PlayerAStood || scoreA.IsBust;
                return playerADone && !roundContext.PlayerBStood && !roundEngine.GetScore(roundContext, PlayerRole.PlayerB).IsBust;
            }
            return false;
        }

        // ── 공개 조회 ────────────────────────────────────────────────

        public PlayerRole GetLocalPublicRole() =>
            GetRoleForPlayerId(Runner.LocalPlayer.PlayerId);

        public int GetLocalPlayerSlot() =>
            GetSlotForPlayerId(Runner.LocalPlayer.PlayerId);

        public bool IsLocalPlayerAllowedToAccuse() =>
            CurrentPhase == GamePhase.Accusation && CanAccuse && IsLosingPlayer(Runner.LocalPlayer.PlayerId);

        public IReadOnlyList<PlayerSeat> BuildPublicPlayerSeats()
        {
            var seats = new List<PlayerSeat>(MaxPlayers);
            for (int slot = 1; slot <= MaxPlayers; slot++)
                seats.Add(CreatePublicSeat(GetPlayerIdForSlot(slot), GetRoleForSlot(slot)));
            return seats;
        }

        // ── 리셋 ────────────────────────────────────────────────────

        private void ResetRoundToWaitingForPlayers()
        {
            // 이미 로비에 있었다면 남은 사람의 캐릭터는 건드리지 않는다. 한 명이 나갔다고
            // 나머지가 고른 동물까지 풀리면, 아무 조작도 하지 않은 사람의 캐릭터가
            // 테이블에서 사라진다. 반대로 매치 중이었다면 배역 자체가 무의미해지므로 비운다.
            bool wasInLobby = CurrentPhase == GamePhase.WaitingForPlayers;

            ReadyMask = 0; // 누가 빠졌으니 어차피 시작 조건이 깨진다
            if (!wasInLobby)
            {
                for (int slot = 1; slot <= 3; slot++)
                {
                    SetRoleForSlot(slot, PlayerRole.None);
                    SetCharacterForSlot(slot, CharacterId.None);
                }
            }

            // 라운드 번호를 되돌리지 않으면 다음 매치가 9라운드에서 시작한다 —
            // Rpc_RequestStartGame은 여기서 하나 올릴 뿐이라, 최대 라운드에 이미 닿아
            // 있으면 첫 판을 열자마자 매치가 끝난다.
            RoundIndex = 0;
            MatchOver = false; // 새 매치 시작
            SurrenderedRole = PlayerRole.None;
            ResetRoundSecrets();
            CurrentPhase = GamePhase.WaitingForPlayers;
        }

        /// <summary>
        /// 최종 순위(1위부터)를 슬롯 번호(1~3)로 반환한다.
        /// 잔액은 역할이 아니라 좌석에 귀속되므로 순위도 슬롯 기준으로 낸다
        /// (역할이 3라운드마다 교대되어도 그대로 성립한다).
        /// 항복한 사람과 파산자는 자동 최하위.
        /// </summary>
        public int[] FinalRankingSlots()
        {
            int withdrawn = FindSlotByRole(SurrenderedRole) - 1;   // 자리가 없으면 -1
            var balances = new int[MaxPlayers];
            for (int slot = 1; slot <= MaxPlayers; slot++) balances[slot - 1] = GetBalanceForSlot(slot);

            var order = RoundSettlement.RankOrder(balances, withdrawn);
            var slots = new int[order.Length];
            for (int i = 0; i < order.Length; i++) slots[i] = order[i] + 1;
            return slots;
        }

        /// <summary>슬롯 번호(1~3)의 현재 잔액.</summary>
        public int BalanceForSlot(int slot) => GetBalanceForSlot(slot);

        /// <summary>로컬 플레이어의 슬롯 번호(1~3). 못 찾으면 0.</summary>
        public int LocalSlot => GetLocalPlayerSlot();

        // ── 틱 기반 제한시간 조회 ─────────────────────────────────────
        //
        // 고발·더블다운은 경과 시간을 누적하지 않고 마감 틱만 들고 있다.
        // UI가 초를 보여 주려면 남은 틱을 초로 환산해 줘야 한다.
        // 틱레이트는 세션 설정에 달렸으므로 상수로 박지 않고 DeltaTime으로 환산한다.

        /// <summary>고발 마감까지 남은 초. 고발 단계가 아니면 0.</summary>
        public float AccusationSecondsLeft => SecondsUntil(AccusationEndTick);

        /// <summary>
        /// 고발 제한시간 전체 길이(초). 화면 시계가 눈금을 잡는 데 쓴다.
        ///
        /// 인스펙터 값을 그대로 주지 않고 틱으로 돌렸다 되돌리는 이유는, 마감이 걸릴 때도
        /// 같은 환산(올림)을 거치기 때문이다. 눈금과 실제 마감이 한 틱이라도 어긋나면
        /// 시계가 0을 지나 잠깐 더 돌거나 0에 닿기 전에 넘어간다.
        /// </summary>
        public float AccusationLimitSeconds => TicksToSeconds(AccusationDurationTicks);

        /// <summary>뇌물 마감까지 남은 초. 마감이 없으면(단계가 아니거나 이미 끝났으면) 0.</summary>
        public float BribeSecondsLeft =>
            BribeSelectionEndTick == 0 ? 0f : SecondsUntil(BribeSelectionEndTick);

        /// <summary>판돈 마감까지 남은 초. 마감이 없으면 0.</summary>
        public float BetSecondsLeft =>
            BetSelectionEndTick == 0 ? 0f : SecondsUntil(BetSelectionEndTick);

        /// <summary>이 역할이 아직 뇌물을 내지 않았는지. 시계를 누구 머리 위에 붙일지 정한다.</summary>
        public bool IsBribePending(PlayerRole role) =>
            role == PlayerRole.PlayerA ? !BribeSubmittedA
            : role == PlayerRole.PlayerB && !BribeSubmittedB;

        /// <summary>이 역할이 아직 판돈을 내지 않았는지.</summary>
        public bool IsBetPending(PlayerRole role) =>
            role == PlayerRole.PlayerA ? !BetSubmittedA
            : role == PlayerRole.PlayerB && !BetSubmittedB;


        /// <summary>
        /// 지금 고발 여부를 정해야 하는 사람(겉보기 패자). 고발 단계가 아니거나
        /// 이미 답했으면 None. 제한시간이 이 사람 머리 위에 붙는다.
        /// </summary>
        public PlayerRole AccuserRole =>
            CurrentPhase != GamePhase.Accusation || !CanAccuse
                ? PlayerRole.None
                : PublicOutcome == MatchOutcome.PlayerAWin ? PlayerRole.PlayerB : PlayerRole.PlayerA;

        private float SecondsUntil(int endTick) =>
            Runner == null ? 0f : Mathf.Max(0f, (endTick - (int)Runner.Tick) * Runner.DeltaTime);

        private float TicksToSeconds(int ticks) =>
            Runner == null ? 0f : ticks * Runner.DeltaTime;

        // 초를 틱으로 바꾼다. 틱레이트를 상수로 박으면 세션 설정을 바꿨을 때
        // 화면의 초와 실제 마감이 어긋나므로 DeltaTime으로 환산한다.
        // 올림을 쓰는 이유는 시계 표시와 같다 — 마감이 표시된 숫자보다 먼저 오면 안 된다.
        private int TicksFromSeconds(float seconds) =>
            Runner == null || Runner.DeltaTime <= 0f
                ? 0
                : Mathf.CeilToInt(seconds / Runner.DeltaTime);

        private void ResetRoundSecrets()
        {
            secretBribes.Clear();
            secretBets.Clear();
            bribeSubmitters.Clear();
            betSubmitters.Clear();
            accusationSubmitters.Clear();
            roundContext = null;
            tieRedealInProgress = false;
            PublicOutcome = MatchOutcome.None;
            RevealedWinnerPlayerId = 0;
            VisibleFinalWinner = FinalWinner.None;
            CanAccuse = false;
            DealerDecisionElapsedTime = 0f;
            PlayerDecisionElapsedTime = 0f;
            ResultRevealEndTick = 0;
            FinalJudgmentEndTick = 0;
            AccusationEndTick = 0;
            RoleAssignmentEndTick = 0;
            BribeSelectionEndTick = 0;
            BetSelectionEndTick = 0;
            // 새 라운드가 열렸으니 지난 라운드의 준비 표시는 모두 지운다.
            // 남겨 두면 다음 결과·다음 라운드 끝에서 이미 모두 준비된 상태가 되어
            // 화면을 볼 새도 없이 판이 넘어간다.
            NextRoundReadyMask = 0;
            ResultReadyMask = 0;
            RematchReadyMask = 0;
            MatchOverEndTick = 0;
            RoundEndEndTick = 0;
            ClearNetworkedRoundState();
        }

        // ── 슬롯/역할 헬퍼 ──────────────────────────────────────────

        private void HostAssignInitialRoles()
        {
            // 로비에서 플레이어가 세 역할을 모두 선택하지 않았으면 슬롯 순서대로 자동 배정
            if (!(HasRole(PlayerRole.PlayerA) && HasRole(PlayerRole.PlayerB) && HasRole(PlayerRole.Dealer)))
            {
                SetRoleForSlot(1, PlayerRole.Dealer);
                SetRoleForSlot(2, PlayerRole.PlayerA);
                SetRoleForSlot(3, PlayerRole.PlayerB);
            }

            // 캐릭터(동물)를 좌석에 고정한다. 시작 시점의 역할로 한 번만 정하고 이후 바꾸지 않는다 —
            // 역할은 3라운드마다 돌지만 사람은 그대로이므로, 캐릭터가 역할을 따라가면
            // 같은 사람이 3라운드마다 다른 종족이 된다.
            for (int slot = 1; slot <= 3; slot++)
                SetCharacterForSlot(slot, CharacterIdentity.FromInitialRole(GetRoleForSlot(slot)));

            // 캐릭터를 각 역할의 시작 위치에 세워 둔다. 여기서 채워두면 아무도 아직
            // 움직이지 않은 순간에도 세 마리가 제자리에 서 있고, 조회 쪽에서
            // '미보고'를 좌표로 추측할 필요가 없다.
            for (int slot = 1; slot <= 3; slot++)
                SetAvatarPosForSlot(slot, AvatarMovement.SpawnPositionFor(GetRoleForSlot(slot)));
        }

        /// <summary>
        /// 자리 주인이 지은 이름을 받는다. <b>보낸 사람의 자리에만</b> 쓴다 —
        /// 자리는 <see cref="RpcInfo"/>가 알려 주는 보낸 이로 찾으므로, 남의 이름을
        /// 고쳐 달라고 보낼 방법이 없다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SetNickname(string name, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            var src = ResolveSource(info);
            int slot = GetSlotForPlayerId(src.PlayerId);
            if (slot == 0) { LogRejected("SetNickname", src, "Unknown slot."); return; }

            // 보내는 쪽이 이미 다듬어 보내지만 여기서 한 번 더 지난다 — 채팅과 같은
            // 이유다. 고쳐 만든 클라이언트는 무엇이든 보낼 수 있다.
            SetNameForSlot(slot, PlayerNames.Sanitize(name));
        }

        private void SetNameForSlot(int slot, string name)
        {
            if (IsSeatSlot(slot)) SlotNames.Set(slot - 1, name);
        }

        private string GetNameForSlot(int slot) =>
            IsSeatSlot(slot) ? SlotNames[slot - 1].ToString() : string.Empty;

        /// <summary>
        /// <see cref="PlayerNames"/>가 묻는 곳. 배역이 아니라 <b>지금 그 배역을 맡은 사람</b>의
        /// 이름을 돌려준다 — 자리가 돌면 답도 따라 바뀐다.
        /// </summary>
        public string NameFor(PlayerRole role) => GetNameForSlot(FindSlotByRole(role));

        private void SetCharacterForSlot(int slot, CharacterId id)
        {
            if (IsSeatSlot(slot)) SlotCharacters.Set(slot - 1, id);
        }

        private CharacterId GetCharacterForSlot(int slot) =>
            IsSeatSlot(slot) ? SlotCharacters[slot - 1] : CharacterId.None;

        /// <summary>
        /// 지금 이 역할을 맡고 있는 <b>사람</b>의 캐릭터. 역할이 교대되면 반환값도 따라 바뀐다.
        /// UI는 역할 자리에 이 값을 그려야 "딜러 자리에 앉은 사람"이 제대로 표시된다.
        /// </summary>
        public CharacterId CharacterForRole(PlayerRole role) => GetCharacterForSlot(FindSlotByRole(role));

        /// <summary>슬롯(=사람)의 캐릭터. 매치 내내 바뀌지 않는다.</summary>
        public CharacterId CharacterForSlot(int slot) => GetCharacterForSlot(slot);

        /// <summary>슬롯(=사람)이 지금 맡고 있는 역할. 3라운드마다 바뀐다.</summary>
        public PlayerRole RoleForSlot(int slot) => GetRoleForSlot(slot);

        private bool HasRole(PlayerRole role) => FindSlotByRole(role) != 0;

        private PlayerRole GetRoleForSlot(int slot) =>
            IsSeatSlot(slot) ? SlotRoles[slot - 1] : PlayerRole.None;

        private int GetSlotForPlayerId(int playerId)
        {
            for (int slot = 1; slot <= MaxPlayers; slot++)
                if (GetPlayerIdForSlot(slot) == playerId) return slot;
            return 0;
        }

        private PlayerRole GetRoleForPlayerId(int playerId)
        {
            return GetRoleForSlot(GetSlotForPlayerId(playerId));
        }

        private int FindPlayerIdByRole(PlayerRole role)
        {
            return GetPlayerIdForSlot(FindSlotByRole(role));
        }

        private bool IsLosingPlayer(int playerId) =>
            playerId != 0
            && RevealedWinnerPlayerId != 0
            && playerId != RevealedWinnerPlayerId
            && GetRoleForPlayerId(playerId) != PlayerRole.Dealer;

        private int FindLosingPlayerId()
        {
            for (int slot = 1; slot <= MaxPlayers; slot++)
            {
                int playerId = GetPlayerIdForSlot(slot);
                if (IsLosingPlayer(playerId)) return playerId;
            }
            return 0;
        }

        private PlayerRef FindPlayerRefById(int playerId)
        {
            foreach (var p in Runner.ActivePlayers)
                if (p.PlayerId == playerId) return p;
            return PlayerRef.None;
        }

        private void SetReady(int slot, bool ready)
        {
            var bit = 1 << (slot - 1);
            ReadyMask = ready ? ReadyMask | bit : ReadyMask & ~bit;
        }

        private bool IsSlotReady(int slot) =>
            slot >= 1 && slot <= 3 && (ReadyMask & (1 << (slot - 1))) != 0;

        // None을 그대로 넘기면 FindSlotByRole이 "아직 아무도 고르지 않은 첫 슬롯"을
        // 돌려주므로, 빈자리를 물었는데 남의 상태가 나온다. 입구에서 잘라낸다.

        /// <summary>이 자리를 맡은 사람의 접속 ID. 0이면 빈자리.</summary>
        public int PlayerIdForRole(PlayerRole role) =>
            role == PlayerRole.None ? 0 : FindPlayerIdByRole(role);

        /// <summary>이 자리를 맡은 사람이 준비를 마쳤는지. 빈자리면 false.</summary>
        public bool IsRoleReady(PlayerRole role) =>
            role != PlayerRole.None && IsSlotReady(FindSlotByRole(role));

        /// <summary>로컬 플레이어가 준비를 마쳤는지.</summary>
        public bool IsLocalPlayerReady() => IsSlotReady(GetLocalPlayerSlot());

        private void SetPlayerIdForSlot(int slot, int id)
        {
            if (IsSeatSlot(slot)) PlayerIds.Set(slot - 1, id);
        }

        private int GetPlayerIdForSlot(int slot) => IsSeatSlot(slot) ? PlayerIds[slot - 1] : 0;

        // ── 준비 게이트 공용 헬퍼 ───────────────────────────────────
        //
        // 결과 공개 → 고발, 라운드 종료 → 다음 라운드. 둘 다 "앉아 있는 모두가 눌러야
        // 넘어간다"는 같은 규칙이라 셈은 한 곳에 둔다. 마스크만 다르다.

        // 빈자리는 세지 않는다. 누군가 나간 상태에서 빈자리까지 기다리면
        // 남은 사람들이 아무리 눌러도 판이 넘어가지 않는다.
        private bool AllSeatsReady(int mask)
        {
            for (int slot = 1; slot <= MaxPlayers; slot++)
            {
                if (GetPlayerIdForSlot(slot) == 0) continue;
                if ((mask & (1 << (slot - 1))) == 0) return false;
            }
            return true;
        }

        private int CountReady(int mask)
        {
            int count = 0;
            for (int slot = 1; slot <= MaxPlayers; slot++)
                if (GetPlayerIdForSlot(slot) != 0 && (mask & (1 << (slot - 1))) != 0)
                    count++;
            return count;
        }

        private bool IsLocalReady(int mask)
        {
            int slot = GetLocalPlayerSlot();
            return slot != 0 && (mask & (1 << (slot - 1))) != 0;
        }

        /// <summary>지금 자리에 앉아 있는 사람 수(빈자리 제외). 준비 게이트의 분모다.</summary>
        public int SeatedPlayerCount
        {
            get
            {
                int count = 0;
                for (int slot = 1; slot <= MaxPlayers; slot++)
                    if (GetPlayerIdForSlot(slot) != 0) count++;
                return count;
            }
        }

        /// <summary>다음 라운드 준비를 마친 사람 수.</summary>
        public int NextRoundReadyCount => CountReady(NextRoundReadyMask);

        /// <summary>로컬 플레이어가 다음 라운드 준비를 눌렀는지.</summary>
        public bool IsLocalReadyForNextRound => IsLocalReady(NextRoundReadyMask);

        /// <summary>고발 단계로 넘어갈 준비를 마친 사람 수.</summary>
        public int AccusationReadyCount => CountReady(ResultReadyMask);

        /// <summary>로컬 플레이어가 고발 단계로 넘어갈 준비를 눌렀는지.</summary>
        public bool IsLocalReadyForAccusation => IsLocalReady(ResultReadyMask);

        private void SetRoleForSlot(int slot, PlayerRole role)
        {
            if (IsSeatSlot(slot)) SlotRoles.Set(slot - 1, role);
        }

        private void SetAvatarPosForSlot(int slot, Vector2 position)
        {
            if (IsSeatSlot(slot)) AvatarPositions.Set(slot - 1, position);
        }

        private Vector2 GetAvatarPosForSlot(int slot) =>
            IsSeatSlot(slot) ? AvatarPositions[slot - 1] : Vector2.zero;

        private int GetEmotionSequence(int slot) =>
            IsSeatSlot(slot) ? EmotionSequences[slot - 1] : 0;

        private void SetEmotionSequence(int slot, int sequence)
        {
            if (IsSeatSlot(slot)) EmotionSequences.Set(slot - 1, sequence);
        }

        private int GetEmotionReadyTick(int slot) =>
            IsSeatSlot(slot) ? EmotionReadyTicks[slot - 1] : int.MaxValue;

        private void SetEmotionReadyTick(int slot, int tick)
        {
            if (IsSeatSlot(slot)) EmotionReadyTicks.Set(slot - 1, tick);
        }

        /// <summary>
        /// 역할이 배정된 좌석의 캐릭터 위치. 시작 위치는 역할 배정 시점에 미리 채워두므로
        /// (<see cref="HostAssignInitialRoles"/>) 여기서 "아직 보고 없음"을 추측하지 않는다.
        /// (0,0)을 미보고 신호로 쓰면 테이블 정중앙에 선 플레이어가 남의 화면에서
        /// 시작 위치로 순간이동한다.
        /// </summary>
        public Vector2 GetAvatarPosition(PlayerRole role)
        {
            for (int slot = 1; slot <= 3; slot++)
                if (GetRoleForSlot(slot) == role) return GetAvatarPosForSlot(slot);

            return AvatarMovement.SpawnPositionFor(role);
        }

        private PlayerSeat CreatePublicSeat(int playerId, PlayerRole role) =>
            new PlayerSeat
            {
                PlayerId = playerId == 0 ? string.Empty : $"fusion-{playerId}",
                DisplayName = playerId == 0 ? "Empty" : $"Player {playerId}",
                Role = role,
                IsConnected = playerId != 0,
                IsReady = playerId != 0 && (ReadyMask & (1 << (GetSlotForPlayerId(playerId) - 1))) != 0
            };

        // Host가 자기 자신에게 호출한 RPC는 네트워크를 거치지 않아 info.Source가
        // PlayerRef.None(-1)으로 도착한다. 이 경우 호스트의 LocalPlayer로 보정한다.
        private PlayerRef ResolveSource(RpcInfo info) =>
            info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;

        private void LogRejected(string action, PlayerRef player, string reason) =>
            Debug.LogWarning($"[FusionGameState] Rejected {action} from {player.PlayerId}: {reason}");

        private void NotifyAccepted(
            PlayerRef player,
            PlayerActionType actionType,
            int bribeAmount = 0,
            int betAmount = 0,
            int chosenIndex = -1,
            AccusationChoice accusationChoice = AccusationChoice.None)
        {
            HostActionAccepted?.Invoke(new PlayerActionRequest
            {
                SenderPlayerId = $"fusion-{player.PlayerId}",
                ActionType = actionType,
                BribeAmount = bribeAmount,
                BetAmount = betAmount,
                ChosenCandidateIndex = chosenIndex,
                AccusationChoice = accusationChoice
            });

            // 카드 배분은 수동 선택과 타임아웃 선택이 함께 지나가는 실제 적용 지점에서 공지한다.
            if (actionType != PlayerActionType.SubmitDealerCardChoice)
                HostPublishApprovedAction(GetRoleForPlayerId(player.PlayerId), actionType);
        }

        private void HostPublishApprovedAction(PlayerRole actorRole, PlayerActionType actionType)
        {
            if (!Object.HasStateAuthority ||
                !ActionAnnouncementMapper.TryMap(actorRole, actionType, out _, out _))
                return;

            LastApprovedActionActor = actorRole;
            LastApprovedActionType = actionType;
            ActionAnnouncementVersion++;
        }
    }
}
#endif
