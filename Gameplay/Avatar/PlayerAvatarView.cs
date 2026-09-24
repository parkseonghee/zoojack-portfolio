using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 테이블 위를 돌아다니는 동물 캐릭터 하나. UGUI Image라서 캔버스 좌표계로 움직인다.
    ///
    /// 위치는 <see cref="Position"/>이 단일 원천이고, RectTransform에는 걷기 흔들림(bob)이
    /// 더해진 값이 들어간다. 흔들림을 위치에 누적하면 가만히 서 있어도 캐릭터가 표류한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PlayerAvatarView : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private TextMeshProUGUI nameLabel;

        // ── 머리 위 명패 ─────────────────────────────────────────────
        // 직군과 남은 시간을 캐릭터 위에 띄운다. 남들이 "지금 누구 차례이고
        // 얼마나 남았나"를 화면 어디를 보든 알 수 있어야 하기 때문에,
        // 상단 타이머 카드 하나가 아니라 사람마다 붙는다.
        [Header("머리 위 명패")]
        [SerializeField] private RectTransform plateRoot;
        [SerializeField] private TextMeshProUGUI plateName;
        [SerializeField] private TextMeshProUGUI plateTimer;
        [SerializeField] private RectTransform emotionAnchor;

        // 명패 바로 위에 얹히는 작은 알약. "지금 이 사람이 무엇을 하는 중인가"를
        // 그 사람 머리 위에서 말한다. 화면 귀퉁이의 대기 상자가 하던 일인데,
        // 귀퉁이는 누구를 기다리는지를 말할 수 없어 이 자리로 옮겼다.
        //
        // 다 낸 사람의 '준비'도 같은 알약에 뜬다. 자리를 나누면 같은 물음("이 사람은
        // 지금 어디까지 왔나")의 답이 머리 위와 발밑에 흩어져, 셋을 훑는 데 눈이 두 번 간다.
        [SerializeField] private RectTransform plateActionRoot;
        [SerializeField] private TextMeshProUGUI plateAction;

        [Header("감정표현 앵커 높이")]
        [Tooltip("알약이 없을 때 말풍선이 앉는 높이(명패 윗변 기준 px).")]
        [SerializeField] private float emotionAnchorRestY = 10f;

        [Tooltip("알약이 섰을 때의 높이. 알약을 비켜설 만큼 올라가야 한다.")]
        [SerializeField] private float emotionAnchorRaisedY = 35f;

        [Header("걷기 애니메이션")]
        [SerializeField] private CharacterWalkAnimationCatalog walkAnimationCatalog;
        [SerializeField] private RectTransform characterVisual;
        [SerializeField] private Animator characterAnimator;

        [Header("크기")]
        [SerializeField] private Vector2 size = new Vector2(168f, 112f);

        [Header("걷기 연출")]
        [Tooltip("걸을 때 위아래로 흔들리는 폭(px). 0이면 흔들리지 않는다.")]
        [SerializeField, Min(0f)] private float bobHeight = 5f;
        [SerializeField, Min(0f)] private float bobSpeed = 11f;

        [Tooltip("이 속도(px/초) 이상이면 '걷는 중'으로 본다.")]
        [SerializeField] private float walkThreshold = 8f;

        [Header("가림 방지")]
        [Tooltip("카드·칩을 가리고 섰을 때의 투명도. 1이면 투명해지지 않는다.")]
        [SerializeField, Range(0f, 1f)] private float fadedAlpha = AvatarOcclusion.FadedAlpha;

        [Tooltip("겹침 판정에 쓰는 몸통 크기 비율(이미지 168x112 기준). " +
                 "줄이면 더 깊이 겹쳐야 흐려지고, 키우면 살짝만 닿아도 흐려진다.")]
        [SerializeField] private Vector2 hitboxScale = new Vector2(0.45f, 0.75f);

        /// <summary>지금 알약에 떠 있어야 할 행동 문구. 없으면 null.</summary>
        private string plateActionText;

        /// <summary>낼 것을 낸 사람인가. 행동 문구가 없을 때만 알약에 뜬다.</summary>
        private bool ready;

        /// <summary>알약 글자의 본래 색. '준비'만 초록으로 바꿔 끼우고 나머지는 이 색이다.</summary>
        private Color plateActionColor = Color.white;

        private RectTransform rect;
        private CanvasGroup group;
        private Graphic plateActionBackground;
        private float bobPhase;
        private Sprite idleSprite;
        private CharacterWalkAnimationCatalog.Entry walkAnimation;
        private static readonly int IsWalkingParameter = Animator.StringToHash("IsWalking");

        /// <summary>흔들림을 제외한 실제 위치. 이동 로직이 보는 값이다.</summary>
        public Vector2 Position { get; private set; }

        /// <summary>이번 프레임에 걷고 있는지.</summary>
        public bool IsWalking { get; private set; }

        /// <summary>명패 상단 위에 있는 감정표현 위치 기준점.</summary>
        public RectTransform EmotionAnchor => emotionAnchor;

        public bool IsVisible => gameObject.activeInHierarchy;

        private RectTransform Rect => rect != null ? rect : (rect = GetComponent<RectTransform>());

        /// <summary>
        /// 겹침 판정에 쓰는 몸통 상자(월드 기준). 이름표는 자식이라 포함되지 않는다
        /// — 이름표가 카드에 닿았다고 캐릭터를 지울 이유는 없다.
        /// </summary>
        public Rect WorldHitbox => AvatarOcclusion.WorldRect(Rect, hitboxScale);

        private void Awake()
        {
            ApplySize();
            EnsureGroup();
            InitializeAnimator();

            // 씨에서 정해 둔 색을 기억해 둔다. 여기서 잃어버리면 '준비'를 한 번 띄운 뒤
            // 모든 행동 문구가 초록으로 남는다.
            if (plateAction != null) plateActionColor = plateAction.color;

            // 씬에 배치된 자리를 출발점으로 삼는다. 이걸 빼면 Teleport를 한 번도
            // 받기 전까지 Position이 (0,0)으로 남아, 그 값을 "원래 자리"로 기억해 둔
            // 쪽(최종 판정 무대 등)이 캐릭터를 화면 한가운데로 되돌린다.
            Position = Rect.anchoredPosition;
        }

        /// <summary>보간 없이 즉시 옮긴다. 스폰·리스폰용.</summary>
        public void Teleport(Vector2 position)
        {
            Position = position;
            bobPhase = 0f;
            IsWalking = false;
            SetAnimatorWalking(false);
            PushToTransform();
        }

        /// <summary>
        /// 위치를 갱신하고 연출을 적용한다. 이동 주체(입력 또는 네트워크)가 계산한
        /// 최종 위치를 그대로 넘긴다.
        /// </summary>
        public void Apply(Vector2 position, float deltaTime)
        {
            float moved = (position - Position).magnitude;
            IsWalking = deltaTime > 0f && moved / deltaTime > walkThreshold;

            // 좌우로 움직일 때만 방향을 바꾼다. 위아래로만 갈 땐 보던 쪽을 유지한다.
            float dx = position.x - Position.x;
            if (Mathf.Abs(dx) > 0.01f) SetFacing(dx);

            Position = position;

            // 실제 걷기 프레임이 있으면 시트 자체의 상하 동작만 사용한다.
            // 기존 procedural bob까지 겹치면 캐릭터와 Plate가 이중으로 튄다.
            if (IsWalking && walkAnimation?.CanAnimate != true) bobPhase += deltaTime * bobSpeed;
            else bobPhase = Mathf.MoveTowards(bobPhase % (Mathf.PI * 2f), 0f, deltaTime * bobSpeed);

            SetAnimatorWalking(IsWalking);
            PushToTransform();
        }

        /// <summary>바라보는 방향. dirX가 음수면 왼쪽을 본다.</summary>
        public void SetFacing(float dirX)
        {
            if (Mathf.Abs(dirX) < 0.01f) return;
            var scale = Rect.localScale;
            scale.x = Mathf.Abs(scale.x) * (dirX < 0f ? -1f : 1f);
            Rect.localScale = scale;

            // 캐릭터를 뒤집어도 글자는 읽을 수 있어야 한다. 부모가 뒤집혔으므로
            // 자식을 한 번 더 뒤집어 원래 방향으로 되돌린다.
            CounterFlip(nameLabel != null ? nameLabel.rectTransform : null, dirX);
            CounterFlip(plateRoot, dirX);
        }

        private static void CounterFlip(RectTransform target, float dirX)
        {
            if (target == null) return;
            var scale = target.localScale;
            scale.x = Mathf.Abs(scale.x) * (dirX < 0f ? -1f : 1f);
            target.localScale = scale;
        }

        public void SetSprite(Sprite sprite)
        {
            if (image == null) return;
            if (idleSprite == sprite)
            {
                if (walkAnimation == null && walkAnimationCatalog != null)
                    walkAnimation = walkAnimationCatalog.FindByIdleSprite(sprite);
                var expected = walkAnimation?.CanAnimate == true
                    ? walkAnimation.AnimatorController
                    : null;
                if (characterAnimator != null &&
                    characterAnimator.runtimeAnimatorController != expected)
                    ConfigureAnimatorController();
                return;
            }

            idleSprite = sprite;
            walkAnimation = walkAnimationCatalog != null
                ? walkAnimationCatalog.FindByIdleSprite(sprite)
                : null;
            ConfigureAnimatorController();
        }

        private void InitializeAnimator()
        {
            if (image == null) return;
            if (characterVisual == null) characterVisual = image.rectTransform;
            if (characterAnimator == null && characterVisual != null)
                characterAnimator = characterVisual.GetComponent<Animator>();
            idleSprite = image.sprite;
            walkAnimation = walkAnimationCatalog != null
                ? walkAnimationCatalog.FindByIdleSprite(idleSprite)
                : null;
            ConfigureAnimatorController();
        }

        private void ConfigureAnimatorController()
        {
            if (image == null) return;

            image.sprite = idleSprite;
            image.enabled = idleSprite != null;
            image.preserveAspect = true;

            if (characterVisual != null && characterVisual != Rect)
            {
                characterVisual.anchorMin = Vector2.zero;
                characterVisual.anchorMax = Vector2.one;
                characterVisual.pivot = new Vector2(0.5f, 0.5f);
                characterVisual.offsetMin = Vector2.zero;
                characterVisual.offsetMax = Vector2.zero;
                characterVisual.localScale = Vector3.one;
            }

            if (characterAnimator == null) return;
            var controller = walkAnimation?.CanAnimate == true
                ? walkAnimation.AnimatorController
                : null;
            if (characterAnimator.runtimeAnimatorController != controller)
            {
                characterAnimator.runtimeAnimatorController = controller;
                characterAnimator.Rebind();
                characterAnimator.Update(0f);
            }
            SetAnimatorWalking(IsWalking);
        }

        private void SetAnimatorWalking(bool walking)
        {
            if (characterAnimator == null ||
                characterAnimator.runtimeAnimatorController == null) return;
            characterAnimator.SetBool(IsWalkingParameter, walking);
        }

        /// <summary>
        /// 발밑 이름표. 매 프레임 호출될 수 있으므로 값이 실제로 바뀔 때만 손댄다
        /// — TMP는 같은 문자열을 다시 넣어도 메시를 새로 만든다.
        /// </summary>
        public void SetLabel(string text)
        {
            if (nameLabel == null) return;

            // 글꼴에 없는 글자는 두부(□)로 찍힌다. 여기에 오는 것은 '✓ 준비'처럼
            // 본문에 잘 안 쓰이는 기호를 품고 있어, 넣기 전에 아틀라스에 새겨 둔다.
            if (nameLabel.font != null && !string.IsNullOrEmpty(text))
                nameLabel.font.TryAddCharacters(text, out _);

            if (nameLabel.text != text) nameLabel.text = text;

            bool show = !string.IsNullOrEmpty(text);
            if (nameLabel.gameObject.activeSelf != show) nameLabel.gameObject.SetActive(show);

            // LobbyScene의 준비 표시는 GameScene의 PlateAction이 아니라 이 라벨을 쓴다.
            // 준비 표시가 떠 있는 동안에는 감정표현도 같은 높이(35px)로 올려 겹치지 않게 한다.
            RaiseEmotionAnchor(show);
        }

        /// <summary>
        /// 머리 위 명패를 갱신한다. <paramref name="secondsLeft"/>가 음수면 초를 숨기고
        /// <paramref name="action"/>이 비면 행동 문구를 숨긴다 — 둘 다 시계를 쥔 사람에게만
        /// 붙어야 누구를 기다리는지 한눈에 보인다.
        ///
        /// 이 메서드는 매 프레임 호출되므로 값이 실제로 바뀔 때만 손댄다.
        /// TMP는 같은 문자열을 다시 넣어도 메시를 다시 만든다.
        /// </summary>
        public void SetPlate(string roleName, int secondsLeft, string action)
        {
            bool hasName = !string.IsNullOrEmpty(roleName);

            if (plateRoot != null && plateRoot.gameObject.activeSelf != hasName)
                plateRoot.gameObject.SetActive(hasName);

            // 이름이 없다는 것은 이 자리가 비었다는 뜻이다. 알약도 같이 내린다 —
            // 남겨 두면 아무도 없는 자리 위에 "고민하는 중..."이 떠 있게 된다.
            plateActionText = hasName ? action : null;
            if (!hasName) ready = false;
            ApplyPlateAction();
            if (!hasName) return;

            if (plateName != null && plateName.text != roleName)
                plateName.text = roleName;

            if (plateTimer == null) return;

            bool showTimer = secondsLeft >= 0;
            if (plateTimer.gameObject.activeSelf != showTimer)
                plateTimer.gameObject.SetActive(showTimer);

            if (!showTimer) return;
            string seconds = secondsLeft + "s";
            if (plateTimer.text != seconds) plateTimer.text = seconds;
        }

        /// <summary>
        /// 낼 것을 냈는가. 행동 문구가 없을 때만 알약에 <see cref="AvatarStage.ReadyBadge"/>로 뜬다.
        /// </summary>
        public void SetReady(bool value)
        {
            if (ready == value) return;
            ready = value;
            ApplyPlateAction();
        }

        /// <summary>
        /// 명패 위 알약 하나에 지금 무엇을 적을지 정한다.
        ///
        /// <b>한 자리에 둘이 들어올 수는 없다.</b> 지금 무엇을 하는 중인지가 먼저고,
        /// 그것이 없을 때만 '다 냈다'가 뜬다. 시계를 쥔 사람에게는 준비가 붙지 않으므로
        /// 실제로 둘이 겹치는 순간은 없지만, 순서를 정해 두면 어느 쪽이 먼저 들어오든 같다.
        ///
        /// 글자가 아니라 <b>알약째로</b> 켜고 끈다 — 글자만 지우면 빈 알약이 명패 위에
        /// 그대로 떠 있는다.
        /// </summary>
        private void ApplyPlateAction()
        {
            string text = !string.IsNullOrEmpty(plateActionText) ? plateActionText
                        : ready ? AvatarStage.ReadyBadge
                        : null;

            bool show = !string.IsNullOrEmpty(text);
            bool showReadyBadge = show && string.IsNullOrEmpty(plateActionText) && ready;

            if (plateActionRoot != null && plateActionRoot.gameObject.activeSelf != show)
                plateActionRoot.gameObject.SetActive(show);

            // 진행 중 문구는 기존 알약 배경을 유지하되, 행동을 마친 뒤의 '준비 ✓'는
            // LobbyScene과 동일하게 배경 없이 체크 표시만 보인다.
            if (plateActionRoot != null)
            {
                var background = plateActionBackground != null
                    ? plateActionBackground
                    : (plateActionBackground = plateActionRoot.GetComponent<Graphic>());
                if (background != null && background.enabled == showReadyBadge)
                    background.enabled = !showReadyBadge;
            }

            if (show && plateAction != null)
            {
                if (plateAction.text != text)
                {
                    // 글꼴에 없는 글자는 두부(□)로 찍힌다. '준비 ✓'의 체크가 그렇다.
                    if (plateAction.font != null) plateAction.font.TryAddCharacters(text, out _);
                    plateAction.text = text;
                }

                // 끝난 사람은 초록, 아직인 사람은 제색 — 로비에서 준비를 마쳤을 때와
                // 같은 초록이라, 읽기 전에 색만 보고도 누가 남았는지가 보인다.
                Color color = string.IsNullOrEmpty(plateActionText)
                    ? AvatarStage.ReadyBadgeColor
                    : plateActionColor;
                if (plateAction.color != color) plateAction.color = color;
            }

            RaiseEmotionAnchor(show);
        }

        /// <summary>
        /// 감정표현 말풍선이 올라앉는 자리. 알약이 서면 그만큼 위로 비켜 준다.
        ///
        /// <b>말풍선이 떴는지가 아니라 알약이 섰는지를 본다.</b> 비켜설 것이 있을 때만
        /// 비켜야, 알약이 아예 없는 화면(로비)에서 말풍선이 이유 없이 머리 위로 떠오르지
        /// 않는다. 알약이 켜지고 꺼지는 것은 단계가 바뀔 때뿐이라 말풍선이 떠 있는 동안
        /// 자리가 흔들릴 일도 없다.
        /// </summary>
        private void RaiseEmotionAnchor(bool raised)
        {
            if (emotionAnchor == null) return;

            float y = raised ? emotionAnchorRaisedY : emotionAnchorRestY;
            Vector2 position = emotionAnchor.anchoredPosition;
            if (Mathf.Approximately(position.y, y)) return;
            emotionAnchor.anchoredPosition = new Vector2(position.x, y);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>
        /// 무언가를 가리고 있으면 반투명해지고, 비켜서면 돌아온다.
        /// 판정은 밖에서 하고 여기서는 연출만 한다.
        /// </summary>
        public void ApplyOcclusion(bool covering, float deltaTime)
        {
            var canvasGroup = EnsureGroup();
            float target = covering ? fadedAlpha : 1f;
            float next = AvatarOcclusion.StepAlpha(canvasGroup.alpha, target, deltaTime);
            if (canvasGroup.alpha != next) canvasGroup.alpha = next;
        }

        // CanvasGroup 하나로 캐릭터와 이름표가 함께 흐려진다. Image.color만 건드리면
        // 이름표는 또렷하게 남아 오히려 더 눈에 띈다.
        private CanvasGroup EnsureGroup()
        {
            if (group != null) return group;

            group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
            }

            // 캐릭터는 클릭 대상이 아니다. 카드 위에 서 있을 때 밑의 버튼을 막으면 곤란하다.
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private void PushToTransform()
        {
            float bob = bobHeight > 0f ? Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight : 0f;
            Rect.anchoredPosition = Position + new Vector2(0f, bob);
        }

        private void ApplySize()
        {
            var center = new Vector2(0.5f, 0.5f);
            if (Rect.anchorMin != center) Rect.anchorMin = center;
            if (Rect.anchorMax != center) Rect.anchorMax = center;
            if (Rect.pivot != center) Rect.pivot = center;
            if (Rect.sizeDelta != size) Rect.sizeDelta = size;
        }

#if UNITY_EDITOR
        // 인스펙터에서 크기를 만지면 씬 뷰에 바로 반영한다.
        // OnValidate 안에서 RectTransform을 바로 건드리면 Unity가 SendMessage 경고를 낸다
        // (레이아웃 재계산이 금지된 구간). 한 프레임 미뤄서 적용한다.
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return; // 그 사이 삭제됐을 수 있다
                ApplySize();
            };
        }
#endif
    }
}
