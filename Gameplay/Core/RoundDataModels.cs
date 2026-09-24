using System;
using System.Collections.Generic;

namespace ZooJack
{
    [Serializable]
    public class ManipulationRecord
    {
        public bool WasManipulated;
        public string ManipulatedByPlayerId;
        public DealerDecisionType DecisionType;
        public string FavoredPlayerId;
        public float DecisionTime;
        public int TurnIndex;
        public string CandidateSetId;
        public int ChosenCandidateIndex;
    }

    [Serializable]
    public class PublicRoundSnapshot
    {
        public GamePhase Phase;
        public int RoundIndex;
        public float DealerDecisionElapsedTime;
        public MatchOutcome PublicOutcome;
        public string ApparentWinnerPlayerId;
        public bool CanAccuse;
    }

    [Serializable]
    public class DealerPrivateSnapshot
    {
        public PlayerSeat PlayerA;
        public PlayerSeat PlayerB;
        public BribeData PlayerABribe;
        public BribeData PlayerBBribe;
        public BlackjackResult CurrentBlackjackResult;
        public CardCandidateSet CurrentCandidateSet;
        public float RemainingDecisionTime;
        public List<DealerDecisionType> AvailableDecisions = new List<DealerDecisionType>();
    }
}
