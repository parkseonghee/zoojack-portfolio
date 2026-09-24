using System;
using System.Collections.Generic;

namespace ZooJack
{
    public class BlackjackDeckService
    {
        public const int DefaultCandidateCount = 3;
        private const int ReshuffleThreshold = 10;

        private readonly List<BlackjackCard> deck = new List<BlackjackCard>(52);
        private readonly Random random;

        public BlackjackDeckService() : this(new Random()) { }

        public BlackjackDeckService(Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            RebuildAndShuffle();
        }

        public CardCandidateSet GenerateCandidateSet(PlayerRole targetRole, BlackjackDealStep dealStep, string candidateSetId = null)
        {
            if (deck.Count < ReshuffleThreshold)
                RebuildAndShuffle();

            var candidates = new List<BlackjackCard>(DefaultCandidateCount);
            int drawCount = Math.Min(DefaultCandidateCount, deck.Count);
            for (int i = 0; i < drawCount; i++)
            {
                int last = deck.Count - 1;
                candidates.Add(deck[last]);
                deck.RemoveAt(last);
            }

            return new CardCandidateSet
            {
                CandidateSetId = candidateSetId ?? GenerateId(),
                TargetPlayerRole = targetRole,
                DealStep = dealStep,
                Candidates = candidates
            };
        }

        private void RebuildAndShuffle()
        {
            deck.Clear();
            foreach (BlackjackCardSuit suit in Enum.GetValues(typeof(BlackjackCardSuit)))
            foreach (BlackjackCardRank rank in Enum.GetValues(typeof(BlackjackCardRank)))
                deck.Add(new BlackjackCard { Rank = rank, Suit = suit });
            Shuffle();
        }

        private void Shuffle()
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = deck[i];
                deck[i] = deck[j];
                deck[j] = temp;
            }
        }

        private static string GenerateId() =>
            Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
