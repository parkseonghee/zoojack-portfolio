using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 놀이법 설명서. 펼친 책 모양이고 좌우 화살표로 장을 넘긴다.
    ///
    /// <b>쪽은 값이고 종이는 여섯 장뿐이다.</b> 내용은 <see cref="pages"/>에 줄지어 있고
    /// (두 쪽이 한 펼침면), 화면에는 펼쳐 놓은 두 장과 넘어가는 장의 앞뒤 네 장만 있다.
    /// 넘길 때마다 그 여섯 장에 내용을 갈아 끼운다 — 쪽마다 오브젝트를 두면 쪽을 늘릴
    /// 때마다 씬을 손봐야 한다.
    ///
    /// <b>쪽은 그림으로 설명한다.</b> 한 쪽은 제목과 긴 글이 아니라 덩어리 여러 개이고,
    /// 그중 본체는 실제 카드 그림이 늘어선 줄이다(<see cref="HowToPlayBlockKind"/>).
    ///
    /// <b>넘어가는 모양.</b> 이 게임의 Canvas는 Screen Space - Overlay라 원근이 없다.
    /// 그래서 책등을 축으로 Y 회전을 주면 종이가 <i>가로로 눌리며</i> 넘어간다 —
    /// 90도에서 폭이 0이 되고, 그 뒤로는 뒷면이 보인다. 눌리기만 하면 종이 같지 않아
    /// 세워질수록 짙어지는 그늘을 함께 얹는다(<see cref="HowToPlayPageView.SetShade"/>).
    ///
    /// 배선은 <c>ZooJack/게임/설명서 만들기</c> 메뉴가 해 준다.
    /// </summary>
    public class HowToPlayPanel : MonoBehaviour
    {
        [Header("열고 닫기")]
        [Tooltip("책을 담은 오브젝트. 여는 버튼은 이 바깥에 있어야 다시 열 수 있다.")]
        [SerializeField] private GameObject window;

        [SerializeField] private Button scrim;
        [SerializeField] private Button btnOpen;
        [SerializeField] private Button btnClose;

        [Header("넘기기")]
        [SerializeField] private Button btnPrev;
        [SerializeField] private Button btnNext;

        [Header("펼쳐 놓은 두 장")]
        [SerializeField] private HowToPlayPageView leftPage;
        [SerializeField] private HowToPlayPageView rightPage;

        [Header("넘어가는 장")]
        [Tooltip("앞으로 넘길 때 도는 장. 책등을 축(pivot x=0)으로 오른쪽에 서 있다.")]
        [SerializeField] private RectTransform flipForward;

        [SerializeField] private HowToPlayPageView flipForwardFront;
        [SerializeField] private HowToPlayPageView flipForwardBack;

        [Tooltip("뒤로 넘길 때 도는 장. 책등을 축(pivot x=1)으로 왼쪽에 서 있다.")]
        [SerializeField] private RectTransform flipBackward;

        [SerializeField] private HowToPlayPageView flipBackwardFront;
        [SerializeField] private HowToPlayPageView flipBackwardBack;

        [Header("책배")]
        [Tooltip("아직 넘기지 않은 장을 옆에서 본 선. 남은 장 수만큼만 켜진다.")]
        [SerializeField] private RectTransform leftEdges;

        [SerializeField] private RectTransform rightEdges;

        [Header("쪽수")]
        [SerializeField] private TextMeshProUGUI spreadCounter;

        [Header("내용")]
        [Tooltip("두 쪽이 한 펼침면이다. 왼쪽 쪽부터 차례로 넣는다.")]
        [SerializeField] private HowToPlayPage[] pages = new HowToPlayPage[8];

        [Tooltip("카드 그림. 게임이 쓰는 것과 같은 묶음이라 카드가 바뀌면 설명서도 따라간다.")]
        [SerializeField] private CardSpriteRegistry cardSprites;

        [Header("연출")]
        [Range(0.2f, 1.5f)]
        [Tooltip("한 장이 넘어가는 데 걸리는 시간(초).")]
        [SerializeField] private float flipSeconds = 0.55f;

        [Range(0f, 1f)]
        [Tooltip("종이가 가장 세워졌을 때 지는 그늘의 짙기.")]
        [SerializeField] private float maxShade = 0.42f;

        /// <summary>지금 펼쳐 놓은 면(0부터). 창을 닫고 다시 열어도 보던 자리를 지킨다.</summary>
        private int spread;

        private Tween flip;

        // 책배 선이 처음 만들어졌을 때의 길이. 오른쪽은 넘길 때마다 길이를 바꿔 다는데,
        // 한 번 덮어쓰면 원래 값이 사라지므로 시작할 때 따로 적어 둔다.
        private float[] leftEdgeHeights;
        private float[] rightEdgeHeights;

        /// <summary>넘어가는 중. 이때 또 누르면 두 장이 겹쳐 돌아 앞뒤가 어긋난다.</summary>
        private bool Turning => flip != null && flip.IsActive() && flip.IsPlaying();

        public bool IsOpen => window != null && window.activeSelf;

        private int SpreadCount => pages == null || pages.Length == 0
            ? 1
            : (pages.Length + 1) / 2;

        private void Awake()
        {
            if (btnOpen != null) btnOpen.onClick.AddListener(Open);
            if (btnClose != null) btnClose.onClick.AddListener(Close);
            if (scrim != null) scrim.onClick.AddListener(Close);

            if (btnPrev != null) btnPrev.onClick.AddListener(() => Turn(false));
            if (btnNext != null) btnNext.onClick.AddListener(() => Turn(true));

            // 넘어가는 장은 넘기는 동안에만 있다. 켜 둔 채로 두면 펼친 장 위에
            // 지난 쪽이 한 장 덮여 있다.
            if (flipForward != null) flipForward.gameObject.SetActive(false);
            if (flipBackward != null) flipBackward.gameObject.SetActive(false);

            leftEdgeHeights = CaptureHeights(leftEdges);
            rightEdgeHeights = CaptureHeights(rightEdges);

            if (window != null) window.SetActive(false);
        }

        private void OnDestroy() => flip?.Kill();

        private static float[] CaptureHeights(RectTransform stack)
        {
            if (stack == null) return null;

            var heights = new float[stack.childCount];
            for (int i = 0; i < heights.Length; i++)
                heights[i] = ((RectTransform)stack.GetChild(i)).sizeDelta.y;
            return heights;
        }

        // ── 열고 닫기 ────────────────────────────────────────────────

        public void Open()
        {
            GameAudio.PlayClick();

            // 넘기던 도중에 닫았을 수 있다. 그대로 다시 열면 종이가 세워진 채 멈춰 있다.
            StopFlip();
            DrawSpread();

            if (window != null) window.SetActive(true);
            transform.SetAsLastSibling();   // 어느 패널이 떠 있든 그 위에
        }

        public void Close()
        {
            GameAudio.PlayClick();
            StopFlip();
            if (window != null) window.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        // ── 넘기기 ───────────────────────────────────────────────────

        /// <summary>한 장 넘긴다. 끝에 닿았거나 이미 넘어가는 중이면 아무 일도 하지 않는다.</summary>
        public void Turn(bool forward)
        {
            if (Turning) return;

            int target = spread + (forward ? 1 : -1);
            if (target < 0 || target >= SpreadCount) return;

            GameAudio.PlayPageTurn();

            RectTransform root;
            HowToPlayPageView front, back;
            float to;

            if (forward)
            {
                // 도는 장의 앞면은 지금 오른쪽 쪽, 뒷면은 넘긴 뒤의 왼쪽 쪽이다.
                root = flipForward;
                front = flipForwardFront;
                back = flipForwardBack;
                to = 180f;

                Fill(front, spread * 2 + 1);
                Fill(back, target * 2);

                // 도는 장이 들리면서 그 아래가 드러난다. 드러나는 것은 넘긴 뒤의
                // 오른쪽 쪽이므로 지금 갈아 둔다. 왼쪽은 도는 장이 덮으러 오는 중이라
                // 아직 그대로 둔다.
                Fill(rightPage, target * 2 + 1);
            }
            else
            {
                root = flipBackward;
                front = flipBackwardFront;
                back = flipBackwardBack;
                to = -180f;

                Fill(front, spread * 2);
                Fill(back, target * 2 + 1);
                Fill(leftPage, target * 2);
            }

            if (root == null) { spread = target; DrawSpread(); return; }

            root.gameObject.SetActive(true);
            ApplyFlip(root, front, back, 0f);

            float angle = 0f;
            flip = DOTween.To(() => angle, v =>
                {
                    angle = v;
                    ApplyFlip(root, front, back, v);
                }, to, flipSeconds)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true)   // 어딘가에서 시간이 멈춰 있어도 넘어가야 한다
                .OnComplete(() =>
                {
                    spread = target;
                    root.gameObject.SetActive(false);
                    DrawSpread();
                });
        }

        /// <summary>
        /// 도는 장을 <paramref name="angle"/>도만큼 세운다.
        /// 90도를 넘으면 뒷면이 보이므로 앞뒤를 바꿔 단다.
        /// </summary>
        private void ApplyFlip(
            RectTransform root, HowToPlayPageView front, HowToPlayPageView back, float angle)
        {
            root.localEulerAngles = new Vector3(0f, angle, 0f);

            float turned = Mathf.Abs(angle) / 180f;   // 0(눕힘) ~ 1(다 넘어감)
            bool showBack = turned > 0.5f;

            if (front != null) front.gameObject.SetActive(!showBack);
            if (back != null) back.gameObject.SetActive(showBack);

            // 90도에서 가장 짙다. 눕힌 상태와 다 넘어간 상태에서는 그늘이 없어야
            // 아래에 깔린 종이와 색이 이어진다.
            float shade = Mathf.Sin(turned * Mathf.PI) * maxShade;
            if (front != null) front.SetShade(shade);
            if (back != null) back.SetShade(shade);
        }

        private void StopFlip()
        {
            flip?.Kill();
            flip = null;
            if (flipForward != null) flipForward.gameObject.SetActive(false);
            if (flipBackward != null) flipBackward.gameObject.SetActive(false);
        }

        // ── 그리기 ───────────────────────────────────────────────────

        private void DrawSpread()
        {
            spread = Mathf.Clamp(spread, 0, SpreadCount - 1);

            Fill(leftPage, spread * 2);
            Fill(rightPage, spread * 2 + 1);

            // 끝에 닿으면 화살표를 지운다. 눌러도 아무 일이 없는 버튼을 남겨 두면
            // 눌리지 않는 것인지 고장인지 구별할 수 없다.
            if (btnPrev != null) btnPrev.gameObject.SetActive(spread > 0);
            if (btnNext != null) btnNext.gameObject.SetActive(spread < SpreadCount - 1);

            // 왼쪽은 넘긴 장, 오른쪽은 남은 장. 둘 다 펼친 쪽에 붙고, 오른쪽만
            // 넘길 때마다 길이가 한 칸씩 당겨진다(DrawEdges 참고).
            DrawEdges(leftEdges, leftEdgeHeights, spread, shiftLengths: false);
            DrawEdges(rightEdges, rightEdgeHeights, SpreadCount - 1 - spread, shiftLengths: true);

            if (spreadCounter != null)
                spreadCounter.text = (spread + 1) + " / " + SpreadCount;
        }

        /// <summary>
        /// 책배의 선을 <paramref name="remaining"/>장만큼 켠다. 선보다 장이 많으면
        /// 다 켜 둔다 — 정확한 수는 쪽수가 말하고, 여기는 "아직 남았다"만 보이면 된다.
        ///
        /// <b>자리는 언제나 펼친 쪽에 붙는다.</b> 자식 0번이 종이 바로 옆이고 뒤로 갈수록
        /// 겉장 쪽이다. 켜는 것은 늘 0번부터라, 무더기가 종이에서 떨어져 뜨는 일이 없다 —
        /// 떨어지면 책 한 권이 아니라 종이 옆에 막대가 몇 개 놓인 것처럼 보인다.
        ///
        /// <b>길이는 오른쪽만 당겨진다</b>(<paramref name="shiftLengths"/>).
        /// 선은 안쪽일수록 길게 만들어 두었으므로, 남은 장이 줄면 길이 목록을 한 칸씩
        /// 밀어 읽는다. 그러면 <b>가장 긴 선(=다음에 넘어갈 장)이 사라지고 그다음 선이
        /// 앞으로 당겨 온다.</b> 자리만 끄면 종이에서 먼 쪽이 사라져, 넘어간 것이 맨 앞
        /// 장이 아니라 맨 뒤 장으로 보인다.
        ///
        /// 왼쪽(넘긴 장)은 쌓이기만 하므로 길이를 그대로 둔다 — 맨 앞이 늘 가장 길고,
        /// 뒤로 넘기면 겉장 쪽 끝이 하나 사라진다.
        /// </summary>
        private static void DrawEdges(
            RectTransform stack, float[] heights, int remaining, bool shiftLengths)
        {
            if (stack == null || heights == null) return;

            int lit = Mathf.Clamp(remaining, 0, stack.childCount);
            int offset = shiftLengths ? stack.childCount - lit : 0;

            for (int i = 0; i < stack.childCount; i++)
            {
                var line = (RectTransform)stack.GetChild(i);
                bool on = i < lit;
                line.gameObject.SetActive(on);
                if (!on) continue;

                var size = line.sizeDelta;
                size.y = heights[Mathf.Min(i + offset, heights.Length - 1)];
                line.sizeDelta = size;
            }
        }

        /// <summary>
        /// <paramref name="index"/>번째 쪽(0부터)을 <paramref name="view"/>에 그린다.
        /// 없는 쪽이면 쪽수 없는 빈 종이가 된다 — 쪽 수가 홀수면 마지막 오른쪽이 그렇다.
        /// </summary>
        private void Fill(HowToPlayPageView view, int index)
        {
            if (view == null) return;
            bool exists = pages != null && index >= 0 && index < pages.Length;
            view.Apply(exists ? pages[index] : null, exists ? index + 1 : 0, cardSprites);
        }
    }
}
