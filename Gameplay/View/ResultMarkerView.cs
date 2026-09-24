using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 승패 표식 — 이긴 사람 머리 위의 왕관, 진 사람 자리의 비석.
    ///
    /// 결과 패널과 최종 판정 무대 두 곳에서 쓰지만 <b>일부러 같은 자리·같은 크기</b>로 띄운다.
    /// 두 단계에서 표식이 같은 모습이어야 눈이 따라가기 쉽다. 그래서 두 벌의 Image를 한
    /// 클래스가 함께 들고 같은 규칙으로 그린다.
    ///
    /// <b>왜 별도 클래스인가.</b> 표식은 그리는 규칙(비율 맞추기·원위치 기억·트윈 정리)만
    /// 180줄이었고, 그 규칙은 누가 이겼는지와 무관하다. 게임 흐름은 "A가 이겼다"만 알면
    /// 되고, 왕관을 몇 픽셀 띄울지는 알 필요가 없다.
    ///
    /// <b>여기 없는 것.</b> 판정을 한 박 미루는 예약 트윈은 <see cref="GameDirector"/>가
    /// 들고 있다. 그건 표식의 생김새가 아니라 연출의 순서라서, 이쪽에 두면 결과 패널의
    /// 진행을 이 클래스가 쥐게 된다.
    /// </summary>
    internal sealed class ResultMarkerView
    {
        // 왕관이 떠 있는 듯 위아래로 오가는 폭(px)과 편도 시간(초).
        private const float BobDistance = 9f;
        private const float BobDuration = 0.9f;

        // 비석이 위에서 제자리로 떨어지는 높이(px)와 시간(초).
        private const float DropHeight = 54f;
        private const float DropDuration = 0.58f;

        private readonly Image resultA, resultB;
        private readonly Image finalA, finalB;
        private readonly Sprite crown, tombstone;
        private readonly float height;

        // 표식의 원래 자리. 애니메이션이 anchoredPosition을 건드리므로
        // 기준점을 처음 한 번만 붙잡아 두지 않으면 라운드마다 위로 밀려 올라간다.
        private readonly Dictionary<Image, Vector2> homes = new Dictionary<Image, Vector2>();

        public ResultMarkerView(
            Image resultA, Image resultB, Image finalA, Image finalB,
            Sprite crown, Sprite tombstone, float height)
        {
            this.resultA = resultA;
            this.resultB = resultB;
            this.finalA = finalA;
            this.finalB = finalB;
            this.crown = crown;
            this.tombstone = tombstone;
            this.height = height;
        }

        /// <summary>결과 패널의 표식. 이긴 쪽에 왕관, 진 쪽에 비석.</summary>
        public void RefreshResult(bool playerAWon)
        {
            Apply(resultA, playerAWon);
            Apply(resultB, !playerAWon);
        }

        /// <summary>
        /// 최종 판정 후보에게 왕관/비석을 붙인다.
        /// <see cref="FinalWinner.None"/>이면 전부 감춘다 — 룰렛이 도는 동안에는
        /// 아직 승자가 없으므로 표식이 후보를 따라 깜빡이면 안 된다.
        ///
        /// 딜러에게는 표식을 붙이지 않는다. 무대가 좁아 머리 위에 같은 크기로 넣을 자리가
        /// 없고, 딜러가 이겼는지는 스포트라이트가 이미 말해 준다.
        /// </summary>
        public void RefreshFinal(FinalWinner winner)
        {
            ApplyFinal(finalA, winner, FinalWinner.PlayerA);
            ApplyFinal(finalB, winner, FinalWinner.PlayerB);
        }

        /// <summary>결과 패널의 표식만 내린다. 최종 판정 쪽은 건드리지 않는다.</summary>
        public void HideResult()
        {
            Hide(resultA);
            Hide(resultB);
        }

        private void ApplyFinal(Image marker, FinalWinner winner, FinalWinner slot)
        {
            if (marker == null) return;

            if (winner == FinalWinner.None)
            {
                StopMotion(marker);
                marker.gameObject.SetActive(false);
                return;
            }

            Apply(marker, winner == slot);
        }

        private void Apply(Image marker, bool isWinner)
        {
            if (marker == null) return;

            Sprite sprite = isWinner ? crown : tombstone;

            marker.gameObject.SetActive(true);
            marker.color = Color.white;      // 그림으로 구분하므로 틴트를 걸지 않는다
            marker.preserveAspect = true;
            if (sprite != null) marker.sprite = sprite;

            FitToSprite(marker, sprite, height);

            StopMotion(marker);
            if (isWinner) PlayBob(marker);
            else PlayDefeatDrop(marker);
        }

        private void Hide(Image marker)
        {
            if (marker == null) return;
            StopMotion(marker);
            marker.gameObject.SetActive(false);
        }

        // 왕관은 가로로 길고 비석은 세로로 길다. 정사각 슬롯에 그대로 넣으면 한쪽이
        // 찌그러지므로 높이를 기준으로 잡고 폭을 원본 비율대로 맞춘다.
        //
        // 크기는 sizeDelta 하나로만 정한다. 스케일까지 크기 조절에 쓰면 같은 height를
        // 줘도 화면에서 다른 크기로 보인다(실제로 결과 패널 표식만 2.4배로 커져 있었다).
        private static void FitToSprite(Image marker, Sprite sprite, float height)
        {
            if (sprite == null || sprite.rect.height <= 0f) return;

            var rect = marker.rectTransform;
            float aspect = sprite.rect.width / sprite.rect.height;
            rect.sizeDelta = new Vector2(height * aspect, height);
            if (rect.localScale != Vector3.one) rect.localScale = Vector3.one;
        }

        // 트윈을 끄고 원래 자리로 돌려놓는다. 기준점을 기억해 두지 않으면
        // 표식이 라운드마다 조금씩 위로 밀려 올라간다.
        private void StopMotion(Image marker)
        {
            RectTransform rt = marker.rectTransform;

            if (!homes.TryGetValue(marker, out Vector2 home))
            {
                home = rt.anchoredPosition;
                homes[marker] = home;
            }

            rt.DOKill();
            rt.anchoredPosition = home;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private void PlayBob(Image marker)
        {
            RectTransform rt = marker.rectTransform;
            Vector2 home = homes[marker];

            rt.DOAnchorPosY(home.y + BobDistance, BobDuration)
              .SetEase(Ease.InOutSine)
              .SetLoops(-1, LoopType.Yoyo)
              .SetUpdate(true)       // 연출 중 timeScale이 흔들려도 계속 움직이도록
              .SetLink(marker.gameObject, LinkBehaviour.PauseOnDisablePlayOnEnable);
        }

        /// <summary>패배 비석이 나타날 때 위에서 제자리로 내려오는 한 번짜리 연출.</summary>
        private void PlayDefeatDrop(Image marker)
        {
            RectTransform rt = marker.rectTransform;
            Vector2 home = homes[marker];
            rt.anchoredPosition = home + new Vector2(0f, DropHeight);
            rt.DOAnchorPos(home, DropDuration)
              .SetEase(Ease.OutCubic)
              .SetUpdate(true)
              .SetLink(marker.gameObject, LinkBehaviour.PauseOnDisablePlayOnEnable);
        }
    }
}
