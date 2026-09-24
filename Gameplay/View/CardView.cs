using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 2D UGUI 카드 표시 컴포넌트. Image에 kenney 카드 스프라이트를 적용한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CardView : MonoBehaviour
    {
        [SerializeField] private CardSpriteRegistry registry;

        // 후보 카드 배지 (선택 가능/조작 시각 구분)
        [SerializeField] private Image badgeImage;
        [SerializeField] private Outline candidateOutline;
        [SerializeField] private Color badgeFair        = new Color(0.1f, 0.75f, 0.1f, 0.85f);
        [SerializeField] private Color badgeManipulated = new Color(0.9f, 0.25f, 0.0f, 0.85f);

        [Header("나눠 주기")]
        [Tooltip("딜러가 준 카드가 위에서 내려오는 시간(초). 0이면 그 자리에 생긴다.")]
        [SerializeField, Min(0f)] private float dealDuration = 0.34f;

        [Tooltip("카드가 얼마나 위에서 내려오는지(px).")]
        [SerializeField, Min(0f)] private float dealDropHeight = 300f;

        [Header("뒤집기")]
        [Tooltip("덮여 있던 카드가 열릴 때 도는 시간(초). 0이면 곧바로 바뀐다.")]
        [SerializeField, Min(0f)] private float flipDuration = 0.36f;

        // 이 슬롯이 지금 무엇을 보여 주고 있는지. 어떤 연출을 붙일지가 전적으로
        // '무엇에서 무엇으로 바뀌는가'로 정해진다.
        //   없음 → 앞면 : 딜러가 준 카드다.   내려오고 → 뒤집는다
        //   없음 → 뒷면 : 상대에게 간 카드다. 내려오기만 한다
        //   뒷면 → 앞면 : 결과 공개다.        뒤집기만 한다
        private enum Facing { None, Back, Face }

        private Image cardImage;
        private Facing facing = Facing.None;
        private Sequence cardSeq;
        private Vector2 home;

        // 지연 초기화: 부모가 비활성인 상태에서 SetActive 되면 Awake가 아직 호출되지
        // 않았을 수 있으므로, 접근 시점에 컴포넌트를 확보한다.
        private Image CardImage => cardImage != null ? cardImage : (cardImage = GetComponent<Image>());
        private RectTransform Body => (RectTransform)transform;

        /// <summary>
        /// 지금 내려오거나 뒤집히는 중인지. 손패 배치(<see cref="AdaptiveHandLayout"/>)가
        /// 이 값을 보고 자리를 건드리지 않는다 — 매 프레임 제자리로 되돌리면 연출이 안 보인다.
        /// </summary>
        public bool IsAnimating => cardSeq != null;

        /// <summary>카드 앞면을 보여준다. 새로 온 카드는 내려오며 뒤집히고, 덮여 있던 카드는 뒤집힌다.</summary>
        public void ShowCard(BlackjackCard card)
        {
            bool wasHidden = facing == Facing.None;
            bool wasFaceDown = facing == Facing.Back;
            gameObject.SetActive(true);
            SetBadge(false, Color.clear);
            var face = registry != null ? registry.GetSprite(card) : null;

            if (wasHidden && CanAnimate)
            {
                facing = Facing.Face;
                PlayDeal(face, flipToFace: true);
                return;
            }

            if (wasFaceDown && CanAnimate)
            {
                facing = Facing.Face;
                PlayFlip(face);
                return;
            }

            facing = Facing.Face;

            // 연출 중이면 앞면을 미리 끼워 넣지 않는다. 화면을 다시 그리는 쪽은 이 카드가
            // 도는 중인지 모르므로, 여기서 막지 않으면 절반쯤 돌다가 앞면이 튀어나온다.
            if (cardSeq != null) return;
            CardImage.sprite = face;
        }

        /// <summary>카드 뒷면(덮인 상태)을 보여준다. 새로 온 카드라면 내려오기만 한다.</summary>
        public void ShowFaceDown()
        {
            bool wasHidden = facing == Facing.None;
            gameObject.SetActive(true);
            SetBadge(false, Color.clear);

            if (wasHidden && CanAnimate)
            {
                facing = Facing.Back;
                PlayDeal(null, flipToFace: false);
                return;
            }

            KillTweens();
            facing = Facing.Back;
            CardImage.sprite = BackSprite;
        }

        /// <summary>딜러 후보 카드를 보여준다. index 0 = 공정, 1+ = 조작.</summary>
        public void ShowCandidate(BlackjackCard card, int index)
        {
            gameObject.SetActive(true);
            KillTweens();
            facing = Facing.Face;
            CardImage.sprite = registry != null ? registry.GetSprite(card) : null;
            SetBadge(true, index == 0 ? badgeFair : badgeManipulated);
        }

        /// <summary>숨긴다 (패에 카드 없음).</summary>
        public void Hide()
        {
            KillTweens();
            facing = Facing.None;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 손패 배치가 정해 준 제자리. 내려오는 중이면 도착점만 갱신하고 자리는 건드리지 않는다.
        /// </summary>
        public void PlaceAt(Vector2 position)
        {
            home = position;
            if (cardSeq == null) Body.anchoredPosition = position;
        }

        // 카드가 꺼지면 트윈도 함께 끝낸다. 남겨 두면 다음 라운드에 이 슬롯이 다시
        // 켜졌을 때 옛 트윈이 폭이나 자리를 되돌리며 카드가 한 번 튄다.
        private void OnDisable() => KillTweens();

        // 비활성이면 트윈이 돌지 않으므로 연출을 걸지 않는다. 걸어 두면 카드가
        // 화면 위쪽에 멈춘 채 남는다.
        private bool CanAnimate => gameObject.activeInHierarchy && Application.isPlaying;

        private Sprite BackSprite => registry != null ? registry.CardBack : null;

        // 뒷면인 채로 위에서 제자리로 내려온 뒤, 필요하면 이어서 뒤집는다.
        // 딜러가 카드를 건네는 동작이라 두 단계가 끊기지 않고 한 시퀀스로 이어져야 한다.
        private void PlayDeal(Sprite face, bool flipToFace)
        {
            KillTweens();

            // 카드가 내려오기 시작하는 순간. 이 함수는 슬롯이 비어 있다가(Facing.None)
            // 카드가 처음 놓일 때만 불리므로, 화면을 몇 번 다시 그리든 한 장에 한 번이다.
            // 앞면으로 받는 카드라면 조금 뒤 뒤집는 소리가 이어서 난다.
            GameAudio.PlayCardDeal();

            var body = Body;
            home = body.anchoredPosition;   // 배치가 방금 정해 준 자리가 곧 도착점이다
            body.anchoredPosition = home + new Vector2(0f, dealDropHeight);
            body.localScale = Vector3.one;
            CardImage.sprite = BackSprite;

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(body.DOAnchorPos(home, dealDuration).SetEase(Ease.OutCubic));
            if (flipToFace) AppendFlip(seq, body, face);

            cardSeq = seq.OnComplete(() => Settle(body, home));
        }

        // 가로 폭을 0까지 줄였다가 앞면으로 갈아 끼우고 다시 편다.
        // 카드가 실제로 도는 것처럼 보이는 가장 단순한 방법이고, 손패 배치를 맡은
        // AdaptiveHandLayout이 위치와 크기만 건드리므로 localScale은 여기서만 쓴다.
        private void PlayFlip(Sprite face)
        {
            KillTweens();

            var body = Body;
            home = body.anchoredPosition;
            body.localScale = Vector3.one;

            var seq = DOTween.Sequence().SetUpdate(true);
            AppendFlip(seq, body, face);
            cardSeq = seq.OnComplete(() => Settle(body, home));
        }

        private void AppendFlip(Sequence seq, RectTransform body, Sprite face)
        {
            float half = flipDuration * 0.5f;
            seq.Append(body.DOScaleX(0f, half).SetEase(Ease.InQuad))
               .AppendCallback(() =>
               {
                   // 폭이 0이 되어 앞면으로 갈아 끼우는 이 순간이 곧 '앞을 보는' 순간이다.
                   // 뒤집기 시작이 아니라 여기에 두어야 소리가 얼굴이 드러나는 것과 맞물린다.
                   // 중간에 끊긴 연출은 이 콜백까지 오지 않으므로 헛소리가 나지 않는다.
                   CardImage.sprite = face;
                   GameAudio.PlayCardFlip();
               })
               .Append(body.DOScaleX(1f, half).SetEase(Ease.OutQuad));
        }

        private void Settle(RectTransform body, Vector2 position)
        {
            cardSeq = null;
            body.localScale = Vector3.one;
            body.anchoredPosition = position;
        }

        // 도중에 끊긴 연출도 카드를 제자리로 돌려놓는다. 폭만 되돌리고 자리를 두면
        // 내려오다 멈춘 카드가 슬롯 위쪽에 뜬 채로 남는다.
        private void KillTweens()
        {
            if (cardSeq != null)
            {
                var seq = cardSeq;
                cardSeq = null;    // OnComplete가 다시 들어오지 않도록 먼저 끊는다
                seq.Kill();
                Body.anchoredPosition = home;
            }
            transform.localScale = Vector3.one;
        }

        public void SetRegistry(CardSpriteRegistry reg) => registry = reg;

        private void SetBadge(bool show, Color color)
        {
            if (candidateOutline != null)
            {
                candidateOutline.enabled = show;
                if (show) candidateOutline.effectColor = color;
            }

            // The outline lives on the card, so it follows the card's tweened RectTransform.
            // Keep the legacy badge reference only for compatibility with older scenes.
            if (badgeImage == null) return;
            badgeImage.gameObject.SetActive(show && candidateOutline == null);
            if (show && candidateOutline == null) badgeImage.color = color;
        }
    }
}
