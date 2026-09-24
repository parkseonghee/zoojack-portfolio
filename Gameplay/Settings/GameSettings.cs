using System.Collections.Generic;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 환경설정 값을 들고 있다가 게임에 먹이고, 다음에 켤 때를 위해 저장한다.
    ///
    /// <b>화면(<see cref="SettingsPanel"/>)과 분리한 이유.</b> 설정은 화면이 열려 있지 않을
    /// 때도 이미 적용돼 있어야 한다 — 게임을 켠 첫 순간부터다. 그래서 값의 주인은 화면이
    /// 아니라 여기이고, 화면은 이 값을 읽어 슬라이더를 맞추고 되돌려 주기만 한다.
    ///
    /// <b>목록을 코드에 적지 않는다.</b> 해상도 후보는 <see cref="Screen.resolutions"/>가
    /// 알려 주는 것을 그대로 쓴다. 기기마다 지원하는 해상도가 다르므로, 몇 가지를 골라
    /// 적어 두면 어떤 화면에서는 없는 해상도를 권하고 어떤 화면에서는 최대치를 못 쓴다.
    /// </summary>
    public static class GameSettings
    {
        // 저장 열쇠. 값을 옮기거나 이름을 바꾸면 이전에 저장된 설정은 기본값으로 돌아간다.
        private const string MasterKey = "zoojack.audio.master";
        private const string MusicKey = "zoojack.audio.music";
        private const string EffectsKey = "zoojack.audio.effects";
        private const string WidthKey = "zoojack.screen.width";
        private const string HeightKey = "zoojack.screen.height";
        private const string FullScreenKey = "zoojack.screen.fullscreen";

        /// <summary>
        /// 예전에 로비 화면이 쓰던 열쇠를 그대로 쓴다. 이름 짓는 자리가 로비에서
        /// 환경설정으로 옮겨 왔을 뿐이라, 열쇠를 바꾸면 이미 지어 둔 이름만 사라진다.
        /// </summary>
        private const string NicknameKey = "zoojack.nickname";

        private const float DefaultMaster = 1f;
        private const float DefaultMusic = 1f;
        private const float DefaultEffects = 1f;

        private static bool loaded;

        /// <summary>가로·세로 한 쌍. 주사율만 다른 중복은 걷어낸 뒤의 후보다.</summary>
        public readonly struct ScreenSize
        {
            public readonly int Width;
            public readonly int Height;

            public ScreenSize(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public override string ToString() => $"{Width} × {Height}";
        }

        private static List<ScreenSize> sizes;

        /// <summary>
        /// 이 기기가 지원하는 해상도. 작은 것부터 큰 것 순이고, 주사율만 다른 중복은 없다.
        /// 목록을 못 얻으면(일부 환경) 지금 창 크기 하나만 들어 있다 — 비어 있는 일은 없다.
        /// </summary>
        public static IReadOnlyList<ScreenSize> Sizes => sizes ??= CollectSizes();

        private static List<ScreenSize> CollectSizes()
        {
            var found = new List<ScreenSize>();
            var seen = new HashSet<long>();

            foreach (var resolution in Screen.resolutions)
            {
                // 같은 크기가 주사율마다 하나씩 올라온다. 크기만 고르는 화면이므로 하나로 줄인다.
                long key = ((long)resolution.width << 32) | (uint)resolution.height;
                if (!seen.Add(key)) continue;
                found.Add(new ScreenSize(resolution.width, resolution.height));
            }

            if (found.Count == 0) found.Add(new ScreenSize(Screen.width, Screen.height));

            found.Sort((a, b) => a.Width != b.Width
                ? a.Width.CompareTo(b.Width)
                : a.Height.CompareTo(b.Height));
            return found;
        }

        // ── 값 ───────────────────────────────────────────────────────

        public static float MasterVolume { get; private set; } = DefaultMaster;
        public static float MusicVolume { get; private set; } = DefaultMusic;
        public static float EffectsVolume { get; private set; } = DefaultEffects;

        /// <summary><see cref="Sizes"/>에서 지금 고른 자리. 목록이 바뀌어도 범위를 벗어나지 않는다.</summary>
        public static int SizeIndex { get; private set; }

        public static bool FullScreen { get; private set; } = true;

        /// <summary>
        /// 남에게 보이는 내 이름. 비어 있으면 이름 없이 배역으로만 불린다.
        ///
        /// <b>값의 주인은 여기다.</b> 방에 알리는 일은 네트워크가 맡고
        /// (<see cref="PlayerNames"/>), 이 값은 방이 없어도, 게임을 껐다 켜도 남는다.
        /// </summary>
        public static string Nickname { get; private set; } = string.Empty;

        /// <summary>이름이 바뀐 순간. 방에 알려야 하는 쪽이 듣는다.</summary>
        public static event System.Action NicknameChanged;

        public static ScreenSize CurrentSize =>
            Sizes[Mathf.Clamp(SizeIndex, 0, Sizes.Count - 1)];

        // ── 켤 때 ────────────────────────────────────────────────────

        /// <summary>
        /// 첫 씬이 열리기 전에 저장된 설정을 되살린다. 화면을 한 번도 열지 않아도
        /// 지난번에 맞춰 둔 음량과 해상도로 시작한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            if (loaded) return;
            loaded = true;

            Nickname = PlayerNames.Sanitize(PlayerPrefs.GetString(NicknameKey, string.Empty));

            MasterVolume = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
            EffectsVolume = PlayerPrefs.GetFloat(EffectsKey, DefaultEffects);
            ApplyAudio();

            FullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) != 0;
            SizeIndex = IndexOfSaved();

            // 저장된 해상도가 없으면(첫 실행) 화면을 건드리지 않는다 — 지금 크기가 곧 기본이다.
            if (PlayerPrefs.HasKey(WidthKey)) ApplyScreen();
        }

        /// <summary>
        /// 첫 씬이 선 뒤에 음량을 <b>한 번 더</b> 먹인다.
        ///
        /// <see cref="Load"/>는 씬이 열리기도 전에 도는데, 그때 쓴 전체 음량
        /// (<see cref="AudioListener.volume"/>)은 남지 않는다 — 씬의 AudioListener가 서면서
        /// 1로 돌아간다. 배율 두 개는 그냥 C# 값이라 살아남지만 전체 음량만 사라져서,
        /// 저장해 둔 30%가 게임을 다시 켤 때마다 100%로 돌아갔다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReapplyAudio()
        {
            Load();
            ApplyAudio();
        }

        /// <summary>저장된 크기가 지금 목록의 몇 번째인지. 없으면 지금 창 크기에 가장 가까운 자리.</summary>
        private static int IndexOfSaved()
        {
            int width = PlayerPrefs.GetInt(WidthKey, Screen.width);
            int height = PlayerPrefs.GetInt(HeightKey, Screen.height);

            for (int i = 0; i < Sizes.Count; i++)
                if (Sizes[i].Width == width && Sizes[i].Height == height)
                    return i;

            // 저장해 둔 해상도를 이제 못 쓰는 경우(모니터를 바꿨다). 가장 가까운 것으로 내려앉는다.
            int best = 0;
            long closest = long.MaxValue;
            for (int i = 0; i < Sizes.Count; i++)
            {
                long gap = System.Math.Abs((long)Sizes[i].Width - width)
                         + System.Math.Abs((long)Sizes[i].Height - height);
                if (gap >= closest) continue;
                closest = gap;
                best = i;
            }
            return best;
        }

        // ── 바꾸기 ───────────────────────────────────────────────────

        public static void SetMasterVolume(float value) => SetVolume(MasterKey, value,
            v => MasterVolume = v);

        public static void SetMusicVolume(float value) => SetVolume(MusicKey, value,
            v => MusicVolume = v);

        public static void SetEffectsVolume(float value) => SetVolume(EffectsKey, value,
            v => EffectsVolume = v);

        private static void SetVolume(string key, float value, System.Action<float> store)
        {
            store(Mathf.Clamp01(value));
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            ApplyAudio();
        }

        /// <summary>
        /// 이름을 짓는다. 다듬은 뒤(<see cref="PlayerNames.Sanitize"/>) 달라진 때만 알린다 —
        /// 입력칸은 글자를 칠 때마다 부르는데, 그때마다 방에 알리면 한 글자에 한 번씩
        /// RPC가 나간다.
        /// </summary>
        public static void SetNickname(string value)
        {
            string name = PlayerNames.Sanitize(value);
            if (Nickname == name) return;

            Nickname = name;
            PlayerPrefs.SetString(NicknameKey, name);
            NicknameChanged?.Invoke();
        }

        /// <summary>해상도를 고른다. 범위를 벗어난 번호는 안쪽으로 당겨 받는다.</summary>
        public static void SetSizeIndex(int index)
        {
            SizeIndex = Mathf.Clamp(index, 0, Sizes.Count - 1);
            PlayerPrefs.SetInt(WidthKey, CurrentSize.Width);
            PlayerPrefs.SetInt(HeightKey, CurrentSize.Height);
            ApplyScreen();
        }

        public static void SetFullScreen(bool value)
        {
            FullScreen = value;
            PlayerPrefs.SetInt(FullScreenKey, value ? 1 : 0);
            ApplyScreen();
        }

        /// <summary>바꾼 값을 디스크에 확정한다. 화면을 닫을 때 한 번 부르면 된다.</summary>
        public static void Save() => PlayerPrefs.Save();

        // ── 먹이기 ───────────────────────────────────────────────────

        private static void ApplyAudio()
        {
            GameAudio.MasterVolume = MasterVolume;
            GameAudio.MusicVolume = MusicVolume;
            GameAudio.EffectsVolume = EffectsVolume;
        }

        /// <summary>
        /// 에디터에서는 아무 일도 일어나지 않는다 — 게임 뷰 크기는 에디터가 정하기 때문이다.
        /// 해상도 설정은 빌드에서 확인해야 한다.
        /// </summary>
        private static void ApplyScreen()
        {
            var size = CurrentSize;
            if (Screen.width == size.Width && Screen.height == size.Height
                && Screen.fullScreen == FullScreen)
                return;

            Screen.SetResolution(size.Width, size.Height, FullScreen);
        }
    }
}
