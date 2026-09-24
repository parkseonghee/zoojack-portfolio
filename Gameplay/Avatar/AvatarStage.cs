using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace ZooJack
{
    /// <summary>
    /// 테이블 위 캐릭터 세 마리를 관리한다.
    ///
    /// 로컬 캐릭터 하나만 키보드 입력으로 움직이고, 나머지는 밖에서 넣어준 목표 위치를
    /// 따라간다. 네트워크를 전혀 모르며, 움직인 결과를 <see cref="LocalPositionChanged"/>로
    /// 알리기만 한다. 그 이벤트를 누가 받아 어디로 보내는지는 디렉터의 몫이다.
    /// </summary>
    public class AvatarStage : MonoBehaviour
    {
        [Header("캐릭터")]
        [SerializeField] private PlayerAvatarView avatarPlayerA;
        [SerializeField] private PlayerAvatarView avatarPlayerB;
        [SerializeField] private PlayerAvatarView avatarDealer;

        [Header("감정표현")]
        [FormerlySerializedAs("emotionDisplayLayer")]
        [SerializeField] private RectTransform emotionBubbleLayer;
        [SerializeField] private EmotionCatalog emotionCatalog;
        [SerializeField] private EmotionBubbleView bubblePlayerA;
        [SerializeField] private EmotionBubbleView bubblePlayerB;
        [SerializeField] private EmotionBubbleView bubbleDealer;

        [Header("입력")]
        [Tooltip("끄면 키보드를 읽지 않는다. 원격 화면이나 리플레이에서 사용.")]
        [SerializeField] private bool readKeyboard = true;

        [Header("가림 방지")]
        [Tooltip("캐릭터가 카드나 베팅 칩 위에 서면 반투명해진다.")]
        [SerializeField] private bool fadeWhenCovering = true;

        [Tooltip("카드·칩 외에 추가로 가리면 안 되는 것들. 비워둬도 된다.")]
        [SerializeField] private RectTransform[] extraOccluders;

        /// <summary>키보드로 조종하는 역할. None이면 아무도 조종하지 않는다.</summary>
        public PlayerRole LocalRole { get; private set; } = PlayerRole.None;

        /// <summary>
        /// 로컬 캐릭터가 의미 있게 움직였을 때 호출된다. 매 프레임이 아니라
        /// <see cref="AvatarMovement.SendInterval"/> 간격으로 솎아서 올라온다.
        /// </summary>
        public event Action<Vector2> LocalPositionChanged;

        /// <summary>감정 이미지를 올리는 최상위 레이어. 아바타 투명도와 좌우 반전의 영향을 받지 않는다.</summary>
        public RectTransform EmotionBubbleLayer => emotionBubbleLayer;

        private Vector2 targetPlayerA, targetPlayerB, targetDealer;
        private float sendTimer;
        private Vector2 lastReported;
        private bool hasReported;

        private RectTransform[] cardOccluders = Array.Empty<RectTransform>();
        private BetChipStackView[] chipOccluders = Array.Empty<BetChipStackView>();

        private void Awake()
        {
            ResetPositions();
            CollectOccluders();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            DriveLocal(dt);
            FollowRemote(PlayerRole.PlayerA, avatarPlayerA, targetPlayerA, dt);
            FollowRemote(PlayerRole.PlayerB, avatarPlayerB, targetPlayerB, dt);
            FollowRemote(PlayerRole.Dealer,  avatarDealer,  targetDealer,  dt);

            // 위치가 확정된 뒤에 판정해야 한 프레임 늦게 흐려지지 않는다.
            UpdateOcclusion(dt);
        }

        // ── 외부 조작 ────────────────────────────────────────────────

        /// <summary>키보드로 조종할 역할을 정한다.</summary>
        public void SetLocalRole(PlayerRole role)
        {
            if (LocalRole == role) return;
            LocalRole = role;
            hasReported = false; // 조종 대상이 바뀌었으니 보고 이력을 리셋한다
        }

        /// <summary>원격 캐릭터의 목표 위치를 갱신한다. 로컬 역할이면 무시한다.</summary>
        public void SetRemoteTarget(PlayerRole role, Vector2 position)
        {
            if (role == LocalRole) return;
            SetTarget(role, AvatarMovement.Clamp(position));
        }

        private void SetTarget(PlayerRole role, Vector2 position)
        {
            if      (role == PlayerRole.PlayerA) targetPlayerA = position;
            else if (role == PlayerRole.PlayerB) targetPlayerB = position;
            else if (role == PlayerRole.Dealer)  targetDealer  = position;
        }

        /// <summary>
        /// 보간 없이 즉시 옮긴다. 캐릭터가 방금 나타났을 때 쓴다 —
        /// 목표만 주면 앞사람이 서 있던 자리에서 미끄러져 들어온다.
        /// </summary>
        public void Teleport(PlayerRole role, Vector2 position)
        {
            position = AvatarMovement.Clamp(position);
            SetTarget(role, position);
            ViewFor(role)?.Teleport(position);
        }

        /// <summary>해당 역할의 현재 위치.</summary>
        public Vector2 GetPosition(PlayerRole role)
        {
            var view = ViewFor(role);
            return view != null ? view.Position : Vector2.zero;
        }

        /// <summary>세 캐릭터를 각자의 시작 위치로 되돌린다.</summary>
        public void ResetPositions()
        {
            targetPlayerA = AvatarMovement.SpawnPositionFor(PlayerRole.PlayerA);
            targetPlayerB = AvatarMovement.SpawnPositionFor(PlayerRole.PlayerB);
            targetDealer  = AvatarMovement.SpawnPositionFor(PlayerRole.Dealer);

            avatarPlayerA?.Teleport(targetPlayerA);
            avatarPlayerB?.Teleport(targetPlayerB);
            avatarDealer?.Teleport(targetDealer);

            // 시작부터 서로 마주보게 한다.
            avatarPlayerA?.SetFacing(1f);
            avatarPlayerB?.SetFacing(-1f);

            hasReported = false;
        }

        /// <summary>캐릭터를 보이거나 숨긴다(관전자, 미접속 슬롯 등).</summary>
        public void SetVisible(PlayerRole role, bool visible) => ViewFor(role)?.SetVisible(visible);

        public bool IsVisible(PlayerRole role) => ViewFor(role)?.IsVisible == true;

        public void SetLabel(PlayerRole role, string text) => ViewFor(role)?.SetLabel(text);

        /// <summary>
        /// 낼 것을 낸 사람에게 붙는 표시.
        ///
        /// <b>로비와 게임 화면이 같은 글자와 같은 색을 쓴다.</b> 로비에서 준비를 마쳤을 때
        /// 뜨던 것과 뜻이 같기 때문이다 — "나는 끝냈고 이제 남을 기다린다". 두 화면이
        /// 저마다 다른 초록을 고르면 같은 뜻인 줄 알아보는 데 한 박자가 든다.
        /// </summary>
        public static string ReadyBadge => ZooJackText.Get("Lobby.Avatar.ReadyBadge", "준비 ✓");

        /// <inheritdoc cref="ReadyBadge"/>
        public static readonly Color ReadyBadgeColor = new Color(0.35f, 0.90f, 0.45f);

        /// <summary>
        /// 낼 것을 낸 사람의 명패 위에 '준비'를 붙인다.
        ///
        /// 행동 문구("선택하는 중...")와 <b>같은 알약</b>을 쓴다. 한 사람이 지금 어디까지
        /// 왔는지는 하나의 답이라, 자리도 하나여야 셋을 한 번에 훑을 수 있다.
        /// </summary>
        public void SetReady(PlayerRole role, bool ready) => ViewFor(role)?.SetReady(ready);

        /// <summary>
        /// 머리 위 명패. secondsLeft가 음수면 초를 숨기고, action이 비면 행동 문구를 숨긴다.
        /// </summary>
        public void SetPlate(PlayerRole role, string roleName, int secondsLeft, string action) =>
            ViewFor(role)?.SetPlate(roleName, secondsLeft, action);

        public void SetSprite(PlayerRole role, Sprite sprite) => ViewFor(role)?.SetSprite(sprite);

        public RectTransform GetEmotionAnchor(PlayerRole role) => ViewFor(role)?.EmotionAnchor;

        /// <summary>후속 감정 표시기가 사용할 앵커와 독립 표시 레이어를 함께 제공한다.</summary>
        public bool TryGetEmotionPresentation(
            PlayerRole role, out RectTransform anchor, out RectTransform layer)
        {
            anchor = GetEmotionAnchor(role);
            layer = emotionBubbleLayer;
            return anchor != null && layer != null;
        }

        /// <summary>역할별 Bubble을 재사용해 감정 연출을 처음부터 재생한다.</summary>
        public void ShowEmotion(PlayerRole role, Sprite sprite) => BubbleFor(role)?.Show(sprite);

        public void ShowEmotion(PlayerRole role, EmotionId emotionId) =>
            ShowEmotion(role, emotionCatalog != null ? emotionCatalog.SpriteFor(emotionId) : null);

        public void HideEmotion(PlayerRole role) => BubbleFor(role)?.HideImmediate();

        public void HideEmotions()
        {
            bubblePlayerA?.HideImmediate();
            bubblePlayerB?.HideImmediate();
            bubbleDealer?.HideImmediate();
        }

        // ── 내부 ─────────────────────────────────────────────────────

        private void DriveLocal(float dt)
        {
            var view = ViewFor(LocalRole);
            if (view == null) return;

            // 채팅에 글을 치는 동안에는 키보드가 저쪽 것이다. "wasd"라고 쳤다고
            // 캐릭터가 테이블을 가로지르면 안 된다.
            Vector2 input = readKeyboard && !ChatFocus.IsTyping ? ReadKeyboard() : Vector2.zero;
            Vector2 next = AvatarMovement.Step(view.Position, input, dt);
            view.Apply(next, dt);

            // 로컬 목표도 같이 끌고 간다. 조종 대상이 바뀌어도 캐릭터가 튀지 않는다.
            if      (LocalRole == PlayerRole.PlayerA) targetPlayerA = next;
            else if (LocalRole == PlayerRole.PlayerB) targetPlayerB = next;
            else if (LocalRole == PlayerRole.Dealer)  targetDealer  = next;

            ReportIfMoved(next, dt);
        }

        private void FollowRemote(PlayerRole role, PlayerAvatarView view, Vector2 target, float dt)
        {
            if (view == null || role == LocalRole) return;
            view.Apply(AvatarMovement.Follow(view.Position, target, dt), dt);
        }

        // 매 프레임 보내면 초당 60~144회가 되므로 간격과 최소 이동거리로 두 번 거른다.
        private void ReportIfMoved(Vector2 position, float dt)
        {
            sendTimer += dt;
            if (sendTimer < AvatarMovement.SendInterval) return;
            sendTimer = 0f;

            if (hasReported &&
                (position - lastReported).sqrMagnitude
                    < AvatarMovement.SendThreshold * AvatarMovement.SendThreshold)
                return;

            lastReported = position;
            hasReported = true;
            LocalPositionChanged?.Invoke(position);
        }

        // 새 Input System에서 WASD와 방향키를 함께 읽는다.
        // .inputactions 에셋 없이 키보드를 직접 보는 방식이라 씬 설정이 필요 없다.
        private static Vector2 ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero; // 키보드가 없는 플랫폼

            float x = 0f, y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)    y += 1f;
            return new Vector2(x, y);
        }

        // ── 가림 방지 ────────────────────────────────────────────────

        // 씬에 놓인 카드·칩을 한 번만 모아둔다. 카드는 손패가 비면 SetActive(false)로
        // 꺼지므로 비활성인 것까지 포함해 찾아야 나중에 켜졌을 때도 판정된다.
        //
        // 캐릭터보다 나중에 그려지는 것(딜러 패널 등)은 제외한다. 캐릭터가 그 위로
        // 지나가도 가려지는 건 캐릭터 쪽이므로, 흐려질 이유가 없다.
        // 계층은 실행 중 바뀌지 않는다고 보고 한 번만 계산한다.
        private void CollectOccluders()
        {
            var cards = new List<RectTransform>();
            foreach (var card in FindObjectsByType<CardView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!AvatarOcclusion.DrawsBefore(card.transform, transform)) continue;
                if (card.transform is RectTransform rect) cards.Add(rect);
            }
            cardOccluders = cards.ToArray();

            var chips = new List<BetChipStackView>();
            foreach (var stack in FindObjectsByType<BetChipStackView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (AvatarOcclusion.DrawsBefore(stack.transform, transform))
                    chips.Add(stack);
            chipOccluders = chips.ToArray();
        }

        private void UpdateOcclusion(float dt)
        {
            if (!fadeWhenCovering) return;

            FadeIfCovering(avatarPlayerA, dt);
            FadeIfCovering(avatarPlayerB, dt);
            FadeIfCovering(avatarDealer,  dt);
        }

        private void FadeIfCovering(PlayerAvatarView view, float dt)
        {
            if (view == null || !view.isActiveAndEnabled) return;
            view.ApplyOcclusion(IsCovering(view.WorldHitbox), dt);
        }

        private bool IsCovering(Rect body)
        {
            foreach (var card in cardOccluders)
            {
                if (card == null || !card.gameObject.activeInHierarchy) continue;
                if (AvatarOcclusion.Overlaps(body, AvatarOcclusion.WorldRect(card, Vector2.one)))
                    return true;
            }

            foreach (var chips in chipOccluders)
            {
                // 베팅 전에는 칩 자리가 비어 있다. 빈 자리까지 피하면
                // 테이블 앞에 서기만 해도 캐릭터가 계속 흐려진다.
                if (chips == null || chips.CurrentAmount <= 0) continue;
                if (!chips.gameObject.activeInHierarchy) continue;

                if (AvatarOcclusion.Overlaps(body, chips.WorldChipBounds()))
                    return true;
            }

            if (extraOccluders != null)
            {
                foreach (var extra in extraOccluders)
                {
                    if (extra == null || !extra.gameObject.activeInHierarchy) continue;
                    if (AvatarOcclusion.Overlaps(body, AvatarOcclusion.WorldRect(extra, Vector2.one)))
                        return true;
                }
            }

            return false;
        }

        private PlayerAvatarView ViewFor(PlayerRole role) => role switch
        {
            PlayerRole.PlayerA => avatarPlayerA,
            PlayerRole.PlayerB => avatarPlayerB,
            PlayerRole.Dealer  => avatarDealer,
            _                  => null
        };

        private EmotionBubbleView BubbleFor(PlayerRole role) => role switch
        {
            PlayerRole.PlayerA => bubblePlayerA,
            PlayerRole.PlayerB => bubblePlayerB,
            PlayerRole.Dealer  => bubbleDealer,
            _                  => null
        };
    }
}
