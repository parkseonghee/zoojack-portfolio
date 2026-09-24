using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 환경설정 화면. 음량 셋과 해상도·화면 모드를 다룬다.
    ///
    /// <b>값을 들고 있지 않다.</b> 주인은 <see cref="GameSettings"/>이고 여기는 그 값을 보여
    /// 주고 되돌려 줄 뿐이다. 그래서 이 화면을 한 번도 열지 않아도 설정은 이미 적용돼 있고,
    /// 씬을 옮겨 이 화면이 사라져도 값은 그대로 남는다.
    ///
    /// <b>해상도 목록을 적어 두지 않는다.</b> 기기가 알려 주는 것을 그대로 훑는다
    /// (<see cref="GameSettings.Sizes"/>). 그래서 화살표는 "다음 후보"일 뿐,
    /// 어떤 해상도가 있는지는 이 코드가 알지 못한다.
    ///
    /// 배선은 <c>ZooJack/게임/환경설정 화면 만들기</c> 메뉴가 해 준다. 손으로 끼운 값이
    /// 있으면 그 메뉴를 다시 돌릴 때 사라지므로, 고칠 일이 생기면 도구 쪽을 고치는 편이 낫다.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("열고 닫기")]
        [Tooltip("설정 내용을 담은 오브젝트. '설정' 버튼은 이 바깥에 있어야 다시 열 수 있다.\n\n" +
                 "비워 두면 붙박이로 동작한다 — 로비의 설정 화면처럼 이미 다른 화면 안에 " +
                 "들어앉아 있어, 열고 닫는 일을 그 화면이 맡는 경우다.")]
        [SerializeField] private GameObject window;

        [Tooltip("화면을 덮는 어두운 판. 여기를 눌러도 닫힌다.")]
        [SerializeField] private Button scrim;

        [SerializeField] private Button btnOpen;
        [SerializeField] private Button btnClose;

        [Tooltip("항복. 누르면 그 자리에서 매치가 끝난다. " +
                 "굴러가는 매치가 없으면(로비의 설정 화면) 스스로 숨는다 — " +
                 "보일지 말지는 MatchSurrender가 정한다.")]
        [SerializeField] private Button btnSurrender;

        [Header("이름")]
        [Tooltip("남에게 보이는 이름. 비워 두면 배역으로만 불린다.\n\n" +
                 "글자 수 제한은 실행 중에 PlayerNames.MaxLength로 맞춘다.")]
        [SerializeField] private TMP_InputField inputNickname;

        [Header("음량")]
        [SerializeField] private Slider sliderMaster;
        [SerializeField] private Slider sliderMusic;
        [SerializeField] private Slider sliderEffects;
        [SerializeField] private TextMeshProUGUI txtMaster;
        [SerializeField] private TextMeshProUGUI txtMusic;
        [SerializeField] private TextMeshProUGUI txtEffects;

        [Header("화면")]
        [Tooltip("해상도 목록. 내용은 실행 중에 기기가 알려 주는 것으로 채운다.")]
        [SerializeField] private TMP_Dropdown dropResolution;

        [SerializeField] private TMP_Dropdown dropScreenMode;

        // 슬라이더를 코드로 맞추는 동안에는 onValueChanged를 무시한다.
        private bool syncing;

        /// <summary>
        /// 지금 사람이 이 설정을 보고 있는지.
        /// 붙박이(<see cref="window"/>가 비어 있음)라면 이 오브젝트가 켜져 있는지가 곧 답이다.
        /// </summary>
        public bool IsOpen => window != null ? window.activeSelf : isActiveAndEnabled;

        /// <summary>
        /// 지금 슬라이더가 말하는 값을 설정으로 받아들여도 되는지.
        ///
        /// <b>왜 열려 있는지까지 보는가.</b> Slider는 사람이 끌 때만 onValueChanged를 부르지
        /// 않는다 — 캔버스를 다시 그릴 때(<c>Slider.Rebuild</c>)도 지금 값으로 한 번 부른다.
        /// 그래서 화면이 닫혀 있는 동안 그 신호를 받아들이면, 게임을 켠 직후 아직 아무 값도
        /// 담기지 않은 슬라이더(0)가 저장된 음량을 0으로 덮어쓴다. 실제로 그랬다.
        ///
        /// <see cref="Open"/>이 <see cref="Sync"/>를 먼저 하고 창을 나중에 켜므로,
        /// 값을 맞추는 동안에도 이 조건이 거짓이라 되받아 저장하는 일이 없다.
        /// </summary>
        private bool AcceptsInput => IsOpen && !syncing;

        private void Awake()
        {
            GameSettings.Load();

            if (btnOpen != null) btnOpen.onClick.AddListener(Open);
            if (btnClose != null) btnClose.onClick.AddListener(Close);
            if (scrim != null) scrim.onClick.AddListener(Close);

            if (sliderMaster != null) sliderMaster.onValueChanged.AddListener(OnMaster);
            if (sliderMusic != null) sliderMusic.onValueChanged.AddListener(OnMusic);
            if (sliderEffects != null) sliderEffects.onValueChanged.AddListener(OnEffects);

            DisableDirectionalNavigation(sliderMaster);
            DisableDirectionalNavigation(sliderMusic);
            DisableDirectionalNavigation(sliderEffects);

            if (btnSurrender != null)
            {
                btnSurrender.onClick.AddListener(Surrender);
                btnSurrender.gameObject.SetActive(false);
            }

            if (inputNickname != null)
            {
                // 상한은 두 곳이 아니라 한 곳에서 온다. 칸이 막아 주면 지우는 일이
                // 생기지 않아, 치는 사람도 왜 글자가 사라졌는지 묻지 않는다.
                inputNickname.characterLimit = PlayerNames.MaxLength;
                inputNickname.onValueChanged.AddListener(OnNickname);

                // 이름을 치는 동안에는 키보드가 이 칸의 것이다. 그러지 않으면 'w'에
                // 캐릭터가 걷고 'e'에 감정표현 휠이 열리며 Enter에 채팅이 뜬다.
                inputNickname.onSelect.AddListener(_ => ChatFocus.SetTyping(true));
                inputNickname.onDeselect.AddListener(_ => ChatFocus.SetTyping(false));
            }

            if (dropResolution != null) dropResolution.onValueChanged.AddListener(OnResolution);
            if (dropScreenMode != null) dropScreenMode.onValueChanged.AddListener(OnScreenMode);

            if (window != null) window.SetActive(false);
        }

        /// <summary>
        /// 캐릭터 이동용 WASD/방향키가 선택된 음량 슬라이더까지 움직이지 않게 한다.
        /// 마우스 드래그와 클릭 입력은 Navigation 설정과 무관하므로 그대로 동작한다.
        /// </summary>
        private static void DisableDirectionalNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            var navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        /// <summary>
        /// 붙박이로 쓸 때는 화면이 켜지는 순간이 곧 여는 순간이다.
        /// 모달일 때는 <see cref="Open"/>이 이미 맞춰 놓았으므로 할 일이 없다.
        /// </summary>
        private void OnEnable()
        {
            if (window == null) Sync();
        }

        // ── 열고 닫기 ────────────────────────────────────────────────

        public void Open()
        {
            GameAudio.PlayClick();
            Sync();
            if (window != null) window.SetActive(true);
            transform.SetAsLastSibling();   // 어느 패널이 떠 있든 그 위에
        }

        public void Close()
        {
            GameAudio.PlayClick();
            ReleaseKeyboard();
            if (window != null) window.SetActive(false);

            // 여기서 한 번만 디스크에 쓴다. 슬라이더를 끄는 동안 매 프레임 쓰면
            // 손잡이 한 번 옮기는 데 파일을 수십 번 건드린다.
            GameSettings.Save();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>
        /// 항복. 창을 먼저 닫고 매치에 넘긴다 — 그러지 않으면 판이 끝난 순간
        /// 순위표 위에 환경설정이 그대로 덮여 있다.
        ///
        /// 되묻지 않는다. 셋이 하는 판을 혼자 접는 버튼이라 확인 한 번쯤은 둘 만하지만,
        /// 지금은 누르면 곧바로 끝나는 것이 정해진 동작이다.
        /// </summary>
        public void Surrender()
        {
            if (!MatchSurrender.Available) return;

            Close();
            MatchSurrender.Request();
        }

        /// <summary>
        /// 창이 사라질 때 잡고 있던 키보드를 놓는다.
        ///
        /// 이름 칸을 잡은 채로 창을 닫으면 <c>onDeselect</c>가 오지 않는 경우가 있고,
        /// 그러면 <see cref="ChatFocus"/>가 참으로 굳어 캐릭터가 영영 걷지 못한다.
        /// </summary>
        private void ReleaseKeyboard()
        {
            if (inputNickname == null || !inputNickname.isFocused) return;
            inputNickname.DeactivateInputField();
            ChatFocus.SetTyping(false);
        }

        private void OnDisable() => ReleaseKeyboard();

        /// <summary>친 이름을 곧바로 받아 둔다. 디스크에는 창을 닫을 때 한 번 쓴다.</summary>
        private void OnNickname(string value)
        {
            if (!AcceptsInput) return;
            GameSettings.SetNickname(value);
        }

        /// <summary>지금 설정값을 위젯에 옮겨 담는다.</summary>
        private void Sync()
        {
            syncing = true;

            // 다듬어진 값으로 되돌려 담는다. 앞뒤 공백을 넣어 두었다면 다음에 열었을 때
            // 실제로 쓰이는 이름이 그대로 보인다.
            if (inputNickname != null) inputNickname.SetTextWithoutNotify(GameSettings.Nickname);

            SetSlider(sliderMaster, GameSettings.MasterVolume);
            SetSlider(sliderMusic, GameSettings.MusicVolume);
            SetSlider(sliderEffects, GameSettings.EffectsVolume);

            syncing = false;

            ShowPercent(txtMaster, GameSettings.MasterVolume);
            ShowPercent(txtMusic, GameSettings.MusicVolume);
            ShowPercent(txtEffects, GameSettings.EffectsVolume);
            ShowScreen();

            // 열 때마다 다시 묻는다. 같은 화면이 로비의 방에도 게임 씬에도 붙어 있고,
            // 매치는 이 화면이 닫혀 있는 동안 시작되고 끝난다.
            if (btnSurrender != null)
                btnSurrender.gameObject.SetActive(MatchSurrender.Available);
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
        }

        // ── 음량 ─────────────────────────────────────────────────────

        private void OnMaster(float value)
        {
            if (!AcceptsInput) return;
            GameSettings.SetMasterVolume(value);
            ShowPercent(txtMaster, value);
        }

        private void OnMusic(float value)
        {
            if (!AcceptsInput) return;
            GameSettings.SetMusicVolume(value);
            ShowPercent(txtMusic, value);
        }

        private void OnEffects(float value)
        {
            if (!AcceptsInput) return;
            GameSettings.SetEffectsVolume(value);
            ShowPercent(txtEffects, value);
        }

        private static void ShowPercent(TextMeshProUGUI label, float value)
        {
            if (label != null)
                label.text = ZooJackText.Get(
                    "Settings.Percent", "{0}%", Mathf.RoundToInt(value * 100f));
        }

        // ── 화면 ─────────────────────────────────────────────────────

        private void OnResolution(int index)
        {
            if (!AcceptsInput) return;
            GameAudio.PlayClick();

            // 목록은 큰 것이 위로 오게 뒤집어 담았다(ShowScreen). 되돌려서 넘긴다.
            GameSettings.SetSizeIndex(ToSizeIndex(index));
        }

        private void OnScreenMode(int index)
        {
            if (!AcceptsInput) return;
            GameAudio.PlayClick();
            GameSettings.SetFullScreen(index == 0);
        }

        /// <summary>목록에 담긴 자리 ↔ <see cref="GameSettings.Sizes"/>의 자리.</summary>
        private static int ToSizeIndex(int listed) => GameSettings.Sizes.Count - 1 - listed;

        private void ShowScreen()
        {
            if (dropResolution != null)
            {
                // 목록을 매번 다시 채운다. 창을 열어 둔 채 모니터를 바꿔 끼울 수도 있고,
                // 무엇보다 이 목록이 어떤 것인지는 실행 중에야 알 수 있다.
                //
                // 큰 해상도가 위로 오게 뒤집는다 — 대개 가장 큰 것을 고르므로
                // 목록을 열자마자 눈에 들어와야 한다.
                var options = new System.Collections.Generic.List<string>(GameSettings.Sizes.Count);
                for (int i = GameSettings.Sizes.Count - 1; i >= 0; i--)
                    options.Add($"{GameSettings.Sizes[i].Width}x{GameSettings.Sizes[i].Height}");

                dropResolution.ClearOptions();
                dropResolution.AddOptions(options);
                dropResolution.SetValueWithoutNotify(ToSizeIndex(GameSettings.SizeIndex));
                dropResolution.RefreshShownValue();
            }

            if (dropScreenMode != null)
            {
                dropScreenMode.ClearOptions();
                dropScreenMode.AddOptions(
                    new System.Collections.Generic.List<string>
                    {
                        ZooJackText.Get("Settings.Fullscreen", "전체 화면"),
                        ZooJackText.Get("Settings.Windowed", "창 모드")
                    });
                dropScreenMode.SetValueWithoutNotify(GameSettings.FullScreen ? 0 : 1);
                dropScreenMode.RefreshShownValue();
            }
        }
    }
}
