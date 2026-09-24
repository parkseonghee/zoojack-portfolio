using System;

namespace ZooJack
{
    // Host에서 직접 호출하는 게임 로직 진입점.
    // Fusion, RPC, MonoBehaviour에 의존하지 않는다.
    public class RoundEngine
    {
        public const int MaxCardsPerHand = 4;

        /// <summary>
        /// 더블다운이 허용되는 손패 장수. 받은 두 장을 그대로 들고 있을 때, 즉 '처음'에만이다.
        /// 다이는 아무 때나 물러날 수 있는 반면 더블다운은 이 한 지점에서만 열린다.
        /// </summary>
        public const int DoubleDownMaxCards = 2;

        /// <summary>
        /// 더블다운한 손이 받을 수 있는 최대 장수. 표준 블랙잭대로 "1장만 받고 종료"라
        /// 초기 2장 + 1장 = 3장이다. 히트 2회 권리를 판돈 2배와 맞바꾸는 셈이다.
        /// </summary>
        public const int DoubledMaxCardsPerHand = DoubleDownMaxCards + 1;

        private readonly BlackjackDeckService deckService;

        public RoundEngine() : this(new BlackjackDeckService()) { }

        public RoundEngine(BlackjackDeckService deckService)
        {
            this.deckService = deckService ?? throw new ArgumentNullException(nameof(deckService));
        }

        // ── 라운드 초기화 ─────────────────────────────────────────────

        public RoundContext CreateContext(string playerAId, string playerBId) =>
            new RoundContext(playerAId, playerBId);

        // ── 초기 딜 ──────────────────────────────────────────────────

        // 다음 초기 딜 후보 세트를 생성하고 context에 저장한다.
        // IsInitialDealComplete가 true이면 호출하지 않는다.
        public CardCandidateSet GenerateNextInitialCandidateSet(RoundContext context)
        {
            if (context.IsInitialDealComplete)
                throw new InvalidOperationException("Initial deal is already complete.");

            var target = context.GetInitialDealTarget();
            var set = deckService.GenerateCandidateSet(target, BlackjackDealStep.InitialDeal);
            context.CurrentCandidateSet = set;
            return set;
        }

        // ── Hit 후보 세트 생성 ───────────────────────────────────────

        public CardCandidateSet GenerateHitCandidateSet(RoundContext context, PlayerRole targetRole)
        {
            if (context.GetHand(targetRole).Cards.Count >= MaxCardsFor(context, targetRole))
                throw new InvalidOperationException("A hand cannot receive more cards than its limit allows.");

            var dealStep = targetRole == PlayerRole.PlayerA
                ? BlackjackDealStep.PlayerAHit
                : BlackjackDealStep.PlayerBHit;

            var set = deckService.GenerateCandidateSet(targetRole, dealStep);
            context.CurrentCandidateSet = set;
            return set;
        }

        // ── Dealer 카드 선택 적용 ────────────────────────────────────

        // Dealer가 후보 인덱스를 선택하면 해당 카드를 Hand에 추가하고 ManipulationRecord를 생성한다.
        public ManipulationRecord ApplyDealerCardChoice(
            RoundContext context,
            DealerCardChoice choice,
            string dealerPlayerId,
            float decisionTime)
        {
            var candidateSet = context.CurrentCandidateSet;
            if (candidateSet == null)
                throw new InvalidOperationException("No active candidate set in context.");
            if (candidateSet.CandidateSetId != choice.CandidateSetId)
                throw new InvalidOperationException("DealerCardChoice refers to a different candidate set.");
            if (choice.ChosenCandidateIndex < 0 || choice.ChosenCandidateIndex >= candidateSet.Candidates.Count)
                throw new ArgumentOutOfRangeException(nameof(choice.ChosenCandidateIndex));

            var card = candidateSet.Candidates[choice.ChosenCandidateIndex];
            var hand = context.GetHand(candidateSet.TargetPlayerRole);
            hand.Cards.Add(card);

            int turnIndex = context.ManipulationRecords.Count;
            var record = ManipulationRecordTracker.CreateAndRecord(
                context, choice, candidateSet, dealerPlayerId, turnIndex, decisionTime);

            // 초기 딜 단계라면 카운터를 진행한다.
            if (candidateSet.DealStep == BlackjackDealStep.InitialDeal)
                context.InitialDealStep++;

            context.CurrentCandidateSet = null;
            return record;
        }

        // ── 점수 조회 ────────────────────────────────────────────────

        public BlackjackScore GetScore(RoundContext context, PlayerRole role) =>
            BlackjackScoreCalculator.Calculate(context.GetHand(role));

        // ── Stand 처리 ───────────────────────────────────────────────

        public void ApplyStand(RoundContext context, PlayerRole role) =>
            context.MarkStood(role);

        // ── Die(다이) 처리 ───────────────────────────────────────────

        /// <summary>
        /// 다이 가능 여부. <b>자기 차례라면 언제든</b> 물러날 수 있다 — 히트로 한 장 받은
        /// 뒤에도 된다. 장수 제한을 두면 "받아 보고 아니면 튄다"를 막을 수는 있지만,
        /// 대신 두 장을 받자마자 결정하라고 몰아세우게 된다.
        ///
        /// 다만 <b>버스트한 뒤에는 안 된다.</b> 이미 진 판을 절반 값에 사는 도피구가 되어,
        /// 21을 넘긴 순간 다이가 항상 최적해가 된다.
        /// </summary>
        public bool CanDie(RoundContext context, PlayerRole role)
        {
            if (context == null || context.HasFolded) return false;
            if (role != PlayerRole.PlayerA && role != PlayerRole.PlayerB) return false;
            if (!context.IsInitialDealComplete) return false;
            if (context.HasStood(role)) return false;
            return !GetScore(context, role).IsBust;
        }

        public void ApplyDie(RoundContext context, PlayerRole role)
        {
            if (!CanDie(context, role))
                throw new InvalidOperationException("Cannot die in the current round state.");
            context.FoldedRole = role;
        }

        // 다이하면 승부 없이 상대가 겉보기 승자가 된다.
        public static MatchOutcome OutcomeAfterDie(PlayerRole folder) =>
            folder == PlayerRole.PlayerA ? MatchOutcome.PlayerBWin : MatchOutcome.PlayerAWin;

        // ── 더블다운 처리 ────────────────────────────────────────────

        /// <summary>
        /// 이 손이 받을 수 있는 최대 장수. 더블다운한 쪽만 3장으로 줄어든다.
        /// 무승부 재딜로 손패가 초기화된 뒤에도 더블다운 상태는 유지되므로, 장수 제한을
        /// 선언 시점이 아니라 이 함수로 계속 물어봐야 "1장만 받는다"가 재딜 이후에도 지켜진다.
        /// </summary>
        public static int MaxCardsFor(RoundContext context, PlayerRole role) =>
            context != null && context.HasDoubled(role) ? DoubledMaxCardsPerHand : MaxCardsPerHand;

        /// <summary>
        /// 더블다운 가능 여부. <b>사람마다 처음 딱 한 번</b>, 받은 두 장을 그대로 들고 있을 때만이다.
        /// 상대가 먼저 걸었는지는 보지 않는다 — 남의 선택이 내 선택을 지우면 안 된다.
        ///
        /// 한 장이라도 받은 뒤에는 불가능하다. 손패가 두 장이라는 조건이 곧 "처음"의 정의이고,
        /// 무승부 재딜로 손패가 다시 두 장이 되어도 <see cref="RoundContext.HasDoubled"/> 기록이
        /// 남아 있어 두 번째 선언은 막힌다.
        ///
        /// 잔액 검증은 여기서 하지 않는다. 양쪽 잔액과 뇌물을 아는 호출측(호스트)의 몫이다.
        /// </summary>
        public bool CanDoubleDown(RoundContext context, PlayerRole role)
        {
            if (context == null || context.HasFolded) return false;
            if (role != PlayerRole.PlayerA && role != PlayerRole.PlayerB) return false;
            if (!context.IsInitialDealComplete) return false;
            if (context.HasStood(role)) return false;
            if (context.HasDoubled(role)) return false;
            return context.GetHand(role).Cards.Count == DoubleDownMaxCards;
        }

        public void ApplyDoubleDown(RoundContext context, PlayerRole role)
        {
            if (!CanDoubleDown(context, role))
                throw new InvalidOperationException("Cannot double down in the current round state.");
            context.MarkDoubled(role);
        }

        /// <summary>더블다운을 선언한 쪽의 반대편. 알림 문구에 쓴다.</summary>
        public static PlayerRole Opponent(PlayerRole role) =>
            role == PlayerRole.PlayerA ? PlayerRole.PlayerB : PlayerRole.PlayerA;

        /// <summary>
        /// 카드를 한 장 받은 뒤 더블다운한 손을 자동으로 Stand시킨다.
        /// 더블다운은 "1장만 받고 종료"이므로, 딜러가 카드를 배분한 직후 이 판정을 태워야 한다.
        /// 호출은 멱등이다(이미 스탠드했으면 아무 일도 하지 않는다).
        /// </summary>
        public void ApplyDoubleDownAutoStand(RoundContext context, PlayerRole role)
        {
            if (context == null || !context.HasDoubled(role)) return;
            if (context.HasStood(role)) return;
            if (context.GetHand(role).Cards.Count < DoubledMaxCardsPerHand) return;
            context.MarkStood(role);
        }

        // ── 양측 결정 완료 여부 ──────────────────────────────────────

        public bool IsBothDecisionDone(RoundContext context)
        {
            var scoreA = GetScore(context, PlayerRole.PlayerA);
            var scoreB = GetScore(context, PlayerRole.PlayerB);
            return context.IsBothDecisionDone(scoreA, scoreB);
        }

        // ── 최종 결과 계산 ───────────────────────────────────────────

        public BlackjackResult CalculateResult(RoundContext context) =>
            BlackjackResultCalculator.Calculate(context.PlayerAHand, context.PlayerBHand);

        // ── 고발 판정 ────────────────────────────────────────────────

        public FinalJudgmentData JudgeAccusation(
            RoundContext context,
            AccusationChoice choice,
            string accuserPlayerId,
            string apparentWinnerPlayerId) =>
            AccusationJudge.Judge(context, choice, accuserPlayerId, apparentWinnerPlayerId);

        // ── 무승부 재딜 ──────────────────────────────────────────────

        // 무승부 시 뇌물·Dealer·조작 기록을 유지한 채 카드 상태만 초기화한다.
        public void ResetForTieRedeal(RoundContext context)
        {
            context.PlayerAHand.Cards.Clear();
            context.PlayerBHand.Cards.Clear();
            context.PlayerAStood = false;
            context.PlayerBStood = false;
            context.InitialDealStep = 0;
            context.CurrentCandidateSet = null;
            // ManipulationRecords는 유지한다 (무승부 재딜 규칙)
            // 더블다운 기록도 유지한다 — 이미 건 돈은 되돌릴 수 없고, 지우면 재딜마다 다시 걸린다.
            // 유지 덕분에 MaxCardsFor가 재딜 이후에도 3장 제한을 계속 걸어준다.
        }
    }
}
