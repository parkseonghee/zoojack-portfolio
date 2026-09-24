namespace ZooJack
{
    /// <summary>
    /// Maps host-approved public actions to the two lines shown by ToastView.
    /// Secret amounts and card indices are deliberately not part of this API.
    /// </summary>
    public static class ActionAnnouncementMapper
    {
        public static bool TryMap(
            PlayerRole actorRole,
            PlayerActionType actionType,
            out string actorText,
            out string actionText)
        {
            actorText = ActorText(actorRole);

            // 항복만 두 줄이 아니라 한 문장이다. "딜러 / 항복"으로 쪼개 놓으면 방금
            // 판이 끝났다는 사실이 히트·스탠드와 똑같은 무게로 지나간다.
            if (actionType == PlayerActionType.RequestSurrender)
            {
                actionText = SurrenderFollowUp;
                if (string.IsNullOrEmpty(actorText)) return false;
                actorText = SurrenderNotice(actorRole);
                return true;   // 딜러도 항복한다. 자리를 가리지 않는다
            }

            actionText = actionType switch
            {
                PlayerActionType.RequestHit => ZooJackText.Get("Game.Decision.Hit.Title", "히트"),
                PlayerActionType.RequestStand => ZooJackText.Get("Game.Decision.Stand.Title", "스탠드"),
                PlayerActionType.RequestDie => ZooJackText.Get("Game.Decision.Die.Title", "다이"),
                PlayerActionType.RequestDoubleDown => ZooJackText.Get("Game.Decision.DoubleDown.Title", "더블다운"),
                PlayerActionType.SubmitDealerCardChoice => ZooJackText.Get("Game.Action.Deal", "카드 배분"),
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(actorText) || string.IsNullOrEmpty(actionText))
                return false;

            bool dealerAction = actionType == PlayerActionType.SubmitDealerCardChoice;
            return dealerAction
                ? actorRole == PlayerRole.Dealer
                : actorRole == PlayerRole.PlayerA || actorRole == PlayerRole.PlayerB;
        }

        /// <summary>
        /// 항복 알림 한 줄. 매치 종료 카드의 사유도 <b>같은 문장</b>을 쓴다 —
        /// 알림과 카드가 다른 말을 하면 무엇 때문에 끝났는지 두 번 읽어야 한다.
        ///
        /// 세 배역 이름이 모두 모음으로 끝나므로(딜러·플레이어 에이·플레이어 비)
        /// 조사는 언제나 '가'다.
        /// </summary>
        public static string SurrenderNotice(PlayerRole role) =>
            ActorText(role) is { Length: > 0 } who
                ? ZooJackText.Get("Game.Action.SurrenderNotice", "{0}가 항복했습니다.", who)
                : string.Empty;

        /// <summary>항복 알림의 둘째 줄. 왜 화면이 갑자기 순위표로 바뀌는지를 말해 준다.</summary>
        public static string SurrenderFollowUp =>
            ZooJackText.Get("Game.Action.SurrenderFollowUp", "매치를 종료합니다.");

        /// <summary>
        /// 배역을 사람에게 보이는 말로. 알림·채팅이 같은 낱말을 쓰도록 여기 한 곳에 둔다 —
        /// 같은 자리를 화면마다 다르게 부르면 그것이 같은 자리인 줄 모른다.
        /// 자리가 없으면 빈 문자열이다.
        /// </summary>
        public static string RoleText(PlayerRole role) => ActorText(role);

        private static string ActorText(PlayerRole role) =>
            role == PlayerRole.None ? string.Empty : ZooJackText.RoleName(role);
    }
}
