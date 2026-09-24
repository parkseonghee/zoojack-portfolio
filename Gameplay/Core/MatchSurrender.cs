namespace ZooJack
{
    /// <summary>
    /// 환경설정의 '항복'과 <b>지금 굴러가는 매치</b>를 잇는 창구.
    ///
    /// <b>왜 사이에 한 겹을 두는가.</b> 항복 버튼은 환경설정 화면 안에 있는데, 그 화면은
    /// 게임 씬에도 로비의 방 화면에도 같은 도구로 만들어 붙는다. 버튼이 매치를 직접
    /// 알면 로비 쪽 사본은 있지도 않은 것을 참조하게 되고, 무엇보다 핫시트냐 네트워크냐에
    /// 따라 항복하는 방법이 다르다(한쪽은 그 자리에서 화면을 바꾸고, 다른 쪽은 호스트에게
    /// RPC를 보낸다). 그래서 화면은 "누가 맡고 있나"만 묻는다.
    ///
    /// <b>아무도 맡지 않으면 버튼은 아예 뜨지 않는다.</b> 로비에는 접을 판이 없으므로
    /// 그것이 곧 맞는 동작이다 — 로비용으로 버튼을 숨기는 조건을 따로 적을 필요가 없다.
    ///
    /// <see cref="GameAudio"/>·<see cref="MatchHistory"/>와 같은 결의 정적 창구다.
    /// 화면 쪽 코드가 씬 배선 없이 부를 수 있어야 한다는 사정이 셋 다 같다.
    /// </summary>
    public static class MatchSurrender
    {
        /// <summary>매치를 들고 있는 쪽(핫시트 디렉터 또는 네트워크 디렉터).</summary>
        public interface IHost
        {
            /// <summary>지금 항복이 성립하는지. 이미 끝난 매치나 아직 안 열린 판에서는 거짓.</summary>
            bool CanSurrender { get; }

            void Surrender();
        }

        private static IHost host;

        /// <summary>매치를 열면서 자기를 등록한다.</summary>
        public static void SetHost(IHost value) => host = value;

        /// <summary>
        /// 등록을 거둔다. <b>자기 것일 때만</b> 지운다 — 씬을 넘어갈 때 옛 디렉터의
        /// OnDestroy가 새 디렉터의 등록 뒤에 올 수 있고, 그때 무조건 비우면 새 매치가
        /// 항복할 수 없는 채로 시작한다.
        /// </summary>
        public static void ClearHost(IHost value)
        {
            if (host == value) host = null;
        }

        /// <summary>지금 항복할 수 있는지. 버튼을 보일지 말지가 이 값 하나로 정해진다.</summary>
        public static bool Available => host != null && host.CanSurrender;

        /// <summary>항복한다. 성립하지 않으면 아무 일도 하지 않는다.</summary>
        public static void Request()
        {
            if (Available) host.Surrender();
        }
    }
}
