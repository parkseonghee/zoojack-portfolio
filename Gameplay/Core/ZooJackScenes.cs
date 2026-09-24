namespace ZooJack
{
    /// <summary>
    /// 씬 이름의 단일 원천.
    ///
    /// 예전에는 <c>"LobbyScene"</c> 문자열이 GameDirector·NetworkGameDirector·AudioLibrary에
    /// 각각 <c>[SerializeField]</c>로 세 벌 있었다. 씬 파일 이름을 바꾸면 셋 중 하나만 고쳐도
    /// 컴파일은 통과하고, 남은 둘은 "로비로 못 돌아간다" 또는 "음악이 안 나온다"로 따로 터졌다.
    /// 여기 한 줄만 고치면 세 곳이 함께 움직인다.
    ///
    /// 값은 <c>Assets/Scenes/</c> 아래 파일 이름과 정확히 같아야 하고,
    /// 빌드 설정(Scenes In Build)에도 올라 있어야 한다.
    /// </summary>
    public static class ZooJackScenes
    {
        public const string Lobby = "LobbyScene";
        public const string Game  = "GameScene";
    }
}
