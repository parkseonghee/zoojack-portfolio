namespace ZooJack
{
    public static class BlackjackScoreCalculator
    {
        private const int BustThreshold = 21;
        private const int AceHighValue = 11;
        private const int AceLowConversion = 10; // 11 → 1 = subtract 10
        private const int FaceCardValue = 10;

        public static BlackjackScore Calculate(BlackjackHand hand)
        {
            int total = 0;
            int aceCount = 0;

            foreach (var card in hand.Cards)
            {
                if (card.Rank == BlackjackCardRank.Ace)
                {
                    aceCount++;
                    total += AceHighValue;
                }
                else if (card.Rank >= BlackjackCardRank.Jack)
                {
                    total += FaceCardValue;
                }
                else
                {
                    total += (int)card.Rank;
                }
            }

            // Ace를 11 → 1로 하나씩 전환해 Bust 탈출
            while (total > BustThreshold && aceCount > 0)
            {
                total -= AceLowConversion;
                aceCount--;
            }

            return new BlackjackScore
            {
                BestValue = total,
                IsBust = total > BustThreshold,
                IsSoft = aceCount > 0 && total <= BustThreshold
            };
        }
    }
}
