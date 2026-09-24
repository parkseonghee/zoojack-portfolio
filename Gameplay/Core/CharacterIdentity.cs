namespace ZooJack
{
    /// <summary>
    /// 좌석과 캐릭터를 잇는 규칙(순수 계산).
    ///
    /// 캐릭터는 <b>사람에게 붙고 매치 내내 고정</b>이다. 역할은 3라운드마다 도는데
    /// (<see cref="RoundSettlement.RoleRotationPeriod"/>) 캐릭터가 역할을 따라가면
    /// 같은 사람이 3라운드마다 다른 종족이 되어, 잔액을 누가 잃고 있는지 추적할 수 없게 된다.
    ///
    /// 그래서 캐릭터는 <b>매치 시작 시점의 역할</b>로 한 번만 정하고, 그 뒤로는 좌석에 남는다.
    /// 로비에서는 반대 방향(<see cref="RoleFor"/>)으로 읽는다 — 사람이 동물을 고르면
    /// 그 동물의 자리가 곧 1라운드의 역할이 된다. 두 함수는 서로의 역함수라야
    /// "고른 동물이 앉은 자리"와 "그 자리에 그려지는 동물"이 어긋나지 않는다.
    /// </summary>
    public static class CharacterIdentity
    {
        /// <summary>매치 시작 시점의 역할로 캐릭터를 정한다. 1라운드의 기존 배역을 그대로 유지한다.</summary>
        public static CharacterId FromInitialRole(PlayerRole role) => role switch
        {
            PlayerRole.PlayerA => CharacterId.Rabbit,
            PlayerRole.PlayerB => CharacterId.Fox,
            PlayerRole.Dealer  => CharacterId.Croc,
            _                  => CharacterId.None
        };

        /// <summary>
        /// <see cref="FromInitialRole"/>의 역함수. 로비에서 고른 동물이 어느 자리에 앉는지 알려준다.
        /// </summary>
        public static PlayerRole RoleFor(CharacterId id) => id switch
        {
            CharacterId.Rabbit => PlayerRole.PlayerA,
            CharacterId.Fox    => PlayerRole.PlayerB,
            CharacterId.Croc   => PlayerRole.Dealer,
            _                  => PlayerRole.None
        };

        /// <summary>로비에서 고를 수 있는 캐릭터. 테이블에 앉는 순서(왼쪽·오른쪽·위)대로 둔다.</summary>
        public static readonly CharacterId[] Selectable =
        {
            CharacterId.Rabbit,
            CharacterId.Fox,
            CharacterId.Croc
        };

        /// <summary>
        /// 화면에 사람을 늘어놓는 차례. 머니바와 기록 화면이 <b>같은 차례</b>를 쓴다.
        ///
        /// <b>자리는 사람마다 고정이다.</b> 역할은 3라운드마다 돌지만 칸은 돌지 않는다 —
        /// 칸이 역할을 따라가면 4라운드에서 왼쪽 칸의 주인이 바뀌어, 매치 내내 한 사람의
        /// 돈을 눈으로 따라가던 것이 끊긴다.
        ///
        /// 1라운드의 배역 순서(플레이어 A · 딜러 · 플레이어 B)와 같다. 그때 누가 어디에
        /// 앉았는지가 곧 이 차례다(<see cref="FromInitialRole"/>).
        /// </summary>
        public static readonly CharacterId[] SeatOrder =
        {
            CharacterId.Rabbit,
            CharacterId.Croc,
            CharacterId.Fox
        };

        /// <summary><see cref="SeatOrder"/>에서 이 캐릭터의 칸 번호. 없으면 -1.</summary>
        public static int SeatIndex(CharacterId id)
        {
            for (int i = 0; i < SeatOrder.Length; i++)
                if (SeatOrder[i] == id) return i;
            return -1;
        }

        /// <summary>UI 표기용 이름.</summary>
        public static string NameKo(CharacterId id) => ZooJackText.CharacterName(id);
    }
}
