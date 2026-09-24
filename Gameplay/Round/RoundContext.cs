using System.Collections.Generic;

namespace ZooJack
{
    public class RoundContext
    {
        public readonly BlackjackHand PlayerAHand;
        public readonly BlackjackHand PlayerBHand;
        public readonly List<ManipulationRecord> ManipulationRecords = new List<ManipulationRecord>();
        public CardCandidateSet CurrentCandidateSet;
        public bool PlayerAStood;
        public bool PlayerBStood;
        public int InitialDealStep; // 0=A 1st, 1=B 1st, 2=A 2nd, 3=B 2nd, 4=complete
        public PlayerRole FoldedRole; // 다이로 물러난 플레이어. None = 아무도 다이하지 않음

        /// <summary>
        /// 더블다운을 선언했는지. 둘이 서로에게 걸림돌이 되지 않도록 자리마다 따로 센다 —
        /// 상대가 먼저 걸었다고 내 선택이 사라지면 곤란하다.
        ///
        /// 무승부 재딜에서도 지워지지 않는다. 지워버리면 재딜마다 다시 걸어 판이 불어나고,
        /// 손패 3장 상한도 재딜 뒤에 풀려 버린다.
        /// </summary>
        public bool PlayerADoubled;
        public bool PlayerBDoubled;

        public RoundContext(string playerAId, string playerBId)
        {
            PlayerAHand = new BlackjackHand { PlayerId = playerAId };
            PlayerBHand = new BlackjackHand { PlayerId = playerBId };
        }

        public bool IsInitialDealComplete => InitialDealStep >= 4;

        public bool HasFolded => FoldedRole != PlayerRole.None;

        /// <summary>이 자리가 이번 라운드에 더블다운을 걸었는지.</summary>
        public bool HasDoubled(PlayerRole role) =>
            role == PlayerRole.PlayerA ? PlayerADoubled
            : role == PlayerRole.PlayerB && PlayerBDoubled;

        /// <summary>둘 중 한 명이라도 걸었는지. 판돈을 두 번 올리지 않으려고 본다.</summary>
        public bool AnyoneDoubled => PlayerADoubled || PlayerBDoubled;

        public void MarkDoubled(PlayerRole role)
        {
            if (role == PlayerRole.PlayerA) PlayerADoubled = true;
            else if (role == PlayerRole.PlayerB) PlayerBDoubled = true;
        }

        public PlayerRole GetInitialDealTarget() =>
            InitialDealStep % 2 == 0 ? PlayerRole.PlayerA : PlayerRole.PlayerB;

        public BlackjackHand GetHand(PlayerRole role) =>
            role == PlayerRole.PlayerA ? PlayerAHand : PlayerBHand;

        public bool HasStood(PlayerRole role) =>
            role == PlayerRole.PlayerA ? PlayerAStood : PlayerBStood;

        public void MarkStood(PlayerRole role)
        {
            if (role == PlayerRole.PlayerA) PlayerAStood = true;
            else if (role == PlayerRole.PlayerB) PlayerBStood = true;
        }

        public bool IsBothDecisionDone(BlackjackScore playerAScore, BlackjackScore playerBScore) =>
            (PlayerAStood || playerAScore.IsBust) && (PlayerBStood || playerBScore.IsBust);
    }
}
