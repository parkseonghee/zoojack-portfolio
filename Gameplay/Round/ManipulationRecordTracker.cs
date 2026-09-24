namespace ZooJack
{
    public static class ManipulationRecordTracker
    {
        // DealerCardChoice와 후보 세트를 받아 ManipulationRecord를 생성하고 RoundContext에 추가한다.
        public static ManipulationRecord CreateAndRecord(
            RoundContext context,
            DealerCardChoice choice,
            CardCandidateSet candidateSet,
            string dealerPlayerId,
            int turnIndex,
            float decisionTime)
        {
            bool wasManipulated = choice.ChosenCandidateIndex > 0;

            var record = new ManipulationRecord
            {
                WasManipulated = wasManipulated,
                ManipulatedByPlayerId = wasManipulated ? dealerPlayerId : string.Empty,
                DecisionType = DealerDecisionType.None, // 실제 의미는 후보 인덱스 기반이므로 None 유지
                FavoredPlayerId = string.Empty,
                DecisionTime = decisionTime,
                TurnIndex = turnIndex,
                CandidateSetId = candidateSet.CandidateSetId,
                ChosenCandidateIndex = choice.ChosenCandidateIndex
            };

            context.ManipulationRecords.Add(record);
            return record;
        }

        public static bool WasAnyManipulationRecorded(RoundContext context)
        {
            foreach (var record in context.ManipulationRecords)
            {
                if (record.WasManipulated) return true;
            }
            return false;
        }
    }
}
