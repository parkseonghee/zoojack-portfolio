namespace ZooJack
{
    /// <summary>
    /// 지금 키보드를 채팅이 쓰고 있는지.
    ///
    /// <b>왜 필요한가.</b> 이 게임은 키보드를 씬 어디서나 직접 읽는다 — WASD로 캐릭터가
    /// 걷고(<see cref="AvatarStage"/>) E로 감정표현 휠이 열린다
    /// (<see cref="EmotionWheelController"/>). 채팅에 "we"라고만 쳐도 캐릭터가 오른쪽 위로
    /// 달려가고 휠이 열린다. 입력칸이 잡고 있는 동안에는 저쪽이 손을 떼야 한다.
    ///
    /// 읽는 곳이 여럿이라 정적으로 둔다. 채팅창이 없는 씬에서는 언제나 거짓이다.
    /// </summary>
    public static class ChatFocus
    {
        /// <summary>글을 치는 중이면 참. 이때 캐릭터 조작과 감정표현 휠은 쉰다.</summary>
        public static bool IsTyping { get; private set; }

        public static void SetTyping(bool typing) => IsTyping = typing;
    }
}
