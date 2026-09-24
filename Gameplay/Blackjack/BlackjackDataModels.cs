using System;
using System.Collections.Generic;

namespace ZooJack
{
    public enum BlackjackCardRank
    {
        Ace = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13
    }

    public enum BlackjackCardSuit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }

    public enum BlackjackDealStep
    {
        InitialDeal,
        PlayerAHit,
        PlayerBHit
    }

    [Serializable]
    public class BlackjackCard
    {
        public BlackjackCardRank Rank;
        public BlackjackCardSuit Suit;
    }

    [Serializable]
    public class BlackjackHand
    {
        public string PlayerId;
        public List<BlackjackCard> Cards = new List<BlackjackCard>();
    }

    [Serializable]
    public class BlackjackScore
    {
        public int BestValue;
        public bool IsBust;
        public bool IsSoft;
    }

    [Serializable]
    public class BlackjackResult
    {
        public BlackjackHand PlayerAHand;
        public BlackjackHand PlayerBHand;
        public BlackjackScore PlayerAScore;
        public BlackjackScore PlayerBScore;
        public MatchOutcome Outcome;
    }

    [Serializable]
    public class CardCandidateSet
    {
        public string CandidateSetId;
        public PlayerRole TargetPlayerRole;
        public BlackjackDealStep DealStep;
        public List<BlackjackCard> Candidates = new List<BlackjackCard>();
    }

    [Serializable]
    public class DealerCardChoice
    {
        public PlayerRole TargetPlayerRole;
        public string CandidateSetId;
        public int ChosenCandidateIndex;
    }
}
