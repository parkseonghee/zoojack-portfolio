namespace ZooJack
{
    public static class BlackjackResultCalculator
    {
        public static BlackjackResult Calculate(BlackjackHand playerAHand, BlackjackHand playerBHand)
        {
            var scoreA = BlackjackScoreCalculator.Calculate(playerAHand);
            var scoreB = BlackjackScoreCalculator.Calculate(playerBHand);
            var outcome = DetermineOutcome(scoreA, scoreB);

            return new BlackjackResult
            {
                PlayerAHand = playerAHand,
                PlayerBHand = playerBHand,
                PlayerAScore = scoreA,
                PlayerBScore = scoreB,
                Outcome = outcome
            };
        }

        private static MatchOutcome DetermineOutcome(BlackjackScore scoreA, BlackjackScore scoreB)
        {
            // 둘 다 Bust 또는 동점 → 무승부
            if (scoreA.IsBust && scoreB.IsBust) return MatchOutcome.Tie;
            if (scoreA.IsBust) return MatchOutcome.PlayerBWin;
            if (scoreB.IsBust) return MatchOutcome.PlayerAWin;
            if (scoreA.BestValue == scoreB.BestValue) return MatchOutcome.Tie;
            return scoreA.BestValue > scoreB.BestValue ? MatchOutcome.PlayerAWin : MatchOutcome.PlayerBWin;
        }
    }
}
