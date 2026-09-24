using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 게임이 쓰는 소리와 그 크기를 한곳에 모아 둔 자산.
    ///
    /// <b>왜 씬이 아니라 자산인가.</b> 소리를 씬의 AudioSource로 두면 씬마다 따로 놓아야 하고,
    /// 씬을 다시 저장할 때 실수로 지워지거나 값이 갈라진다. 자산 하나만 두고
    /// <see cref="GameAudio"/>가 씬 밖에서 들고 있으면 그런 일이 없다.
    ///
    /// <see cref="ResourceName"/> 이름으로 Resources에 있어야 한다 — 그래야 어느 씬에서
    /// 시작하든(빌드든 에디터든) 아무 배선 없이 스스로 찾아 쓴다.
    ///
    /// 자산은 <c>ZooJack/사운드/오디오 라이브러리 만들기</c> 메뉴로 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "ZooJack/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        /// <summary>Resources 안에서의 이름. <see cref="GameAudio"/>가 이 이름으로 찾는다.</summary>
        public const string ResourceName = "AudioLibrary";

        [Header("배경음악")]
        [Tooltip("로비 씬에서 흐르는 곡.")]
        [SerializeField] private AudioClip lobbyMusic;

        [Tooltip("게임 씬에서 흐르는 곡.")]
        [SerializeField] private AudioClip gameMusic;

        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;

        // 어느 씬에서 어떤 곡을 트는지는 ZooJackScenes가 정한다. 예전에는 이 둘이
        // 에셋에 직렬화된 문자열이라, 씬 이름을 바꿨을 때 코드는 멀쩡한데 음악만
        // 조용히 사라졌다 — 에셋을 열어 보기 전에는 원인을 알 수 없었다.

        [Header("칩")]
        [Tooltip("판돈 칩을 고를 때, 그리고 테이블에 칩이 실제로 놓일 때.")]
        [SerializeField] private AudioClip chip;

        [SerializeField, Range(0f, 1f)] private float chipVolume = 0.85f;

        [Header("코인")]
        [Tooltip("뇌물 코인을 고를 때.")]
        [SerializeField] private AudioClip coin;

        [SerializeField, Range(0f, 1f)] private float coinVolume = 0.8f;

        [Header("버튼")]
        [Tooltip("일반 버튼을 누를 때(제출·결정·행동 선택·계속 등). 코인·칩·카드는 각자 제 소리를 낸다.")]
        [SerializeField] private AudioClip click;

        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.7f;

        [Header("판정 주사위")]
        [Tooltip("큐브가 구르는 동안. 원본은 길어서 큐브가 멈출 때 잘라 쓴다.")]
        [SerializeField] private AudioClip diceSpin;

        [SerializeField, Range(0f, 1f)] private float diceSpinVolume = 0.75f;

        [Tooltip("승자의 면이 서는 순간.")]
        [SerializeField] private AudioClip diceReveal;

        [SerializeField, Range(0f, 1f)] private float diceRevealVolume = 0.9f;

        [Header("카드")]
        [Tooltip("딜러가 준 카드가 위에서 내려올 때.")]
        [SerializeField] private AudioClip cardDeal;

        [SerializeField, Range(0f, 1f)] private float cardDealVolume = 0.8f;

        [Tooltip("덮여 있던 카드가 뒤집혀 앞면이 드러나는 순간.")]
        [SerializeField] private AudioClip cardFlip;

        [SerializeField, Range(0f, 1f)] private float cardFlipVolume = 0.8f;

        [Tooltip("딜러가 후보 카드를 골라 카드끼리 자리를 바꿀 때. 카드를 나눠 주는 소리와 " +
                 "다른 소리다 — 이쪽은 아직 아무것도 확정되지 않은, 테이블 위에서 카드를 " +
                 "미는 손짓이다.")]
        [SerializeField] private AudioClip cardSlide;

        [SerializeField, Range(0f, 1f)] private float cardSlideVolume = 0.8f;

        [Header("설명서")]
        [Tooltip("설명서의 장을 넘길 때. 카드가 스치는 소리로 대신하던 자리다 — " +
                 "종이는 종이 소리를 내야 넘긴 것이 책으로 읽힌다.")]
        [SerializeField] private AudioClip pageTurn;

        [SerializeField, Range(0f, 1f)] private float pageTurnVolume = 0.8f;

        [Header("행동 알림")]
        [Tooltip("히트·스탠드·더블다운·다이가 승인돼 화면 한가운데 알림이 뜰 때.")]
        [SerializeField] private AudioClip playerAction;

        [SerializeField, Range(0f, 1f)] private float playerActionVolume = 0.7f;

        [Header("감정표현")]
        [Tooltip("감정표현이 Plate 위에 나타나는 순간 이 셋 중 하나를 무작위로 재생한다.")]
        [SerializeField] private AudioClip[] emotionPops = new AudioClip[3];

        [SerializeField, Range(0f, 1f)] private float emotionPopVolume = 0.7f;

        [Header("발걸음")]
        [Tooltip("캐릭터가 걷는 동안 0번과 1번을 무작위 없이 번갈아 재생한다.")]
        [SerializeField] private AudioClip[] footsteps = new AudioClip[2];

        [Tooltip("세 캐릭터가 동시에 걸을 수 있으므로 일반 SFX보다는 낮게 둔다.")]
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.3f;

        [Header("차례 시계")]
        [Tooltip("남은 시간이 5초 아래로 내려갔을 때의 초읽기. 클립 뒤쪽에서 잘라 쓴다 — " +
                 "마지막 초침이 0초에 떨어져야 한다.")]
        [SerializeField] private AudioClip countdown;

        [SerializeField, Range(0f, 1f)] private float countdownVolume = 0.6f;

        [Tooltip("초읽기 클립에서 마지막 초침 뒤에 남는 무음(초). 이만큼을 빼고 잘라야 " +
                 "마지막 초침과 시계의 0초가 맞는다. 소리를 갈아 끼우면 이 값도 다시 재야 한다.")]
        [SerializeField, Min(0f)] private float countdownTailSeconds = 1.2f;

        [Header("역할 공개")]
        [Tooltip("첫 역할 배정과 역할 교대 때 '당신은 …입니다' 카드가 떠오를 때.")]
        [SerializeField] private AudioClip roleReveal;

        [SerializeField, Range(0f, 1f)] private float roleRevealVolume = 0.75f;

        [Header("승리")]
        [Tooltip("겉보기 승리 — 라운드가 끝나고 카드가 다 열린 뒤 승패 문구가 뜰 때.")]
        [SerializeField] private AudioClip apparentWin;

        [SerializeField, Range(0f, 1f)] private float apparentWinVolume = 0.7f;

        [Tooltip("최종 승리 — 판정 큐브가 멈추고 최종 승자 문구가 뜰 때.")]
        [SerializeField] private AudioClip finalWin;

        [SerializeField, Range(0f, 1f)] private float finalWinVolume = 0.8f;

        [Tooltip("매치 종료 — 순위표와 종료 카드가 올라올 때.")]
        [SerializeField] private AudioClip matchOver;

        [SerializeField, Range(0f, 1f)] private float matchOverVolume = 0.8f;

        public float MusicVolume => musicVolume;

        /// <summary>
        /// 이 씬에서 흐를 곡. 로비도 게임도 아닌 씬(디버그 씬 등)에서는 null —
        /// 음악을 틀지 않는다는 뜻이다.
        /// </summary>
        public AudioClip MusicForScene(string sceneName)
        {
            if (sceneName == ZooJackScenes.Lobby) return lobbyMusic;
            if (sceneName == ZooJackScenes.Game) return gameMusic;
            return null;
        }

        public AudioClip Chip => chip;
        public float ChipVolume => chipVolume;

        public AudioClip Coin => coin;
        public float CoinVolume => coinVolume;

        public AudioClip Click => click;
        public float ClickVolume => clickVolume;

        public AudioClip DiceSpin => diceSpin;
        public float DiceSpinVolume => diceSpinVolume;

        public AudioClip DiceReveal => diceReveal;
        public float DiceRevealVolume => diceRevealVolume;

        public AudioClip CardDeal => cardDeal;
        public float CardDealVolume => cardDealVolume;

        public AudioClip CardFlip => cardFlip;
        public float CardFlipVolume => cardFlipVolume;

        public AudioClip CardSlide => cardSlide;
        public float CardSlideVolume => cardSlideVolume;

        public AudioClip PageTurn => pageTurn;
        public float PageTurnVolume => pageTurnVolume;

        public AudioClip PlayerAction => playerAction;
        public float PlayerActionVolume => playerActionVolume;

        public float EmotionPopVolume => emotionPopVolume;

        public AudioClip RandomEmotionPop()
        {
            if (emotionPops == null || emotionPops.Length == 0) return null;

            int start = Random.Range(0, emotionPops.Length);
            for (int offset = 0; offset < emotionPops.Length; offset++)
            {
                AudioClip clip = emotionPops[(start + offset) % emotionPops.Length];
                if (clip != null) return clip;
            }
            return null;
        }

        public float FootstepVolume => footstepVolume;

        public AudioClip Footstep(bool useSecond)
        {
            if (footsteps == null || footsteps.Length == 0) return null;
            int preferred = useSecond ? 1 : 0;
            if (preferred < footsteps.Length && footsteps[preferred] != null)
                return footsteps[preferred];

            for (int i = 0; i < footsteps.Length; i++)
                if (footsteps[i] != null) return footsteps[i];
            return null;
        }

        public AudioClip Countdown => countdown;
        public float CountdownVolume => countdownVolume;
        public float CountdownTailSeconds => countdownTailSeconds;

        public AudioClip RoleReveal => roleReveal;
        public float RoleRevealVolume => roleRevealVolume;

        public AudioClip ApparentWin => apparentWin;
        public float ApparentWinVolume => apparentWinVolume;

        public AudioClip FinalWin => finalWin;
        public float FinalWinVolume => finalWinVolume;

        public AudioClip MatchOver => matchOver;
        public float MatchOverVolume => matchOverVolume;

#if UNITY_EDITOR
        /// <summary>
        /// 만들기 도구가 채워 넣는 소리 묶음. 이름을 붙여 넘기게 한 이유는 순서다 —
        /// 인자를 열 개 넘게 줄줄이 세우면 둘을 맞바꿔 넣어도 컴파일이 통과한다.
        /// </summary>
        public struct EditorClips
        {
            public AudioClip LobbyMusic, GameMusic;
            public AudioClip Chip, Coin, Click;
            public AudioClip CardDeal, CardFlip, CardSlide;
            public AudioClip PageTurn;
            public AudioClip DiceSpin, DiceReveal;
            public AudioClip PlayerAction;
            public AudioClip[] EmotionPops;
            public AudioClip[] Footsteps;
            public AudioClip Countdown;
            public AudioClip RoleReveal;
            public AudioClip ApparentWin, FinalWin, MatchOver;
        }

        /// <summary>
        /// 만들기 도구가 소리를 끼워 넣는 통로. 크기 값은 건드리지 않는다 —
        /// 인스펙터에서 손으로 맞춘 값을 도구를 다시 돌렸다고 되돌리면 안 된다.
        /// </summary>
        public void EditorSetClips(EditorClips clips)
        {
            lobbyMusic = clips.LobbyMusic;
            gameMusic = clips.GameMusic;
            chip = clips.Chip;
            coin = clips.Coin;
            click = clips.Click;
            cardDeal = clips.CardDeal;
            cardFlip = clips.CardFlip;
            cardSlide = clips.CardSlide;
            pageTurn = clips.PageTurn;
            diceSpin = clips.DiceSpin;
            diceReveal = clips.DiceReveal;
            playerAction = clips.PlayerAction;
            emotionPops = clips.EmotionPops;
            footsteps = clips.Footsteps;
            countdown = clips.Countdown;
            roleReveal = clips.RoleReveal;
            apparentWin = clips.ApparentWin;
            finalWin = clips.FinalWin;
            matchOver = clips.MatchOver;
        }
#endif
    }
}
