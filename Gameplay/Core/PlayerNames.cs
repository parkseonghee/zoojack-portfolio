namespace ZooJack
{
    /// <summary>
    /// 자리에 앉은 사람의 이름을 묻는 창구.
    ///
    /// <b>왜 사이에 한 겹을 두는가.</b> 이름은 방(<c>FusionGameState</c>)이 들고 있는데,
    /// 그것을 그리는 곳은 명패·채팅처럼 네트워크를 모르는 화면 코드다. 화면이 방을 직접
    /// 알면 핫시트(혼자 하는 판)에서는 있지도 않은 것을 참조하게 된다. 그래서 화면은
    /// "이 자리 사람 이름이 뭔가"만 묻고, 아무도 답하지 않으면 빈 문자열을 받는다 —
    /// 핫시트에서 이름이 안 붙는 것이 곧 맞는 동작이다.
    ///
    /// <see cref="MatchSurrender"/>·<see cref="ChatRelay"/>와 같은 결의 정적 창구다.
    /// 내 이름을 저장하는 일은 여기가 아니라 <see cref="GameSettings"/>가 맡는다 —
    /// 여기는 <b>방의 이름표</b>를, 저기는 <b>내 설정</b>을 본다.
    /// </summary>
    public static class PlayerNames
    {
        /// <summary>
        /// 이름 길이 상한.
        ///
        /// 명패는 배역 이름과 초까지 함께 이고 있고, 채팅은 한 줄에 이름과 말이 같이
        /// 들어간다. 열 글자를 넘기면 그 둘이 먼저 무너진다. 입력칸도 같은 값으로 막으므로
        /// (환경설정) 애초에 더 칠 수 없다.
        /// </summary>
        public const int MaxLength = 10;

        /// <summary>이름표를 들고 있는 쪽. 지금은 네트워크 방 하나뿐이다.</summary>
        public interface IBoard
        {
            /// <summary>지금 이 배역을 맡은 사람의 이름. 없으면 빈 문자열.</summary>
            string NameFor(PlayerRole role);
        }

        private static IBoard board;

        /// <summary>방을 열면서 자기를 등록한다.</summary>
        public static void SetBoard(IBoard value) => board = value;

        /// <summary>
        /// 등록을 거둔다. <b>자기 것일 때만</b> 지운다 — 씬을 넘어갈 때 옛 방의 정리가
        /// 새 방의 등록 뒤에 올 수 있고, 그때 무조건 비우면 새 방에 이름이 붙지 않는다.
        /// </summary>
        public static void ClearBoard(IBoard value)
        {
            if (board == value) board = null;
        }

        /// <summary>이 배역을 맡은 사람의 이름. 방이 없거나 아직 안 알려졌으면 빈 문자열.</summary>
        public static string Of(PlayerRole role) =>
            board == null ? string.Empty : board.NameFor(role) ?? string.Empty;

        /// <summary>
        /// 이 자리에 적을 이름. <b>이름을 지었으면 이름 하나만</b> 남고 배역은 붙지 않는다.
        ///
        /// 배역은 이미 화면이 말하고 있다 — 명패는 자리마다 정해진 곳에 서 있고, 상단
        /// 상태줄이 지금 누구 차례인지 적는다. 거기에 배역을 한 번 더 얹으면 이름이
        /// 묻힌다. 사람을 알아보라고 짓는 이름인데 그러면 짓는 뜻이 없다.
        ///
        /// 이름이 없으면(핫시트, 아직 안 지은 사람) <paramref name="fallback"/>이 그대로
        /// 남는다 — 부르는 쪽이 배역을 넣으면 배역이, 동물을 넣으면 동물이 뜬다.
        /// </summary>
        public static string Label(PlayerRole role, string fallback)
        {
            string name = Of(role);
            return string.IsNullOrEmpty(name) ? fallback : name;
        }

        /// <summary>
        /// 남에게 보내도 되는 이름으로 다듬는다. 앞뒤 공백과 제어 문자를 떼고
        /// <see cref="MaxLength"/>까지 자른다.
        ///
        /// <b>채팅과 같은 손을 탄다</b>(<see cref="ChatLog.Sanitize"/>). 이름도 결국 채팅
        /// 줄과 명패에 TMP로 그려지므로, 꺾쇠를 그대로 두면 <c>&lt;size=400&gt;</c> 한 번으로
        /// 화면이 뒤집힌다. 두 곳이 같은 규칙을 쓰는 편이 낫다 — 규칙이 갈라지면 한쪽만
        /// 막힌 채 남는다.
        /// </summary>
        public static string Sanitize(string raw)
        {
            string text = ChatLog.Sanitize(raw);
            if (text.Length <= MaxLength) return text;

            // 열 글자째가 대리 쌍(이모지 같은 것)의 앞짝이면 거기서 자를 수 없다.
            // 반쪽만 남기면 글자가 아니라 깨진 부호가 된다.
            int cut = MaxLength;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            return text.Substring(0, cut);
        }
    }
}
