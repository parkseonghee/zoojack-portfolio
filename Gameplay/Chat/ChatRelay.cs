namespace ZooJack
{
    /// <summary>
    /// 친 말을 <b>남에게 보내 주는</b> 쪽으로 잇는 창구.
    ///
    /// <b>왜 사이에 한 겹을 두는가.</b> 채팅창은 게임 씬에도 로비의 방에도 같은 도구로
    /// 붙는데, 말을 실어 나르는 것은 두 곳 모두 하나뿐인 네트워크 상태다. 화면이 그것을
    /// 직접 알면 Fusion이 없는 빌드에서 컴파일이 깨지고(이 프로젝트는 그 경우 핫시트로
    /// 동작한다), 화면과 네트워크의 수명이 서로 묶인다.
    ///
    /// 받는 쪽은 여기를 지나지 않는다 — 도착한 말은 <see cref="ChatLog"/>에 바로 쌓이고
    /// 화면은 거기만 본다. <see cref="MatchSurrender"/>와 같은 결이다.
    /// </summary>
    public static class ChatRelay
    {
        public interface IHost
        {
            /// <summary>지금 말을 보낼 수 있는지. 세션이 없거나 자리가 없으면 거짓.</summary>
            bool CanSend { get; }

            /// <summary>이미 다듬어진 글이 온다. 그대로 실어 보내면 된다.</summary>
            void Send(string text);
        }

        private static IHost host;

        public static void SetHost(IHost value) => host = value;

        /// <summary>
        /// 등록을 거둔다. <b>자기 것일 때만</b> 지운다 — 세션이 갈릴 때 옛 쪽의 정리가
        /// 새 쪽의 등록보다 늦게 올 수 있고, 그때 무조건 비우면 채팅이 죽은 채로 시작한다.
        /// </summary>
        public static void ClearHost(IHost value)
        {
            if (host == value) host = null;
        }

        /// <summary>
        /// 지금 친 말이 <b>남에게까지</b> 가는지. 거짓이면 내 화면에만 남는다 —
        /// 채팅창은 이 값을 보고 안내 글자를 바꾼다.
        /// </summary>
        public static bool Available => host != null && host.CanSend;

        /// <summary>혼자 있을 때 내 말 앞에 붙는 이름. 남에게 가지 않았다는 표시다.</summary>
        public static string LocalOnlyName => ZooJackText.Get(
            "Chat.Speaker.LocalOnly", "나 (혼잣말)");

        /// <summary>
        /// 말을 보낸다.
        ///
        /// <b>보낼 곳이 없어도 삼키지 않는다.</b> 세션에 붙기 전이나 핫시트로 켠 게임
        /// 씬에서는 실어 나를 곳이 없는데, 그때 조용히 버리면 눌러도 아무 일이 없어
        /// 채팅이 고장 난 것처럼 보인다(실제로 그렇게 보였다). 내 화면에만 적고
        /// 이름으로 그 사실을 말한다.
        /// </summary>
        public static void Send(string text)
        {
            text = ChatLog.Sanitize(text);
            if (text.Length == 0) return;

            if (Available) host.Send(text);
            else ChatLog.Add(CharacterId.None, LocalOnlyName, text);
        }
    }
}
