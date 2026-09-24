using System;

namespace ZooJack
{
    [Serializable]
    public class FinalJudgmentData
    {
        public string ApparentWinnerPlayerId;
        public string AccuserPlayerId;
        public AccusationChoice AccusationChoice;
        public AccusationResult AccusationResult;
        public FinalWinner FinalWinner;
        public string ReasonText;
    }
}
