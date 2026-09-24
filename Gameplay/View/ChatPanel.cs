using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 화면 오른쪽 위의 채팅.
    ///
    /// <b>평소에는 창이 없다.</b> 오간 말만 테이블 위에 얹혀 있다가 조용히 옅어져 사라진다.
    /// Enter를 눌러야 그때 반투명 판이 깔리고 입력칸이 선다. 창을 늘 띄워 두면 아홉 라운드
    /// 내내 판의 한 귀퉁이를 가리는데, 정작 글을 치는 시간은 그중 몇 초뿐이다.
    ///
    /// <b>버튼이 없다.</b> 전송도 숨기기도 Enter와 Esc가 대신한다 — 버튼이 있으면 그것을
    /// 누르러 마우스가 화면을 가로질러야 하고, 그동안 판은 계속 가려져 있다.
    ///
    /// <b>말을 들고 있지 않다.</b> 주인은 <see cref="ChatLog"/>이고 여기는 그것을 그릴 뿐이다.
    /// 그래서 방 로비에서 나눈 이야기가 게임 씬의 채팅에 그대로 이어진다.
    ///
    /// <b>보내는 길도 모른다.</b> <see cref="ChatRelay"/>에 넘길 뿐이다. 아직 방에 붙지
    /// 않았으면 친 말이 내 화면에만 남고, 입력칸이 그 사실을 안내 글자로 알려 준다.
    ///
    /// 배선은 <c>ZooJack/게임/채팅 화면 만들기</c> 메뉴가 해 준다.
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        [Header("조각")]
        [Tooltip("채팅 전체의 투명도를 쥔다. 옅어지는 것도 클릭을 막는 것도 여기서 한다.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("글을 치는 동안에만 서는 것들 — 반투명 판과 입력칸.")]
        [SerializeField] private GameObject typingChrome;

        [SerializeField] private ScrollRect scroll;

        [Tooltip("오간 말 전체를 담는 한 덩어리. 줄마다 오브젝트를 만들지 않는다 — " +
                 "백 줄 남짓이라 통째로 다시 쓰는 편이 단순하고, 줄바꿈도 TMP가 맡는다.")]
        [SerializeField] private TextMeshProUGUI body;

        [SerializeField] private TMP_InputField input;

        [Tooltip("입력칸의 안내 글자. 보낼 곳이 없을 때는 그 사실을 대신 적는다.")]
        [SerializeField] private TextMeshProUGUI placeholder;

        [Header("옅어지기")]
        [Tooltip("새 말이 온 뒤 또렷하게 남아 있는 시간(초).")]
        [SerializeField, Min(0f)] private float holdSeconds = 6f;

        [Tooltip("다 옅어지기까지 걸리는 시간(초). 짧으면 사라지는 것이 아니라 깜빡이는 것으로 보인다.")]
        [SerializeField, Min(0.05f)] private float fadeSeconds = 1.5f;

        [Tooltip("다 옅어졌을 때의 투명도. 0이면 완전히 사라진다.")]
        [SerializeField, Range(0f, 1f)] private float fadedAlpha;

        /// <summary>보낼 곳이 없을 때. 칠 수는 있지만 남에게 가지 않는다는 뜻이다.</summary>
        private const string OfflineHint = "연결 전 — 나만 보입니다";

        /// <summary>
        /// 보내는 법과 무르는 법. <b>버튼이 없으므로 여기 말고는 적힐 데가 없다.</b>
        /// </summary>
        private const string ReadyHint = "Enter로 보내기 · Esc로 취소";

        /// <summary>마지막으로 적어 둔 안내 글자. 매 프레임 같은 글을 다시 넣지 않는다.</summary>
        private bool hintShowsOffline;

        /// <summary>보낸 프레임. 같은 Enter가 곧바로 입력칸을 다시 잡는 것을 막는다.</summary>
        private int submittedFrame = -1;

        /// <summary>다시 그린 뒤 맨 아래로 내려야 한다. 레이아웃이 잡힌 다음이라야 한다.</summary>
        private bool scrollPending;

        /// <summary>글을 치는 중인가. 입력칸의 isFocused가 아니라 이 값이 화면을 정한다.</summary>
        private bool typing;

        /// <summary>
        /// 입력칸이 실제로 키보드를 잡은 것을 한 번이라도 보았는가.
        ///
        /// <c>ActivateInputField</c>는 그 자리에서 듣지 않고 <b>다음 프레임</b>에 걸린다.
        /// 이것을 두지 않으면 Enter를 누른 바로 그 프레임에 "아직 안 잡았네" 하고 판을
        /// 도로 내려, 창이 한 번 깜빡이고 만다.
        /// </summary>
        private bool focusSettled;

        /// <summary>이 시각이 지나면 옅어지기 시작한다.</summary>
        private float visibleUntil;

        private void Awake()
        {
            if (input != null)
            {
                input.characterLimit = ChatLog.MaxTextLength;
                input.lineType = TMP_InputField.LineType.SingleLine;
                input.onSubmit.AddListener(_ => Send());

                // 걸어 다니는 것만으로 채팅창이 열리던 것을 막는다. UI의 Navigate가
                // WASD에 묶여 있어서, 보내고 난 뒤에도 EventSystem이 이 칸을 고른 채로
                // 두면 W를 누르는 순간 이웃 칸으로 고름이 옮겨 다니다 여기로 돌아오고,
                // TMP는 고르는 즉시 키보드를 가져간다. 걸음 대신 글자가 찍히는 이유다.
                //
                // 채팅칸은 Enter로만 잡는다 — 그 길은 이 두 줄과 무관하다.
                input.navigation = new Navigation { mode = Navigation.Mode.None };
                input.shouldActivateOnSelect = false;
            }
        }

        private void OnEnable()
        {
            ChatLog.Changed += OnChatChanged;

            // 손을 뗀 채로, 그리고 <b>옅어진 채로</b> 시작한다. 씬을 넘어오자마자 앞 씬의
            // 대화가 통째로 떠올랐다 사라지면, 아무도 말하지 않았는데 말이 오간 것처럼 보인다.
            StopTyping();
            visibleUntil = 0f;
            ApplyFade();
            Redraw();
        }

        private void OnDisable()
        {
            ChatLog.Changed -= OnChatChanged;
            StopTyping();
        }

        private void OnDestroy()
        {
            ChatLog.Changed -= OnChatChanged;
            StopTyping();
        }

        private void Update()
        {
            RefreshSendable();
            FollowFocus();
            ApplyFade();
            ReadKeyboard();
        }

        private void LateUpdate()
        {
            if (!scrollPending || scroll == null) return;
            scrollPending = false;

            // 방금 넣은 줄만큼 내용이 길어진 뒤라야 맨 아래를 알 수 있다.
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }

        // ── 치는 중 / 아닌 중 ────────────────────────────────────────

        private void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (typing)
            {
                // 무르고 싶을 때가 있다. Esc로 손을 떼면 곧바로 다시 걸어 다닐 수 있다.
                // 친 글은 지우지 않는다 — 다시 Enter를 누르면 이어서 쓴다.
                if (keyboard.escapeKey.wasPressedThisFrame) StopTyping();
                return;
            }

            if (input == null) return;
            if (Time.frameCount == submittedFrame) return;

            // 다른 입력칸이 키보드를 잡고 있으면(환경설정의 이름 칸) 비켜선다.
            // 이름을 치다 Enter를 누르면 채팅이 열리며 잡고 있던 키보드를 채 가고,
            // 치던 이름은 그대로 멈춘다.
            if (ChatFocus.IsTyping) return;

            if (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame)
                StartTyping();
        }

        /// <summary>
        /// 입력칸이 스스로 키보드를 놓았으면(딴 데를 눌렀다든지) 화면도 따라 내린다.
        /// 잡기까지 한 프레임이 걸리므로, <see cref="focusSettled"/> 전에는 기다린다.
        /// </summary>
        private void FollowFocus()
        {
            if (!typing) return;
            if (input == null) { StopTyping(); return; }

            if (input.isFocused) { focusSettled = true; return; }
            if (focusSettled) StopTyping();
        }

        private void StartTyping()
        {
            typing = true;
            focusSettled = false;

            // 입력칸이 살아 있어야 키보드를 줄 수 있다. 판을 먼저 세운다.
            if (typingChrome != null) typingChrome.SetActive(true);
            ChatFocus.SetTyping(true);
            Wake();

            if (input == null) return;
            input.ActivateInputField();
            input.Select();
        }

        /// <summary>
        /// 입력칸에서 손을 떼고 판을 내린다.
        ///
        /// <b>고름까지 풀어야 한다.</b> <c>DeactivateInputField</c>는 키보드만 놓고
        /// EventSystem이 고른 대상은 그대로 둔다. 고른 채로 남으면 그 칸은 계속 UI
        /// 이동 신호를 받으므로, 방금 말을 마친 사람이 걸어가려고 WASD를 누른 순간
        /// 채팅이 저절로 열린다.
        /// </summary>
        private void StopTyping()
        {
            typing = false;
            focusSettled = false;

            if (input != null)
            {
                input.DeactivateInputField();

                var events = EventSystem.current;
                if (events != null && events.currentSelectedGameObject == input.gameObject)
                    events.SetSelectedGameObject(null);
            }

            if (typingChrome != null) typingChrome.SetActive(false);

            // <b>무조건</b> 쓴다 — 기억해 둔 값과 정적의 값이 어긋나 있으면
            // 조건부 쓰기는 그것을 고치지 못한다.
            ChatFocus.SetTyping(false);
        }

        // ── 보내기 ───────────────────────────────────────────────────

        private void Send()
        {
            if (input == null) return;

            string text = input.text;
            input.text = string.Empty;
            submittedFrame = Time.frameCount;

            // 보낸 뒤에는 손을 뗀다. 잡은 채로 두면 이어서 치기는 편하지만 캐릭터가
            // 움직이지 않는 이유를 아무도 모른 채 한참을 헤맨다.
            StopTyping();
            Wake();

            ChatRelay.Send(text);
        }

        /// <summary>
        /// 안내 글자만 갈아 끼운다.
        ///
        /// <b>칸을 잠그지 않는다.</b> 예전에는 보낼 곳이 없으면 입력칸을 잠갔는데, 잠긴
        /// 이유가 화면 어디에도 없어 "채팅이 안 쳐진다"로만 보였다. 언제나 칠 수 있게 두고
        /// 남에게 가지 않는다는 사실은 말로 적는다.
        /// </summary>
        private void RefreshSendable()
        {
            bool can = ChatRelay.Available;
            if (placeholder == null || hintShowsOffline == !can) return;

            hintShowsOffline = !can;
            placeholder.text = can
                ? ZooJackText.Get("Chat.Placeholder.Ready", ReadyHint)
                : ZooJackText.Get("Chat.Placeholder.Offline", OfflineHint);
        }

        // ── 옅어지기 ─────────────────────────────────────────────────

        /// <summary>다시 또렷하게. 새 말이 왔거나 방금 무언가를 했을 때 부른다.</summary>
        private void Wake() => visibleUntil = Time.unscaledTime + holdSeconds;

        /// <summary>
        /// 시간이 지난 만큼 옅어진다. 게임이 멈춰 있어도 흘러야 하므로 unscaled를 쓴다 —
        /// 최종 판정 연출 중에 온 말이 화면에 못 박힌 채로 남으면 안 된다.
        /// </summary>
        private void ApplyFade()
        {
            if (group == null) return;

            float alpha;
            if (typing)
            {
                // 치는 동안에는 사라지지 않는다. 무슨 말이 오갔는지 보면서 답해야 한다.
                alpha = 1f;
                visibleUntil = Time.unscaledTime + holdSeconds;
            }
            else
            {
                float over = Time.unscaledTime - visibleUntil;
                alpha = over <= 0f
                    ? 1f
                    : Mathf.Lerp(1f, fadedAlpha, Mathf.Clamp01(over / fadeSeconds));
            }

            if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;

            // 치지 않는 동안에는 클릭을 받지 않는다. 옅어져 보이지도 않는 판이 테이블 위를
            // 덮고 앉아 캐릭터도 감정표현도 눌리지 않던 것이 이 한 줄로 사라진다.
            if (group.blocksRaycasts != typing) group.blocksRaycasts = typing;
        }

        // ── 그리기 ───────────────────────────────────────────────────

        private void OnChatChanged()
        {
            Wake();
            Redraw();
        }

        private void Redraw()
        {
            if (body == null) return;

            var lines = ChatLog.Lines;
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');

                var line = lines[i];
                // 이름은 그 사람의 동물 색으로 적는다. 매치 내내 서로를 부르던 호칭이
                // 토끼·여우·악어라, 머니바·순위표와 같은 색을 쓰면 누가 한 말인지가
                // 이름을 읽기 전에 보인다.
                sb.Append("<color=#")
                  .Append(ColorUtility.ToHtmlStringRGB(ZooJackPalette.CharacterAccent(line.Speaker)))
                  .Append('>')
                  .Append(line.Who)
                  .Append("</color>: ")
                  .Append(line.Text);
            }

            body.text = sb.ToString();
            scrollPending = true;
        }
    }
}
