namespace ZooJack
{
    /// <summary>
    /// 핫시트(한 기기에서 셋이 번갈아 하는 모드)의 좌석 식별자.
    ///
    /// 이 문자열은 <c>balances</c>·<c>bets</c>·<c>bribes</c> 딕셔너리의 키로 쓰인다.
    /// 리터럴로 흩어 두면 오타가 컴파일 타임에 걸리지 않고, 잘못된 키는 예외 대신
    /// "잔액 0"으로 조용히 나타나 원인을 찾기 어렵다. 그래서 한곳에 모은다.
    ///
    /// 네트워크 모드는 Fusion의 PlayerId(int)를 쓰므로 이 값을 쓰지 않는다.
    /// </summary>
    public static class HotseatSeats
    {
        public const string Dealer  = "dealer";
        public const string PlayerA = "player-a";
        public const string PlayerB = "player-b";

        /// <summary>화면에 보이는 기본 이름. 플레이어가 이름을 정하기 전의 값이다.</summary>
        public const string DealerDisplayName  = "Dealer";
        public const string PlayerADisplayName = "Player A";
        public const string PlayerBDisplayName = "Player B";
    }
}
