namespace ZooJack
{
    /// <summary>
    /// 한 라운드 정산 결과. 좌석 역할(A/B/Dealer)별 잔액 증감과, 배신(딜러 단독 승리)이
    /// 반영된 최종 승자를 담는다. 실제 좌석 id에 매핑하는 책임은 호출측(디렉터)에 있다.
    /// </summary>
    public struct SettlementResult
    {
        public int PlayerADelta;
        public int PlayerBDelta;
        public int DealerDelta;
        public FinalWinner ResolvedWinner; // 딜러 단독 승리까지 반영된 최종 승자
        public string ReasonText;
    }

    /// <summary>
    /// Zoo Jack 재화 정산 규칙(순수 계산). 기획 확정본의 4분기 판정표를 그대로 구현한다.
    ///
    ///  ┌ 고발 성공(조작O)   : 고발자 +S −50 +150 / 상대 −S / 딜러 −150 (뇌물 전액 반납)
    ///  ├ 고발 실패(조작X)   : 겉보기 승자 +S / 고발자 −S −50 −100 / 딜러 +20
    ///  ├ 겉보기 승자 승리   : 승자 +S / 패자 −S / 딜러 조작 시 +승자 뇌물, 공정 시 +20
    ///  └ 딜러 단독 승리     : 딜러 +2S +뇌물합 / A·B 각 −S
    ///
    /// 뇌물 이동은 딜러가 **이긴 쪽만 선택적으로 챙기는** 구조다.
    ///  - 조작 + 미고발  : 겉보기 승자의 뇌물만 딜러가 획득. 패자 뇌물은 반납.
    ///  - 조작 + 고발 성공: 양쪽 뇌물 전액 반납(발각되면 못 챙긴다).
    ///  - 딜러 단독 승리 : 양쪽 뇌물 전액 획득.
    ///  - 공정 진행      : 양쪽 뇌물 전액 반납 + <see cref="SafeReward"/> 획득.
    ///
    /// 제로섬이 아니다. 안전 보상(+20)이 유입되고, 고발 보증금(−50)과 무고 벌금(−100)은
    /// 아무에게도 가지 않고 판에서 사라진다. 이 유출입을 없애면 고발의 기대값이 무너진다
    /// (고발 성공 순이익 +100 / 실패 순손실 −150).
    /// </summary>
    public static class RoundSettlement
    {
        // ── 공통 재화 상수(핫시트/네트워크 공유) ──
        public const int SeedMoney       = 1000; // 시작 시드머니
        public const int MaxBribe        = 100;  // 뇌물 상한
        public const int MinBet          = 10;   // 최소 판돈 베팅
        public const int DefaultBet      = 50;   // 베팅 슬라이더 기본값
        public const int DealerFine      = 150;  // 고발 성공 시 딜러 벌금(고발자에게 지급)
        public const int FalseAccuseFine = 100;  // 고발 실패 시 무고 벌금(소멸)

        /// <summary>
        /// 판돈의 최소 단위(원). 슬라이더는 이 단위의 배수로만 멈추고, 프리셋 칩도 배수다.
        ///
        /// <b>왜 상수인가.</b> 예전에는 이 값이 <c>/ 10</c>, <c>* 10</c>이라는 맨 숫자로
        /// 두 디렉터에 스무 곳 흩어져 있었다. 단위를 5원으로 낮추려면 스무 곳을 빠짐없이
        /// 찾아야 했고, 하나라도 놓치면 슬라이더 눈금과 실제 판돈이 어긋났다.
        /// 값을 읽고 쓰는 것은 아래 두 함수만 하도록 모았다.
        /// </summary>
        public const int BetStep = 10;

        /// <summary>
        /// 판돈 프리셋 칩 금액. 잔액을 넘는 칩은 버튼이 자동으로 꺼진다.
        ///
        /// 뇌물 쪽 <see cref="BribeSelector.Presets"/>와 같은 방식이다. 예전에는 판돈만
        /// <c>btnBetPreset100</c>처럼 <b>금액이 필드 이름에까지</b> 박혀 있어, 300을 200으로
        /// 바꾸려면 이름·리스너·활성화 조건까지 여덟 곳을 고쳐야 했다.
        /// 지금은 이 배열의 순서가 곧 씬에 연결한 버튼의 순서다.
        /// </summary>
        public static readonly int[] BetPresets = { 100, 300, 500, 700 };

        /// <summary>판돈(원) → 슬라이더 눈금.</summary>
        public static int BetToSteps(int bet) => bet / BetStep;

        /// <summary>슬라이더 눈금 → 판돈(원).</summary>
        public static int StepsToBet(int steps) => steps * BetStep;

        /// <summary>
        /// 딜러가 조작 없이 공정하게 라운드를 진행했을 때의 보상.
        /// 뇌물을 이긴 쪽만 선택적으로 챙기는 구조로 바뀌면서 공정 진행의 수익이 0이 되었다.
        /// 그대로 두면 조작이 지배 전략이 되므로, 공정 진행에 최소 수익선을 깔아준다.
        /// </summary>
        public const int SafeReward = 20;

        /// <summary>
        /// 고발할 때 무조건 빠져나가는 보증금. 성공해도 돌려받지 못하고, 딜러에게 가지도 않는다.
        /// 고발 자체에 비용을 붙여 "일단 지르고 보는" 고발을 막는 장치다.
        /// 이 값 때문에 고발 성공 순이익은 +100(−50+150), 실패 순손실은 −150(−50−100)이 된다.
        /// </summary>
        public const int AccusationDeposit = 50;

        /// <summary>
        /// 한 매치의 최대 라운드 수. 이 라운드를 마치면 파산자가 없어도 잔액 순위로 매치를 끝낸다.
        /// 핫시트와 네트워크가 같은 값을 써야 하므로 여기(규칙 단일 원천)에 둔다.
        /// <see cref="RoleRotationPeriod"/>의 배수여야 세 좌석이 딜러를 똑같은 횟수만큼 맡는다.
        /// </summary>
        public const int MaxRounds = 9;

        /// <summary>
        /// 역할(딜러→A→B→딜러)을 교대하는 주기(라운드 수).
        /// 딜러와 플레이어는 수익 구조가 아예 다르므로 한쪽 역할에 고정되면 파산 위험이 비대칭이다.
        /// 9라운드를 3라운드씩 끊어 세 좌석이 딜러를 정확히 3라운드씩 맡게 한다.
        /// </summary>
        public const int RoleRotationPeriod = 3;

        /// <summary>
        /// <paramref name="completedRoundIndex"/>번째 라운드를 마친 시점에 역할을 교대해야 하는지.
        /// 라운드 인덱스는 1부터 시작하므로 3·6라운드를 마쳤을 때 교대한다(4·7라운드부터 새 역할).
        /// 마지막 라운드 뒤에는 교대해봐야 매치가 끝나므로 호출측에서 걸러진다.
        /// </summary>
        public static bool ShouldRotateRolesAfterRound(int completedRoundIndex) =>
            completedRoundIndex > 0 && completedRoundIndex % RoleRotationPeriod == 0;

        /// <summary>
        /// 자리가 돌 때 이 역할이 <b>다음에 맡게 되는</b> 역할. 딜러 → 플레이어 A →
        /// 플레이어 B → 딜러.
        ///
        /// <b>도는 방향을 여기 한 곳에만 적는다.</b> 실제로 돌리는 쪽과 화면에서 방향을
        /// 말로 적는 쪽(기록 화면의 교대 줄)이 같은 함수를 봐야 한다. 둘이 갈라지면
        /// 화면이 거짓말을 하는데, 그 거짓말은 3라운드에 한 번만 드러난다.
        /// </summary>
        public static PlayerRole NextRole(PlayerRole role) => role switch
        {
            PlayerRole.Dealer  => PlayerRole.PlayerA,
            PlayerRole.PlayerA => PlayerRole.PlayerB,
            PlayerRole.PlayerB => PlayerRole.Dealer,
            _                  => role
        };

        /// <summary>
        /// <paramref name="roundIndex"/>를 치르는 동안, 자리가 돌기까지 남은 라운드 수.
        /// 0이면 <b>이번 라운드가 끝나는 순간</b> 돈다. 더 돌 일이 없으면(마지막 세 판) -1.
        ///
        /// 마지막 라운드 뒤의 교대는 세지 않는다 — 그때는 매치가 끝나 돌 자리가 없다.
        /// </summary>
        public static int RoundsUntilRotation(int roundIndex)
        {
            if (roundIndex <= 0) return -1;

            for (int round = roundIndex; round < MaxRounds; round++)
                if (ShouldRotateRolesAfterRound(round)) return round - roundIndex;
            return -1;
        }

        /// <summary>딜러 결정 제한시간(초). Time Bluffing의 기준값.</summary>
        public const float DealerDecisionSeconds = 15f;
        /// <summary>플레이어 히트/스탠드/다이 결정 제한시간(초).</summary>
        public const float PlayerDecisionSeconds = 30f;
        /// <summary>
        /// 고발 여부 결정 제한시간(초). 넘기면 자동 승복.
        ///
        /// 고를 것이 둘뿐이고(고발·수용) 그 판단에 쓸 정보는 이미 다 보고 있으므로
        /// 다른 단계만큼 길 이유가 없다. 오래 끌면 나머지 둘이 빈 화면을 보고 앉아 있다.
        ///
        /// 네트워크에서는 이 값이 <c>FusionGameState</c> 프리팹의 기본값으로 쓰이고,
        /// 인스펙터에서 덮어쓸 수 있다.
        /// </summary>
        public const float AccusationSeconds = 15f;

        // 뇌물·판돈은 A와 B가 동시에 고르는 유일한 두 단계다. 그래서 다른 단계와 달리
        // 제한시간이 없으면 한 사람만 응답을 멈춰도 라운드 전체가 멈춘다
        // (제출 수가 2에 도달해야만 다음 단계로 넘어가기 때문). 마감을 둬야 하는 이유다.

        /// <summary>뇌물 결정 제한시간(초). 넘기면 <see cref="TimedOutBribe"/>로 자동 제출.</summary>
        public const float BribeSelectionSeconds = 30f;
        /// <summary>판돈 결정 제한시간(초). 넘기면 <see cref="TimedOutBet"/>로 자동 제출.</summary>
        public const float BetSelectionSeconds = 30f;

        /// <summary>
        /// 시간이 다 됐는데 아무것도 내지 않은 사람 대신 낼 뇌물. 뇌물은 안 내는 것이
        /// 기본 상태이므로 0이다. 잠수한 사람의 돈을 임의로 딜러에게 넘기지 않는다.
        /// </summary>
        public const int TimedOutBribe = 0;

        /// <summary>
        /// 시간이 다 됐을 때 대신 낼 판돈. 베팅은 0이 될 수 없으므로(라운드가 성립하지 않는다)
        /// 낼 수 있는 가장 작은 금액을 쓴다. <see cref="MaxBribeFor"/>가 최소 판돈만큼은
        /// 항상 남겨 두므로 이 금액은 어떤 잔액에서도 <see cref="IsAffordable"/>를 통과한다.
        /// </summary>
        public static int TimedOutBet => MinBet;

        // ── 파산과 최종 순위 ───────────────────────────────────────
        // 한 명이라도 파산하면 매치가 끝나고, 순위는 잔액으로 정한다.

        /// <summary>
        /// 파산 판정. 최소 판돈(<see cref="MinBet"/>)조차 낼 수 없으면 파산으로 본다.
        /// 기준이 "0 이하"가 아니라 "MinBet 미만"인 이유: 잔액이 1~9면 살아 있는 것처럼 보이지만
        /// 뇌물·판돈 제출이 <see cref="IsAffordable"/>에서 항상 거부되어 라운드를 시작할 수 없다.
        /// </summary>
        public static bool IsBankrupt(int balance) => balance < MinBet;

        /// <summary>
        /// 최종 순위. 항복한 사람과 파산자를 아래로 내리고, 나머지는 잔액 내림차순.
        /// 반환값은 1위부터의 인덱스 목록이며, 동점은 입력 순서를 유지한다(안정 정렬).
        /// </summary>
        /// <param name="withdrawnIndex">
        /// 스스로 판을 접은 자리(항복). 잔액과 무관하게 <b>맨 아래</b>로 간다 —
        /// 앞서고 있을 때 접어도 마찬가지다. 그러지 않으면 이기고 있을 때 항복하는 것이
        /// 판을 끝내면서 1등을 굳히는 수가 된다. 없으면 -1.
        /// </param>
        public static int[] RankOrder(int[] balances, int withdrawnIndex = -1)
        {
            int n = balances.Length;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;

            for (int i = 1; i < n; i++) // 삽입 정렬(안정)
            {
                int cur = order[i];
                int j = i - 1;
                while (j >= 0 && RanksBelow(order[j], cur, balances, withdrawnIndex))
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = cur;
            }
            return order;
        }

        // a가 b보다 아래 순위여야 하면 true. 아래로 갈 이유는 셋이고 순서가 있다 —
        // 판을 접은 것이 가장 아래, 그다음 파산, 마지막이 잔액이다.
        private static bool RanksBelow(int a, int b, int[] balances, int withdrawnIndex)
        {
            bool aGone = a == withdrawnIndex, bGone = b == withdrawnIndex;
            if (aGone != bGone) return aGone;

            bool aOut = IsBankrupt(balances[a]), bOut = IsBankrupt(balances[b]);
            if (aOut != bOut) return aOut;            // 파산자가 아래로
            return balances[a] < balances[b];         // 잔액이 적은 쪽이 아래로
        }

        /// <summary>두 플레이어 베팅에서 실제 판돈을 산출한다(포커식 최소 매칭).</summary>
        public static int ResolveStake(int betA, int betB) => betA < betB ? betA : betB;

        // ── 예산 규칙: 뇌물 + 판돈 <= 잔액 ──────────────────────────
        // 뇌물을 먼저 떼고 남은 금액이 판돈의 상한이 된다. 뇌물은 최소 판돈을
        // 낼 여지를 남겨야 하므로 (잔액 - MinBet) 을 넘을 수 없다.

        /// <summary>이 잔액에서 낼 수 있는 뇌물 상한.</summary>
        public static int MaxBribeFor(int balance)
        {
            int limit = balance - MinBet;
            if (limit > MaxBribe) limit = MaxBribe;
            return limit < 0 ? 0 : limit;
        }

        /// <summary>뇌물을 차감하고 남은 판돈 상한.</summary>
        public static int MaxBetFor(int balance, int bribe)
        {
            if (bribe < 0) bribe = 0;
            int limit = balance - bribe;
            return limit < MinBet ? MinBet : limit;
        }

        /// <summary>
        /// 다이(패를 접고 물러남) 비용. 끝까지 가서 지는 것(−S)의 딱 절반이라,
        /// 승률을 50% 미만으로 보면 다이가 이득이 되는 지점에 맞춰져 있다.
        /// </summary>
        public static int DieCost(int stake) => stake / 2;

        // ── 더블다운 ────────────────────────────────────────────────
        // 판돈 S 자체를 2배로 올린다. 베팅액이 아니라 S를 올리는 이유:
        // S = min(betA, betB)라 한쪽 베팅만 2배로 만들면 최소 매칭에 먹혀 S가 그대로다.
        // 게다가 판돈은 A↔B 사이의 이동이므로, 한쪽만 2배로 노출시키면 승패에 따라
        // 돈이 생성되거나 사라진다. 양쪽을 2S에 함께 올리는 것이 유일하게 성립하는 형태다.
        //
        // 그래서 더블다운은 상대를 끌어들인다. 상대가 2S를 못 내면 마이너스로 떨어져
        // 파산하고 매치가 끝난다 — 막지 않는다. 잔액이 얇은 상대를 판에서 밀어내는 것도
        // 더블다운을 거는 이유다.
        //
        // S를 올려두면 DieCost(S/2), 배신(+2S), 고발 뒤집기(±S)가 전부 새 S를 따라오므로
        // 정산 쪽에는 손댈 곳이 없다.

        /// <summary>더블다운 이후의 판돈.</summary>
        public static int DoubledStake(int stake) => stake * 2;

        /// <summary>
        /// 테이블 위에 올라와 있는 한 사람의 금액. 더블다운을 <b>선언한 쪽만</b> 자기가 건 만큼
        /// 한 번 더 얹으므로, 그 사람의 액수만 2배가 되고 상대는 그대로다.
        ///
        /// 표시 전용이다. 정산이 보는 값은 여전히 <see cref="ResolveStake"/>의 결과다.
        /// </summary>
        public static int TableBet(int bet, bool doubled) => doubled ? bet * 2 : bet;

        /// <summary>
        /// 이 좌석이 판돈 <paramref name="stake"/>를 감당할 수 있는지.
        /// 뇌물은 이미 나갔으므로 잔액에서 함께 뺀다.
        ///
        /// 검사 대상은 <b>선언하는 본인뿐</b>이다. 상대의 감당 여부는 보지 않는다 —
        /// 상대가 2S를 못 내면 그 사람은 마이너스로 떨어져 파산하고 매치가 끝난다.
        /// 그것이 더블다운을 거는 이유이기도 하다. 애초에 상대의 뇌물이 비공개라
        /// 클라이언트는 상대의 감당 여부를 계산할 수도 없어서, 양쪽 검사를 두면
        /// 버튼은 켜져 있는데 눌러도 아무 일이 없는 화면이 된다.
        ///
        /// 제출 시점의 <see cref="IsAffordable"/>는 `뇌물 + 베팅 &lt;= 잔액`만 봤고
        /// 2S는 베팅액을 넘길 수 있다. 그래서 더블다운 시점의 재검증이 반드시 필요하다.
        /// </summary>
        public static bool CanAffordStake(int balance, int bribe, int stake)
        {
            if (bribe < 0) bribe = 0;
            return balance - bribe >= stake;
        }

        /// <summary>
        /// 지금 더블다운을 선언하면 판돈이 얼마가 되는지.
        ///
        /// <b>이미 누가 걸었으면 그대로다.</b> 판돈이 오르는 것은 라운드당 한 번뿐이라
        /// (둘 다 걸어도 2배에서 멈춘다), 두 번째 선언자는 지금 판돈만 감당하면 된다.
        /// 이걸 구분하지 않고 늘 2배로 검사하면 두 번째 선언자에게 실제 부담(2S)의
        /// 두 배인 4S를 요구하게 된다.
        /// </summary>
        public static int StakeAfterDoubleDown(int stake, bool anyoneDoubled) =>
            anyoneDoubled ? stake : DoubledStake(stake);

        // ── 고발 선택지 문구 ──────────────────────────────────────────
        // 두 장의 카드 중 하나를 고르는 화면에 그대로 들어간다. 금액이 섞인 문장이라
        // 상수 옆에 둬야 값을 고쳤을 때 화면 설명이 따라오지 않는 사고가 없다.
        //
        // 같은 정보를 두 번 말하지 않는 것이 규칙이다. 보증금은 태그가 말하므로
        // 본문에서 되풀이하지 않고, 본문은 '성공하면 / 실패하면'만 다룬다.

        /// <summary>고발 카드의 태그. 이 선택이 무엇을 걸고 무엇을 노리는지 두 낱말로 요약한다.</summary>
        public static string AccuseTagEffect() => ZooJackText.Get(
            "Game.Accusation.Tag.Reversal", "승패 역전");
        public static string AccuseTagCost() => ZooJackText.Get(
            "Game.Accusation.Tag.Deposit", "보증금 {0} <sprite index=0>", AccusationDeposit);

        /// <summary>승복 카드의 태그.</summary>
        public static string AcceptTagEffect() => ZooJackText.Get(
            "Game.Accusation.Tag.Safe", "위험 없음");

        // 줄바꿈을 직접 넣는다. 한글은 글자 단위로 접히기 때문에 자동 줄바꿈에 맡기면
        // '딜러'가 '딜 / 러'로 갈라져 읽는 속도가 떨어진다.
        public static string AccuseCardBody() => ZooJackText.Get(
            "Game.Accusation.AccuseBody",
            "조작이 있었다면 <b>승패가 뒤집히고</b>\n딜러에게 벌금 {0} <sprite index=0>을 받습니다.\n아니라면 무고 벌금 {1} <sprite index=0>을 뭅니다.",
            DealerFine, FalseAccuseFine);

        public static string AcceptCardBody() => ZooJackText.Get(
            "Game.Accusation.AcceptBody",
            "패배를 그대로 받아들입니다.\n더 잃지도, 더 얻지도 않습니다.");

        // ── 행동 설명 문구 ────────────────────────────────────────────
        // 결정 패널에서 버튼에 마우스를 올렸을 때 뜨는 한 줄 설명이다.
        // 고발 카드 문구와 같은 이유로 여기 둔다 — 숫자가 섞인 문장이라 규칙 상수를
        // 고쳤을 때 화면 설명이 따라오지 않는 사고를 막아야 한다.
        //
        // 한 줄로 끊는다. 이 자리는 원래 두 줄("누구의 차례" + "행동을 선택하세요")이라
        // 설명이 두 줄이 되면 카드 밖으로 흘러넘친다.

        public static string ActionTitle(PlayerActionType action) => action switch
        {
            PlayerActionType.RequestHit => ZooJackText.Get("Game.Decision.Hit.Title", "히트"),
            PlayerActionType.RequestStand => ZooJackText.Get("Game.Decision.Stand.Title", "스탠드"),
            PlayerActionType.RequestDoubleDown => ZooJackText.Get("Game.Decision.DoubleDown.Title", "더블다운"),
            PlayerActionType.RequestDie => ZooJackText.Get("Game.Decision.Die.Title", "다이"),
            _                                  => string.Empty
        };

        public static string ActionBody(PlayerActionType action) => action switch
        {
            PlayerActionType.RequestHit => ZooJackText.Get(
                "Game.Decision.Hit.Body", "카드를 한 장 더 받습니다."),
            PlayerActionType.RequestStand => ZooJackText.Get(
                "Game.Decision.Stand.Body", "카드를 더 받지 않고\n턴을 종료합니다."),
            // 2배·1장은 DoubledStake와 DoubleDownMaxCards가 정하는 값이다.
            PlayerActionType.RequestDoubleDown => ZooJackText.Get(
                "Game.Decision.DoubleDown.Body", "판돈을 2배로 올리고\n카드 한 장만 받고 멈춥니다."),
            // 절반은 DieCost(stake / 2)에서 온다.
            PlayerActionType.RequestDie => ZooJackText.Get(
                "Game.Decision.Die.Body", "포기하는 대신\n판돈의 절반만 잃습니다."),
            _                                  => string.Empty
        };

        // ── 더블다운 알림 문구 ────────────────────────────────────────
        // 더블다운은 선언한 쪽만 자기 판돈을 올리므로 상대의 동의가 필요 없다.
        // 그래서 고르는 화면이 아니라 "이런 일이 일어났다"는 알림 한 줄이면 된다.

        /// <summary>더블다운 알림의 제목. 누가 걸었는지가 핵심이라 이름이 먼저 온다.</summary>
        public static string DoubleDownNoticeTitle(string declarerName) =>
            ZooJackText.Get("Game.DoubleDown.NoticeTitle", "{0} 더블다운!", declarerName);

        /// <summary>
        /// 알림의 설명. 그래서 무엇이 달라지는지를 두 가지로 압축한다.
        /// 숫자 뒤에는 조사를 붙이지 않는다 — 20은 '이십'이라 '20으로', 3은 '삼'이라 '3으로'가
        /// 되는 식이어서 한글 받침 규칙으로는 고를 수 없다. 화살표가 그 자리를 대신한다.
        /// </summary>
        public static string DoubleDownNoticeBody(string declarerName, int bet) =>
            ZooJackText.Get(
                "Game.DoubleDown.NoticeBody",
                "건 돈 {0} → {1} (2배)\n{2} 카드를 한 장만 더 받고 멈춥니다.",
                bet, TableBet(bet, true), declarerName);

        /// <summary>
        /// 이름 뒤에 붙일 조사를 고른다. 받침이 있으면 <paramref name="withFinal"/>,
        /// 없으면 <paramref name="withoutFinal"/>이다.
        ///
        /// "이(가)"처럼 둘 다 적어 두면 화면이 지저분해지고 소리 내어 읽히지도 않는다.
        /// 한글이 아닌 글자로 끝나는 이름("플레이어 A")은 받침 없음으로 본다 —
        /// 이 게임의 이름은 A(에이)·B(비)처럼 모두 모음으로 소리 나기 때문이다.
        /// </summary>
        public static string Josa(string word, string withFinal, string withoutFinal)
        {
            if (string.IsNullOrEmpty(word)) return withoutFinal;

            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return withoutFinal;
            return (last - 0xAC00) % 28 == 0 ? withoutFinal : withFinal;
        }

        /// <summary>제출된 뇌물/판돈이 잔액 안에 들어오는지 검증한다(서버 권위 검증용).</summary>
        public static bool IsAffordable(int balance, int bribe, int bet) =>
            bribe >= 0 && bribe <= MaxBribe
            && bet >= MinBet
            && bribe + bet <= balance;

        /// <summary>
        /// 딜러 단독 승리(배신) 조건: 두 뇌물의 액수가 다르고, 딜러가 조작해서
        /// '뇌물을 적게 낸 쪽'을 겉보기 승자로 만든 경우.
        ///
        /// <b>안 낸 것도 적게 낸 것이다.</b> 예전에는 양쪽 다 0보다 커야 성립했다.
        /// 그래서 한 푼도 안 낸 쪽을 이기게 만들면 — 배신 중에서도 가장 노골적인
        /// 경우인데 — 규칙에 걸리지 않고 그냥 겉보기 승자의 승리로 끝났다. 뇌물을 낸
        /// 쪽에서 보면 상대가 0을 냈든 10을 냈든 '나보다 적게 냈다'는 사실은 같으므로,
        /// 0도 같은 자리에 둔다.
        ///
        /// 둘 다 0이면 여전히 발동하지 않는다 — 그때는 적게 낸 쪽이랄 것이 없다.
        /// </summary>
        public static bool IsDealerBetrayal(bool wasManipulated, int bribeA, int bribeB,
            MatchOutcome apparentOutcome)
        {
            if (!wasManipulated) return false;
            if (bribeA == bribeB) return false;           // 동액(둘 다 0 포함)이면 미발동
            bool lowerIsA          = bribeA < bribeB;
            bool apparentWinnerIsA = apparentOutcome == MatchOutcome.PlayerAWin;
            return lowerIsA == apparentWinnerIsA;          // 적게 낸 쪽이 이겼는가
        }

        /// <param name="apparentOutcome">블랙잭 겉보기 결과. PlayerAWin/PlayerBWin만 유효(무승부는 정산 대상 아님).</param>
        /// <param name="stake">이 라운드 판돈 S(두 베팅의 최소 매칭 결과). <see cref="ResolveStake"/> 참고.</param>
        /// <param name="foldedRole">
        /// 다이로 물러난 직군. None이 아니면 판돈 S 대신 <see cref="DieCost"/>만 오가고,
        /// 배신은 발동하지 않는다. 뇌물은 다이 여부와 무관하게 '이긴 쪽 것만 딜러가 챙긴다'는
        /// 일반 규칙을 그대로 따른다(다이한 쪽은 패자이므로 반납받는다).
        /// 고발이 성공하면 다이는 없던 일이 되어 이 값과 무관하게 판이 뒤집힌다.
        /// </param>
        /// <summary>
        /// 딜러가 <b>실제로 챙긴</b> 뇌물. 나머지는 반납되어 아무 데도 가지 않는다.
        ///
        /// 정산(<see cref="Compute"/>)과 기록 화면이 같은 답을 내야 해서 여기 한 곳에만 둔다.
        /// 낸 금액과 뺏긴 금액은 다르다 — 뇌물은 조작이 발각되거나 딜러가 공정하게 굴리면
        /// 전액 돌아오고, 조작하고도 들키지 않으면 <b>이긴 쪽 것만</b> 넘어간다.
        /// </summary>
        public static void BribesKept(
            bool wasManipulated,
            FinalWinner winner,
            bool accuseSucceeded,
            MatchOutcome apparentOutcome,
            int bribeA, int bribeB,
            out int keptFromA, out int keptFromB)
        {
            keptFromA = 0;
            keptFromB = 0;

            if (!wasManipulated) return;   // 공정 진행: 양쪽 전액 반납
            if (accuseSucceeded) return;   // 조작이 발각되면 전액 반납 — 고발의 본체다

            if (winner == FinalWinner.Dealer)
            {
                // 딜러 단독 승리(배신): 양쪽 뇌물을 모두 가져간다.
                keptFromA = bribeA;
                keptFromB = bribeB;
                return;
            }

            // 조작 + 미고발: 겉보기 승자의 뇌물만 획득. 지게 만든 쪽의 뇌물까지 챙기면
            // 뇌물을 받고 배신하는 것이 무손실이 되어 뇌물을 거는 쪽의 기대값이 무너진다.
            if (apparentOutcome == MatchOutcome.PlayerAWin) keptFromA = bribeA;
            else                                            keptFromB = bribeB;
        }

        public static SettlementResult Compute(
            AccusationChoice choice,
            AccusationResult result,
            MatchOutcome apparentOutcome,
            bool wasManipulated,
            int bribeA, int bribeB,
            int stake,
            PlayerRole foldedRole = PlayerRole.None)
        {
            int S = stake;
            bool apparentWinnerIsA = apparentOutcome == MatchOutcome.PlayerAWin;
            bool folded = foldedRole != PlayerRole.None;
            PlayerRole apparentLoserRole = apparentWinnerIsA
                ? PlayerRole.PlayerB
                : PlayerRole.PlayerA;
            string accuserName = ZooJackText.RoleName(apparentLoserRole);

            // 다이한 라운드는 승부가 성립하지 않았으므로 판돈 전액이 아니라 다이 비용만 오간다.
            int pot = folded ? DieCost(S) : S;

            int dA = 0, dB = 0, dDealer = 0;
            FinalWinner winner;
            string reason;

            bool accused        = choice == AccusationChoice.Accuse;
            bool accuseSucceeded = accused && result == AccusationResult.Success;

            if (accuseSucceeded)
            {
                // 고발 성공: 고발자(겉보기 패자)가 최종 승리 + 딜러 벌금.
                // 다이했더라도 조작이 밝혀지면 판이 통째로 뒤집히므로 판돈 전액(S)을 쓴다.
                if (apparentWinnerIsA) { dB += S + DealerFine; dA -= S; winner = FinalWinner.PlayerB; }
                else                   { dA += S + DealerFine; dB -= S; winner = FinalWinner.PlayerA; }
                dDealer -= DealerFine;
                reason = folded
                    ? ZooJackText.Get("Game.Judgment.AccuseSuccessAfterDie",
                        "고발 성공! 다이는 무효가 되고 딜러의 조작이 확인되었습니다. {0}는 벌금 {1}을 받고 승패를 뒤집었습니다.",
                        accuserName, DealerFine)
                    : ZooJackText.Get("Game.Judgment.AccuseSuccess",
                        "고발 성공! 딜러의 조작이 확인되었습니다. {0}는 벌금 {1}을 받고 승패를 뒤집었습니다.",
                        accuserName, DealerFine);
            }
            else if (accused && result == AccusationResult.Failed)
            {
                // 고발 실패: 겉보기 승자 유지 + 고발자에게 무고 벌금.
                // 벌금은 딜러에게 가지 않고 판에서 사라진다 — 딜러가 받으면 '무고 유도'가
                // 공정 진행의 수익원이 되어 안전 보상과 이중으로 얹힌다.
                if (apparentWinnerIsA) { dA += pot; dB -= pot + FalseAccuseFine; winner = FinalWinner.PlayerA; }
                else                   { dB += pot; dA -= pot + FalseAccuseFine; winner = FinalWinner.PlayerB; }
                reason = ZooJackText.Get("Game.Judgment.AccuseFailed",
                    "고발 실패! 조작이 없었습니다. {0}는 무고 벌금 {1}을 냈습니다.",
                    accuserName, FalseAccuseFine);
            }
            else if (!folded && IsDealerBetrayal(wasManipulated, bribeA, bribeB, apparentOutcome))
            {
                // 딜러 단독 승리: 양쪽에게서 판돈을 뜯고 뇌물까지 보유.
                // 다이한 라운드에는 발동하지 않는다 — 발동시키면 딜러가 '상대를 도망가게 만들기'로
                // 큰돈을 벌 수 있어 조작↔고발 루프가 무너진다.
                dA -= S; dB -= S; dDealer += 2 * S;
                winner = FinalWinner.Dealer;
                reason = ZooJackText.Get("Game.Judgment.DealerSoloWin",
                    "딜러 단독 승리! 더 많은 뇌물을 낸 쪽을 배신하고 판을 통째로 가져갔습니다.");
            }
            else
            {
                // 겉보기 승자 승리(패자 수용 또는 다이)
                if (apparentWinnerIsA) { dA += pot; dB -= pot; winner = FinalWinner.PlayerA; }
                else                   { dB += pot; dA -= pot; winner = FinalWinner.PlayerB; }
                reason = folded
                    ? ZooJackText.Get("Game.Judgment.DieAccepted",
                        "{0}가 다이한 결과에 승복했습니다. 판돈의 절반만 잃습니다.",
                        ZooJackText.RoleName(foldedRole))
                    : ZooJackText.Get("Game.Judgment.Accepted", "{0}가 결과에 승복했습니다.",
                        accuserName);
            }

            // ── 고발 보증금: 고발했다는 사실 자체에 붙는 비용 ──────────
            // 성공·실패를 가리지 않고 고발자에게서 빠져나가며, 딜러에게 가지 않고 소멸한다.
            // 고발자는 언제나 겉보기 패자다(IsLocalPlayerAllowedToAccuse).
            if (accused)
            {
                if (apparentWinnerIsA) dB -= AccusationDeposit;
                else                   dA -= AccusationDeposit;
            }

            // ── 뇌물 정산: 딜러는 '이긴 쪽 뇌물만' 선택적으로 챙긴다 ───
            // 누가 얼마를 뺏기는지는 BribesKept가 정한다. 기록 화면도 같은 함수를 읽으므로
            // '반납'이라 적힌 뇌물이 실제로는 빠져나가는 일이 생기지 않는다.
            BribesKept(wasManipulated, winner, accuseSucceeded, apparentOutcome,
                bribeA, bribeB, out int keptFromA, out int keptFromB);

            // 공정 진행에만 붙는 안전 보상. 조작 라운드에는 붙지 않으므로
            // '조작해서 뇌물 + 안전 보상' 이중 수익은 불가능하다.
            if (!wasManipulated) dDealer += SafeReward;

            dA -= keptFromA;
            dB -= keptFromB;
            dDealer += keptFromA + keptFromB;

            return new SettlementResult
            {
                PlayerADelta  = dA,
                PlayerBDelta  = dB,
                DealerDelta   = dDealer,
                ResolvedWinner = winner,
                ReasonText    = reason
            };
        }
    }
}
