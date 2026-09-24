using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 패널을 잠깐 걷어내 뒤의 테이블을 보게 하는 토글 버튼.
    ///
    /// 딜러의 카드 배분 화면과 고발 화면은 화면을 가득 덮는다. 그런데 두 화면 모두
    /// "테이블 위에 뭐가 깔렸는지"를 보고 판단해야 하는 자리다 — 가려 놓고 고르라는 셈이다.
    /// 이 버튼은 자기 자신만 남기고 같은 패널의 나머지를 잠시 가린다.
    ///
    /// 무엇을 가릴지 목록으로 받지 않고 <b>같은 부모의 나머지 형제 전부</b>로 정한다.
    /// 패널에 요소가 하나 늘 때마다 배선을 고쳐야 하는 구조면, 언젠가 하나가 빠진 채
    /// 화면에 남는다.
    ///
    /// <b>끄지 않고 투명하게 만든다.</b> 예전에는 <c>SetActive(false)</c>로 껐는데,
    /// 화면을 다시 그리는 쪽이 같은 오브젝트를 <c>SetActive(true)</c>로 되켜면서 싸웠다
    /// (딜러 화면의 <see cref="DealerCardArranger"/>는 갱신마다 후보 카드를 켠다).
    /// 그 결과 스크립트가 관리하는 것만 되살아나고 장식은 꺼진 채 남아, 카드만 떠 있는
    /// 화면이 됐다. <see cref="CanvasGroup"/>의 알파는 아무도 건드리지 않으므로
    /// 누가 언제 켜든 걷어낸 상태가 그대로 유지된다.
    ///
    /// <b>시계는 건드리지 않는다.</b> 패널 루트는 계속 켜 둔 채 자식만 가리므로,
    /// 핫시트의 <c>IsShowing(panel)</c>도 그대로 참이다. 숨겨 놓고 제한시간을 버는
    /// 길이 생기면 안 된다.
    /// </summary>
    public class PanelPeekToggle : MonoBehaviour
    {
        [Header("배선")]
        [SerializeField] private Button button;

        [Tooltip("눌린 상태를 테두리 색으로 알리는 버튼 본체 그래픽.")]
        [SerializeField] private RoundedPanelGraphic frame;

        [Tooltip("가운데 카드 아이콘 조각들. 색만 바뀐다.")]
        [SerializeField] private Graphic[] iconParts;

        [Header("색")]
        // 기본값은 PeekButtonSetupTool이 프리팹에 박아 주는 값과 같다. 두 곳이 갈라지면
        // 게임을 켜는 순간(Awake의 ApplyVisualState) 버튼 색이 한 번 튄다.
        [SerializeField] private Color idleBorder = new Color(0.788f, 0.867f, 0.890f, 1f);
        [SerializeField] private Color peekBorder = new Color(0.918f, 0.976f, 1f, 1f);
        [SerializeField] private Color idleIcon = new Color(0.863f, 0.953f, 0.976f, 1f);
        [SerializeField] private Color peekIcon = Color.white;
        [SerializeField, Min(0f)] private float borderThickness = 2.5f;

        /// <summary>지금 패널을 걷어낸 상태인지.</summary>
        public bool IsPeeking { get; private set; }

        /// <summary>
        /// 가려 둔 형제 하나와 그 전의 상태. 알파를 1로 되돌리지 않고 <b>원래 값</b>으로
        /// 되돌리는 이유: 반투명하게 설계된 요소를 걷어냈다 되돌릴 때 불투명해지면 안 된다.
        /// </summary>
        private struct Veiled
        {
            public CanvasGroup Group;
            public float Alpha;
            public bool Blocks;
            public bool Interactable;
        }

        private readonly List<Veiled> veiled = new List<Veiled>();

        // 패널 루트의 딤 이미지. 이것도 꺼야 화면이 실제로 걷힌다.
        // Awake에서 잡지 않고 쓸 때 찾는다 — 에디터에서 부품을 옮겨 붙이거나
        // Awake가 돌지 않은 상태로 불려도 같은 결과가 나와야 한다.
        private Graphic Backdrop =>
            backdrop != null ? backdrop
                : backdrop = transform.parent != null ? transform.parent.GetComponent<Graphic>() : null;
        private Graphic backdrop;
        private bool backdropWasEnabled;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (frame == null) frame = GetComponent<RoundedPanelGraphic>();

            if (button != null) button.onClick.AddListener(Toggle);
            ApplyVisualState();
        }

        // 걷어낸 채로 패널이 꺼지면 다음에 이 패널이 다시 떴을 때 내용이 사라진 화면이 된다 —
        // 알파는 부모가 꺼져도 그대로 남기 때문이다.
        //
        // 내려갈 때와 올라올 때 모두 되돌린다. 한쪽만 두면 그 콜백이 불리지 않는 경로
        // (에디터, 도메인 리로드 직후)에서 반쯤 지워진 패널이 그대로 올라온다.
        // 이미 되돌렸으면 Restore는 즉시 빠져나오므로 두 번 불려도 값이 없다.
        private void OnDisable() => Restore();
        private void OnEnable() => Restore();

        /// <summary>버튼이 부르는 진입점. 걷어냈으면 되돌리고, 아니면 걷어낸다.</summary>
        public void Toggle()
        {
            // 클릭음은 여기서 낸다. 이 버튼은 디렉터가 아니라 자기 자신에게 연결돼 있으므로
            // (Awake의 button.onClick), 핫시트든 네트워크든 이 한 곳이면 둘 다 소리가 난다.
            GameAudio.PlayClick();

            if (IsPeeking) Restore();
            else Hide();
        }

        private void Hide()
        {
            if (IsPeeking) return;

            var parent = transform.parent;
            if (parent == null) return;

            veiled.Clear();
            foreach (Transform child in parent)
            {
                if (child == transform) continue;

                // 꺼져 있는 형제도 함께 가린다. 걷어낸 사이에 화면 갱신이 그것을 켤 수
                // 있는데, 그때 알파가 1이면 걷어낸 화면 위로 튀어나온다.
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();

                veiled.Add(new Veiled
                {
                    Group = group,
                    Alpha = group.alpha,
                    Blocks = group.blocksRaycasts,
                    Interactable = group.interactable
                });

                group.alpha = 0f;
                // 보이지 않는 버튼이 눌리면 안 된다. 걷어낸 동안은 보기만 하는 상태다.
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            var dim = Backdrop;
            if (dim != null)
            {
                backdropWasEnabled = dim.enabled;
                dim.enabled = false;
            }

            IsPeeking = true;
            ApplyVisualState();
        }

        private void Restore()
        {
            if (!IsPeeking) return;

            foreach (var item in veiled)
            {
                if (item.Group == null) continue;
                item.Group.alpha = item.Alpha;
                item.Group.blocksRaycasts = item.Blocks;
                item.Group.interactable = item.Interactable;
            }
            veiled.Clear();

            var dim = Backdrop;
            if (dim != null) dim.enabled = backdropWasEnabled;

            IsPeeking = false;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            frame?.SetBorder(IsPeeking ? peekBorder : idleBorder, borderThickness);

            if (iconParts == null) return;
            Color iconColor = IsPeeking ? peekIcon : idleIcon;
            foreach (var part in iconParts)
                if (part != null) part.color = iconColor;
        }
    }
}
