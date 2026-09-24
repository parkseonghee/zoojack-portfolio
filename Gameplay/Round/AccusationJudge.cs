namespace ZooJack
{
    public static class AccusationJudge
    {
        public static FinalJudgmentData Judge(
            RoundContext context,
            AccusationChoice choice,
            string accuserPlayerId,
            string apparentWinnerPlayerId)
        {
            // 고발하지 않으면 겉보기 결과 유지
            if (choice == AccusationChoice.AcceptResult || choice == AccusationChoice.None)
            {
                return new FinalJudgmentData
                {
                    ApparentWinnerPlayerId = apparentWinnerPlayerId,
                    AccuserPlayerId = accuserPlayerId,
                    AccusationChoice = choice,
                    AccusationResult = AccusationResult.None,
                    FinalWinner = ResolveFinalWinnerFromPlayerId(apparentWinnerPlayerId, context),
                    ReasonText = ZooJackText.Get(
                        "Game.Judgment.Accepted", "패자가 결과를 수용했습니다.")
                };
            }

            bool wasManipulated = ManipulationRecordTracker.WasAnyManipulationRecorded(context);

            if (wasManipulated)
            {
                // 고발 성공: 패배자(고발자)가 최종 승리
                return new FinalJudgmentData
                {
                    ApparentWinnerPlayerId = apparentWinnerPlayerId,
                    AccuserPlayerId = accuserPlayerId,
                    AccusationChoice = choice,
                    AccusationResult = AccusationResult.Success,
                    FinalWinner = ResolveFinalWinnerFromPlayerId(accuserPlayerId, context),
                    ReasonText = ZooJackText.Get(
                        "Game.Judgment.AccusationSuccess", "고발 성공: 딜러의 카드 조작이 확인되었습니다.")
                };
            }
            else
            {
                // 고발 실패: 겉보기 승자가 최종 승리, 고발자는 추가 페널티
                return new FinalJudgmentData
                {
                    ApparentWinnerPlayerId = apparentWinnerPlayerId,
                    AccuserPlayerId = accuserPlayerId,
                    AccusationChoice = choice,
                    AccusationResult = AccusationResult.Failed,
                    FinalWinner = ResolveFinalWinnerFromPlayerId(apparentWinnerPlayerId, context),
                    ReasonText = ZooJackText.Get(
                        "Game.Judgment.AccusationFailed",
                        "고발 실패: 조작 기록 없음. 고발자에게 페널티가 부여됩니다.")
                };
            }
        }

        private static FinalWinner ResolveFinalWinnerFromPlayerId(string playerId, RoundContext context)
        {
            if (playerId == context.PlayerAHand.PlayerId) return FinalWinner.PlayerA;
            if (playerId == context.PlayerBHand.PlayerId) return FinalWinner.PlayerB;
            return FinalWinner.Dealer;
        }
    }
}
