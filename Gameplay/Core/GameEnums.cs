namespace ZooJack
{
    public enum PlayerRole
    {
        None,
        PlayerA,
        PlayerB,
        Dealer,
        Spectator
    }

    /// <summary>
    /// 동물 캐릭터. <b>사람(좌석)에 붙는 값이지 역할에 붙는 값이 아니다.</b>
    /// 역할은 3라운드마다 도는데(<see cref="RoundSettlement.RoleRotationPeriod"/>)
    /// 캐릭터까지 따라 돌면 같은 사람이 3라운드마다 다른 종족이 된다.
    /// </summary>
    public enum CharacterId
    {
        None,
        Rabbit,
        Fox,
        Croc
    }

    public enum GamePhase
    {
        WaitingForPlayers,
        RoleAssignment,
        BribeSelection,
        SystemResultGeneration,
        CardCandidateGeneration,
        DealerCardDistribution,
        BetSelection,
        PlayerDecision,
        BlackjackResultCalculation,
        DealerDecision,
        ResultReveal,
        Accusation,
        FinalJudgment,
        TieRedeal,
        RoundEnd
        // 새 값은 열거형 끝에 붙인다 — 중간에 끼우면 기존 값들의 정수가 밀린다.
    }

    public enum DealerDecisionType
    {
        None,
        ApproveSystemResult,
        ManipulateForPlayerA,
        ManipulateForPlayerB
    }

    public enum AccusationChoice
    {
        None,
        Accuse,
        AcceptResult
    }

    public enum AccusationResult
    {
        None,
        Success,
        Failed
    }

    public enum MatchOutcome
    {
        None,
        PlayerAWin,
        PlayerBWin,
        Tie
    }

    public enum FinalWinner
    {
        None,
        PlayerA,
        PlayerB,
        Dealer
    }

    public enum PlayerActionType
    {
        SubmitBribe,
        SubmitBet,
        RequestHit,
        RequestStand,
        RequestDie,
        RequestDoubleDown,
        SubmitDealerCardChoice,
        SubmitDealerDecision,
        SubmitAccusation,
        Ready,
        RequestStartGame,
        LeaveRoom,
        SelectRole,

        /// <summary>항복 — 누른 사람의 자리와 상관없이 그 자리에서 매치가 끝난다.</summary>
        RequestSurrender
    }
}
