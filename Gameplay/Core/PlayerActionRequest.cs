using System;

namespace ZooJack
{
    [Serializable]
    public class PlayerActionRequest
    {
        public string SenderPlayerId;
        public PlayerActionType ActionType;
        public int BribeAmount;
        public int BetAmount;
        public DealerDecisionType DealerDecision;
        public AccusationChoice AccusationChoice;
        public PlayerRole TargetPlayerRole;
        public string CandidateSetId;
        public int ChosenCandidateIndex;

        public static PlayerActionRequest CreateBribe(string playerId, int amount)
        {
            return new PlayerActionRequest
            {
                SenderPlayerId = playerId,
                ActionType = PlayerActionType.SubmitBribe,
                BribeAmount = amount
            };
        }

        public static PlayerActionRequest CreateBet(string playerId, int amount)
        {
            return new PlayerActionRequest
            {
                SenderPlayerId = playerId,
                ActionType = PlayerActionType.SubmitBet,
                BetAmount = amount
            };
        }

        public static PlayerActionRequest CreateDealerDecision(string playerId, DealerDecisionType decision)
        {
            return new PlayerActionRequest
            {
                SenderPlayerId = playerId,
                ActionType = PlayerActionType.SubmitDealerDecision,
                DealerDecision = decision
            };
        }

        public static PlayerActionRequest CreateAccusation(string playerId, AccusationChoice choice)
        {
            return new PlayerActionRequest
            {
                SenderPlayerId = playerId,
                ActionType = PlayerActionType.SubmitAccusation,
                AccusationChoice = choice
            };
        }

        public static PlayerActionRequest CreateDealerCardChoice(string playerId, DealerCardChoice choice)
        {
            return new PlayerActionRequest
            {
                SenderPlayerId = playerId,
                ActionType = PlayerActionType.SubmitDealerCardChoice,
                TargetPlayerRole = choice == null ? PlayerRole.None : choice.TargetPlayerRole,
                CandidateSetId = choice == null ? string.Empty : choice.CandidateSetId,
                ChosenCandidateIndex = choice == null ? -1 : choice.ChosenCandidateIndex
            };
        }
    }
}
