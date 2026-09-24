using System.Collections.Generic;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 지난 라운드와 지난 매치의 결과를 담아 두는 곳. 기록 화면(<see cref="HistoryPanel"/>)이
    /// 여기서 읽는다.
    ///
    /// <b>왜 씬 밖의 static인가.</b> 랭킹은 매치 <b>여러 판</b>에 걸쳐 쌓인다 — 매치가 끝나면
    /// 방 로비로 돌아갔다가 게임 씬을 다시 여는데, 씬에 붙은 오브젝트로 들고 있으면 그때마다
    /// 사라진다. <see cref="GameAudio"/>가 씬 밖에 사는 것과 같은 이유다.
    ///
    /// <b>디스크에 쓰지 않는다.</b> 여기 담긴 사람은 토끼·여우·악어인데, 그 배정은 이번
    /// 세션의 자리 배치에서만 뜻이 있다. 게임을 껐다 켜면 같은 토끼가 같은 사람이 아니므로
    /// 남겨 두면 거짓말이 된다.
    ///
    /// <b>끝난 뒤에만 담는다.</b> 뇌물 금액·조작 여부·고발 여부는 모두 최종 판정
    /// 브로드캐스트(<c>Rpc_BroadcastFinalJudgment</c>)로만 들어온다 — 즉 그 라운드가 이미
    /// 끝난 뒤다. 진행 중인 라운드는 기록되지 않으므로 이 화면을 열어 답을 미리 볼 방법이 없다.
    ///
    /// 뇌물이 여기 들어오는 것은 <b>라운드가 끝난 뒤의 공개</b>다. 뇌물 단계가 도는 동안에는
    /// 여전히 딜러 말고는 아무도 금액을 알 수 없다(<c>secretBribes</c>).
    /// </summary>
    public static class MatchHistory
    {
        /// <summary>한 역할이 그 라운드에 얼마를 잃거나 얻었는지.</summary>
        public readonly struct Change
        {
            /// <summary>그 라운드에 이 역할을 맡았던 사람(토끼·여우·악어).</summary>
            public readonly CharacterId Character;

            /// <summary>그 라운드의 역할. 3라운드마다 도므로 라운드마다 다를 수 있다.</summary>
            public readonly PlayerRole Role;

            public readonly int Delta;

            /// <summary>
            /// 플레이어 자리에서는 <b>딜러에게 낸 뇌물</b>, 딜러 자리에서는 <b>실제로 챙긴 합계</b>.
            /// 라운드가 끝난 뒤에만 채워진다.
            /// </summary>
            public readonly int Bribe;

            /// <summary>
            /// 낸 뇌물 중 딜러에게 넘어간 몫. 나머지는 반납된 것이다.
            /// 딜러 자리에서는 <see cref="Bribe"/>와 같다.
            /// </summary>
            public readonly int BribeKept;

            public Change(CharacterId character, PlayerRole role, int delta,
                int bribe = 0, int bribeKept = 0)
            {
                Character = character;
                Role = role;
                Delta = delta;
                Bribe = bribe;
                BribeKept = bribeKept;
            }
        }

        /// <summary>라운드 한 판의 결과.</summary>
        public readonly struct RoundEntry
        {
            public readonly int Round;

            /// <summary>플레이어 A · 딜러 · 플레이어 B 순서. 화면에 그 순서로 늘어선다.</summary>
            public readonly Change[] Changes;

            /// <summary>딜러가 카드를 조작했는지.</summary>
            public readonly bool Manipulated;

            /// <summary>겉보기 패자가 고발했는지, 승복했는지.</summary>
            public readonly AccusationChoice Accusation;

            /// <summary>고발했다면 그것이 맞았는지.</summary>
            public readonly AccusationResult AccusationResult;

            public readonly FinalWinner Winner;

            public RoundEntry(int round, Change[] changes, bool manipulated,
                AccusationChoice accusation, AccusationResult accusationResult, FinalWinner winner)
            {
                Round = round;
                Changes = changes;
                Manipulated = manipulated;
                Accusation = accusation;
                AccusationResult = accusationResult;
                Winner = winner;
            }
        }

        /// <summary>매치가 끝났을 때 한 사람의 성적.</summary>
        public readonly struct Standing
        {
            public readonly CharacterId Character;

            /// <summary>매치가 끝난 시점의 역할.</summary>
            public readonly PlayerRole Role;

            public readonly int Balance;
            public readonly bool Bankrupt;

            /// <summary>스스로 판을 접었는지(항복). 잔액과 무관하게 꼴찌다.</summary>
            public readonly bool Surrendered;

            public Standing(CharacterId character, PlayerRole role, int balance, bool bankrupt,
                bool surrendered = false)
            {
                Character = character;
                Role = role;
                Balance = balance;
                Bankrupt = bankrupt;
                Surrendered = surrendered;
            }
        }

        /// <summary>매치 한 판의 최종 순위.</summary>
        public readonly struct RankEntry
        {
            /// <summary>몇 번째 매치였는지(1부터).</summary>
            public readonly int MatchNumber;

            /// <summary>1등부터 차례로.</summary>
            public readonly Standing[] Ranked;

            public RankEntry(int matchNumber, Standing[] ranked)
            {
                MatchNumber = matchNumber;
                Ranked = ranked;
            }
        }

        private static readonly List<RoundEntry> rounds = new List<RoundEntry>();
        private static readonly List<RankEntry> ranks = new List<RankEntry>();

        /// <summary>
        /// 이 매치는 이미 끝났다. 다음 라운드 기록이 들어오면 새 매치로 보고 라운드 목록을 비운다.
        ///
        /// 라운드 번호만 보고 판단할 수는 없다 — 1라운드에 파산해서 끝난 뒤 다시 시작하면
        /// 새 1라운드와 지난 1라운드가 같은 번호라 구별이 안 된다.
        /// </summary>
        private static bool matchClosed;

        /// <summary>
        /// 내용이 바뀔 때마다 올라간다. 화면이 "다시 그릴 것이 있는지"를 이 숫자로 판단한다 —
        /// 목록을 매 프레임 훑어 비교하는 것보다 싸고, 무엇이 바뀌었는지 알 필요도 없다.
        /// </summary>
        public static int Version { get; private set; }

        /// <summary>지금 매치의 라운드 기록. 1라운드부터 차례로.</summary>
        public static IReadOnlyList<RoundEntry> Rounds => rounds;

        /// <summary>이 세션에서 끝난 매치들의 순위. 첫 매치부터 차례로.</summary>
        public static IReadOnlyList<RankEntry> Ranks => ranks;

        /// <summary>
        /// 플레이를 시작할 때마다 비운다. static은 도메인이 다시 실리기 전까지 남으므로,
        /// 에디터에서 두 번째로 플레이를 누르면 지난 판의 기록이 그대로 얹힌다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Reset()
        {
            rounds.Clear();
            ranks.Clear();
            matchClosed = false;
            Version = 0;
        }

        /// <summary>
        /// 새 매치가 열렸다. 지난 매치의 라운드 기록을 비우고 이 매치를 연다.
        /// 랭킹은 건드리지 않는다 — 그쪽은 세션 내내 쌓이는 것이다.
        ///
        /// <b>왜 따로 불러야 하는가.</b> 예전에는 "닫힌 매치에 1라운드가 들어오면 그때
        /// 비운다"는 규칙 하나로 대신했다. 그 규칙은 두 곳에서 어긋난다.
        ///
        /// ① 새 매치의 1라운드가 <b>끝나기 전까지</b> 기록 화면이 지난 매치의 아홉 줄을
        ///    그대로 보여 준다. 새 판을 시작해 놓고 지난 판을 읽게 된다.
        ///
        /// ② 한 라운드도 끝나지 않고 끝난 매치(1라운드 항복)는 그 규칙에 걸릴 일이 없어
        ///    매치가 닫힌 채로 남는다. 그러면 <see cref="RecordRanking"/>이 다음 매치의
        ///    순위를 통째로 버린다 — 실제로 항복한 판이 랭킹에 안 남았다.
        ///
        /// 여러 번 불러도 안전하다.
        /// </summary>
        public static void BeginMatch()
        {
            if (rounds.Count == 0 && !matchClosed) return;

            rounds.Clear();
            matchClosed = false;
            Version++;
        }

        /// <summary>
        /// 라운드 하나의 결과를 적는다. <b>같은 라운드를 두 번 적어도 안전하다</b> —
        /// 화면은 상태가 조금이라도 바뀌면 다시 그려지고, 그때마다 이 함수를 지나기 때문이다.
        ///
        /// <b>부르는 자리가 중요하다.</b> 최종 판정 단계에 <i>들어서는</i> 순간이 아니라
        /// 두구두구가 멈춰 <i>승자가 공개되는</i> 순간에 불러야 한다. 앞쪽에서 부르면
        /// 주사위가 아직 구르는 동안 이 화면에 답이 먼저 떠서, 기록 화면이 결과를
        /// 미리 보는 창구가 된다.
        /// </summary>
        /// <param name="round">라운드 번호(1부터).</param>
        public static void RecordRound(
            int round,
            Change playerA, Change dealer, Change playerB,
            bool manipulated,
            AccusationChoice accusation, AccusationResult accusationResult,
            FinalWinner winner)
        {
            if (round <= 0) return;

            if (matchClosed)
            {
                // 매치가 끝난 뒤에도 판정 화면은 몇 번씩 다시 그려지고, 그때마다 방금 끝난
                // 매치의 <b>마지막 라운드</b>가 여기로 다시 실려 온다. 그것을 새 매치로
                // 오해하면 아래에서 목록을 비워, 방금 쌓은 아홉 줄이 통째로 날아간다.
                //
                // 새 매치는 반드시 1라운드부터 시작하므로 1라운드가 아니면 메아리다.
                // (지난 매치가 1라운드에 파산으로 끝났다면 구별할 수 없지만, 그때는
                //  비우고 다시 적어도 같은 한 줄이라 보이는 것이 달라지지 않는다.)
                if (round != 1) return;

                // 지난 매치의 라운드는 이제 남의 이야기다. 랭킹만 쌓아 두고 여기는 비운다.
                rounds.Clear();
                matchClosed = false;
                Version++;
            }

            foreach (var existing in rounds)
                if (existing.Round == round) return;   // 이미 적었다

            rounds.Add(new RoundEntry(
                round,
                new[] { playerA, dealer, playerB },
                manipulated, accusation, accusationResult, winner));
            Version++;
        }

        /// <summary>
        /// 매치가 끝난 순위를 적고 이 매치를 닫는다. <b>여러 번 불러도 한 번만 적힌다</b> —
        /// 매치 종료 화면도 상태가 바뀔 때마다 다시 그려진다.
        /// </summary>
        /// <param name="ranked">1등부터 정렬된 성적.</param>
        public static void RecordRanking(IReadOnlyList<Standing> ranked)
        {
            if (matchClosed || ranked == null || ranked.Count == 0) return;

            var copy = new Standing[ranked.Count];
            for (int i = 0; i < ranked.Count; i++) copy[i] = ranked[i];

            ranks.Add(new RankEntry(ranks.Count + 1, copy));
            matchClosed = true;
            Version++;
        }
    }
}
