using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 게임의 소리를 내는 단 하나의 창구.
    ///
    /// <b>씬 밖에서 산다.</b> 첫 씬이 열리기도 전에 스스로 생겨나 <see cref="Object.DontDestroyOnLoad"/>로
    /// 남는다. 이 게임은 로비 → 게임 → (매치 종료) → 로비 → 게임으로 씬을 오가므로, 씬에 붙은
    /// AudioSource로는 씬이 바뀔 때마다 소리가 끊기고 다시 배선해야 한다.
    ///
    /// <b>배선이 필요 없다.</b> 씬에 아무것도 놓지 않아도 되고, 씬을 새로 만들어도 그대로 동작한다.
    /// 소리와 크기는 전부 Resources의 <see cref="AudioLibrary"/> 자산이 들고 있다.
    ///
    /// 소리 재생은 전부 static 창구(<see cref="PlayChip"/>·<see cref="PlayCoin"/>)를 지난다.
    /// 부르는 쪽은 AudioSource가 어디 있는지, 지금 켜져 있는지 알 필요가 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameAudio : MonoBehaviour
    {
        private static GameAudio instance;
        private static bool libraryMissingReported;

        /// <summary>끊을 때 줄이는 시간(초). 뚝 끊으면 '틱' 하고 튄다.</summary>
        private const float FadeSeconds = 0.18f;

        private AudioLibrary library;
        private AudioSource effects;

        // 골라서 멈춰야 하는 소리는 전용 자리를 하나씩 갖는다. PlayOneShot으로 낸 소리는
        // 하나만 집어 멈출 수 없는데, 이 둘은 그림이 끝나는 순간에 <b>정확히</b> 끊어야 한다.
        private StoppableSound spin;        // 판정 큐브가 구르는 소리
        private StoppableSound countdown;   // 차례 시계의 마지막 초읽기

        private MusicTrack music;           // 씬마다 바뀌는 배경음악

        /// <summary>
        /// 시작·마감·중간 취소를 모두 감당하는 소리 한 자리.
        ///
        /// 크기를 줄이며 끝내는 일은 <see cref="Tick"/>이 맡는다. 연출 도중 timeScale이
        /// 흔들려도 같은 길이로 끝나야 하므로 실시간(unscaled)을 쓴다 — 그림 쪽도 같다.
        /// </summary>
        private sealed class StoppableSound
        {
            private readonly AudioSource source;
            private float fadeFrom;   // 줄이기 시작할 시각(unscaled)
            private float fadeTo;     // 완전히 멎을 시각. 0이면 줄일 것이 없다
            private float peak;       // 줄이기 전 크기

            public StoppableSound(AudioSource source) => this.source = source;

            /// <param name="seconds">
            /// 소리를 마감할 시각(초). 클립이 이보다 길면 그 지점에서 줄이며 끝낸다 —
            /// 그림이 끝난 뒤로 소리가 새면 무엇이 끝났는지 흐려진다.
            /// 0 이하이거나 클립이 더 짧으면 그냥 끝까지 튼다.
            /// </param>
            /// <param name="startAt">클립의 어디부터 틀지(초).</param>
            public void Play(AudioClip clip, float volume, float seconds, float startAt)
            {
                if (clip == null || source == null || volume <= 0f) return;

                source.Stop();
                source.clip = clip;
                source.volume = volume;
                // 클립 끝을 넘겨 지정하면 재생이 시작되지 않는다.
                source.time = Mathf.Clamp(startAt, 0f, Mathf.Max(0f, clip.length - 0.05f));
                source.Play();

                peak = volume;

                // 남은 재생 분량보다 마감이 늦으면 알아서 끝나므로 줄일 것이 없다.
                if (seconds <= 0f || seconds >= clip.length - source.time)
                {
                    fadeTo = 0f;
                    return;
                }

                float fade = Mathf.Min(FadeSeconds, seconds);
                fadeTo = Time.unscaledTime + seconds;
                fadeFrom = fadeTo - fade;
            }

            /// <summary>지금 거둔다. 연출이 도중에 취소될 때 부른다.</summary>
            public void Stop()
            {
                if (source == null || !source.isPlaying) return;

                fadeFrom = Time.unscaledTime;
                fadeTo = fadeFrom + FadeSeconds;
            }

            public bool IsPlaying => source != null && source.isPlaying;

            public void Tick()
            {
                if (fadeTo <= 0f || source == null) return;

                float now = Time.unscaledTime;
                if (now >= fadeTo)
                {
                    source.Stop();
                    source.volume = peak;
                    fadeTo = 0f;
                    return;
                }

                if (now >= fadeFrom)
                    source.volume = Mathf.Lerp(0f, peak, Mathf.InverseLerp(fadeTo, fadeFrom, now));
            }
        }

        /// <summary>
        /// 끊기지 않고 흐르는 배경음악 한 줄.
        ///
        /// 곡을 바꾸라는 요청이 오면 <b>지금 곡을 줄여 끈 다음</b> 새 곡을 올린다. 두 곡이
        /// 겹치지 않게 하려면 자리가 둘이어야 하는데, 배경음악은 씬이 바뀌는 순간에만
        /// 갈리므로 짧은 정적이 오히려 자연스럽다.
        ///
        /// <b>같은 곡이면 아무것도 하지 않는다.</b> 같은 씬을 다시 열 때(방 로비 → 캐릭터 선택
        /// 같은 재진입) 음악이 처음부터 다시 시작하면 씬을 오갈 때마다 도입부만 듣게 된다.
        ///
        /// <b>무엇이 흘러야 하는지(의도)를 실제로 흐르는 것(<see cref="source"/>)과 따로 든다.</b>
        /// 둘을 하나로 묶어 두면 크기가 0이 되는 순간 곡을 잃어버린다 — 예전에 그래서
        /// 환경설정에서 배경음악을 0까지 내리면 다시 올려도 음악이 돌아오지 않았다.
        /// </summary>
        private sealed class MusicTrack
        {
            private const float FadeOutSeconds = 0.5f;
            private const float FadeInSeconds = 1.2f;

            private readonly AudioSource source;

            private AudioClip wanted;     // 이 씬에서 흘러야 하는 곡. null이면 음악 없음
            private float wantedVolume;   // 그 곡의 목표 크기. 0이어도 곡은 버리지 않는다
            private bool swapping;        // 곡을 갈아 끼우려고 지금 줄이는 중

            public MusicTrack(AudioSource source)
            {
                this.source = source;
                if (source != null) source.loop = true;
            }

            public AudioClip Current => source != null ? source.clip : null;
            public bool IsPlaying => source != null && source.isPlaying;

            /// <summary>
            /// 크기만 바꾼다. 곡을 건드리지 않으므로 0까지 내렸다가 다시 올려도
            /// 그 자리에서 이어서 흐른다.
            /// </summary>
            public void SetVolume(float volume) => wantedVolume = Mathf.Max(0f, volume);

            /// <summary>곡을 바꾼다. <paramref name="clip"/>이 null이면 음악을 거둔다.</summary>
            public void Play(AudioClip clip, float volume)
            {
                wantedVolume = Mathf.Max(0f, volume);
                if (clip == wanted) return;   // 같은 곡이다. 이어서 흐른다

                wanted = clip;
                swapping = IsPlaying;         // 흐르는 곡이 있으면 먼저 줄여 끈다
                if (!swapping) Restart();
            }

            public void Tick()
            {
                if (source == null) return;

                // 곡을 갈아 끼우는 중에는 크기가 0까지 내려가야 한다. 흘릴 곡이 아예 없는
                // 씬에서도 0이다 — 그러지 않으면 멎은 자리에서 크기만 슬금슬금 올라간다.
                float target = swapping || wanted == null ? 0f : wantedVolume;

                if (!Mathf.Approximately(source.volume, target))
                {
                    // 줄일 때가 올릴 때보다 빨라야 곡이 갈리는 사이의 정적이 짧다.
                    float seconds = target > source.volume ? FadeInSeconds : FadeOutSeconds;
                    source.volume = Mathf.MoveTowards(
                        source.volume, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
                }

                if (swapping)
                {
                    if (source.volume > 0f) return;   // 아직 줄고 있다
                    swapping = false;
                    Restart();
                    return;
                }

                if (wanted == null)
                {
                    if (source.isPlaying) source.Stop();
                    return;
                }

                // 흘러야 할 곡이 서 있지 않다면 여기서 다시 세운다.
                //
                // <b>크기가 0이어도 세워 둔다.</b> 슬라이더는 끌다가 0을 스쳐 지나므로,
                // 0에서 곡을 멈추면 지나칠 때마다 음악이 도입부로 되감긴다.
                if (!source.isPlaying) Restart();
            }

            private void Restart()
            {
                if (wanted == null) { source.Stop(); return; }

                source.clip = wanted;
                source.volume = 0f;
                source.time = 0f;
                source.Play();
            }
        }

        // 같은 프레임에 같은 소리가 두 번 울리는 걸 막는다. 베팅 칩은 두 자리(A·B)가
        // 한 프레임에 함께 놓이므로, 막지 않으면 똑같은 파형이 겹쳐 두 배로 크고 위상이 뭉갠다.
        private AudioClip lastOneShot;
        private int lastOneShotFrame = -1;

        /// <summary>
        /// 게임의 모든 소리에 곱해지는 크기(0~1). 음악과 효과음을 함께 올리고 내린다.
        /// </summary>
        public static float MasterVolume
        {
            get => AudioListener.volume;
            set => AudioListener.volume = Mathf.Clamp01(value);
        }

        // 아래 둘은 <b>자산에 적힌 크기에 곱해지는 배율</b>이다. 소리마다 손으로 맞춰 둔
        // 균형(칩은 크게, 음악은 작게)을 유지한 채 갈래별로만 올리고 내린다.
        private static float musicScale = 1f;
        private static float effectsScale = 1f;

        /// <summary>배경음악 배율(0~1). 바꾸면 흐르는 중에도 곧바로 반영된다.</summary>
        public static float MusicVolume
        {
            get => musicScale;
            set
            {
                musicScale = Mathf.Clamp01(value);
                if (instance != null && instance.library != null)
                    instance.music?.SetVolume(instance.library.MusicVolume * musicScale);
            }
        }

        /// <summary>
        /// 효과음 배율(0~1). 다음에 나는 소리부터 반영된다 —
        /// 이미 울리고 있는 짧은 소리를 도중에 줄일 방법은 없다.
        /// </summary>
        public static float EffectsVolume
        {
            get => effectsScale;
            set => effectsScale = Mathf.Clamp01(value);
        }

        // ── 만들어지는 곳 ────────────────────────────────────────────

        /// <summary>
        /// 첫 씬이 열리기 전에 스스로 선다. 플레이 한 번에 한 번만 불리므로,
        /// 씬을 몇 번 오가든 창구는 하나로 유지된다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;

            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameAudio>();
        }

        private void Awake()
        {
            // 씬에 손으로 놓인 사본이 있어도 창구는 하나여야 한다.
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            library = Resources.Load<AudioLibrary>(AudioLibrary.ResourceName);
            if (library == null)
            {
                if (!libraryMissingReported)
                {
                    libraryMissingReported = true;
                    Debug.LogWarning(
                        $"[GameAudio] Resources/{AudioLibrary.ResourceName} 자산이 없어 소리가 나지 않는다. " +
                        "메뉴 ZooJack/사운드/오디오 라이브러리 만들기 로 만들 것.");
                }
                return;
            }

            effects = CreateSource("Effects");
            spin = new StoppableSound(CreateSource("Spin"));
            countdown = new StoppableSound(CreateSource("Countdown"));
            music = new MusicTrack(CreateSource("Music"));

            // 음악은 씬이 정한다. 씬마다 오브젝트를 놓고 배선하는 대신 여기 한 곳에서
            // 이름을 보고 고르므로, 어느 씬에서 플레이를 시작하든(에디터에서 게임 씬을
            // 직접 열어도) 맞는 곡이 흐른다.
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplySceneMusic(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 겹쳐 여는 씬은 무대를 바꾸지 않으므로 음악도 건드리지 않는다.
            if (mode == LoadSceneMode.Additive) return;
            ApplySceneMusic(scene.name);
        }

        private void ApplySceneMusic(string sceneName)
        {
            if (library == null || music == null) return;
            music.Play(library.MusicForScene(sceneName), library.MusicVolume * musicScale);
        }

        private AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            // 화면 어디서 나든 같은 크기로 들려야 한다. 위치가 있는 소리가 아니다.
            // (자산 자체는 3D로 임포트돼 있어서 여기서 눌러 두지 않으면 거리에 따라 작아진다.)
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;

            return source;
        }

        // ── 효과음 ───────────────────────────────────────────────────

        /// <summary>판돈 칩 소리. 칩을 고를 때와 테이블에 칩이 놓일 때 모두 이 소리다.</summary>
        public static void PlayChip()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.Chip, instance.library.ChipVolume);
        }

        /// <summary>뇌물 코인 소리.</summary>
        public static void PlayCoin()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.Coin, instance.library.CoinVolume);
        }

        // ── 판정 주사위 ──────────────────────────────────────────────

        /// <summary>
        /// 구르는 소리의 길이(초). 0이면 소리가 없다.
        ///
        /// 큐브 연출이 이 값을 보고 제 박자를 정한다 — 소리를 잘라 그림에 맞추는 게 아니라
        /// 그림이 소리에 맞춘다. 소리를 갈아 끼우면 연출 길이도 따라 움직인다.
        /// </summary>
        public static float SpinSeconds =>
            instance != null && instance.library != null && instance.library.DiceSpin != null
                ? instance.library.DiceSpin.length
                : 0f;

        /// <summary>
        /// 큐브가 구르는 소리를 낸다.
        /// </summary>
        /// <param name="seconds">
        /// 소리를 마감할 시각(초). 클립이 이보다 길면 그 지점에서 줄이며 끝낸다 —
        /// 큐브가 선 뒤로 바람 소리가 새면 결과가 흐려지기 때문이다.
        /// 0 이하이거나 클립이 더 짧으면 그냥 끝까지 튼다.
        /// </param>
        /// <param name="startAt">
        /// 클립의 어디부터 틀지(초). 짧게 다시 돌릴 때 쓴다 — 이 소리는 앞부분이 조용해서
        /// 0부터 틀면 1초 남짓한 구간에서는 아무것도 들리지 않는다.
        /// </param>
        public static void PlaySpin(float seconds = 0f, float startAt = 0f)
        {
            if (instance == null || instance.library == null) return;
            instance.spin.Play(
                instance.library.DiceSpin, instance.library.DiceSpinVolume * effectsScale,
                seconds, startAt);
        }

        /// <summary>구르는 소리를 거둔다. 연출이 도중에 취소될 때 부른다.</summary>
        public static void StopSpin() => instance?.spin?.Stop();

        /// <summary>승자의 면이 서는 순간.</summary>
        public static void PlayDiceReveal()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.DiceReveal, instance.library.DiceRevealVolume);
        }

        // ── 차례 시계 초읽기 ─────────────────────────────────────────

        /// <summary>
        /// 초읽기 째깍임을 낸다. 마지막 초침이 <b>남은 시간이 0이 되는 순간</b>에 떨어지도록
        /// 클립의 뒤쪽에서 잘라 튼다 — 앞에서부터 틀면 시계가 0을 가리킨 뒤로도 소리가 남는다.
        /// </summary>
        /// <param name="secondsLeft">지금부터 시계가 0이 될 때까지의 시간(초).</param>
        public static void PlayCountdown(float secondsLeft)
        {
            if (instance == null || instance.library == null || secondsLeft <= 0f) return;

            var clip = instance.library.Countdown;
            if (clip == null) return;

            float startAt = clip.length - instance.library.CountdownTailSeconds - secondsLeft;
            instance.countdown.Play(
                clip, instance.library.CountdownVolume * effectsScale,
                secondsLeft, Mathf.Max(0f, startAt));
        }

        /// <summary>초읽기를 거둔다. 시간이 남았는데 차례가 끝났을 때 부른다.</summary>
        public static void StopCountdown() => instance?.countdown?.Stop();

        /// <summary>초읽기가 지금 울리고 있는지.</summary>
        public static bool IsCountdownPlaying => instance != null && instance.countdown != null
                                                 && instance.countdown.IsPlaying;

        // 줄이는 구간에서만 실제로 일하는 가벼운 갱신.
        private void Update()
        {
            spin?.Tick();
            countdown?.Tick();
            music?.Tick();
        }

        /// <summary>배경음악. 지금 흐르는 곡의 이름이 필요할 때(설정 화면 등).</summary>
        public static AudioClip CurrentMusic => instance?.music?.Current;

        // ── 그 밖의 소리 ─────────────────────────────────────────────

        /// <summary>일반 버튼을 누를 때. 코인·칩·카드는 각자 제 소리가 따로 있다.</summary>
        public static void PlayClick()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.Click, instance.library.ClickVolume);
        }

        /// <summary>
        /// 버튼을 누를 때 클릭음이 나도록 이어 준다.
        ///
        /// <b>화면마다 손으로 리스너를 붙이지 않는다.</b> 버튼을 하나 늘릴 때 소리만
        /// 빠뜨리기 쉬운데, 그건 눈으로는 보이지 않는 차이라 아무도 못 찾는다. 실제로
        /// 로비는 통째로 소리가 없었다 — 게임 화면에만 목록이 있었기 때문이다.
        ///
        /// null은 그냥 건너뛴다. 화면마다 없는 버튼이 있어서 부르는 쪽이 걸러야 하면
        /// 그 목록이 조건문 범벅이 된다.
        /// </summary>
        public static void AttachClick(params Button[] buttons)
        {
            if (buttons == null) return;
            foreach (var button in buttons)
                if (button != null) button.onClick.AddListener(PlayClick);
        }

        /// <summary>딜러가 준 카드가 위에서 내려올 때.</summary>
        public static void PlayCardDeal()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.CardDeal, instance.library.CardDealVolume);
        }

        /// <summary>카드가 뒤집혀 앞면이 드러나는 순간.</summary>
        public static void PlayCardFlip()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.CardFlip, instance.library.CardFlipVolume);
        }

        /// <summary>
        /// 딜러가 후보 카드를 골라 카드끼리 자리를 바꾸는 순간.
        ///
        /// 두 장이 동시에 움직이지만 소리는 한 번만 난다 — 한 프레임에 같은 소리를
        /// 두 번 내지 않는 <see cref="PlayOneShot"/> 덕에 부르는 쪽이 신경 쓸 일이 없다.
        /// </summary>
        public static void PlayCardSlide()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.CardSlide, instance.library.CardSlideVolume);
        }

        /// <summary>
        /// 설명서의 장을 넘기는 순간.
        ///
        /// 카드 소리(<see cref="PlayCardSlide"/>)를 빌려 쓰던 자리다. 종이가 스치는 것은
        /// 맞았지만 넘어가는 것이 <b>책</b>이라는 말은 못 했다.
        /// </summary>
        public static void PlayPageTurn()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.PageTurn, instance.library.PageTurnVolume);
        }

        /// <summary>
        /// 히트·스탠드·더블다운·다이가 승인돼 화면 한가운데 알림이 뜰 때.
        /// 버튼 클릭음과는 다른 소리다 — 누른 순간이 아니라 <b>받아들여진</b> 순간이고,
        /// 남이 한 행동에도 울린다.
        /// </summary>
        public static void PlayPlayerAction()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.PlayerAction, instance.library.PlayerActionVolume);
        }

        /// <summary>
        /// 감정표현이 실제로 나타나는 순간의 팝 소리. AudioLibrary의 세 Mouth 클립 중
        /// 하나를 무작위로 고르며 일반 효과음과 같은 Effects 배율을 따른다.
        /// </summary>
        public static void PlayEmotionPop()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(
                instance.library.RandomEmotionPop(),
                instance.library.EmotionPopVolume);
        }

        /// <summary>
        /// 걷고 있는 캐릭터가 지정한 순번의 발소리를 낸다. 선택은 호출한 캐릭터의
        /// AvatarFootstepEvents가 관리하므로 여기서는 무작위 선택을 하지 않는다.
        /// </summary>
        public static void PlayFootstep(bool useSecondClip)
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(
                instance.library.Footstep(useSecondClip),
                instance.library.FootstepVolume);
        }

        /// <summary>역할 공개 — '당신은 …입니다' 카드가 떠오르는 순간.</summary>
        public static void PlayRoleReveal()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.RoleReveal, instance.library.RoleRevealVolume);
        }

        /// <summary>겉보기 승리 — 라운드 결과 문구와 왕관·비석이 함께 올라오는 순간.</summary>
        public static void PlayApparentWin()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.ApparentWin, instance.library.ApparentWinVolume);
        }

        /// <summary>최종 승리 — 판정 큐브가 멈춘 뒤 최종 승자 문구가 뜨는 순간.</summary>
        public static void PlayFinalWin()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.FinalWin, instance.library.FinalWinVolume);
        }

        /// <summary>매치 종료 — 순위표와 종료 카드가 올라오는 순간.</summary>
        public static void PlayMatchOver()
        {
            if (instance == null || instance.library == null) return;
            instance.PlayOneShot(instance.library.MatchOver, instance.library.MatchOverVolume);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            volume *= effectsScale;
            if (clip == null || effects == null || volume <= 0f) return;

            // 한 프레임에 같은 소리는 한 번만. 두 자리가 동시에 칩을 놓아도 한 번으로 들린다.
            if (clip == lastOneShot && lastOneShotFrame == Time.frameCount) return;
            lastOneShot = clip;
            lastOneShotFrame = Time.frameCount;

            effects.PlayOneShot(clip, volume);
        }
    }
}
