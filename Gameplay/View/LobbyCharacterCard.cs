using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>로비 캐릭터 카드가 나타낼 수 있는 상태. 색과 문구는 여기서만 갈린다.</summary>
    public enum LobbyCardState
    {
        /// <summary>아무도 고르지 않았다. 누를 수 있다.</summary>
        Free,

        /// <summary>내가 골랐다. 아직 준비 전이라 다른 카드로 바꾸거나 취소할 수 있다.</summary>
        Mine,

        /// <summary>내가 고르고 준비까지 마쳤다. 준비를 풀기 전에는 못 바꾼다.</summary>
        MineReady,

        /// <summary>남이 앉아 있다. 뺏을 수 없다.</summary>
        Taken,

        /// <summary>남이 앉아 준비까지 마쳤다.</summary>
        TakenReady
    }

    /// <summary>
    /// 로비 아래쪽 캐릭터 선택 카드 한 장. 동물 하나가 곧 자리 하나이므로
    /// (<see cref="CharacterIdentity.RoleFor"/>) 카드에는 그 자리의 역할만 표시한다.
    ///
    /// 생김새는 게임 화면의 잔액 카드(<c>ZJ_BalanceCard_*</c>)를 구운 프리팹을 그대로 쓴다.
    /// 자리 색(파랑·갈색·자주)까지 같은 값이라 로비와 게임이 한 화면처럼 이어진다.
    ///
    /// 상태 판단은 <see cref="NetworkLobbyManager"/>가 하고 여기서는 그리기만 한다 —
    /// 카드가 네트워크 상태를 직접 읽으면 로비 밖(에디터 미리보기 등)에서 쓸 수 없다.
    /// </summary>
    public class LobbyCharacterCard : MonoBehaviour
    {
        [Header("이 카드가 나타내는 캐릭터")]
        [SerializeField] private CharacterId character = CharacterId.Rabbit;

        [Header("조각")]
        [SerializeField] private Button button;
        [SerializeField] private RoundedPanelGraphic panel;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;   // 작은 줄: "플레이어 A"
        [SerializeField] private TextMeshProUGUI stateLabel;  // 큰 줄: 빈자리 / 내 캐릭터 / ✓ 준비 완료

        [Header("선택 안내")]
        [Tooltip("아직 아무 자리도 고르지 않은 사람에게만 뜨는 화살표. 카드 위에서 위아래로 오간다.\n\n" +
                 "비워 두면 안내 없이 동작한다 — 방 화면 도구가 붙여 준다.")]
        [SerializeField] private RectTransform hintArrow;

        [Header("색")]
        [Tooltip("고를 수 없는 카드에 곱하는 색조. 어둡게만 할 수 있다(버텍스 색은 1을 넘지 못한다).")]
        [SerializeField] private Color dimTint = new Color(0.45f, 0.45f, 0.47f, 1f);
        [SerializeField] private Color selectedBorder = new Color(0.82f, 0.57f, 0.18f, 1f);
        [SerializeField, Min(0f)] private float selectedBorderThickness = 3f;
        [SerializeField] private Color textName = new Color(0.74f, 0.69f, 0.58f);
        [SerializeField] private Color textState = new Color(0.94f, 0.90f, 0.79f);
        [SerializeField] private Color textReady = new Color(0.35f, 0.90f, 0.45f);
        [SerializeField] private Color textSelected = new Color(0.95f, 0.78f, 0.36f);

        /// <summary>고른 캐릭터가 눌렸을 때. 인자는 이 카드의 캐릭터.</summary>
        public event System.Action<CharacterId> Clicked;

        public CharacterId Character => character;

        /// <summary>이 카드가 앉히는 자리. 동물과 1:1이다.</summary>
        public PlayerRole Role => CharacterIdentity.RoleFor(character);

        public Button Button => button;

        // 속도는 승리 왕관과 같다(GameDirector.MarkerBobDuration). 화면 안에서 "이걸
        // 눌러라"라고 말하는 움직임이 둘인데 서로 다른 리듬이면 각각을 따로 익혀야 한다.
        //
        // 폭만 왕관(9)보다 한 칸 좁다. 화살표가 쉬는 자리는 카드 윗변에서 7px이고
        // (LobbyRoomSetupTool.HintGap) 여기에 이 폭을 더한 15px가 가장 높이 뜨는 자리다.
        private const float HintBobDistance = 8f;
        private const float HintBobDuration = 0.9f;

        // 화살표의 원래 자리. 트윈이 anchoredPosition을 건드리므로 기준점을 처음 한 번만
        // 붙잡아 두지 않으면 껐다 켤 때마다 조금씩 위로 밀려 올라간다.
        private Vector2 hintHome;
        private bool hintHomeKnown;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(character));
            if (nameLabel != null) nameLabel.text = HeadlineFor(character);
        }

        private void OnDisable()
        {
            // 방 화면이 통째로 꺼질 때 트윈만 살아남으면, 다시 켰을 때 화살표가
            // 엉뚱한 높이에서 멈춰 있다.
            if (hintArrow == null) return;
            hintArrow.DOKill();
            if (hintHomeKnown) hintArrow.anchoredPosition = hintHome;
        }

        /// <summary>카드 위쪽 작은 줄. 에디터 조립 도구도 같은 문구를 쓴다.</summary>
        public static string HeadlineFor(CharacterId id) =>
            RoleKo(CharacterIdentity.RoleFor(id));

        /// <summary>
        /// 카드를 상태에 맞춰 다시 그린다. 매 프레임 불리므로 값이 실제로 바뀔 때만 손댄다
        /// — TMP는 같은 문자열을 다시 넣어도 메시를 새로 만든다.
        /// </summary>
        /// <param name="guide">
        /// 이 사람이 아직 아무 자리도 고르지 않았는지. 참이면 빈 카드 위에 화살표가 뜬다.
        /// 고르고 나면 안내는 사라진다 — 다 아는 사람에게 계속 흔들어 보이면 잔소리다.
        /// </param>
        public void Render(LobbyCardState state, bool guide = false)
        {
            bool mine = state is LobbyCardState.Mine or LobbyCardState.MineReady;
            bool ready = state is LobbyCardState.MineReady or LobbyCardState.TakenReady;
            bool taken = state is LobbyCardState.Taken or LobbyCardState.TakenReady;

            // 빈 카드는 앉으려고, 내 카드는 다시 눌러 취소하려고 누른다.
            // 준비를 마쳤으면 둘 다 잠긴다 — 준비를 풀어야 자리를 옮긴다.
            bool clickable = state is LobbyCardState.Free or LobbyCardState.Mine;
            if (button != null && button.interactable != clickable)
                button.interactable = clickable;

            // 아직 아무 데도 앉지 않은 사람에게, 앉을 수 있는 자리만 가리킨다.
            // 남이 앉은 카드에도 화살표를 세우면 누를 수 없는 것을 누르라고 가리키는 셈이다.
            ShowHint(guide && state == LobbyCardState.Free);

            if (panel != null)
            {
                // 남이 앉은 자리는 어둡게 눌러 둔다. 자리 고유색(파랑·갈색·자주)은
                // 프리팹 인스턴스에 그대로 두고 색조만 곱한다.
                var tint = taken ? dimTint : Color.white;
                if (panel.color != tint) panel.color = tint;

                // 내가 고른 자리에만 금색 테두리를 두른다. 색조로는 밝게 만들 수 없어서
                // "선택됨"은 테두리로 표시한다.
                panel.SetBorder(mine ? selectedBorder : Color.clear, mine ? selectedBorderThickness : 0f);
            }

            if (icon != null)
            {
                var tint = taken ? dimTint : Color.white;
                if (icon.color != tint) icon.color = tint;
            }

            SetText(nameLabel, HeadlineFor(character), taken ? textName * dimTint : textName);
            SetText(stateLabel, StateText(state),
                ready ? textReady : mine ? textSelected : taken ? textName : textState);
        }

        private static string StateText(LobbyCardState state) => state switch
        {
            LobbyCardState.Free => ZooJackText.Get("Lobby.Card.Free", "빈자리"),
            LobbyCardState.Mine => ZooJackText.Get("Lobby.Card.Mine", "내 캐릭터"),
            LobbyCardState.MineReady => ZooJackText.Get("Lobby.Card.Ready", "✓ 준비 완료"),
            LobbyCardState.Taken => ZooJackText.Get("Lobby.Card.Taken", "다른 플레이어"),
            LobbyCardState.TakenReady => ZooJackText.Get("Lobby.Card.Ready", "✓ 준비 완료"),
            _                         => string.Empty
        };

        /// <summary>
        /// 화살표를 띄우고 위아래로 오가게 한다. 매 프레임 불리므로 <b>이미 오가는 중이면
        /// 손대지 않는다</b> — 매번 트윈을 새로 걸면 화살표가 첫 자리에서 떨지 못한다.
        /// </summary>
        private void ShowHint(bool show)
        {
            if (hintArrow == null) return;

            if (!hintHomeKnown)
            {
                hintHome = hintArrow.anchoredPosition;
                hintHomeKnown = true;
            }

            bool up = hintArrow.gameObject.activeSelf;
            if (show && up && DOTween.IsTweening(hintArrow)) return;
            if (!show && !up) return;

            hintArrow.DOKill();
            hintArrow.anchoredPosition = hintHome;
            hintArrow.gameObject.SetActive(show);
            if (!show) return;

            hintArrow.DOAnchorPosY(hintHome.y + HintBobDistance, HintBobDuration)
                     .SetEase(Ease.InOutSine)
                     .SetLoops(-1, LoopType.Yoyo);
        }

        private static void SetText(TextMeshProUGUI label, string text, Color color)
        {
            if (label == null) return;
            if (label.text != text) label.text = text;
            if (label.color != color) label.color = color;
        }

        private static string RoleKo(PlayerRole role) =>
            role == PlayerRole.None ? string.Empty : ZooJackText.RoleName(role);
    }
}
